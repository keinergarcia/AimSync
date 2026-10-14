namespace AimSync.Core.Diagnostics;

/// <summary>
/// Logger nulo por defecto - no hace nada.
/// </summary>
public sealed class NullDiagnosticLogger : IDiagnosticLogger
{
    public static readonly NullDiagnosticLogger Instance = new();
    public void Log(DiagnosticEvent evt) { }
    public void Clear() { }
    private NullDiagnosticLogger() { }
}