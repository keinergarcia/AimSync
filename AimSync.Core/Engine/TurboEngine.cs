using AimSync.Core.Mapping;

namespace AimSync.Core.Engine;

/// <summary>
/// Modo turbo: pulsa los gatillos a una frecuencia configurable mientras su entrada esta activa.
/// Los valores base los lee del propio estado (ya mapeado) y los sobrescribe con 0 durante la
/// fase OFF del ciclo.
/// </summary>
public sealed class TurboEngine
{
    private const int ButtonCount = 16;

    private readonly AimSyncSettings _settings;
    private readonly int[] _buttonTicks = new int[ButtonCount];
    private int _leftTick;
    private int _rightTick;

    public TurboEngine(AimSyncSettings settings) => _settings = settings;

    public void Reset()
    {
        _leftTick = 0;
        _rightTick = 0;
        Array.Clear(_buttonTicks);
    }

    public void Apply(ControllerState target)
    {
        var cfg = _settings.Turbo;

        if (!cfg.Enabled)
        {
            // Al desactivar no debe quedar ningun contador de fase a medias: la siguiente
            // activacion arranca limpia y ningun boton queda "pegado" en fase OFF.
            Reset();
            return;
        }

        double ticksPerSecond = TicksPerSecond(_settings);

        // Gatillos y botones usan velocidades independientes.
        int triggerTicksPerCycle = TicksPerCycle(ticksPerSecond, cfg.TriggerFrequencyHz);
        int triggerHoldTicks = HoldTicks(triggerTicksPerCycle, cfg.TriggerDutyCycle);
        int buttonTicksPerCycle = TicksPerCycle(ticksPerSecond, cfg.FrequencyHz);
        int buttonHoldTicks = HoldTicks(buttonTicksPerCycle, cfg.DutyCycle);

        ApplyTrigger(cfg.Enabled && cfg.LeftEnabled, ref _leftTick, triggerTicksPerCycle, triggerHoldTicks, true, target);
        ApplyTrigger(cfg.Enabled && cfg.RightEnabled, ref _rightTick, triggerTicksPerCycle, triggerHoldTicks, false, target);

        var rapid = cfg.RapidButtons;
        for (int i = 0; i < rapid.Count; i++)
            ApplyRapidButton(buttonTicksPerCycle, buttonHoldTicks, rapid[i], target);
        var rapidAlways = cfg.RapidAlways;
        for (int i = 0; i < rapidAlways.Count; i++)
            ApplyRapidAlwaysInternal(buttonTicksPerCycle, buttonHoldTicks, rapidAlways[i], target);
    }

    private static int TicksPerCycle(double ticksPerSecond, float frequencyHz)
        => Math.Max(1, (int)Math.Round(ticksPerSecond / Math.Max(0.1, frequencyHz)));

    private static int HoldTicks(int ticksPerCycle, float dutyCycle)
        => Math.Clamp((int)Math.Round(ticksPerCycle * Math.Clamp(dutyCycle, 0.05f, 1f)), 1, ticksPerCycle);

    /// <summary>
    /// Rafaga por boton: mientras el boton esta pulsado, alterna ON/OFF.
    /// </summary>
    private void ApplyRapidButton(int ticksPerCycle, int holdTicks, ControllerButton button, ControllerState target)
    {
        int index = (int)button;
        if (!target.GetButton(button))
        {
            _buttonTicks[index] = 0;
            return;
        }

        _buttonTicks[index]++;
        if (_buttonTicks[index] >= ticksPerCycle)
            _buttonTicks[index] = 0;

        if (_buttonTicks[index] >= holdTicks)
            target.SetButton(button, false);
    }

    private void ApplyRapidAlwaysInternal(int ticksPerCycle, int holdTicks, ControllerButton button, ControllerState target)
    {
        int index = (int)button;
        _buttonTicks[index]++;
        if (_buttonTicks[index] >= ticksPerCycle)
            _buttonTicks[index] = 0;
        if (_buttonTicks[index] < holdTicks)
            target.SetButton(button, true);
        else
            target.SetButton(button, false);
    }
    private static void ApplyTrigger(bool active, ref int tick, int ticksPerCycle, int holdTicks, bool left, ControllerState target)
    {
        byte baseValue = left ? target.LeftTrigger : target.RightTrigger;

        if (!active || baseValue == 0)
        {
            tick = 0;
            return;
        }

        tick++;
        if (tick >= ticksPerCycle)
            tick = 0;

        if (tick < holdTicks)
            return;

        if (left)
            target.LeftTrigger = 0;
        else
            target.RightTrigger = 0;
    }

    internal static double TicksPerSecond(AimSyncSettings settings)
        => 1000.0 / Math.Max(1.0, settings.LoopInterval.TotalMilliseconds);
}
