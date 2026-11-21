using System;
using System.Runtime.InteropServices;

static class Native
{
    public const int WM_WINDOWPOSCHANGING = 0x0046;
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_NOACTIVATE = 0x08000000;
    public const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOZORDER = 0x4, SWP_NOACTIVATE = 0x10;
    public static readonly IntPtr HWND_BOTTOM = new IntPtr(1);

    [StructLayout(LayoutKind.Sequential)]
    public struct WINDOWPOS
    {
        public IntPtr hwnd; public IntPtr hwndInsertAfter;
        public int x, y, cx, cy; public uint flags;
    }
    [StructLayout(LayoutKind.Sequential)] public struct MARGINS { public int l, r, t, b; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)]
    struct AccentPolicy { public int AccentState, AccentFlags, GradientColor, AnimationId; }
    [StructLayout(LayoutKind.Sequential)]
    struct WCAD { public int Attribute; public IntPtr Data; public int SizeOfData; }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string cls, string name);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowLong(IntPtr h, int idx);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int SetWindowLong(IntPtr h, int idx, int v);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] static extern int SetWindowCompositionAttribute(IntPtr h, ref WCAD d);
    [DllImport("user32.dll")] public static extern int SetWindowRgn(IntPtr h, IntPtr rgn, bool redraw);
    [DllImport("gdi32.dll")] public static extern IntPtr CreateRoundRectRgn(int l, int t, int r, int b, int w, int h);
    [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr h, ref MARGINS m);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int v, int size);

    [StructLayout(LayoutKind.Sequential)]
    public struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag;
        public int BatteryLifeTime, BatteryFullLifeTime;
    }
    [DllImport("kernel32.dll")] public static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS s);
    [DllImport("secur32.dll", CharSet = CharSet.Unicode)]
    public static extern bool GetUserNameEx(int format, System.Text.StringBuilder name, ref uint size);

    // SetupAPI (Bluetooth device battery)
    [StructLayout(LayoutKind.Sequential)]
    public struct SP_DEVINFO_DATA { public int cbSize; public Guid ClassGuid; public int DevInst; public IntPtr Reserved; }
    [StructLayout(LayoutKind.Sequential)]
    public struct DEVPROPKEY { public Guid fmtid; public uint pid; }
    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr SetupDiGetClassDevs(IntPtr classGuid, string enumerator, IntPtr hwnd, uint flags);
    [DllImport("setupapi.dll", SetLastError = true)]
    public static extern bool SetupDiEnumDeviceInfo(IntPtr set, uint index, ref SP_DEVINFO_DATA d);
    [DllImport("setupapi.dll", SetLastError = true)]
    public static extern bool SetupDiGetDevicePropertyW(IntPtr set, ref SP_DEVINFO_DATA d, ref DEVPROPKEY key,
        out uint type, byte[] buf, uint size, out uint req, uint flags);
    [DllImport("setupapi.dll")] public static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);

    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc p, IntPtr l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string name);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);

    // The top-level window (Progman or a WorkerW) that hosts the desktop icons.
    // Widgets are placed just above it: over the wallpaper/icons, under every app window.
    public static IntPtr DesktopHost()
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows(delegate(IntPtr h, IntPtr l)
        {
            if (FindWindowEx(h, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero) { found = h; return false; }
            return true;
        }, IntPtr.Zero);
        if (found == IntPtr.Zero) found = FindWindow("Progman", null);
        return found;
    }

    public static void EnableBlur(IntPtr hwnd)
    {
        AccentPolicy a = new AccentPolicy();
        a.AccentState = 3; // ACCENT_ENABLE_BLURBEHIND
        int size = Marshal.SizeOf(typeof(AccentPolicy));
        IntPtr p = Marshal.AllocHGlobal(size);
        Marshal.StructureToPtr(a, p, false);
        WCAD d = new WCAD(); d.Attribute = 19; d.Data = p; d.SizeOfData = size;
        SetWindowCompositionAttribute(hwnd, ref d);
        Marshal.FreeHGlobal(p);
    }

    public static void ExtendFrame(IntPtr hwnd)
    {
        MARGINS m = new MARGINS(); m.l = -1; m.r = -1; m.t = -1; m.b = -1;
        DwmExtendFrameIntoClientArea(hwnd, ref m);
    }

    public static void RoundCorners(IntPtr hwnd)
    {
        int v = 2; // DWMWCP_ROUND
        DwmSetWindowAttribute(hwnd, 33, ref v, 4);
    }
}
