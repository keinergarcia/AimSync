using System.Windows.Threading;
using AimSync.App.Infrastructure;
using AimSync.App.Services;
using AimSync.Core.Input;

namespace AimSync.App.ViewModels;

public sealed class InputTesterViewModel : ObservableObject, IDisposable
{
    private readonly IAimSyncService _service;
    private readonly DispatcherTimer _timer;
    private TesterSnapshot? _current;
    private Services.DispatcherDiagnosticLogger? _logger;

    public InputTesterViewModel(IAimSyncService service)
    {
        _service = service;

        SetSensitivityCommand = new RelayCommand(SetSensitivity);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _timer.Tick += (_, _) =>
        {
            var settings = service.Settings.Mapping;
            float sensX = settings.MouseSensitivityX;
            float sensY = settings.MouseSensitivityY;

            Current = new TesterSnapshot(
                service.Keyboard.IsPressed(KeyCode.W),
                service.Keyboard.IsPressed(KeyCode.A),
                service.Keyboard.IsPressed(KeyCode.S),
                service.Keyboard.IsPressed(KeyCode.D),
                service.Mouse.DeltaX,
                service.Mouse.DeltaY,
                (int)MathF.Round(service.Mouse.DeltaX * sensX),
                (int)MathF.Round(service.Mouse.DeltaY * sensY),
                sensX,
                sensY,
                settings.MouseMaxDelta,
                service.IsEnabled,
                service.IsConnected,
                service.IsLoopRunning,
                service.Mouse.IsButtonDown(MouseButton.Left),
                service.Mouse.IsButtonDown(MouseButton.Right),
                ControllerSnapshot.From(service.CurrentOutput));
        };
        _timer.Start();
    }

    public void SetLogger(Services.DispatcherDiagnosticLogger logger) => _logger = logger;
    public System.Collections.ObjectModel.ObservableCollection<AimSync.Core.Diagnostics.DiagnosticEvent> DiagnosticEvents
        => _logger?.Events ?? new System.Collections.ObjectModel.ObservableCollection<AimSync.Core.Diagnostics.DiagnosticEvent>();

    public RelayCommand SetSensitivityCommand { get; }

    /// <summary>Etiqueta del bucle de entrada, derivada de la frecuencia real configurada.</summary>
    public string LoopRateText
    {
        get
        {
            double ms = Math.Max(1.0, _service.Settings.LoopInterval.TotalMilliseconds);
            return $"Bucle de entrada ({1000.0 / ms:0} Hz)";
        }
    }

    public TesterSnapshot? Current
    {
        get => _current;
        private set
        {
            if (ReferenceEquals(_current, value))
                return;
            _current = value;
            OnPropertyChanged();
        }
    }

    private void SetSensitivity(object? parameter)
    {
        if (parameter is not string text || !double.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out double value))
            return;

        _service.Settings.Mapping.MouseSensitivityX = (float)value;
        _service.Settings.Mapping.MouseSensitivityY = (float)value;
    }

    public void Dispose() => _timer.Stop();
}