using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Lumen.App;
using Lumen.Core;
using Xunit;

public class EnlargedValueBoundsTests
{
    public static IEnumerable<object[]> Designs() => Settings.Selectable.Select(style => new object[] {style});

    [Theory, MemberData(nameof(Designs))]
    public void Enlarged_readings_stay_inside_the_card(WidgetStyle style) => UiHost.Run(() =>
    {
        var (snapshot,history) = Render.Demo(); L.Use("de");
        snapshot = snapshot with { Network = snapshot.Network! with { DownloadBytesPerSecond = 999_900_000, UploadBytesPerSecond = 123_400_000 } };
        var problems = new List<string>();
        foreach (double width in new[] {280d,360d})
        foreach (double scale in new[] {1.5d,2d})
        foreach (var kind in new[] {WidgetKind.Cpu,WidgetKind.Gpu,WidgetKind.Memory,WidgetKind.Drives,WidgetKind.Network})
        {
            var s = Settings.Starter(); s.Language = "de"; s.Width = width;
            var slot = s.Widgets.Single(w => w.Kind == kind); slot.Style = style; slot.WidgetScale = scale;
            var card = Cards.Create(kind,style,0,Palette.For(true,false),Metrics.For(Density.Fit),s);
            var window = new Window { Width = width, Height = 2400, Content = new StackPanel { Children = {card} } };
            try
            {
                window.Show(); ((IWidget)card).Update(snapshot,history);
                for (int pass = 0; pass < 3; pass++) { Dispatcher.UIThread.RunJobs(); using var image = window.CaptureRenderedFrame(); }
                foreach (var text in card.GetLogicalDescendants().OfType<TextBlock>().Where(t => SensorDetails.Visible(t,card) && (t.FontFeatures != null && (t.Text ?? "").Any(char.IsDigit)
                    || (t.Text ?? "").Trim() is "°C" or "°F" or "%" or "W" or "GHz" or "MHz" or "KB/s" or "MB/s" or "GB/s" or "B/s" or "rpm")))
                {
                    if (text.Bounds.Width <= 0 || text.GetLogicalAncestors().OfType<ScrollViewer>().Any()) continue;
                    var a = text.TranslatePoint(default,card); var b = text.TranslatePoint(new Point(text.Bounds.Width,text.Bounds.Height),card);
                    if (a.HasValue && b.HasValue && (a.Value.X < -1.5 || b.Value.X > card.Bounds.Width + 1.5))
                        problems.Add($"{kind} {width}px ×{scale}: {text.Text}: {a.Value.X:0.0}–{b.Value.X:0.0}/{card.Bounds.Width:0.0}");
                }
            }
            finally { window.Close(); }
        }
        Assert.True(problems.Count == 0,style + "\n" + string.Join("\n",problems));
        return true;
    });

    [Theory]
    [InlineData(WidgetStyle.Dial)] [InlineData(WidgetStyle.RingPair)] [InlineData(WidgetStyle.Thermostat)]
    [InlineData(WidgetStyle.PurpleDash)] [InlineData(WidgetStyle.SciFi)]
    public void Enlarged_drive_gauges_never_overlap(WidgetStyle style) => UiHost.Run(() =>
    {
        var (snapshot,history) = Render.Demo(); var s = Settings.Starter(); s.Width = 280;
        s.Widgets.Single(w => w.Kind == WidgetKind.Drives).WidgetScale = 2;
        var card = Cards.Create(WidgetKind.Drives,style,0,Palette.For(true,false),Metrics.For(Density.Fit),s);
        var window = new Window { Width = 280, Height = 2000, Content = new StackPanel { Children = {card} } };
        try
        {
            window.Show(); ((IWidget)card).Update(snapshot,history); Dispatcher.UIThread.RunJobs(); using var frame = window.CaptureRenderedFrame();
            var cells = card.GetLogicalDescendants().OfType<DesignValueFaces>().Single().Children.Where(c => c.IsVisible).ToArray();
            Assert.True(cells.Length >= 2);
            for (int i = 0; i < cells.Length; i++) for (int j = i + 1; j < cells.Length; j++)
                Assert.False(cells[i].Bounds.Intersects(cells[j].Bounds.Deflate(.1)),style + ": gauge cells overlap");
        }
        finally { window.Close(); }
        return true;
    });
}
