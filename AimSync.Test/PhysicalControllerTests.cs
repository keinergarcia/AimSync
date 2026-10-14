using AimSync.Core.Devices;

namespace AimSync.Test;

/// <summary>
/// Pruebas de la Fase A: deteccion y mapeo de datos del mando fisico, usando
/// una fuente simulada. No requieren hardware conectado.
/// </summary>
internal static class PhysicalControllerTests
{
    public static int RunAll()
    {
        int failures = 0;

        Run("C1  Deteccion de conexion", TestConnectionDetected, ref failures);
        Run("C2  Deteccion de desconexion", TestDisconnectionDetected, ref failures);
        Run("C3  Evento DevicesChanged", TestDevicesChangedEvent, ref failures);
        Run("C4  Seleccion de mando (slot)", TestSelectSlot, ref failures);
        Run("C5  Normalizacion de ejes", TestAxisNormalization, ref failures);
        Run("C6  Normalizacion de gatillos", TestTriggerNormalization, ref failures);
        Run("C7  Mapeo de botones ABXY/LB/RB", TestButtonMapping, ref failures);
        Run("C8  Direccion de cruceta con diagonales", TestDPadDirection, ref failures);
        Run("C9  Nombre real de Windows (probe)", TestNameFromProbe, ref failures);
        Run("C10 Nombre generico sin probe", TestGenericName, ref failures);
        Run("C11 Estado Error si Windows reporta problema", TestErrorStatus, ref failures);
        Run("C12 Fuente no disponible no rompe", TestUnavailableSource, ref failures);
        Run("C13 Estado se publica para el slot seleccionado", TestSelectedSlotState, ref failures);
        Run("C14 Prefiere la clase XInput para el nombre", TestXInputClassPreferred, ref failures);

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

    private static (bool, string) TestConnectionDetected()
    {
        var source = new FakePhysicalControllerSource();
        using var monitor = new PhysicalControllerMonitor(source, new FakeWindowsDeviceProbe());

        monitor.PollOnce();
        bool empty = monitor.Devices.Count == 0;

        source.SetState(1, buttons: PhysicalButtons.A);
        monitor.PollOnce();
        bool detected = monitor.Devices.Count == 1
                        && monitor.Devices[0].Index == 1
                        && monitor.Devices[0].Status == PhysicalControllerStatus.Connected;

        return empty && detected
            ? (true, "sin mando -> 0 dispositivos; tras conectar slot 1 -> detectado Conectado")
            : (false, $"inicial={empty} detectado={detected} count={monitor.Devices.Count}");
    }

    private static (bool, string) TestDisconnectionDetected()
    {
        var source = new FakePhysicalControllerSource();
        using var monitor = new PhysicalControllerMonitor(source, new FakeWindowsDeviceProbe());

        source.SetState(0);
        monitor.PollOnce();
        bool connected = monitor.Devices.Count == 1 && monitor.Devices[0].IsConnected;

        source.SetDisconnected(0);
        monitor.PollOnce();
        bool disconnected = monitor.Devices.Count == 1
                            && monitor.Devices[0].Status == PhysicalControllerStatus.Disconnected
                            && !monitor.Latest.IsConnected;

        return connected && disconnected
            ? (true, "conectado -> desconectado sin excepcion, estado Desconectado")
            : (false, $"conectado={connected} desconectado={disconnected}");
    }

    private static (bool, string) TestDevicesChangedEvent()
    {
        var source = new FakePhysicalControllerSource();
        using var monitor = new PhysicalControllerMonitor(source, new FakeWindowsDeviceProbe());

        int events = 0;
        monitor.DevicesChanged += () => events++;

        monitor.PollOnce(); // sin cambios relevantes (0 dispositivos antes y despues)
        int beforeConnect = events;

        source.SetState(0);
        monitor.PollOnce();
        bool firedOnConnect = events > beforeConnect;

        source.SetDisconnected(0);
        monitor.PollOnce();
        bool firedOnDisconnect = events > beforeConnect + 1;

        return firedOnConnect && firedOnDisconnect
            ? (true, $"eventos: conexion y desconexion notificados (total={events})")
            : (false, $"beforeConnect={beforeConnect} onConnect={firedOnConnect} onDisconnect={firedOnDisconnect} total={events}");
    }

    private static (bool, string) TestSelectSlot()
    {
        var source = new FakePhysicalControllerSource();
        using var monitor = new PhysicalControllerMonitor(source, new FakeWindowsDeviceProbe());

        source.SetState(0);
        source.SetState(2, buttons: PhysicalButtons.B);
        monitor.PollOnce();

        monitor.SelectIndex(2);
        monitor.PollOnce();
        bool ok = monitor.SelectedIndex == 2 && monitor.Latest.Index == 2 && monitor.Latest.Has(PhysicalButtons.B);

        return ok
            ? (true, "slot 2 seleccionado, Latest refleja su estado")
            : (false, $"selected={monitor.SelectedIndex} latestIndex={monitor.Latest.Index}");
    }

    private static (bool, string) TestAxisNormalization()
    {
        bool max = Math.Abs(PhysicalControllerState.NormalizeAxis(short.MaxValue) - 1f) < 0.0001f;
        bool min = Math.Abs(PhysicalControllerState.NormalizeAxis(short.MinValue) + 1f) < 0.0001f;
        bool zero = PhysicalControllerState.NormalizeAxis(0) == 0f;
        bool half = Math.Abs(PhysicalControllerState.NormalizeAxis(16384) - 0.5f) < 0.001f;

        return max && min && zero && half
            ? (true, "32767->1.0, -32768->-1.0, 0->0.0, 16384->~0.5")
            : (false, $"max={max} min={min} zero={zero} half={half}");
    }

    private static (bool, string) TestTriggerNormalization()
    {
        var zero = new PhysicalControllerState(0, 1, true, 0, 0, 0, 0, 0, 0, PhysicalButtons.None);
        var full = new PhysicalControllerState(0, 1, true, 0, 0, 0, 0, 255, 255, PhysicalButtons.None);
        var half = new PhysicalControllerState(0, 1, true, 0, 0, 0, 0, 128, 128, PhysicalButtons.None);

        bool ok = zero.LeftTriggerLevel == 0f && zero.RightTriggerLevel == 0f
                  && Math.Abs(full.LeftTriggerLevel - 1f) < 0.0001f
                  && Math.Abs(half.LeftTriggerLevel - 128f / 255f) < 0.0001f;

        return ok
            ? (true, "0->0.0, 255->1.0, 128->~0.502")
            : (false, $"zero={zero.LeftTriggerLevel} full={full.LeftTriggerLevel} half={half.LeftTriggerLevel}");
    }

    private static (bool, string) TestButtonMapping()
    {
        var source = new FakePhysicalControllerSource();
        using var monitor = new PhysicalControllerMonitor(source, new FakeWindowsDeviceProbe());

        var buttons = PhysicalButtons.A | PhysicalButtons.B | PhysicalButtons.X | PhysicalButtons.Y
                      | PhysicalButtons.LeftShoulder | PhysicalButtons.RightShoulder
                      | PhysicalButtons.View | PhysicalButtons.Menu;

        source.SetState(0, buttons: buttons);
        monitor.PollOnce();

        var s = monitor.Latest;
        bool ok = s.Has(PhysicalButtons.A) && s.Has(PhysicalButtons.B) && s.Has(PhysicalButtons.X) && s.Has(PhysicalButtons.Y)
                  && s.Has(PhysicalButtons.LeftShoulder) && s.Has(PhysicalButtons.RightShoulder)
                  && s.Has(PhysicalButtons.View) && s.Has(PhysicalButtons.Menu)
                  && !s.Has(PhysicalButtons.Nexus);

        return ok
            ? (true, "ABXY + LB/RB + View/Menu detectados; Nexus no reportado")
            : (false, $"buttons=0x{(ushort)s.Buttons:X4}");
    }

    private static (bool, string) TestDPadDirection()
    {
        var center = new PhysicalControllerState(0, 1, true, 0, 0, 0, 0, 0, 0, PhysicalButtons.None);
        var up = center with { Buttons = PhysicalButtons.DPadUp };
        var upLeft = center with { Buttons = PhysicalButtons.DPadUp | PhysicalButtons.DPadLeft };
        var right = center with { Buttons = PhysicalButtons.DPadRight };

        bool ok = center.DPadDirection == "Centro"
                  && up.DPadDirection == "Arriba"
                  && upLeft.DPadDirection == "Arriba-Izquierda"
                  && right.DPadDirection == "Derecha";

        return ok
            ? (true, $"Centro / Arriba / Arriba-Izquierda / Derecha")
            : (false, $"center={center.DPadDirection} up={up.DPadDirection} upLeft={upLeft.DPadDirection} right={right.DPadDirection}");
    }

    private static (bool, string) TestNameFromProbe()
    {
        var source = new FakePhysicalControllerSource();
        var probe = new FakeWindowsDeviceProbe();
        probe.Add("Controlador de Xbox 360 para Windows",
            "USB\\VID_3537&PID_1096&MI_00\\X", "USB\\VID_3537&PID_1096&MI_00", 0x3537, 0x1096);

        using var monitor = new PhysicalControllerMonitor(source, probe);
        source.SetState(0);
        monitor.PollOnce();

        var device = monitor.Devices[0];
        bool ok = device.Name == "Controlador de Xbox 360 para Windows"
                  && device.VendorId == 0x3537 && device.ProductId == 0x1096;

        return ok
            ? (true, $"nombre Windows='{device.Name}' VID=0x{device.VendorId:X4} PID=0x{device.ProductId:X4}")
            : (false, $"nombre='{device.Name}' vid={device.VendorId} pid={device.ProductId}");
    }

    private static (bool, string) TestGenericName()
    {
        var source = new FakePhysicalControllerSource();
        using var monitor = new PhysicalControllerMonitor(source, new FakeWindowsDeviceProbe());

        source.SetState(3);
        monitor.PollOnce();

        bool ok = monitor.Devices[0].Name == "Mando XInput (slot 3)";
        return ok
            ? (true, $"sin nombre de Windows -> '{monitor.Devices[0].Name}'")
            : (false, $"nombre='{monitor.Devices[0].Name}'");
    }

    private static (bool, string) TestErrorStatus()
    {
        var source = new FakePhysicalControllerSource();
        var probe = new FakeWindowsDeviceProbe();
        probe.Add("Mando con problema", "USB\\X", "USB\\X", null, null, true, "Codigo de problema 43");

        using var monitor = new PhysicalControllerMonitor(source, probe);
        source.SetState(0);
        monitor.PollOnce();

        source.SetDisconnected(0);
        monitor.PollOnce();

        var device = monitor.Devices[0];
        bool ok = device.Status == PhysicalControllerStatus.Error && device.StatusText == "Error";
        return ok
            ? (true, $"Windows reporta problema -> estado Error ('{device.Problem}')")
            : (false, $"status={device.Status} problem={device.Problem}");
    }

    private static (bool, string) TestUnavailableSource()
    {
        var source = new FakePhysicalControllerSource { IsAvailable = false };
        using var monitor = new PhysicalControllerMonitor(source, new FakeWindowsDeviceProbe());

        int errors = 0;
        monitor.Error += _ => errors++;

        monitor.PollOnce();
        monitor.PollOnce();

        bool ok = !monitor.IsAvailable && monitor.Devices.Count == 0 && errors == 1;
        return ok
            ? (true, $"fuente no disponible: 0 dispositivos, 1 aviso (no se repite)")
            : (false, $"available={monitor.IsAvailable} devices={monitor.Devices.Count} errores={errors}");
    }

    private static (bool, string) TestSelectedSlotState()
    {
        var source = new FakePhysicalControllerSource();
        using var monitor = new PhysicalControllerMonitor(source, new FakeWindowsDeviceProbe());

        source.SetState(0, leftX: 100);
        source.SetState(1, leftX: 20000);
        monitor.PollOnce();

        monitor.SelectIndex(1);
        monitor.PollOnce();

        bool ok = monitor.Latest.Index == 1 && monitor.Latest.LeftX == 20000;
        return ok
            ? (true, "Latest publica el estado del slot seleccionado (LX=20000)")
            : (false, $"index={monitor.Latest.Index} LX={monitor.Latest.LeftX}");
    }

    private static (bool, string) TestXInputClassPreferred()
    {
        var source = new FakePhysicalControllerSource();
        var probe = new FakeWindowsDeviceProbe();

        // Caso real: Windows expone varias entradas (HID) del mismo mando,
        // pero solo una de la clase XInput (XnaComposite).
        probe.Add("Dispositivo de entrada USB", "USB\\VID_3537&PID_1096&IG_00\\X", "USB\\VID_3537&PID_1096&IG_00", 0x3537, 0x1096, isXInput: false);
        probe.Add("Controlador de Xbox 360 para Windows", "USB\\VID_3537&PID_1096&MI_00\\Y", "USB\\VID_3537&PID_1096&MI_00", 0x3537, 0x1096, isXInput: true);
        probe.Add("Controlador del sistema compatible con HID", "HID\\VID_4E53&PID_5406\\Z", "HID\\VID_4E53&PID_5406", 0x4E53, 0x5406, isXInput: false);

        using var monitor = new PhysicalControllerMonitor(source, probe);
        source.SetState(0);
        monitor.PollOnce();

        var device = monitor.Devices[0];
        bool ok = device.Name == "Controlador de Xbox 360 para Windows" && device.VendorId == 0x3537;
        return ok
            ? (true, $"nombre de la clase XInput aunque haya mas entradas HID: '{device.Name}'")
            : (false, $"nombre='{device.Name}' (esperado Controlador de Xbox 360 para Windows)");
    }
}
