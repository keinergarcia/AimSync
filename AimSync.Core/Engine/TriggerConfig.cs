namespace AimSync.Core.Engine;

/// <summary>
/// Procesamiento del valor analogico de un gatillo (0..255) en modo normal, antes de
/// escribirlo al mando virtual. Permite dos ajustes independientes:
/// <list type="bullet">
/// <item><see cref="Sensitivity"/>: amplifica (o atenua) el recorrido del gatillo.</item>
/// <item><see cref="ActivationThreshold"/>: umbral de activacion; por debajo, el gatillo vale 0.</item>
/// </list>
/// Con los valores por defecto (sensibilidad 1 y umbral 0) el gatillo pasa intacto: 0..255
/// sin interrupciones ni pulsaciones artificiales. El procesamiento se aplica SIEMPRE al valor
/// de entrada (mando fisico o raton); el modo de pulsacion repetida (turbo) es independiente y
/// solo actua cuando el usuario lo activa de forma explicita.
/// </summary>
public sealed class TriggerConfig
{
    public const float SensitivityMin = 0.1f;
    public const float SensitivityMax = 3f;
    public const float ThresholdMin = 0f;
    public const float ThresholdMax = 0.9f;

    private float _sensitivity = 1f;
    private float _activationThreshold;

    /// <summary>Multiplicador del recorrido del gatillo (0.1..3.0). 1 = sin cambios.</summary>
    public float Sensitivity
    {
        get => _sensitivity;
        set => _sensitivity = Clamp(value, SensitivityMin, SensitivityMax);
    }

    /// <summary>Umbral de activacion (0..0.9). Si el gatillo no lo supera, vale 0. 0 = sin umbral.</summary>
    public float ActivationThreshold
    {
        get => _activationThreshold;
        set => _activationThreshold = Clamp(value, ThresholdMin, ThresholdMax);
    }

    /// <summary>
    /// Aplica sensibilidad y umbral a un valor crudo de gatillo. Nunca devuelve un valor
    /// distinto de 0 cuando la entrada es 0, de modo que soltar el gatillo siempre da 0.
    /// </summary>
    public byte Process(byte raw)
    {
        if (raw == 0)
            return 0;

        float level = raw / 255f;
        if (level < _activationThreshold)
            return 0;

        float scaled = Math.Clamp(level * _sensitivity, 0f, 1f);
        return (byte)Math.Clamp((int)MathF.Round(scaled * 255f), 0, 255);
    }

    private static float Clamp(float value, float min, float max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
}
