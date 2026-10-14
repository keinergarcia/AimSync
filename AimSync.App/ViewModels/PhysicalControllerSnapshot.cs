using AimSync.Core.Devices;

namespace AimSync.App.ViewModels;

/// <summary>
/// Instantanea para enlazar (binding) los datos reales del mando fisico.
/// Separa los valores crudos que entrega la API de los valores normalizados.
/// </summary>
public sealed record PhysicalControllerSnapshot(
    bool IsApiAvailable,
    string ApiName,
    string DeviceName,
    string StatusText,
    bool IsConnected,
    bool HasError,
    int Index,
    double LeftX,
    double LeftY,
    double RightX,
    double RightY,
    double LeftTriggerLevel,
    double RightTriggerLevel,
    short RawLeftX,
    short RawLeftY,
    short RawRightX,
    short RawRightY,
    byte RawLeftTrigger,
    byte RawRightTrigger,
    bool A,
    bool B,
    bool X,
    bool Y,
    bool LeftShoulder,
    bool RightShoulder,
    bool View,
    bool Menu,
    bool Nexus,
    bool LeftStick,
    bool RightStick,
    bool DPadUp,
    bool DPadDown,
    bool DPadLeft,
    bool DPadRight,
    string DPadDirection,
    uint PacketNumber,
    double UpdateRateHz,
    string PressedButtonsText)
{
    public static PhysicalControllerSnapshot Empty(string apiName, bool isApiAvailable) => new(
        isApiAvailable, apiName, "Ninguno detectado", "Desconectado", false, false, -1,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        false, false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, "Centro", 0, 0, "—");

    public static PhysicalControllerSnapshot From(PhysicalControllerState state, PhysicalControllerDevice? device, double hz, bool apiAvailable, string apiName)
    {
        string name = device?.Name ?? "Ninguno detectado";
        string status = device?.StatusText ?? "Desconectado";

        return new PhysicalControllerSnapshot(
            apiAvailable,
            apiName,
            name,
            status,
            state.IsConnected,
            device?.Status == PhysicalControllerStatus.Error,
            state.Index,
            state.LeftXNormalized,
            state.LeftYNormalized,
            state.RightXNormalized,
            state.RightYNormalized,
            state.LeftTriggerLevel,
            state.RightTriggerLevel,
            state.LeftX,
            state.LeftY,
            state.RightX,
            state.RightY,
            state.LeftTrigger,
            state.RightTrigger,
            state.Has(PhysicalButtons.A),
            state.Has(PhysicalButtons.B),
            state.Has(PhysicalButtons.X),
            state.Has(PhysicalButtons.Y),
            state.Has(PhysicalButtons.LeftShoulder),
            state.Has(PhysicalButtons.RightShoulder),
            state.Has(PhysicalButtons.View),
            state.Has(PhysicalButtons.Menu),
            state.Has(PhysicalButtons.Nexus),
            state.Has(PhysicalButtons.LeftStick),
            state.Has(PhysicalButtons.RightStick),
            state.Has(PhysicalButtons.DPadUp),
            state.Has(PhysicalButtons.DPadDown),
            state.Has(PhysicalButtons.DPadLeft),
            state.Has(PhysicalButtons.DPadRight),
            state.DPadDirection,
            state.PacketNumber,
            hz,
            DescribeButtons(state.Buttons));
    }

    private static string DescribeButtons(PhysicalButtons buttons)
    {
        if (buttons == PhysicalButtons.None)
            return "—";

        var parts = new List<string>();
        foreach (PhysicalButtons value in Enum.GetValues<PhysicalButtons>())
        {
            if (value == PhysicalButtons.None)
                continue;
            if ((buttons & value) != 0)
                parts.Add(Label(value));
        }

        return parts.Count == 0 ? "—" : string.Join(" + ", parts);
    }

    private static string Label(PhysicalButtons button) => button switch
    {
        PhysicalButtons.DPadUp => "DPad ↑",
        PhysicalButtons.DPadDown => "DPad ↓",
        PhysicalButtons.DPadLeft => "DPad ←",
        PhysicalButtons.DPadRight => "DPad →",
        PhysicalButtons.Menu => "Menu",
        PhysicalButtons.View => "View",
        PhysicalButtons.LeftStick => "LS",
        PhysicalButtons.RightStick => "RS",
        PhysicalButtons.LeftShoulder => "LB",
        PhysicalButtons.RightShoulder => "RB",
        PhysicalButtons.Nexus => "Guide",
        _ => button.ToString(),
    };
}
