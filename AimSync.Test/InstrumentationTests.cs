using AimSync.Core;
using AimSync.Core.Devices;
using AimSync.Core.Input;
using AimSync.Core.Mapping;

namespace AimSync.Test;

/// <summary>
/// Pruebas de instrumentación: verifican que la ruta completa de señal
/// (entrada -> mapeo -> motor -> salida virtual) queda registrada y que los
/// estados de ejecución se derivan del motor real, no de la interfaz.
/// No requieren hardware ni videojuego.
/// </summary>
internal static class InstrumentationTests
{
    public static int RunAll()
    {
        int failures = 0;

        Run("IT-1 Ruta teclado -> macro -> salida virtual queda registrada", TestKeyboardMacroPath, ref failures);
        Run("IT-2 Mando fisico conectado se refleja y dispara macro", TestPhysicalPath, ref failures);
        Run("IT-3 Error de salida se captura y se registra (no oculto)", TestOutputErrorCaptured, ref failures);
        Run("IT-4 Desactivar detiene la macro y limpia la salida", TestDeactivateCleans, ref failures);
        Run("IT-5 Mando fisico reenvia los sticks completos a la salida virtual", TestPhysicalSticksForwarded, ref failures);
        Run("IT-6 Turbo sobre mando fisico: Y cambio de arma y RT disparo alternan", TestPhysicalTurboPulses, ref failures);

        return failures;
    }

