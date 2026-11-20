using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Lumen.App;
using Lumen.Core;
using Xunit;

/// <summary>
/// The design picker shows a small real preview of every design (demonstration readings), stored as
/// Assets/Thumbs/&lt;Style&gt;.png. Set LUMEN_WRITE_THUMBS=1 to render them again after a design changed.
/// </summary>
public class DesignThumbnailTests
{
    internal const int Width = 192, Height = 112;
    private static string Folder => Path.Combine(FindRoot(), "src", "Lumen.App", "Assets", "Thumbs");
    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Directory.Build.props"))) return dir.FullName;
        throw new DirectoryNotFoundException("repository root");
    }

    [Fact]
    public void Every_selectable_design_has_a_preview() => UiHost.Run(() =>
    {
        if (Environment.GetEnvironmentVariable("LUMEN_WRITE_THUMBS") == "1") WriteAll();
        var missing = Settings.Selectable.Where(s => !File.Exists(Path.Combine(Folder, s + ".png"))).ToArray();
        Assert.True(missing.Length == 0, "Missing previews: " + string.Join(", ", missing));
        return true;
    });

    [Fact]
    public void Design_names_are_distinct_and_have_no_number() => UiHost.Run(() =>
    {
        var names = Settings.Selectable.Select(SettingsWindow.StyleName).ToArray();
        Assert.Equal(names.Length, names.Distinct().Count());
        Assert.DoesNotContain(names, n => System.Text.RegularExpressions.Regex.IsMatch(n, @"^\d+ ·"));
        return true;
    });

    private static void WriteAll()
    {
        Directory.CreateDirectory(Folder);
        foreach (var style in Settings.Selectable) Write(style);
    }

    private static void Write(WidgetStyle style)
    {
        var s = Settings.Starter(); s.HiddenValues.Clear(); DesignWidgetSelection.Apply(s, style);
        bool aio = AioDesigns.Contains(style);
        if (aio) AioDesigns.ShowAllParts(s);
        var kind = aio ? WidgetKind.Overview : WidgetKind.Cpu;
        var slot = s.Widgets.FirstOrDefault(w => w.Kind == kind) ?? new WidgetSlot { Kind = kind, Style = style };
        slot.Style = style;
        double width = 320;
        var card = Cards.Create(kind, style, 0, Palette.For(true, false), Metrics.For(Density.Comfortable), s.ForSlot(slot));
        if (card is Lumen.App.INamed named) named.Own(slot, 0);
        var (snap, hist) = Render.Demo();
        var window = new Window { Width = width, Height = 900, Content = new Border { Background = Brushes.Black, Child = card, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top }, Background = Brushes.Black };
        try
        {
            window.Show();
            for (int i = 0; i < 3; i++) { ((IWidget)card).Update(snap, hist); TextColours.Apply(card, s); Dispatcher.UIThread.RunJobs(); }
            using var frame = window.CaptureRenderedFrame()!;
            // The top of the design (where it shows the CPU), scaled into the preview.
            double cropHeight = width * Height / Width;
            var target = new RenderTargetBitmap(new Avalonia.PixelSize(Width, Height));
            using (var dc = target.CreateDrawingContext())
            {
                dc.FillRectangle(Brushes.Black, new Avalonia.Rect(0, 0, Width, Height));
                dc.DrawImage(frame, new Avalonia.Rect(0, 0, width, cropHeight), new Avalonia.Rect(0, 0, Width, Height));
            }
            target.Save(Path.Combine(Folder, style + ".png"));
        }
        finally { window.Close(); }
    }
}
