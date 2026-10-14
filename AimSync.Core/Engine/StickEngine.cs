namespace AimSync.Core.Engine;

public sealed class StickEngine
{
    private ICurveProvider _curve;

    public float Sensitivity { get; set; }
    public float Deadzone { get; set; }

    public ICurveProvider Curve
    {
        get => _curve;
        set => _curve = value ?? new LinearCurveProvider();
    }

    public StickEngine(float sensitivity = 1f, float deadzone = 0f, ICurveProvider? curve = null)
    {
        Sensitivity = sensitivity;
        Deadzone = Math.Clamp(deadzone, 0f, 0.95f);
        _curve = curve ?? new LinearCurveProvider();
    }

    public void Process(float rawX, float rawY, out float x, out float y)
    {
        x = 0f;
        y = 0f;

        float magnitude = MathF.Sqrt(rawX * rawX + rawY * rawY);
        if (magnitude <= 0f || magnitude <= Deadzone || Deadzone >= 1f)
            return;

        float normalized = (magnitude - Deadzone) / (1f - Deadzone);
        float curved = _curve.Evaluate(Math.Clamp(normalized, 0f, 1f));
        float scaled = Math.Clamp(curved * Sensitivity, -1f, 1f);

        x = (rawX / magnitude) * scaled;
        y = (rawY / magnitude) * scaled;
    }
}