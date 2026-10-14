using System.Collections.Concurrent;
using AimSync.Core.Diagnostics;

namespace AimSync.Test;

/// <summary>
/// Registrador de diagnóstico de prueba que conserva todos los eventos en memoria
/// para poder afirmar sobre la ruta completa de señal.
/// </summary>
internal sealed class RecordingDiagnosticLogger : IDiagnosticLogger
{
    private readonly ConcurrentQueue<DiagnosticEvent> _events = new();

    public IReadOnlyCollection<DiagnosticEvent> Events => _events.ToArray();

    public void Log(DiagnosticEvent evt) => _events.Enqueue(evt);

    public void Clear()
    {
        while (_events.TryDequeue(out _))
        {
        }
    }
}
