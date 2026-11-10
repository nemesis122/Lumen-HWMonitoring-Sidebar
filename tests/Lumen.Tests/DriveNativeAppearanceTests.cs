using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Lumen.App;
using Lumen.Core;
using Xunit;

public class DriveNativeAppearanceTests
{
    [Theory]
    [InlineData(WidgetStyle.NeonTiles)]
    [InlineData(WidgetStyle.LevelColumns)]
    [InlineData(WidgetStyle.SmartPanel)]
    public void Native_tiles_and_columns_render_optional_metrics_even_without_temperature(WidgetStyle style) => UiHost.Run(() =>
    {
        var (snap, history) = Render.Demo(); var settings = new Settings();
        var slot = settings.Widgets.Single(w => w.Kind == WidgetKind.Drives);
        slot.HiddenSensors.Clear(); slot.HiddenSensors.Add("drives.temp");
        var card = Cards.Create(WidgetKind.Drives, style, 0, Palette.For(true, false), Metrics.For(Density.Fit), settings);
        ((IWidget)card).Update(snap, history);
        var key = "drive:" + snap.Disks.First().Id + ":hours";
        var values = card.GetLogicalDescendants().OfType<Control>()
            .Where(c => SensorDetails.KeyOf(c).Split('|').Contains(key) && SensorDetails.Visible(c, card)).ToArray();
        Assert.NotEmpty(values);
        Assert.All(values, value => Assert.DoesNotContain(value.GetLogicalAncestors(), a => a is SensorDetails));
        return true;
    });

    public static IEnumerable<object[]> NativeRows() =>
        new[] { WidgetStyle.Segments, WidgetStyle.Pill, WidgetStyle.PillRow, WidgetStyle.Dial,
            WidgetStyle.HistoryGraph, WidgetStyle.RingPair, WidgetStyle.Thermostat, WidgetStyle.SciFi, WidgetStyle.Pure }
        .SelectMany(style => new[] { true, false }.Select(dark => new object[] { style, dark }));

    [Theory, MemberData(nameof(NativeRows))]
    public void Additional_drive_readings_use_the_same_native_bar_factory(WidgetStyle style, bool dark) => UiHost.Run(() =>
    {
        var (snap, history) = Render.Demo(); var settings = new Settings();
        settings.Widgets.Single(w => w.Kind == WidgetKind.Drives).HiddenSensors.Clear();
        var palette = Palette.For(dark, false);
        var card = Cards.Create(WidgetKind.Drives, style, 0, palette, Metrics.For(Density.Fit), settings);
        ((IWidget)card).Update(snap, history);
        var window = new Window { Content = card, Width = 375, Height = 1800 };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame();
            var native = card.GetLogicalDescendants().OfType<DriveRows>().Single();
            Control[] Bars(Control root) => root.GetLogicalDescendants().OfType<Control>()
                .Where(c => c is Meter or FillBack or RingBar or GradientBar or SegmentBar).ToArray();
            var original = Bars(native).First();
            var extras = card.GetLogicalDescendants().OfType<SensorDetails>().SelectMany(Bars).ToArray();
            Assert.NotEmpty(extras);
            foreach (var extra in extras)
            {
                Assert.Equal(original.GetType(), extra.GetType());
                Assert.Equal(original.Height, extra.Height);
                if (original is FillBack fill && extra is FillBack next)
                { Assert.Equal(fill.Radius, next.Radius); Assert.Equal(fill.Track, next.Track); }
                if (original is Meter meter && extra is Meter other)
                    Assert.Equal(meter.Segments, other.Segments);
            }
        }
        finally { window.Close(); }
        return true;
    });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Capsule_graph_uses_native_capsules_for_optional_drive_values(bool dark) => UiHost.Run(() =>
    {
        var (snap, history) = Render.Demo(); var settings = new Settings();
        settings.Widgets.Single(w => w.Kind == WidgetKind.Drives).HiddenSensors.Clear();
        var palette = Palette.For(dark, false);
        var card = Cards.Create(WidgetKind.Drives, WidgetStyle.PillRowGraph, 0, palette, Metrics.For(Density.Fit), settings);
        ((IWidget)card).Update(snap, history);
        var window = new Window { Content = card, Width = 375, Height = 1800 };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame();
            var key = "drive:" + snap.Disks.First().Id + ":used";
            var value = card.GetLogicalDescendants().OfType<TextBlock>().Single(t => SensorDetails.KeyOf(t) == key && SensorDetails.Visible(t, card));
            Assert.DoesNotContain(value.GetLogicalAncestors(), a => a is SensorDetails);
            var capsule = value.GetLogicalAncestors().OfType<Panel>().First(p => p.Children.OfType<FillBack>().Any());
            var fill = capsule.Children.OfType<FillBack>().Single();
            Assert.Equal(capsule.Height / 2, fill.Radius);
            Assert.Equal(Palette.Alpha(palette.Cool, dark ? .34 : .28), fill.Fill);
            Assert.Equal(Palette.Mix(palette.Card, palette.Ink, dark ? .06 : .05), fill.Track);
            Assert.True(capsule.Height >= 38);
        }
        finally { window.Close(); }
        return true;
    });
}
