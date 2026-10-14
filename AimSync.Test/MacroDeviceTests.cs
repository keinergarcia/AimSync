using AimSync.Core;
using AimSync.Core.Devices;
using AimSync.Core.Engine;
using AimSync.Core.Input;

namespace AimSync.Test;

/// <summary>
/// Prueba REAL del dispositivo virtual (ViGEm + readback XInput) para macros.
/// A diferencia de las pruebas con Spy, este archivo escribe en el mando virtual
/// real y comprueba, leyendo otro slot de XInput, que las pulsaciones generadas
/// por la macro llegan al dispositivo. Requiere ViGEmBus instalado.
/// Uso: --macroreal
/// </summary>
internal static class MacroDeviceTests
{
    private const ushort XInputX = 0x4000;
    private const ushort XInputY = 0x8000;

    public static int Run()
    {
        Console.WriteLine("=== AimSync Macro REAL device test (ViGEm -> XInput readback) ===");
        Console.WriteLine();

        if (!ViGEmUtil.IsBusInstalled())
        {
            Console.WriteLine("[SKIP] ViGEmBus no instalado. Prueba real no ejecutada.");
            return 0;
        }

        int failures = 0;
        Run("MR-1 Macro por teclado -> mando virtual real (X/Y alternan)", TestKeyboardMacroReachesDevice, ref failures);
        Run("MR-2 Macro por mando fisico -> mando virtual real (Y -> X)", TestPhysicalMacroReachesDevice, ref failures);
        Run("MR-3 Soltar el disparador libera los botones en el dispositivo real", TestReleaseClearsDevice, ref failures);
        Run("MR-4 Desactivar AimSync limpia el dispositivo real", TestDeactivateClearsDevice, ref failures);

        Console.WriteLine();
        Console.WriteLine(failures == 0
            ? "RESULTADO (real device): TODAS PASARON"
            : $"RESULTADO (real device): {failures} FALLARON");
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

    /// <summary>
    /// Conecta un ViGEm real y devuelve el slot XInput que ocupa.
    /// Identifica su propio slot escribiendo una firma unica en el stick izquierdo y
    /// buscando el slot que la refleja. Asi no se confunde con otro mando virtual ya
    /// presente (por ejemplo, la app abierta a la vez).
    /// </summary>
    private static (DeviceManager, IVirtualController, uint) ConnectVirtualPad()
    {
        const short sigX = 12345;
        const short sigY = -23456;

        var deviceManager = new DeviceManager();
        var controller = deviceManager.CreateController();
        if (!controller.Connect())
            throw new InvalidOperationException($"ViGEm Connect() fallo: {controller.LastError}");

        controller.SetLeftStick(sigX, sigY);

        uint slot = uint.MaxValue;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(3))
        {
            Thread.Sleep(80);
            foreach (var candidate in XInputReader.FindConnectedSlots())
            {
                if (XInputReader.TryGetStateVirtual(candidate, out var st)
                    && st.Gamepad.sThumbLX == sigX && st.Gamepad.sThumbLY == sigY)
                {
                    slot = candidate;
                    break;
                }
            }

            if (slot != uint.MaxValue)
                break;
        }

        controller.SetLeftStick(0, 0);
        Thread.Sleep(60);

        if (slot == uint.MaxValue)
            throw new InvalidOperationException("No se encontro el slot XInput propio del mando virtual (firma de stick no detectada).");

        return (deviceManager, controller, slot);
    }

    private static (bool, string) TestKeyboardMacroReachesDevice()
    {
        var settings = new AimSyncSettings();
        settings.Macros.Enabled = true;
        settings.Macros.Macros.Add(new Macro
        {
            Name = "K",
            TriggerButton = ControllerButton.A, // Space -> A
            Loop = true,
            Steps =
            [
                new MacroStep { Button = ControllerButton.X, DurationMs = 60 },
                new MacroStep { Button = ControllerButton.Y, DurationMs = 60 },
            ],
        });

        var (deviceManager, controller, slot) = ConnectVirtualPad();
        try
        {
            var keyboard = new FakeKeyboardCapture();
            var mouse = new FakeMouseCapture();
            using var manager = new InputManager(settings, () => controller, keyboard, mouse, new XInputControllerSource());
            if (!manager.Activate())
                return (false, $"Activate fallo: {manager.LastError}");

            keyboard.Press(KeyCode.Space);

            int xTicks = 0, yTicks = 0;
            for (int i = 0; i < 90; i++)
            {
                manager.TryTick();
                if (XInputReader.TryGetStateVirtual(slot, out var st))
                {
                    if ((st.Gamepad.wButtons & XInputX) != 0) xTicks++;
                    if ((st.Gamepad.wButtons & XInputY) != 0) yTicks++;
                }
                Thread.Sleep(4);
            }

            keyboard.Release(KeyCode.Space);
            for (int i = 0; i < 8; i++)
            {
                manager.TryTick();
                Thread.Sleep(4);
            }

            bool released = XInputReader.TryGetStateVirtual(slot, out var final)
                && (final.Gamepad.wButtons & (XInputX | XInputY)) == 0;

            bool ok = xTicks > 0 && yTicks > 0 && released;
            return (ok, $"X vio {xTicks} ticks, Y vio {yTicks} ticks, liberado={released} (slot {slot})");
        }
        finally
        {
            deviceManager.Dispose();
        }
    }

