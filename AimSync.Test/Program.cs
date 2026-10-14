using System.Runtime.InteropServices;
using AimSync.Core;
using AimSync.Core.Input;

namespace AimSync.Test;

internal static class Program
{
    private static bool _autoMode;

    private static void Main(string[] args)
    {
        _autoMode = args.Contains("--auto");
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        if (args.Length > 0)
        {
            if (args.Contains("--phase0")) { RunPhase0(); return; }
            if (args.Contains("--phase1")) { RunPhase1(); return; }
            if (args.Contains("--phase2")) { RunPhase2(); return; }
            if (args.Contains("--integration")) { RunIntegration(); return; }
            if (args.Contains("--tester")) { InputTester.RunTester(args.Contains("--real")); return; }
            if (args.Contains("--testmode")) { InputTester.RunTestMode(); return; }
            if (args.Contains("--mouseprobe")) { MouseProbe.Run(); return; }
            if (args.Contains("--controllersim")) { RunControllerTests(); return; }
            if (args.Contains("--controllerprobe")) { ControllerProbe.Run(args.Contains("--watch")); return; }
            if (args.Contains("--physiodiag")) { PhysioDiag.Run(); return; }
            if (args.Contains("--slotdiag")) { SlotDiag.Run(); return; }
            if (args.Contains("--macroreal")) { MacroDeviceTests.Run(); return; }
            if (args.Contains("--instrument")) { RunInstrumentation(); return; }
            if (args.Contains("--trigger")) { RunTrigger(); return; }
            if (args.Contains("--trigdiag")) { TriggerDiag.Run(); return; }
            if (args.Contains("--antirecoil")) { RunAntiRecoil(); return; }
            if (args.Contains("--recoilreal")) { AntiRecoilDeviceTests.Run(); return; }
        }

        while (true)
        {
            Console.Clear();
            Console.WriteLine("=== AimSync Console ===");
            Console.WriteLine();
            Console.WriteLine(" 1.- Phase 0: validacion ViGEm / XInput");
            Console.WriteLine(" 2.- Phase 1: pruebas automatizadas (1-15)");
            Console.WriteLine(" 3.- Phase 2: pruebas de la cadena UI-service (deterministas)");
            Console.WriteLine(" 4.- Phase 1: integracion real (SendInput)");
            Console.WriteLine(" 5.- Input Tester (controlador viGEm real si esta instalado)");
            Console.WriteLine(" 6.- TEST mode: ruta completa con readback XInput");
            Console.WriteLine(" 7.- Mouse probe: calibracion de MouseMaxDelta con datos reales");
            Console.WriteLine(" 8.- Controller Fase A: deteccion simulada (sin hardware)");
            Console.WriteLine(" 9.- Controller Fase A: sonda hardware (XInput real)");
            Console.WriteLine(" 0.- Salir");
            Console.WriteLine();
            Console.Write("Opcion: ");
            var key = Console.ReadKey(true);
            switch (key.Key)
            {
                case ConsoleKey.D1: RunPhase0(); break;
                case ConsoleKey.D2: RunPhase1(); break;
                case ConsoleKey.D3: RunPhase2(); break;
                case ConsoleKey.D4: RunIntegration(); break;
                case ConsoleKey.D5: InputTester.RunTester(ViGEmUtil.IsBusInstalled()); break;
                case ConsoleKey.D6: InputTester.RunTestMode(); break;
                case ConsoleKey.D7: MouseProbe.Run(); break;
                case ConsoleKey.D8: RunControllerTests(); break;
                case ConsoleKey.D9: ControllerProbe.Run(watch: false); break;
                case ConsoleKey.D0: return;
            }
        }
    }

    private static void RunPhase1()
    {
        Console.WriteLine("=== AimSync Phase 1 Test (automatizado, determinista) ===");
        Console.WriteLine();
        Console.WriteLine("Pipeline: Input -> Mapping -> StickEngine -> VirtualController (Spy)");
        Console.WriteLine();
        int failures = Phase1Tests.RunAll();
        Console.WriteLine();
        Console.WriteLine(failures == 0
            ? "RESULTADO: TODAS LAS PRUEBAS PASARON"
            : $"RESULTADO: {failures} prueba(s) FALLARON");
        Pause();
    }

    private static void RunIntegration()
    {
        Console.WriteLine("=== AimSync Phase 1 Integration Test (entrada real) ===");
        Console.WriteLine();
        int failures = IntegrationTests.RunAll();
        Console.WriteLine();
        Console.WriteLine(failures == 0
            ? "RESULTADO: TODAS LAS INTEGRACIONES PASARON"
            : $"RESULTADO: {failures} integracion(es) FALLARON");
        Pause();
    }

