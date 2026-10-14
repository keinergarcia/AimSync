using AimSync.Core;
using AimSync.Core.Input;

namespace AimSync.Test;

internal static class Phase1Tests
{
    private const short MaxAxis = short.MaxValue;
    private const short MinAxis = -short.MaxValue;

    public static int RunAll()
    {
        int failures = 0;

        Run("1. W genera Left Stick Up", Test1_W_LeftStickUp, ref failures);
        Run("2. A genera Left Stick Left", Test2_A_LeftStickLeft, ref failures);
        Run("3. S genera Left Stick Down", Test3_S_LeftStickDown, ref failures);
        Run("4. D genera Left Stick Right", Test4_D_LeftStickRight, ref failures);
        Run("5. W+D genera diagonal correcta", Test5_WD_Diagonal, ref failures);
        Run("6. Soltar tecla deja el eje en 0", Test6_ReleaseKeyZeroesAxis, ref failures);
        Run("7. Mouse DX positivo mueve Right Stick a X+", Test7_MouseDXPositive, ref failures);
        Run("8. Mouse DX negativo mueve Right Stick a X-", Test8_MouseDXNegative, ref failures);
        Run("9. Mouse DY segun configuracion (normal/invertido)", Test9_MouseDYConfig, ref failures);
        Run("10. Click izquierdo activa RT", Test10_LeftClickActivatesRT, ref failures);
        Run("11. Click derecho activa LT", Test11_RightClickActivatesLT, ref failures);
        Run("12. Activar crea el controlador virtual", Test12_ActivateCreatesController, ref failures);
        Run("13. Desactivar devuelve todo a estado neutro", Test13_DeactivateNeutral, ref failures);
        Run("14. F8 activa/desactiva el sistema", Test14_F8Toggle, ref failures);
        Run("15. Input Tester refleja las entradas", Test15_TesterReflectsInputs, ref failures);
        Run("16. Deteccion KeyDown/KeyUp (eventos)", Test16_KeyDownUpEdges, ref failures);
        Run("17. Remapeo Space->A y Q->LB", Test17_ButtonRemap, ref failures);

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

    private static (InputManager manager, FakeKeyboardCapture keyboard, FakeMouseCapture mouse, SpyVirtualController spy) NewPipeline(
        AimSyncSettings? settings = null)
    {
        settings ??= new AimSyncSettings();
        var keyboard = new FakeKeyboardCapture();
        var mouse = new FakeMouseCapture();
        var spy = new SpyVirtualController();
        var manager = new InputManager(settings, () => spy, keyboard, mouse);
        return (manager, keyboard, mouse, spy);
    }

    private static (bool, string) Test1_W_LeftStickUp()
    {
        var (manager, keyboard, _, spy) = NewPipeline();
        manager.Activate();
        keyboard.SetDown(KeyCode.W, true);
        manager.TryTick();
        return spy.LeftX == 0 && spy.LeftY == MaxAxis
            ? (true, $"LeftStick X={spy.LeftX} Y={spy.LeftY}")
            : (false, $"LeftStick X={spy.LeftX} Y={spy.LeftY}, esperado (0, {MaxAxis})");
    }

    private static (bool, string) Test2_A_LeftStickLeft()
    {
        var (manager, keyboard, _, spy) = NewPipeline();
        manager.Activate();
        keyboard.SetDown(KeyCode.A, true);
        manager.TryTick();
        return spy.LeftX == MinAxis && spy.LeftY == 0
            ? (true, $"LeftStick X={spy.LeftX} Y={spy.LeftY}")
            : (false, $"LeftStick X={spy.LeftX} Y={spy.LeftY}, esperado ({MinAxis}, 0)");
    }

    private static (bool, string) Test3_S_LeftStickDown()
    {
        var (manager, keyboard, _, spy) = NewPipeline();
        manager.Activate();
        keyboard.SetDown(KeyCode.S, true);
        manager.TryTick();
        return spy.LeftX == 0 && spy.LeftY == MinAxis
            ? (true, $"LeftStick X={spy.LeftX} Y={spy.LeftY}")
            : (false, $"LeftStick X={spy.LeftX} Y={spy.LeftY}, esperado (0, {MinAxis})");
    }

    private static (bool, string) Test4_D_LeftStickRight()
    {
        var (manager, keyboard, _, spy) = NewPipeline();
        manager.Activate();
        keyboard.SetDown(KeyCode.D, true);
        manager.TryTick();
        return spy.LeftX == MaxAxis && spy.LeftY == 0
            ? (true, $"LeftStick X={spy.LeftX} Y={spy.LeftY}")
            : (false, $"LeftStick X={spy.LeftX} Y={spy.LeftY}, esperado ({MaxAxis}, 0)");
    }

    private static (bool, string) Test5_WD_Diagonal()
    {
        var (manager, keyboard, _, spy) = NewPipeline();
        manager.Activate();
        keyboard.SetDown(KeyCode.W, true);
        keyboard.SetDown(KeyCode.D, true);
        manager.TryTick();
        bool equal = Math.Abs(spy.LeftX - spy.LeftY) <= 1;
        bool magnitude = Math.Abs(spy.LeftX) > 16000 && Math.Abs(spy.LeftY) > 16000;
        return equal && magnitude && spy.LeftX > 0 && spy.LeftY > 0
            ? (true, $"LeftStick X={spy.LeftX} Y={spy.LeftY} (normalizado 0.707)")
            : (false, $"LeftStick X={spy.LeftX} Y={spy.LeftY}, esperado diagonal 0.707");
    }

    private static (bool, string) Test6_ReleaseKeyZeroesAxis()
    {
        var (manager, keyboard, _, spy) = NewPipeline();
        manager.Activate();
        keyboard.SetDown(KeyCode.W, true);
        manager.TryTick();
        keyboard.SetDown(KeyCode.W, false);
        manager.TryTick();
        return spy.LeftX == 0 && spy.LeftY == 0
            ? (true, "LeftStick vuelto a (0, 0)")
            : (false, $"LeftStick X={spy.LeftX} Y={spy.LeftY}, esperado (0, 0)");
    }

    private static (bool, string) Test7_MouseDXPositive()
    {
        var (manager, _, mouse, spy) = NewPipeline();
        manager.Activate();
        mouse.SetDelta(60, 0);
        manager.TryTick();
        return spy.RightX > 0 && spy.RightY == 0
            ? (true, $"RightStick X={spy.RightX} Y={spy.RightY} (DX=60)")
            : (false, $"RightStick X={spy.RightX} Y={spy.RightY}, esperado X>0");
    }

    private static (bool, string) Test8_MouseDXNegative()
    {
        var (manager, _, mouse, spy) = NewPipeline();
        manager.Activate();
        mouse.SetDelta(-60, 0);
        manager.TryTick();
        return spy.RightX < 0 && spy.RightY == 0
            ? (true, $"RightStick X={spy.RightX} Y={spy.RightY} (DX=-60)")
            : (false, $"RightStick X={spy.RightX} Y={spy.RightY}, esperado X<0");
    }

    private static (bool, string) Test9_MouseDYConfig()
    {
        var defaultSettings = new AimSyncSettings();
        var (managerNorm, _, mouseNorm, spyNorm) = NewPipeline(defaultSettings);
        managerNorm.Activate();
        mouseNorm.SetDelta(0, 60);
        managerNorm.TryTick();
        bool normalOk = spyNorm.RightY < 0;

        var inverted = new AimSyncSettings();
        inverted.Mapping.InvertMouseY = true;
        var (managerInv, _, mouseInv, spyInv) = NewPipeline(inverted);
        managerInv.Activate();
        mouseInv.SetDelta(0, 60);
        managerInv.TryTick();
        bool invertedOk = spyInv.RightY > 0;

        return normalOk && invertedOk
            ? (true, $"normal(DY=60)->Y={spyNorm.RightY} (abajo), invertido(DY=60)->Y={spyInv.RightY} (arriba)")
            : (false, $"normal(DY=60)->Y={spyNorm.RightY} (esperado <0), invertido(DY=60)->Y={spyInv.RightY} (esperado >0)");
    }

    private static (bool, string) Test10_LeftClickActivatesRT()
    {
        var (manager, _, mouse, spy) = NewPipeline();
        manager.Activate();
        mouse.SetButtonDown(MouseButton.Left, true);
        manager.TryTick();
        return spy.RightTrigger == byte.MaxValue && spy.LeftTrigger == 0
            ? (true, $"RT={spy.RightTrigger} LT={spy.LeftTrigger}")
            : (false, $"RT={spy.RightTrigger} LT={spy.LeftTrigger}, esperado RT=255 LT=0");
    }

    private static (bool, string) Test11_RightClickActivatesLT()
    {
        var (manager, _, mouse, spy) = NewPipeline();
        manager.Activate();
        mouse.SetButtonDown(MouseButton.Right, true);
        manager.TryTick();
        return spy.LeftTrigger == byte.MaxValue && spy.RightTrigger == 0
            ? (true, $"LT={spy.LeftTrigger} RT={spy.RightTrigger}")
            : (false, $"LT={spy.LeftTrigger} RT={spy.RightTrigger}, esperado LT=255 RT=0");
    }

    private static (bool, string) Test12_ActivateCreatesController()
    {
        int created = 0;
        SpyVirtualController? createdSpy = null;
        var settings = new AimSyncSettings();
        var manager = new InputManager(settings, () =>
        {
            created++;
            createdSpy = new SpyVirtualController();
            return createdSpy;
        }, new FakeKeyboardCapture(), new FakeMouseCapture());

        bool ok = manager.Activate();
        if (!ok)
            return (false, "Activate() devolvio false");
        if (created != 1 || createdSpy is null)
            return (false, $"factory invocada {created} veces, esperado 1");

        manager.Activate();
        bool reActivateNoRecreate = created == 1;

        return createdSpy.IsConnected && manager.IsEnabled && manager.Controller == createdSpy && reActivateNoRecreate
            ? (true, "controlador creado por factory, conectado y reutilizado")
            : (false, $"conectado={createdSpy.IsConnected} enabled={manager.IsEnabled} recreado={!reActivateNoRecreate}");
    }

    private static (bool, string) Test13_DeactivateNeutral()
    {
        var (manager, keyboard, _, spy) = NewPipeline();
        manager.Activate();
        keyboard.SetDown(KeyCode.W, true);
        manager.TryTick();
        if (spy.LeftY == 0)
            return (false, "precondicion fallida: LeftY no se activo antes de desactivar");

        manager.Deactivate();
        bool neutralAfterDeactivate = spy.IsNeutral;
        manager.TryTick();
        bool stillNeutralAfterTick = spy.IsNeutral;
        bool outputNeutral = manager.CurrentOutput.LeftX == 0 && manager.CurrentOutput.LeftY == 0;

        return !manager.IsEnabled && neutralAfterDeactivate && stillNeutralAfterTick && outputNeutral
            ? (true, "estado neutro tras Deactivate (Reset llamado y sin escrituras posteriores)")
            : (false, $"enabled={manager.IsEnabled} neutral={neutralAfterDeactivate} despuesTick={stillNeutralAfterTick} output={outputNeutral}");
    }

    private static (bool, string) Test14_F8Toggle()
    {
        var (manager, keyboard, _, spy) = NewPipeline();
        if (manager.IsEnabled)
            return (false, "precondicion fallida: sistema inicialmente activado");

        keyboard.SetDown(KeyCode.F8, true);
        manager.TryTick();
        bool enabledFirst = manager.IsEnabled && spy.IsConnected && spy.ConnectCount == 1;

        keyboard.SetDown(KeyCode.F8, false);
        manager.TryTick();
        bool heldOnce = manager.IsEnabled;

        keyboard.SetDown(KeyCode.F8, true);
        manager.TryTick();
        bool disabledSecond = !manager.IsEnabled && spy.IsNeutral;

        keyboard.SetDown(KeyCode.F8, false);
        manager.TryTick();
        bool releasedOk = !manager.IsEnabled;

        return enabledFirst && heldOnce && disabledSecond && releasedOk
            ? (true, "F8: activa (crea+conecta) y desactiva (neutro)")
            : (false, $"1ra={enabledFirst} sostenida={heldOnce} 2daOff={disabledSecond} liberada={releasedOk}");
    }

    private static (bool, string) Test15_TesterReflectsInputs()
    {
        var (manager, keyboard, mouse, spy) = NewPipeline();
        manager.Activate();
        keyboard.SetDown(KeyCode.W, true);
        mouse.SetDelta(0, -50);
        mouse.SetButtonDown(MouseButton.Left, true);
        manager.TryTick();

        var output = manager.CurrentOutput;
        bool keyboardReflects = manager.Keyboard.IsPressed(KeyCode.W);
        bool mouseReflects = manager.Mouse.DeltaX == 0 && manager.Mouse.DeltaY == -50
                             && manager.Mouse.IsButtonDown(MouseButton.Left);
        bool outputMatches = output.LeftY > 0 && output.RightY > 0 && output.RightTrigger == byte.MaxValue;
        bool spyMatches = spy.LeftY > 0 && spy.RightY > 0 && spy.RightTrigger == byte.MaxValue;
        bool outputEqualsSpy = output.LeftY > 0 && output.RightY > 0 && output.RightTrigger == byte.MaxValue
                               && Math.Abs(output.LeftY * short.MaxValue - spy.LeftY) <= 1f
                               && Math.Abs(output.RightY * short.MaxValue - spy.RightY) <= 1f;

        return keyboardReflects && mouseReflects && outputMatches && spyMatches && outputEqualsSpy
            ? (true, $"testigo: KB(W) Mouse(DY=-50,L) Out(LSY>0 RSY>0 RT=255) Spy espejo Ok")
            : (false, $"KB={keyboardReflects} Mouse={mouseReflects} Output={outputMatches} Spy={spyMatches} Espejo={outputEqualsSpy}");
    }

    private static (bool, string) Test16_KeyDownUpEdges()
    {
        var keyboard = new FakeKeyboardCapture();
        var mouse = new FakeMouseCapture();
        var spy = new SpyVirtualController();
        var manager = new InputManager(new AimSyncSettings(), () => spy, keyboard, mouse);

        int keyDownCount = 0;
        int keyUpCount = 0;
        KeyCode? lastDown = null;
        KeyCode? lastUp = null;
        manager.Keyboard.KeyDown += k => { keyDownCount++; lastDown = k; };
        manager.Keyboard.KeyUp += k => { keyUpCount++; lastUp = k; };

        keyboard.SetDown(KeyCode.W, true);
        manager.TryTick();
        bool downFired = keyDownCount == 1 && lastDown == KeyCode.W;

        bool noRepeatWhileHeld = keyDownCount == 1;
        manager.TryTick();
        noRepeatWhileHeld = noRepeatWhileHeld && keyDownCount == 1;

        keyboard.SetDown(KeyCode.W, false);
        manager.TryTick();
        bool upFired = keyUpCount == 1 && lastUp == KeyCode.W;

        return downFired && noRepeatWhileHeld && upFired
            ? (true, $"KeyDown x{keyDownCount} (W), sin repeticion mientras se mantiene, KeyUp x{keyUpCount} (W)")
            : (false, $"down={downFired} sinRepetir={noRepeatWhileHeld} up={upFired} (downCount={keyDownCount}, upCount={keyUpCount})");
    }

    private static (bool, string) Test17_ButtonRemap()
    {
        var (manager, keyboard, _, spy) = NewPipeline();
        manager.Activate();

        keyboard.SetDown(KeyCode.Space, true);
        manager.TryTick();
        bool aPressed = spy.GetButton(ControllerButton.A);

        keyboard.SetDown(KeyCode.Q, true);
        manager.TryTick();
        bool lbPressed = spy.GetButton(ControllerButton.LeftShoulder);

        keyboard.SetDown(KeyCode.Space, false);
        manager.TryTick();
        bool aReleased = !spy.GetButton(ControllerButton.A);

        return aPressed && lbPressed && aReleased
            ? (true, "Space->A [up], Q->LB [up], Space liberado->A off")
            : (false, $"A={aPressed} LB={lbPressed} ALiberado={aReleased}");
    }
}