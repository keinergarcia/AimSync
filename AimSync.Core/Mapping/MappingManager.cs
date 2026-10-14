using AimSync.Core.Input;

namespace AimSync.Core.Mapping;

public sealed class MappingManager
{
    private readonly MappingConfig _config;
    private readonly ControllerState _state = new();

    public MappingManager(MappingConfig config) => _config = config;

    public MappingConfig Config => _config;

    public ControllerState Process(KeyboardManager keyboard, MouseManager mouse)
    {
        _state.Reset();

        float moveX = (Pressed(keyboard, _config.MoveRight) ? 1f : 0f)
                    - (Pressed(keyboard, _config.MoveLeft) ? 1f : 0f);
        float moveY = (Pressed(keyboard, _config.MoveUp) ? 1f : 0f)
                    - (Pressed(keyboard, _config.MoveDown) ? 1f : 0f);
        float length = MathF.Sqrt(moveX * moveX + moveY * moveY);
        if (length > 1f)
        {
            moveX /= length;
            moveY /= length;
        }

        _state.LeftX = moveX;
        _state.LeftY = moveY;

        float aimX = _config.InvertMouseX ? -mouse.DeltaX : mouse.DeltaX;
        float aimY = _config.InvertMouseY ? mouse.DeltaY : -mouse.DeltaY;

        // Mouse ΔX/ΔY → Sensibilidad (per-axis) → Curva (LINEAL por ahora) → Clamp → Stick [-1, 1]
        float scaledX = aimX * _config.MouseSensitivityX;
        float scaledY = aimY * _config.MouseSensitivityY;

        _state.RightX = Clamp(scaledX / _config.MouseMaxDelta, -1f, 1f);
        _state.RightY = Clamp(scaledY / _config.MouseMaxDelta, -1f, 1f);

        _state.RightTrigger = mouse.IsButtonDown(_config.RightTriggerButton) ? byte.MaxValue : (byte)0;
        _state.LeftTrigger = mouse.IsButtonDown(_config.LeftTriggerButton) ? byte.MaxValue : (byte)0;

        foreach (var (key, button) in _config.ButtonMap)
        {
            if (Pressed(keyboard, key))
                _state.SetButton(button, true);
        }

        return _state;
    }

    private static bool Pressed(KeyboardManager keyboard, KeyCode key) => keyboard.IsPressed(key);

    private static float Clamp(float value, float min, float max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
}