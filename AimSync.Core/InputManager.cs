using System.Runtime.InteropServices;
using AimSync.Core.Devices;
using AimSync.Core.Engine;
using AimSync.Core.Input;
using AimSync.Core.Mapping;

namespace AimSync.Core;

public sealed class InputManager : IDisposable
{
    [DllImport("winmm.dll")]
    private static extern uint timeBeginPeriod(uint uMilliseconds);

    [DllImport("winmm.dll")]
    private static extern uint timeEndPeriod(uint uMilliseconds);

    private static readonly ControllerButton[] AllButtons = Enum.GetValues<ControllerButton>();

    private static readonly (PhysicalButtons Physical, ControllerButton Virtual)[] PhysicalMap =
    [
        (PhysicalButtons.A, ControllerButton.A),
        (PhysicalButtons.B, ControllerButton.B),
        (PhysicalButtons.X, ControllerButton.X),
        (PhysicalButtons.Y, ControllerButton.Y),
        (PhysicalButtons.LeftShoulder, ControllerButton.LeftShoulder),
        (PhysicalButtons.RightShoulder, ControllerButton.RightShoulder),
        (PhysicalButtons.View, ControllerButton.Back),
        (PhysicalButtons.Menu, ControllerButton.Start),
        (PhysicalButtons.Nexus, ControllerButton.Guide),
        (PhysicalButtons.LeftStick, ControllerButton.LeftThumb),
        (PhysicalButtons.RightStick, ControllerButton.RightThumb),
        (PhysicalButtons.DPadUp, ControllerButton.DPadUp),
        (PhysicalButtons.DPadDown, ControllerButton.DPadDown),
        (PhysicalButtons.DPadLeft, ControllerButton.DPadLeft),
        (PhysicalButtons.DPadRight, ControllerButton.DPadRight),
    ];

    private readonly AimSyncSettings _settings;
    private readonly Func<IVirtualController> _controllerFactory;
    private readonly KeyboardManager _keyboard;
    private readonly MouseManager _mouse;
    private readonly MappingManager _mapper;
    private readonly RecoilCompensator _recoil;
    private readonly TurboEngine _turbo;
    private readonly MacroEngine _macro;
    private readonly Diagnostics.IDiagnosticLogger _diagnostics;
    private readonly IPhysicalControllerSource? _physicalSource;
    private readonly ControllerState _output = new();
    private readonly HashSet<KeyCode> _monitoredKeys = [];
    private readonly bool _ownsMouseCapture;
    private readonly bool[] _lastButtons = new bool[AllButtons.Length];
    private byte _lastLeftTrigger;
    private byte _lastRightTrigger;
    private PhysicalButtons _lastPhysicalButtons;
    private bool _hadPhysicalSnapshot;
    private long _actionsSent;
    private string? _outputError;

    private Thread? _loopThread;
    private volatile bool _loopRunning;
    private readonly object _tickLock = new();
    private IVirtualController? _controller;
    private bool _disposed;

    public AimSyncSettings Settings => _settings;
    public KeyboardManager Keyboard => _keyboard;
    public MouseManager Mouse => _mouse;
    public MappingManager Mapper => _mapper;
    public ControllerState CurrentOutput => _output;

    public bool IsEnabled { get; private set; }
    public IVirtualController? Controller => _controller;
    public bool IsLoopRunning => _loopRunning;
    public string? LastError { get; private set; }

    /// <summary>true si la ultima lectura del mando fisico devolvio un dispositivo conectado.</summary>
    public bool IsPhysicalControllerConnected { get; private set; }

    /// <summary>true si el interruptor maestro de macros esta activado.</summary>
    public bool IsMacroEnabled => _settings.Macros.Enabled;

    /// <summary>true mientras alguna macro tiene una ejecucion en curso.</summary>
    public bool IsMacroRunning => _macro.RunningCount > 0;

    /// <summary>Numero de paquetes de estado escritos correctamente al mando virtual.</summary>
    public long ActionsSent => Interlocked.Read(ref _actionsSent);

    /// <summary>Ultimo error de escritura al mando virtual, si lo hubo.</summary>
    public string? OutputError => _outputError;

    private bool DiagnosticsEnabled => !ReferenceEquals(_diagnostics, Diagnostics.NullDiagnosticLogger.Instance);

    /// <summary>Arma activa en vivo (1 o 2) segun deteccion por teclas o seleccion manual.</summary>
    public int ActiveWeapon => _recoil.ActiveWeapon;

    /// <summary>Se dispara cuando cambia el arma activa por deteccion o seleccion manual.</summary>
    public event Action? WeaponChanged;

    public event Action<bool>? EnabledChanged;

