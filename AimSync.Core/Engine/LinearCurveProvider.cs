namespace AimSync.Core.Engine;

public sealed class LinearCurveProvider : ICurveProvider
{
    public string Name => "Linear";

    public float Evaluate(float input) => Math.Clamp(input, 0f, 1f);
}