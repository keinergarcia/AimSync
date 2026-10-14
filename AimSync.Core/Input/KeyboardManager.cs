namespace AimSync.Core.Input;

public sealed class KeyboardManager
{
    private readonly IKeyboardCapture _capture;
    private readonly HashSet<KeyCode> _pressed = [];
    private readonly HashSet<KeyCode> _newlyPressed = [];

    public event Action<KeyCode>? KeyDown;
    public event Action<KeyCode>? KeyUp;

    public IKeyboardCapture Capture => _capture;
    public IReadOnlySet<KeyCode> PressedKeys => _pressed;

    public KeyboardManager(IKeyboardCapture capture) => _capture = capture;

    public bool IsPressed(KeyCode key) => _pressed.Contains(key);

    public bool IsNewlyPressed(KeyCode key) => _newlyPressed.Contains(key);

    public void Poll(IEnumerable<KeyCode> monitoredKeys)
    {
        _newlyPressed.Clear();

        foreach (var key in monitoredKeys)
        {
            if (key == KeyCode.None)
                continue;

            bool down = _capture.IsKeyDown(key);
            bool was = _pressed.Contains(key);

            if (down && !was)
            {
                _pressed.Add(key);
                _newlyPressed.Add(key);
                KeyDown?.Invoke(key);
            }
            else if (!down && was)
            {
                _pressed.Remove(key);
                KeyUp?.Invoke(key);
            }
        }
    }

    public void Reset()
    {
        var released = new List<KeyCode>(_pressed);
        _pressed.Clear();
        _newlyPressed.Clear();

        foreach (var key in released)
            KeyUp?.Invoke(key);
    }
}