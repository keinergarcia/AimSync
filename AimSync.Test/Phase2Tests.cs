using AimSync.App.Services;
using AimSync.Core;
using AimSync.Core.Devices;
using AimSync.Core.Input;
using AimSync.Core.Storage;

namespace AimSync.Test;

internal static class Phase2Tests
{
    private const short MaxAxis = short.MaxValue;

    private static float BoostForTest(float value) => MathF.CopySign(MathF.Sqrt(MathF.Abs(value)), value);

    public static int RunAll()
    {
        int failures = 0;

        Run("P2-1 Service Activate: instancia unica + IsEnabled + IsConnected", TestServiceActivateSingleInstance, ref failures);
        Run("P2-2 Service Deactivate: IsEnabled=false y neutro", TestServiceDeactivate, ref failures);
        Run("P2-3 Service Toggle: on/off/on", TestServiceToggle, ref failures);
        Run("P2-4 StateChanged se dispara al activar/desactivar", TestStateChangedEvents, ref failures);
        Run("P2-5 Sensibilidad 1x: DX=60 -> RightStick X=0.60", TestSens1x, ref failures);
        Run("P2-6 Sensibilidad 2x: DX=60 -> clamp a full stick", TestSens2xClamp, ref failures);
        Run("P2-7 Sensibilidad 8x: DX=15 -> full stick", TestSens8x, ref failures);
        Run("P2-8 Movimiento lento DX=5 (sin deadzone) -> X != 0", TestSlowMovementNotEaten, ref failures);
        Run("P2-9 MaxDelta 100: DX=100 -> full stick a 1x", TestMaxDeltaFullAt1x, ref failures);
        Run("P2-10 Anti-recoil Arma 1: empuje vertical 0.3 al disparar", TestAntiRecoilArma1, ref failures);
        Run("P2-11 Anti-recoil Arma 2: empuje vertical 0.5 al disparar", TestAntiRecoilArma2, ref failures);
        Run("P2-12 Anti-recoil: sin disparo no aplica empuje", TestAntiRecoilNotFiring, ref failures);
        Run("P2-13 Anti-recoil: desactivado no aplica nada", TestAntiRecoilDisabled, ref failures);
        Run("P2-14 Oscilacion: stick izquierdo ondula mientras se dispara", TestOscillationLeftStick, ref failures);
        Run("P2-15 Deteccion auto: tecla 1 cambia a Arma 1 al disparar", TestWeaponDetectionKey1, ref failures);
        Run("P2-16 Deteccion auto: tecla 2 cambia a Arma 2 al disparar", TestWeaponDetectionKey2, ref failures);
        Run("P2-17 Deteccion OFF: teclas 1/2 ignoradas (perfil manual)", TestWeaponDetectionDisabledIgnoresKeys, ref failures);
        Run("P2-18 SelectWeapon: ActiveWeapon + evento WeaponChanged + persistencia", TestSelectWeaponEvent, ref failures);
        Run("P2-19 Deteccion solo con anti-recoil ON", TestWeaponDetectionRequiresEnabled, ref failures);
        Run("P2-20 Empuje + mouse simultaneos (X e Y) sin perderse", TestAntiRecoilCombinedWithMouse, ref failures);
        Run("P2-21 Clamp al disparar: empuje + mouse extremos -> [-1,1]", TestAntiRecoilClamp, ref failures);
        Run("P2-22 Fase de oscilacion se reinicia al soltar el disparo", TestOscillationPhaseReset, ref failures);
        Run("P2-23 Rueda del raton: arriba=Arma 1, abajo=Arma 2", TestWheelSwitchWeapon, ref failures);
        Run("P2-24 Rueda con deteccion OFF: ignorada", TestWheelIgnoredWhenDetectionOff, ref failures);
        Run("P2-25 Empuje vertical negativo (izquierda) baja la mira", TestNegativeVerticalPush, ref failures);
        Run("P2-26 Valores por defecto: empuje en 0 y oscilacion a 8 Hz (registrable por el juego)", TestDefaultsZeroAndFaster, ref failures);
        Run("P2-27 Curva de fuerza: medio amplificado y 100%/-100% = stick completo", TestPushForceCurve, ref failures);
        Run("P2-28 Oscilacion a 120 Hz: alterna rapido y nunca se congela", TestHighFreqOscillation, ref failures);
        Run("P2-29 Refuerzo por rafagas: reafirma a ~20 Hz y nunca excede el stick completo", TestPumpOverdrive, ref failures);
        Run("P2-30 Turbo 10 Hz: gatillo alterna 255/0 en ciclos de 25 ticks", TestTurboPulsePattern, ref failures);
        Run("P2-31 Turbo desactivado o sin gatillo activo: gatillo constante", TestTurboDisabledStaysConstant, ref failures);
        Run("P2-32 Macro: reproduce los pasos en orden (X 60ms -> Y 60ms)", TestMacroReproducesSteps, ref failures);
        Run("P2-33 Macro: se reinicia al soltar el disparador", TestMacroReinitOnRelease, ref failures);
        Run("P2-34 Macro con Loop: la secuencia se repite mientras se mantiene", TestMacroLoopRepeats, ref failures);
        Run("P2-35 Persistencia: guardar/cargar turbo y macros en JSON (redondear)", TestSettingsStoreRoundTrip, ref failures);
        Run("P3-1 Passthrough: boton Y del mando fisico pasa al virtual (y se suelta)", TestPhysicalPassthroughButton, ref failures);
        Run("P3-2 Mando fisico como entrada: dispara una macro (Y -> X)", TestPhysicalTriggersMacro, ref failures);
        Run("P3-3 Rafaga maxima: Y alterna ON/OFF 1+1 tick (~125 clics/s) al mantener", TestRapidButtonPulsePattern, ref failures);
        Run("P3-4 Rafaga maxima: modos turbo OFF = boton constante (sin rafaga)", TestRapidButtonNeedsMaster, ref failures);
        Run("P3-5 Gatillos fisicos se combinan (Max) y sticks fisicos se reenvian", TestPhysicalTriggersAndSticks, ref failures);
        Run("P3-6 Persistencia: la lista de botones en rafaga se guarda y se recarga", TestRapidButtonsRoundTrip, ref failures);

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

    private static (InputManager, FakeKeyboardCapture, FakeMouseCapture, SpyVirtualController) NewPipeline(AimSyncSettings settings)
    {
        var keyboard = new FakeKeyboardCapture();
        var mouse = new FakeMouseCapture();
        var spy = new SpyVirtualController();
        var manager = new InputManager(settings, () => spy, keyboard, mouse);
        manager.Activate();
        return (manager, keyboard, mouse, spy);
    }

    private static (InputManager, FakeKeyboardCapture, FakeMouseCapture, SpyVirtualController) NewPipeline(AimSyncSettings settings, IPhysicalControllerSource physical)
    {
        var keyboard = new FakeKeyboardCapture();
        var mouse = new FakeMouseCapture();
        var spy = new SpyVirtualController();
        var manager = new InputManager(settings, () => spy, keyboard, mouse, physical);
        manager.Activate();
        return (manager, keyboard, mouse, spy);
    }

    private static (bool, string) TickExpectX(InputManager manager, FakeMouseCapture mouse, SpyVirtualController spy, int dx, float expected)
    {
        mouse.SetDelta(dx, 0);
        manager.TryTick();
        short expectedShort = (short)MathF.Round(Math.Clamp(expected, -1f, 1f) * MaxAxis);
        bool ok = spy.RightX == expectedShort;
        return (ok, $"DX={dx} -> X={spy.RightX} (normalizado {spy.RightX / (double)MaxAxis:0.00}), esperado {expectedShort} ({expected:0.00})");
    }

    private static (bool, string) TestServiceActivateSingleInstance()
    {
        var settings = new AimSyncSettings();
        var spy = new SpyVirtualController();
        int created = 0;
        using var service = new AimSyncService(settings, () =>
        {
            created++;
            return spy;
        });

        if (service.IsEnabled || service.IsConnected)
            return (false, $"precondicion: enabled={service.IsEnabled} connected={service.IsConnected}");

        bool ok = service.Activate();
        bool enabledAfter = service.IsEnabled;
        bool connectedAfter = service.IsConnected;
        bool createdOnce = created == 1;
        bool controllerSame = service.Controller == spy;

        service.Activate();
        bool noRecreate = created == 1;
        service.Deactivate();

        return ok && enabledAfter && connectedAfter && createdOnce && controllerSame && noRecreate
            ? (true, $"enabled={enabledAfter} connected={connectedAfter} factory={created} vez(es), misma instancia={controllerSame}")
            : (false, $"ok={ok} enabled={enabledAfter} connected={connectedAfter} created={created} misma={controllerSame} noRecrea={noRecreate}");
    }

    private static (bool, string) TestServiceDeactivate()
    {
        var settings = new AimSyncSettings();
        var spy = new SpyVirtualController();
        using var service = new AimSyncService(settings, () => spy);

        if (!service.Activate())
            return (false, "Activate fallo");

        service.Deactivate();
        bool disabled = !service.IsEnabled;
        bool spyNeutral = spy.IsNeutral;

        return disabled && spyNeutral
            ? (true, $"enabled={service.IsEnabled} spy.IsNeutral={spyNeutral}")
            : (false, $"enabled={service.IsEnabled} spy.IsNeutral={spyNeutral}");
    }

    private static (bool, string) TestServiceToggle()
    {
        var settings = new AimSyncSettings();
        var spy = new SpyVirtualController();
        using var service = new AimSyncService(settings, () => spy);

        service.Toggle();
        bool firstOn = service.IsEnabled && spy.IsConnected;
        service.Toggle();
        bool secondOff = !service.IsEnabled;

        service.Toggle();
        bool thirdOn = service.IsEnabled;

        return firstOn && secondOff && thirdOn
            ? (true, "Toggle: on->off->on")
            : (false, $"1er={firstOn} 2do={secondOff} 3ro={thirdOn}");
    }

    private static (bool, string) TestStateChangedEvents()
    {
        var settings = new AimSyncSettings();
        var spy = new SpyVirtualController();
        using var service = new AimSyncService(settings, () => spy);

        int eventCount = 0;
        service.StateChanged += () => eventCount++;

        service.Activate();
        int afterActivate = eventCount;
        bool stateRanOn = service.IsEnabled && service.IsConnected;

        service.Deactivate();
        bool stateRanOff = !service.IsEnabled;
        int total = eventCount;

        return stateRanOn && stateRanOff && afterActivate >= 1 && total >= 2
            ? (true, $"StateChanged: Activate x{afterActivate}, Deactivate x{total - afterActivate}, total={total}")
            : (false, $"afterActivate={afterActivate} total={total} on={stateRanOn} off={stateRanOff}");
    }

    private static (bool, string) TestSens1x()
    {
        var settings = new AimSyncSettings();
        settings.Mapping.MouseSensitivityX = 1f;
        settings.Mapping.MouseSensitivityY = 1f;
        settings.Mapping.MouseMaxDelta = 100f;
        var (manager, _, mouse, spy) = NewPipeline(settings);
        return TickExpectX(manager, mouse, spy, dx: 60, expected: 60f / 100f);
    }

    private static (bool, string) TestSens2xClamp()
    {
        var settings = new AimSyncSettings();
        settings.Mapping.MouseSensitivityX = 2f;
        settings.Mapping.MouseSensitivityY = 2f;
        settings.Mapping.MouseMaxDelta = 100f;
        var (manager, _, mouse, spy) = NewPipeline(settings);
        var (ok, detail) = TickExpectX(manager, mouse, spy, dx: 60, expected: 1f);
        return ok
            ? (true, $"{detail} — antes (Sens1/200) habria sido solo {60 / 200f:0.00}")
            : (false, detail);
    }

    private static (bool, string) TestSens8x()
    {
        var settings = new AimSyncSettings();
        settings.Mapping.MouseSensitivityX = 8f;
        settings.Mapping.MouseSensitivityY = 8f;
        settings.Mapping.MouseMaxDelta = 100f;
        var (manager, _, mouse, spy) = NewPipeline(settings);
        return TickExpectX(manager, mouse, spy, dx: 15, expected: 1f);
    }

    private static (bool, string) TestSlowMovementNotEaten()
    {
        var settings = new AimSyncSettings();
        settings.Mapping.MouseSensitivityX = 1f;
        settings.Mapping.MouseSensitivityY = 1f;
        settings.Mapping.MouseMaxDelta = 100f;
        settings.RightStick.Deadzone = 0f;
        var (manager, _, mouse, spy) = NewPipeline(settings);
        var (ok, detail) = TickExpectX(manager, mouse, spy, dx: 5, expected: 5f / 100f);
        return ok
            ? (true, $"{detail} — con deadzone 0.05 anterior se anulaba")
            : (false, detail);
    }

    private static (bool, string) TestMaxDeltaFullAt1x()
    {
        var settings = new AimSyncSettings();
        settings.Mapping.MouseSensitivityX = 1f;
        settings.Mapping.MouseSensitivityY = 1f;
        settings.Mapping.MouseMaxDelta = 100f;
        var (manager, _, mouse, spy) = NewPipeline(settings);
        return TickExpectX(manager, mouse, spy, dx: 100, expected: 1f);
    }

    private static (bool, string) TestAntiRecoilArma1()
    {
        var settings = new AimSyncSettings();
        var cfg = settings.AntiRecoil;
        cfg.Enabled = true;
        cfg.UseArma2 = false;
        cfg.Arma1Vertical = 0.3f;
        cfg.Arma1Horizontal = 0f;
        var (manager, _, mouse, spy) = NewPipeline(settings);

        mouse.SetButtonDown(MouseButton.Left, true); // gatillo derecho (disparo)
        manager.TryTick();
        short expectedY = (short)MathF.Round(BoostForTest(0.3f) * MaxAxis);
        bool ok = spy.RightY == expectedY && spy.RightX == 0;
        return ok
            ? (true, $"disparando: RightY={spy.RightY} (esperado {expectedY}), X={spy.RightX}")
            : (false, $"RightY={spy.RightY} (esperado {expectedY}), X={spy.RightX}");
    }

    private static (bool, string) TestAntiRecoilArma2()
    {
        var settings = new AimSyncSettings();
        var cfg = settings.AntiRecoil;
        cfg.Enabled = true;
        cfg.UseArma2 = true;
        cfg.Arma2Vertical = 0.5f;
        cfg.Arma2Horizontal = 0f;
        var (manager, _, mouse, spy) = NewPipeline(settings);

        mouse.SetButtonDown(MouseButton.Left, true);
        manager.TryTick();
        short expectedY = (short)MathF.Round(BoostForTest(0.5f) * MaxAxis);
        bool ok = spy.RightY == expectedY;
        return ok
            ? (true, $"Arma 2 activa: RightY={spy.RightY} (esperado {expectedY})")
            : (false, $"RightY={spy.RightY} (esperado {expectedY})");
    }

    private static (bool, string) TestAntiRecoilNotFiring()
    {
        var settings = new AimSyncSettings();
        settings.AntiRecoil.Enabled = true;
        settings.AntiRecoil.Arma1Vertical = 0.3f;
        var (manager, _, mouse, spy) = NewPipeline(settings);

        manager.TryTick(); // sin disparar
        bool ok = spy.RightY == 0 && spy.RightX == 0;
        return ok
            ? (true, "sin disparo: RightY=0, RightX=0")
            : (false, $"RightY={spy.RightY} RightX={spy.RightX}");
    }

    private static (bool, string) TestAntiRecoilDisabled()
    {
        var settings = new AimSyncSettings();
        settings.AntiRecoil.Enabled = false;
        settings.AntiRecoil.Arma1Vertical = 0.9f;
        var (manager, _, mouse, spy) = NewPipeline(settings);

        mouse.SetButtonDown(MouseButton.Left, true);
        manager.TryTick();
        bool ok = spy.RightY == 0;
        return ok
            ? (true, "sistema OFF: disparar no aplica empuje")
            : (false, $"RightY={spy.RightY} (esperado 0)");
    }

    private static (bool, string) TestOscillationLeftStick()
    {
        var settings = new AimSyncSettings();
        var cfg = settings.AntiRecoil;
        cfg.Enabled = true;
        cfg.Arma1Vertical = 0f;
        cfg.Arma1Horizontal = 0f;
        cfg.Oscillation = true;
        cfg.OscillationStrength = 0.3f;
        cfg.OscillationFrequency = 2f;
        var (manager, _, mouse, spy) = NewPipeline(settings);

        mouse.SetButtonDown(MouseButton.Left, true);

        for (int i = 0; i < 20; i++)
            manager.TryTick();
        short at20 = spy.LeftX;

        for (int i = 20; i < 110; i++)
            manager.TryTick();
        short at110 = spy.LeftX;

        bool ok = at20 > 0 && at110 < 0;
        return ok
            ? (true, $"oscila: LeftX tick20={at20} (>0), tick110={at110} (<0)")
            : (false, $"LeftX tick20={at20}, tick110={at110}");
    }

    private static (bool, string) TestWeaponDetectionKey1()
    {
        var settings = new AimSyncSettings();
        var cfg = settings.AntiRecoil;
        cfg.Enabled = true;
        cfg.UseArma2 = true; // arranca en Arma 2
        cfg.Arma1Vertical = 0.3f;
        cfg.Arma2Vertical = 0.5f;
        cfg.WeaponDetectionEnabled = true;
        var (manager, keyboard, mouse, spy) = NewPipeline(settings);

        mouse.SetButtonDown(MouseButton.Left, true);
        manager.TryTick();
        short before = spy.RightY; // arma 2 -> 0.5

        keyboard.Press(KeyCode.Digit1);
        manager.TryTick(); // cambia a arma 1 y aplica
        short after = spy.RightY;

        short expBefore = (short)MathF.Round(BoostForTest(0.5f) * MaxAxis);
        short expAfter = (short)MathF.Round(BoostForTest(0.3f) * MaxAxis);
        bool ok = before == expBefore && after == expAfter && manager.ActiveWeapon == 1;
        return ok
            ? (true, $"arma2->arma1: Y antes={before} ({expBefore}), despues={after} ({expAfter}), ActiveWeapon={manager.ActiveWeapon}")
            : (false, $"Y antes={before} ({expBefore}), despues={after} ({expAfter}), ActiveWeapon={manager.ActiveWeapon}");
    }

    private static (bool, string) TestWeaponDetectionKey2()
    {
        var settings = new AimSyncSettings();
        var cfg = settings.AntiRecoil;
        cfg.Enabled = true;
        cfg.Arma1Vertical = 0.3f;
        cfg.Arma2Vertical = 0.5f;
        cfg.WeaponDetectionEnabled = true;
        var (manager, keyboard, mouse, spy) = NewPipeline(settings);

        mouse.SetButtonDown(MouseButton.Left, true);
        manager.TryTick();
        short before = spy.RightY; // arma 1 -> 0.3

        keyboard.Press(KeyCode.Digit2);
        manager.TryTick(); // cambia a arma 2 y aplica
        short after = spy.RightY;

        short expBefore = (short)MathF.Round(BoostForTest(0.3f) * MaxAxis);
        short expAfter = (short)MathF.Round(BoostForTest(0.5f) * MaxAxis);
        bool ok = before == expBefore && after == expAfter && manager.ActiveWeapon == 2;
        return ok
            ? (true, $"arma1->arma2: Y antes={before} ({expBefore}), despues={after} ({expAfter}), ActiveWeapon={manager.ActiveWeapon}")
            : (false, $"Y antes={before} ({expBefore}), despues={after} ({expAfter}), ActiveWeapon={manager.ActiveWeapon}");
    }

    private static (bool, string) TestWeaponDetectionDisabledIgnoresKeys()
    {
        var settings = new AimSyncSettings();
        var cfg = settings.AntiRecoil;
        cfg.Enabled = true;
        cfg.WeaponDetectionEnabled = false;
        cfg.Arma1Vertical = 0.3f;
        cfg.Arma2Vertical = 0.5f;
        var (manager, keyboard, mouse, spy) = NewPipeline(settings);

        mouse.SetButtonDown(MouseButton.Left, true);
        manager.TryTick();
        short before = spy.RightY; // arma 1 -> 0.3

        keyboard.Press(KeyCode.Digit2);
        manager.TryTick();

        short exp = (short)MathF.Round(BoostForTest(0.3f) * MaxAxis);
        bool ok = spy.RightY == exp && manager.ActiveWeapon == 1;
        return ok
            ? (true, $"deteccion OFF: sigue arma 1, Y={spy.RightY} ({exp}), ActiveWeapon={manager.ActiveWeapon}")
            : (false, $"Y={spy.RightY} ({exp}), ActiveWeapon={manager.ActiveWeapon}");
    }

    private static (bool, string) TestSelectWeaponEvent()
    {
        var settings = new AimSyncSettings();
        settings.AntiRecoil.Enabled = true;
        var (manager, keyboard, mouse, spy) = NewPipeline(settings);

        int events = 0;
        manager.WeaponChanged += () => events++;

        bool init = manager.ActiveWeapon == 1 && events == 0;
        manager.SelectWeapon(2);
        bool to2 = manager.ActiveWeapon == 2 && events == 1 && settings.AntiRecoil.UseArma2;
        manager.SelectWeapon(2);
        bool noDuplicate = events == 1;
        manager.SelectWeapon(9);
        bool invalidIgnored = manager.ActiveWeapon == 2 && events == 1;
        manager.SelectWeapon(1);
        bool back1 = manager.ActiveWeapon == 1 && events == 2 && !settings.AntiRecoil.UseArma2;

        bool ok = init && to2 && noDuplicate && invalidIgnored && back1;
        return ok
            ? (true, $"init=1, 2->{manager.ActiveWeapon}, eventos={events}, UseArma2 persistido={settings.AntiRecoil.UseArma2}")
            : (false, $"init={init} to2={to2} noDup={noDuplicate} invalid={invalidIgnored} back1={back1}, eventos={events}");
    }

    private static (bool, string) TestWeaponDetectionRequiresEnabled()
    {
        var settings = new AimSyncSettings();
        settings.AntiRecoil.Enabled = false; // sistema OFF, aunque manager este activo
        var (manager, keyboard, mouse, spy) = NewPipeline(settings);

        int events = 0;
        manager.WeaponChanged += () => events++;

        keyboard.Press(KeyCode.Digit2);
        mouse.SetButtonDown(MouseButton.Left, true);
        manager.TryTick();

        bool ok = manager.ActiveWeapon == 1 && events == 0;
        return ok
            ? (true, "anti-recoil OFF: tecla 2 ignorada, ActiveWeapon=1, 0 eventos")
            : (false, $"ActiveWeapon={manager.ActiveWeapon}, eventos={events}");
    }

    private static (bool, string) TestAntiRecoilCombinedWithMouse()
    {
        var settings = new AimSyncSettings();
        settings.Mapping.MouseSensitivityX = 1f;
        settings.Mapping.MouseSensitivityY = 1f;
        settings.Mapping.MouseMaxDelta = 100f;
        var cfg = settings.AntiRecoil;
        cfg.Enabled = true;
        cfg.Arma1Vertical = 0.3f;
        cfg.Arma1Horizontal = 0f;
        var (manager, _, mouse, spy) = NewPipeline(settings);

        mouse.SetDelta(50, 0);
        mouse.SetButtonDown(MouseButton.Left, true);
        manager.TryTick();

        short expX = (short)MathF.Round(0.5f * MaxAxis);
        short expY = (short)MathF.Round(BoostForTest(0.3f) * MaxAxis);
        bool ok = spy.RightX == expX && spy.RightY == expY;
        return ok
            ? (true, $"DX=50 -> X={spy.RightX} ({expX}), Y={spy.RightY} ({expY})")
            : (false, $"X={spy.RightX} ({expX}), Y={spy.RightY} ({expY})");
    }

    private static (bool, string) TestAntiRecoilClamp()
    {
        var settings = new AimSyncSettings();
        settings.Mapping.MouseSensitivityX = 1f;
        settings.Mapping.MouseSensitivityY = 1f;
        settings.Mapping.MouseMaxDelta = 100f;
        var cfg = settings.AntiRecoil;
        cfg.Enabled = true;
        cfg.Arma1Vertical = 0.9f;
        cfg.Arma1Horizontal = 0.3f;
        var (manager, _, mouse, spy) = NewPipeline(settings);

        mouse.SetDelta(100, 0); // X = 1.0 (full stick) ya
        mouse.SetButtonDown(MouseButton.Left, true);
        manager.TryTick();

        short expX = MaxAxis; // 1.0 + 0.3 -> clamp a full
        short expY = (short)MathF.Round(BoostForTest(0.9f) * MaxAxis);
        bool ok = spy.RightX == expX && spy.RightY == expY;
        return ok
            ? (true, $"clamp: X={spy.RightX} (esperado {expX}), Y={spy.RightY} ({expY})")
            : (false, $"X={spy.RightX} (esperado {expX}), Y={spy.RightY} ({expY})");
    }

    private static (bool, string) TestOscillationPhaseReset()
    {
        var settings = new AimSyncSettings();
        var cfg = settings.AntiRecoil;
        cfg.Enabled = true;
        cfg.Arma1Vertical = 0f;
        cfg.Arma1Horizontal = 0f;
        cfg.Oscillation = true;
        cfg.OscillationStrength = 0.3f;
        cfg.OscillationFrequency = 2f;
        var (manager, _, mouse, spy) = NewPipeline(settings);

        mouse.SetButtonDown(MouseButton.Left, true);

        short[] burstA = new short[4];
        for (int i = 0; i < burstA.Length; i++)
        {
            manager.TryTick();
            burstA[i] = spy.LeftX;
        }

        mouse.SetButtonDown(MouseButton.Left, false);
        for (int i = 0; i < 3; i++)
            manager.TryTick();
        bool neutral = spy.LeftX == 0;

        mouse.SetButtonDown(MouseButton.Left, true);
        short[] burstB = new short[4];
        for (int i = 0; i < burstB.Length; i++)
        {
            manager.TryTick();
            burstB[i] = spy.LeftX;
        }

        bool sameSequence = burstA.SequenceEqual(burstB);
        bool ok = sameSequence && neutral && burstB.Any(v => v != 0);
        return ok
            ? (true, $"fase reinicia: burstA=[{string.Join(",", burstA)}] == burstB=[{string.Join(",", burstB)}], suelto={neutral}")
            : (false, $"burstA=[{string.Join(",", burstA)}] vs burstB=[{string.Join(",", burstB)}], suelto={neutral}");
    }

    private static (bool, string) TestWheelSwitchWeapon()
    {
        var settings = new AimSyncSettings();
        var cfg = settings.AntiRecoil;
        cfg.Enabled = true;
        cfg.UseArma2 = true; // arranca en arma 2
        cfg.Arma1Vertical = 0.3f;
        cfg.Arma2Vertical = 0.5f;
        cfg.WeaponDetectionEnabled = true;
        var (manager, _, mouse, spy) = NewPipeline(settings);

        mouse.SetButtonDown(MouseButton.Left, true);

        mouse.SetWheel(+120);
        manager.TryTick();
        short afterUp = spy.RightY; // rueda arriba -> arma 1 (0.3)
        bool upOk = afterUp == (short)MathF.Round(BoostForTest(0.3f) * MaxAxis) && manager.ActiveWeapon == 1;

        mouse.SetWheel(-120);
        manager.TryTick();
        short afterDown = spy.RightY; // rueda abajo -> arma 2 (0.5)
        bool downOk = afterDown == (short)MathF.Round(BoostForTest(0.5f) * MaxAxis) && manager.ActiveWeapon == 2;

        bool ok = upOk && downOk;
        return ok
            ? (true, $"rueda: up Y={afterUp} (arma1), down Y={afterDown} (arma2), ActiveWeapon={manager.ActiveWeapon}")
            : (false, $"up Y={afterUp} upOk={upOk}, down Y={afterDown} downOk={downOk}, ActiveWeapon={manager.ActiveWeapon}");
    }

    private static (bool, string) TestWheelIgnoredWhenDetectionOff()
    {
        var settings = new AimSyncSettings();
        var cfg = settings.AntiRecoil;
        cfg.Enabled = true;
        cfg.WeaponDetectionEnabled = false;
        cfg.Arma1Vertical = 0.3f;
        cfg.Arma2Vertical = 0.5f;
        var (manager, keyboard, mouse, spy) = NewPipeline(settings);

        mouse.SetButtonDown(MouseButton.Left, true);
        manager.TryTick(); // arma 1 -> 0.3

        mouse.SetWheel(+120);
        mouse.SetDelta(0, 0);
        manager.TryTick();

        bool ok = spy.RightY == (short)MathF.Round(BoostForTest(0.3f) * MaxAxis) && manager.ActiveWeapon == 1;
        return ok
            ? (true, $"rueda ignorada con deteccion OFF: Y={spy.RightY}, ActiveWeapon={manager.ActiveWeapon}")
            : (false, $"Y={spy.RightY}, ActiveWeapon={manager.ActiveWeapon}");
    }

    private static (bool, string) TestNegativeVerticalPush()
    {
        var settings = new AimSyncSettings();
        var cfg = settings.AntiRecoil;
        cfg.Enabled = true;
        cfg.Arma1Vertical = -0.4f;
        cfg.Arma1Horizontal = 0f;
        var (manager, keyboard, mouse, spy) = NewPipeline(settings);

        mouse.SetButtonDown(MouseButton.Left, true);
        manager.TryTick();

        short expected = (short)MathF.Round(BoostForTest(-0.4f) * MaxAxis);
        bool ok = spy.RightY == expected && spy.RightX == 0;
        return ok
            ? (true, $"empuje negativo: RightY={spy.RightY} (esperado {expected}), X=0 (baja la mira)")
            : (false, $"RightY={spy.RightY} (esperado {expected}), X={spy.RightX}");
    }

    private static (bool, string) TestDefaultsZeroAndFaster()
    {
        var settings = new AimSyncSettings();
        var cfg = settings.AntiRecoil;

        bool zero = cfg.Arma1Vertical == 0f && cfg.Arma1Horizontal == 0f
                 && cfg.Arma2Vertical == 0f && cfg.Arma2Horizontal == 0f;
        bool freq = cfg.OscillationFrequency == 8f;
        bool enabled = zero && freq;

        return enabled
            ? (true, $"empujes en 0 (V1={cfg.Arma1Vertical}, H1={cfg.Arma1Horizontal}, V2={cfg.Arma2Vertical}, H2={cfg.Arma2Horizontal}), osc a {cfg.OscillationFrequency} Hz (visible para el juego)")
            : (false, $"V1={cfg.Arma1Vertical} H1={cfg.Arma1Horizontal} V2={cfg.Arma2Vertical} H2={cfg.Arma2Horizontal}, freq={cfg.OscillationFrequency}");
    }

    private static (bool, string) TestPushForceCurve()
    {
        // Valor medio (36%) amplificado por la curva y superior al empuje lineal.
        var midSettings = new AimSyncSettings();
        var midCfg = midSettings.AntiRecoil;
        midCfg.Enabled = true;
        midCfg.Arma1Vertical = 0.36f;
        var (midManager, _, midMouse, midSpy) = NewPipeline(midSettings);
        midMouse.SetButtonDown(MouseButton.Left, true);
        midManager.TryTick();

        short boosted = midSpy.RightY;
        short linearShort = (short)MathF.Round(0.36f * MaxAxis);
        short expBoosted = (short)MathF.Round(BoostForTest(0.36f) * MaxAxis);
        bool midOk = boosted == expBoosted && boosted > linearShort;
        midManager.Deactivate();

        // 100% arriba = deflexion completa.
        bool fullOk;
        {
            var s = new AimSyncSettings();
            var c = s.AntiRecoil;
            c.Enabled = true;
            c.Arma1Vertical = 1f;
            var (m, _, mo, sp) = NewPipeline(s);
            mo.SetButtonDown(MouseButton.Left, true);
            m.TryTick();
            fullOk = sp.RightY == MaxAxis;
            m.Deactivate();
        }

        // -100% abajo = deflexion completa negativa.
        bool negOk;
        {
            var s = new AimSyncSettings();
            var c = s.AntiRecoil;
            c.Enabled = true;
            c.Arma1Vertical = -1f;
            var (m, _, mo, sp) = NewPipeline(s);
            mo.SetButtonDown(MouseButton.Left, true);
            m.TryTick();
            negOk = sp.RightY == -MaxAxis;
            m.Deactivate();
        }

        bool ok = midOk && fullOk && negOk;
        return ok
            ? (true, $"36% -> {boosted} (>lineal {linearShort}); 100% -> stick {MaxAxis}; -100% -> stick {-MaxAxis}")
            : (false, $"36% -> {boosted} (lineal {linearShort}, esperado {expBoosted}); full={fullOk} neg={negOk}");
    }

    private static (bool, string) TestHighFreqOscillation()
    {
        var settings = new AimSyncSettings();
        var cfg = settings.AntiRecoil;
        cfg.Enabled = true;
        cfg.Arma1Vertical = 0f;
        cfg.Arma1Horizontal = 0f;
        cfg.Oscillation = true;
        cfg.OscillationStrength = 0.3f;
        cfg.OscillationFrequency = 250f; // por encima del max util -> se limita internamente a 120
        var (manager, _, mouse, spy) = NewPipeline(settings);

        mouse.SetButtonDown(MouseButton.Left, true);

        short[] seq = new short[4];
        for (int i = 0; i < seq.Length; i++)
        {
            manager.TryTick();
            seq[i] = spy.LeftX;
        }

        // A 120 Hz la senoide alterna de signo cada tick (nunca congelada/constante).
        bool notFrozen = seq[0] != seq[1] && seq[1] != seq[2] && seq[2] != seq[3];
        bool alternates = seq[0] > 0 && seq[1] < 0 && seq[2] > 0 && seq[3] < 0;
        bool ok = notFrozen && alternates;
        return ok
            ? (true, $"120 Hz efectivos (pedido 250): secuencia=[{string.Join(",", seq)}] alterna {alternates} y no se congela")
            : (false, $"secuencia=[{string.Join(",", seq)}] notFrozen={notFrozen} alterna={alternates}");
    }

    private static (bool, string) TestPumpOverdrive()
    {
        short releaseShort = (short)MathF.Round(0.6f * MaxAxis);

        bool pumpOk;
        short[] seq;
        {
            var settings = new AimSyncSettings();
            var cfg = settings.AntiRecoil;
            cfg.Enabled = true;
            cfg.Arma1Vertical = -1f;
            cfg.Arma1Horizontal = 0f;
            cfg.PumpEnabled = true;
            var (manager, _, mouse, spy) = NewPipeline(settings);

            mouse.SetButtonDown(MouseButton.Left, true);
            seq = new short[24];
            for (int i = 0; i < seq.Length; i++)
            {
                manager.TryTick();
                seq[i] = spy.RightY;
            }

            bool firstFull = true;
            for (int i = 0; i < 9; i++)
                firstFull &= seq[i] == -MaxAxis;

            bool releasePhase = seq[9] == -releaseShort;
            int fullCount = seq.Count(v => v == -MaxAxis);
            int releaseCount = seq.Count(v => v == -releaseShort);
            bool noOvershoot = seq.All(v => v >= -MaxAxis && v <= MaxAxis);
            pumpOk = firstFull && releasePhase && fullCount == 18 && releaseCount == 6 && noOvershoot;
        }

        bool steadyOk;
        {
            var settings = new AimSyncSettings();
            var cfg = settings.AntiRecoil;
            cfg.Enabled = true;
            cfg.Arma1Vertical = -1f;
            cfg.PumpEnabled = false;
            var (manager, _, mouse, spy) = NewPipeline(settings);

            mouse.SetButtonDown(MouseButton.Left, true);
            bool constant = true;
            for (int i = 0; i < 12; i++)
            {
                manager.TryTick();
                constant &= spy.RightY == -MaxAxis;
            }
            steadyOk = constant;
        }

        bool ok = pumpOk && steadyOk;
        return ok
            ? (true, $"rafagas: 9 ticks a {MaxAxis} + 3 a {releaseShort} (ciclo 12), 18/6 en 24, sin overshoot; sin rafagas: constante {-MaxAxis}")
            : (false, $"secuencia=[{string.Join(",", seq)}] (full={pumpOk}, constante sin rafagas={steadyOk})");
    }

    private static (bool, string) TestTurboPulsePattern()
    {
        var settings = new AimSyncSettings();
        var cfg = settings.Turbo;
        cfg.Enabled = true;
        cfg.LeftEnabled = false;
        cfg.RightEnabled = true;
        cfg.TriggerFrequencyHz = 10f;
        cfg.TriggerDutyCycle = 0.5f;
        var (manager, _, mouse, spy) = NewPipeline(settings);

        mouse.SetButtonDown(MouseButton.Left, true); // gatillo derecho = full (255)

        byte[] seq = new byte[50];
        for (int i = 0; i < seq.Length; i++)
        {
            manager.TryTick();
            seq[i] = spy.RightTrigger;
        }

        int cycle = (int)Math.Round(1000.0 / settings.LoopInterval.TotalMilliseconds / cfg.TriggerFrequencyHz); // 25
        int hold = Math.Clamp((int)Math.Round(cycle * cfg.TriggerDutyCycle), 1, cycle); // 12
        int onPerCycle = hold; // el tick que vuelve a 0 del ciclo tambien es fase ON
        int expectedOn = onPerCycle * 2;
        int expectedOff = seq.Length - expectedOn;

        int on = seq.Count(v => v == byte.MaxValue);
        int off = seq.Count(v => v == 0);
        bool noOtherValues = on + off == seq.Length;
        // El muestreo arranca con el contador en 1 (no en 0), asi la primera
        // rafaga ON tiene hold-1 ticks; luego ON=hold y OFF=cycle-hold por ciclo.
        int firstOn = onPerCycle - 1;
        int offBlock = cycle - onPerCycle;
        bool blocks = seq.Take(firstOn).All(v => v == byte.MaxValue)          // 11 x ON
                   && seq.Skip(firstOn).Take(offBlock).All(v => v == 0)       // 13 x OFF
                   && seq.Skip(firstOn + offBlock).Take(onPerCycle).All(v => v == byte.MaxValue) // 12 x ON
                   && seq.Skip(firstOn + offBlock + onPerCycle).Take(offBlock).All(v => v == 0)  // 13 x OFF
                   && seq[^1] == byte.MaxValue;                                // arranque del 3er ciclo
        bool leftUntouched = spy.LeftTrigger == 0;

        bool ok = on == expectedOn && off == expectedOff && noOtherValues && blocks && leftUntouched;
        return ok
            ? (true, $"10 Hz/{cycle} ticks ciclo, fase ON={onPerCycle} (50%): 50 ticks -> ON={on} ({expectedOn}), OFF={off} ({expectedOff}), bloques [{firstOn}+{onPerCycle} ON / {offBlock} OFF], LT=0")
            : (false, $"ON={on} (esperado {expectedOn}), OFF={off} ({expectedOff}), bloques={blocks}, solo255/0={noOtherValues}, LT={spy.LeftTrigger}");
    }

    private static (bool, string) TestTurboDisabledStaysConstant()
    {
        bool disabledOk;
        {
            var settings = new AimSyncSettings();
            var cfg = settings.Turbo;
            cfg.Enabled = false;
            cfg.RightEnabled = true;
            var (manager, _, mouse, spy) = NewPipeline(settings);

            mouse.SetButtonDown(MouseButton.Left, true);
            manager.TryTick();
            bool first = spy.RightTrigger == byte.MaxValue;
            bool constant = true;
            for (int i = 0; i < 10; i++)
            {
                manager.TryTick();
                constant &= spy.RightTrigger == byte.MaxValue;
            }
            disabledOk = first && constant;
        }

        bool notFiringOk;
        {
            var settings = new AimSyncSettings();
            var cfg = settings.Turbo;
            cfg.Enabled = true;
            cfg.LeftEnabled = true;
            cfg.RightEnabled = false; // solo izquierdo activo
            var (manager, _, mouse, spy) = NewPipeline(settings);

            mouse.SetButtonDown(MouseButton.Left, true); // disparo = gatillo derecho, no pulsado por turbo
            manager.TryTick();
            bool first = spy.RightTrigger == byte.MaxValue;
            bool constant = true;
            for (int i = 0; i < 10; i++)
            {
                manager.TryTick();
                constant &= spy.RightTrigger == byte.MaxValue;
            }
            notFiringOk = first && constant;
        }

        bool ok = disabledOk && notFiringOk;
        return ok
            ? (true, $"turbo OFF / gatillo sin turbo: RT se mantiene en {byte.MaxValue} (sin rafaga)")
            : (false, $"desactivado={disabledOk}, solo-izquierdo={notFiringOk}");
    }

    private static (bool, string) TestMacroReproducesSteps()
    {
        var settings = new AimSyncSettings();
        var cfg = settings.Macros;
        cfg.Enabled = true;
        cfg.Macros.Add(new AimSync.Core.Engine.Macro
        {
            Name = "Combo 1",
            TriggerButton = ControllerButton.A, // Space -> A (mapa por defecto)
            Loop = false,
            Steps =
            [
                new AimSync.Core.Engine.MacroStep { Button = ControllerButton.X, DurationMs = 60 },
                new AimSync.Core.Engine.MacroStep { Button = ControllerButton.Y, DurationMs = 60 },
            ],
        });
        var (manager, keyboard, _, spy) = NewPipeline(settings);

        keyboard.Press(KeyCode.Space);

        bool xFirst = true;
        for (int i = 0; i < 15; i++)
        {
            manager.TryTick();
            xFirst &= spy.GetButton(ControllerButton.X) && !spy.GetButton(ControllerButton.Y);
        }

        bool ySecond = true;
        for (int i = 0; i < 15; i++)
        {
            manager.TryTick();
            ySecond &= spy.GetButton(ControllerButton.Y) && !spy.GetButton(ControllerButton.X);
        }

        bool endsAfterSequence = true;
        for (int i = 0; i < 12; i++)
        {
            manager.TryTick();
            endsAfterSequence &= !spy.GetButton(ControllerButton.X) && !spy.GetButton(ControllerButton.Y);
        }

        bool holdsTrigger = spy.GetButton(ControllerButton.A); // la entrada mapeada sigue

        bool ok = xFirst && ySecond && endsAfterSequence && holdsTrigger;
        return ok
            ? (true, "X 15 ticks -> Y 15 ticks -> nada tras completar (A sigue pulsado desde el mapa)")
            : (false, $"X-primero={xFirst}, Y-segundo={ySecond}, termina={endsAfterSequence}, A-map={holdsTrigger}");
    }

    private static (bool, string) TestMacroReinitOnRelease()
    {
        var settings = new AimSyncSettings();
        var cfg = settings.Macros;
        cfg.Enabled = true;
        cfg.Macros.Add(new AimSync.Core.Engine.Macro
        {
            Name = "Combo corto",
            TriggerButton = ControllerButton.A,
            Loop = false,
            Steps = [new AimSync.Core.Engine.MacroStep { Button = ControllerButton.X, DurationMs = 60 }],
        });
        var (manager, keyboard, _, spy) = NewPipeline(settings);

        keyboard.Press(KeyCode.Space);

        bool burstA = true;
        for (int i = 0; i < 8; i++)
        {
            manager.TryTick();
            burstA &= spy.GetButton(ControllerButton.X);
        }

        keyboard.Release(KeyCode.Space);
        bool releasedNeutral = true;
        for (int i = 0; i < 4; i++)
        {
            manager.TryTick();
            releasedNeutral &= !spy.GetButton(ControllerButton.X);
        }

        keyboard.Press(KeyCode.Space);
        bool burstB = true;
        for (int i = 0; i < 8; i++)
        {
            manager.TryTick();
            burstB &= spy.GetButton(ControllerButton.X);
        }

        bool ok = burstA && releasedNeutral && burstB;
        return ok
            ? (true, "mantener -> X on; soltar -> neutro; mantener de nuevo -> X on otra vez")
            : (false, $"burstA={burstA}, neutro al soltar={releasedNeutral}, burstB={burstB}");
    }

    private static (bool, string) TestMacroLoopRepeats()
    {
        var settings = new AimSyncSettings();
        var cfg = settings.Macros;
        cfg.Enabled = true;
        cfg.Macros.Add(new AimSync.Core.Engine.Macro
        {
            Name = "Loop",
            TriggerButton = ControllerButton.A,
            Loop = true,
            Steps =
            [
                new AimSync.Core.Engine.MacroStep { Button = ControllerButton.X, DurationMs = 60 },
                new AimSync.Core.Engine.MacroStep { Button = ControllerButton.Y, DurationMs = 60 },
            ],
        });
        var (manager, keyboard, _, spy) = NewPipeline(settings);

        keyboard.Press(KeyCode.Space);

        byte[] pattern = new byte[60];
        for (int i = 0; i < pattern.Length; i++)
        {
            manager.TryTick();
            bool x = spy.GetButton(ControllerButton.X);
            bool y = spy.GetButton(ControllerButton.Y);
            pattern[i] = (byte)((x ? 1 : 0) | (y ? 2 : 0));
        }

        // ciclo de 30 ticks: 15 x X (valor 1), 15 x Y (valor 2), se repite dos veces
        bool cycleA = pattern.Take(15).All(v => v == 1) && pattern.Skip(15).Take(15).All(v => v == 2);
        bool cycleB = pattern.Skip(30).Take(15).All(v => v == 1) && pattern.Skip(45).Take(15).All(v => v == 2);
        bool xCount = pattern.Count(v => v == 1) == 30;
        bool yCount = pattern.Count(v => v == 2) == 30;

        bool ok = cycleA && cycleB && xCount && yCount;
        return ok
            ? (true, $"patron=[15x X, 15x Y] x2: X={xCount} ticks, Y={yCount} ticks")
            : (false, $"cicloA={cycleA} cicloB={cycleB} X={xCount} Y={yCount}");
    }

    private static (bool, string) TestSettingsStoreRoundTrip()
    {
        string path = Path.Combine(Path.GetTempPath(), $"aimsync_p2_{Guid.NewGuid():N}.json");
        try
        {
            var source = new AimSyncSettings();
            var turbo = source.Turbo;
            turbo.Enabled = true;
            turbo.LeftEnabled = true;
            turbo.RightEnabled = false;
            turbo.FrequencyHz = 17.5f;
            turbo.DutyCycle = 0.35f;
            turbo.TriggerFrequencyHz = 6.5f;
            turbo.TriggerDutyCycle = 0.8f;

            var macros = source.Macros;
            macros.Enabled = true;
            macros.Macros.Add(new AimSync.Core.Engine.Macro
            {
                Name = "Guardada",
                Enabled = false,
                TriggerButton = ControllerButton.LeftShoulder,
                Loop = true,
                Steps =
                [
                    new AimSync.Core.Engine.MacroStep { Button = ControllerButton.B, DurationMs = 40 },
                    new AimSync.Core.Engine.MacroStep { Button = ControllerButton.Start, DurationMs = 120 },
                ],
            });

            var store = new SettingsStore(path);
            store.Save(source);

            var loaded = new AimSyncSettings();
            bool found = store.Load(loaded);

            bool turboOk = loaded.Turbo.Enabled && loaded.Turbo.LeftEnabled && !loaded.Turbo.RightEnabled
                        && Math.Abs(loaded.Turbo.FrequencyHz - 17.5f) < 0.001f
                        && Math.Abs(loaded.Turbo.DutyCycle - 0.35f) < 0.001f
                        && Math.Abs(loaded.Turbo.TriggerFrequencyHz - 6.5f) < 0.001f
                        && Math.Abs(loaded.Turbo.TriggerDutyCycle - 0.8f) < 0.001f;
            bool macroOk = loaded.Macros.Enabled && loaded.Macros.Macros.Count == 1;
            var macro = loaded.Macros.Macros[0];
            bool fieldsOk = macro.Name == "Guardada" && !macro.Enabled && macro.TriggerButton == ControllerButton.LeftShoulder && macro.Loop;
            bool stepsOk = macro.Steps.Count == 2
                        && macro.Steps[0].Button == ControllerButton.B && macro.Steps[0].DurationMs == 40
                        && macro.Steps[1].Button == ControllerButton.Start && macro.Steps[1].DurationMs == 120;

            bool missing = !new SettingsStore(path + ".missing").Load(new AimSyncSettings());

            bool ok = found && turboOk && macroOk && fieldsOk && stepsOk && missing;
            return ok
                ? (true, $"JSON redondeado: turbo(botones freq={loaded.Turbo.FrequencyHz}, duty={loaded.Turbo.DutyCycle}; gatillos freq={loaded.Turbo.TriggerFrequencyHz}, duty={loaded.Turbo.TriggerDutyCycle}), macro '{macro.Name}' con {macro.Steps.Count} pasos; archivo inexistente ignorado")
                : (false, $"found={found} turbo={turboOk} macroCount={macroOk} fields={fieldsOk} steps={stepsOk} missing={missing}");
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static (bool, string) TestPhysicalPassthroughButton()
    {
        var settings = new AimSyncSettings();
        var physical = new FakePhysicalSource();
        var (manager, _, _, spy) = NewPipeline(settings, physical);

        manager.TryTick(); // mando desconectado
        bool nothingWhenDisconnected = !spy.GetButton(ControllerButton.Y);

        physical.Press(PhysicalButtons.Y);
        bool held = true;
        for (int i = 0; i < 5; i++)
        {
            manager.TryTick();
            held &= spy.GetButton(ControllerButton.Y);
        }

        physical.Release(PhysicalButtons.Y);
        bool released = true;
        for (int i = 0; i < 3; i++)
        {
            manager.TryTick();
            released &= !spy.GetButton(ControllerButton.Y);
        }

        bool ok = nothingWhenDisconnected && held && released;
        return ok
            ? (true, $"desconectado: Y off; Y fisico: {5} ticks on; soltar: {3} ticks off")
            : (false, $"desconectado={nothingWhenDisconnected} mantenido={held} soltado={released}");
    }

    private static (bool, string) TestPhysicalTriggersMacro()
    {
        var settings = new AimSyncSettings();
        var cfg = settings.Macros;
        cfg.Enabled = true;
        cfg.Macros.Add(new AimSync.Core.Engine.Macro
        {
            Name = "Por mando",
            TriggerButton = ControllerButton.Y,
            Loop = false,
            Steps = [new AimSync.Core.Engine.MacroStep { Button = ControllerButton.X, DurationMs = 60 }],
        });
        var physical = new FakePhysicalSource();
        var (manager, _, _, spy) = NewPipeline(settings, physical);

        physical.Press(PhysicalButtons.Y);

        bool playsX = true;
        for (int i = 0; i < 15; i++)
        {
            manager.TryTick();
            playsX &= spy.GetButton(ControllerButton.X);
        }

        physical.Release(PhysicalButtons.Y);
        bool yReleased = true;
        for (int i = 0; i < 5; i++)
        {
            manager.TryTick();
            yReleased &= !spy.GetButton(ControllerButton.Y) && !spy.GetButton(ControllerButton.X);
        }

        bool ok = playsX && yReleased;
        return ok
            ? (true, "Y fisico -> macro reproduce X 15 ticks; al soltar, Y y X en neutro")
            : (false, $"X reproduciendose={playsX} neutro al soltar={yReleased}");
    }

    private static (bool, string) TestRapidButtonPulsePattern()
    {
        var settings = new AimSyncSettings();
        var turbo = settings.Turbo;
        turbo.Enabled = true;
        turbo.LeftEnabled = false;
        turbo.RightEnabled = false;
        turbo.FrequencyHz = 40f; // ciclo de 6 ticks (25 ms)
        turbo.DutyCycle = 0.5f;  // fase ON = 3 ticks (12 ms)
        turbo.RapidButtons = [ControllerButton.Y];
        var physical = new FakePhysicalSource();
        var (manager, _, _, spy) = NewPipeline(settings, physical);

        physical.Press(PhysicalButtons.Y);

        bool[] seq = new bool[8];
        for (int i = 0; i < seq.Length; i++)
        {
            manager.TryTick();
            seq[i] = spy.GetButton(ControllerButton.Y);
        }

        // Mismo ciclo que el turbo de gatillos: primera fase ON de 2 ticks, luego
        // 3 ON / 3 OFF por ciclo. Pulsos de 12 ms (no 4 ms) para que el juego los registre.
        bool[] expected = [true, true, false, false, false, true, true, true];
        bool ok = seq.SequenceEqual(expected);

        return ok
            ? (true, $"rafaga Y={string.Join("", seq.Select(v => v ? "1" : "0"))} (ciclo 25 ms, ON 12 ms -> ~40 clics/s)")
            : (false, $"secuencia esperada [1,1,0,0,0,1,1,1], recibida {string.Join("", seq.Select(v => v ? "1" : "0"))}");
    }

    private static (bool, string) TestRapidButtonNeedsMaster()
    {
        bool masterOffOk;
        {
            var settings = new AimSyncSettings();
            var turbo = settings.Turbo;
            turbo.Enabled = false; // modo turbo apagado
            turbo.RapidButtons = [ControllerButton.Y];
            var physical = new FakePhysicalSource();
            var (manager, _, _, spy) = NewPipeline(settings, physical);

            physical.Press(PhysicalButtons.Y);
            bool constant = true;
            for (int i = 0; i < 8; i++)
            {
                manager.TryTick();
                constant &= spy.GetButton(ControllerButton.Y);
            }
            masterOffOk = constant;
        }

        bool notListedOk;
        {
            var settings = new AimSyncSettings();
            var turbo = settings.Turbo;
            turbo.Enabled = true;
            turbo.LeftEnabled = false;
            turbo.RightEnabled = false;
            turbo.RapidButtons = [ControllerButton.X]; // Y no esta en la lista
            var physical = new FakePhysicalSource();
            var (manager, _, _, spy) = NewPipeline(settings, physical);

            physical.Press(PhysicalButtons.Y);
            bool constant = true;
            for (int i = 0; i < 8; i++)
            {
                manager.TryTick();
                constant &= spy.GetButton(ControllerButton.Y);
            }
            notListedOk = constant;
        }

        bool ok = masterOffOk && notListedOk;
        return ok
            ? (true, "master OFF o boton no listado: Y se mantiene constante (passthrough, sin rafaga)")
            : (false, $"masterOff={masterOffOk} noListado={notListedOk}");
    }

    private static (bool, string) TestPhysicalTriggersAndSticks()
    {
        var settings = new AimSyncSettings();
        var physical = new FakePhysicalSource();
        var (manager, _, mouse, spy) = NewPipeline(settings, physical);

        // Sin disparo por mouse: el gatillo fisico aparece en el virtual.
        physical.SetTriggers(right: 200);
        physical.Press(PhysicalButtons.Y);
        manager.TryTick();
        bool rawTrigger = spy.RightTrigger == 200;

        // Combinado: mouse izquierdo ya da RT=255 -> el Max gana.
        mouse.SetButtonDown(MouseButton.Left, true);
        manager.TryTick();
        bool maxWins = spy.RightTrigger == byte.MaxValue;

        // Con el mando fisico conectado, los sticks fisicos controlan la salida virtual.
        physical.SetSticks(leftX: -32767, leftY: 0, rightX: 0, rightY: 32767);
        manager.TryTick();
        bool sticksForwarded = spy.LeftX == -32767 && spy.LeftY == 0 && spy.RightX == 0 && spy.RightY == 32767;

        bool ok = rawTrigger && maxWins && sticksForwarded;
        return ok
            ? (true, $"RT fisico 200 -> {spy.RightTrigger}; con mouse+RT -> {spy.RightTrigger}; sticks fisicos -> L({spy.LeftX},{spy.LeftY}) R({spy.RightX},{spy.RightY})")
            : (false, $"RT crudo={rawTrigger} Max={maxWins} sticks={sticksForwarded}");
    }

    private static (bool, string) TestRapidButtonsRoundTrip()
    {
        string path = Path.Combine(Path.GetTempPath(), $"aimsync_p3_{Guid.NewGuid():N}.json");
        try
        {
            var source = new AimSyncSettings();
            var turbo = source.Turbo;
            turbo.Enabled = true;
            turbo.RapidButtons = [ControllerButton.Y, ControllerButton.RightShoulder, ControllerButton.DPadDown];

            var store = new SettingsStore(path);
            store.Save(source);

            var loaded = new AimSyncSettings();
            bool found = store.Load(loaded);

            bool rapidOk = loaded.Turbo.RapidButtons.Count == 3
                        && loaded.Turbo.RapidButtons.Contains(ControllerButton.Y)
                        && loaded.Turbo.RapidButtons.Contains(ControllerButton.RightShoulder)
                        && loaded.Turbo.RapidButtons.Contains(ControllerButton.DPadDown);
            bool masterOk = loaded.Turbo.Enabled;

            bool ok = found && rapidOk && masterOk;
            return ok
                ? (true, $"JSON con rafaga: lista=[{string.Join(", ", loaded.Turbo.RapidButtons)}], master={loaded.Turbo.Enabled}")
                : (false, $"found={found} rapid={rapidOk} master={masterOk}");
        }
        finally
        {
            File.Delete(path);
        }
    }
}