    private static void RunPhase2()
    {
        Console.WriteLine("=== AimSync Phase 2 Test (cadena Service -> InputManager -> VirtualController) ===");
        Console.WriteLine();
        Console.WriteLine("Pipeline UI: ViewModel -> AimSyncService -> InputManager -> IVirtualController (Spy)");
        Console.WriteLine();
        int failures = Phase2Tests.RunAll();
        Console.WriteLine();
        Console.WriteLine(failures == 0
            ? "RESULTADO: TODAS LAS PRUEBAS FASE 2 PASARON"
            : $"RESULTADO: {failures} prueba(s) FASE 2 FALLARON");
        Pause();
    }

    private static void RunInstrumentation()
    {
        Console.WriteLine("=== AimSync Instrumentation Test (autocontenido, sin videojuego) ===");
        Console.WriteLine();
        Console.WriteLine("Verifica que la ruta entrada -> mapeo -> motor -> salida virtual queda registrada.");
        Console.WriteLine();
        int failures = InstrumentationTests.RunAll();
        Console.WriteLine();
        Console.WriteLine(failures == 0
            ? "RESULTADO: TODAS LAS PRUEBAS DE INSTRUMENTACION PASARON"
            : $"RESULTADO: {failures} prueba(s) DE INSTRUMENTACION FALLARON");
        Pause();
    }

    private static void RunTrigger()
    {
        Console.WriteLine("=== AimSync Trigger Test (gatillo RT en modo normal) ===");
        Console.WriteLine();
        Console.WriteLine("Verifica: paso analogico 0..255, sensibilidad/umbral, retorno a cero, F8 sin");
        Console.WriteLine("estados atascados, cambio de perfil y separacion respecto a turbo/macros.");
        Console.WriteLine();
        int failures = TriggerTests.RunAll();
        Console.WriteLine();
        Console.WriteLine(failures == 0
            ? "RESULTADO: TODAS LAS PRUEBAS DE GATILLO PASARON"
            : $"RESULTADO: {failures} prueba(s) DE GATILLO FALLARON");
        Pause();
    }

    private static void RunAntiRecoil()
    {
        Console.WriteLine("=== AimSync Anti-Recoil Test ===");
        Console.WriteLine();
        Console.WriteLine("Verifica: empuje al stick derecho al disparar, perfiles Arma 1/2, horizontal,");
        Console.WriteLine("oscilacion del stick izquierdo, persistencia y auto-activado desde la UI.");
        Console.WriteLine();
        int failures = AntiRecoilTests.RunAll() + AntiRecoilUiTests.RunAll();
        Console.WriteLine();
        Console.WriteLine(failures == 0
            ? "RESULTADO: TODAS LAS PRUEBAS DE ANTI-RECOIL PASARON"
            : $"RESULTADO: {failures} prueba(s) DE ANTI-RECOIL FALLARON");
        Pause();
    }

    private static void RunControllerTests()
    {        Console.WriteLine("=== AimSync Fase A: mando fisico (deteccion simulada) ===");        Console.WriteLine();
        Console.WriteLine("No requiere hardware. Usa una fuente XInput simulada.");
        Console.WriteLine();
        int failures = PhysicalControllerTests.RunAll();
        Console.WriteLine();
        Console.WriteLine(failures == 0
            ? "RESULTADO: TODAS LAS PRUEBAS FASE A PASARON"
            : $"RESULTADO: {failures} prueba(s) FASE A FALLARON");
        Pause();
    }

    private static void RunPhase0()
    {        Console.WriteLine("=== AimSync Phase 0 Test ===");
        Console.WriteLine();
        Console.WriteLine("PRE-FLIGHT");
        Console.WriteLine("----------");

        if (!ViGEmUtil.IsBusInstalled())
        {
            Console.WriteLine("ERROR: ViGEmBus driver files not found.");
            Console.WriteLine("Install ViGEmBus 1.22.0 from https://github.com/nefarius/ViGEmBus/releases/tag/v1.22.0");
            Pause();
            return;
        }

        Console.WriteLine("[OK] ViGEmBus driver files present (ViGEmBus.sys + PnP device).");
        Console.WriteLine();

        try
        {
            RunPhase0Core();
        }
        catch (TypeLoadException ex)
        {
            Console.WriteLine($"[FAIL] ViGEmBus driver not accessible from this process: {ex.Message}");
            Console.WriteLine("The Nefarius.ViGEm.Client native shim needs the bus driver loaded;");
            Console.WriteLine("reinstall ViGEmBus and retry.");
            Pause();
        }

        Console.WriteLine();
        Console.WriteLine("Test finished.");
        Pause();
    }

