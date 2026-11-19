using System.Runtime.InteropServices;

namespace Lumen.App;

internal static class MonitorRefresh
{
    public static double? Read(nint window)
    {
        if (!OperatingSystem.IsWindows() || window == 0) return null;
        var monitor = MonitorFromWindow(window, 2);
        if (monitor == 0) return null;
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info) || string.IsNullOrEmpty(info.Device)) return null;
        var context = CreateDC("DISPLAY", info.Device, null, 0);
        if (context == 0) return null;
        try
        {
            int hz = GetDeviceCaps(context, 116); // VREFRESH. Values 0 and 1 mean the default rate.
            return hz > 1 ? hz : null;
        }
        finally { DeleteDC(context); }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfo
    {
        public int Size;
        public int Left, Top, Right, Bottom;
        public int WorkLeft, WorkTop, WorkRight, WorkBottom;
        public int Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string? Device;
    }

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint window, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("gdi32.dll", EntryPoint = "CreateDCW", CharSet = CharSet.Unicode)]
    private static extern nint CreateDC(string driver, string device, string? port, nint initData);
    [DllImport("gdi32.dll")]
    private static extern int GetDeviceCaps(nint context, int index);
    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(nint context);
}
