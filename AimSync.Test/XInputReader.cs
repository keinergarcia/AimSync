using System.Runtime.InteropServices;

namespace AimSync.Test;

internal static class XInputReader
{
    private const uint ErrorSuccess = 0;

    public static bool IsAvailable { get; private set; }

    public static int ConnectedSlotCount { get; private set; }

    public static string FormatSlots()
    {
        var slots = FindConnectedSlots();
        return slots.Count == 0 ? "none" : string.Join(",", slots);
    }

    public static string FormatSlots(List<uint> slots)
        => slots.Count == 0 ? "none" : string.Join(",", slots);

    public static string FormatGamepad(XInputGamepad? state) => state is null
        ? "n/a"
        : $"LX={state.Value.sThumbLX} LY={state.Value.sThumbLY} RX={state.Value.sThumbRX} RY={state.Value.sThumbRY} LT={state.Value.bLeftTrigger} RT={state.Value.bRightTrigger} B=0x{state.Value.wButtons:X4}";

    public static bool TryGetFirstState(out XInputState state)
    {
        state = default;
        for (uint user = 0; user < 4; user++)
        {
            if (!TryGetState(user, out var st) || st.dwPacketNumber == 0)
                continue;

            state = st;
            return true;
        }

        return false;
    }

    public static XInputState? ReadFirstConnected()
    {
        for (uint user = 0; user < 4; user++)
        {
            if (!TryGetState(user, out var st) || st.dwPacketNumber == 0)
                continue;

            return st;
        }

        return null;
    }

    public static XInputGamepad? ReadFirstGamepad() => ReadFirstConnected()?.Gamepad;

    public static bool TryGetStateVirtual(uint slot, out XInputState state)
        => TryGetState(slot, out state);

    public static void RefreshAvailability()
    {
        var slots = FindConnectedSlots();
        ConnectedSlotCount = slots.Count;
    }

    public static List<uint> FindConnectedSlots()
    {
        var slots = new List<uint>();
        for (uint user = 0; user < 4; user++)
        {
            if (TryGetState(user, out _))
                slots.Add(user);
        }

        return slots;
    }

    private static bool TryGetState(uint userIndex, out XInputState state)
    {
        state = default;
        try
        {
            uint result = XInputGetState(userIndex, out state);
            IsAvailable = true;
            return result == ErrorSuccess;
        }
        catch (DllNotFoundException)
        {
            IsAvailable = false;
            return false;
        }
        catch (BadImageFormatException)
        {
            IsAvailable = false;
            return false;
        }
    }

    [DllImport("xinput1_4.dll")]
    private static extern uint XInputGetState(uint dwUserIndex, out XInputState pState);

    [StructLayout(LayoutKind.Sequential)]
    public struct XInputGamepad
    {
        public ushort wButtons;
        public byte bLeftTrigger;
        public byte bRightTrigger;
        public short sThumbLX;
        public short sThumbLY;
        public short sThumbRX;
        public short sThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XInputState
    {
        public uint dwPacketNumber;
        public XInputGamepad Gamepad;
    }
}