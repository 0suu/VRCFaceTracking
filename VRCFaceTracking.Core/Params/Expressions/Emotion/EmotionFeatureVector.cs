using VRCFaceTracking.Core.Params.Data;

namespace VRCFaceTracking.Core.Params.Expressions;

public struct EmotionFeatureVector
{
    public float Smile { get; set; }
    public float SadMouth { get; set; }
    public float BrowDown { get; set; }
    public float BrowUp { get; set; }
    public float BrowInnerUp { get; set; }
    public float EyeWide { get; set; }
    public float EyeSquint { get; set; }
    public float CheekSquint { get; set; }
    public float MouthOpen { get; set; }
    public float JawOpen { get; set; }
    public float MouthPress { get; set; }
    public float MouthTightener { get; set; }
    public float NoseSneer { get; set; }

    public void Add(EmotionFeatureVector other)
    {
        Smile += other.Smile;
        SadMouth += other.SadMouth;
        BrowDown += other.BrowDown;
        BrowUp += other.BrowUp;
        BrowInnerUp += other.BrowInnerUp;
        EyeWide += other.EyeWide;
        EyeSquint += other.EyeSquint;
        CheekSquint += other.CheekSquint;
        MouthOpen += other.MouthOpen;
        JawOpen += other.JawOpen;
        MouthPress += other.MouthPress;
        MouthTightener += other.MouthTightener;
        NoseSneer += other.NoseSneer;
    }

    public EmotionFeatureVector Divide(float divisor)
    {
        if (divisor <= 0f)
        {
            return this;
        }

        return new EmotionFeatureVector
        {
            Smile = Smile / divisor,
            SadMouth = SadMouth / divisor,
            BrowDown = BrowDown / divisor,
            BrowUp = BrowUp / divisor,
            BrowInnerUp = BrowInnerUp / divisor,
            EyeWide = EyeWide / divisor,
            EyeSquint = EyeSquint / divisor,
            CheekSquint = CheekSquint / divisor,
            MouthOpen = MouthOpen / divisor,
            JawOpen = JawOpen / divisor,
            MouthPress = MouthPress / divisor,
            MouthTightener = MouthTightener / divisor,
            NoseSneer = NoseSneer / divisor,
        };
    }

    public EmotionFeatureVector SubtractBaseline(EmotionFeatureVector baseline)
    {
        return new EmotionFeatureVector
        {
            Smile = ClampPositive(Smile - baseline.Smile),
            SadMouth = ClampPositive(SadMouth - baseline.SadMouth),
            BrowDown = ClampPositive(BrowDown - baseline.BrowDown),
            BrowUp = ClampPositive(BrowUp - baseline.BrowUp),
            BrowInnerUp = ClampPositive(BrowInnerUp - baseline.BrowInnerUp),
            EyeWide = ClampPositive(EyeWide - baseline.EyeWide),
            EyeSquint = ClampPositive(EyeSquint - baseline.EyeSquint),
            CheekSquint = ClampPositive(CheekSquint - baseline.CheekSquint),
            MouthOpen = ClampPositive(MouthOpen - baseline.MouthOpen),
            JawOpen = ClampPositive(JawOpen - baseline.JawOpen),
            MouthPress = ClampPositive(MouthPress - baseline.MouthPress),
            MouthTightener = ClampPositive(MouthTightener - baseline.MouthTightener),
            NoseSneer = ClampPositive(NoseSneer - baseline.NoseSneer),
        };
    }

    private static float ClampPositive(float value) => Math.Clamp(value, 0f, 1f);
}

public static class EmotionFeatureExtractor
{
    public static EmotionFeatureVector Extract(UnifiedTrackingData data)
    {
        var jawOpen = W(data, UnifiedExpressions.JawOpen);

        return new EmotionFeatureVector
        {
            Smile = Avg(
                W(data, UnifiedExpressions.MouthCornerPullLeft) * 0.8f +
                W(data, UnifiedExpressions.MouthCornerSlantLeft) * 0.2f,
                W(data, UnifiedExpressions.MouthCornerPullRight) * 0.8f +
                W(data, UnifiedExpressions.MouthCornerSlantRight) * 0.2f),

            SadMouth = Avg(
                Max(W(data, UnifiedExpressions.MouthFrownLeft), W(data, UnifiedExpressions.MouthStretchLeft) * 0.35f),
                Max(W(data, UnifiedExpressions.MouthFrownRight), W(data, UnifiedExpressions.MouthStretchRight) * 0.35f)),

            BrowDown = Avg(
                W(data, UnifiedExpressions.BrowLowererLeft) * 0.75f +
                W(data, UnifiedExpressions.BrowPinchLeft) * 0.25f,
                W(data, UnifiedExpressions.BrowLowererRight) * 0.75f +
                W(data, UnifiedExpressions.BrowPinchRight) * 0.25f),

            BrowUp = Avg(
                W(data, UnifiedExpressions.BrowOuterUpLeft) * 0.6f +
                W(data, UnifiedExpressions.BrowInnerUpLeft) * 0.4f,
                W(data, UnifiedExpressions.BrowOuterUpRight) * 0.6f +
                W(data, UnifiedExpressions.BrowInnerUpRight) * 0.4f),

            BrowInnerUp = Avg(
                W(data, UnifiedExpressions.BrowInnerUpLeft),
                W(data, UnifiedExpressions.BrowInnerUpRight)),

            EyeWide = Avg(
                W(data, UnifiedExpressions.EyeWideLeft),
                W(data, UnifiedExpressions.EyeWideRight)),

            EyeSquint = Avg(
                W(data, UnifiedExpressions.EyeSquintLeft),
                W(data, UnifiedExpressions.EyeSquintRight)),

            CheekSquint = Avg(
                W(data, UnifiedExpressions.CheekSquintLeft),
                W(data, UnifiedExpressions.CheekSquintRight)),

            JawOpen = jawOpen,

            MouthOpen = Math.Clamp(
                jawOpen * 0.5f +
                Avg(
                    W(data, UnifiedExpressions.MouthUpperUpLeft) + W(data, UnifiedExpressions.MouthLowerDownLeft),
                    W(data, UnifiedExpressions.MouthUpperUpRight) + W(data, UnifiedExpressions.MouthLowerDownRight)) * 0.25f,
                0f,
                1f),

            MouthPress = Avg(
                W(data, UnifiedExpressions.MouthPressLeft),
                W(data, UnifiedExpressions.MouthPressRight)),

            MouthTightener = Avg(
                W(data, UnifiedExpressions.MouthTightenerLeft),
                W(data, UnifiedExpressions.MouthTightenerRight)),

            NoseSneer = Avg(
                W(data, UnifiedExpressions.NoseSneerLeft),
                W(data, UnifiedExpressions.NoseSneerRight)),
        };
    }

    private static float W(UnifiedTrackingData data, UnifiedExpressions shape)
    {
        var value = data.Shapes[(int)shape].Weight;
        return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Math.Clamp(value, 0f, 1f);
    }

    private static float Avg(float a, float b) => (a + b) * 0.5f;

    private static float Max(float a, float b) => Math.Max(a, b);
}
