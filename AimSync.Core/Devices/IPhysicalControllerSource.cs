namespace AimSync.Core.Devices;

/// <summary>
/// Fuente de lectura de mandos fisicos. La implementacion real usa XInput;
/// las pruebas usan una implementacion simulada.
/// </summary>
public interface IPhysicalControllerSource : IDisposable
{
    /// <summary>Nombre del API de entrada usado (p. ej. "XInput (xinput1_4)").</summary>
    string ApiName { get; }

    /// <summary>Indica si el API pudo cargarse en este sistema.</summary>
    bool IsAvailable { get; }

    /// <summary>Numero de slots consultables (XInput: 4).</summary>
    int SlotCount { get; }

    /// <summary>
    /// Intenta leer el estado de un slot. Devuelve false si el slot no tiene
    /// un mando conectado o si el API no esta disponible.
    /// </summary>
    bool TryRead(int index, out PhysicalControllerState state);
}
