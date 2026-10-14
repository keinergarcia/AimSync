using System.Runtime.InteropServices;

namespace AimSync.Core.Devices;

/// <summary>
/// Lectura de mandos fisicos mediante XInput (nativo de Windows). No requiere
/// dependencias externas. Carga dinamicamente xinput1_4 -> xinput1_3 ->
/// xinput9_1_0 para cubrir Windows 10/11 y versiones antiguas, y falla de
/// forma controlada si ninguna esta disponible.
/// </summary>
public sealed class XInputControllerSource : IPhysicalControllerSource
{
    private const uint ErrorSuccess = 0;
    private const uint ErrorDeviceNotConnected = 1167; // ERROR_DEVICE_NOT_CONNECTED
    private const int XInputSlotCount = 4;

    private delegate uint XInputGetStateDelegate(uint dwUserIndex, out NativeXInputState state);

    private static readonly string[] CandidateLibraries =
    [
        "xinput1_4",
        "xinput1_3",
        "xinput9_1_0",
    ];

    private readonly XInputGetStateDelegate? _getState;
    private readonly IntPtr _libraryHandle;
    private bool _disposed;

    public XInputControllerSource()
    {
        foreach (var candidate in CandidateLibraries)
        {
            if (!NativeLibrary.TryLoad(candidate, out IntPtr handle))
                continue;

            if (!NativeLibrary.TryGetExport(handle, "XInputGetState", out IntPtr export))
            {
                NativeLibrary.Free(handle);
                continue;
            }

            _libraryHandle = handle;
            _getState = Marshal.GetDelegateForFunctionPointer<XInputGetStateDelegate>(export);
            ApiName = $"XInput ({candidate})";
            IsAvailable = true;
            return;
        }

        ApiName = "XInput (no disponible)";
        IsAvailable = false;
    }

    public string ApiName { get; }

    public bool IsAvailable { get; }

    public int SlotCount => XInputSlotCount;

    public bool TryRead(int index, out PhysicalControllerState state)
    {
        state = PhysicalControllerState.Disconnected(index);

        if (_disposed || _getState is null || index < 0 || index >= XInputSlotCount)
            return false;

        uint result;
        NativeXInputState native;
        try
        {
            result = _getState((uint)index, out native);
        }
        catch (Exception)
        {
            // Nunca propagar una excepcion desde el hilo de sondeo.
            return false;
        }

        if (result == ErrorDeviceNotConnected)
            return false;

        if (result != ErrorSuccess)
            return false;

        state = new PhysicalControllerState(
            index,
            native.PacketNumber,
            true,
            native.Gamepad.ThumbLX,
            native.Gamepad.ThumbLY,
            native.Gamepad.ThumbRX,
            native.Gamepad.ThumbRY,
            native.Gamepad.LeftTrigger,
            native.Gamepad.RightTrigger,
            (PhysicalButtons)native.Gamepad.Buttons);

        return true;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_libraryHandle != IntPtr.Zero)
            NativeLibrary.Free(_libraryHandle);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeXInputGamepad
    {
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short ThumbLX;
        public short ThumbLY;
        public short ThumbRX;
        public short ThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeXInputState
    {
        public uint PacketNumber;
        public NativeXInputGamepad Gamepad;
    }
}