    private static (bool, string) TestPhysicalMacroReachesDevice()
    {
        var settings = new AimSyncSettings();
        settings.Macros.Enabled = true;
        settings.Macros.Macros.Add(new Macro
        {
            Name = "P",
            TriggerButton = ControllerButton.Y,
            Loop = false,
            Steps = [new MacroStep { Button = ControllerButton.X, DurationMs = 80 }],
        });

        var (deviceManager, controller, slot) = ConnectVirtualPad();
        try
        {
            var keyboard = new FakeKeyboardCapture();
            var mouse = new FakeMouseCapture();
            var physical = new FakePhysicalSource();
            using var manager = new InputManager(settings, () => controller, keyboard, mouse, physical);
            if (!manager.Activate())
                return (false, $"Activate fallo: {manager.LastError}");

            physical.Press(PhysicalButtons.Y);

            int xTicks = 0;
            for (int i = 0; i < 20; i++)
            {
                manager.TryTick();
                if (XInputReader.TryGetStateVirtual(slot, out var st) && (st.Gamepad.wButtons & XInputX) != 0)
                    xTicks++;
                Thread.Sleep(4);
            }

            bool yPass = XInputReader.TryGetStateVirtual(slot, out var mid) && (mid.Gamepad.wButtons & XInputY) != 0;

            physical.Release(PhysicalButtons.Y);
            for (int i = 0; i < 8; i++)
            {
                manager.TryTick();
                Thread.Sleep(4);
            }

            bool clean = XInputReader.TryGetStateVirtual(slot, out var final)
                && (final.Gamepad.wButtons & (XInputX | XInputY)) == 0;

            bool ok = xTicks > 0 && clean;
            return (ok, $"Y fisico -> X se vio {xTicks} ticks, Y passthrough={yPass}, limpio al soltar={clean} (slot {slot})");
        }
        finally
        {
            deviceManager.Dispose();
        }
    }

    private static (bool, string) TestReleaseClearsDevice()
    {
        var settings = new AimSyncSettings();
        settings.Macros.Enabled = true;
        settings.Macros.Macros.Add(new Macro
        {
            Name = "R",
            TriggerButton = ControllerButton.A,
            Loop = true,
            Steps = [new MacroStep { Button = ControllerButton.X, DurationMs = 200 }],
        });

        var (deviceManager, controller, slot) = ConnectVirtualPad();
        try
        {
            var keyboard = new FakeKeyboardCapture();
            var mouse = new FakeMouseCapture();
            using var manager = new InputManager(settings, () => controller, keyboard, mouse);
            manager.Activate();

            keyboard.Press(KeyCode.Space);
            for (int i = 0; i < 10; i++) { manager.TryTick(); Thread.Sleep(4); }
            bool pressed = XInputReader.TryGetStateVirtual(slot, out var on) && (on.Gamepad.wButtons & XInputX) != 0;

            keyboard.Release(KeyCode.Space);
            for (int i = 0; i < 10; i++) { manager.TryTick(); Thread.Sleep(4); }
            bool cleared = XInputReader.TryGetStateVirtual(slot, out var off) && (off.Gamepad.wButtons & XInputX) == 0;

            return (pressed && cleared, $"X pulsado={pressed}, libre tras soltar={cleared}");
        }
        finally
        {
            deviceManager.Dispose();
        }
    }

    private static (bool, string) TestDeactivateClearsDevice()
    {
        var settings = new AimSyncSettings();
        settings.Macros.Enabled = true;
        settings.Macros.Macros.Add(new Macro
        {
            Name = "D",
            TriggerButton = ControllerButton.A,
            Loop = true,
            Steps = [new MacroStep { Button = ControllerButton.X, DurationMs = 500 }],
        });

        var (deviceManager, controller, slot) = ConnectVirtualPad();
        try
        {
            var keyboard = new FakeKeyboardCapture();
            var mouse = new FakeMouseCapture();
            using var manager = new InputManager(settings, () => controller, keyboard, mouse);
            manager.Activate();

            keyboard.Press(KeyCode.Space);
            for (int i = 0; i < 12; i++) { manager.TryTick(); Thread.Sleep(4); }
            bool pressed = XInputReader.TryGetStateVirtual(slot, out var on) && (on.Gamepad.wButtons & XInputX) != 0;

            manager.Deactivate();
            Thread.Sleep(150);
            bool cleared = XInputReader.TryGetStateVirtual(slot, out var off) && off.Gamepad.wButtons == 0

                ;
            return (pressed && cleared, $"X pulsado={pressed}, device sin botones tras Deactivate={cleared}");
        }
        finally
        {
            deviceManager.Dispose();
        }
    }
}