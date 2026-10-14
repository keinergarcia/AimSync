using AimSync.Core;
using AimSync.Core.Devices;
using AimSync.Core.Engine;
using AimSync.Core.Input;

namespace AimSync.Test;

/// <summary>
/// Diagnostico con HARDWARE real: lee el mando fisico (GameSir) por XInput y,
/// por separado, comprueba que el mando virtual emite los pulsos y vuelven a
/// leerse por XInput (lo que veria el juego).
/// Espera interactivamente a que el usuario pulse (no depende de tiempos).
/// </summary>
internal static class PhysioDiag
{
public static void Run()
        {
            Console.WriteLine("=== Physio Diag: lectura fisica + feedback virtual (HARDWARE) ===");
            Console.WriteLine();

            using var source = new XInputControllerSource();
            var wasConnected = new bool[source.SlotCount];

            Console.WriteLine("--- 1) Mapa de slots XInput ---");
            for (int i = 0; i < source.SlotCount; i++)
            {
                bool ok = source.TryRead(i, out var st);
                wasConnected[i] = st.IsConnected;
                Console.WriteLine($"  slot {i}: ok={ok} connected={st.IsConnected} B=0x{(ushort)st.Buttons:X4} LT={st.LeftTrigger} RT={st.RightTrigger}");
            }
            Console.WriteLine();

            Console.WriteLine("--- 2) Lectura del mando fisico ---");
            Console.WriteLine("   Pulsa y suelta 'Y' del GameSir. Esperando...");
            if (WaitFor(25_000, source, "Y", s => (s.Buttons & PhysicalButtons.Y) != 0))
                Console.WriteLine("   [OK] Y fisico leido y soltado.");
            else
                Console.WriteLine("   [FALLO] No se detecto el Y fisico en 25 s.");
            Console.WriteLine("   Pulsa y suelta el gatillo derecho (RT). Esperando...");
            if (WaitFor(25_000, source, "RT", s => s.RightTrigger > 128))
                Console.WriteLine("   [OK] RT fisico leido y soltado.");
            else
                Console.WriteLine("   [FALLO] No se detecto el RT fisico en 25 s.");
            Console.WriteLine();

        Console.WriteLine("--- 3) Mando virtual: conectar + pulsos de Y con el mando fisico ---");
        using var deviceManager = new DeviceManager();
        var virtualController = deviceManager.CreateController();
        Console.Write("  Conectando ViGEm virtual... ");
        bool connected = virtualController.Connect();
        Console.WriteLine(connected ? "OK" : "FALLO");
        if (!connected)
        {
            Console.WriteLine("  El mando virtual no conecto: el juego no puede recibir nada.");
            return;
        }

        Thread.Sleep(300);
        int virtualSlot = -1;
        Console.WriteLine("  Slot asignado al virtual:");
        for (int i = 0; i < source.SlotCount; i++)
        {
            source.TryRead(i, out var st);
            Console.WriteLine($"    slot {i}: connected={st.IsConnected} B=0x{(ushort)st.Buttons:X4}");
            if (st.IsConnected && !wasConnected[i])
                virtualSlot = i;
        }
        if (virtualSlot < 0)
        {
            virtualSlot = 0;
            Console.WriteLine($"  (slot del virtual no detectado por diferencia; comprobando slot {virtualSlot})");
        }

        var settings = new AimSyncSettings();
        var turbo = settings.Turbo;
        turbo.Enabled = true;
        turbo.LeftEnabled = false;
        turbo.RightEnabled = false;
        turbo.FrequencyHz = 12f;
        turbo.DutyCycle = 0.5f;
        turbo.RapidButtons = [ControllerButton.Y];
        var manager = new InputManager(settings, () => virtualController, physicalSource: source);
        manager.Activate();

        Console.WriteLine();
        Console.WriteLine("  DALE al Y del GameSir cuando quieras: mide 4 s de rafaga mientras lo mantengas pulsado.");
        System.Media.SystemSounds.Asterisk.Play();
        var swW = System.Diagnostics.Stopwatch.StartNew();
        bool physicalHeld = false;
        while (!physicalHeld)
        {
            manager.TryTick();
            Thread.Sleep(4);
            for (int i = 0; i < source.SlotCount; i++)
            {
                source.TryRead(i, out var pst);
                if (pst.IsConnected && (pst.Buttons & PhysicalButtons.Y) != 0)
                {
                    physicalHeld = true;
                    break;
                }
            }
            if (swW.ElapsedMilliseconds > 30_000)
                break;
        }
        if (!physicalHeld)
        {
            Console.WriteLine("  [FALLO] No se detecto el Y fisico en 30 s: no se puede medir la rafaga.");
        }
        else
        {
            Console.WriteLine($"  [OK] Y fisico detectado en t={swW.ElapsedMilliseconds}ms. Contando pulsos en el mando virtual (slot {virtualSlot}) 4 s...");
            var sw2 = System.Diagnostics.Stopwatch.StartNew();
            int observedOn = 0;
            int observedOff = 0;
            int physicalYSeen = 0;
            while (sw2.ElapsedMilliseconds < 4000)
            {
                manager.TryTick();
                Thread.Sleep(4);
                source.TryRead(virtualSlot, out var st);
                if (!st.IsConnected)
                    continue;
                if ((st.Buttons & PhysicalButtons.Y) != 0)
                    observedOn++;
                else
                    observedOff++;
                for (int i = 0; i < source.SlotCount; i++)
                {
                    source.TryRead(i, out var pst);
                    if (pst.IsConnected && (pst.Buttons & PhysicalButtons.Y) != 0)
                    {
                        physicalYSeen++;
                        break;
                    }
                }
            }
            Console.WriteLine($"  Feedback virtual (slot {virtualSlot}) en 4s: Y ON={observedOn}, OFF={observedOff} (total {observedOn + observedOff}); Y fisico leido={physicalYSeen}");

            string verdict;
            if (observedOn == 0)
                verdict = $"[FALLO] El Y fisico SI se leyo (muestras {physicalYSeen}) pero el mando virtual (slot {virtualSlot}) nunca puso Y en ON.";
            else if (observedOff == 0)
                verdict = $"[FALLO] El mando virtual (slot {virtualSlot}) quedo ENGANCHADO en Y ON (muestras {observedOn}) sin ciclar.";
            else
                verdict = $"[OK] El mando virtual (slot {virtualSlot}) alterna Y ON/OFF ({observedOn}/{observedOn + observedOff}) mientras mantienes el Y fisico.";
            Console.WriteLine(verdict);

            Console.WriteLine("IMPORTANTE: el GameSir fisico sigue ocupando otro slot XInput. Un juego un jugador lee por norma el slot 0 = GameSir, NO este virtual.");
        }

        manager.Stop();
        virtualController.Disconnect();
        Thread.Sleep(100);
        manager.Dispose();
    }

    private static bool WaitFor(int timeoutMs, XInputControllerSource source, string name, Func<PhysicalControllerState, bool> isPressed)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        bool pressed = false;
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            for (int i = 0; i < source.SlotCount; i++)
            {
                source.TryRead(i, out var st);
                if (!st.IsConnected)
                    continue;
                bool on = isPressed(st);
                if (on && !pressed)
                {
                    Console.WriteLine($"     {name} pulsado en slot {i} (t={sw.ElapsedMilliseconds}ms)");
                    pressed = true;
                }
                else if (!on && pressed)
                {
                    Console.WriteLine($"     {name} soltado (t={sw.ElapsedMilliseconds}ms)");
                    return true;
                }
            }
            Thread.Sleep(10);
        }
        return false;
    }
}