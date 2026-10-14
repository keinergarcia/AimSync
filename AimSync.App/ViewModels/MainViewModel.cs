using System.Windows;
using System.Windows.Threading;
using AimSync.App.Infrastructure;
using AimSync.App.Services;
using AimSync.Core.Devices;
using AimSync.Core.Storage;

namespace AimSync.App.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private const string DashboardIcon = "M3,3 H10 V10 H3 Z M14,3 H21 V10 H14 Z M3,14 H10 V21 H3 Z M14,14 H21 V21 H14 Z";
    private const string ControllerIcon = "M7,9 C5.9,9 5,9.9 5,11 V13 H3.5 C2.7,13 2,13.7 2,14.5 C2,15.3 2.7,16 3.5,16 H5.4 C6.2,17.8 8,19 10.2,19 H13.8 C16,19 17.8,17.8 18.6,16 H20.5 C21.3,16 22,15.3 22,14.5 C22,13.7 21.3,13 20.5,13 H19 V11 C19,9.9 18.1,9 17,9 Z";
    private const string MouseIcon = "M5,4 C5,2.9 5.9,2 7,2 H17 C18.1,2 19,2.9 19,4 V13 C19,15.8 16.3,17.5 13,17.5 H11 C7.7,17.5 5,15.8 5,13 Z M12,2 V7";
    private const string KeyboardIcon = "M3,7 H21 V16.5 C21,17.3 20.3,18 19.5,18 H4.5 C3.7,18 3,17.3 3,16.5 Z M6,10.5 H9 M11,10.5 H14 M16,10.5 H18 M6,14 H8";
    private const string ProfilesIcon = "M12,7 C9.8,7 8,8.8 8,11 C8,13.2 9.8,15 12,15 C14.2,15 16,13.2 16,11 C16,8.8 14.2,7 12,7 Z M5,18.5 C5,15.8 8,14 12,14 C16,14 19,15.8 19,18.5";
    private const string TesterIcon = "M2,12 H6 L9,4 L14,20 L17,12 H22";
    private const string SettingsIcon = "M2,6 H22 M6,3 V9 M2,12 H22 M15,9 V15 M2,18 H22 M9,15 V21";
    private const string RecoilIcon = "M12,3 V9 M12,21 V15 M3,12 H9 M21,12 H15 M12,12 L15,15";
    private const string TurboMacrosIcon = "M4,13 H11 L9,22 L19,8 H13 L15,2 Z";

    private readonly IAimSyncService _service;
    private readonly DispatcherTimer _statusTimer;
    private readonly PhysicalControllerMonitor _physicalController;
    private readonly bool _ownsPhysicalController;
    private NavItem? _selectedNavItem;
    private object? _selectedPage;
    private string _globalStateText = "INACTIVE";

    public MainViewModel(IAimSyncService service, PhysicalControllerMonitor? physicalController = null, SettingsStore? store = null)
    {
        _service = service;

        _ownsPhysicalController = physicalController is null;
        _physicalController = physicalController ?? new PhysicalControllerMonitor();

        Dashboard = new DashboardViewModel(service);
        Controller = new ControllerViewModel(service, _physicalController);
        Mouse = new MouseViewModel(service);
        AntiRecoil = new AntiRecoilViewModel(service, store);
        Macros = new MacrosViewModel(service, store);
        Keyboard = new KeyboardViewModel(service);
        InputTester = new InputTesterViewModel(service);
        Profiles = new ComingSoonViewModel("Perfiles",
            "Guarda y cambia entre configuraciones de entrada completas.\nNo forma parte de esta iteración.");
        Settings = new ComingSoonViewModel("Ajustes",
            "Opciones globales de AimSync.\nNo forma parte de esta iteración.");

        NavItems =
        [
            new NavItem("Panel", DashboardIcon, Dashboard),
            new NavItem("Controller", ControllerIcon, Controller),
            new NavItem("Mouse", MouseIcon, Mouse),
            new NavItem("Anti Recoil", RecoilIcon, AntiRecoil),
            new NavItem("Turbo y Macros", TurboMacrosIcon, Macros),
            new NavItem("Teclado", KeyboardIcon, Keyboard),
            new NavItem("Perfiles", ProfilesIcon, Profiles, IsComingSoon: true),
            new NavItem("Probador", TesterIcon, InputTester),
            new NavItem("Ajustes", SettingsIcon, Settings, IsComingSoon: true),
        ];
        SelectedNavItem = NavItems[0];

        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _statusTimer.Tick += (_, _) => RefreshStatus();
        _statusTimer.Start();
        service.StateChanged += OnStateChanged;
        RefreshStatus();
    }

    public DashboardViewModel Dashboard { get; }

    public ControllerViewModel Controller { get; }

    public MouseViewModel Mouse { get; }

    public AntiRecoilViewModel AntiRecoil { get; }

    public MacrosViewModel Macros { get; }

    public KeyboardViewModel Keyboard { get; }

    public InputTesterViewModel InputTester { get; }

    public ComingSoonViewModel Profiles { get; }

    public ComingSoonViewModel Settings { get; }

    public IReadOnlyList<NavItem> NavItems { get; }

    public string AppName => "AIMSYNC";

    public string AppVersion => "v0.2.0 · Phase 2";

    public NavItem? SelectedNavItem
    {
        get => _selectedNavItem;
        set
        {
            if (SetProperty(ref _selectedNavItem, value) && value is not null)
                SelectedPage = value.ViewModel;
        }
    }

    public object? SelectedPage
    {
        get => _selectedPage;
        private set => SetProperty(ref _selectedPage, value);
    }

    public bool IsEnabled => _service.IsEnabled;

    public string GlobalStateText
    {
        get => _globalStateText;
        private set => SetProperty(ref _globalStateText, value);
    }

    public string GlobalStatusBrushKey
    {
        get
        {
            if (!IsEnabled)
                return "INACTIVO";
            if (!string.IsNullOrEmpty(_service.OutputError))
                return "ERROR";
            if (!_service.IsConnected || !_service.IsLoopRunning)
                return "WARNING";
            return "ACTIVO";
        }
    }

    public string ToggleKeyLabel => KeyNames.Key(_service.Settings.ToggleKey);

    private void OnStateChanged()
    {
        if (Application.Current?.Dispatcher is { } dispatcher)
            dispatcher.BeginInvoke(RefreshStatus);
        else
            RefreshStatus();
    }

    private void RefreshStatus()
    {
        if (!IsEnabled)
            GlobalStateText = "INACTIVE";
        else if (!string.IsNullOrEmpty(_service.OutputError))
            GlobalStateText = "OUTPUT ERROR";
        else if (!_service.IsConnected)
            GlobalStateText = "ACTIVE · NO OUTPUT";
        else if (!_service.IsLoopRunning)
            GlobalStateText = "ACTIVE · LOOP STOPPED";
        else
            GlobalStateText = "ACTIVE";
        OnPropertyChanged(nameof(GlobalStatusBrushKey));
    }

    public void Dispose()
    {
        _statusTimer.Stop();
        _service.StateChanged -= OnStateChanged;
        Dashboard.Dispose();
        Controller.Dispose();
        Mouse.Dispose();
        InputTester.Dispose();

        if (_ownsPhysicalController)
            _physicalController.Dispose();
    }
}