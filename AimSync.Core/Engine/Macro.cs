namespace AimSync.Core.Engine;

/// <summary>
/// Macro de botones: al pulsar la entrada mapeada a <see cref="TriggerButton"/> se reproduce
/// <see cref="Steps"/>. Con <see cref="Loop"/> activo la secuencia se repite mientras se mantiene.
/// </summary>
public sealed class Macro
{
    public string Name { get; set; } = "Macro";

    public bool Enabled { get; set; } = true;

    /// <summary>Boton del mando virtual que, al pulsarse, dispara la macro.</summary>
    public ControllerButton TriggerButton { get; set; } = ControllerButton.A;

    /// <summary>Repetir la secuencia mientras el boton disparador esta pulsado.</summary>
    public bool Loop { get; set; } = true;

    /// <summary>Activa modo rafaga: pulsa el boton disparador repetidamente mientras este pulsado.</summary>
    public bool RapidFire { get; set; }

    public List<MacroStep> Steps { get; set; } = [];
}
