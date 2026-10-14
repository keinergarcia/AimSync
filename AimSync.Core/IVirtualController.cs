namespace AimSync.Core;

public interface IVirtualController : IDisposable
{
    bool IsConnected { get; }

    string? LastError { get; }

    event Action? Connected;
    event Action? Disconnected;

    bool Connect();
    void Disconnect();

    void SetLeftStick(short x, short y);
    void SetRightStick(short x, short y);
    void SetLeftTrigger(byte value);
    void SetRightTrigger(byte value);
    void SetButton(ControllerButton button, bool pressed);

    /// <summary>
    /// Envia al dispositivo virtual el estado acumulado por las llamadas Set*.
    /// Se llama una sola vez por tick del bucle (en lugar de dejar que cada Set*
    /// dispare su propio reporte), para poder sostener tasas de ~1000 Hz.
    /// </summary>
    void Submit();

    void Reset();
}