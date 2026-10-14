using System.Runtime.InteropServices;

namespace AimSync.Core.Input;

public sealed class MouseCapture : IMouseCapture
{
    private const uint WmInput = 0x00FF;
    private const uint WmQuit = 0x0012;
    private const uint WmAction = 0x8000;
    private const uint RimTypeMouse = 0;
    private const uint RidInput = 0x10000003;
    private const uint RidevInputSink = 0x00000100;
    private const ushort MouseWheelFlag = 0x0400;
    private const int WhMouseLl = 14;
    private const int WmMouseMove = 0x0200;

    private static readonly IntPtr HwndMessage = new(-3);

    private readonly Thread _thread;
    private readonly object _sync = new();
    private readonly Queue<Action> _actions = new();
    private volatile bool _running;
    private volatile int _suppressLegacy;
    private uint _nativeThreadId;
    private int _deltaX;
    private int _deltaY;
    private int _wheelDelta;
    private IntPtr _hookHandle;
    private LowLevelMouseProc? _hookProc;   // must stay rooted while the hook is installed

    public MouseCapture()
    {
        _running = true;
        _thread = new Thread(ThreadMain)
        {
            IsBackground = true,
            Name = "AimSync.RawInputMouse",
        };
        _thread.Start();
    }

    /// <summary>
    /// Mientras esta activo y la supresion esta ON, un hook global (WH_MOUSE_LL) descarta los
    /// eventos legacy de movimiento del mouse: el cursor no se mueve y el juego en primer plano
    /// no recibe movimientos reales (por eso deja de parpadear "teclado" y se queda en mando).
    /// La captura Raw Input sigue recibiendo los deltas normalmente.
    /// </summary>
    public void SetSuppressLegacy(bool suppress)
    {
        lock (_sync)
        {
            _actions.Enqueue(() => ApplySuppression(suppress));
        }

        if (_nativeThreadId != 0)
            PostThreadMessage(_nativeThreadId, WmAction, IntPtr.Zero, IntPtr.Zero);
    }

    public bool TryTakeDeltas(out int deltaX, out int deltaY)
    {
        lock (_sync)
        {
            deltaX = _deltaX;
            deltaY = _deltaY;
            _deltaX = 0;
            _deltaY = 0;
        }

        return true;
    }

    public bool TryTakeWheel(out int wheelDelta)
    {
        lock (_sync)
        {
            wheelDelta = _wheelDelta;
            _wheelDelta = 0;
        }

        return true;
    }

    public bool IsButtonDown(MouseButton button)
    {
        int vk = button switch
        {
            MouseButton.Left => 0x01,
            MouseButton.Right => 0x02,
            MouseButton.Middle => 0x04,
            MouseButton.XButton1 => 0x05,
            MouseButton.XButton2 => 0x06,
            _ => 0,
        };

        return (GetAsyncKeyState(vk) & 0x8000) != 0;
    }

    public void Dispose()
    {
        if (!_running)
            return;

        _running = false;
        if (_nativeThreadId != 0)
            PostThreadMessage(_nativeThreadId, WmQuit, IntPtr.Zero, IntPtr.Zero);

        if (_thread.IsAlive)
            _thread.Join(2000);
    }

    private void ApplySuppression(bool suppress)
    {
        if (suppress && _hookHandle == IntPtr.Zero)
        {
            _hookProc = OnLowLevelMouse;
            _hookHandle = SetWindowsHookEx(WhMouseLl, _hookProc, GetModuleHandleW(null), 0);
        }
        else if (!suppress && _hookHandle != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
            _hookProc = null;
        }

        _suppressLegacy = suppress ? 1 : 0;
    }

    private IntPtr OnLowLevelMouse(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam.ToInt32() == WmMouseMove)
            return new IntPtr(-1);

        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private void ThreadMain()
    {
        _nativeThreadId = GetCurrentThreadId();

        IntPtr hwnd = CreateWindowExW(
            0, "STATIC", "AimSyncRawInputMouse", 0,
            0, 0, 0, 0,
            HwndMessage, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

        if (hwnd == IntPtr.Zero)
        {
            _running = false;
            return;
        }

        RawInputDevice device = new()
        {
            UsagePage = 0x01,
            Usage = 0x02,
            Flags = RidevInputSink,
            Target = hwnd,
        };
        RegisterRawInputDevices(ref device, 1, (uint)Marshal.SizeOf<RawInputDevice>());

        while (_running && GetMessageW(out Msg msg, IntPtr.Zero, 0, 0) > 0)
        {
            if (msg.Message == WmInput)
            {
                ProcessRawInput(msg.LParam);
            }
            else if (msg.Message == WmAction)
            {
                Action[] actions;
                lock (_sync)
                {
                    actions = _actions.ToArray();
                    _actions.Clear();
                }

                foreach (var action in actions)
                {
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"MouseCapture action: {ex.Message}");
                    }
                }
            }
        }

        ApplySuppression(false);
        DestroyWindow(hwnd);
    }

    private void ProcessRawInput(IntPtr lParam)
    {
        uint size = 0;
        uint headerSize = (uint)Marshal.SizeOf<RawInputHeader>();
        if (GetRawInputData(lParam, RidInput, IntPtr.Zero, ref size, headerSize) == unchecked((uint)-1) || size == 0)
            return;

        IntPtr buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            uint read = GetRawInputData(lParam, RidInput, buffer, ref size, headerSize);
            if (read == unchecked((uint)-1))
                return;

            var header = Marshal.PtrToStructure<RawInputHeader>(buffer);
            if (header.Type != RimTypeMouse)
                return;

            IntPtr mousePtr = IntPtr.Add(buffer, Marshal.SizeOf<RawInputHeader>());
            var mouse = Marshal.PtrToStructure<RawMouse>(mousePtr);

            lock (_sync)
            {
                _deltaX += mouse.LastX;
                _deltaY += mouse.LastY;
                if ((mouse.Flags & MouseWheelFlag) != 0)
                    _wheelDelta += (short)mouse.ButtonData;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterRawInputDevices(ref RawInputDevice pRawInputDevices, uint uiNumDevices, uint cbSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowExW(
        uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
        int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll", EntryPoint = "GetMessageW")]
    private static extern int GetMessageW(out Msg lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string? lpModuleName);

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputDevice
    {
        public ushort UsagePage;
        public ushort Usage;
        public uint Flags;
        public IntPtr Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputHeader
    {
        public uint Type;
        public uint Size;
        public IntPtr Device;
        public IntPtr WParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawMouse
    {
        public ushort Flags;
        public ushort ButtonFlags;
        public ushort ButtonData;
        public uint RawButtons;
        public int LastX;
        public int LastY;
        public uint ExtraInformation;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Msg
    {
        public IntPtr Hwnd;
        public uint Message;
        public IntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public Point Pt;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }
}