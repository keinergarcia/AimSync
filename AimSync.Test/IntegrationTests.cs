using AimSync.Core;
using AimSync.Core.Input;

namespace AimSync.Test;

internal static class IntegrationTests
{
    public static int RunAll()
    {
        Console.WriteLine("AVISO: estas pruebas inyectan movimiento real de mouse (SendInput).");
        Console.WriteLine("El cursor de Windows se moverá unos píxeles durante la ejecución.");
        Console.WriteLine();

        int failures = 0;
        Run("IT-1 Mouse real DX>0 -> RightStick X+", TestRealMouseDXPositive, ref failures);
        Run("IT-2 Mouse real DX<0 -> RightStick X-", TestRealMouseDXNegative, ref failures);
        Run("IT-3 Mouse real DY>0 -> RightStick Y segun config", TestRealMouseDY, ref failures);
        Run("IT-4 F8 real (GetAsyncKeyState+loop) activa/desactiva", TestRealF8Toggle, ref failures);
        return failures;
    }

    private static void Run(string name, Func<(bool, string)> test, ref int failures)
    {
        try
        {
            var (ok, detail) = test();
            Console.WriteLine(ok
                ? $"[PASS] {name} -- {detail}"
                : $"[FAIL] {name} -- {detail}");
            if (!ok)
                failures++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FAIL] {name} -- exception: {ex.Message}");
            failures++;
        }
    }

    private static (InputManager, SpyVirtualController) NewRealPipeline()
    {
        var settings = new AimSyncSettings();
        var spy = new SpyVirtualController();
        var manager = new InputManager(settings, () => spy, new KeyboardCapture(), new MouseCapture());
        manager.Activate();
        return (manager, spy);
    }

    private static void Drain(InputManager manager, int iterations = 5)
    {
        for (int i = 0; i < iterations; i++)
        {
            Thread.Sleep(40);
            manager.TryTick();
        }
    }

    private static (bool, string) TestRealMouseDXPositive()
    {
        var (manager, spy) = NewRealPipeline();
        try
        {
            Drain(manager);
            if (!InputInjector.MoveMouseRelative(120, 0))
                return (false, "SendInput devolvio error");

            Thread.Sleep(80);
            manager.TryTick();
            return spy.RightX > 0
                ? (true, $"DX real +120 -> RightStick X={spy.RightX}")
                : (false, $"RightStick X={spy.RightX}, esperado >0");
        }
        finally
        {
            manager.Dispose();
        }
    }

    private static (bool, string) TestRealMouseDXNegative()
    {
        var (manager, spy) = NewRealPipeline();
        try
        {
            Drain(manager);
            if (!InputInjector.MoveMouseRelative(-120, 0))
                return (false, "SendInput devolvio error");

            Thread.Sleep(80);
            manager.TryTick();
            return spy.RightX < 0
                ? (true, $"DX real -120 -> RightStick X={spy.RightX}")
                : (false, $"RightStick X={spy.RightX}, esperado <0");
        }
        finally
        {
            manager.Dispose();
        }
    }

    private static (bool, string) TestRealMouseDY()
    {
        var (manager, spy) = NewRealPipeline();
        try
        {
            Drain(manager);
            if (!InputInjector.MoveMouseRelative(0, 120))
                return (false, "SendInput devolvio error");

            Thread.Sleep(80);
            manager.TryTick();
            return spy.RightY < 0
                ? (true, $"DY real +120 -> RightStick Y={spy.RightY} (config normal, mouse abajo = stick abajo)")
                : (false, $"RightStick Y={spy.RightY}, esperado <0 con config normal");
        }
        finally
        {
            manager.Dispose();
        }
    }

    private static (bool, string) TestRealF8Toggle()
    {
        var settings = new AimSyncSettings();
        var spy = new SpyVirtualController();
        using var manager = new InputManager(settings, () => spy, new KeyboardCapture(), new MouseCapture());
        manager.Start();

        try
        {
            Thread.Sleep(300);
            if (manager.IsEnabled)
            {
                InputInjector.SendKey(settings.ToggleKey, true);
                Thread.Sleep(200);
                InputInjector.SendKey(settings.ToggleKey, false);
                Thread.Sleep(250);
            }

            InputInjector.SendKey(settings.ToggleKey, true);
            Thread.Sleep(300);
            bool turnedOn = manager.IsEnabled && spy.IsConnected;

            InputInjector.SendKey(settings.ToggleKey, false);
            Thread.Sleep(250);
            InputInjector.SendKey(settings.ToggleKey, true);
            Thread.Sleep(300);
            bool turnedOff = !manager.IsEnabled && spy.IsNeutral;

            InputInjector.SendKey(settings.ToggleKey, false);
            Thread.Sleep(150);

            return turnedOn && turnedOff
                ? (true, "F8 real: encendido (controlador conectado) y apagado (estado neutro)")
                : (false, $"encendido={turnedOn} (enabled={manager.IsEnabled} spy={spy.IsConnected}) apagado={turnedOff}");
        }
        finally
        {
            manager.Stop();
        }
    }
}