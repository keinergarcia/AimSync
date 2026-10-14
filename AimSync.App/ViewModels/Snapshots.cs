using AimSync.Core;
using AimSync.Core.Mapping;

namespace AimSync.App.ViewModels;

public sealed record ControllerSnapshot(
    double LeftX,
    double LeftY,
    double RightX,
    double RightY,
    double LeftTriggerLevel,
    double RightTriggerLevel,
    bool A,
    bool B,
    bool X,
    bool Y,
    bool LeftShoulder,
    bool RightShoulder,
    bool DPadUp,
    bool DPadDown,
    bool DPadLeft,
    bool DPadRight)
{
    public static ControllerSnapshot From(ControllerState state) => new(
        state.LeftX,
        state.LeftY,
        state.RightX,
        state.RightY,
        state.LeftTrigger / 255d,
        state.RightTrigger / 255d,
        state.GetButton(ControllerButton.A),
        state.GetButton(ControllerButton.B),
        state.GetButton(ControllerButton.X),
        state.GetButton(ControllerButton.Y),
        state.GetButton(ControllerButton.LeftShoulder),
        state.GetButton(ControllerButton.RightShoulder),
        state.GetButton(ControllerButton.DPadUp),
        state.GetButton(ControllerButton.DPadDown),
        state.GetButton(ControllerButton.DPadLeft),
        state.GetButton(ControllerButton.DPadRight));
}

public sealed record TesterSnapshot(
    bool W,
    bool A,
    bool S,
    bool D,
    int MouseDX,
    int MouseDY,
    int MouseScaledX,
    int MouseScaledY,
    double SensX,
    double SensY,
    double MaxDelta,
    bool IsEnabled,
    bool IsConnected,
    bool IsLoopRunning,
    bool LeftClick,
    bool RightClick,
    ControllerSnapshot Controller);

public sealed record MappingRow(string Input, string Output);

public sealed record NavItem(string Title, string IconData, object ViewModel, bool IsComingSoon = false);