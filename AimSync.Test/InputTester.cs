using AimSync.Core;
using AimSync.Core.Input;
using AimSync.Core.Mapping;

namespace AimSync.Test;

internal static class InputTester
{
    private static readonly ControllerButton[] AllButtons = Enum.GetValues<ControllerButton>();

    public static void RunTester(bool useRealController)
    {
        var settings = new AimSyncSettings();
        using var deviceManager = new DeviceManager();
        IVirtualController? created = useRealController ? deviceManager.CreateController() : null;
        using var manager = new InputManager(settings, () => created!);

        TryClear();
        Console.WriteLine("Preparando Input Tester...");
        manager.Start();

        RunLoop(manager, useRealController: useRealController, xinputMode: false);
    }

    public static void RunTestMode()
    {
        TryClear();
        Console.WriteLine("=== AimSync TEST MODE ===");
        Console.WriteLine("Ruta completa: Input -> Mapping -> StickEngine -> VirtualController -> ViGEm -> XInput");
        Console.WriteLine();

        if (!ViGEmUtil.IsBusInstalled())
        {
            Console.WriteLine("ERROR: no se encontraron los archivos del driver ViGEmBus.");
            Console.WriteLine("Instala ViGEmBus 1.22.0 desde https://github.com/nefarius/ViGEmBus/releases/tag/v1.22.0");
            Console.WriteLine();
            Console.WriteLine("Vas a entrar al Input Tester con controlador espejo (Spy).");
            Pause();
            RunTester(useRealController: false);
            return;
        }

        var settings = new AimSyncSettings();
        using var deviceManager = new DeviceManager();
        IVirtualController? created = deviceManager.CreateController();
        using var manager = new InputManager(settings, () => created!);

        manager.Start();
        RunLoop(manager, useRealController: true, xinputMode: true);
    }

    private static void RunLoop(InputManager manager, bool useRealController, bool xinputMode)
    {
        bool refreshXInput = false;
        if (xinputMode)
        {
            XInputReader.RefreshAvailability();
            refreshXInput = XInputReader.IsAvailable;
            if (!refreshXInput)
            {
                Console.WriteLine("[WARN] xinput1_4.dll no disponible; el readback XInput se omite.");
                Console.WriteLine("       Se mostrarÃ¡ solo la salida del controlador virtual.");
                Pause();
            }
        }

        while (true)
        {
            if (TryHasKey())
            {
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Escape)
                    break;
            }

            Render(manager, useRealController, xinputMode && refreshXInput);
            Thread.Sleep(33);
        }

