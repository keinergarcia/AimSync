using AimSync.App.Infrastructure;
using AimSync.App.Services;

namespace AimSync.App.ViewModels;

public sealed class KeyboardViewModel : ObservableObject
{
    private string _editStatus = string.Empty;

    public KeyboardViewModel(IAimSyncService service)
    {
        var mapping = service.Settings.Mapping;

        var rows = new List<MappingRow>
        {
            new(KeyNames.Key(mapping.MoveUp), "Stick izquierdo arriba"),
            new(KeyNames.Key(mapping.MoveLeft), "Stick izquierdo izquierda"),
            new(KeyNames.Key(mapping.MoveDown), "Stick izquierdo abajo"),
            new(KeyNames.Key(mapping.MoveRight), "Stick izquierdo derecha"),
        };

        foreach (var (key, button) in mapping.ButtonMap)
            rows.Add(new MappingRow(KeyNames.Key(key), KeyNames.Button(button)));

        rows.Add(new MappingRow(
            $"Mouse {KeyNames.Mouse(mapping.RightTriggerButton)}", "Gatillo derecho (RT)"));
        rows.Add(new MappingRow(
            $"Mouse {KeyNames.Mouse(mapping.LeftTriggerButton)}", "Gatillo izquierdo (LT)"));

        Rows = rows;

        EditMappingCommand = new RelayCommand(() =>
        {
            EditStatus = "El editor completo de mapeo se prepara para la próxima iteración.";
            OnPropertyChanged(nameof(EditStatus));
        });
    }

    public IReadOnlyList<MappingRow> Rows { get; }

    public RelayCommand EditMappingCommand { get; }

    public string EditStatus
    {
        get => _editStatus;
        private set => SetProperty(ref _editStatus, value);
    }
}