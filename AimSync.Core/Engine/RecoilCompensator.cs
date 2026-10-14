using AimSync.Core.Mapping;

namespace AimSync.Core.Engine;

/// <summary>
/// Aplica compensacion de retroceso mientras se dispara: un empuje constante al stick
/// derecho (vertical = sube contra el retroceso descendente; horizontal = deriva lateral)
/// y una oscilacion senoidal opcional en el stick izquierdo.
/// Los valores se leen en vivo de AimSyncSettings, igual que el resto del pipeline.
/// </summary>
public sealed class RecoilCompensator
{
    private const float MaxOscillationHz = 120f;
    private const int PumpPeriodTicks = 12;      // ciclo de ~20 Hz con bucle base 250Hz
    private const int PumpHoldTicks = 9;         // ticks con fuerza maxima
    private const float PumpReleaseFactor = 0.6f; // fuerza minima durante la breve liberacion

    private readonly AimSyncSettings _settings;
    private double _phase;
    private int _pumpTick;

    public RecoilCompensator(AimSyncSettings settings) => _settings = settings;

    private double TicksPerSecond => 1000.0 / Math.Max(1.0, _settings.LoopInterval.TotalMilliseconds);

    /// <summary>
    /// Arma activa en vivo (1 o 2). Es la fuente unica de verdad del perfil aplicado;
    /// se cambia con <c>InputManager.SelectWeapon</c> (deteccion por teclas o seleccion manual).
    /// </summary>
    public int ActiveWeapon { get; set; } = 1;

    public void Reset()
    {
        _phase = 0;
        _pumpTick = 0;
    }

    public void Apply(ControllerState target, bool firing)
    {
        var cfg = _settings.AntiRecoil;

        if (!cfg.Enabled || !firing)
        {
            _phase = 0;
            _pumpTick = 0;
            return;
        }

        bool useArma2 = ActiveWeapon == 2;
        float vertical = useArma2 ? cfg.Arma2Vertical : cfg.Arma1Vertical;
        float horizontal = useArma2 ? cfg.Arma2Horizontal : cfg.Arma1Horizontal;

        // Curva de potencia: amplifica la fuerza percibida en valores medios sin perder el
        // 100% en el extremo. 0.36 -> 51%, 0.5 -> 63%, 0.9 -> 93%, 1.0 -> 100% (stick completo).
        float vPush = Boost(vertical);
        float hPush = Boost(horizontal);

        // Refuerzo por rafagas: reafirma la fuerza a ~20 Hz (liberacion breve cada ciclo).
        float factor = 1f;
        if (cfg.PumpEnabled && (vPush != 0f || hPush != 0f))
        {
            _pumpTick++;
            int step = _pumpTick % PumpPeriodTicks;
            // Ciclo de 12 ticks: 9 mantienen la fuerza maxima y 3 la liberan (75% duty).
            if (step == 0 || step > PumpHoldTicks)
                factor = PumpReleaseFactor;
        }

        if (vPush != 0f)
            target.RightY = Clamp(target.RightY + vPush * factor, -1f, 1f);

        if (hPush != 0f)
            target.RightX = Clamp(target.RightX + hPush * factor, -1f, 1f);

        if (cfg.Oscillation && cfg.OscillationStrength > 0f)
        {
            // La intensidad ES la amplitud: 100% = recorrido completo del stick (-1..1).
            // (Antes se multiplicaba por 0.5, lo que dejaba la oscilacion por debajo de la
            // zona muerta de la mayoria de juegos y "no se notaba".)
            float freq = MathF.Min(cfg.OscillationFrequency, MaxOscillationHz);
            _phase += freq * 2d * Math.PI / TicksPerSecond;
            if (_phase >= Math.PI * 2d)
                _phase -= Math.PI * 2d;

            float amp = MathF.Min(cfg.OscillationStrength, 1f);
            float sway = (float)(Math.Sin(_phase) * amp);
            target.LeftX = Clamp(target.LeftX + sway, -1f, 1f);
        }
    }

    private static float Boost(float value)
    {
        if (value == 0f)
            return 0f;
        // Curva agresiva: 36% -> 60%, 50% -> 71%, 90% -> 95%, 100% -> 100% (stick completo).
        return MathF.CopySign(MathF.Sqrt(MathF.Abs(value)), value);
    }

    private static float Clamp(float value, float min, float max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
}