    public InputManager(
        AimSyncSettings settings,
        Func<IVirtualController> controllerFactory,
        IKeyboardCapture? keyboardCapture = null,
        IMouseCapture? mouseCapture = null,
        IPhysicalControllerSource? physicalSource = null,
        Diagnostics.IDiagnosticLogger? diagnostics = null)
    {
        _diagnostics = diagnostics ?? Diagnostics.NullDiagnosticLogger.Instance;
        _settings = settings;
        _controllerFactory = controllerFactory;
        _keyboard = new KeyboardManager(keyboardCapture ?? new KeyboardCapture());
        _ownsMouseCapture = mouseCapture is null;
        _mouse = new MouseManager(mouseCapture ?? new MouseCapture());
        _mapper = new MappingManager(settings.Mapping);
        _recoil = new RecoilCompensator(settings)
        {
            ActiveWeapon = settings.AntiRecoil.UseArma2 ? 2 : 1,
        };
        _turbo = new TurboEngine(settings);
        _macro = new MacroEngine(settings, _diagnostics);
        _physicalSource = physicalSource;

        foreach (var key in settings.Mapping.MonitoredKeys)
            _monitoredKeys.Add(key);
        _monitoredKeys.Add(settings.ToggleKey);
        _monitoredKeys.Add(settings.AntiRecoil.Weapon1Key);
        _monitoredKeys.Add(settings.AntiRecoil.Weapon2Key);
    }

    public bool Activate()
    {
        ThrowIfDisposed();

        if (IsEnabled)
            return true;

        if (_controller is null)
        {
            try
            {
                _controller = _controllerFactory();
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return false;
            }
        }

        if (_controller is null)
        {
            LastError = "Controller factory returned null.";
            return false;
        }

        if (!_controller.IsConnected && !_controller.Connect())
        {
            LastError = _controller.LastError ?? "Controller.Connect() returned false (ViGEmBus not running?).";
            return false;
        }

        LastError = null;
        _outputError = null;
        ResetOutputSnapshot();
        IsEnabled = true;
        SetLegacySuppression(true);
        EnabledChanged?.Invoke(true);
        return true;
    }

    public void Deactivate()
    {
        ThrowIfDisposed();

        if (!IsEnabled)
            return;

        IsEnabled = false;
        _output.Reset();
        _recoil.Reset();
        _turbo.Reset();
        _macro.Reset();
        _controller?.Reset();
        ResetOutputSnapshot();
        _outputError = null;
        LastError = null;
        SetLegacySuppression(false);
        EnabledChanged?.Invoke(false);
    }

    private void SetLegacySuppression(bool enabled)
    {
        if (_mouse.Capture is MouseCapture real)
            real.SetSuppressLegacy(enabled);
    }

    public void SelectWeapon(int weapon)
    {
        if (weapon is not (1 or 2))
            return;
        if (_recoil.ActiveWeapon == weapon)
            return;

        _recoil.ActiveWeapon = weapon;
        _settings.AntiRecoil.UseArma2 = weapon == 2;
        WeaponChanged?.Invoke();
    }

    public void Toggle()
    {
        if (IsEnabled)
            Deactivate();
        else
            Activate();
    }

    public void Start()
    {
        ThrowIfDisposed();

        if (_loopRunning)
            return;

        _loopRunning = true;
        _loopThread = new Thread(Loop)
        {
            IsBackground = true,
            Name = "AimSync.InputLoop",
        };
        _loopThread.Start();
    }

    public void Stop()
    {
        if (!_loopRunning)
            return;

        _loopRunning = false;
        _loopThread?.Join(500);
        _loopThread = null;
    }

    /// <summary>
    /// Slot del mando fisico que se usa como entrada adicional (por defecto 0).
    /// Con el mando conectado se reenvian botones, gatillos y sticks a la salida virtual,
    /// de modo que el mando fisico controla el mando virtual por completo.
    /// </summary>
    public int PhysicalInputSlot { get; set; }

