using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using AimSync.Core.Diagnostics;

namespace AimSync.App.Services;

public sealed class DispatcherDiagnosticLogger : IDiagnosticLogger
{
    private readonly ObservableCollection<DiagnosticEvent> _events = new();
    private readonly Dispatcher? _dispatcher;
    private readonly DiagnosticLogOptions _options;
    private long _lastLogTicks;

    public DispatcherDiagnosticLogger(Dispatcher? dispatcher = null, DiagnosticLogOptions? options = null)
    {
        _dispatcher = dispatcher ?? Application.Current?.Dispatcher;
        _options = options ?? DiagnosticLogOptions.Default;
    }

    public ObservableCollection<DiagnosticEvent> Events => _events;

    public DiagnosticLogOptions Options => _options;

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

        void Add()
        {
            _events.Insert(0, evt);
            while (_events.Count > _options.MaxEvents)
                _events.RemoveAt(_events.Count - 1);
        }

        if (_dispatcher is not null && !_dispatcher.CheckAccess())
            _dispatcher.BeginInvoke(Add);
        else
            Add();
    }

    public void Clear()
    {
        void DoClear() => _events.Clear();
        if (_dispatcher is not null && !_dispatcher.CheckAccess())
            _dispatcher.BeginInvoke(DoClear);
        else
            DoClear();
    }
}
