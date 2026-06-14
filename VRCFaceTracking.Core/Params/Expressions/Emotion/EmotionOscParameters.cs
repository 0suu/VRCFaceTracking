using VRCFaceTracking.Core.Contracts;
using VRCFaceTracking.Core.OSC.DataTypes;
using VRCFaceTracking.Core.Params;

namespace VRCFaceTracking.Core.Params.Expressions;

public static class EmotionOscParameters
{
    public static readonly Parameter[] Parameters =
    {
        new EmotionOscParameterPair(),
    };
}

public sealed class EmotionOscParameterPair : Parameter
{
    private readonly Parameter[] _parameters =
    {
        new BaseParam<int>("v2/FaceEmotionIndex", _ => GetIndex()),
        new BaseParam<float>("v2/FaceEmotionPower", _ => GetPower()),
    };

    public override Parameter[] ResetParam(IParameterDefinition[] newParams)
    {
        return _parameters.SelectMany(parameter => parameter.ResetParam(newParams)).ToArray();
    }

    public override (string, Parameter)[] GetParamNames()
    {
        return _parameters.SelectMany(parameter => parameter.GetParamNames()).ToArray();
    }

    private static int GetIndex()
    {
        var snapshot = EmotionOscRuntime.GetSnapshot();
        return snapshot.Enabled ? snapshot.CurrentResult.Index : 0;
    }

    private static float GetPower()
    {
        var snapshot = EmotionOscRuntime.GetSnapshot();
        return snapshot.Enabled
            ? MathF.Round(Math.Clamp(snapshot.CurrentResult.Power, 0f, 1f), 3)
            : 0f;
    }
}
