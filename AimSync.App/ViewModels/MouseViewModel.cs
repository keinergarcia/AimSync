using System.Windows.Threading;
using AimSync.App.Infrastructure;
using AimSync.App.Services;

namespace AimSync.App.ViewModels;

public sealed class MouseViewModel : ObservableObject, IDisposable
{
    private const double SensitivityMin = 0.05f;
    private const double SensitivityMax = 16f;
    private const double DeadzoneMin = 0f;
    private const double DeadzoneMax = 0.95f;
    private const double MaxDeltaMin = 20f;
    private const double MaxDeltaMax = 600f;

    private readonly IAimSyncService _service;
    private readonly DispatcherTimer _timer;
    private string _mouseOutX = "0.00";
    private string _mouseOutY = "0.00";

    public MouseViewModel(IAimSyncService service)
    {
        _service = service;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
        _timer.Tick += (_, _) => RefreshOutput();
        _timer.Start();
        RefreshOutput();
    }

    public double SensitivityMinimum => SensitivityMin;

    public double SensitivityMaximum => SensitivityMax;

    public double DeadzoneMinimum => DeadzoneMin;

    public double DeadzoneMaximum => DeadzoneMax;

    public double MaxDeltaMinimum => MaxDeltaMin;

    public double MaxDeltaMaximum => MaxDeltaMax;

    public double HorizontalSensitivity
    {
        get => _service.Settings.Mapping.MouseSensitivityX;
        set => SetMappingValue(() => _service.Settings.Mapping.MouseSensitivityX = (float)Math.Clamp(value, SensitivityMin, SensitivityMax), nameof(HorizontalSensitivity));
    }

    public double VerticalSensitivity
    {
        get => _service.Settings.Mapping.MouseSensitivityY;
        set => SetMappingValue(() => _service.Settings.Mapping.MouseSensitivityY = (float)Math.Clamp(value, SensitivityMin, SensitivityMax), nameof(VerticalSensitivity));
    }

    public double MouseMaxDelta
    {
        get => _service.Settings.Mapping.MouseMaxDelta;
        set => SetMappingValue(() => _service.Settings.Mapping.MouseMaxDelta = (float)Math.Clamp(value, MaxDeltaMin, MaxDeltaMax), nameof(MouseMaxDelta));
    }

    public double Deadzone
    {
        get => _service.Settings.RightStick.Deadzone;
        set
        {
            var clamped = (float)Math.Clamp((float)value, DeadzoneMin, DeadzoneMax);
            if (_service.Settings.RightStick.Deadzone == clamped)
                return;

            _service.Settings.RightStick.Deadzone = clamped;
            OnPropertyChanged();
        }
    }

    public bool InvertX
    {
        get => _service.Settings.Mapping.InvertMouseX;
        set
        {
            if (_service.Settings.Mapping.InvertMouseX == value)
                return;

            _service.Settings.Mapping.InvertMouseX = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(InvertXLabel));
        }
    }

    public bool InvertY
    {
        get => _service.Settings.Mapping.InvertMouseY;
        set
        {
            if (_service.Settings.Mapping.InvertMouseY == value)
                return;

            _service.Settings.Mapping.InvertMouseY = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(InvertYLabel));
        }
    }

    public string InvertXLabel => InvertX ? "SÍ" : "NO";

    public string InvertYLabel => InvertY ? "SÍ" : "NO";

    public string MouseOutX
    {
        get => _mouseOutX;
        private set => SetProperty(ref _mouseOutX, value);
    }

    public string MouseOutY
    {
        get => _mouseOutY;
        private set => SetProperty(ref _mouseOutY, value);
    }

    public string SensXLabel => HorizontalSensitivity.ToString("0.00");

    public string SensYLabel => VerticalSensitivity.ToString("0.00");

    public string MaxDeltaLabel => MouseMaxDelta.ToString("0");

    private void SetMappingValue(Action apply, string name)
    {
        apply();
        OnPropertyChanged(name);
        OnPropertyChanged(nameof(SensXLabel));
        OnPropertyChanged(nameof(SensYLabel));
        OnPropertyChanged(nameof(MaxDeltaLabel));
    }

    private void RefreshOutput()
    {
        MouseOutX = _service.CurrentOutput.RightX.ToString("0.00");
        MouseOutY = _service.CurrentOutput.RightY.ToString("0.00");
    }

    public void Dispose() => _timer.Stop();
}