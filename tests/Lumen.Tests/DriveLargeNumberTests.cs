using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Lumen.App;
using Lumen.Core;
using Xunit;

public class DriveLargeNumberTests
{
    [Fact]
    public void Fresh_install_starts_standard_and_a_saved_choice_survives_restart()
    {
        var fresh = Settings.Normalize(new Settings());
        Assert.All(fresh.Widgets, slot => Assert.Equal(WidgetStyle.Standard, slot.Style));
        foreach (var slot in fresh.Widgets.Where(w => Settings.HasStyles(w.Kind))) slot.Style = WidgetStyle.PureNumber;
        var loaded = Settings.Normalize(JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(fresh, Settings.Json), Settings.Json)!);
        Assert.All(loaded.Widgets.Where(w => Settings.HasStyles(w.Kind)), slot => Assert.Equal(WidgetStyle.PureNumber, slot.Style));
    }

    [Theory]
    [InlineData(WidgetStyle.CleanDark)]
    [InlineData(WidgetStyle.CompactPro)]
    [InlineData(WidgetStyle.PerformanceView)]
    [InlineData(WidgetStyle.PureBar)]
    [InlineData(WidgetStyle.PureNumber)]
    [InlineData(WidgetStyle.PureSegments)]
    public void Drive_temperatures_match_the_native_main_numbers_side_by_side_and_stay_still(WidgetStyle style) => UiHost.Run(() =>
    {
        var (snapshot, history) = Render.Demo();
        foreach (bool dark in new[] { true, false })
        foreach (double width in new[] { 280d, 375d })
        {
            var settings = new Settings(); var palette = Palette.For(dark, false);
            var cpu = Cards.Create(WidgetKind.Cpu, style, 0, palette, Metrics.For(Density.Fit), settings);
            ((IWidget)cpu).Update(snapshot, history);
            var prototype = cpu.GetLogicalDescendants().OfType<TextBlock>().Single(t => SensorDetails.KeyOf(t) == "cpu.temp");
            var card = Cards.Create(WidgetKind.Drives, style, 0, palette, Metrics.For(Density.Fit), settings);
            var window = new Window { Content = card, Width = width, Height = 1600 };
            double? height = null;
            try
            {
                window.Show();
                foreach (double temperature in new[] { 9d, 41d, 100d })
                {
                    ((IWidget)card).Update(snapshot with { Disks = snapshot.Disks.Select(d => d with { Temperature = temperature }).ToArray() }, history);
                    Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame();
                    var values = card.GetLogicalDescendants().OfType<TextBlock>().Where(t => SensorDetails.Visible(t, card) && SensorDetails.KeyOf(t).EndsWith(":temp")).ToArray();
                    Assert.Equal(snapshot.Disks.Count, values.Length);
                    Assert.All(values, value => Assert.Equal(prototype.FontSize, value.FontSize));
                    Assert.InRange(Math.Abs(values[0].TranslatePoint(default, card)!.Value.Y - values[1].TranslatePoint(default, card)!.Value.Y), 0, 1);
                    Assert.True(values[1].TranslatePoint(default, card)!.Value.X > values[0].TranslatePoint(default, card)!.Value.X);
                    if (height.HasValue) Assert.InRange(Math.Abs(card.Bounds.Height - height.Value), 0, 1);
                    height ??= card.Bounds.Height;
                }
            }
            finally { window.Close(); }
        }
        return true;
    });
}
