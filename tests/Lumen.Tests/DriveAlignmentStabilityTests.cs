using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Lumen.App;
using Lumen.Core;
using Xunit;

public class DriveAlignmentStabilityTests
{
    public static IEnumerable<object[]> Designs() => Settings.Selectable.Select(style => new object[] { style });

    [Fact]
    public void Segment_design_gives_every_additional_numeric_drive_value_its_native_segment_bar() => UiHost.Run(() =>
    {
        var (snap, history) = Render.Demo(); var settings = new Settings();
        settings.Widgets.Single(w => w.Kind == WidgetKind.Drives).HiddenSensors.Clear();
        var card = Cards.Create(WidgetKind.Drives, WidgetStyle.SegmentGraph, 0, Palette.For(true, false), Metrics.For(Density.Fit), settings);
        ((IWidget)card).Update(snap, history);
        var window = new Window { Content = card, Width = 375, Height = 1800 };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame();
            var values = card.GetLogicalDescendants().OfType<TextBlock>().Where(t => SensorDetails.KeyOf(t).StartsWith("drive:") && !SensorDetails.KeyOf(t).Contains(":model")
                && !SensorDetails.KeyOf(t).EndsWith(":read") && !SensorDetails.KeyOf(t).EndsWith(":write")).ToArray();
            Assert.NotEmpty(values);
            foreach (var value in values)
                Assert.Contains(value.GetLogicalAncestors().OfType<Grid>(), grid => grid.Children.OfType<Meter>().Any(meter => meter.Segments == 20));
            Assert.All(card.GetLogicalDescendants().OfType<SensorDetails>().SelectMany(d => d.GetLogicalDescendants()).OfType<TextBlock>()
                .Where(t => SensorDetails.KeyOf(t).EndsWith(":hours")), t => Assert.Contains(t.GetLogicalAncestors().OfType<Grid>(), g => g.Children.OfType<Meter>().Any()));
        }
        finally { window.Close(); }
        return true;
    });

    [Theory, MemberData(nameof(Designs))]
    public void Drive_updates_keep_widget_height_and_value_column_edges_still(WidgetStyle style) => UiHost.Run(() =>
    {
        L.Use("en"); var (demo, _) = Render.Demo();
        foreach (bool all in new[] { false, true })
        foreach (double width in new[] { 280d, 375d })
        {
            var settings = new Settings { SensorSelectionVersion = SensorDefaults.SelectionVersion };
            if (all) settings.Widgets.Single(w => w.Kind == WidgetKind.Drives).HiddenSensors.Clear();
            var card = Cards.Create(WidgetKind.Drives, style, 0, Palette.For(true, false), Metrics.For(Density.Fit), settings);
            var marker = new Border { Height = 2 };
            var host = new StackPanel { Children = { card, marker } };
            var window = new Window { Content = host, Width = width, Height = 2000 };
            var history = new History(); var hold = new SensorHold();
            var anchors = new Dictionary<string, double>(); double? height = null, next = null;
            try
            {
                window.Show();
                int tick = 0;
                foreach (double rate in new[] { 0d, 999d, 1024d, 999_999d, 13_000_000_000d, 0d })
                {
                    var snapshot = hold.Apply(demo with { Time = demo.Time.AddSeconds(tick++), Disks = demo.Disks.Select(d => d with
                    { Temperature = tick == 4 ? null : tick % 2 == 0 ? 100 : 9, ReadBytesPerSecond = rate, WriteBytesPerSecond = rate / 2,
                      UsedPercent = tick % 2 == 0 ? 100 : 1, PowerOnHours = tick % 2 == 0 ? 999999 : 1, PowerOnCount = tick % 2 == 0 ? 99999 : 1,
                      ReadActivity = tick % 2 == 0 ? 100 : 0, WriteActivity = tick % 2 == 0 ? 100 : 0, TotalActivity = tick % 2 == 0 ? 100 : 0 } ).ToArray() });
                    history.Record(snapshot); ((IWidget)card).Update(snapshot, history);
                    for (int pass = 0; pass < 3; pass++) { Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame(); }
                    height ??= card.Bounds.Height; next ??= marker.Bounds.Y;
                    Assert.True(Math.Abs(card.Bounds.Height - height.Value) <= 1, $"{style}/{width}/{all}: drive height jumped {height:0.0} -> {card.Bounds.Height:0.0}");
                    Assert.True(Math.Abs(marker.Bounds.Y - next.Value) <= 1, $"{style}/{width}/{all}: next widget moved");
                    foreach (var value in card.GetLogicalDescendants().OfType<TextBlock>().Where(t => SensorDetails.Visible(t, card) && t.FontFeatures != null))
                    {
                        string key = SensorDetails.KeyOf(value);
                        if (key.Length == 0 || key.EndsWith(":model")) continue;
                        // Right-aligned detail values keep their right edge; large left-aligned
                        // figures keep their start, and ring values keep their centre.
                        double anchor = value.HorizontalAlignment == HorizontalAlignment.Center ? value.Bounds.Width / 2
                            : value.TextAlignment == Avalonia.Media.TextAlignment.Right ? value.Bounds.Width : 0;
                        var edge = value.TranslatePoint(new Point(anchor, 0), card)!.Value.X;
                        if (anchors.TryGetValue(key, out double previous))
                            Assert.True(Math.Abs(edge - previous) <= 1, $"{style}/{width}/{all}: {key} right edge moved {previous:0.0} -> {edge:0.0}");
                        else anchors[key] = edge;
                    }
                }
            }
            finally { window.Close(); }
        }
        return true;
    }, seconds: 120);
}
