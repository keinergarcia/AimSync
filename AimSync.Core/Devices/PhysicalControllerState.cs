namespace AimSync.Core.Devices;

/// <summary>
/// Estado inmutable de un mando fisico en un instante. Se publica como
/// referencia (record) para poder leerlo desde el hilo de UI sin bloqueos.
/// Los valores crudos son los que entrega la API (XInput) sin transformar.
/// </summary>
public sealed record PhysicalControllerState(
    int Index,
    uint PacketNumber,
    bool IsConnected,
    short LeftX,
    short LeftY,
    short RightX,
    short RightY,
    byte LeftTrigger,
    byte RightTrigger,
    PhysicalButtons Buttons)
{
    public const short XInputAxisMin = -32768;
    public const short XInputAxisMax = 32767;

    public static PhysicalControllerState Disconnected(int index)
        => new(index, 0, false, 0, 0, 0, 0, 0, 0, PhysicalButtons.None);

    public bool Has(PhysicalButtons button) => (Buttons & button) != 0;

    public float LeftXNormalized => NormalizeAxis(LeftX);
    public float LeftYNormalized => NormalizeAxis(LeftY);
    public float RightXNormalized => NormalizeAxis(RightX);
    public float RightYNormalized => NormalizeAxis(RightY);

    public float LeftTriggerLevel => LeftTrigger / 255f;
    public float RightTriggerLevel => RightTrigger / 255f;

    /// <summary>Direccion de la cruceta en texto (admite diagonales).</summary>
    public string DPadDirection
    {
        get
        {
            bool up = Has(PhysicalButtons.DPadUp);
            bool down = Has(PhysicalButtons.DPadDown);
            bool left = Has(PhysicalButtons.DPadLeft);
            bool right = Has(PhysicalButtons.DPadRight);

            if (!up && !down && !left && !right)
                return "Centro";

            string vertical = up && !down ? "Arriba" : down && !up ? "Abajo" : "";
            string horizontal = left && !right ? "Izquierda" : right && !left ? "Derecha" : "";

            if (vertical.Length > 0 && horizontal.Length > 0)
                return $"{vertical}-{horizontal}";

            return vertical.Length > 0 ? vertical : horizontal;
        }
    }

    public static float NormalizeAxis(short value) => Math.Clamp(value / 32767f, -1f, 1f);
}
