using AimSync.Core;
using AimSync.Core.Devices;
using AimSync.Core.Engine;
using AimSync.Core.Input;
using AimSync.Core.Mapping;
using AimSync.Core.Storage;

namespace AimSync.Test;

/// <summary>
/// Pruebas centradas en el procesamiento del gatillo RT en modo normal: paso analogico 0..255,
/// sensibilidad/umbral, retorno a cero, activacion/desactivacion (F8) sin estados atascados,
/// cambio de perfil y separacion respecto al modo de pulsacion repetida (turbo) y a las macros.
/// </summary>
internal static class TriggerTests
{
    public static int RunAll()
    {
        int failures = 0;

        Run("T-1 RT analogico pasa intacto 0 -> 100 -> 200 -> 255 -> 0", TestAnalogPassthrough, ref failures);
        Run("T-2 RT pulsacion parcial conserva su valor (137)", TestPartialPressPreserved, ref failures);
        Run("T-3 Umbral de activacion: por debajo = 0, por encima = valor real", TestActivationThreshold, ref failures);
        Run("T-4 Sensibilidad: amplifica y satura en 255", TestSensitivity, ref failures);
        Run("T-5 Limites de sensibilidad/umbral se validan (clamp)", TestConfigBounds, ref failures);
        Run("T-6 F8 on/off no deja RT atascado y se recupera al reactivar", TestToggleNoStuckTrigger, ref failures);
        Run("T-7 Cambio de perfil de arma no bloquea RT", TestProfileChangeKeepsTrigger, ref failures);
        Run("T-8 Turbo OFF: RT analogico constante (sin pulsos artificiales)", TestTurboOffPassthrough, ref failures);
        Run("T-9 Turbo ON sin gatillo en rafaga: RT pasa intacto", TestTurboOnWithoutTriggerRapid, ref failures);
        Run("T-10 Macro activa no reescribe RT", TestMacroDoesNotTouchTrigger, ref failures);
        Run("T-11 Persistencia: sensibilidad/umbral de RT se guardan y recargan", TestTriggerPersistence, ref failures);
        Run("T-12 Velocidad de botones y de gatillos independientes", TestButtonAndTriggerFrequenciesIndependent, ref failures);

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

    private static (InputManager, FakeKeyboardCapture, FakeMouseCapture, FakePhysicalSource, SpyVirtualController) NewPipeline(AimSyncSettings settings)
    {
        var keyboard = new FakeKeyboardCapture();
        var mouse = new FakeMouseCapture();
        var physical = new FakePhysicalSource();
        var spy = new SpyVirtualController();
        var manager = new InputManager(settings, () => spy, keyboard, mouse, physical);
        manager.Activate();
        return (manager, keyboard, mouse, physical, spy);
    }

    private static (bool, string) TestAnalogPassthrough()
    {
        var settings = new AimSyncSettings();
        var (manager, _, _, physical, spy) = NewPipeline(settings);

        var expected = new byte[] { 0, 100, 200, 255, 0 };
        var actual = new byte[expected.Length];
        for (int i = 0; i < expected.Length; i++)
        {
            physical.SetTriggers(right: expected[i]);
            manager.TryTick();
            actual[i] = spy.RightTrigger;
        }

        bool ok = actual.SequenceEqual(expected);
        return ok
            ? (true, $"RT=[{string.Join(",", actual)}] (esperado [{string.Join(",", expected)}]), sin alteraciones")
            : (false, $"RT=[{string.Join(",", actual)}], esperado [{string.Join(",", expected)}]");
    }

    private static (bool, string) TestPartialPressPreserved()
    {
        var settings = new AimSyncSettings();
        var (manager, _, _, physical, spy) = NewPipeline(settings);

        physical.SetTriggers(right: 137);
        manager.TryTick();
        bool first = spy.RightTrigger == 137;

        byte constant = 137;
        bool held = true;
        for (int i = 0; i < 20; i++)
        {
            manager.TryTick();
            held &= spy.RightTrigger == constant;
        }

        bool ok = first && held;
        return ok
            ? (true, $"RT parcial 137 se mantiene intacto ({spy.RightTrigger}) durante 20 ticks")
            : (false, $"primero={first}, constante={held}, ultimo={spy.RightTrigger}");
    }

    private static (bool, string) TestActivationThreshold()
    {
        var settings = new AimSyncSettings();
        settings.RightTrigger.ActivationThreshold = 0.5f; // 50%
        var (manager, _, _, physical, spy) = NewPipeline(settings);

        physical.SetTriggers(right: 100); // 0.392 -> por debajo
        manager.TryTick();
        byte below = spy.RightTrigger;

        physical.SetTriggers(right: 200); // 0.784 -> por encima
        manager.TryTick();
        byte above = spy.RightTrigger;

        bool ok = below == 0 && above == 200;
        return ok
            ? (true, $"umbral 50%: 100 -> {below} (0), 200 -> {above} (200, valor real sin reescalar)")
            : (false, $"umbral 50%: esperado 100->0 y 200->200, obtenido 100->{below}, 200->{above}");
    }

    private static (bool, string) TestSensitivity()
    {
        var settings = new AimSyncSettings();
        settings.RightTrigger.Sensitivity = 2f;
        var (manager, _, _, physical, spy) = NewPipeline(settings);

        physical.SetTriggers(right: 100); // 0.392 * 2 = 0.784 -> 200
        manager.TryTick();
        byte doubled = spy.RightTrigger;

        physical.SetTriggers(right: 200); // 0.784 * 2 = 1.568 -> satura 255
        manager.TryTick();
        byte saturated = spy.RightTrigger;

        bool ok = doubled == 200 && saturated == 255;
        return ok
            ? (true, $"sens 2x: 100 -> {doubled} (200), 200 -> {saturated} (255 saturado)")
            : (false, $"sens 2x: esperado 100->200 y 200->255, obtenido {doubled} / {saturated}");
    }

    private static (bool, string) TestConfigBounds()
    {
        var config = new TriggerConfig { Sensitivity = 9f, ActivationThreshold = 5f };
        bool highClamped = config.Sensitivity == TriggerConfig.SensitivityMax
                        && config.ActivationThreshold == TriggerConfig.ThresholdMax;

        config.Sensitivity = 0.001f;
        config.ActivationThreshold = -1f;
        bool lowClamped = config.Sensitivity == TriggerConfig.SensitivityMin
                       && config.ActivationThreshold == TriggerConfig.ThresholdMin;

        bool zeroInZeroOut = config.Process(0) == 0;

        bool ok = highClamped && lowClamped && zeroInZeroOut;
        return ok
            ? (true, $"clamp alto ({TriggerConfig.SensitivityMax}/{TriggerConfig.ThresholdMax}), bajo ({TriggerConfig.SensitivityMin}/{TriggerConfig.ThresholdMin}), Process(0)=0")
            : (false, $"alto={highClamped}, bajo={lowClamped}, zero={zeroInZeroOut}");
    }

    private static (bool, string) TestToggleNoStuckTrigger()
    {
        var settings = new AimSyncSettings();
        var (manager, _, _, physical, spy) = NewPipeline(settings);

        physical.SetTriggers(right: 200);
        manager.TryTick();
        bool active = spy.RightTrigger == 200;

        manager.Deactivate();
        bool releasedOnDeactivate = spy.RightTrigger == 0;

        // Desactivado: aunque el gatillo fisico siga pulsado, no debe escribirse nada.
        manager.TryTick();
        bool staysZeroWhileDisabled = spy.RightTrigger == 0;

        manager.Activate();
        manager.TryTick();
        bool resumes = spy.RightTrigger == 200;

        bool ok = active && releasedOnDeactivate && staysZeroWhileDisabled && resumes;
        return ok
            ? (true, $"activo={active}, al desactivar={releasedOnDeactivate}, desactivado={staysZeroWhileDisabled}, al reactivar={resumes}")
            : (false, $"activo={active}, released={releasedOnDeactivate}, disabled={staysZeroWhileDisabled}, resume={resumes}");
    }

    private static (bool, string) TestProfileChangeKeepsTrigger()
    {
        var settings = new AimSyncSettings();
        settings.AntiRecoil.Enabled = true;
        settings.AntiRecoil.WeaponDetectionEnabled = true;
        settings.AntiRecoil.Arma1Vertical = 0.3f;
        settings.AntiRecoil.Arma2Vertical = 0.5f;
        var (manager, keyboard, _, physical, spy) = NewPipeline(settings);

        physical.SetTriggers(right: 150);
        manager.TryTick();
        bool before = spy.RightTrigger == 150;

        // Tecla 2: cambia a Arma 2 (deteccion de arma) sin tocar el gatillo.
        keyboard.Press(KeyCode.Digit2);
        manager.TryTick();
        byte afterKey = spy.RightTrigger;
        int weapon = manager.ActiveWeapon;

        keyboard.Release(KeyCode.Digit2);
        manager.SelectWeapon(1);
        manager.TryTick();
        byte afterSelect = spy.RightTrigger;

        bool ok = before && afterKey == 150 && afterSelect == 150 && weapon == 2;
        return ok
            ? (true, $"RT se mantiene 150 tras cambiar de arma (deteccion={afterKey}, manual={afterSelect}, arma={weapon})")
            : (false, $"before={before}, tras tecla={afterKey}, tras SelectWeapon={afterSelect}, arma={weapon}");
    }

    private static (bool, string) TestTurboOffPassthrough()
    {
        var settings = new AimSyncSettings();
        settings.Turbo.Enabled = false;
        settings.Turbo.RightEnabled = true; // aunque este marcado, turbo esta OFF
        var (manager, _, _, physical, spy) = NewPipeline(settings);

        physical.SetTriggers(right: 180);
        bool constant = true;
        for (int i = 0; i < 20; i++)
        {
            manager.TryTick();
            constant &= spy.RightTrigger == 180;
        }

        bool ok = constant;
        return ok
            ? (true, "turbo OFF: RT=180 constante en 20 ticks (sin pulsos)")
            : (false, $"RT dejo de ser 180 (ultimo {spy.RightTrigger})");
    }

    private static (bool, string) TestTurboOnWithoutTriggerRapid()
    {
        var settings = new AimSyncSettings();
        settings.Turbo.Enabled = true;
        settings.Turbo.RightEnabled = false; // gatillo NO en rafaga
        settings.Turbo.LeftEnabled = false;
        settings.Turbo.RapidButtons.Add(ControllerButton.Y);
        settings.Turbo.FrequencyHz = 10f;
        settings.Turbo.DutyCycle = 0.5f;
        var (manager, _, _, physical, spy) = NewPipeline(settings);

        physical.SetTriggers(right: 200);
        physical.Press(PhysicalButtons.Y);

        bool triggerConstant = true;
        bool yPulsedOn = false;
        bool yPulsedOff = false;
        for (int i = 0; i < 40; i++)
        {
            manager.TryTick();
            triggerConstant &= spy.RightTrigger == 200;
            if (spy.GetButton(ControllerButton.Y)) yPulsedOn = true;
            else yPulsedOff = true;
        }

        bool ok = triggerConstant && yPulsedOn && yPulsedOff;
        return ok
            ? (true, "RT=200 constante mientras Y alterna (turbo de boton separado del gatillo)")
            : (false, $"RT constante={triggerConstant}, Y on={yPulsedOn} off={yPulsedOff}");
    }

    private static (bool, string) TestMacroDoesNotTouchTrigger()
    {
        var settings = new AimSyncSettings();
        settings.Macros.Enabled = true;
        settings.Macros.Macros.Add(new Macro
        {
            Name = "Combo RT",
            TriggerButton = ControllerButton.A, // Space -> A
            Loop = true,
            Steps = [new MacroStep { Button = ControllerButton.X, DurationMs = 40 }],
        });
        var (manager, keyboard, _, physical, spy) = NewPipeline(settings);

        keyboard.Press(KeyCode.Space);
        physical.SetTriggers(right: 123);

        bool rtConstant = true;
        for (int i = 0; i < 30; i++)
        {
            manager.TryTick();
            rtConstant &= spy.RightTrigger == 123;
        }
        bool xPressed = spy.GetButton(ControllerButton.X);

        bool ok = rtConstant && xPressed;
        return ok
            ? (true, $"macro activa (X pulsado={xPressed}) y RT intacto en 123")
            : (false, $"RT constante={rtConstant}, X pulsado={xPressed}, ultimo RT={spy.RightTrigger}");
    }

    private static (bool, string) TestTriggerPersistence()
    {
        string path = Path.Combine(Path.GetTempPath(), $"aimsync_trig_{Guid.NewGuid():N}.json");
        try
        {
            var source = new AimSyncSettings();
            source.RightTrigger.Sensitivity = 1.5f;
            source.RightTrigger.ActivationThreshold = 0.2f;
            source.LeftTrigger.Sensitivity = 0.7f;
            source.LeftTrigger.ActivationThreshold = 0.1f;

            var store = new SettingsStore(path);
            store.Save(source);

            var loaded = new AimSyncSettings();
            bool found = store.Load(loaded);

            bool right = Math.Abs(loaded.RightTrigger.Sensitivity - 1.5f) < 0.001f
                      && Math.Abs(loaded.RightTrigger.ActivationThreshold - 0.2f) < 0.001f;
            bool left = Math.Abs(loaded.LeftTrigger.Sensitivity - 0.7f) < 0.001f
                     && Math.Abs(loaded.LeftTrigger.ActivationThreshold - 0.1f) < 0.001f;

            bool ok = found && right && left;
            return ok
                ? (true, $"RT(sens={loaded.RightTrigger.Sensitivity}, umbral={loaded.RightTrigger.ActivationThreshold}) y LT(sens={loaded.LeftTrigger.Sensitivity}, umbral={loaded.LeftTrigger.ActivationThreshold}) persistidos")
                : (false, $"found={found}, right={right}, left={left}");
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static (bool, string) TestButtonAndTriggerFrequenciesIndependent()
    {
        var settings = new AimSyncSettings();
        settings.Turbo.Enabled = true;
        settings.Turbo.LeftEnabled = false;
        settings.Turbo.RightEnabled = true;
        settings.Turbo.FrequencyHz = 50f;          // botones rapidos
        settings.Turbo.DutyCycle = 0.5f;
        settings.Turbo.TriggerFrequencyHz = 5f;    // gatillos lentos
        settings.Turbo.TriggerDutyCycle = 0.5f;
        settings.Turbo.RapidButtons.Add(ControllerButton.Y);
        var (manager, _, _, physical, spy) = NewPipeline(settings);

        physical.Press(PhysicalButtons.Y);
        physical.SetTriggers(right: 255);

        int yChanges = 0;
        int rtChanges = 0;
        bool prevY = spy.GetButton(ControllerButton.Y);
        int prevRt = spy.RightTrigger;
        for (int i = 0; i < 400; i++)
        {
            manager.TryTick();
            bool y = spy.GetButton(ControllerButton.Y);
            int rt = spy.RightTrigger;
            if (y != prevY) yChanges++;
            if (rt != prevRt) rtChanges++;
            prevY = y;
            prevRt = rt;
        }

        // A 50 Hz (botones) el ciclo es ~5 ticks y a 5 Hz (gatillos) ~50 ticks, asi que
        // los botones deben alternar (mucho) mas veces que los gatillos en la misma ventana.
        bool ok = yChanges > rtChanges * 2 && rtChanges > 0 && yChanges > 0;
        return ok
            ? (true, $"independientes: botones Y={yChanges} cambios a 50 Hz vs gatillos RT={rtChanges} cambios a 5 Hz")
            : (false, $"Y cambios={yChanges}, RT cambios={rtChanges} (se esperaba Y >> RT)");
    }
}
