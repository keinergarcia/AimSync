namespace AimSync.Core.Devices;

/// <summary>
/// Entrada de dispositivo de juego obtenida de Windows (PnP). Solo describe
/// lo que Windows reporta; no completa datos que el sistema no exponga.
/// </summary>
public sealed record WindowsGameDevice(
    string Name,
    string InstanceId,
    string? HardwareId,
    ushort? VendorId,
    ushort? ProductId,
    bool Present,
    string? Problem,
    bool IsXInput = false)
{
    public bool HasProblem => !string.IsNullOrEmpty(Problem);
}

/// <summary>
/// Enumera los dispositivos de juego que Windows reconoce, para obtener el
/// nombre real y el estado. Implementacion real via SetupAPI; las pruebas
/// usan una implementacion simulada.
/// </summary>
public interface IWindowsDeviceProbe
{
    IReadOnlyList<WindowsGameDevice> Enumerate();
}
