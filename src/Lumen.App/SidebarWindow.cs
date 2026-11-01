using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Lumen.Core;

namespace Lumen.App;

// A borderless full-height strip on one edge of one screen, with real acrylic behind it on Windows.
public sealed class SidebarWindow : Window
{
    private bool reserved;

    public SidebarView? View { get; private set; }

    public SidebarWindow()
    {
        Title = "Lumen";
        SystemDecorations = SystemDecorations.None;
        CanResize = false;
        ShowInTaskbar = false;
        ShowActivated = false;
        Background = Brushes.Transparent;
        Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://Lumen/Assets/lumen.ico")));
        ScalingChanged += (_, _) => { if (settings != null) Place(settings); };
        // Any later size change (DPI, columns, width) must keep the strip flush with its edge.
        Resized += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (settings != null && Position != target) Position = target; }, Avalonia.Threading.DispatcherPriority.Background);
        Opened += (_, _) => { if (settings != null) Place(settings); };
    }

    private Settings? settings;

    public void Apply(Settings s, SidebarView view)
    {
        settings = s;
        View = view;
        Content = view;
        Topmost = s.Topmost;
        TransparencyLevelHint = s.Glass
            ? new[] { WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.Blur, WindowTransparencyLevel.Transparent }
            : new[] { WindowTransparencyLevel.None };
        Place(s);
        ClickThrough(s.ClickThrough);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int index);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int index, int value);

    // Mouse clicks pass through to the windows behind (WS_EX_TRANSPARENT); the sidebar stays visible.
    private void ClickThrough(bool on)
    {
        if (!OperatingSystem.IsWindows()) return;
        var handle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (handle == IntPtr.Zero) { if (on) Opened += (_, _) => ClickThrough(true); return; }
        const int ExStyle = -20, Transparent = 0x20, Layered = 0x80000;
        int style = GetWindowLong(handle, ExStyle);
        // Clicks only fall through a layered window; fully opaque layering keeps it looking the same.
        int wanted = on ? style | Transparent | Layered : style & ~Transparent & ~(addedLayered ? Layered : 0);
        if (wanted == style) return;
        if (on && (style & Layered) == 0) addedLayered = true;
        SetWindowLong(handle, ExStyle, wanted);
        if (on && (wanted & Layered) != 0) SetLayeredWindowAttributes(handle, 0, 255, 2);
        if (!on) addedLayered = false;
    }

    private bool addedLayered;

    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint key, byte alpha, uint flags);

    private void Place(Settings s)
    {
        var all = Screens.All;
        if (all.Count == 0) { Width = SidebarView.WindowWidth(s); Height = 900; return; }
        var screen = all[Math.Clamp(s.Screen, 0, all.Count - 1)];
        double scale = screen.Scaling;
        var work = screen.WorkingArea;
        // Full screen: the whole working area, from the left edge to the right, top to taskbar.
        double logical = s.FullScreen ? work.Width / scale : SidebarView.WindowWidth(s);
        int width = s.FullScreen ? work.Width : (int)Math.Round(logical * scale);
        bool right = s.Edge == Edge.Right;
        int x = s.FullScreen ? work.X : right ? work.Right - width : work.X, y = work.Y, h = work.Height;
        var handle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (OperatingSystem.IsWindows() && handle != IntPtr.Zero)
        {
            if (s.ReserveSpace && !s.FullScreen)
            {
                // The work area already excludes our own strip once reserved, so ask against the full bounds.
                var b = screen.Bounds;
                (x, y, _, h) = AppBar.Reserve(handle, right, b.X, work.Y, b.Right, work.Bottom, width);
                reserved = true;
            }
            else if (reserved) { AppBar.Release(handle); reserved = false; }
        }
        // Size first, then position: Windows keeps the left edge when a window is resized, so
        // moving before resizing left a right-docked sidebar stranded away from the edge.
        Width = logical;
        Height = h / scale;
        Position = new PixelPoint(x, y);
        target = new PixelPoint(x, y);
        // The resize lands a moment later; dock again once it has.
        Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (Position != target) Position = target; }, Avalonia.Threading.DispatcherPriority.Background);
    }

    private PixelPoint target;

    public void Release()
    {
        var handle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (reserved && handle != IntPtr.Zero && OperatingSystem.IsWindows()) AppBar.Release(handle);
        reserved = false;
    }

    protected override void OnClosed(EventArgs e)
    {
        Release();
        base.OnClosed(e);
    }
}