    private static void RunPhase0Core()
    {
        const uint XInputLeftThumbDeadZone = 7849;
        const uint XInputRightThumbDeadZone = 8689;

        const ushort XInputA = 0x1000;
        const ushort XInputB = 0x2000;
        const ushort XInputDpadUp = 0x0001;

        var connectedBefore = XInputReader.FindConnectedSlots();
        Console.WriteLine($"[INFO] XInput slots connected BEFORE creating the virtual pad: {XInputReader.FormatSlots(connectedBefore)}");
        Console.WriteLine();

        using var deviceManager = new DeviceManager();
        var controller = deviceManager.CreateController();
        Console.WriteLine("[INFO] IVirtualController created (Xbox 360 via ViGEm).");

        controller.Connected += () => Console.WriteLine("[EVENT] Connected raised by controller.");
        controller.Disconnected += () => Console.WriteLine("[EVENT] Disconnected raised by controller.");

        Console.WriteLine();
        Console.Write("[TEST] Connect() ... ");
        if (!controller.Connect())
        {
            Console.WriteLine("[FAIL] Connect() returned false.");
            Console.WriteLine("The ViGEmBus service/device may be disabled. Check Device Manager.");
            Pause();
            return;
        }

        Console.WriteLine("[PASS] Connect() succeeded.");
        Console.WriteLine($"[INFO] controller.IsConnected = {controller.IsConnected}");
        Console.WriteLine();

        var connectedAfter = XInputReader.FindConnectedSlots();
        Console.WriteLine($"[INFO] XInput slots connected AFTER creating the virtual pad: {XInputReader.FormatSlots(connectedAfter)}");
        var newSlots = connectedAfter.Except(connectedBefore).ToArray();
        if (newSlots.Length == 0)
        {
            Console.WriteLine("[FAIL] No NEW XInput slot appeared after Connect().");
            Pause();
            return;
        }

        Console.WriteLine($"[PASS] Windows detaches the virtual Xbox 360 controller: slot(s) {string.Join(",", newSlots)}");
        Console.WriteLine("       Verify manually with: 'joy.cpl' or Settings > Bluetooth & devices > Game controllers.");
        Console.WriteLine();

        Pause("Windows should now show the controller. Press ENTER to move the left stick...");

        controller.Reset();

        TestStick(controller, "LEFT", 0, -32768, XInputLeftThumbDeadZone);
        TestStick(controller, "LEFT", 32767, 0, XInputLeftThumbDeadZone);
        TestStick(controller, "LEFT", 0, 32767, XInputLeftThumbDeadZone);
        TestStick(controller, "LEFT", -32768, 0, XInputLeftThumbDeadZone);
        TestStick(controller, "LEFT", 0, 0, XInputLeftThumbDeadZone);

        Console.WriteLine();
        TestStick(controller, "RIGHT", 32767, 32767, XInputRightThumbDeadZone);

        Console.WriteLine();
        TestTrigger(controller, true, 255);
        TestTrigger(controller, false, 255);

        Console.WriteLine();
        TestButton(controller, ControllerButton.A, XInputA, "A");
        TestButton(controller, ControllerButton.B, XInputB, "B");
        TestButton(controller, ControllerButton.DPadUp, XInputDpadUp, "DPadUp");

        Console.WriteLine();
        controller.Reset();
        Console.Write("[TEST] Reset() -> readback... ");
        var state = XInputReader.ReadFirstConnected();
        Console.WriteLine(state is null
            ? "[FAIL] no XInput slot readable."
            : ExpectedState(state.Value.Gamepad, 0, 0, 0, 0, 0, 0)
                ? "[PASS] all values returned to neutral."
                : $"[FAIL] values not neutral: {XInputReader.FormatGamepad(state.Value.Gamepad)}");
        Console.WriteLine();

        Console.WriteLine();
        Console.Write("[TEST] Disconnect() ... ");
        controller.Disconnect();
        Console.WriteLine("[PASS] Disconnect() called (no exception).");

        var afterDisconnect = WaitForSlots(connectedBefore, TimeSpan.FromSeconds(2));
        var removed = connectedAfter.Except(afterDisconnect).ToArray();
        Console.WriteLine($"[INFO] XInput slots after disconnect: {XInputReader.FormatSlots(afterDisconnect)}");
        Console.WriteLine(removed.Length > 0
            ? $"[PASS] Virtual pad released slot(s) {string.Join(",", removed)}."
            : "[WARN] Slot(s) still reported by XInput after 2s (device removal is async; verify manually).");
    }

