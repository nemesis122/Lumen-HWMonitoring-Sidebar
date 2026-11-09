using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Lumen.App;
using Lumen.Core;
using Xunit;

public class RightWidgetsTests
{
    [Theory]
    [InlineData(WidgetKind.Cpu)]
    [InlineData(WidgetKind.Gpu)]
    public void All_supplementary_readings_are_in_the_right_tile(WidgetKind kind) => UiHost.Run(() =>
    {
        L.Use("en");
        var (snap, history) = Render.Demo();
        var settings = new Settings();
        var palette = Palette.For(true, false);
        var card = Assert.IsType<RightWidgetsTile>(Cards.Create(kind, WidgetStyle.PureWidgetsRight, 0, palette, Metrics.For(Density.Fit), settings));
        card.Update(snap, history);
        var expected = CompleteReadings.For(kind, 0, snap, history, settings.ForWidget(kind), palette)
            .Where(r => r.Key != WidgetSensors.PrimaryKey(kind) && r.Key != WidgetSensors.SecondaryKey(kind)).Select(r => r.Key).ToArray();
        if (kind == WidgetKind.Gpu && snap.Gpus.First().MemoryJunction.HasValue) expected = expected.Append("gpu.junction").ToArray();
        Assert.Equal(expected, card.RightKeys);
        var right = card.GetLogicalDescendants().OfType<Border>().Single(c => Equals(c.Tag, "right-widgets-values"));
        var left = card.GetLogicalDescendants().OfType<Border>().Single(c => Equals(c.Tag, "right-widgets-ring"));
        var readouts = card.GetLogicalDescendants().OfType<TextBlock>().Where(t => t.Tag is string tag && tag.StartsWith("right-reading:")).ToArray();
        Assert.Equal(expected.Length, readouts.Length);
        Assert.All(readouts, t => Assert.Contains(right, t.GetLogicalAncestors()));
        Assert.Contains(kind == WidgetKind.Cpu ? "cpu.fan" : "gpu.fan", card.RightKeys);
        Assert.Contains(kind == WidgetKind.Cpu ? "cpu.power" : "gpu.power", card.RightKeys);
        var window = new Window { Width = 375, Height = 1000, Content = card };
        window.Show();
        for (int i = 0; i < 3; i++) { Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame(); }
        var rightTop = right.TranslatePoint(default, card)!.Value.Y;
        var leftTop = left.TranslatePoint(default, card)!.Value.Y;
        Assert.Equal(leftTop, rightTop, 2);
        window.Close();
        return true;
    });

    [Fact]
    public void Independent_sensor_choices_and_stopped_fan_keep_their_meaning() => UiHost.Run(() =>
    {
        var (snap, history) = Render.Demo();
        snap = snap with { Cpu = snap.Cpu! with { FanRpm = 0 } };
        var settings = new Settings();
        settings.Widgets.Single(w => w.Kind == WidgetKind.Cpu).HiddenSensors.Add("cpu.voltage");
        var card = Assert.IsType<RightWidgetsTile>(Cards.Create(WidgetKind.Cpu, WidgetStyle.PureWidgetsRight, 0, Palette.For(true, false), Metrics.For(Density.Fit), settings));
        card.Update(snap, history);
        Assert.DoesNotContain("cpu.voltage", card.RightKeys);
        Assert.Contains("cpu.fan", card.RightKeys);
        var fan = card.GetLogicalDescendants().OfType<TextBlock>().Single(t => Equals(t.Tag, "right-reading:cpu.fan"));
        Assert.Equal(Format.Rpm(0), fan.Text);
        card.Update(snap with { Time = snap.Time.AddSeconds(1), Cpu = snap.Cpu with { FanRpm = 1130 } }, history);
        Assert.Same(fan, card.GetLogicalDescendants().OfType<TextBlock>().Single(t => Equals(t.Tag, "right-reading:cpu.fan")));
        return true;
    });
}
