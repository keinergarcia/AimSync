using System.Diagnostics;
using AimSync.Core;
using AimSync.Core.Devices;

namespace AimSync.Test;

/// <summary>
/// Diagnostico sin videojuego: lee el gatillo RT del mando fisico (XInput) y muestra, en
/// paralelo, el valor que AimSync escribiria en el mando virtual tras el procesamiento
/// (sensibilidad/umbral) y el modo turbo. Sirve para confirmar el paso analogico 0..255,
/// el retorno a cero y la ausencia de pulsos artificiales cuando el turbo esta OFF.
/// </summary>
internal static class TriggerDiag
{
    public static void Run()
    {
        using var physical = new XInputControllerSource();
        if (!physical.IsAvailable)
        {
            Console.WriteLine("[FAIL] XInput no esta disponible en este sistema.");
            Console.WriteLine("Pulsa una tecla para continuar...");
            Console.ReadKey(true);
            return;
        }

        int slot = 0;
        var settings = new AimSyncSettings();
        var keyboard = new FakeKeyboardCapture();
        var mouse = new FakeMouseCapture();
        var spy = new SpyVirtualController();
        using var manager = new InputManager(settings, () => spy, keyboard, mouse, physical);
        manager.PhysicalInputSlot = slot;
        manager.Activate();

        Console.WriteLine("=== AimSync: diagnostico de gatillo RT (sin juego) ===");
        Console.WriteLine();
        Console.WriteLine($"  Turbo: {(settings.Turbo.Enabled ? "ON" : "OFF")}  (RT en rafaga: {(settings.Turbo.RightEnabled ? "si" : "no")})");
        Console.WriteLine($"  RT sensibilidad: {settings.RightTrigger.Sensitivity:0.00}x   umbral: {settings.RightTrigger.ActivationThreshold:P0}");
        Console.WriteLine($"  Slot fisico: {slot}");
        Console.WriteLine();
        Console.WriteLine("  Manten y suelta RT en el mando. ESC para salir.");
        Console.WriteLine();
        Console.WriteLine("  FISICO                          VIRTUAL");
        Console.WriteLine("  ------------------------------  ------------------------------");

        var sw = Stopwatch.StartNew();
        lastPrint = 0;

        try
        {
            while (true)
            {
                if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)
                    break;

                bool ok = physical.TryRead(slot, out PhysicalControllerState phys);
                manager.TryTick();

                if (sw.ElapsedMilliseconds - lastPrint >= 60)
                {
                    lastPrint = sw.ElapsedMilliseconds;
                    Print(phys, ok, spy);
                }

                Thread.Sleep(1);
            }
        }
        finally
        {
            manager.Deactivate();
        }

        Console.WriteLine();
        Console.WriteLine("Diagnostico finalizado.");
        Console.WriteLine("Pulsa una tecla para continuar...");
        Console.ReadKey(true);
    }

    private static long lastPrint;
    private static int _prevPhysical = -1;
    private static int _prevVirtual = -1;

    private static void Print(PhysicalControllerState phys, bool ok, SpyVirtualController spy)
    {
        int physicalRt = ok ? phys.RightTrigger : -1;
        int virtualRt = spy.RightTrigger;

        bool changed = physicalRt != _prevPhysical || virtualRt != _prevVirtual;
        _prevPhysical = physicalRt;
        _prevVirtual = virtualRt;

        string physText = ok ? $"{physicalRt,3} ({physicalRt / 255f,5:P0})" : "  -- sin mando --";
        string virtText = $"{virtualRt,3} ({virtualRt / 255f,5:P0})";
        bool match = physicalRt == virtualRt;

        string marker = changed ? "  <" : "";
        string status = match ? "OK " : "DIF";
        Console.WriteLine($"  RT {physText,-28}  {virtText,-28} [{status}]{marker}");
    }
}
