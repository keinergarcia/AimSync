namespace AimSync.Core.Engine;

/// <summary>
/// Coleccion de macros de botones y su interruptor maestro.
/// </summary>
public sealed class MacroConfig
{
    public bool Enabled { get; set; }

    public List<Macro> Macros { get; set; } = [];
}
