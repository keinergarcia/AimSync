using System.Runtime.InteropServices;

namespace AimSync.Core.Input;

public sealed class KeyboardCapture : IKeyboardCapture
{
    public bool IsKeyDown(KeyCode key)
    {
        if (key == KeyCode.None)
            return false;

        return (GetAsyncKeyState((int)key) & 0x8000) != 0;
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);
}