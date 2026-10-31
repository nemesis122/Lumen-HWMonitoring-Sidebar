using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace Lumen.App;

// Everything that touches Windows itself: rights, autostart, the reserved screen edge.
public class Platform
{
    public string SettingsPath { get; init; } = Lumen.Core.Settings.DefaultPath();
    private const string Task = "Lumen Sidebar";

    public virtual bool IsAdministrator
    {
        get
        {
            if (!OperatingSystem.IsWindows()) return false;
            using var id = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    public static string ExePath => Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "Lumen.exe");

    public virtual void RestartElevated()
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            Process.Start(new ProcessStartInfo(ExePath) { UseShellExecute = true, Verb = "runas", Arguments = "--elevated" });
            Environment.Exit(0);
        }
        catch (System.ComponentModel.Win32Exception) { /* the user said no to the prompt */ }
    }

    // A logon task with highest rights starts Lumen elevated without a UAC prompt every time.
    public virtual bool AutostartEnabled => OperatingSystem.IsWindows() && RunSchtasks($"/Query /TN \"{Task}\"", false) == 0;

    public virtual bool SetAutostart(bool on)
    {
        if (!OperatingSystem.IsWindows()) return false;
        string args = on
            ? $"/Create /F /TN \"{Task}\" /SC ONLOGON /RL HIGHEST /DELAY 0000:10 /TR \"\\\"{ExePath}\\\"\""
            : $"/Delete /F /TN \"{Task}\"";
        return RunSchtasks(args, !IsAdministrator) == 0;
    }

    private static int RunSchtasks(string args, bool elevate)
    {
        try
        {
            var info = new ProcessStartInfo("schtasks.exe", args) { CreateNoWindow = true, UseShellExecute = elevate, WindowStyle = ProcessWindowStyle.Hidden };
            if (elevate) info.Verb = "runas";
            using var p = Process.Start(info);
            if (p is null) return -1;
            p.WaitForExit(15000);
            return p.ExitCode;
        }
        catch { return -1; }
    }
}

// Reserves a strip of the desktop, like the taskbar does, so maximised windows stop before the sidebar.
public static class AppBar
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct Data
    {
        public int cbSize; public IntPtr hWnd; public uint uCallbackMessage; public uint uEdge; public Rect rc; public IntPtr lParam;
    }

    [DllImport("shell32.dll")] private static extern UIntPtr SHAppBarMessage(uint msg, ref Data data);
    [DllImport("user32.dll")] private static extern uint RegisterWindowMessage(string name);

    private const uint New = 0, Remove = 1, QueryPos = 2, SetPos = 3;
    private static readonly HashSet<IntPtr> registered = new();

    // Returns the strip actually granted (in physical pixels), which may differ from the request.
    public static (int X, int Y, int W, int H) Reserve(IntPtr hwnd, bool right, int left, int top, int rightEdge, int bottom, int width)
    {
        var d = new Data { cbSize = Marshal.SizeOf<Data>(), hWnd = hwnd, uEdge = right ? 2u : 0u };
        if (registered.Add(hwnd)) { d.uCallbackMessage = RegisterWindowMessage("LumenAppBar"); SHAppBarMessage(New, ref d); }
        d.rc = new Rect { Top = top, Bottom = bottom, Left = right ? rightEdge - width : left, Right = right ? rightEdge : left + width };
        SHAppBarMessage(QueryPos, ref d);
        if (right) d.rc.Left = d.rc.Right - width; else d.rc.Right = d.rc.Left + width;
        SHAppBarMessage(SetPos, ref d);
        return (d.rc.Left, d.rc.Top, d.rc.Right - d.rc.Left, d.rc.Bottom - d.rc.Top);
    }

    public static void Release(IntPtr hwnd)
    {
        if (!registered.Remove(hwnd)) return;
        var d = new Data { cbSize = Marshal.SizeOf<Data>(), hWnd = hwnd };
        SHAppBarMessage(Remove, ref d);
    }
}
