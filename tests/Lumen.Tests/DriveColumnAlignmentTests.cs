using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Lumen.App;
using Lumen.Core;
using Xunit;

public class DriveColumnAlignmentTests
{
    public static IEnumerable<object[]> LocalStyles() => LocalDesigns.Styles.Select(s => new object[] { s });
    [Theory, MemberData(nameof(LocalStyles))]
    public void Local_designs_keep_drive_temperatures_side_by_side(WidgetStyle style) => UiHost.Run(() =>
    {
        var (snapshot, history) = Render.Demo();
        foreach (double width in new[] { 280d, 375d })
        {
            var card = Cards.Create(WidgetKind.Drives, style, 0, Palette.For(true, false), Metrics.For(Density.Fit), new Settings());
            var window = new Window { Content = card, Width = width, Height = 1200 };
            try
            {
                window.Show(); ((IWidget)card).Update(snapshot, history); Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame();
                var values = card.GetLogicalDescendants().OfType<TextBlock>().Where(t => SensorDetails.KeyOf(t).EndsWith(":temp") && SensorDetails.Visible(t, card)).ToArray();
                Assert.Equal(snapshot.Disks.Count, values.Length);
                var a = values[0].TranslatePoint(default, card)!.Value; var b = values[1].TranslatePoint(default, card)!.Value;
                Assert.InRange(Math.Abs(a.Y - b.Y), 0, 1); Assert.True(b.X > a.X);
            }
            finally { window.Close(); }
        }
        return true;
    });
    [Theory]
    [InlineData(WidgetStyle.SignalRows)]
    [InlineData(WidgetStyle.OpenPanel)]
    public void Drive_temperature_and_used_match_native_main_value_size_and_colours(WidgetStyle style) => UiHost.Run(() =>
    {
        var (snapshot, history) = Render.Demo();
        foreach (bool dark in new[] { true, false })
        foreach (double scale in new[] { 1d, 1.3 })
        {
            var settings = new Settings(); var palette = Palette.For(dark, false);
            settings.Widgets.Single(w => w.Kind == WidgetKind.Drives).HiddenSensors.Remove("drives.used");
            foreach (var slot in settings.Widgets) slot.ValueScale = slot.HeroScale = scale;
            var cpu = Cards.Create(WidgetKind.Cpu, style, 0, palette, Metrics.For(Density.Fit), settings);
            var drives = Cards.Create(WidgetKind.Drives, style, 0, palette, Metrics.For(Density.Fit), settings);
            ((IWidget)cpu).Update(snapshot, history); ((IWidget)drives).Update(snapshot, history);
            var prototype = cpu.GetLogicalDescendants().OfType<TextBlock>().Single(t => SensorDetails.KeyOf(t) == "cpu.temp");
            foreach (var value in drives.GetLogicalDescendants().OfType<TextBlock>().Where(t => SensorDetails.KeyOf(t).StartsWith("drive:") && (SensorDetails.KeyOf(t).EndsWith(":temp") || SensorDetails.KeyOf(t).EndsWith(":used"))))
            {
                Assert.Equal(prototype.FontSize, value.FontSize); Assert.Equal(prototype.FontWeight, value.FontWeight);
                Assert.Equal(SensorDetails.KeyOf(value).EndsWith(":temp") ? palette.Cool : palette.Calm, Assert.IsAssignableFrom<Avalonia.Media.ISolidColorBrush>(value.Foreground).Color);
                Assert.Equal(Avalonia.Media.TextAlignment.Right, value.TextAlignment);
            }
        }
        return true;
    });
    [Theory]
    [InlineData(WidgetStyle.Pill)]
    [InlineData(WidgetStyle.PillRow)]
    [InlineData(WidgetStyle.Segments)]
    public void Native_temperature_and_optional_bars_share_all_three_column_edges(WidgetStyle style) => UiHost.Run(() =>
    {
        var (snapshot, history) = Render.Demo();
        foreach (bool dark in new[] { true, false })
        foreach (double width in new[] { 280d, 375d })
        {
            var settings = new Settings(); var slot = settings.Widgets.Single(w => w.Kind == WidgetKind.Drives);
            var card = Cards.Create(WidgetKind.Drives, style, 0, Palette.For(dark, false), Metrics.For(Density.Fit), settings);
            var window = new Window { Content = card, Width = width, Height = 2200 };
            try
            {
                window.Show();
                foreach (bool all in new[] { false, true, false })
                {
                    slot.HiddenSensors = all ? new() : SensorDefaults.For(WidgetKind.Drives);
                    slot.HiddenSensors.Remove("drives.used");
                    ((IWidget)card).Update(snapshot, history);
                    for (int i = 0; i < 4; i++) { Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame(); }
                    var rows = card.GetLogicalDescendants().OfType<Grid>().Where(g => SensorDetails.Visible(g, card) && g.ColumnDefinitions.Count == 3
                        && g.ColumnDefinitions[0].SharedSizeGroup == "LumenName" && g.Children.Count == 3).ToArray();
                    Assert.True(rows.Length >= snapshot.Disks.Count * 2);
                    var edges = rows.Select(g => (Label: g.Children[0].TranslatePoint(default, card)!.Value.X,
                        Bar: g.Children[1].TranslatePoint(default, card)!.Value.X, End: g.Children[1].TranslatePoint(new Point(g.Children[1].Bounds.Width, 0), card)!.Value.X,
                        Value: g.Children[2].TranslatePoint(new Point(g.Children[2].Bounds.Width, 0), card)!.Value.X)).ToArray();
                    foreach (var edge in edges)
                    {
                        Assert.True(Math.Abs(edge.Label - edges[0].Label) <= 1, $"{style}/{width}/{all}: " + string.Join("; ", rows.Select(g => ((TextBlock)g.Children[0]).Text + "=" + g.Children[0].TranslatePoint(default, card)!.Value.X)));
                        Assert.InRange(Math.Abs(edge.Bar - edges[0].Bar), 0, 1);
                        Assert.InRange(Math.Abs(edge.End - edges[0].End), 0, 1);
                        Assert.InRange(Math.Abs(edge.Value - edges[0].Value), 0, 1);
                    }
                }
            }
            finally { window.Close(); }
        }
        return true;
    });
}
