namespace AimSync.Core;

public sealed class DeviceManager : IDisposable
{
    private readonly List<IVirtualController> _controllers = [];
    private bool _disposed;

    public IReadOnlyList<IVirtualController> Controllers => _controllers.AsReadOnly();

    public IVirtualController CreateController()
    {
        ThrowIfDisposed();
        var controller = new ViGEmController();
        _controllers.Add(controller);
        return controller;
    }

    public bool RemoveController(IVirtualController controller)
    {
        ThrowIfDisposed();
        if (!_controllers.Remove(controller))
            return false;

        controller.Dispose();
        return true;
    }

    public void RemoveAllControllers()
    {
        ThrowIfDisposed();
        foreach (var controller in _controllers)
        {
            controller.Dispose();
        }
        _controllers.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        foreach (var controller in _controllers)
        {
            controller.Dispose();
        }
        _controllers.Clear();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}