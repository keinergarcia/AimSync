namespace AimSync.Core.Devices;

public enum PhysicalControllerStatus
{
    Connected,
    Disconnected,
    Error,
}

/// <summary>
/// Descripcion de un mando fisico detectado. El nombre proviene de Windows
/// (PnP). No se inventan nombres comerciales: si Windows devuelve un nombre
/// generico, se muestra tal cual.
/// </summary>
public sealed record PhysicalControllerDevice(
    int Index,
    string Name,
    string ApiName,
    PhysicalControllerStatus Status,
    string? InstanceId = null,
    string? HardwareId = null,
    ushort? VendorId = null,
    ushort? ProductId = null,
    string? Problem = null)
{
    public bool IsConnected => Status == PhysicalControllerStatus.Connected;

    public string StatusText => Status switch
    {
        PhysicalControllerStatus.Connected => "Conectado",
        PhysicalControllerStatus.Disconnected => "Desconectado",
        _ => "Error",
    };

    public string? VendorVid => VendorId is { } v ? $"0x{v:X4}" : null;
}
