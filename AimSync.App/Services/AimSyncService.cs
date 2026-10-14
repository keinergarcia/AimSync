using AimSync.Core;
using AimSync.Core.Devices;
using AimSync.Core.Input;
using AimSync.Core.Mapping;

namespace AimSync.App.Services;

public sealed class AimSyncService : IAimSyncService
{
    private readonly AimSyncSettings _settings;
    private readonly Func<IVirtualController>? _controllerFactory;
    private readonly DeviceManager _deviceManager = new();
    private readonly IPhysicalControllerSource _physicalSource;
    private readonly bool _ownsPhysicalSource;
    private InputManager? _inputManager;
    private bool _subscribed;

    private readonly AimSync.Core.Diagnostics.IDiagnosticLogger? _diagnostics;
    public AimSyncService(
        AimSyncSettings? settings = null,
        Func<IVirtualController>? controllerFactory = null,
        IPhysicalControllerSource? physicalSource = null,
        AimSync.Core.Diagnostics.IDiagnosticLogger? diagnostics = null)
    {
        _settings = settings ?? new AimSyncSettings();
        _controllerFactory = controllerFactory;
        _ownsPhysicalSource = physicalSource is null;
        _physicalSource = physicalSource ?? new XInputControllerSource();
        _diagnostics = diagnostics;
    }

    private InputManager Manager
    {
        get
        {
            if (_inputManager is null)
            {
                _inputManager = new InputManager(
                    _settings,
                    _controllerFactory ?? _deviceManager.CreateController,
                    physicalSource: _physicalSource,
                    diagnostics: _diagnostics);
                _inputManager.EnabledChanged += _ => StateChanged?.Invoke();
                _inputManager.WeaponChanged += () => WeaponChanged?.Invoke();
                _subscribed = true;
            }

            return _inputManager;
        }
    }

    public AimSyncSettings Settings => _settings;

    public bool IsEnabled => _inputManager?.IsEnabled ?? false;

    public bool IsConnected => _inputManager?.Controller?.IsConnected ?? false;

    public bool IsLoopRunning => _inputManager?.IsLoopRunning ?? false;

    public string? LastError => _inputManager?.LastError;

    public bool IsPhysicalControllerConnected => _inputManager?.IsPhysicalControllerConnected ?? false;

    public bool IsMacroEnabled => _inputManager?.IsMacroEnabled ?? _settings.Macros.Enabled;

    public bool IsMacroRunning => _inputManager?.IsMacroRunning ?? false;

    public long ActionsSent => _inputManager?.ActionsSent ?? 0;

    public string? OutputError => _inputManager?.OutputError;

    public ControllerState CurrentOutput => Manager.CurrentOutput;

    public KeyboardManager Keyboard => Manager.Keyboard;

    public MouseManager Mouse => Manager.Mouse;

    public IVirtualController? Controller => _inputManager?.Controller;

    public int ActiveWeapon => _inputManager?.ActiveWeapon ?? (_settings.AntiRecoil.UseArma2 ? 2 : 1);
    public int PhysicalInputSlot
    {
        get => _inputManager?.PhysicalInputSlot ?? _settings.PhysicalInputSlot;
        set
        {
            if (_inputManager is not null)
                _inputManager.PhysicalInputSlot = value;
            _settings.PhysicalInputSlot = value;
        }
    }

    public event Action? StateChanged;

    public event Action? WeaponChanged;

    public bool Activate() => Manager.Activate();

    public void Deactivate() => Manager.Deactivate();

    public void Toggle() => Manager.Toggle();

    public void SelectWeapon(int weapon) => Manager.SelectWeapon(weapon);

    public void Start() => Manager.Start();

    public void Stop() => Manager.Stop();

    public void Dispose()
    {
        Stop();
        if (_inputManager is not null)
        {
            if (_subscribed)
                _inputManager.EnabledChanged -= _ => StateChanged?.Invoke();
            _inputManager.Dispose();
            _inputManager = null;
        }

        if (_ownsPhysicalSource)
            _physicalSource.Dispose();

        _deviceManager.Dispose();
    }
}
