using System.Runtime.InteropServices;
using AimSync.Core.Input;

namespace AimSync.Test;

internal static class InputInjector
{
    private const uint InputKeyboard = 1;
    private const uint InputMouse = 0;
    private const ushort KeyeventfKeyup = 0x0002;
    private const ushort MouseeventfMove = 0x0001;
    private const ushort MouseeventfLeftdown = 0x0002;
    private const ushort MouseeventfLeftup = 0x0004;
    private const ushort MouseeventfRightdown = 0x0008;
    private const ushort MouseeventfRightup = 0x0010;
    private const ushort MouseeventfMiddledown = 0x0020;
    private const ushort MouseeventfMiddleup = 0x0040;

    public static bool PressAndRelease(KeyCode key)
    {
        if (!SendKey(key, true) || !SendKey(key, false))
            return false;
        Thread.Sleep(30);
        return true;
    }

    public static bool SendKey(KeyCode key, bool down)
    {
        NativeInput[] inputs =
        {
            new()
            {
                type = InputKeyboard,
                U = new NativeInputUnion
                {
                    ki = new NativeKeybdInput
                    {
                        wVk = (ushort)key,
                        wScan = 0,
                        dwFlags = down ? (uint)0 : KeyeventfKeyup,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero,
                    },
                },
            },
        };

        return SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeInput>()) == inputs.Length;
    }

    public static bool MoveMouseRelative(int dx, int dy)
    {
        NativeInput[] inputs =
        {
            new()
            {
                type = InputMouse,
                U = new NativeInputUnion
                {
                    mi = new NativeMouseInput
                    {
                        dx = dx,
                        dy = dy,
                        mouseData = 0,
                        dwFlags = MouseeventfMove,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero,
                    },
                },
            },
        };

        return SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeInput>()) == inputs.Length;
    }

    public static bool Click(MouseButton button)
    {
        ushort down = button switch
        {
            MouseButton.Left => MouseeventfLeftdown,
            MouseButton.Right => MouseeventfRightdown,
            MouseButton.Middle => MouseeventfMiddledown,
            _ => 0,
        };
        ushort up = button switch
        {
            MouseButton.Left => MouseeventfLeftup,
            MouseButton.Right => MouseeventfRightup,
            MouseButton.Middle => MouseeventfMiddleup,
            _ => 0,
        };

        if (down == 0 || up == 0)
            return false;

        return SendButtonFlags(down) && SendButtonFlags(up);
    }

    private static bool SendButtonFlags(ushort flags)
    {
        NativeInput[] inputs =
        {
            new()
            {
                type = InputMouse,
                U = new NativeInputUnion
                {
                    mi = new NativeMouseInput
                    {
                        dx = 0,
                        dy = 0,
                        mouseData = 0,
                        dwFlags = flags,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero,
                    },
                },
            },
        };

        return SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeInput>()) == inputs.Length;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint cInputs, NativeInput[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeInput
    {
        public uint type;
        public NativeInputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct NativeInputUnion
    {
        [FieldOffset(0)] public NativeMouseInput mi;
        [FieldOffset(0)] public NativeKeybdInput ki;
        [FieldOffset(0)] public NativeHardwareInput hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMouseInput
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeKeybdInput
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeHardwareInput
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }
}