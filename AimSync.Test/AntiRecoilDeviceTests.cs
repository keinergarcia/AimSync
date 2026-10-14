using AimSync.Core;
using AimSync.Core.Devices;

namespace AimSync.Test;

/// <summary>
/// Prueba REAL del anti-recoil sobre el dispositivo virtual (ViGEm + readback XInput).
/// Verifica que el empuje al stick derecho que produce el motor anti-recoil llega
/// fisicamente al mando virtual que lee el juego. Requiere ViGEmBus instalado.
/// Uso: --recoilreal
/// </summary>
internal static class AntiRecoilDeviceTests
{
    public static int Run()
    {
        Console.WriteLine("=== AimSync Anti-Recoil REAL device test (ViGEm -> XInput readback) ===");
        Console.WriteLine();

        if (!ViGEmUtil.IsBusInstalled())
        {
            Console.WriteLine("[SKIP] ViGEmBus no instalado. Prueba real no ejecutada.");
            return 0;
        }

        int failures = 0;
        Run("RR-1 Vertical al disparar (mando fisico RT) llega al stick derecho del virtual", TestVerticalReachesDevice, ref failures);
        Run("RR-2 Sin disparar: el stick derecho vuelve a neutro en el virtual", TestIdleNeutral, ref failures);
        Run("RR-3 Horizontal al disparar llega al eje X del virtual", TestHorizontalReachesDevice, ref failures);

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

    private static (DeviceManager, IVirtualController, uint) ConnectVirtualPad()
    {
        const short sigX = 12345;
        const short sigY = -23456;

        var deviceManager = new DeviceManager();
        var controller = deviceManager.CreateController();
        if (!controller.Connect())
            throw new InvalidOperationException($"ViGEm Connect() fallo: {controller.LastError}");

        controller.SetLeftStick(sigX, sigY);
        controller.Submit();

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
        controller.Submit();
        Thread.Sleep(60);

        if (slot == uint.MaxValue)
            throw new InvalidOperationException("No se encontro el slot XInput propio del mando virtual (firma de stick no detectada).");

        return (deviceManager, controller, slot);
    }

    private static (bool, string) TestVerticalReachesDevice()
    {
        var settings = new AimSyncSettings();
        settings.AntiRecoil.Enabled = true;
        settings.AntiRecoil.Arma1Vertical = 0.5f;

        var (deviceManager, controller, slot) = ConnectVirtualPad();
        try
        {
            var physical = new FakePhysicalSource();
            using var manager = new InputManager(settings, () => controller, new FakeKeyboardCapture(), new FakeMouseCapture(), physical);
            if (!manager.Activate())
                return (false, $"Activate fallo: {manager.LastError}");

            physical.SetTriggers(right: 255);

            short expected = (short)MathF.Round(MathF.Sqrt(0.5f) * short.MaxValue);
            short lastRy = 0;
            bool reached = false;
            for (int i = 0; i < 12; i++)
            {
                manager.TryTick();
                if (XInputReader.TryGetStateVirtual(slot, out var st))
                {
                    lastRy = st.Gamepad.sThumbRY;
                    if (Math.Abs(lastRy - expected) < 2500)
                        reached = true;
                }
                Thread.Sleep(4);
            }

            return (reached, $"RT fisico 255 -> RY virtual={lastRy} (esperado ~{expected}) en slot {slot}");
        }
        finally
        {
            deviceManager.Dispose();
        }
    }

    private static (bool, string) TestIdleNeutral()
    {
        var settings = new AimSyncSettings();
        settings.AntiRecoil.Enabled = true;
        settings.AntiRecoil.Arma1Vertical = 0.5f;

        var (deviceManager, controller, slot) = ConnectVirtualPad();
        try
        {
            var physical = new FakePhysicalSource();
            using var manager = new InputManager(settings, () => controller, new FakeKeyboardCapture(), new FakeMouseCapture(), physical);
            manager.Activate();

            physical.SetTriggers(right: 255);
            for (int i = 0; i < 6; i++) { manager.TryTick(); Thread.Sleep(4); }

            physical.SetTriggers(right: 0);
            short lastRy = 999;
            for (int i = 0; i < 8; i++)
            {
                manager.TryTick();
                if (XInputReader.TryGetStateVirtual(slot, out var st))
                    lastRy = st.Gamepad.sThumbRY;
                Thread.Sleep(4);
            }

            return (lastRy == 0, $"sin disparo: RY virtual={lastRy} (esperado 0) en slot {slot}");
        }
        finally
        {
            deviceManager.Dispose();
        }
    }

    private static (bool, string) TestHorizontalReachesDevice()
    {
        var settings = new AimSyncSettings();
        settings.AntiRecoil.Enabled = true;
        settings.AntiRecoil.Arma1Horizontal = -0.5f;

        var (deviceManager, controller, slot) = ConnectVirtualPad();
        try
        {
            var physical = new FakePhysicalSource();
            using var manager = new InputManager(settings, () => controller, new FakeKeyboardCapture(), new FakeMouseCapture(), physical);
            manager.Activate();

            physical.SetTriggers(right: 255);
            for (int i = 0; i < 8; i++) { manager.TryTick(); Thread.Sleep(4); }

            short expected = (short)MathF.Round(-MathF.Sqrt(0.5f) * short.MaxValue);
            short lastRx = 0;
            if (XInputReader.TryGetStateVirtual(slot, out var st))
                lastRx = st.Gamepad.sThumbRX;

            return (Math.Abs(lastRx - expected) < 2500, $"horizontal -50% -> RX virtual={lastRx} (esperado ~{expected}) en slot {slot}");
        }
        finally
        {
            deviceManager.Dispose();
        }
    }
}
