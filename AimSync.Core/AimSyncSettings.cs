using AimSync.Core.Engine;
using AimSync.Core.Input;
using AimSync.Core.Mapping;

namespace AimSync.Core;

public sealed class AimSyncSettings
{
    public KeyCode ToggleKey { get; set; } = KeyCode.F8;

    public TimeSpan LoopInterval { get; set; } = TimeSpan.FromMilliseconds(4);

    public MappingConfig Mapping { get; } = new();

    public StickEngine LeftStick { get; } = new(sensitivity: 1f, deadzone: 0f);

    public StickEngine RightStick { get; } = new(sensitivity: 1f, deadzone: 0f);

    public AntiRecoilConfig AntiRecoil { get; } = new();
    public int PhysicalInputSlot { get; set; }

    /// <summary>Procesamiento del gatillo derecho (RT) en modo normal: sensibilidad y umbral.</summary>
    public TriggerConfig RightTrigger { get; } = new();

    /// <summary>Procesamiento del gatillo izquierdo (LT) en modo normal: sensibilidad y umbral.</summary>
    public TriggerConfig LeftTrigger { get; } = new();

    public TurboConfig Turbo { get; } = new();

    public MacroConfig Macros { get; } = new();
}
