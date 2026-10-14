using AimSync.Core.Mapping;

namespace AimSync.Core.Engine;

/// <summary>
/// Reproduce secuencias de botones (macros) cuando se pulsa la entrada mapeada al boton disparador.
/// Las pulsaciones de la macro se anaden sobre el estado ya mapeado antes de escribirlo al mando.
/// El motor mantiene, por macro, que boton tiene pulsado para poder liberarlo de forma explicita
/// al cambiar de paso, al terminar, al cancelar o al soltar el disparador.
/// </summary>
public sealed class MacroEngine
{
    private readonly AimSyncSettings _settings;
    private readonly Dictionary<Macro, Runtime> _runtimes = [];
    private readonly Diagnostics.IDiagnosticLogger _logger;

    public MacroEngine(AimSyncSettings settings, Diagnostics.IDiagnosticLogger? logger = null)
    {
        _settings = settings;
        _logger = logger ?? Diagnostics.NullDiagnosticLogger.Instance;
    }

    /// <summary>Numero de macros que tienen una ejecucion en curso (no completada y con disparador mantenido).</summary>
    public int RunningCount
    {
        get
        {
            int count = 0;
            foreach (var runtime in _runtimes.Values)
            {
                if (runtime.Active && !runtime.Completed)
                    count++;
            }
            return count;
        }
    }

    public void Reset() => _runtimes.Clear();

    public void Apply(ControllerState target, ControllerState input)
    {
        var cfg = _settings.Macros;
        if (!cfg.Enabled || cfg.Macros.Count == 0)
        {
            if (_runtimes.Count > 0)
                _runtimes.Clear();
            return;
        }

        double ticksPerSecond = TurboEngine.TicksPerSecond(_settings);
        var macros = cfg.Macros;

        for (int i = 0; i < macros.Count; i++)
        {
            var macro = macros[i];
            if (macro is null || !macro.Enabled || macro.Steps.Count == 0)
                continue;

            if (!_runtimes.TryGetValue(macro, out var state))
            {
                state = new Runtime();
                _runtimes[macro] = state;
            }

            bool pressed = input.GetButton(macro.TriggerButton);
            if (!pressed)
            {
                bool wasRunning = state.Active || state.StepIndex != 0 || state.TicksInStep != 0 || state.Completed;
                Release(state, target);
                if (wasRunning)
                {
                    _logger.Log(new Diagnostics.DiagnosticEvent
                    {
                        ButtonName = macro.TriggerButton.ToString(),
                        MacroName = macro.Name,
                        MacroActive = false,
                        Cancellations = "Disparador soltado: macro detenida",
                        ActionExecuted = "Boton liberado",
                        ActionPlanned = "Esperar disparador",
                    });
                }
                state.Reset();
                continue;
            }

            if (state.Completed)
            {
                Release(state, target);
                continue;
            }

            state.Active = true;

            if (macro.RapidFire)
            {
                state.PulseTick++;
                bool on = (state.PulseTick % 2) == 1;
                target.SetButton(macro.TriggerButton, on);
                state.Held = on ? macro.TriggerButton : null;
                continue;
            }

            int index = Math.Clamp(state.StepIndex, 0, macro.Steps.Count - 1);
            var step = macro.Steps[index];

            if (state.Held is { } held && held != step.Button)
                target.SetButton(held, false);

            target.SetButton(step.Button, true);
            state.Held = step.Button;

            int stepTicks = Math.Max(1, (int)Math.Round(step.DurationMs * ticksPerSecond / 1000.0));
            state.TicksInStep++;
            if (state.TicksInStep < stepTicks)
                continue;

            state.TicksInStep = 0;
            state.StepIndex++;
            if (state.StepIndex >= macro.Steps.Count)
            {
                if (macro.Loop)
                    state.StepIndex = 0;
                else
                {
                    state.Completed = true;
                    state.Active = false;
                }
            }
        }
    }

    private static void Release(Runtime state, ControllerState target)
    {
        if (state.Held is { } held)
        {
            target.SetButton(held, false);
            state.Held = null;
        }
    }

    private sealed class Runtime
    {
        public int StepIndex;
        public int TicksInStep;
        public int PulseTick;
        public bool Completed;
        public bool Active;
        public ControllerButton? Held;

        public void Reset()
        {
            StepIndex = 0;
            TicksInStep = 0;
            PulseTick = 0;
            Completed = false;
            Active = false;
            Held = null;
        }
    }
}
