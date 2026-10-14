using AimSync.App.Services;
using AimSync.App.ViewModels;
using AimSync.Core;

namespace AimSync.Test;

/// <summary>
/// Verifica el auto-activado del anti-recoil en la capa UI: mover un empuje o encender la
/// oscilacion debe poner Enabled=true y seleccionar el arma correspondiente, para que el
/// usuario no tenga que pulsar la radio de arma por separado (como en turbo/macros).
/// </summary>
internal static class AntiRecoilUiTests
{
    public static int RunAll()
    {
        int failures = 0;

        Run("UI-1 Empuje vertical de Arma 1 activa y selecciona Arma 1", TestAutoEnableArma1, ref failures);
        Run("UI-2 Empuje de Arma 2 activa y selecciona Arma 2", TestAutoEnableArma2, ref failures);
        Run("UI-3 Encender oscilacion activa el anti-recoil", TestOscillationEnables, ref failures);
        Run("UI-4 Fuerza 0 no activa el anti-recoil", TestZeroDoesNotEnable, ref failures);

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

    private static (AimSyncSettings, AntiRecoilViewModel) NewViewModel()
    {
        var settings = new AimSyncSettings();
        var spy = new SpyVirtualController();
        var service = new AimSyncService(settings, () => spy, new FakePhysicalSource());
        var vm = new AntiRecoilViewModel(service);
        return (settings, vm);
    }

    private static (bool, string) TestAutoEnableArma1()
    {
        var (settings, vm) = NewViewModel();
        vm.Arma1Vertical = 0.7;

        bool ok = settings.AntiRecoil.Enabled
               && !settings.AntiRecoil.UseArma2
               && vm.ActiveWeapon == 1
               && Math.Abs(settings.AntiRecoil.Arma1Vertical - 0.7f) < 0.001f;

        vm.Dispose();
        return ok
            ? (true, $"enabled={settings.AntiRecoil.Enabled}, arma activa={vm.ActiveWeapon}, v1={settings.AntiRecoil.Arma1Vertical}")
            : (false, $"enabled={settings.AntiRecoil.Enabled}, arma activa={vm.ActiveWeapon}, v1={settings.AntiRecoil.Arma1Vertical}");
    }

    private static (bool, string) TestAutoEnableArma2()
    {
        var (settings, vm) = NewViewModel();
        vm.Arma2Vertical = 0.5;

        bool ok = settings.AntiRecoil.Enabled
               && settings.AntiRecoil.UseArma2
               && vm.ActiveWeapon == 2;

        vm.Dispose();
        return ok
            ? (true, $"enabled={settings.AntiRecoil.Enabled}, arma activa={vm.ActiveWeapon}")
            : (false, $"enabled={settings.AntiRecoil.Enabled}, arma activa={vm.ActiveWeapon}");
    }

    private static (bool, string) TestOscillationEnables()
    {
        var (settings, vm) = NewViewModel();
        vm.Oscillation = true;

        bool ok = settings.AntiRecoil.Enabled && settings.AntiRecoil.Oscillation;

        vm.Dispose();
        return ok
            ? (true, $"enabled={settings.AntiRecoil.Enabled}, oscilacion={settings.AntiRecoil.Oscillation}")
            : (false, $"enabled={settings.AntiRecoil.Enabled}, oscilacion={settings.AntiRecoil.Oscillation}");
    }

    private static (bool, string) TestZeroDoesNotEnable()
    {
        var (settings, vm) = NewViewModel();
        vm.Arma1Vertical = 0;

        bool ok = !settings.AntiRecoil.Enabled;

        vm.Dispose();
        return ok
            ? (true, $"fuerza 0 mantiene enabled={settings.AntiRecoil.Enabled}")
            : (false, $"enabled={settings.AntiRecoil.Enabled} (deberia seguir false)");
    }
}
