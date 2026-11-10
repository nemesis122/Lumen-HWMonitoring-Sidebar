using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Lumen.App;
using Lumen.Core;
using Xunit;

public class FavoriteDesignTests
{
    public static IEnumerable<object[]> Designs() => FavoriteTile.Styles.Select(s => new object[] { s });

    [Theory]
    [MemberData(nameof(Designs))]
    public void Complete_readings_survive_design_choice_in_both_themes(WidgetStyle style) => UiHost.Run(() =>
    {
        var (snap, history) = Render.Demo();
        foreach (bool dark in new[] { true, false })
        foreach (var kind in new[] { WidgetKind.Cpu, WidgetKind.Gpu, WidgetKind.Memory, WidgetKind.Drives, WidgetKind.Network })
        {
            var settings = new Settings();
            var palette = Palette.For(dark, false);
            var card = Assert.IsType<FavoriteTile>(Cards.Create(kind, style, 0, palette, Metrics.For(Density.Fit), settings));
            card.Update(snap, history);
            var expected = CompleteReadings.Body(kind, 0, snap, history, settings.ForWidget(kind), palette, style).Select(r => r.Key).ToArray();
            Assert.Equal(expected, card.RenderedKeys);
            var rendered = card.GetLogicalDescendants().OfType<TextBlock>()
                .Where(t => t.Tag is string tag && tag.StartsWith("favorite-value:")).ToArray();
            Assert.Equal(expected.Length, rendered.Length);
            Assert.All(rendered, t => Assert.False(string.IsNullOrWhiteSpace(t.Text)));
            foreach (double width in new[] { 280d, 375d, 520d })
            {
                card.Measure(new Size(width, double.PositiveInfinity));
                Assert.True(double.IsFinite(card.DesiredSize.Height) && card.DesiredSize.Height > 0);
            }
        }
        return true;
    });

    [Theory]
    [MemberData(nameof(Designs))]
    public void Sensor_filters_hide_values_and_history_without_rebuilding_steady_readouts(WidgetStyle style) => UiHost.Run(() =>
    {
        var (snap, history) = Render.Demo();
        var settings = new Settings();
        var slot = settings.Widgets.Single(w => w.Kind == WidgetKind.Cpu);
        slot.HiddenSensors.Add("cpu.power");
        slot.HiddenSensors.Add("cpu.load.history");
        var card = Assert.IsType<FavoriteTile>(Cards.Create(WidgetKind.Cpu, style, 0, Palette.For(true, false), Metrics.For(Density.Fit), settings));
        card.Update(snap, history);
        Assert.DoesNotContain("cpu.power", card.RenderedKeys);
        Assert.All(card.GetLogicalDescendants().OfType<AreaChart>(), c => Assert.False(c.IsEffectivelyVisible));
        var before = card.GetLogicalDescendants().OfType<TextBlock>().Where(t => t.Tag is string tag && tag.StartsWith("favorite-value:")).ToArray();
        card.Update(snap, history);
        var after = card.GetLogicalDescendants().OfType<TextBlock>().Where(t => t.Tag is string tag && tag.StartsWith("favorite-value:")).ToArray();
        Assert.Equal(before.Length, after.Length);
        for (int i = 0; i < before.Length; i++) Assert.Same(before[i], after[i]);
        return true;
    });
}
