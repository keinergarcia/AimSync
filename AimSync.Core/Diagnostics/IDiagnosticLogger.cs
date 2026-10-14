namespace AimSync.Core.Diagnostics;

/// <summary>
/// Interfaz para registrar eventos de diagnóstico. Implementaciones pueden enviar a UI.
/// </summary>
public interface IDiagnosticLogger
{
    void Log(DiagnosticEvent evt);
    void Clear();
}