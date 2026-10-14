using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AimSync.Core.Devices;

/// <summary>
/// Supervisa los mandos fisicos: sondea la fuente (XInput) en un hilo de
/// fondo, detecta conexiones/desconexiones en caliente, asigna el nombre real
/// de Windows (mejor esfuerzo) y publica el estado mas reciente de forma
/// segura para el hilo de UI. No dibuja ni procesa nada: solo lee y reporta.
/// </summary>
public sealed class PhysicalControllerMonitor : IDisposable
{
    private static readonly TimeSpan FastPoll = TimeSpan.FromTicks(5000);   // 0,5 ms (~2 kHz) con mando conectado
    private static readonly TimeSpan SlowPoll = TimeSpan.FromMilliseconds(16); // sin mando: no castigar la CPU

    private readonly IPhysicalControllerSource _source;
    private readonly IWindowsDeviceProbe _probe;
    private readonly TimeSpan _idlePollInterval;

    private readonly object _sync = new();
    private readonly Dictionary<int, string> _names = [];
    private readonly Dictionary<int, string> _hardwareIds = [];
    private readonly Dictionary<int, ushort?> _vids = [];
    private readonly Dictionary<int, ushort?> _pids = [];
    private readonly Dictionary<int, string?> _problems = [];
    private readonly HashSet<int> _everConnected = [];

    private PhysicalControllerDevice[] _devices = [];
    private PhysicalControllerState _latest = PhysicalControllerState.Disconnected(0);
    private int _selectedIndex;

    private Thread? _thread;
    private volatile bool _running;
    private bool _disposed;
    private bool _unavailableReported;

    private readonly Stopwatch _rateClock = Stopwatch.StartNew();
    private long _rateWindowStartTicks;
    private uint _lastPacket;
    private int _ratePackets;
    private double _updateRateHz;

    public PhysicalControllerMonitor(
        IPhysicalControllerSource? source = null,
        IWindowsDeviceProbe? probe = null,
        TimeSpan? pollInterval = null)
    {
        _source = source ?? new XInputControllerSource();
        _probe = probe ?? new WindowsDeviceProbe();
        _idlePollInterval = pollInterval ?? SlowPoll;
        _rateWindowStartTicks = _rateClock.ElapsedTicks;
    }

    public string ApiName => _source.ApiName;

    public bool IsAvailable => _source.IsAvailable;

    public bool IsRunning => _running;

    public IReadOnlyList<PhysicalControllerDevice> Devices => Volatile.Read(ref _devices);

    public PhysicalControllerState Latest => Volatile.Read(ref _latest);

    public double UpdateRateHz => _updateRateHz;

    public int SelectedIndex
    {
        get => _selectedIndex;
        set => SelectIndex(value);
    }