    private void MergePhysical(ControllerState state)
    {
        if (_physicalSource is null)
        {
            IsPhysicalControllerConnected = false;
            return;
        }

        int slot = Math.Clamp(PhysicalInputSlot, 0, Math.Max(0, _physicalSource.SlotCount - 1));
        if (!_physicalSource.TryRead(slot, out var physical) || !physical.IsConnected)
        {
            IsPhysicalControllerConnected = false;
            _hadPhysicalSnapshot = false;
            _lastPhysicalButtons = PhysicalButtons.None;
            return;
        }

        IsPhysicalControllerConnected = true;

        if (DiagnosticsEnabled)
            LogPhysicalEdges(physical.Buttons);
        _lastPhysicalButtons = physical.Buttons;
        _hadPhysicalSnapshot = true;

        state.LeftTrigger = Math.Max(state.LeftTrigger, physical.LeftTrigger);
        state.RightTrigger = Math.Max(state.RightTrigger, physical.RightTrigger);

        // Con el mando fisico conectado, el mando fisico controla los sticks (el usuario
        // apunta y se mueve con el). Se aplica una zona muerta pequena para eliminar la
        // deriva del hardware en reposo. Sin mando fisico, los sticks siguen viniendo del
        // raton/teclado.
        state.LeftX = DeadzoneAxis(physical.LeftXNormalized);
        state.LeftY = DeadzoneAxis(physical.LeftYNormalized);
        state.RightX = DeadzoneAxis(physical.RightXNormalized);
        state.RightY = DeadzoneAxis(physical.RightYNormalized);

        foreach (var (physicalButton, virtualButton) in PhysicalMap)
        {
            if (physical.Has(physicalButton))
            {
                state.SetButton(virtualButton, true);

            }
        }
    }

    private const float PhysicalStickDeadzone = 0.08f;

    private static float DeadzoneAxis(float value)
        => MathF.Abs(value) < PhysicalStickDeadzone ? 0f : value;

    private void LogPhysicalEdges(PhysicalButtons current)
    {
        foreach (var (physicalButton, virtualButton) in PhysicalMap)
        {
            bool now = (current & physicalButton) != 0;
            bool before = _hadPhysicalSnapshot && (_lastPhysicalButtons & physicalButton) != 0;
            if (now == before)
                continue;

            _diagnostics.Log(new Diagnostics.DiagnosticEvent
            {
                ButtonName = virtualButton.ToString(),
                IsPhysicalPressed = now,
                IsPhysicalReleased = !now,
                ActionPlanned = now ? "Entrada fisica reconocida" : "Entrada fisica liberada",
                ActionExecuted = now ? "Mapeo aplicado" : "Liberacion aplicada",
                StateSentToVirtual = now ? "Boton activado" : "Boton desactivado",
            });
        }
    }

    public bool TryTick()
    {
        ThrowIfDisposed();

        lock (_tickLock)
        {
            _keyboard.Poll(_monitoredKeys);
            _mouse.Poll();

            if (_keyboard.IsNewlyPressed(_settings.ToggleKey))
                Toggle();

            UpdateWeaponDetection();

            if (!IsEnabled)
                return false;

            var controller = _controller;
            if (controller is null)
                return false;

            var state = _mapper.Process(_keyboard, _mouse);
            MergePhysical(state);

            // Modo normal de gatillos: sensibilidad y umbral independientes (por defecto 1 / 0,
            // que deja el valor analogico intacto). Se aplica aqui, antes de los motores, para que
            // el modo de pulsacion repetida (turbo) siga siendo el unico que reescribe el gatillo.
            state.RightTrigger = _settings.RightTrigger.Process(state.RightTrigger);
            state.LeftTrigger = _settings.LeftTrigger.Process(state.LeftTrigger);

            _settings.LeftStick.Process(state.LeftX, state.LeftY, out float leftX, out float leftY);
            _settings.RightStick.Process(state.RightX, state.RightY, out float rightX, out float rightY);

            _output.LeftX = leftX;
            _output.LeftY = leftY;
            _output.RightX = rightX;
            _output.RightY = rightY;
            _output.LeftTrigger = state.LeftTrigger;
            _output.RightTrigger = state.RightTrigger;
            foreach (var button in AllButtons)
                _output.SetButton(button, state.GetButton(button));

            _recoil.Apply(_output, _output.RightTrigger > 0);
            _macro.Apply(_output, state);
            _turbo.Apply(_output);

            return WriteOutput(controller);
        }
    }

    /// <summary>
    /// Unica ruta controlada de escritura al mando virtual. Captura errores de salida y
    /// registra las transiciones (pulsado/liberado) que llegan realmente al dispositivo.
    /// </summary>
    private bool WriteOutput(IVirtualController controller)
    {
        try
        {
            controller.SetLeftStick(ToShort(_output.LeftX), ToShort(_output.LeftY));
            controller.SetRightStick(ToShort(_output.RightX), ToShort(_output.RightY));
            controller.SetLeftTrigger(_output.LeftTrigger);
            controller.SetRightTrigger(_output.RightTrigger);
            foreach (var button in AllButtons)
                controller.SetButton(button, _output.GetButton(button));

            // Un unico envio por tick (AutoSubmitReport esta desactivado en el mando virtual)
            // para poder sostener ~1000 Hz en vez de un reporte por cada Set*.
            controller.Submit();
            _outputError = null;
        }
        catch (Exception ex)
        {
            _outputError = ex.Message;
            LastError = ex.Message;
            _diagnostics.Log(new Diagnostics.DiagnosticEvent
            {
                Errors = ex.Message,
                ActionPlanned = "Escribir estado al mando virtual",
                ActionExecuted = "Error de salida",
            });
            return false;
        }

        Interlocked.Increment(ref _actionsSent);
        LogOutputTransitions();
        return true;
    }

