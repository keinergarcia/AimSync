namespace AimSync.Core.Diagnostics;

/// <summary>
/// Opciones de registro de diagnóstico. Permite acotar el número de eventos retenidos
/// y limitar la frecuencia de registro para no degradar el bucle de entrada.
/// </summary>
public sealed class DiagnosticLogOptions
{
    public static readonly DiagnosticLogOptions Default = new();

    /// <summary>Número máximo de eventos retenidos. Los más antiguos se descartan.</summary>
    public int MaxEvents { get; set; } = 200;

    /// <summary>
    /// Intervalo mínimo entre eventos registrados, en milisegundos.
    /// 0 desactiva el limitado por frecuencia. Evita inundar la UI en bucles de alta frecuencia.
    /// </summary>
    public int MinIntervalMs { get; set; } = 0;

    /// <summary>Si es false, no se registra ningún evento (instrumentación desactivada).</summary>
    public bool Enabled { get; set; } = true;
}
