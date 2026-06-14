using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using VRCFaceTracking.Core.Params.Expressions;

namespace VRCFaceTracking.Core.Params.Data.Mutation;

public sealed class EmotionOscOutput : TrackingMutation
{
    private bool _isActive;

    public override string Name => "Facial Emotion OSC Output";

    public override string Description => "Outputs optional calibration-aware facial emotion helper parameters for VRChat OSC.";

    public override MutationPriority Step => MutationPriority.Postprocessor;

    public override int Order => 1000;

    public override bool IsSaved => true;

    public override bool IsActive
    {
        get => _isActive;
        set
        {
            _isActive = value;
            if (!value)
            {
                EmotionOscRuntime.SetDisabled();
            }

            RefreshCalibrationComponents();
        }
    }

    [MutationProperty("Joy Activation Threshold", true, 0f, 1f)]
    public float joyActivationThreshold = 0.25f;

    [MutationProperty("Angry Activation Threshold", true, 0f, 1f)]
    public float angryActivationThreshold = 0.25f;

    [MutationProperty("Sad Activation Threshold", true, 0f, 1f)]
    public float sadActivationThreshold = 0.25f;

    [MutationProperty("Surprise Activation Threshold", true, 0f, 1f)]
    public float surpriseActivationThreshold = 0.25f;

    [MutationProperty("Debug Logging", true)]
    public bool debugLogging = false;

    public EmotionCalibrationData Calibration { get; set; } = new();

    private readonly List<MutationStatus> _calibrationStatusComponents = new();

    private readonly List<MutationStatusAction> _calibrationActionComponents = new();

    public override void Initialize(UnifiedTrackingData data)
    {
        Calibration ??= new EmotionCalibrationData();
        Calibration.EnsureDefaults();
        EmotionOscRuntime.Register(this);
        RefreshCalibrationComponents();

        if (!IsActive)
        {
            EmotionOscRuntime.SetDisabled();
        }
    }

    public override void CreateProperties()
    {
        _calibrationStatusComponents.Clear();
        _calibrationActionComponents.Clear();

        var settingComponents = MutationComponentFactory.CreateComponents(this);
        var components = new ObservableCollection<IMutationComponent>();

        components.Add(new MutationInfo(
            "Make the target expression first, then press its Calibrate button and hold the expression for 1 second. This helper stays disabled until Neutral, Joy, Angry, Sad, and Surprise are all calibrated."));
        AddStatus(components, "Calibration", () => IsCalibrationReady() ? "Ready" : "Required");
        AddStatus(components, "Output", GetOutputStatus);
        AddCalibrationAction(components, "Neutral", FaceEmotion.Neutral);
        AddCalibrationAction(components, "Joy", FaceEmotion.Joy);
        AddCalibrationAction(components, "Angry", FaceEmotion.Angry);
        AddCalibrationAction(components, "Sad", FaceEmotion.Sad);
        AddCalibrationAction(components, "Surprise", FaceEmotion.Surprise);
        components.Add(new MutationAction("Reset", ResetEmotionCalibration, "Reset"));

        foreach (var component in settingComponents)
        {
            components.Add(component);
        }

        Components = components;
        RefreshCalibrationComponents();
    }

    public override void MutateData(ref UnifiedTrackingData data)
    {
        if (!IsActive)
        {
            EmotionOscRuntime.SetDisabled();
            return;
        }

        EmotionOscRuntime.Update(data, this);
    }

    private void ResetEmotionCalibration()
    {
        EmotionOscRuntime.Register(this);
        EmotionOscRuntime.ResetCalibration();
        _ = SaveSettingsAsync();
    }

    private void BeginCalibration(FaceEmotion emotion)
    {
        EmotionOscRuntime.Register(this);

        if (!IsActive)
        {
            Logger?.LogWarning("FER calibration requested while Facial Emotion OSC Output is disabled.");
            return;
        }

        EmotionOscRuntime.BeginCalibration(emotion);
    }

    public void RefreshCalibrationComponents()
    {
        foreach (var status in _calibrationStatusComponents)
        {
            status.Refresh();
        }

        foreach (var action in _calibrationActionComponents)
        {
            action.Refresh();
        }
    }

    private void AddStatus(ObservableCollection<IMutationComponent> components, string name, Func<string> getValue)
    {
        var status = new MutationStatus(name, getValue);
        _calibrationStatusComponents.Add(status);
        components.Add(status);
    }

    private void AddCalibrationAction(
        ObservableCollection<IMutationComponent> components,
        string name,
        FaceEmotion emotion)
    {
        var action = new MutationStatusAction(
            name,
            () => IsEmotionCalibrated(emotion) ? "Calibrated" : "Not calibrated",
            () => IsEmotionCalibrated(emotion) ? "Recalibrate" : "Calibrate",
            () => CanCalibrateEmotion(emotion),
            () => BeginCalibration(emotion));

        _calibrationActionComponents.Add(action);
        components.Add(action);
    }

    private bool IsCalibrationReady()
    {
        return (Calibration ?? new EmotionCalibrationData()).IsReadyForEmotionOsc();
    }

    private string GetOutputStatus()
    {
        if (!IsActive)
        {
            return "Disabled";
        }

        return IsCalibrationReady() ? "Enabled" : "Waiting for calibration";
    }

    private bool IsEmotionCalibrated(FaceEmotion emotion)
    {
        var calibration = Calibration ?? new EmotionCalibrationData();
        return emotion == FaceEmotion.Neutral
            ? calibration.HasNeutralBaseline
            : calibration.HasReference(emotion);
    }

    private bool CanCalibrateEmotion(FaceEmotion emotion)
    {
        return IsActive;
    }

    public void RequestSave()
    {
        _ = SaveSettingsAsync();
    }

    private async Task SaveSettingsAsync()
    {
        if (LocalSettingsService == null)
        {
            return;
        }

        try
        {
            await LocalSettingsService.SaveSettingAsync(Name, this, true);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Failed to save FER emotion OSC settings.");
        }
    }
}
