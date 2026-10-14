namespace AimSync.Core.Devices;

/// <summary>
/// Botones expuestos por XInput (mascara wButtons). Los nombres siguen la
/// convencion Xbox: View/Back = 0x20, Menu/Start = 0x10, Nexus/Guide = 0x400.
/// XInputGetState no entrega de forma fiable el boton Nexus en todos los
/// dispositivos; si el mando no lo reporta permanece en 0.
/// </summary>
[Flags]
public enum PhysicalButtons : ushort
{
    None = 0x0000,
    DPadUp = 0x0001,
    DPadDown = 0x0002,
    DPadLeft = 0x0004,
    DPadRight = 0x0008,
    Menu = 0x0010,
    View = 0x0020,
    LeftStick = 0x0040,
    RightStick = 0x0080,
    LeftShoulder = 0x0100,
    RightShoulder = 0x0200,
    Nexus = 0x0400,
    A = 0x1000,
    B = 0x2000,
    X = 0x4000,
    Y = 0x8000,
}
