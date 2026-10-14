using System.Windows;
using System.Windows.Threading;
using AimSync.App.Infrastructure;
using AimSync.App.Services;

namespace AimSync.App.ViewModels;

public sealed class DashboardViewModel : ObservableObject, IDisposable
{
    private readonly IAimSyncService _service;
    private readonly DispatcherTimer _timer;
    private string _lastMessage = string.Empty;

    public DashboardViewModel(IAimSyncService service)
    {
        _service = service;
        ActivateCommand = new RelayCommand(Activate, () => !IsEnabled);
        DeactivateCommand = new RelayCommand(Deactivate, () => IsEnabled);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(160) };
        _timer.Tick += (_, _) => Refresh();
        service.StateChanged += OnStateChanged;
        _timer.Start();
        Refresh();
    }

    public RelayCommand ActivateCommand { get; }

    public RelayCommand DeactivateCommand { get; }

    public string KeyboardStatus => "Conectado";

    public string MouseStatus => "Conectado";

    public bool IsEnabled => _service.IsEnabled;

    public bool IsConnected => _service.IsConnected;

    public bool IsLoopRunning => _service.IsLoopRunning;

    public string ControllerStatus =>
        IsEnabled
            ? (IsConnected ? "Conectado" : "Conectando...")
            : "Desconectado";

    public string ProfileName => "Por defecto";

    public string EngineStatus => IsEnabled ? "Activo" : "Inactivo";

    public string PhysicalControllerStatus =>
        _service.IsPhysicalControllerConnected ? "Conectado" : "Desconectado";

    public string MacroStatus =>
        !_service.IsMacroEnabled
            ? "Deshabilitada"
            : (_service.IsMacroRunning ? "Ejecutando" : "Lista");

    public string ActionsSentText => _service.ActionsSent.ToString();

    public bool HasOutputError => !string.IsNullOrEmpty(_service.OutputError);

    public string OutputErrorText => _service.OutputError ?? "Sin errores";

    public string ToggleKeyLabel => KeyNames.Key(_service.Settings.ToggleKey);

    public string SystemStateText
    {
        get
        {
            if (!IsEnabled)
                return "SISTEMA INACTIVO";
            if (HasOutputError)
                return "ERROR DE SALIDA";
            if (!IsConnected)
                return "ACTIVADO · SIN MANDO VIRTUAL";
            if (!IsLoopRunning)
                return "ACTIVADO · BUCLE DETENIDO";
            return "SISTEMA FUNCIONANDO";
        }
    }

    public string SystemStatusBrushKey
    {
        get
        {
            if (!IsEnabled)
                return "INACTIVO";
            if (HasOutputError)
                return "ERROR";
            if (!IsConnected || !IsLoopRunning)
                return "WARNING";
            return "ACTIVO";
        }
    }

    public string LastMessage
    {
        get => _lastMessage;
        private set => SetProperty(ref _lastMessage, value);
    }

    private void Activate()
    {
        bool ok = _service.Activate();
        LastMessage = ok
            ? $"Mando virtual conectado. Activado={_service.IsEnabled} Conectado={_service.IsConnected}"
            : $"No se pudo activar: {_service.LastError ?? "error desconocido"}";
        Refresh();
    }

    private void Deactivate()
    {
        _service.Deactivate();
        LastMessage = "Sistema desactivado. Mando reiniciado a neutro.";
        Refresh();
    }

    private void OnStateChanged()
    {
        if (Application.Current?.Dispatcher is { } dispatcher)
            dispatcher.BeginInvoke(Refresh);
        else
            Refresh();
    }

    private void Refresh()
    {
        OnPropertyChanged(nameof(IsEnabled));
        OnPropertyChanged(nameof(IsConnected));
        OnPropertyChanged(nameof(IsLoopRunning));
        OnPropertyChanged(nameof(ControllerStatus));
        OnPropertyChanged(nameof(EngineStatus));
        OnPropertyChanged(nameof(PhysicalControllerStatus));
        OnPropertyChanged(nameof(MacroStatus));
        OnPropertyChanged(nameof(ActionsSentText));
        OnPropertyChanged(nameof(HasOutputError));
        OnPropertyChanged(nameof(OutputErrorText));
        OnPropertyChanged(nameof(SystemStateText));
        OnPropertyChanged(nameof(SystemStatusBrushKey));
        ActivateCommand.RaiseCanExecuteChanged();
        DeactivateCommand.RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        _timer.Stop();
        _service.StateChanged -= OnStateChanged;
    }
}