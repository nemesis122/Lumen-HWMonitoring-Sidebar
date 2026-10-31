using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Lumen.App;

// A small notice beside the sidebar, styled like it, that closes by itself after a few seconds.
public sealed class Toast : Window
{
    public Toast(Palette p, string titleText, string message)
    {
        SystemDecorations = SystemDecorations.None;
        CanResize = false;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Width = 340;
        SizeToContent = SizeToContent.Height;
        Background = Brushes.Transparent;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.Transparent };
        var hot = p.Ramp[^1];
        var title = Ui.Text(titleText, 13.5, p.Ink, FontWeight.SemiBold);
        var text = Ui.Text(message, 12.5, p.Soft);
        text.TextWrapping = TextWrapping.Wrap;
        text.TextTrimming = TextTrimming.None;
        var icon = Ui.Glyph(Glyphs.Shield, hot, 20);
        icon.VerticalAlignment = VerticalAlignment.Top;
        var body = Ui.Stack(3, title, text);
        body.Margin = new Thickness(12, 0, 0, 0);
        Content = new Border
        {
            Background = Palette.Brush(Palette.Mix(p.Dark ? Color.Parse("#171C25") : Colors.White, hot, .08)),
            BorderBrush = Palette.Brush(Palette.Alpha(hot, .55)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16, 14),
            Child = Ui.Columns("Auto,*", icon, body)
        };
        PointerPressed += (_, _) => Close();
        DispatcherTimer.RunOnce(Close, TimeSpan.FromSeconds(9));
    }

    // Bottom corner of the sidebar's screen, on the inner side of the sidebar.
    public void ShowBeside(Window sidebar, bool right)
    {
        var screen = sidebar.Screens.ScreenFromWindow(sidebar) ?? sidebar.Screens.Primary;
        Show();
        if (screen is null) return;
        double scale = screen.Scaling;
        var work = screen.WorkingArea;
        int w = (int)(Width * scale), h = (int)(Bounds.Height * scale), gap = (int)(16 * scale);
        int sideWidth = (int)(sidebar.Bounds.Width * scale);
        int x = right ? work.Right - sideWidth - w - gap : work.X + sideWidth + gap;
        Position = new PixelPoint(x, work.Bottom - h - gap);
    }
}