    public event Action? DevicesChanged;
    public event Action<string>? Error;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_running)
            return;

        _running = true;
        _thread = new Thread(Loop)
        {
            IsBackground = true,
            Name = "AimSync.PhysicalControllerMonitor",
        };
        _thread.Start();
    }

    public void Stop()
    {
        if (!_running)
            return;

        _running = false;
        _thread?.Join(500);
        _thread = null;
    }

    /// <summary>Un unico sondeo sincronico. Expuesto para pruebas deterministas.</summary>
    public void PollOnce()
    {
        if (_disposed)
            return;

        if (!_source.IsAvailable)
        {
            if (!_unavailableReported)
            {
                _unavailableReported = true;
                Error?.Invoke("XInput no esta disponible en este sistema.");
            }

            return;
        }

        int slotCount = Math.Max(1, _source.SlotCount);
        var states = new PhysicalControllerState[slotCount];
        bool[] connected = new bool[slotCount];
        bool newlyConnected = false;

        for (int index = 0; index < slotCount; index++)
        {
            if (_source.TryRead(index, out PhysicalControllerState state))
            {
                connected[index] = true;
                states[index] = state;

                if (_everConnected.Add(index))
                    newlyConnected = true;
            }
            else
            {
                states[index] = PhysicalControllerState.Disconnected(index);
            }
        }

        if (newlyConnected)
            RefreshNames(connected);

        PhysicalControllerState selected = states[_selectedIndex];
        Volatile.Write(ref _latest, selected);
        TrackRate(selected.PacketNumber);

        var devices = new List<PhysicalControllerDevice>();
        for (int index = 0; index < slotCount; index++)
        {
            if (connected[index])
            {
                devices.Add(new PhysicalControllerDevice(
                    index, GetName(index, index), _source.ApiName, PhysicalControllerStatus.Connected,
                    GetHardwareId(index), GetHardwareId(index), GetVid(index), GetPid(index), null));
            }
            else if (_everConnected.Contains(index))
            {
                string? problem = GetProblem(index);
                devices.Add(new PhysicalControllerDevice(
                    index, GetName(index, index), _source.ApiName,
                    problem is null ? PhysicalControllerStatus.Disconnected : PhysicalControllerStatus.Error,
                    GetHardwareId(index), GetHardwareId(index), GetVid(index), GetPid(index), problem));
            }
        }

        PublishDevices(devices);
    }

    public void SelectIndex(int index)
    {
        if (index < 0 || index >= Math.Max(1, _source.SlotCount))
            return;

        _selectedIndex = index;

        if (_source.TryRead(index, out PhysicalControllerState state))
            Volatile.Write(ref _latest, state);
        else
            Volatile.Write(ref _latest, PhysicalControllerState.Disconnected(index));

        _lastPacket = 0;
        _ratePackets = 0;
        _updateRateHz = 0;
        _rateWindowStartTicks = _rateClock.ElapsedTicks;
    }

    private void TrackRate(uint packet)
    {
        if (packet != _lastPacket)
        {
            if (_lastPacket != 0)
                _ratePackets++;
            _lastPacket = packet;
        }

        long now = _rateClock.ElapsedTicks;
        double elapsedSeconds = (now - _rateWindowStartTicks) / (double)Stopwatch.Frequency;
        if (elapsedSeconds >= 1.0)
        {
            _updateRateHz = _ratePackets / elapsedSeconds;
            _ratePackets = 0;
            _rateWindowStartTicks = now;
        }
    }

    private void RefreshNames(bool[] connected)
    {
        IReadOnlyList<WindowsGameDevice> probed;
        try
        {
            probed = _probe.Enumerate();
        }
        catch
        {
            return;
        }

        var connectedSlots = new List<int>();
        for (int i = 0; i < connected.Length; i++)
        {
            if (connected[i])
                connectedSlots.Add(i);
        }

        // Se prefieren los dispositivos de la clase que XInput enlaza
        // (XnaComposite). Asi el nombre real de Windows se asigna aunque
        // tambien existan entradas HID del mismo mando.
        List<WindowsGameDevice> xinputCandidates = [];
        foreach (WindowsGameDevice device in probed)
        {
            if (device.IsXInput)
                xinputCandidates.Add(device);
        }

        IReadOnlyList<WindowsGameDevice> matched =
            xinputCandidates.Count == connectedSlots.Count && xinputCandidates.Count > 0
                ? xinputCandidates
                : probed;

        // Asignacion por orden, solo cuando la cuenta coincide (mejor esfuerzo).
        // Si no coincide, se conserva el nombre previo o el generico.
        if (matched.Count != connectedSlots.Count || matched.Count == 0)
            return;

        for (int i = 0; i < connectedSlots.Count; i++)
        {
            int slot = connectedSlots[i];
            WindowsGameDevice device = matched[i];

            lock (_sync)
            {
                _names[slot] = device.Name;
                _hardwareIds[slot] = device.HardwareId ?? device.InstanceId;
                _vids[slot] = device.VendorId;
                _pids[slot] = device.ProductId;
                _problems[slot] = device.Problem;
            }
        }
    }

    private void PublishDevices(List<PhysicalControllerDevice> devices)
    {
        var array = devices.ToArray();
        var previous = Volatile.Read(ref _devices);
        if (DevicesEqual(previous, array))
            return;

        Volatile.Write(ref _devices, array);
        DevicesChanged?.Invoke();
    }

    private static bool DevicesEqual(PhysicalControllerDevice[] a, PhysicalControllerDevice[] b)
    {
        if (a.Length != b.Length)
            return false;

        for (int i = 0; i < a.Length; i++)
        {
            if (a[i].Index != b[i].Index || a[i].Status != b[i].Status || a[i].Name != b[i].Name)
                return false;
        }

        return true;
    }

    private string GetName(int index, int slot)
    {
        lock (_sync)
            return _names.TryGetValue(index, out string? name) && !string.IsNullOrWhiteSpace(name)
                ? name
                : $"Mando XInput (slot {slot})";
    }

    private string? GetHardwareId(int index)
    {
        lock (_sync)
            return _hardwareIds.TryGetValue(index, out string? id) ? id : null;
    }

    private ushort? GetVid(int index)
    {
        lock (_sync)
            return _vids.TryGetValue(index, out ushort? v) ? v : null;
    }

    private ushort? GetPid(int index)
    {
        lock (_sync)
            return _pids.TryGetValue(index, out ushort? p) ? p : null;
    }

    private string? GetProblem(int index)
    {
        lock (_sync)
            return _problems.TryGetValue(index, out string? p) ? p : null;
    }

    private void Loop()
    {
        TimeBeginPeriod(1);

        try
        {
            long freq = Stopwatch.Frequency;
            while (_running)
            {
                PollOnce();

                // Con un mando conectado se sondea a alta frecuencia (para poder
                // medir su tasa real de reporte); sin mando, se descansa la CPU.
                bool connected = Volatile.Read(ref _latest).IsConnected;
                TimeSpan interval = connected ? FastPoll : _idlePollInterval;
                WaitPrecise(interval, freq);
            }
        }
        catch (Exception ex)
        {
            Error?.Invoke(ex.Message);
        }
        finally
        {
            TimeEndPeriod(1);
        }
    }

    private void WaitPrecise(TimeSpan interval, long freq)
    {
        long target = Stopwatch.GetTimestamp() + (interval.Ticks * freq / TimeSpan.TicksPerSecond);

        // Dormir la mayor parte y girar (spin) el ultimo tramo para lograr
        // resoluciones por debajo del milisegundo sin vaciar un nucleo entero.
        long sleepThreshold = freq / 200; // 5 ms
        long remaining = target - Stopwatch.GetTimestamp();
        if (remaining > sleepThreshold)
        {
            int sleepMs = (int)((remaining - sleepThreshold) * 1000 / freq);
            if (sleepMs > 0)
                Thread.Sleep(sleepMs);
        }

        while (_running && Stopwatch.GetTimestamp() < target)
            Thread.SpinWait(40);
    }

    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
    private static extern uint TimeBeginPeriod(uint milliseconds);

    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
    private static extern uint TimeEndPeriod(uint milliseconds);

    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();
        _disposed = true;
        _source.Dispose();
    }
}
