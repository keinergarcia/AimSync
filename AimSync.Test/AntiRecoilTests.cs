using AimSync.Core;
using AimSync.Core.Devices;
using AimSync.Core.Input;
using AimSync.Core.Storage;

namespace AimSync.Test;

/// <summary>
/// Pruebas del sistema anti-recoil: empuje al stick derecho mientras se dispara, perfiles
/// de Arma 1/Arma 2, oscilacion del stick izquierdo y persistencia de la configuracion.
/// </summary>
internal static class AntiRecoilTests
{
    public static int RunAll()
    {
        int failures = 0;

        Run("AR-1 Disparando + Arma 1 vertical: empuja el stick derecho hacia arriba", TestVerticalPush, ref failures);
        Run("AR-2 Sin disparar: no toca el stick derecho", TestNoFiringNoPush, ref failures);
        Run("AR-3 Desactivado: no empuja aunque se dispare", TestDisabledNoPush, ref failures);
        Run("AR-4 Perfil Arma 2 usa sus propias fuerzas", TestArma2Profile, ref failures);
        Run("AR-5 Empuje horizontal aplicado al stick derecho", TestHorizontalPush, ref failures);
        Run("AR-6 Vertical negativo empuja hacia abajo", TestVerticalNegative, ref failures);
        Run("AR-7 Oscilacion mueve el stick izquierdo mientras se dispara", TestOscillation, ref failures);
        Run("AR-8 Persistencia: la configuracion de anti-recoil se guarda y recarga", TestPersistence, ref failures);

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

    private static (InputManager, FakePhysicalSource, SpyVirtualController) NewPipeline(AimSyncSettings settings)
    {
        var keyboard = new FakeKeyboardCapture();
        var mouse = new FakeMouseCapture();
        var physical = new FakePhysicalSource();
        var spy = new SpyVirtualController();
        var manager = new InputManager(settings, () => spy, keyboard, mouse, physical);
        manager.Activate();
        return (manager, physical, spy);
    }

    private static (bool, string) TestVerticalPush()
    {
        var settings = new AimSyncSettings();
        settings.AntiRecoil.Enabled = true;
        settings.AntiRecoil.Arma1Vertical = 0.5f;
        var (manager, physical, spy) = NewPipeline(settings);

        physical.SetTriggers(right: 200);
        manager.TryTick();

        short expected = (short)MathF.Round(MathF.Sqrt(0.5f) * short.MaxValue);
        bool ok = spy.RightTrigger == 200 && spy.RightY >= expected - 2000 && spy.RightY <= expected + 2000;
        return ok
            ? (true, $"RT=200 (disparando) -> RightY={spy.RightY} (esperado ~{expected} por arma 1 vertical 50%)")
            : (false, $"RT={spy.RightTrigger}, RightY={spy.RightY} (esperado ~{expected})");
    }

    private static (bool, string) TestNoFiringNoPush()
    {
        var settings = new AimSyncSettings();
        settings.AntiRecoil.Enabled = true;
        settings.AntiRecoil.Arma1Vertical = 0.5f;
        settings.AntiRecoil.Oscillation = true;
        settings.AntiRecoil.OscillationStrength = 0.6f;
        var (manager, physical, spy) = NewPipeline(settings);

        physical.SetTriggers(right: 0);
        manager.TryTick();

        bool ok = spy.RightY == 0 && spy.RightX == 0 && spy.LeftX == 0;
        return ok
            ? (true, $"sin disparar (RT=0): RightY={spy.RightY}, RightX={spy.RightX}, LeftX={spy.LeftX} (intactos)")
            : (false, $"RightY={spy.RightY}, RightX={spy.RightX}, LeftX={spy.LeftX} (deberia ser 0)");
    }

    private static (bool, string) TestDisabledNoPush()
    {
        var settings = new AimSyncSettings();
        settings.AntiRecoil.Enabled = false;
        settings.AntiRecoil.Arma1Vertical = 0.5f;
        settings.AntiRecoil.Oscillation = true;
        settings.AntiRecoil.OscillationStrength = 0.6f;
        var (manager, physical, spy) = NewPipeline(settings);

        physical.SetTriggers(right: 200);
        manager.TryTick();

        bool ok = spy.RightY == 0 && spy.RightX == 0 && spy.LeftX == 0;
        return ok
            ? (true, $"desactivado + disparando: RightY={spy.RightY}, RightX={spy.RightX}, LeftX={spy.LeftX} (intactos)")
            : (false, $"RightY={spy.RightY}, RightX={spy.RightX}, LeftX={spy.LeftX} (deberia ser 0)");
    }

    private static (bool, string) TestArma2Profile()
    {
        var settings = new AimSyncSettings();
        settings.AntiRecoil.Enabled = true;
        settings.AntiRecoil.UseArma2 = true;       // arranca en Arma 2
        settings.AntiRecoil.Arma1Vertical = 0f;    // Arma 1 sin fuerza
        settings.AntiRecoil.Arma2Vertical = 0.9f;  // Arma 2 con fuerza
        var (manager, physical, spy) = NewPipeline(settings);

        bool activeIsArma2 = manager.ActiveWeapon == 2;

        physical.SetTriggers(right: 255);
        manager.TryTick();

        short expected = (short)MathF.Round(MathF.Sqrt(0.9f) * short.MaxValue);
        bool ok = activeIsArma2 && spy.RightY >= expected - 2000;
        return ok
            ? (true, $"arma activa={manager.ActiveWeapon}, RightY={spy.RightY} (esperado ~{expected})")
            : (false, $"arma activa={manager.ActiveWeapon}, RightY={spy.RightY} (esperado ~{expected})");
    }

    private static (bool, string) TestHorizontalPush()
    {
        var settings = new AimSyncSettings();
        settings.AntiRecoil.Enabled = true;
        settings.AntiRecoil.Arma1Horizontal = -0.5f;
        var (manager, physical, spy) = NewPipeline(settings);

        physical.SetTriggers(right: 200);
        manager.TryTick();

        bool ok = spy.RightX < -10000;
        return ok
            ? (true, $"horizontal -50%: RightX={spy.RightX} (hacia la izquierda)")
            : (false, $"RightX={spy.RightX} (esperado negativo)");
    }

    private static (bool, string) TestVerticalNegative()
    {
        var settings = new AimSyncSettings();
        settings.AntiRecoil.Enabled = true;
        settings.AntiRecoil.Arma1Vertical = -0.5f;
        var (manager, physical, spy) = NewPipeline(settings);

        physical.SetTriggers(right: 200);
        manager.TryTick();

        bool ok = spy.RightY < -10000;
        return ok
            ? (true, $"vertical -50%: RightY={spy.RightY} (hacia abajo)")
            : (false, $"RightY={spy.RightY} (esperado negativo)");
    }

    private static (bool, string) TestOscillation()
    {
        var settings = new AimSyncSettings();
        settings.AntiRecoil.Enabled = true;
        settings.AntiRecoil.Arma1Vertical = 0.4f;
        settings.AntiRecoil.Oscillation = true;
        settings.AntiRecoil.OscillationStrength = 0.6f;
        settings.AntiRecoil.OscillationFrequency = 10f;
        var (manager, physical, spy) = NewPipeline(settings);

        physical.SetTriggers(right: 200);
        int maxAbs = 0;
        for (int i = 0; i < 30; i++)
        {
            manager.TryTick();
            maxAbs = Math.Max(maxAbs, Math.Abs(spy.LeftX));
        }

        // La intensidad ES la amplitud (60% -> ~19660), sin reducir a la mitad.
        short expected = (short)MathF.Round(0.6f * short.MaxValue);
        bool ok = maxAbs >= expected - 1500;
        return ok
            ? (true, $"intensidad 60% -> LeftX maximo |{maxAbs}| (esperado ~{expected}, amplitud completa)")
            : (false, $"LeftX maximo |{maxAbs}| (esperado ~{expected}); la oscilacion es demasiado debil o nula");
    }

    private static (bool, string) TestPersistence()
    {
        string path = Path.Combine(Path.GetTempPath(), $"aimsync_recoil_{Guid.NewGuid():N}.json");
        try
        {
            var source = new AimSyncSettings();
            var r = source.AntiRecoil;
            r.Enabled = true;
            r.UseArma2 = true;
            r.WeaponDetectionEnabled = false;
            r.Arma1Vertical = 0.42f;
            r.Arma1Horizontal = -0.17f;
            r.Arma2Vertical = 0.71f;
            r.Arma2Horizontal = 0.09f;
            r.Oscillation = true;
            r.OscillationStrength = 0.55f;
            r.OscillationFrequency = 33f;
            r.PumpEnabled = true;

            var store = new SettingsStore(path);
            store.Save(source);

            var loaded = new AimSyncSettings();
            bool found = store.Load(loaded);
            var l = loaded.AntiRecoil;

            bool ok = found && l.Enabled && l.UseArma2 && !l.WeaponDetectionEnabled
                   && Math.Abs(l.Arma1Vertical - 0.42f) < 0.001f
                   && Math.Abs(l.Arma1Horizontal - (-0.17f)) < 0.001f
                   && Math.Abs(l.Arma2Vertical - 0.71f) < 0.001f
                   && Math.Abs(l.Arma2Horizontal - 0.09f) < 0.001f
                   && l.Oscillation && Math.Abs(l.OscillationStrength - 0.55f) < 0.001f
                   && Math.Abs(l.OscillationFrequency - 33f) < 0.001f && l.PumpEnabled;

            return ok
                ? (true, $"recargado: enabled={l.Enabled}, arma2={l.UseArma2}, v1={l.Arma1Vertical}, h1={l.Arma1Horizontal}, v2={l.Arma2Vertical}, osc={l.Oscillation}@{l.OscillationFrequency}Hz, pump={l.PumpEnabled}")
                : (false, $"found={found}, enabled={l.Enabled}, arma2={l.UseArma2}, v1={l.Arma1Vertical}, osc={l.Oscillation}, pump={l.PumpEnabled}");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
