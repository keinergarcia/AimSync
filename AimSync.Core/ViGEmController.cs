using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace AimSync.Core;

public sealed class ViGEmController : IVirtualController
{
    private readonly ViGEmClient _client;
    private readonly IXbox360Controller _controller;
    private bool _isConnected;
    private bool _disposed;
    private string? _lastError;

    public bool IsConnected => _isConnected;

    public string? LastError => _lastError;

    public event Action? Connected;
    public event Action? Disconnected;

    public ViGEmController()
    {
        _client = new ViGEmClient();
        _controller = _client.CreateXbox360Controller();

        // Un unico SubmitReport por tick (ver Submit). Sin esto, cada Set* envia su
        // propio reporte HID (~20 por tick), lo que impide acercarse a 1000 Hz.
        _controller.AutoSubmitReport = false;

        _controller.FeedbackReceived += OnFeedbackReceived;
    }

    public bool Connect()
    {
        ThrowIfDisposed();

        try
        {
            if (IsConnected)
                return true;

            _controller.Connect();
            _isConnected = true;
            _lastError = null;
            Connected?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            return false;
        }
    }

    public void Disconnect()
    {
        ThrowIfDisposed();

        if (!IsConnected)
            return;

        _isConnected = false;

        try
        {
            _controller.Disconnect();
            Disconnected?.Invoke();
        }
        catch
        {
        }
    }

    public void SetLeftStick(short x, short y)
    {
        ThrowIfDisposed();
        _controller.SetAxisValue(Xbox360Axis.LeftThumbX, x);
        _controller.SetAxisValue(Xbox360Axis.LeftThumbY, y);
    }

    public void SetRightStick(short x, short y)
    {
        ThrowIfDisposed();
        _controller.SetAxisValue(Xbox360Axis.RightThumbX, x);
        _controller.SetAxisValue(Xbox360Axis.RightThumbY, y);
    }

    public void SetLeftTrigger(byte value)
    {
        ThrowIfDisposed();
        _controller.SetSliderValue(Xbox360Slider.LeftTrigger, value);
    }

    public void SetRightTrigger(byte value)
    {
        ThrowIfDisposed();
        _controller.SetSliderValue(Xbox360Slider.RightTrigger, value);
    }

    public void SetButton(ControllerButton button, bool pressed)
    {
        ThrowIfDisposed();
        _controller.SetButtonState(MapButton(button), pressed);
    }

    public void Submit()
    {
        ThrowIfDisposed();
        _controller.SubmitReport();
    }

    public void Reset()
    {
        ThrowIfDisposed();
        _controller.SetAxisValue(Xbox360Axis.LeftThumbX, 0);
        _controller.SetAxisValue(Xbox360Axis.LeftThumbY, 0);
        _controller.SetAxisValue(Xbox360Axis.RightThumbX, 0);
        _controller.SetAxisValue(Xbox360Axis.RightThumbY, 0);
        _controller.SetSliderValue(Xbox360Slider.LeftTrigger, 0);
        _controller.SetSliderValue(Xbox360Slider.RightTrigger, 0);
        _controller.SetButtonState(Xbox360Button.A, false);
        _controller.SetButtonState(Xbox360Button.B, false);
        _controller.SetButtonState(Xbox360Button.X, false);
        _controller.SetButtonState(Xbox360Button.Y, false);
        _controller.SetButtonState(Xbox360Button.LeftShoulder, false);
        _controller.SetButtonState(Xbox360Button.RightShoulder, false);
        _controller.SetButtonState(Xbox360Button.Back, false);
        _controller.SetButtonState(Xbox360Button.Start, false);
        _controller.SetButtonState(Xbox360Button.Guide, false);
        _controller.SetButtonState(Xbox360Button.LeftThumb, false);
        _controller.SetButtonState(Xbox360Button.RightThumb, false);
        _controller.SetButtonState(Xbox360Button.Up, false);
        _controller.SetButtonState(Xbox360Button.Down, false);
        _controller.SetButtonState(Xbox360Button.Left, false);
        _controller.SetButtonState(Xbox360Button.Right, false);

        // El estado "en blanco" tambien debe llegar al dispositivo (AutoSubmitReport esta apagado).
        _controller.SubmitReport();
    }

    private void OnFeedbackReceived(object? sender, Xbox360FeedbackReceivedEventArgs e)
    {
    }

    private static Xbox360Button MapButton(ControllerButton button) => button switch
    {
        ControllerButton.A => Xbox360Button.A,
        ControllerButton.B => Xbox360Button.B,
        ControllerButton.X => Xbox360Button.X,
        ControllerButton.Y => Xbox360Button.Y,
        ControllerButton.LeftShoulder => Xbox360Button.LeftShoulder,
        ControllerButton.RightShoulder => Xbox360Button.RightShoulder,
        ControllerButton.Back => Xbox360Button.Back,
        ControllerButton.Start => Xbox360Button.Start,
        ControllerButton.Guide => Xbox360Button.Guide,
        ControllerButton.LeftThumb => Xbox360Button.LeftThumb,
        ControllerButton.RightThumb => Xbox360Button.RightThumb,
        ControllerButton.DPadUp => Xbox360Button.Up,
        ControllerButton.DPadDown => Xbox360Button.Down,
        ControllerButton.DPadLeft => Xbox360Button.Left,
        ControllerButton.DPadRight => Xbox360Button.Right,
        _ => throw new ArgumentOutOfRangeException(nameof(button), button, null),
    };

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            if (_isConnected)
            {
                _isConnected = false;
                _controller.Disconnect();
                Disconnected?.Invoke();
            }

            _controller.FeedbackReceived -= OnFeedbackReceived;
            _client.Dispose();
        }
        catch
        {
        }
    }
}