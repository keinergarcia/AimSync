using AimSync.Core;
using AimSync.Core.Devices;
using AimSync.Core.Input;

namespace AimSync.Test;

internal sealed class FakePhysicalSource : IPhysicalControllerSource
{
    public string ApiName => "fake";
    public bool IsAvailable => true;
    public int SlotCount => 1;

    private PhysicalControllerState _state = PhysicalControllerState.Disconnected(0);

    public PhysicalControllerState State => _state;

    public void DisconnectAll() => _state = PhysicalControllerState.Disconnected(0);

    public void Press(PhysicalButtons button) => _state = _state with
    {
        IsConnected = true,
        Buttons = _state.Buttons | button,
    };

    public void Release(PhysicalButtons button) => _state = _state with
    {
        IsConnected = true,
        Buttons = _state.Buttons & ~button,
    };

    public void SetTriggers(byte left = 0, byte right = 0) => _state = _state with
    {
        IsConnected = true,
        LeftTrigger = left,
        RightTrigger = right,
    };

    public void SetSticks(short leftX = 0, short leftY = 0, short rightX = 0, short rightY = 0) => _state = _state with
    {
        IsConnected = true,
        LeftX = leftX,
        LeftY = leftY,
        RightX = rightX,
        RightY = rightY,
    };

    public bool TryRead(int index, out PhysicalControllerState state)
    {
        state = _state;
        return _state.IsConnected && index == 0;
    }

    public void Dispose()
    {
    }
}

internal sealed class FakeKeyboardCapture : IKeyboardCapture
{
    private readonly HashSet<KeyCode> _down = [];

    public IReadOnlySet<KeyCode> DownKeys => _down;

    public void SetDown(KeyCode key, bool down)
    {
        if (down)
            _down.Add(key);
        else
            _down.Remove(key);
    }

    public void Press(params KeyCode[] keys)
    {
        foreach (var key in keys)
            _down.Add(key);
    }

    public void Release(params KeyCode[] keys)
    {
        foreach (var key in keys)
            _down.Remove(key);
    }

    public void ReleaseAll() => _down.Clear();

    public bool IsKeyDown(KeyCode key) => _down.Contains(key);
}

internal sealed class FakeMouseCapture : IMouseCapture
{
    private readonly HashSet<MouseButton> _down = [];
    private int _pendingDeltaX;
    private int _pendingDeltaY;
    private int _pendingWheel;

    public void SetDelta(int deltaX, int deltaY)
    {
        _pendingDeltaX = deltaX;
        _pendingDeltaY = deltaY;
    }

    public void SetWheel(int wheelDelta) => _pendingWheel = wheelDelta;

    public void ClearDelta()
    {
        _pendingDeltaX = 0;
        _pendingDeltaY = 0;
        _pendingWheel = 0;
    }

    public void SetButtonDown(MouseButton button, bool down)
    {
        if (down)
            _down.Add(button);
        else
            _down.Remove(button);
    }

    public bool TryTakeDeltas(out int deltaX, out int deltaY)
    {
        deltaX = _pendingDeltaX;
        deltaY = _pendingDeltaY;
        _pendingDeltaX = 0;
        _pendingDeltaY = 0;
        return true;
    }

    public bool TryTakeWheel(out int wheelDelta)
    {
        wheelDelta = _pendingWheel;
        _pendingWheel = 0;
        return true;
    }

    public bool IsButtonDown(MouseButton button) => _down.Contains(button);

    public void Dispose()
    {
    }
}

internal sealed class SpyVirtualController : IVirtualController
{
    public bool IsConnected { get; private set; }
    public string? LastError => null;
    public short LeftX { get; private set; }
    public short LeftY { get; private set; }
    public short RightX { get; private set; }
    public short RightY { get; private set; }
    public byte LeftTrigger { get; private set; }
    public byte RightTrigger { get; private set; }
    public int ConnectCount { get; private set; }
    public int DisconnectCount { get; private set; }
    public int ResetCount { get; private set; }

    private readonly bool[] _buttons = new bool[16];

    public event Action? Connected;
    public event Action? Disconnected;

    public bool GetButton(ControllerButton button) => _buttons[(int)button];

    public bool Connect()
    {
        ConnectCount++;
        IsConnected = true;
        Connected?.Invoke();
        return true;
    }

    public void Disconnect()
    {
        DisconnectCount++;
        IsConnected = false;
        Disconnected?.Invoke();
    }

    public void SetLeftStick(short x, short y)
    {
        LeftX = x;
        LeftY = y;
    }

    public void SetRightStick(short x, short y)
    {
        RightX = x;
        RightY = y;
    }

    public void SetLeftTrigger(byte value) => LeftTrigger = value;

    public void SetRightTrigger(byte value) => RightTrigger = value;

    public void SetButton(ControllerButton button, bool pressed) => _buttons[(int)button] = pressed;

    public int SubmitCount { get; private set; }

    public void Submit() => SubmitCount++;

    public void Reset()
    {
        ResetCount++;
        LeftX = 0;
        LeftY = 0;
        RightX = 0;
        RightY = 0;
        LeftTrigger = 0;
        RightTrigger = 0;
        Array.Clear(_buttons);
    }

    public bool IsNeutral =>
        LeftX == 0 && LeftY == 0 && RightX == 0 && RightY == 0 &&
        LeftTrigger == 0 && RightTrigger == 0 && !_buttons.Any(static b => b);

    public void Dispose()
    {
    }
}