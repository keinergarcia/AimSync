using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using AimSync.App.Infrastructure;
using AimSync.App.Services;
using AimSync.Core.Devices;

namespace AimSync.App.ViewModels;

public sealed class ControllerViewModel : ObservableObject, IDisposable
{
    private readonly IAimSyncService _service;
    private readonly PhysicalControllerMonitor _monitor;
    private readonly DispatcherTimer _timer;
    private PhysicalControllerSnapshot? _current;
    private ControllerSnapshot? _virtualOutput;
    private PhysicalControllerDevice? _selectedDevice;
    private bool _disposed;

    public ControllerViewModel(IAimSyncService service, PhysicalControllerMonitor monitor)
    {
        _service = service;
        _monitor = monitor;

        Devices = [];
        RefreshCommand = new RelayCommand(RefreshDevices);

        _monitor.DevicesChanged += OnDevicesChanged;
        _monitor.Error += OnMonitorError;
        _monitor.Start();

        SyncDevices();
        Current = BuildSnapshot();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
    }

    public ObservableCollection<PhysicalControllerDevice> Devices { get; }

    public RelayCommand RefreshCommand { get; }

    public PhysicalControllerSnapshot? Current
    {
        get => _current;
        private set => SetProperty(ref _current, value);
    }

    /// <summary>Salida del mando virtual (seccion heredada; se conserva).</summary>
    public ControllerSnapshot? VirtualOutput
    {
        get => _virtualOutput;
        private set => SetProperty(ref _virtualOutput, value);
    }

    public PhysicalControllerDevice? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (SetProperty(ref _selectedDevice, value) && value is not null)
            {
                _monitor.SelectIndex(value.Index);
                _service.PhysicalInputSlot = value.Index;
                Current = BuildSnapshot();
            }
        }
    }

    public bool IsApiAvailable => _monitor.IsAvailable;

    public string ApiName => _monitor.ApiName;

    public string DeviceCountText => Devices.Count == 0
        ? "Sin mandos detectados"
        : $"{Devices.Count} mando(s) detectado(s)";

    private void Tick()
    {
        Current = BuildSnapshot();
        VirtualOutput = ControllerSnapshot.From(_service.CurrentOutput);
    }

    private PhysicalControllerSnapshot BuildSnapshot()
    {
        var device = _selectedDevice;
        return PhysicalControllerSnapshot.From(
            _monitor.Latest,
            device,
            _monitor.UpdateRateHz,
            _monitor.IsAvailable,
            _monitor.ApiName);
    }

    private void OnDevicesChanged()
    {
        if (Application.Current?.Dispatcher is { } dispatcher)
            dispatcher.BeginInvoke(SyncDevices);
        else
            SyncDevices();
    }

    private void OnMonitorError(string message)
    {
        if (Application.Current?.Dispatcher is { } dispatcher)
            dispatcher.BeginInvoke(RefreshDevices);
        else
            RefreshDevices();
    }

    private void RefreshDevices()
    {
        SyncDevices();
        Current = BuildSnapshot();
    }

    private void SyncDevices()
    {
        var snapshot = _monitor.Devices;
        int previousIndex = _selectedDevice?.Index ?? -1;

        Devices.Clear();
        foreach (var device in snapshot)
            Devices.Add(device);

        PhysicalControllerDevice? selected = null;
        if (previousIndex >= 0)
            selected = Devices.FirstOrDefault(d => d.Index == previousIndex);

        selected ??= Devices.FirstOrDefault(d => d.IsConnected) ?? Devices.FirstOrDefault();
        _selectedDevice = selected;

        if (selected is not null)
        {
            _monitor.SelectIndex(selected.Index);
            _service.PhysicalInputSlot = selected.Index;
        }

        OnPropertyChanged(nameof(SelectedDevice));
        OnPropertyChanged(nameof(DeviceCountText));
        OnPropertyChanged(nameof(IsApiAvailable));
        OnPropertyChanged(nameof(ApiName));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _timer.Stop();
        _monitor.DevicesChanged -= OnDevicesChanged;
        _monitor.Error -= OnMonitorError;
    }
}
