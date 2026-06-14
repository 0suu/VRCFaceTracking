using System.Diagnostics;
using Microsoft.Extensions.Logging;
using VRCFaceTracking.Core.Params.Data;
using VRCFaceTracking.Core.Params.Data.Mutation;

namespace VRCFaceTracking.Core.Params.Expressions;

public readonly record struct EmotionRuntimeSnapshot(bool Enabled, EmotionResult CurrentResult);

public static class EmotionOscRuntime
{
    private const float CalibrationDurationSeconds = 1f;
    private const float HoldTimeSeconds = 0.12f;
    private const float SwitchMargin = 0.08f;

    private static readonly object Lock = new();

    private static EmotionOscOutput? _owner;
    private static bool _enabled;
    private static bool _saveRequested;
    private static EmotionResult _currentResult = new(FaceEmotion.Neutral, 0, 0f);
    private static FaceEmotion _candidateEmotion = FaceEmotion.Neutral;
    private static long _candidateSinceTimestamp;
    private static CalibrationSession? _calibrationSession;

    public static void Register(EmotionOscOutput owner)
    {
        lock (Lock)
        {
            _owner = owner;
        }
    }

    public static void SetDisabled()
    {
        lock (Lock)
        {
            SetDisabledUnderLock();
            _calibrationSession = null;
        }
    }

    public static EmotionRuntimeSnapshot GetSnapshot()
    {
        lock (Lock)
        {
            var ownerEnabled = _owner?.IsActive == true;
            var mutatorEnabled = global::VRCFaceTracking.UnifiedTracking.Mutator?.Enabled == true;
            if (!_enabled || !ownerEnabled || !mutatorEnabled)
            {
                return new EmotionRuntimeSnapshot(false, new EmotionResult(FaceEmotion.Neutral, 0, 0f));
            }

            return new EmotionRuntimeSnapshot(_enabled, _currentResult);
        }
    }

    public static void Update(UnifiedTrackingData data, EmotionOscOutput owner)
    {
        EmotionOscOutput? ownerToSave = null;

        lock (Lock)
        {
            _owner = owner;
            if (!owner.IsActive)
            {
                SetDisabledUnderLock();
                _calibrationSession = null;
                if (ConsumeSaveRequestedUnderLock())
                {
                    ownerToSave = owner;
                }
            }
            else
            {
                var calibration = owner.Calibration;
                var rawFeatures = EmotionFeatureExtractor.Extract(data);

                ProcessCalibrationUnderLock(rawFeatures);

                if (!calibration.IsReadyForEmotionOsc())
                {
                    SetDisabledUnderLock();
                    if (ConsumeSaveRequestedUnderLock())
                    {
                        ownerToSave = owner;
                    }
                }
                else
                {
                    _enabled = true;
                    var scores = EmotionClassifier.GetReferenceScores(rawFeatures, calibration);

                    var candidate = EmotionClassifier.Classify(
                        scores,
                        owner.joyActivationThreshold,
                        owner.angryActivationThreshold,
                        owner.sadActivationThreshold,
                        owner.surpriseActivationThreshold);
                    _currentResult = ApplySwitchRulesUnderLock(candidate, scores);

                    if (ConsumeSaveRequestedUnderLock())
                    {
                        ownerToSave = owner;
                    }
                }
            }
        }

        ownerToSave?.RequestSave();
    }

    public static void BeginCalibration(FaceEmotion emotion)
    {
        lock (Lock)
        {
            if (_owner == null)
            {
                return;
            }

            _owner.Calibration.EnsureDefaults();
            _calibrationSession = new CalibrationSession(emotion, CalibrationDurationSeconds);
            _owner.Logger?.LogInformation("Starting FER calibration: {emotion}", emotion);
        }
    }

    public static void ResetCalibration()
    {
        lock (Lock)
        {
            _owner?.Calibration.Reset();
            _calibrationSession = null;
            SetDisabledUnderLock();
            _owner?.RefreshCalibrationComponents();
            _owner?.Logger?.LogInformation("Reset FER calibration");
        }
    }

    private static void SetDisabledUnderLock()
    {
        _enabled = false;
        _candidateEmotion = FaceEmotion.Neutral;
        _candidateSinceTimestamp = 0;
        _currentResult = new EmotionResult(FaceEmotion.Neutral, 0, 0f);
    }

