using System.Text.Json;
using System.Text.Json.Serialization;
using AimSync.Core.Engine;

namespace AimSync.Core.Storage;

/// <summary>
/// Persistencia en disco de la configuracion de turbo y macros en un JSON legible
/// (%AppData%\AimSync\settings.json por defecto). Los fallos de lectura/escritura nunca
/// propagan: la aplicacion debe seguir funcionando con los valores por defecto.
/// </summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public SettingsStore(string? filePath = null)
    {
        FilePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AimSync",
            "settings.json");
    }

    public string FilePath { get; }

    public bool Load(AimSyncSettings settings)
    {
        try
        {
            if (!File.Exists(FilePath))
                return false;

            var data = JsonSerializer.Deserialize<PersistedSettings>(File.ReadAllText(FilePath), Options);
            if (data is null)
                return false;

            Apply(data, settings);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool Save(AimSyncSettings settings)
    {
        try
        {
            string? dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(FilePath, JsonSerializer.Serialize(PersistedSettings.From(settings), Options));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void Apply(PersistedSettings data, AimSyncSettings settings)
    {
        var turbo = settings.Turbo;
        turbo.Enabled = data.Turbo.Enabled;
        turbo.LeftEnabled = data.Turbo.LeftEnabled;
        turbo.RightEnabled = data.Turbo.RightEnabled;
        turbo.FrequencyHz = data.Turbo.FrequencyHz;
        turbo.DutyCycle = data.Turbo.DutyCycle;
        turbo.TriggerFrequencyHz = data.Turbo.TriggerFrequencyHz;
        turbo.TriggerDutyCycle = data.Turbo.TriggerDutyCycle;
        turbo.RapidButtons = new List<ControllerButton>(data.Turbo.RapidButtons);

        var macros = settings.Macros;
        macros.Enabled = data.Macros.Enabled;
        macros.Macros.Clear();
        foreach (var macro in data.Macros.Macros)
            macros.Macros.Add(macro);

        settings.RightTrigger.Sensitivity = data.RightTrigger.Sensitivity;
        settings.RightTrigger.ActivationThreshold = data.RightTrigger.ActivationThreshold;
        settings.LeftTrigger.Sensitivity = data.LeftTrigger.Sensitivity;
        settings.LeftTrigger.ActivationThreshold = data.LeftTrigger.ActivationThreshold;

        var recoil = settings.AntiRecoil;
        var recoilData = data.AntiRecoil;
        recoil.Enabled = recoilData.Enabled;
        recoil.UseArma2 = recoilData.UseArma2;
        recoil.WeaponDetectionEnabled = recoilData.WeaponDetectionEnabled;
        recoil.Weapon1Key = recoilData.Weapon1Key;
        recoil.Weapon2Key = recoilData.Weapon2Key;
        recoil.Arma1Vertical = recoilData.Arma1Vertical;
        recoil.Arma1Horizontal = recoilData.Arma1Horizontal;
        recoil.Arma2Vertical = recoilData.Arma2Vertical;
        recoil.Arma2Horizontal = recoilData.Arma2Horizontal;
        recoil.Oscillation = recoilData.Oscillation;
        recoil.OscillationStrength = recoilData.OscillationStrength;
        recoil.OscillationFrequency = recoilData.OscillationFrequency;
        recoil.PumpEnabled = recoilData.PumpEnabled;
    }

    private sealed class PersistedSettings
    {
        public TurboConfig Turbo { get; set; } = new();

        public MacroConfig Macros { get; set; } = new();

        public TriggerConfig RightTrigger { get; set; } = new();

        public TriggerConfig LeftTrigger { get; set; } = new();

        public AntiRecoilConfig AntiRecoil { get; set; } = new();

        public static PersistedSettings From(AimSyncSettings settings) => new()
        {
            Turbo = new TurboConfig
            {
                Enabled = settings.Turbo.Enabled,
                LeftEnabled = settings.Turbo.LeftEnabled,
                RightEnabled = settings.Turbo.RightEnabled,
                FrequencyHz = settings.Turbo.FrequencyHz,
                DutyCycle = settings.Turbo.DutyCycle,
                TriggerFrequencyHz = settings.Turbo.TriggerFrequencyHz,
                TriggerDutyCycle = settings.Turbo.TriggerDutyCycle,
                RapidButtons = new List<ControllerButton>(settings.Turbo.RapidButtons),
            },
            Macros = new MacroConfig
            {
                Enabled = settings.Macros.Enabled,
                Macros = settings.Macros.Macros,
            },
            RightTrigger = new TriggerConfig
            {
                Sensitivity = settings.RightTrigger.Sensitivity,
                ActivationThreshold = settings.RightTrigger.ActivationThreshold,
            },
            LeftTrigger = new TriggerConfig
            {
                Sensitivity = settings.LeftTrigger.Sensitivity,
                ActivationThreshold = settings.LeftTrigger.ActivationThreshold,
            },
            AntiRecoil = new AntiRecoilConfig
            {
                Enabled = settings.AntiRecoil.Enabled,
                UseArma2 = settings.AntiRecoil.UseArma2,
                WeaponDetectionEnabled = settings.AntiRecoil.WeaponDetectionEnabled,
                Weapon1Key = settings.AntiRecoil.Weapon1Key,
                Weapon2Key = settings.AntiRecoil.Weapon2Key,
                Arma1Vertical = settings.AntiRecoil.Arma1Vertical,
                Arma1Horizontal = settings.AntiRecoil.Arma1Horizontal,
                Arma2Vertical = settings.AntiRecoil.Arma2Vertical,
                Arma2Horizontal = settings.AntiRecoil.Arma2Horizontal,
                Oscillation = settings.AntiRecoil.Oscillation,
                OscillationStrength = settings.AntiRecoil.OscillationStrength,
                OscillationFrequency = settings.AntiRecoil.OscillationFrequency,
                PumpEnabled = settings.AntiRecoil.PumpEnabled,
            },
        };
    }
}
