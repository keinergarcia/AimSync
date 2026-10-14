using System.Globalization;
using System.Text;

namespace AimSync.Core.Diagnostics;

/// <summary>
/// Registrador de diagnóstico que escribe en la consola. Útil para ejecuciones manuales
/// y para depurar la ruta de señal sin la interfaz WPF.
/// </summary>
public sealed class ConsoleDiagnosticLogger : IDiagnosticLogger
{
    private readonly DiagnosticLogOptions _options;
    private long _lastLogTicks;

    public ConsoleDiagnosticLogger(DiagnosticLogOptions? options = null)
        => _options = options ?? DiagnosticLogOptions.Default;

    public void Log(DiagnosticEvent evt)
    {
        if (!_options.Enabled)
            return;

        if (_options.MinIntervalMs > 0)
        {
            long now = Environment.TickCount64;
            long last = Interlocked.Read(ref _lastLogTicks);
            if (now - last < _options.MinIntervalMs)
                return;
            Interlocked.Exchange(ref _lastLogTicks, now);
        }

        var sb = new StringBuilder();
        sb.Append(evt.Timestamp.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture));

        if (!string.IsNullOrEmpty(evt.ButtonName))
            sb.Append(" | button=").Append(evt.ButtonName);
        if (evt.IsPhysicalPressed)
            sb.Append(" | physical=press");
        if (evt.IsPhysicalReleased)
            sb.Append(" | physical=release");
        if (!string.IsNullOrEmpty(evt.MacroName))
            sb.Append(" | macro=").Append(evt.MacroName);
        if (evt.MacroActive)
            sb.Append(" | macro=active");
        if (!string.IsNullOrEmpty(evt.ActionPlanned))
            sb.Append(" | planned=").Append(evt.ActionPlanned);
        if (!string.IsNullOrEmpty(evt.ActionExecuted))
            sb.Append(" | executed=").Append(evt.ActionExecuted);
        if (!string.IsNullOrEmpty(evt.StateSentToVirtual))
            sb.Append(" | output=").Append(evt.StateSentToVirtual);
        if (!string.IsNullOrEmpty(evt.Cancellations))
            sb.Append(" | cancelled=").Append(evt.Cancellations);
        if (!string.IsNullOrEmpty(evt.Errors))
            sb.Append(" | error=").Append(evt.Errors);

        Console.WriteLine(sb.ToString());
    }

    public void Clear()
    {
    }
}
