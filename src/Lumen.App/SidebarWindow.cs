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
    }

    private void Place(Settings s)
    {
        var all = Screens.All;
        if (all.Count == 0) { Width = SidebarView.WindowWidth(s); Height = 900; return; }
        var screen = all[Math.Clamp(s.Screen, 0, all.Count - 1)];
        double scale = screen.Scaling;
        double logical = SidebarView.WindowWidth(s);
        int width = (int)Math.Round(logical * scale);
        var work = screen.WorkingArea;
        bool right = s.Edge == Edge.Right;
        int x = right ? work.Right - width : work.X, y = work.Y, h = work.Height;
        var handle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (OperatingSystem.IsWindows() && handle != IntPtr.Zero)
        {
            if (s.ReserveSpace)
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
