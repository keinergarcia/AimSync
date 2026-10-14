namespace AimSync.Core.Input;

public interface IMouseCapture : IDisposable
{
    bool TryTakeDeltas(out int deltaX, out int deltaY);
    bool TryTakeWheel(out int wheelDelta);
    bool IsButtonDown(MouseButton button);
}