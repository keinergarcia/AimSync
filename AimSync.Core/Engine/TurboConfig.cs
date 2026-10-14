namespace AimSync.Core.Engine;

/// <summary>
/// Modo turbo: mientras la entrada esta activa, la salida alterna ON/OFF en vez de mantenerse
/// fija. La velocidad de los GATILLOS (LT/RT) y la de los BOTONES en rafaga son independientes:
/// cada una tiene su propia frecuencia y duracion de pulso.
/// </summary>
public sealed class TurboConfig
{
    /// <summary>Modo turbo activo/desactivado.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Turbo aplicado al gatillo izquierdo (LT). Por defecto DESACTIVADO: activar el modo turbo
    /// (por ejemplo para la rafaga de botones) no debe pulsar los gatillos por accidente. El
    /// disparo normal del gatillo queda intacto hasta que el usuario active esto de forma explicita.
    /// </summary>
    public bool LeftEnabled { get; set; }

    /// <summary>
    /// Turbo aplicado al gatillo derecho (RT). Por defecto DESACTIVADO: activar el modo turbo
    /// (por ejemplo para la rafaga de botones) no debe pulsar los gatillos por accidente. El
    /// disparo normal analogico del gatillo queda intacto hasta que el usuario active esto.
    /// </summary>
    public bool RightEnabled { get; set; }

    /// <summary>Velocidad de la rafaga de BOTONES: pulsos por segundo (ciclos completos ON+OFF).</summary>
    public float FrequencyHz { get; set; } = 10f;

    /// <summary>Fraccion del ciclo de la rafaga de BOTONES durante la que el boton se mantiene pulsado (0.05..1).</summary>
    public float DutyCycle { get; set; } = 0.5f;

    /// <summary>
    /// Velocidad de la rafaga de GATILLOS (LT/RT), INDEPENDIENTE de la de los botones.
    /// Pulsos por segundo (ciclos completos ON+OFF).
    /// </summary>
    public float TriggerFrequencyHz { get; set; } = 10f;

    /// <summary>
    /// Fraccion del ciclo de la rafaga de GATILLOS durante la que el gatillo se mantiene pulsado (0.05..1).
    /// Independiente de <see cref="DutyCycle"/> (botones).
    /// </summary>
    public float TriggerDutyCycle { get; set; } = 0.5f;

    /// <summary>
    /// Botones en rafaga maxima: mientras su entrada esta pulsada, el boton alterna
    /// ON/OFF a la frecuencia de botones configurada.
    /// </summary>
    public List<ControllerButton> RapidButtons { get; set; } = [];

    /// <summary>
    /// Botones en rafaga constante: alternan ON/OFF siempre mientras Modo turbo esta activo,
    /// SIN depender de que se mantenga pulsado ningun boton (ideal para usar solo con mando virtual).
    /// </summary>
    public List<ControllerButton> RapidAlways { get; set; } = [];
}
