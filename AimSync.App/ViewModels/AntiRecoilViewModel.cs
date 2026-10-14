using System.Windows.Threading;
using AimSync.App.Infrastructure;
using AimSync.App.Services;
using AimSync.Core.Storage;

namespace AimSync.App.ViewModels;

public sealed class AntiRecoilViewModel : ObservableObject, IDisposable
{
    private const double VMin = -1f;
    private const double VMax = 1f;
    private const double HMin = -1f;
    private const double HMax = 1f;
    private const double StrengthMin = 0f;
    private const double StrengthMax = 1f;
    private const double FreqMin = 0.5f;
    private const double FreqMax = 120f;

    private readonly IAimSyncService _service;
    private readonly SettingsStore? _store;
    private readonly DispatcherTimer _timer;

    public AntiRecoilViewModel(IAimSyncService service, SettingsStore? store = null)
    {
        _service = service;
        _store = store;
        _service.WeaponChanged += OnWeaponChanged;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _timer.Tick += (_, _) => RefreshLive();
        _timer.Start();
    }

    private void RefreshLive()
    {
        OnPropertyChanged(nameof(Firing));
        OnPropertyChanged(nameof(FiringText));
        OnPropertyChanged(nameof(OutputRightY));
        OnPropertyChanged(nameof(OutputRightX));
        OnPropertyChanged(nameof(OutputRightYRaw));
        OnPropertyChanged(nameof(OutputRightXRaw));
        OnPropertyChanged(nameof(OutputLeftX));
        OnPropertyChanged(nameof(OutputLeftXRaw));
    }

