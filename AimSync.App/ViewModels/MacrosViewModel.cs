using System.Collections.ObjectModel;
using AimSync.App.Infrastructure;
using AimSync.App.Services;
using AimSync.Core;
using AimSync.Core.Engine;
using AimSync.Core.Storage;

namespace AimSync.App.ViewModels;

/// <summary>
/// Pagina "Turbo y Macros": configuracion en vivo del modo turbo de gatillos y de las
/// macros de botones. Cada cambio se refleja en AimSyncSettings y se persiste en disco.
/// </summary>
public sealed class MacrosViewModel : ObservableObject
{
    public const double FreqMin = 1;
    public const double FreqMax = 250;
    public const double DutyMin = 0.05;
    public const double DutyMax = 1;

    public const double RtSensitivityMin = TriggerConfig.SensitivityMin;
    public const double RtSensitivityMax = TriggerConfig.SensitivityMax;
    public const double RtThresholdMin = TriggerConfig.ThresholdMin;
    public const double RtThresholdMax = TriggerConfig.ThresholdMax;

    private readonly IAimSyncService _service;
    private readonly SettingsStore? _store;
    private readonly RelayCommand _removeMacroCommand;
    private readonly RelayCommand _addStepCommand;
    private MacroItemViewModel? _selectedMacro;

    public MacrosViewModel(IAimSyncService service, SettingsStore? store = null)
    {
        _service = service;
        _store = store;

        foreach (var macro in _service.Settings.Macros.Macros)
            Macros.Add(new MacroItemViewModel(macro, Persist));

        foreach (var option in Options)
            RapidButtons.Add(new RapidButtonViewModel(option.Value, option.Label, _service.Settings.Turbo.RapidButtons, Persist));
        foreach (var option in Options)
            RapidAlways.Add(new RapidButtonViewModel(option.Value, option.Label, _service.Settings.Turbo.RapidAlways, Persist));

        _selectedMacro = Macros.FirstOrDefault();

        AddMacroCommand = new RelayCommand(AddMacro);
        _removeMacroCommand = new RelayCommand(RemoveMacro, () => SelectedMacro is not null);
        _addStepCommand = new RelayCommand(AddStep, () => SelectedMacro is not null);
        RemoveMacroCommand = _removeMacroCommand;
        AddStepCommand = _addStepCommand;
        RemoveStepCommand = new RelayCommand(RemoveStep);
    }

    private static readonly IReadOnlyList<ButtonOption> Options = BuildButtonOptions();

    public IReadOnlyList<ButtonOption> ButtonOptions => Options;

    public static string LabelOf(ControllerButton button)
        => Options.FirstOrDefault(option => option.Value == button)?.Label ?? button.ToString();

    public ObservableCollection<MacroItemViewModel> Macros { get; } = [];

    /// <summary>Botones en rafaga maxima (interruptores de la pagina).</summary>
    public ObservableCollection<RapidButtonViewModel> RapidButtons { get; } = [];

    /// <summary>Botones en rafaga constante (interruptores de la pagina).</summary>
    public ObservableCollection<RapidButtonViewModel> RapidAlways { get; } = [];

    public RelayCommand AddMacroCommand { get; }

    public RelayCommand RemoveMacroCommand { get; }

    public RelayCommand AddStepCommand { get; }

    public RelayCommand RemoveStepCommand { get; }

    public double FreqMinValue => FreqMin;
    public double FreqMaxValue => FreqMax;
    public double DutyMinValue => DutyMin;
    public double DutyMaxValue => DutyMax;

    public MacroItemViewModel? SelectedMacro
    {
        get => _selectedMacro;
        set
        {
            if (!SetProperty(ref _selectedMacro, value))
                return;
            OnPropertyChanged(nameof(HasSelectedMacro));
            _removeMacroCommand.RaiseCanExecuteChanged();
            _addStepCommand.RaiseCanExecuteChanged();
        }
    }

    public bool HasSelectedMacro => SelectedMacro is not null;

    public bool TurboEnabled
    {
        get => _service.Settings.Turbo.Enabled;
        set => SetConfig(() => _service.Settings.Turbo.Enabled = value, nameof(TurboEnabled));
    }

    public bool TurboLeft
    {
        get => _service.Settings.Turbo.LeftEnabled;
        set => SetConfig(() => _service.Settings.Turbo.LeftEnabled = value, nameof(TurboLeft));
    }

    public bool TurboRight
    {
        get => _service.Settings.Turbo.RightEnabled;
        set => SetConfig(() => _service.Settings.Turbo.RightEnabled = value, nameof(TurboRight));
    }

