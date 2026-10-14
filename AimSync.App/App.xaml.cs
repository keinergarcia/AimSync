using System.Windows;
using AimSync.App.Services;
using AimSync.App.ViewModels;
using AimSync.Core;
using AimSync.Core.Storage;

namespace AimSync.App;

public partial class App : Application
{
    private IAimSyncService? _service;
    private SettingsStore? _store;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnMainWindowClose;

        _store = new SettingsStore();
        var settings = new AimSyncSettings();
        _store.Load(settings);
        // Force 1000Hz for app runtime
        settings.LoopInterval = TimeSpan.FromMilliseconds(1);

        var diag = new Services.DispatcherDiagnosticLogger(options: new AimSync.Core.Diagnostics.DiagnosticLogOptions
        {
            MaxEvents = 200,
            MinIntervalMs = 8,
            Enabled = true,
        });
        _service = new AimSyncService(settings, diagnostics: diag);
        _service.Start();

        var viewModel = new MainViewModel(_service, store: _store);
        // Pasar logger al InputTester si es necesario
        try { viewModel.InputTester.SetLogger(diag); } catch { }
        var window = new MainWindow
        {
            DataContext = viewModel,
        };
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (MainWindow?.DataContext is MainViewModel viewModel)
        {
            viewModel.Dispose();
            _store?.Save(viewModel.Macros.ServiceSettings);
        }

        _service?.Dispose();
        _service = null;

        base.OnExit(e);
    }
}