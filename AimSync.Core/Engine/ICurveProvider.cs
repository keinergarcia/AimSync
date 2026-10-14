namespace AimSync.Core.Engine;

public interface ICurveProvider
{
    string Name { get; }
    float Evaluate(float input);
}