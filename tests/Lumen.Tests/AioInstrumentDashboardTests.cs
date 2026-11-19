using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Lumen.App;
using Lumen.Core;
using Xunit;

public class AioInstrumentDashboardTests
{
    [Theory]
    [InlineData(WidgetStyle.AioTealInstruments)]
    [InlineData(WidgetStyle.AioAmberInstruments)]
    public void Instruments_use_live_values_and_respect_sensor_selection(WidgetStyle style) => UiHost.Run(() =>
    {
        var (snapshot, history) = Render.Demo();
        snapshot = snapshot with { Fps = new FpsInfo("Demo", 117, 8.55, null), RefreshHz = 144 };
        var settings = Settings.Starter();
        AioDesigns.ShowAllParts(settings);
        var slot = settings.Widgets.Single(w => w.Kind == WidgetKind.Overview);
        slot.Style = style;
        slot.GraphicScale = 1.25;
        slot.HeroScale = 1.25;
        settings.Fahrenheit = true;
        var card = new AioTile(style, Palette.For(settings, true, null), Metrics.For(Density.Fit), settings);
        card.Own(slot, 0);
        card.Update(snapshot, history);

        TextBlock Reading(string key) => Assert.Single(card.GetLogicalDescendants().OfType<TextBlock>(), t => SensorDetails.KeyOf(t) == key);
        Assert.DoesNotContain("aio.performance.fps", SensorDetails.VisibleKeys(card));
        Assert.DoesNotContain("aio.performance.refresh", SensorDetails.VisibleKeys(card));
        WidgetSensors.Select(settings, slot, "aio.performance.fps", true, snapshot);
        WidgetSensors.Select(settings, slot, "aio.performance.refresh", true, snapshot);
        card.Update(snapshot, history);
        Assert.Equal("117", Reading("aio.performance.fps").Text);
        Assert.Equal("144", Reading("aio.performance.refresh").Text);
        Assert.Equal(Format.Temperature(snapshot.Cpu!.Temperature, true), Reading("aio.cpu.temp").Text);
        settings.ValueFont = "orbitron";
        slot.SensorColours["aio.cpu.temp"] = new() { ValueColor = "#35E6D0", AutomaticWarningColors = false };
        TextColours.Apply(card, settings);
        Assert.Equal(Ui.FamilyFor("orbitron"), Reading("aio.cpu.temp").FontFamily);
        Assert.Equal(Color.Parse("#35E6D0"), Assert.IsAssignableFrom<ISolidColorBrush>(Reading("aio.cpu.temp").Foreground).Color);

        var disk = snapshot.Disks.First();
        string rate = "aio.drives." + disk.Id + ".read";
        Assert.DoesNotContain(rate, SensorDetails.VisibleKeys(card));
        WidgetSensors.Select(settings, slot, rate, true, snapshot);
        WidgetSensors.Select(settings, slot, "aio.cpu.load", false, snapshot);
        WidgetSensors.Select(settings, slot, "aio.graphs", false, snapshot);
        card.Update(snapshot with { Fps = null }, history);
        Assert.Equal("—", Reading("aio.performance.fps").Text);
        Assert.Contains(rate, SensorDetails.VisibleKeys(card));
        Assert.DoesNotContain("aio.cpu.load", SensorDetails.VisibleKeys(card));
        Assert.DoesNotContain(card.GetLogicalDescendants(), c => c.GetType().Name == "InstrumentHistory");

        foreach (int width in new[] { 240, 346, 640 })
        {
            card.Measure(new Size(width, double.PositiveInfinity));
            card.Arrange(new Rect(0, 0, width, card.DesiredSize.Height));
            Assert.Equal(width, card.Bounds.Width);
            Assert.All(card.GetLogicalDescendants().OfType<Border>().Where(b => b.Tag is string s && s.StartsWith("dashboard-part:")),
                b => Assert.InRange(b.Bounds.Width, 1, width));
        }
        return true;
    });
}