    private void OnWeaponChanged()
    {
        OnPropertyChanged(nameof(IsOff));
        OnPropertyChanged(nameof(IsArma1));
        OnPropertyChanged(nameof(IsArma2));
        OnPropertyChanged(nameof(ActiveWeapon));
        OnPropertyChanged(nameof(ActiveWeaponDisplay));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusHint));
        Persist();
    }

    public double VMinValue => VMin;
    public double VMaxValue => VMax;
    public double HMinValue => HMin;
    public double HMaxValue => HMax;
    public double StrengthMinValue => StrengthMin;
    public double StrengthMaxValue => StrengthMax;
    public double FreqMinValue => FreqMin;
    public double FreqMaxValue => FreqMax;

    /// <summary>true mientras se dispara (gatillo derecho detecta el boton izquierdo del raton).</summary>
    public bool Firing => _service.CurrentOutput.RightTrigger > 0;

    public string FiringText => Firing ? "PULSADO" : "LIBRE";

    /// <summary>Fuerza enviada al stick derecho (vertical) en vivo, -1..1.</summary>
    public double OutputRightY => _service.CurrentOutput.RightY;

    /// <summary>Fuerza enviada al stick derecho (horizontal) en vivo, -1..1.</summary>
    public double OutputRightX => _service.CurrentOutput.RightX;

    /// <summary>Valor crudo del mando (SHORT de XInput): 32767 = EXTREMO absoluto.</summary>
    public short OutputRightYRaw => (short)MathF.Round((float)OutputRightY * short.MaxValue);

    /// <summary>Valor crudo del mando (SHORT de XInput): 32767 = EXTREMO absoluto.</summary>
    public short OutputRightXRaw => (short)MathF.Round((float)OutputRightX * short.MaxValue);

    /// <summary>Oscilacion enviada al stick izquierdo (horizontal) en vivo, -1..1.</summary>
    public double OutputLeftX => _service.CurrentOutput.LeftX;

    /// <summary>Valor crudo del mando (SHORT de XInput) del stick izquierdo (oscilacion).</summary>
    public short OutputLeftXRaw => (short)MathF.Round((float)OutputLeftX * short.MaxValue);

    /// <summary>
    /// Refuerzo por rafagas (experimental): bombea el empuje a ~20 Hz. En algunos juegos
    /// contrarresta mejor el retroceso por disparo; en otros puede empeorar.
    /// </summary>
    public bool PumpEnabled
    {
        get => _service.Settings.AntiRecoil.PumpEnabled;
        set
        {
            if (_service.Settings.AntiRecoil.PumpEnabled == value)
                return;
            _service.Settings.AntiRecoil.PumpEnabled = value;
            NotifyAll();
        }
    }

    public bool IsArma1
    {
        get => _service.Settings.AntiRecoil.Enabled && _service.ActiveWeapon == 1;
        set
        {
            if (!value)
                return;
            _service.Settings.AntiRecoil.Enabled = true;
            _service.SelectWeapon(1);
            NotifyAll();
        }
    }

    public bool IsArma2
    {
        get => _service.Settings.AntiRecoil.Enabled && _service.ActiveWeapon == 2;
        set
        {
            if (!value)
                return;
            _service.Settings.AntiRecoil.Enabled = true;
            _service.SelectWeapon(2);
            NotifyAll();
        }
    }

    public bool IsOff
    {
        get => !_service.Settings.AntiRecoil.Enabled;
        set
        {
            if (!value)
                return;
            _service.Settings.AntiRecoil.Enabled = false;
            NotifyAll();
        }
    }

    /// <summary>Arma activa en vivo (1 o 2). Cambia al pulsar 1/2 en el juego.</summary>
    public int ActiveWeapon => _service.ActiveWeapon;

    public string ActiveWeaponDisplay => $"ARMA {_service.ActiveWeapon}";

    public bool WeaponDetectionEnabled
    {
        get => _service.Settings.AntiRecoil.WeaponDetectionEnabled;
        set
        {
            if (_service.Settings.AntiRecoil.WeaponDetectionEnabled == value)
                return;
            _service.Settings.AntiRecoil.WeaponDetectionEnabled = value;
            NotifyAll();
        }
    }

    public double Arma1Vertical
    {
        get => ClampD(_service.Settings.AntiRecoil.Arma1Vertical);
        set => SetForce(value, useArma2: false, f => _service.Settings.AntiRecoil.Arma1Vertical = f);
    }

    public double Arma1Horizontal
    {
        get => ClampD(_service.Settings.AntiRecoil.Arma1Horizontal);
        set => SetForce(value, useArma2: false, f => _service.Settings.AntiRecoil.Arma1Horizontal = f);
    }

    public double Arma2Vertical
    {
        get => ClampD(_service.Settings.AntiRecoil.Arma2Vertical);
        set => SetForce(value, useArma2: true, f => _service.Settings.AntiRecoil.Arma2Vertical = f);
    }

    public double Arma2Horizontal
    {
        get => ClampD(_service.Settings.AntiRecoil.Arma2Horizontal);
        set => SetForce(value, useArma2: true, f => _service.Settings.AntiRecoil.Arma2Horizontal = f);
    }

    public bool Oscillation
    {
        get => _service.Settings.AntiRecoil.Oscillation;
        set
        {
            if (_service.Settings.AntiRecoil.Oscillation == value)
                return;
            _service.Settings.AntiRecoil.Oscillation = value;
            if (value)
                _service.Settings.AntiRecoil.Enabled = true;
            OnPropertyChanged();
            NotifyAll();
        }
    }

    public double OscillationStrength
    {
        get => Math.Clamp(_service.Settings.AntiRecoil.OscillationStrength, StrengthMin, StrengthMax);
        set => SetConfig(() => _service.Settings.AntiRecoil.OscillationStrength = (float)Math.Clamp(value, StrengthMin, StrengthMax));
    }

    public double OscillationFrequency
    {
        get => (double)Math.Clamp(_service.Settings.AntiRecoil.OscillationFrequency, FreqMin, FreqMax);
        set => SetConfig(() => _service.Settings.AntiRecoil.OscillationFrequency = (float)Math.Clamp(value, FreqMin, FreqMax));
    }

    public string StatusText
    {
        get
        {
            var cfg = _service.Settings.AntiRecoil;
            if (!cfg.Enabled)
                return "ANTI-RECOIL DESACTIVADO";

            string arma = $"ARMA {_service.ActiveWeapon}";
            return cfg.WeaponDetectionEnabled
                ? $"ANTI-RECOIL ACTIVO · {arma} · DETECCIÓN AUTO"
                : $"ANTI-RECOIL ACTIVO · {arma}";
        }
    }

    public string StatusHint
    {
        get
        {
            var cfg = _service.Settings.AntiRecoil;
            if (!cfg.Enabled)
                return "Selecciona ARMA 1 o ARMA 2 para activar la compensacion.";

            bool arma2 = _service.ActiveWeapon == 2;
            float vertical = arma2 ? cfg.Arma2Vertical : cfg.Arma1Vertical;
            float horizontal = arma2 ? cfg.Arma2Horizontal : cfg.Arma1Horizontal;
            string push = (vertical == 0f && horizontal == 0f)
                ? "Sin fuerzas configuradas con esta arma."
                : $"Empuje vertical {(vertical >= 0 ? "+" : "")}{vertical:0%} · horizontal {horizontal:+#0%;-#0%;0%}.";

            string osc = cfg.Oscillation ? " Oscilacion del stick izquierdo activa." : string.Empty;
            string det = cfg.WeaponDetectionEnabled ? " Cambia de arma con las teclas 1/2 o la rueda del ratón." : string.Empty;
            return push + osc + det;
        }
    }

    private void SetConfig(Action apply)
    {
        apply();
        NotifyAll();
    }

    /// <summary>
    /// Aplica una fuerza de empuje. Si el valor no es cero, activa el anti-recoil y selecciona
    /// el arma correspondiente, de modo que lo recien configurado sea lo que se aplica en el juego
    /// sin tener que pulsar la radio de arma por separado.
    /// </summary>
    private void SetForce(double value, bool useArma2, Action<float> apply)
    {
        float force = (float)ClampD(value);
        apply(force);
        if (force != 0f)
        {
            _service.Settings.AntiRecoil.Enabled = true;
            _service.SelectWeapon(useArma2 ? 2 : 1);
        }
        NotifyAll();
    }

    private void NotifyAll()
    {
        OnPropertyChanged(nameof(IsOff));
        OnPropertyChanged(nameof(IsArma1));
        OnPropertyChanged(nameof(IsArma2));
        OnPropertyChanged(nameof(ActiveWeapon));
        OnPropertyChanged(nameof(ActiveWeaponDisplay));
        OnPropertyChanged(nameof(WeaponDetectionEnabled));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusHint));
        Persist();
    }

    private void Persist() => _store?.Save(_service.Settings);

    private static double ClampD(double value) => Math.Clamp(value, -1f, 1f);

    public void Dispose() => _timer.Stop();
}