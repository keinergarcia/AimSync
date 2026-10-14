using AimSync.Core;
using AimSync.Core.Input;

namespace AimSync.App.ViewModels;

public static class KeyNames
{
    public static string Key(KeyCode key) => key switch
    {
        KeyCode.None => "-",
        KeyCode.Space => "Space",
        KeyCode.Enter => "Enter",
        KeyCode.Backspace => "Backspace",
        KeyCode.Tab => "Tab",
        KeyCode.Escape => "Esc",
        KeyCode.LeftControl => "Left Ctrl",
        KeyCode.RightControl => "Right Ctrl",
        KeyCode.LeftShift => "Left Shift",
        KeyCode.RightShift => "Right Shift",
        KeyCode.LeftAlt => "Left Alt",
        KeyCode.RightAlt => "Right Alt",
        KeyCode.LeftArrow => "Left",
        KeyCode.RightArrow => "Right",
        KeyCode.UpArrow => "Up",
        KeyCode.DownArrow => "Down",
        KeyCode.OemSemicolon => ";",
        KeyCode.OemPlus => "+",
        KeyCode.OemComma => ",",
        KeyCode.OemMinus => "-",
        KeyCode.OemPeriod => ".",
        KeyCode.OemQuestion => "/",
        KeyCode.OemTilde => "`",
        KeyCode.OemOpenBrackets => "[",
        KeyCode.OemCloseBrackets => "]",
        KeyCode.OemQuotes => "'",
        KeyCode.OemBackslash => "\\",
        _ when key >= KeyCode.Digit0 && key <= KeyCode.Digit9 => ((int)key - (int)KeyCode.Digit0).ToString(),
        _ => key.ToString(),
    };

    public static string Mouse(MouseButton button) => button switch
    {
        MouseButton.Left => "Left Click",
        MouseButton.Right => "Right Click",
        MouseButton.Middle => "Middle Click",
        MouseButton.XButton1 => "X1",
        MouseButton.XButton2 => "X2",
        _ => button.ToString(),
    };

    public static string Button(ControllerButton button) => button switch
    {
        ControllerButton.LeftShoulder => "LB",
        ControllerButton.RightShoulder => "RB",
        ControllerButton.LeftThumb => "LS",
        ControllerButton.RightThumb => "RS",
        ControllerButton.DPadUp => "DPad Up",
        ControllerButton.DPadDown => "DPad Down",
        ControllerButton.DPadLeft => "DPad Left",
        ControllerButton.DPadRight => "DPad Right",
        _ => button.ToString(),
    };
}