    /// <summary>Velocidad de la rafaga de BOTONES (pulsos por segundo). Independiente de los gatillos.</summary>
    public double TurboButtonFrequency
    {
        get => Math.Clamp(_service.Settings.Turbo.FrequencyHz, FreqMin, FreqMax);
        set => SetConfig(() => _service.Settings.Turbo.FrequencyHz = (float)Math.Clamp(value, FreqMin, FreqMax), nameof(TurboButtonFrequency));
    }

    /// <summary>Duracion del pulso de la rafaga de BOTONES (fraccion del ciclo).</summary>
    public double TurboButtonDuty
    {
        get => Math.Clamp(_service.Settings.Turbo.DutyCycle, DutyMin, DutyMax);
        set => SetConfig(() => _service.Settings.Turbo.DutyCycle = (float)Math.Clamp(value, DutyMin, DutyMax), nameof(TurboButtonDuty));
    }

    /// <summary>Velocidad de la rafaga de GATILLOS (pulsos por segundo). Independiente de los botones.</summary>
    public double TurboTriggerFrequency
    {
        get => Math.Clamp(_service.Settings.Turbo.TriggerFrequencyHz, FreqMin, FreqMax);
        set => SetConfig(() => _service.Settings.Turbo.TriggerFrequencyHz = (float)Math.Clamp(value, FreqMin, FreqMax), nameof(TurboTriggerFrequency));
    }

    /// <summary>Duracion del pulso de la rafaga de GATILLOS (fraccion del ciclo).</summary>
    public double TurboTriggerDuty
    {
        get => Math.Clamp(_service.Settings.Turbo.TriggerDutyCycle, DutyMin, DutyMax);
        set => SetConfig(() => _service.Settings.Turbo.TriggerDutyCycle = (float)Math.Clamp(value, DutyMin, DutyMax), nameof(TurboTriggerDuty));
    }

    public double RtSensitivityMinValue => RtSensitivityMin;
    public double RtSensitivityMaxValue => RtSensitivityMax;
    public double RtThresholdMinValue => RtThresholdMin;
    public double RtThresholdMaxValue => RtThresholdMax;

    /// <summary>Sensibilidad del gatillo derecho (RT) en modo normal (0.1..3.0). 1 = sin cambios.</summary>
    public double RightTriggerSensitivity
    {
        get => Math.Clamp(_service.Settings.RightTrigger.Sensitivity, (float)RtSensitivityMin, (float)RtSensitivityMax);
        set => SetConfig(
            () => _service.Settings.RightTrigger.Sensitivity = (float)Math.Clamp(value, RtSensitivityMin, RtSensitivityMax),
            nameof(RightTriggerSensitivity));
    }

    /// <summary>Umbral de activacion del gatillo derecho (RT) en modo normal (0..0.9). 0 = sin umbral.</summary>
    public double RightTriggerThreshold
    {
        get => Math.Clamp(_service.Settings.RightTrigger.ActivationThreshold, (float)RtThresholdMin, (float)RtThresholdMax);
        set => SetConfig(
            () => _service.Settings.RightTrigger.ActivationThreshold = (float)Math.Clamp(value, RtThresholdMin, RtThresholdMax),
            nameof(RightTriggerThreshold));
    }

    public bool MacrosEnabled
    {
        get => _service.Settings.Macros.Enabled;
        set => SetConfig(() => _service.Settings.Macros.Enabled = value, nameof(MacrosEnabled));
    }

    private void SetConfig(Action apply, string property)
    {
        apply();
        OnPropertyChanged(property);
        Persist();
    }

    private void AddMacro()
    {
        var macro = new Macro
        {
            Name = $"Macro {Macros.Count + 1}",
            TriggerButton = ControllerButton.A,
            Steps =
            [
                new MacroStep { Button = ControllerButton.A, DurationMs = 60 },
                new MacroStep { Button = ControllerButton.X, DurationMs = 60 },
            ],
        };

        _service.Settings.Macros.Macros.Add(macro);
        var vm = new MacroItemViewModel(macro, Persist);
        Macros.Add(vm);
        SelectedMacro = vm;
        Persist();
    }

    private void RemoveMacro()
    {
        if (SelectedMacro is not { } macro)
            return;

        _service.Settings.Macros.Macros.Remove(macro.Model);
        Macros.Remove(macro);
        SelectedMacro = Macros.FirstOrDefault();
        Persist();
    }

    private void AddStep() => SelectedMacro?.AddStep();

    private static void RemoveStep(object? parameter)
    {
        // El boton de quitar de cada paso envia el propio paso como CommandParameter.
        if (parameter is MacroStepViewModel step && step.Owner is not null)
            step.Owner.RemoveStep(step);
    }

    private void Persist() => _store?.Save(_service.Settings);

    /// <summary>Configuracion en vivo del servicio (turbo y macros se guardan desde aqui).</summary>
    public AimSyncSettings ServiceSettings => _service.Settings;

