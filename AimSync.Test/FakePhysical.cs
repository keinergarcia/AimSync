using AimSync.Core.Devices;

namespace AimSync.Test;

internal sealed class FakePhysicalControllerSource : IPhysicalControllerSource
{
    private readonly Dictionary<int, PhysicalControllerState> _states = [];

    public string ApiName { get; set; } = "FakeXInput (test)";

    public bool IsAvailable { get; set; } = true;

    public int SlotCount { get; set; } = 4;

    public void SetDisconnected(int index, uint packet = 0)
    {
        _states[index] = PhysicalControllerState.Disconnected(index);
    }

    public void SetState(
        int index,
        short leftX = 0,
        short leftY = 0,
        short rightX = 0,
        short rightY = 0,
        byte leftTrigger = 0,
        byte rightTrigger = 0,
        PhysicalButtons buttons = PhysicalButtons.None,
        uint packet = 1)
    {
        _states[index] = new PhysicalControllerState(
            index, packet, true, leftX, leftY, rightX, rightY, leftTrigger, rightTrigger, buttons);
    }

    public bool TryRead(int index, out PhysicalControllerState state)
    {
        if (_states.TryGetValue(index, out PhysicalControllerState? stored) && stored.IsConnected)
        {
            state = stored;
            return true;
        }

        state = PhysicalControllerState.Disconnected(index);
        return false;
    }

    public void Dispose()
    {
    }
}

internal sealed class FakeWindowsDeviceProbe : IWindowsDeviceProbe
{
    public List<WindowsGameDevice> Devices { get; } = [];

    public void Add(string name, string instanceId, string? hardwareId, ushort? vid, ushort? pid, bool present = true, string? problem = null, bool isXInput = true)
        => Devices.Add(new WindowsGameDevice(name, instanceId, hardwareId, vid, pid, present, problem, isXInput));

    public IReadOnlyList<WindowsGameDevice> Enumerate() => Devices;
}
