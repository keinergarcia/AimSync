using AimSync.Core.Devices;

namespace AimSync.Test;

/// <summary>
/// Sonda de hardware para la Fase A. Requiere un mando GameSir/XInput real
/// conectado. No forma parte de las pruebas automatizadas.
/// Uso: --controllerprobe [--watch]
/// </summary>
internal static class ControllerProbe
{
    public static void Run(bool watch)
    {
        Console.WriteLine("=== AimSync Controller Probe (Fase A) ===");
        Console.WriteLine();

        using var source = new XInputControllerSource();
        Console.WriteLine($"[INFO] API de entrada : {source.ApiName}");
        Console.WriteLine($"[INFO] Disponible     : {source.IsAvailable}");
        Console.WriteLine();

        Console.WriteLine("[INFO] Dispositivos de juego que reporta Windows (PnP):");
        var probe = new WindowsDeviceProbe();
        var windowsDevices = probe.Enumerate();
        if (windowsDevices.Count == 0)
        {
            Console.WriteLine("       (ninguno presente)");
        }
        else
        {
            foreach (var device in windowsDevices)
            {
                string vid = device.VendorId is { } v ? $"0x{v:X4}" : "n/a";
                string pid = device.ProductId is { } p ? $"0x{p:X4}" : "n/a";
                string kind = device.IsXInput ? "XInput" : "HID";
                Console.WriteLine($"       - [{kind}] {device.Name}");
                Console.WriteLine($"         InstanceId : {device.InstanceId}");
                Console.WriteLine($"         HardwareId : {device.HardwareId ?? "n/a"}");
                Console.WriteLine($"         VID/PID    : {vid} / {pid}{(device.Problem is null ? "" : $"  [{device.Problem}]")}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("[INFO] Slots XInput activos y estado crudo:");
        int connected = 0;
        for (int index = 0; index < source.SlotCount; index++)
        {
            if (source.TryRead(index, out PhysicalControllerState state))
            {
                connected++;
                Console.WriteLine($"       slot {index}: {Format(state)}");
            }
        }

        if (connected == 0)
        {
            Console.WriteLine("       (ningun mando XInput conectado)");
            Console.WriteLine();
            Console.WriteLine("Conecta el GameSir en modo XInput y vuelve a ejecutar la sonda.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine($"[OK] {connected} mando(s) XInput detectado(s).");

        if (!watch)
        {
            Console.WriteLine("Ejecuta con --watch para ver el estado en vivo (ESC para salir).");
            return;
        }

        Console.WriteLine("Estado en vivo (mueve sticks, gatillos y botones). ESC para salir.");
        Console.WriteLine();

        bool interactive = !Console.IsInputRedirected;
        var timer = System.Diagnostics.Stopwatch.StartNew();
        while (true)
        {
            if (interactive && Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)
                break;

            if (!interactive && timer.ElapsedMilliseconds > 8000)
                break;

            int slot = FirstConnected(source);
            if (slot < 0)
                break;

            if (!source.TryRead(slot, out PhysicalControllerState state))
            {
                Thread.Sleep(32);
                continue;
            }

            Console.Write($"\rslot {slot}: {Format(state)}      ");
            Thread.Sleep(16);
        }

        Console.WriteLine();
    }

    private static int FirstConnected(IPhysicalControllerSource source)
    {
        for (int index = 0; index < source.SlotCount; index++)
        {
            if (source.TryRead(index, out _))
                return index;
        }

        return -1;
    }

    private static string Format(PhysicalControllerState s)
        => $"LX={s.LeftX,6} LY={s.LeftY,6} RX={s.RightX,6} RY={s.RightY,6} "
           + $"LT={s.LeftTrigger,3} RT={s.RightTrigger,3} B=0x{(ushort)s.Buttons:X4} DPad={s.DPadDirection}";
}