    private static IReadOnlyList<ButtonOption> BuildButtonOptions() =>
    [
        new(ControllerButton.A, "A"),
        new(ControllerButton.B, "B"),
        new(ControllerButton.X, "X"),
        new(ControllerButton.Y, "Y"),
        new(ControllerButton.LeftShoulder, "LB"),
        new(ControllerButton.RightShoulder, "RB"),
        new(ControllerButton.Back, "Back / View"),
        new(ControllerButton.Start, "Start / Menu"),
        new(ControllerButton.Guide, "Guide / Xbox"),
        new(ControllerButton.LeftThumb, "L3 (clic stick izq.)"),
        new(ControllerButton.RightThumb, "R3 (clic stick der.)"),
        new(ControllerButton.DPadUp, "D-Pad Arriba"),
        new(ControllerButton.DPadDown, "D-Pad Abajo"),
        new(ControllerButton.DPadLeft, "D-Pad Izquierda"),
        new(ControllerButton.DPadRight, "D-Pad Derecha"),
    ];
}

public sealed record ButtonOption(ControllerButton Value, string Label);

/// <summary>
/// Interruptor de un boton en rafaga maxima. Mantiene la lista de botones en
/// <see cref="AimSync.Core.Engine.TurboConfig.RapidButtons"/> y persiste cada cambio.
/// </summary>
public sealed class RapidButtonViewModel : ObservableObject
{
    private readonly ControllerButton _button;
    private readonly List<ControllerButton> _target;
    private readonly Action _onChanged;

    public RapidButtonViewModel(ControllerButton button, string label, List<ControllerButton> target, Action onChanged)
    {
        _button = button;
        Label = label;
        _target = target;
        _onChanged = onChanged;
    }

    public string Label { get; }

    public ControllerButton Button => _button;

    public bool Enabled
    {
        get => _target.Contains(_button);
        set
        {
            if (value == Enabled)
                return;
            if (value)
                _target.Add(_button);
            else
                _target.Remove(_button);
            OnPropertyChanged();
            _onChanged();
        }
    }
}

public sealed class MacroItemViewModel : ObservableObject
{
    private readonly Action _onChanged;

    public MacroItemViewModel(Macro macro, Action onChanged)
    {
        Model = macro;
        _onChanged = onChanged;
        foreach (var step in macro.Steps)
            Steps.Add(new MacroStepViewModel(step, onChanged) { Owner = this });
    }

    public Macro Model { get; }

    public ObservableCollection<MacroStepViewModel> Steps { get; } = [];

    public string Name
    {
        get => Model.Name;
        set
        {
            if (Model.Name == value)
                return;
            Model.Name = value;
            OnPropertyChanged();
            _onChanged();
        }
    }

    public bool Enabled
    {
        get => Model.Enabled;
        set
        {
            if (Model.Enabled == value)
                return;
            Model.Enabled = value;
            OnPropertyChanged();
            _onChanged();
        }
    }

    public ControllerButton TriggerButton
    {
        get => Model.TriggerButton;
        set
        {
            if (Model.TriggerButton == value)
                return;
            Model.TriggerButton = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TriggerLabel));
            _onChanged();
        }
    }

    public string TriggerLabel => MacrosViewModel.LabelOf(Model.TriggerButton);

    public bool Loop
    {
        get => Model.Loop;
        set
        {
            if (Model.Loop == value)
                return;
            Model.Loop = value;
            OnPropertyChanged();
            _onChanged();
        }
    }

    public void AddStep()
    {
        var step = new MacroStep { Button = ControllerButton.A, DurationMs = 60 };
        Model.Steps.Add(step);
        Steps.Add(new MacroStepViewModel(step, _onChanged) { Owner = this });
        _onChanged();
    }

    public void RemoveStep(MacroStepViewModel step)
    {
        Model.Steps.Remove(step.Model);
        Steps.Remove(step);
        _onChanged();
    }
}

public sealed class MacroStepViewModel : ObservableObject
{
    private readonly Action _onChanged;

    public MacroStepViewModel(MacroStep step, Action onChanged)
    {
        Model = step;
        _onChanged = onChanged;
    }

    public MacroStep Model { get; }

    public MacroItemViewModel? Owner { get; init; }

    public ControllerButton Button
    {
        get => Model.Button;
        set
        {
            if (Model.Button == value)
                return;
            Model.Button = value;
            OnPropertyChanged();
            _onChanged();
        }
    }

    public int DurationMs
    {
        get => Model.DurationMs;
        set
        {
            int clamped = Math.Clamp(value, 10, 60000);
            if (Model.DurationMs == clamped)
                return;
            Model.DurationMs = clamped;
            OnPropertyChanged();
            _onChanged();
        }
    }
}
