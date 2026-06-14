using System.Runtime.Serialization;

namespace VRCFaceTracking.Core.Params.Expressions;

public sealed class EmotionCalibrationData
{
    public EmotionFeatureVector NeutralBaseline { get; set; }

    public bool HasNeutralBaseline { get; set; }

    public EmotionReferenceCalibrationData JoyReference { get; set; } = new();
    public EmotionReferenceCalibrationData AngryReference { get; set; } = new();
    public EmotionReferenceCalibrationData SadReference { get; set; } = new();
    public EmotionReferenceCalibrationData SurpriseReference { get; set; } = new();

    [OnDeserialized]
    private void OnDeserialized(StreamingContext context)
    {
        EnsureDefaults();
    }

    public bool IsReadyForEmotionOsc()
    {
        return HasNeutralBaseline && HasCompleteEmotionReferences();
    }

    public bool HasCompleteEmotionReferences()
    {
        return JoyReference.HasReference &&
            AngryReference.HasReference &&
            SadReference.HasReference &&
            SurpriseReference.HasReference;
    }

    public bool HasReference(FaceEmotion emotion)
    {
        return TryGetReference(emotion)?.HasReference == true;
    }

    public EmotionFeatureVector GetRawReference(FaceEmotion emotion)
    {
        return TryGetReference(emotion)?.RawReference ?? default;
    }

    public void SetRawReference(FaceEmotion emotion, EmotionFeatureVector reference, int sampleCount)
    {
        EnsureDefaults();
        var referenceData = TryGetReference(emotion);
        if (referenceData == null)
        {
            return;
        }

        referenceData.HasReference = true;
        referenceData.SampleCount = Math.Max(sampleCount, 0);
        referenceData.RawReference = reference;
    }

    public void Reset()
    {
        NeutralBaseline = default;
        HasNeutralBaseline = false;
        ClearEmotionReferences();
    }

    public void ClearEmotionReferences()
    {
        JoyReference = new EmotionReferenceCalibrationData();
        AngryReference = new EmotionReferenceCalibrationData();
        SadReference = new EmotionReferenceCalibrationData();
        SurpriseReference = new EmotionReferenceCalibrationData();
    }

    public void EnsureDefaults()
    {
        JoyReference ??= new EmotionReferenceCalibrationData();
        AngryReference ??= new EmotionReferenceCalibrationData();
        SadReference ??= new EmotionReferenceCalibrationData();
        SurpriseReference ??= new EmotionReferenceCalibrationData();
    }

    private EmotionReferenceCalibrationData? TryGetReference(FaceEmotion emotion)
    {
        return emotion switch
        {
            FaceEmotion.Joy => JoyReference,
            FaceEmotion.Angry => AngryReference,
            FaceEmotion.Sad => SadReference,
            FaceEmotion.Surprise => SurpriseReference,
            _ => null,
        };
    }
}

public sealed class EmotionReferenceCalibrationData
{
    public bool HasReference { get; set; }

    public int SampleCount { get; set; }

    public EmotionFeatureVector RawReference { get; set; }

}