    private static void TestStick(IVirtualController controller, string stick, short x, short y, uint deadZone)
    {
        Console.Write($"[TEST] Set{stick}Stick({x}, {y}) ... ");
        if (stick == "LEFT")
            controller.SetLeftStick(x, y);
        else
            controller.SetRightStick(x, y);

        Thread.Sleep(150);

        var state = XInputReader.ReadFirstConnected();
        if (state is null)
        {
            Console.WriteLine("[FAIL] no XInput slot readable.");
            return;
        }

        var g = state.Value.Gamepad;
        short readX = stick == "LEFT" ? g.sThumbLX : g.sThumbRX;
        short readY = stick == "LEFT" ? g.sThumbLY : g.sThumbRY;

        var expectZero = x == 0 && Math.Abs((int)readX) < deadZone;
        var expectZeroY = y == 0 && Math.Abs((int)readY) < deadZone;
        bool ok = (expectZero || readX == x) && (expectZeroY || readY == y);

        Console.WriteLine(ok
            ? $"[PASS] XInput readback = ({readX}, {readY})"
            : $"[FAIL] readback = ({readX}, {readY}), expected ({x}, {y})");
    }

    private static void TestTrigger(IVirtualController controller, bool left, byte value)
    {
        string name = left ? "Left" : "Right";
        Console.Write($"[TEST] Set{name}Trigger({value}) ... ");
        if (left)
            controller.SetLeftTrigger(value);
        else
            controller.SetRightTrigger(value);

        Thread.Sleep(150);

        var state = XInputReader.ReadFirstConnected();
        if (state is null)
        {
            Console.WriteLine("[FAIL] no XInput slot readable.");
            return;
        }

        byte read = left ? state.Value.Gamepad.bLeftTrigger : state.Value.Gamepad.bRightTrigger;
        Console.WriteLine(read == value
            ? $"[PASS] XInput readback = {read}"
            : $"[FAIL] readback = {read}, expected {value}");
    }

    private static void TestButton(IVirtualController controller, ControllerButton button, ushort expectedBit, string label)
    {
        Console.Write($"[TEST] SetButton({label}, true) ... ");
        controller.SetButton(button, true);
        Thread.Sleep(150);

        var state = XInputReader.ReadFirstConnected();
        if (state is null)
        {
            Console.WriteLine("[FAIL] no XInput slot readable.");
            return;
        }

        bool pressed = (state.Value.Gamepad.wButtons & expectedBit) != 0;
        Console.WriteLine(pressed
            ? $"[PASS] XInput reports button pressed (wButtons=0x{state.Value.Gamepad.wButtons:X4})"
            : $"[FAIL] button not reported (wButtons=0x{state.Value.Gamepad.wButtons:X4})");

        Console.Write($"[TEST] SetButton({label}, false) ... ");
        controller.SetButton(button, false);
        Thread.Sleep(150);

        state = XInputReader.ReadFirstConnected();
        if (state is null)
        {
            Console.WriteLine("[FAIL] no XInput slot readable.");
            return;
        }

        bool stillPressed = (state.Value.Gamepad.wButtons & expectedBit) != 0;
        Console.WriteLine(!stillPressed ? "[PASS] button released." : "[FAIL] button still pressed.");
    }

    private static List<uint> WaitForSlots(List<uint> target, TimeSpan timeout)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var current = XInputReader.FindConnectedSlots();
        while (!current.SequenceEqual(target) && sw.Elapsed < timeout)
        {
            Thread.Sleep(100);
            current = XInputReader.FindConnectedSlots();
        }

        return current;
    }

    private static bool ExpectedState(XInputReader.XInputGamepad g, short lx, short ly, short rx, short ry, byte lt, byte rt)
        => g.sThumbLX == lx && g.sThumbLY == ly && g.sThumbRX == rx && g.sThumbRY == ry
           && g.bLeftTrigger == lt && g.bRightTrigger == rt && g.wButtons == 0;

    private static void Pause(string? message = null)
    {
        if (_autoMode)
            return;

        Console.WriteLine(message ?? "Press any key to continue...");
        Console.ReadKey(false);
        Console.WriteLine();
    }
}