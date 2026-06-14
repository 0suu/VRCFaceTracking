namespace VRCFaceTracking.Core.Params.Expressions;

public readonly record struct EmotionResult(FaceEmotion Emotion, int Index, float Power);

public readonly record struct EmotionRawScores(float Joy, float Angry, float Sad, float Surprise);

public static class EmotionClassifier
{
    private const float ReferenceMinNorm = 0.001f;

    public static EmotionRawScores GetReferenceScores(EmotionFeatureVector rawFeatures, EmotionCalibrationData calibration)
    {
        var features = calibration.HasNeutralBaseline
            ? rawFeatures.SubtractBaseline(calibration.NeutralBaseline)
            : rawFeatures;

        return new EmotionRawScores(
            GetReferenceScore(features, GetReferenceDelta(calibration, FaceEmotion.Joy)),
            GetReferenceScore(features, GetReferenceDelta(calibration, FaceEmotion.Angry)),
            GetReferenceScore(features, GetReferenceDelta(calibration, FaceEmotion.Sad)),
            GetReferenceScore(features, GetReferenceDelta(calibration, FaceEmotion.Surprise)));
    }

    public static EmotionResult Classify(
        EmotionRawScores scores,
        float joyActivationThreshold,
        float angryActivationThreshold,
        float sadActivationThreshold,
        float surpriseActivationThreshold)
    {
        var emotion = FaceEmotion.Joy;
        var maxScore = scores.Joy;
        var activationThreshold = joyActivationThreshold;

        if (scores.Angry > maxScore)
        {
            emotion = FaceEmotion.Angry;
            maxScore = scores.Angry;
            activationThreshold = angryActivationThreshold;
        }

        if (scores.Sad > maxScore)
        {
            emotion = FaceEmotion.Sad;
            maxScore = scores.Sad;
            activationThreshold = sadActivationThreshold;
        }

        if (scores.Surprise > maxScore)
        {
            emotion = FaceEmotion.Surprise;
            maxScore = scores.Surprise;
            activationThreshold = surpriseActivationThreshold;
        }

        if (maxScore < Math.Clamp(activationThreshold, 0f, 1f))
        {
            var neutralScore = Math.Clamp(1f - maxScore, 0f, 1f);
            return new EmotionResult(FaceEmotion.Neutral, (int)FaceEmotion.Neutral, neutralScore);
        }

        return new EmotionResult(emotion, (int)emotion, Math.Clamp(maxScore, 0f, 1f));
    }

    public static float GetScoreForEmotion(EmotionRawScores scores, FaceEmotion emotion)
    {
        return emotion switch
        {
            FaceEmotion.Joy => scores.Joy,
            FaceEmotion.Angry => scores.Angry,
            FaceEmotion.Sad => scores.Sad,
            FaceEmotion.Surprise => scores.Surprise,
            _ => Math.Clamp(1f - Math.Max(Math.Max(scores.Joy, scores.Angry), Math.Max(scores.Sad, scores.Surprise)), 0f, 1f),
        };
    }

    private static float GetReferenceScore(
        EmotionFeatureVector features,
        EmotionFeatureVector reference)
    {
        var currentNorm = GetVectorNorm(features);
        var referenceNorm = GetVectorNorm(reference);
        if (currentNorm < ReferenceMinNorm || referenceNorm < ReferenceMinNorm)
        {
            return 0f;
        }

        var cosine = GetDotProduct(features, reference) / (currentNorm * referenceNorm);
        var strength = Math.Clamp(currentNorm / referenceNorm, 0f, 1f);
        return ClampScore(cosine * strength);
    }

    private static EmotionFeatureVector GetReferenceDelta(EmotionCalibrationData calibration, FaceEmotion emotion)
    {
        var rawReference = calibration.GetRawReference(emotion);
        return calibration.HasNeutralBaseline
            ? rawReference.SubtractBaseline(calibration.NeutralBaseline)
            : rawReference;
    }

    private static float GetVectorNorm(EmotionFeatureVector features)
    {
        return MathF.Sqrt(GetDotProduct(features, features));
    }

    private static float GetDotProduct(
        EmotionFeatureVector left,
        EmotionFeatureVector right)
    {
        return
            Product(left.Smile, right.Smile) +
            Product(left.SadMouth, right.SadMouth) +
            Product(left.BrowDown, right.BrowDown) +
            Product(left.BrowUp, right.BrowUp) +
            Product(left.BrowInnerUp, right.BrowInnerUp) +
            Product(left.EyeWide, right.EyeWide) +
            Product(left.EyeSquint, right.EyeSquint) +
            Product(left.CheekSquint, right.CheekSquint) +
            Product(left.MouthOpen, right.MouthOpen) +
            Product(left.JawOpen, right.JawOpen) +
            Product(left.MouthPress, right.MouthPress) +
            Product(left.MouthTightener, right.MouthTightener) +
            Product(left.NoseSneer, right.NoseSneer);
    }

    private static float Product(float left, float right)
    {
        return Math.Clamp(left, 0f, 1f) * Math.Clamp(right, 0f, 1f);
    }

    private static float ClampScore(float score) => Math.Clamp(score, 0f, 1f);
}