    private static EmotionResult ApplySwitchRulesUnderLock(EmotionResult candidate, EmotionRawScores scores)
    {
        if (_owner == null)
        {
            return new EmotionResult(FaceEmotion.Neutral, 0, 0f);
        }

        if (candidate.Emotion == _currentResult.Emotion)
        {
            _candidateEmotion = candidate.Emotion;
            _candidateSinceTimestamp = Stopwatch.GetTimestamp();
            return candidate;
        }

        if (!CanSwitchUnderLock(candidate.Emotion, scores))
        {
            return BuildResultForEmotion(_currentResult.Emotion, scores);
        }

        var now = Stopwatch.GetTimestamp();
        if (_candidateEmotion != candidate.Emotion)
        {
            _candidateEmotion = candidate.Emotion;
            _candidateSinceTimestamp = now;
            return BuildResultForEmotion(_currentResult.Emotion, scores);
        }

        var elapsedSeconds = (now - _candidateSinceTimestamp) / (double)Stopwatch.Frequency;
        if (elapsedSeconds < HoldTimeSeconds)
        {
            return BuildResultForEmotion(_currentResult.Emotion, scores);
        }

        if (_owner.debugLogging)
        {
            _owner.Logger?.LogDebug("FER classified {emotion} with power {power}", candidate.Emotion, candidate.Power);
        }

        return candidate;
    }

    private static bool CanSwitchUnderLock(FaceEmotion candidateEmotion, EmotionRawScores scores)
    {
        if (_owner == null || candidateEmotion == FaceEmotion.Neutral || _currentResult.Emotion == FaceEmotion.Neutral)
        {
            return true;
        }

        var candidateScore = EmotionClassifier.GetScoreForEmotion(scores, candidateEmotion);
        var currentScore = EmotionClassifier.GetScoreForEmotion(scores, _currentResult.Emotion);

        return candidateScore >= currentScore + SwitchMargin;
    }

    private static EmotionResult BuildResultForEmotion(FaceEmotion emotion, EmotionRawScores scores)
    {
        var score = EmotionClassifier.GetScoreForEmotion(scores, emotion);
        return new EmotionResult(emotion, (int)emotion, Math.Clamp(score, 0f, 1f));
    }

    private static void ProcessCalibrationUnderLock(EmotionFeatureVector rawFeatures)
    {
        if (_owner == null || _calibrationSession == null)
        {
            return;
        }

        _calibrationSession.FeatureSum.Add(rawFeatures);
        _calibrationSession.SampleCount++;

        var elapsedSeconds = (Stopwatch.GetTimestamp() - _calibrationSession.StartTimestamp) / (double)Stopwatch.Frequency;
        if (elapsedSeconds < _calibrationSession.DurationSeconds)
        {
            return;
        }

        CompleteCalibrationUnderLock(_calibrationSession);
        _calibrationSession = null;
        _saveRequested = true;
    }

    private static void CompleteCalibrationUnderLock(CalibrationSession session)
    {
        if (_owner == null)
        {
            return;
        }

        if (session.SampleCount <= 0)
        {
            _owner.Logger?.LogWarning("FER calibration completed without samples: {emotion}", session.Emotion);
            return;
        }

        if (session.Emotion == FaceEmotion.Neutral)
        {
            _owner.Calibration.NeutralBaseline = session.FeatureSum.Divide(session.SampleCount);
            _owner.Calibration.HasNeutralBaseline = true;
            _owner.RefreshCalibrationComponents();
            _owner.Logger?.LogInformation("Completed FER calibration: Neutral");
            return;
        }

        var reference = session.FeatureSum.Divide(session.SampleCount);
        _owner.Calibration.SetRawReference(session.Emotion, reference, session.SampleCount);
        _owner.RefreshCalibrationComponents();
        _owner.Logger?.LogInformation(
            "Completed FER calibration: {emotion}, reference samples = {samples}",
            session.Emotion,
            session.SampleCount);
    }

    private static bool ConsumeSaveRequestedUnderLock()
    {
        if (!_saveRequested)
        {
            return false;
        }

        _saveRequested = false;
        return true;
    }

    private sealed class CalibrationSession
    {
        public CalibrationSession(FaceEmotion emotion, float durationSeconds)
        {
            Emotion = emotion;
            DurationSeconds = durationSeconds;
            StartTimestamp = Stopwatch.GetTimestamp();
        }

        public FaceEmotion Emotion { get; }

        public float DurationSeconds { get; }

        public long StartTimestamp { get; }

        public int SampleCount { get; set; }

        public EmotionFeatureVector FeatureSum;
    }
}
