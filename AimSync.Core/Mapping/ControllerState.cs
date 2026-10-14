namespace AimSync.Core.Mapping;

public sealed class ControllerState
{
    private const int ButtonCount = 16;
    private readonly bool[] _buttons = new bool[ButtonCount];

    public float LeftX { get; set; }
    public float LeftY { get; set; }
    public float RightX { get; set; }
    public float RightY { get; set; }
    public byte LeftTrigger { get; set; }
    public byte RightTrigger { get; set; }

    public bool GetButton(ControllerButton button) => _buttons[(int)button];

    public void SetButton(ControllerButton button, bool pressed) => _buttons[(int)button] = pressed;

    public void Reset()
    {
        LeftX = 0f;
        LeftY = 0f;
        RightX = 0f;
        RightY = 0f;
        LeftTrigger = 0;
        RightTrigger = 0;
        Array.Clear(_buttons);
    }
}