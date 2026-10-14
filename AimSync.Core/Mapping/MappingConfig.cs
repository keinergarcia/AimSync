using AimSync.Core.Input;

namespace AimSync.Core.Mapping;

public sealed class MappingConfig
{
    private readonly Dictionary<KeyCode, ControllerButton> _buttonMap = [];

    public KeyCode MoveUp { get; set; } = KeyCode.W;
    public KeyCode MoveDown { get; set; } = KeyCode.S;
    public KeyCode MoveLeft { get; set; } = KeyCode.A;
    public KeyCode MoveRight { get; set; } = KeyCode.D;

    public MouseButton RightTriggerButton { get; set; } = MouseButton.Left;
    public MouseButton LeftTriggerButton { get; set; } = MouseButton.Right;

    public bool InvertMouseX { get; set; }

    public bool InvertMouseY { get; set; }

    public float MouseSensitivityX { get; set; } = 1f;

    public float MouseSensitivityY { get; set; } = 1f;

    public float MouseMaxDelta { get; set; } = 100f;

    public IReadOnlyDictionary<KeyCode, ControllerButton> ButtonMap => _buttonMap;

    public MappingConfig() => SetDefaultButtonMap();

    public void MapButton(KeyCode key, ControllerButton button) => _buttonMap[key] = button;

    public void ClearButton(KeyCode key) => _buttonMap.Remove(key);

    public void SetDefaultButtonMap()
    {
        _buttonMap.Clear();
        MapButton(KeyCode.Space, ControllerButton.A);
        MapButton(KeyCode.Q, ControllerButton.LeftShoulder);
        MapButton(KeyCode.E, ControllerButton.X);
        MapButton(KeyCode.R, ControllerButton.RightShoulder);
    }

    public IEnumerable<KeyCode> MonitoredKeys
    {
        get
        {
            yield return MoveUp;
            yield return MoveDown;
            yield return MoveLeft;
            yield return MoveRight;
            foreach (var key in _buttonMap.Keys)
                yield return key;
        }
    }
}