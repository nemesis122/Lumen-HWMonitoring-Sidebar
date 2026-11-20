using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Lumen.App;
using Lumen.Core;
using Xunit;

/// <summary>Isometric Tiles: the CPU and GPU titles sit on one line at every graphic size (user report 4.5.6).</summary>
public class AioIsometricTilesAlignmentTests
{
    [Theory]
    [InlineData(WidgetStyle.AioIsometricTiles, 1.0, 360)] [InlineData(WidgetStyle.AioIsometricTilesRed, 1.0, 360)]
    [InlineData(WidgetStyle.AioIsometricTiles, 1.3, 360)] [InlineData(WidgetStyle.AioIsometricTilesRed, 1.3, 360)]
    [InlineData(WidgetStyle.AioIsometricTilesRed, 0.8, 360)] [InlineData(WidgetStyle.AioIsometricTilesRed, 1.0, 300)]
    [InlineData(WidgetStyle.AioIsometricTilesRed, 1.5, 440)]
    public void Cpu_and_gpu_titles_line_up(WidgetStyle style, double graphics, double width) => UiHost.Run(() =>
    {
        var s = Settings.Starter(); s.HiddenValues.Clear(); DesignWidgetSelection.Apply(s, style); AioDesigns.ShowAllParts(s);
        foreach (var w in s.Widgets) w.HiddenSensors.Clear();
        var overview = s.Widgets.Single(w => w.Kind == WidgetKind.Overview); overview.GraphicScale = graphics;
        var card = Cards.Create(WidgetKind.Overview, style, 0, Palette.For(true, false), Metrics.For(Density.Comfortable), s.ForSlot(overview));
        ((Lumen.App.INamed)card).Own(overview, 0);
        var (snap, hist) = Render.Demo();
        var window = new Window { Width = width, Height = 2400, Content = card, Background = Brushes.Black };
        try
        {
            window.Show();
            for (int i = 0; i < 3; i++) { ((IWidget)card).Update(snap, hist); Dispatcher.UIThread.RunJobs(); }
            double Top(string text) => card.GetLogicalDescendants().OfType<TextBlock>().First(t => t.Text == text).TranslatePoint(new Point(0, 0), card)!.Value.Y;
            double cpu = Top("CPU"), gpu = Top("GPU");
            if (Environment.GetEnvironmentVariable("LUMEN_APPEARANCE_OUTPUT") is { Length: > 0 } dir)
            { Directory.CreateDirectory(dir); using var f = window.CaptureRenderedFrame(); f!.Save(Path.Combine(dir, $"tiles-{style}-{graphics}-{width}.png")); }
            Assert.True(Math.Abs(cpu - gpu) < 1, $"CPU title at {cpu:0.0}, GPU title at {gpu:0.0}");
        }
        finally { window.Close(); }
        return true;
    });
}
