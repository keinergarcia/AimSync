namespace AimSync.Core.Input;

public sealed class MouseManager
{
    private static readonly MouseButton[] AllButtons = Enum.GetValues<MouseButton>();

    private readonly IMouseCapture _capture;
    private readonly HashSet<MouseButton> _pressed = [];

    public event Action<MouseButton>? ButtonDown;
    public event Action<MouseButton>? ButtonUp;

    public IMouseCapture Capture => _capture;
    public IReadOnlySet<MouseButton> PressedButtons => _pressed;

    public int DeltaX { get; private set; }
    public int DeltaY { get; private set; }
    public int WheelDelta { get; private set; }

    public MouseManager(IMouseCapture capture) => _capture = capture;

    public bool IsButtonDown(MouseButton button) => _pressed.Contains(button);

    public void Poll()
    {
        _capture.TryTakeDeltas(out int deltaX, out int deltaY);
        DeltaX = deltaX;
        DeltaY = deltaY;

        _capture.TryTakeWheel(out int wheelDelta);
        WheelDelta = wheelDelta;

        foreach (var button in AllButtons)
        {
            bool down = _capture.IsButtonDown(button);
            bool was = _pressed.Contains(button);

            if (down && !was)
            {
                _pressed.Add(button);
                ButtonDown?.Invoke(button);
            }
            else if (!down && was)
            {
                _pressed.Remove(button);
                ButtonUp?.Invoke(button);
            }
        }
    }

    public void Reset()
    {
        _pressed.Clear();
        DeltaX = 0;
        DeltaY = 0;
        WheelDelta = 0;
    }
}