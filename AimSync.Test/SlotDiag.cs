using AimSync.Core;

namespace AimSync.Test;

/// <summary>
/// Diagnostico del slot XInput del mando virtual: conecta un ViGEm, localiza
/// que slot ocupa comparando antes/despues, escribe botones y lee SIEMPRE el
/// slot del mando virtual (no el primero conectado).
/// Uso: --slotdiag
/// </summary>
internal static class SlotDiag
{
    public static void Run()
    {
        Console.WriteLine("=== AimSync SlotDiag ===");
        Console.WriteLine();

        if (!ViGEmUtil.IsBusInstalled())
        {
            Console.WriteLine("ERROR: ViGEmBus no instalado.");
            return;
        }

        var before = XInputReader.FindConnectedSlots();
        Console.WriteLine($"[INFO] Slots XInput antes  : {XInputReader.FormatSlots(before)}");

        using var deviceManager = new DeviceManager();
        var controller = deviceManager.CreateController();
        if (!controller.Connect())
        {
            Console.WriteLine($"[FAIL] Connect(): {controller.LastError}");
            return;
        }
        Console.WriteLine("[INFO] Mando virtual conectado (ViGEm).");

        Thread.Sleep(500);
        var after = XInputReader.FindConnectedSlots();
        Console.WriteLine($"[INFO] Slots XInput despues : {XInputReader.FormatSlots(after)}");

        var newSlots = after.Except(before).ToList();
        uint virtualSlot;
        if (newSlots.Count == 1)
        {
            virtualSlot = newSlots[0];
            Console.WriteLine($"[OK] Slot del mando virtual detectado: {virtualSlot}");
        }
        else
        {
            Console.WriteLine($"[WARN] Cambio de slots ambiguo: {string.Join(",", newSlots)}. Cayendo al primero de la diferencia.");
            virtualSlot = newSlots.FirstOrDefault();
        }

        void WriteAndRead(string label, Action write, ushort expectedButton)
        {
            Console.Write($"[TEST] {label} ... ");
            write();
            Thread.Sleep(250);
            bool ok = XInputReader.TryGetStateVirtual(virtualSlot, out var st);
            if (!ok)
            {
                Console.WriteLine("[FAIL] slot virtual no legible via XInputGetState.");
                return;
            }
            bool buttonOk = expectedButton == 0 || (st.Gamepad.wButtons & expectedButton) != 0;
            Console.WriteLine(ok && buttonOk
                ? $"[PASS] wButtons=0x{st.Gamepad.wButtons:X4} LX={st.Gamepad.sThumbLX} LY={st.Gamepad.sThumbLY} RT={st.Gamepad.bRightTrigger}"
                : $"[FAIL] wButtons=0x{st.Gamepad.wButtons:X4} LX={st.Gamepad.sThumbLX} LY={st.Gamepad.sThumbLY} RT={st.Gamepad.bRightTrigger} (esperado bit 0x{expectedButton:X4})");
        }

        WriteAndRead("A on", () => controller.SetButton(ControllerButton.A, true), 0x1000);
        WriteAndRead("X on", () => controller.SetButton(ControllerButton.X, true), 0x4000);
        WriteAndRead("Y on", () => controller.SetButton(ControllerButton.Y, true), 0x8000);

        Console.Write("\n[TEST] Reset() y sondeo durante 2s ... ");
        controller.Reset();
        for (int i = 0; i < 20; i++)
        {
            Thread.Sleep(100);
            if (XInputReader.TryGetStateVirtual(virtualSlot, out var st))
                Console.Write($"[{i * 100}ms B=0x{st.Gamepad.wButtons:X4}] ");
        }
        XInputReader.TryGetStateVirtual(virtualSlot, out var afterReset);
        Console.WriteLine(afterReset.Gamepad.wButtons == 0
            ? "\n[PASS] Reset limpio botones."
            : $"\n[FAIL] Reset NO limpio: B=0x{afterReset.Gamepad.wButtons:X4}");

        WriteAndRead("SetButton(A,false) explicito", () => controller.SetButton(ControllerButton.A, false), 0);
        WriteAndRead("RightTrigger 200", () => controller.SetRightTrigger(200), 0);

        controller.Dispose();
        Thread.Sleep(300);
        var final = XInputReader.FindConnectedSlots();
        Console.WriteLine($"[INFO] Slots XInput tras soltar: {XInputReader.FormatSlots(final)}");
    }
}