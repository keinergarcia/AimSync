namespace AimSync.Core.Diagnostics;

/// <summary>
/// Evento de diagnóstico temporal para el Input Tester.
/// Registra el recorrido: mando físico → captura → detección → motor de macros → estado procesado → salida virtual.
/// </summary>
public sealed class DiagnosticEvent
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public string ButtonName { get; init; } = string.Empty;
    public bool IsPhysicalPressed { get; init; }
    public bool IsPhysicalReleased { get; init; }
    public string MacroName { get; init; } = string.Empty;
    public bool MacroActive { get; init; }
    public string ActionPlanned { get; init; } = string.Empty;
    public string ActionExecuted { get; init; } = string.Empty;
    public string StateSentToVirtual { get; init; } = string.Empty;
    public string Errors { get; init; } = string.Empty;
    public string Cancellations { get; init; } = string.Empty;
}