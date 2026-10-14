using System.Runtime.InteropServices;
using System.Text;

namespace AimSync.Core.Devices;

/// <summary>
/// Enumera dispositivos de juego reconocidos por Windows usando SetupAPI.
/// Obtiene el nombre real (FriendlyName/DeviceDesc), el InstanceId, el
/// HardwareId y el estado de problema, sin depender de WMI ni de paquetes
/// externos. Los datos se muestran tal cual los entrega Windows.
/// </summary>
public sealed class WindowsDeviceProbe : IWindowsDeviceProbe
{
    // Clase "Xbox Composite Device" (mandos XInput) y clase HID.
    private static readonly Guid XnaCompositeClass = new("D61CA365-5AF4-4486-998B-9DB4734C6CA3");
    private static readonly Guid HidClass = new("745A17A0-74D3-11D0-B6FE-00A0C90F57DA");

    private const uint DigcfPresent = 0x00000002;
    private const uint SpdrpDeviceDesc = 0x00000000;
    private const uint SpdrpHardwareId = 0x00000001;
    private const uint SpdrpFriendlyName = 0x0000000C;

    public IReadOnlyList<WindowsGameDevice> Enumerate()
    {
        var results = new List<WindowsGameDevice>();

        try
        {
            foreach (var device in EnumerateClass(XnaCompositeClass, isXInput: true))
            {
                if (device.Present)
                    results.Add(device);
            }

            foreach (var device in EnumerateClass(HidClass, isXInput: false))
            {
                if (!device.Present)
                    continue;

                if (LooksLikeGameController(device))
                    results.Add(device);
            }
        }
        catch
        {
            // La sonda es informativa: nunca debe tumbar la aplicacion.
        }

        return results;
    }

    private static bool LooksLikeGameController(WindowsGameDevice device)
    {
        string name = device.Name;
        if (name.Contains("game", StringComparison.OrdinalIgnoreCase)
            || name.Contains("joy", StringComparison.OrdinalIgnoreCase)
            || name.Contains("xbox", StringComparison.OrdinalIgnoreCase)
            || name.Contains("controller", StringComparison.OrdinalIgnoreCase)
            || name.Contains("mando", StringComparison.OrdinalIgnoreCase)
            || name.Contains("controlador", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Colecciones de juego HID reportan "IG_" en el InstanceId.
        return device.InstanceId.Contains("IG_", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<WindowsGameDevice> EnumerateClass(Guid classGuid, bool isXInput)
    {
        IntPtr deviceInfoSet = SetupDiGetClassDevs(ref classGuid, IntPtr.Zero, IntPtr.Zero, DigcfPresent);
        if (deviceInfoSet == IntPtr.Zero || deviceInfoSet == new IntPtr(-1))
            yield break;

        try
        {
            var info = new SpDevInfoData();
            info.cbSize = (uint)Marshal.SizeOf<SpDevInfoData>();

            for (uint index = 0; SetupDiEnumDeviceInfo(deviceInfoSet, index, ref info); index++)
            {
                string instanceId = GetInstanceId(deviceInfoSet, ref info);
                string? hardwareId = GetMultiStringProperty(deviceInfoSet, ref info, SpdrpHardwareId);
                string name = GetStringProperty(deviceInfoSet, ref info, SpdrpFriendlyName)
                              ?? GetStringProperty(deviceInfoSet, ref info, SpdrpDeviceDesc)
                              ?? "Dispositivo de juego (sin nombre reportado por Windows)";

                (ushort? vid, ushort? pid) = ParseVidPid(hardwareId) ?? ParseVidPid(instanceId) ?? (null, null);
                string? problem = GetProblem(info.DevInst);

                yield return new WindowsGameDevice(
                    name.Trim(),
                    instanceId,
                    hardwareId,
                    vid,
                    pid,
                    Present: true,
                    problem,
                    IsXInput: isXInput);
            }
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(deviceInfoSet);
        }
    }

    private static string GetInstanceId(IntPtr deviceInfoSet, ref SpDevInfoData info)
    {
        uint required = 0;
        SetupDiGetDeviceInstanceId(deviceInfoSet, ref info, null, 0, out required);
        if (required == 0)
            return string.Empty;

        var buffer = new StringBuilder((int)required + 1);
        return SetupDiGetDeviceInstanceId(deviceInfoSet, ref info, buffer, (uint)buffer.Capacity, out required)
            ? buffer.ToString()
            : string.Empty;
    }

    private static string? GetStringProperty(IntPtr deviceInfoSet, ref SpDevInfoData info, uint property)
    {
        byte[]? data = GetRawProperty(deviceInfoSet, ref info, property);
        if (data is null)
            return null;

        string value = Encoding.Unicode.GetString(data).TrimEnd('\0');
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? GetMultiStringProperty(IntPtr deviceInfoSet, ref SpDevInfoData info, uint property)
    {
        byte[]? data = GetRawProperty(deviceInfoSet, ref info, property);
        if (data is null)
            return null;

        string value = Encoding.Unicode.GetString(data).TrimEnd('\0');
        int separator = value.IndexOf('\0');
        return separator >= 0 ? value[..separator] : value;
    }

    private static byte[]? GetRawProperty(IntPtr deviceInfoSet, ref SpDevInfoData info, uint property)
    {
        uint required = 0;
        SetupDiGetDeviceRegistryProperty(deviceInfoSet, ref info, property, out _, null, 0, out required);
        if (required == 0)
            return null;

        var buffer = new byte[required];
        return SetupDiGetDeviceRegistryProperty(deviceInfoSet, ref info, property, out _, buffer, required, out _)
            ? buffer
            : null;
    }

    private static string? GetProblem(uint devNode)
    {
        try
        {
            int result = CM_Get_DevNode_Status(out _, out uint problem, devNode, 0);
            return result == 0 && problem != 0 ? $"Codigo de problema {problem}" : null;
        }
        catch
        {
            return null;
        }
    }

    private static (ushort? Vid, ushort? Pid)? ParseVidPid(string? source)
    {
        if (string.IsNullOrEmpty(source))
            return null;

        ushort? vid = ExtractId(source, "VID_");
        ushort? pid = ExtractId(source, "PID_");
        return vid is null && pid is null ? null : (vid, pid);
    }

    private static ushort? ExtractId(string source, string token)
    {
        int at = source.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        if (at < 0)
            return null;

        int start = at + token.Length;
        if (start + 4 > source.Length)
            return null;

        string hex = source.Substring(start, 4);
        return ushort.TryParse(hex, System.Globalization.NumberStyles.HexNumber,
            System.Globalization.CultureInfo.InvariantCulture, out ushort value)
            ? value
            : null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SpDevInfoData
    {
        public uint cbSize;
        public Guid ClassGuid;
        public uint DevInst;
        public IntPtr Reserved;
    }

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, IntPtr enumerator, IntPtr hwndParent, uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInfo(IntPtr deviceInfoSet, uint memberIndex, ref SpDevInfoData deviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiGetDeviceInstanceId(IntPtr deviceInfoSet, ref SpDevInfoData deviceInfoData,
        StringBuilder? deviceInstanceId, uint deviceInstanceIdSize, out uint requiredSize);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiGetDeviceRegistryProperty(IntPtr deviceInfoSet, ref SpDevInfoData deviceInfoData,
        uint property, out uint propertyRegDataType, byte[]? propertyBuffer, uint propertyBufferSize, out uint requiredSize);

    [DllImport("cfgmgr32.dll", SetLastError = true)]
    private static extern int CM_Get_DevNode_Status(out uint status, out uint problemNumber, uint devInst, uint flags);
}
