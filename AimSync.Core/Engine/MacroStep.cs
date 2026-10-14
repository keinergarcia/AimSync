namespace AimSync.Core.Engine;

/// <summary>
/// Un paso de una macro: mantiene pulsado <see cref="Button"/> durante <see cref="DurationMs"/>.
/// </summary>
public sealed class MacroStep
{
    public ControllerButton Button { get; set; } = ControllerButton.A;

    public int DurationMs { get; set; } = 60;
}
