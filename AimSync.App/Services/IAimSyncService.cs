using AimSync.Core;
using AimSync.Core.Input;
using AimSync.Core.Mapping;

namespace AimSync.App.Services;

public interface IAimSyncService : IDisposable
{
    bool IsEnabled { get; }

    bool IsConnected { get; }

    bool IsLoopRunning { get; }

    string? LastError { get; }

    bool IsPhysicalControllerConnected { get; }

    bool IsMacroEnabled { get; }

    bool IsMacroRunning { get; }

    long ActionsSent { get; }

    string? OutputError { get; }

    AimSyncSettings Settings { get; }

    ControllerState CurrentOutput { get; }

    KeyboardManager Keyboard { get; }

    MouseManager Mouse { get; }

    IVirtualController? Controller { get; }

    int ActiveWeapon { get; }

    int PhysicalInputSlot { get; set; }

    event Action? StateChanged;

    event Action? WeaponChanged;

    bool Activate();

    void Deactivate();

    void Toggle();

    void SelectWeapon(int weapon);

    void Start();

    void Stop();
}
