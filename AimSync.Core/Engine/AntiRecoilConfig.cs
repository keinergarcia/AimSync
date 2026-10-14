using AimSync.Core.Input;

namespace AimSync.Core.Engine;

/// <summary>
/// Configuracion del sistema anti-recoil: dos armas con empuje vertical y horizontal
/// propios, mas oscilacion (movimiento lateral del stick izquierdo) mientras se dispara.
/// </summary>
public sealed class AntiRecoilConfig
{
    /// <summary>Sistema activo/desactivado.</summary>
    public bool Enabled { get; set; }

    /// <summary>true = el perfil inicial es Arma 2; false = Arma 1. Solo decide el arma de arranque; en vivo se usa <see cref="ActiveWeapon"/>.</summary>
    public bool UseArma2 { get; set; }

    /// <summary>Deteccion automatica de arma: pulsar <see cref="Weapon1Key"/> o <see cref="Weapon2Key"/> cambia el perfil al instante.</summary>
    public bool WeaponDetectionEnabled { get; set; } = true;

    /// <summary>Tecla que selecciona Arma 1 (por defecto '1' del teclado).</summary>
    public KeyCode Weapon1Key { get; set; } = KeyCode.Digit1;

    /// <summary>Tecla que selecciona Arma 2 (por defecto '2' del teclado).</summary>
    public KeyCode Weapon2Key { get; set; } = KeyCode.Digit2;

    /// <summary>Fuerza de empuje vertical del Arma 1 (-1..1). Positivo sube el apuntador, negativo lo baja.</summary>
    public float Arma1Vertical { get; set; }

    /// <summary>Fuerza de empuje horizontal del Arma 1 (-1..1). Negativo = izquierda.</summary>
    public float Arma1Horizontal { get; set; }

    /// <summary>Fuerza de empuje vertical del Arma 2 (-1..1). Positivo sube el apuntador, negativo lo baja.</summary>
    public float Arma2Vertical { get; set; }

    /// <summary>Fuerza de empuje horizontal del Arma 2 (-1..1). Negativo = izquierda.</summary>
    public float Arma2Horizontal { get; set; }

    /// <summary>Oscilacion del stick izquierdo mientras se dispara.</summary>
    public bool Oscillation { get; set; }

    /// <summary>Intensidad de la oscilacion (0..1, % de deflexion).</summary>
    public float OscillationStrength { get; set; } = 0.3f;

    /// <summary>
    /// Frecuencia de la oscilacion en Hz (ciclos por segundo). Valores bajos (2..12 Hz) son los
    /// que los juegos registran: frecuencias altas el filtrado/aliasing del stick de movimiento
    /// las elimina. Maximo tecnico ~120 Hz (Nyquist del bucle).
    /// </summary>
    public float OscillationFrequency { get; set; } = 8f;

    /// <summary>
    /// Refuerzo por rafagas (experimental): mientras se dispara, el empuje no es constante sino
    /// que se reafirma a ~20 Hz (mantiene la fuerza maxima la mayor parte del ciclo y la libera
    /// brevemente). En algunos juegos esto contrarresta mejor el retroceso por disparo; en otros
    /// puede empeorar. Desactivado por defecto.
    /// </summary>
    public bool PumpEnabled { get; set; }
}