    private static void Run(string name, Func<(bool, string)> test, ref int failures)
    {
        try
        {
            var (ok, detail) = test();
            Console.WriteLine(ok ? $"[PASS] {name} -- {detail}" : $"[FAIL] {name} -- {detail}");
            if (!ok)
                failures++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FAIL] {name} -- exception: {ex.Message}");
            failures++;
        }
    }

    private static (bool, string) TestKeyboardMacroPath()
    {
        var settings = new AimSyncSettings();
        settings.Macros.Enabled = true;
        settings.Macros.Macros.Add(new AimSync.Core.Engine.Macro
        {
            Name = "Combo",
            TriggerButton = ControllerButton.A,
            Loop = false,
            Steps = [new AimSync.Core.Engine.MacroStep { Button = ControllerButton.X, DurationMs = 60 }],
        });

        var keyboard = new FakeKeyboardCapture();
        var mouse = new FakeMouseCapture();
        var spy = new SpyVirtualController();
        var logger = new RecordingDiagnosticLogger();
        using var manager = new InputManager(settings, () => spy, keyboard, mouse, diagnostics: logger);
        manager.Activate();

        keyboard.Press(KeyCode.Space);
        manager.TryTick();

        var events = logger.Events;
        bool macroEvent = events.Any(e => e.ButtonName == "X" && e.MacroActive);
        bool planned = events.Any(e => e.ButtonName == "X" && !string.IsNullOrEmpty(e.ActionPlanned));
        bool executed = events.Any(e => e.ButtonName == "X" && !string.IsNullOrEmpty(e.ActionExecuted));
        bool sent = events.Any(e => e.ButtonName == "X" && !string.IsNullOrEmpty(e.StateSentToVirtual));
        bool running = manager.IsMacroRunning;
        bool actions = manager.ActionsSent > 0;
        bool outputX = spy.GetButton(ControllerButton.X);

        keyboard.Release(KeyCode.Space);
        manager.TryTick();

        bool cancelled = logger.Events.Any(e => e.ButtonName == "A" && !string.IsNullOrEmpty(e.Cancellations));
        bool cleanAfter = !spy.GetButton(ControllerButton.X) && !manager.IsMacroRunning;

        bool ok = macroEvent && planned && executed && sent && running && actions && outputX && cancelled && cleanAfter;
        return (ok, $"eventoMacro={macroEvent} plan={planned} ejec={executed} enviado={sent} running={running} " +
                    $"acciones={manager.ActionsSent} salidaX={outputX} cancelado={cancelled} limpio={cleanAfter}");
    }

    private static (bool, string) TestPhysicalPath()
    {
        var settings = new AimSyncSettings();
        settings.Macros.Enabled = true;
        settings.Macros.Macros.Add(new AimSync.Core.Engine.Macro
        {
            Name = "Fisica",
            TriggerButton = ControllerButton.Y,
            Steps = [new AimSync.Core.Engine.MacroStep { Button = ControllerButton.X, DurationMs = 60 }],
        });

        var keyboard = new FakeKeyboardCapture();
        var mouse = new FakeMouseCapture();
        var spy = new SpyVirtualController();
        var logger = new RecordingDiagnosticLogger();
        var physical = new FakePhysicalSource();
        using var manager = new InputManager(settings, () => spy, keyboard, mouse, physical, logger);
        manager.Activate();

        physical.Press(PhysicalButtons.Y);
        manager.TryTick();

        bool connected = manager.IsPhysicalControllerConnected;
        bool physicalEvent = logger.Events.Any(e => e.IsPhysicalPressed && e.ButtonName == "Y");
        bool macroRan = spy.GetButton(ControllerButton.X) && logger.Events.Any(e => e.ButtonName == "X" && e.MacroActive);

        physical.Release(PhysicalButtons.Y);
        manager.TryTick();

        bool clean = !spy.GetButton(ControllerButton.X) && !spy.GetButton(ControllerButton.Y);

        bool ok = connected && physicalEvent && macroRan && clean;
        return (ok, $"conectado={connected} eventoFisico={physicalEvent} macroCorrio={macroRan} limpio={clean}");
    }

    private static (bool, string) TestOutputErrorCaptured()
    {
        var settings = new AimSyncSettings();
        var keyboard = new FakeKeyboardCapture();
        var mouse = new FakeMouseCapture();
        var logger = new RecordingDiagnosticLogger();
        var faulty = new FaultyVirtualController();
        using var manager = new InputManager(settings, () => faulty, keyboard, mouse, diagnostics: logger);
        manager.Activate();

        keyboard.Press(KeyCode.Space);
        bool tickOk = manager.TryTick();

        bool errorSet = !string.IsNullOrEmpty(manager.OutputError);
        bool lastErrorSet = !string.IsNullOrEmpty(manager.LastError);
        bool errorEvent = logger.Events.Any(e => !string.IsNullOrEmpty(e.Errors));
        bool notTreatedAsOk = !tickOk;

        bool ok = errorSet && lastErrorSet && errorEvent && notTreatedAsOk;
        return (ok, $"tick={tickOk} outputError={errorSet} lastError={lastErrorSet} eventoError={errorEvent}");
    }

    private static (bool, string) TestDeactivateCleans()
    {
        var settings = new AimSyncSettings();
        settings.Macros.Enabled = true;
        settings.Macros.Macros.Add(new AimSync.Core.Engine.Macro
        {
            Name = "Combo",
            TriggerButton = ControllerButton.A,
            Loop = true,
            Steps = [new AimSync.Core.Engine.MacroStep { Button = ControllerButton.X, DurationMs = 60 }],
        });

        var keyboard = new FakeKeyboardCapture();
        var mouse = new FakeMouseCapture();
        var spy = new SpyVirtualController();
        using var manager = new InputManager(settings, () => spy, keyboard, mouse);
        manager.Activate();

        keyboard.Press(KeyCode.Space);
        manager.TryTick();
        bool runningBefore = manager.IsMacroRunning;

        manager.Deactivate();

        bool disabled = !manager.IsEnabled;
        bool notRunning = !manager.IsMacroRunning;
        bool neutral = spy.IsNeutral;

        bool ok = runningBefore && disabled && notRunning && neutral;
        return (ok, $"corriaAntes={runningBefore} desactivado={disabled} sinMacro={notRunning} neutro={neutral}");
    }

    private static (bool, string) TestPhysicalSticksForwarded()
    {
        var settings = new AimSyncSettings();
        var keyboard = new FakeKeyboardCapture();
        var mouse = new FakeMouseCapture();
        var spy = new SpyVirtualController();
        var physical = new FakePhysicalSource();
        using var manager = new InputManager(settings, () => spy, keyboard, mouse, physical);
        manager.Activate();

        physical.SetSticks(leftX: -32767, leftY: 0, rightX: 0, rightY: 32767);
        manager.TryTick();
        short lx = spy.LeftX, ly = spy.LeftY, rx = spy.RightX, ry = spy.RightY;
        bool forwarded = lx == -32767 && ly == 0 && rx == 0 && ry == 32767;

        physical.DisconnectAll();
        manager.TryTick();
        bool released = spy.LeftX == 0 && spy.RightY == 0;

        bool ok = forwarded && released;
        return (ok, $"reenviado={forwarded} (L={lx},{ly} R={rx},{ry}); al desconectar neutro={released}");
    }

    private static (bool, string) TestPhysicalTurboPulses()
    {
        var settings = new AimSyncSettings();
        settings.Turbo.Enabled = true;
        settings.Turbo.RightEnabled = true;
        settings.Turbo.FrequencyHz = 12;
        settings.Turbo.DutyCycle = 0.4f;
        settings.Turbo.TriggerFrequencyHz = 12;
        settings.Turbo.TriggerDutyCycle = 0.4f;
        settings.Turbo.RapidButtons = [ControllerButton.Y];

        var keyboard = new FakeKeyboardCapture();
        var mouse = new FakeMouseCapture();
        var spy = new SpyVirtualController();
        var physical = new FakePhysicalSource();
        using var manager = new InputManager(settings, () => spy, keyboard, mouse, physical);
        manager.Activate();

        physical.Press(PhysicalButtons.Y);
        physical.SetTriggers(right: 255);

        bool yOn = false, yOff = false, rtOn = false, rtOff = false;
        for (int i = 0; i < 60; i++)
        {
            manager.TryTick();
            if (spy.GetButton(ControllerButton.Y)) yOn = true; else yOff = true;
            if (spy.RightTrigger > 0) rtOn = true; else rtOff = true;
        }

        bool ok = yOn && yOff && rtOn && rtOff;
        return (ok, $"Y: on={yOn} off={yOff}; RT: on={rtOn} off={rtOff} (turbo pulsa el mando fisico)");
    }

    private sealed class FaultyVirtualController : IVirtualController
    {
        public bool IsConnected => true;
        public string? LastError => null;

        public event Action? Connected { add { } remove { } }
        public event Action? Disconnected { add { } remove { } }

        public bool Connect() => true;
        public void Disconnect() { }
        public void Reset() { }
        public void SetLeftStick(short x, short y) { }
        public void SetRightStick(short x, short y) { }
        public void SetLeftTrigger(byte value) { }
        public void SetRightTrigger(byte value) { }
        public bool GetButton(ControllerButton button) => false;
        public void SetButton(ControllerButton button, bool pressed) => throw new InvalidOperationException("fallo de escritura simulado");
        public void Submit() { }
        public void Dispose() { }
    }
}