        TryClear();
        Console.WriteLine("Input Tester finalizado.");
    }

    private static void Render(InputManager manager, bool useRealController, bool xinputMode)
    {
        var lines = new List<string>
        {
            "AimSync -- Input Tester        F8 = activar/desactivar | ESC = salir",
            $"Modo  : {(useRealController ? "Controlador virtual real (ViGEm)" : "Controlador espejo (Spy, sin ViGEm)")}",
            $"Estado: {(manager.IsEnabled ? "ACTIVADO" : "DESACTIVADO")}   ViGEm: {(manager.Controller?.IsConnected == true ? "conectado" : "desconectado")}",
            "",
            "KEYBOARD",
            $"  W[{Indicator(IsPressed(manager, KeyCode.W))}] A[{Indicator(IsPressed(manager, KeyCode.A))}] " +
            $"S[{Indicator(IsPressed(manager, KeyCode.S))}] D[{Indicator(IsPressed(manager, KeyCode.D))}]",
            $"  Otras: {FormatPressedKeys(manager)}",
            "",
            "MOUSE",
            $"  DX={manager.Mouse.DeltaX,6}  DY={manager.Mouse.DeltaY,6}",
            $"  Botones: {FormatMouseButtons(manager.Mouse)}",
            "",
            "VIRTUAL CONTROLLER",
            $"  Left Stick : X={manager.CurrentOutput.LeftX,+7:0.000} Y={manager.CurrentOutput.LeftY,+7:0.000}",
            $"  Right Stick: X={manager.CurrentOutput.RightX,+7:0.000} Y={manager.CurrentOutput.RightY,+7:0.000}",
            $"  RT: {manager.CurrentOutput.RightTrigger,3}  LT: {manager.CurrentOutput.LeftTrigger,3}",
            $"  Botones: {FormatButtons(manager.CurrentOutput)}",
        };

        if (xinputMode)
        {
            var gamepad = XInputReader.ReadFirstGamepad();
            lines.Add("");
            lines.Add("XINPUT READBACK (lo que verÃ­a el juego)");
            lines.Add($"  {XInputReader.FormatGamepad(gamepad)}");
        }

        lines.Add("");
        lines.Add("Mueve el mouse o pulsa teclas para ver el flujo en tiempo real.");

if (!TrySetCursor(0, 0))
        {
            int count = 0;
            foreach (var line in lines)
            {
                if (++count <= 17)
                    Console.WriteLine(line);
            }

            return;
        }

        int width = TryGetWindowWidth() ?? 120;
        foreach (var line in lines)
        {
            Console.Write(line);
            Console.Write(new string(' ', Math.Max(0, width - line.Length)));
            Console.WriteLine();
        }
    }

    private static int? TryGetWindowWidth()
    {
        try
        {
            return Console.WindowWidth;
        }
        catch
        {
            return null;
        }
    }

    private static bool TryHasKey()
    {
        try
        {
            return Console.KeyAvailable;
        }
        catch
        {
            return false;
        }
    }

private static bool TryClear()
    {
        try
        {
            Console.Clear();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TrySetCursor(int left, int top)
    {
        try
        {
            Console.SetCursorPosition(left, top);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsPressed(InputManager manager, KeyCode key) => manager.Keyboard.IsPressed(key);

    private static string Indicator(bool on) => on ? "1" : "-";

    private static string FormatPressedKeys(InputManager manager)
    {
        var names = manager.Keyboard.PressedKeys.Select(static k => k.ToString()).ToList();
        return names.Count == 0 ? "(ninguna)" : string.Join(", ", names);
    }

    private static string FormatMouseButtons(MouseManager mouse)
    {
        string[] cells = new string[5];
        cells[0] = $"L[{Indicator(mouse.IsButtonDown(MouseButton.Left))}]";
        cells[1] = $"R[{Indicator(mouse.IsButtonDown(MouseButton.Right))}]";
        cells[2] = $"M[{Indicator(mouse.IsButtonDown(MouseButton.Middle))}]";
        cells[3] = $"X1[{Indicator(mouse.IsButtonDown(MouseButton.XButton1))}]";
        cells[4] = $"X2[{Indicator(mouse.IsButtonDown(MouseButton.XButton2))}]";
        return string.Join(" ", cells);
    }

    private static string FormatButtons(ControllerState state)
    {
        string[] cells = new string[AllButtons.Length];
        for (int i = 0; i < AllButtons.Length; i++)
            cells[i] = $"{ShortName(AllButtons[i])}:{(state.GetButton(AllButtons[i]) ? 1 : 0)}";
        return string.Join(" ", cells);
    }

    private static string ShortName(ControllerButton button) => button switch
    {
        ControllerButton.LeftShoulder => "LB",
        ControllerButton.RightShoulder => "RB",
        ControllerButton.LeftThumb => "LS",
        ControllerButton.RightThumb => "RS",
        ControllerButton.DPadUp => "DUp",
        ControllerButton.DPadDown => "DDn",
        ControllerButton.DPadLeft => "DLt",
        ControllerButton.DPadRight => "DRt",
        _ => button.ToString(),
    };

    private static void Pause()
    {
        Console.WriteLine("Pulsa Enter para continuar...");
        Console.ReadKey(true);
        Console.WriteLine();
    }
}