    private void LogOutputTransitions()
    {
        if (DiagnosticsEnabled)
        {
            bool macroActive = _macro.RunningCount > 0;
            for (int i = 0; i < AllButtons.Length; i++)
            {
                var button = AllButtons[i];
                bool now = _output.GetButton(button);
                bool before = _lastButtons[i];
                if (now == before)
                    continue;

                _diagnostics.Log(new Diagnostics.DiagnosticEvent
                {
                    ButtonName = button.ToString(),
                    MacroActive = macroActive,
                    MacroName = macroActive ? "ejecutando" : string.Empty,
                    ActionPlanned = now ? "Pulsar boton" : "Liberar boton",
                    ActionExecuted = now ? "Transicion pulsado" : "Transicion liberado",
                    StateSentToVirtual = FormatOutputSummary(button),
                });
            }

            if (_output.LeftTrigger != _lastLeftTrigger)
                LogTriggerTransition("LeftTrigger", _output.LeftTrigger, _lastLeftTrigger);
            if (_output.RightTrigger != _lastRightTrigger)
                LogTriggerTransition("RightTrigger", _output.RightTrigger, _lastRightTrigger);
        }

        for (int i = 0; i < AllButtons.Length; i++)
            _lastButtons[i] = _output.GetButton(AllButtons[i]);
        _lastLeftTrigger = _output.LeftTrigger;
        _lastRightTrigger = _output.RightTrigger;
    }

    private void LogTriggerTransition(string name, byte now, byte before)
    {
        _diagnostics.Log(new Diagnostics.DiagnosticEvent
        {
            ButtonName = name,
            MacroActive = _macro.RunningCount > 0,
            ActionPlanned = now > before ? "Subir gatillo" : "Bajar gatillo",
            ActionExecuted = $"{name}: {before} -> {now}",
            StateSentToVirtual = $"RT={_output.RightTrigger}; LT={_output.LeftTrigger}",
        });
    }

    private string FormatOutputSummary(ControllerButton changed)
        => $"{changed}={_output.GetButton(changed)}; RT={_output.RightTrigger}; LT={_output.LeftTrigger}";

    private void ResetOutputSnapshot()
    {
        Array.Clear(_lastButtons);
        _lastLeftTrigger = 0;
        _lastRightTrigger = 0;
    }

    private void UpdateWeaponDetection()
    {
        var cfg = _settings.AntiRecoil;
        if (!cfg.Enabled || !cfg.WeaponDetectionEnabled)
            return;

        int requested;
        if (_keyboard.IsNewlyPressed(cfg.Weapon1Key) || _mouse.WheelDelta > 0)
            requested = 1;
        else if (_keyboard.IsNewlyPressed(cfg.Weapon2Key) || _mouse.WheelDelta < 0)
            requested = 2;
        else
            return;

        if (requested == _recoil.ActiveWeapon)
            return;

        _recoil.ActiveWeapon = requested;
        _settings.AntiRecoil.UseArma2 = requested == 2;
        WeaponChanged?.Invoke();
    }


    private void Loop()
    {
        timeBeginPeriod(1);
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            long targetTicks = TimeSpan.FromMilliseconds(Math.Max(1, (int)_settings.LoopInterval.TotalMilliseconds)).Ticks;
            long sleepThresholdTicks = TimeSpan.FromMilliseconds(1).Ticks * 2;

            while (_loopRunning)
            {
                sw.Restart();
                TryTick();

                long remaining = targetTicks - sw.ElapsedTicks;
                if (remaining > sleepThresholdTicks)
                    Thread.Sleep(1);

                while (sw.ElapsedTicks < targetTicks)
                    Thread.SpinWait(1);
            }
        }
        finally
        {
            timeEndPeriod(1);
        }
    }

    private static short ToShort(float value)
    {
        float rounded = MathF.Round(value * short.MaxValue);
        return (short)Math.Clamp(rounded, short.MinValue, short.MaxValue);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _loopRunning = false;
        _loopThread?.Join(500);
        _loopThread = null;

        if (IsEnabled)
        {
            IsEnabled = false;
            _output.Reset();
            _controller?.Reset();
            SetLegacySuppression(false);
            EnabledChanged?.Invoke(false);
        }

        _controller?.Dispose();
        _controller = null;

        if (_ownsMouseCapture)
            _mouse.Capture.Dispose();
    }
}

