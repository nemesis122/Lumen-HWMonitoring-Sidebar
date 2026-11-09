using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Lumen.App;
using Lumen.Core;
using Xunit;

public class PureAlternativeTests
{
    public static IEnumerable<object[]> Designs() => PureLoadIndicator.Styles.Select(s => new object[] { s });

    [Theory]
    [MemberData(nameof(Designs))]
    public void Load_is_exact_and_readout_stays_stable(WidgetStyle style) => UiHost.Run(() =>
    {
        var indicator = new PureLoadIndicator(style, Palette.For(true, false));
        var texts = indicator.GetLogicalDescendants().OfType<TextBlock>().ToArray();
        foreach (double load in new[] { 0d, .01, .06, .5, 1, 2, -.1 })
        {
            indicator.Set(load, Avalonia.Media.Colors.Cyan, Avalonia.Media.Colors.Gray);
            Assert.Equal(Math.Clamp(load, 0, 1), indicator.Fraction);
            Assert.Equal(Format.Percent(Math.Clamp(load, 0, 1) * 100), indicator.ValueText);
        }
        foreach (double? missing in new double?[] { null, double.NaN, double.PositiveInfinity })
        {
            indicator.Set(missing, Avalonia.Media.Colors.Cyan, Avalonia.Media.Colors.Gray);
            Assert.Null(indicator.Fraction);
            Assert.Equal("—", indicator.ValueText);
        }
        var after = indicator.GetLogicalDescendants().OfType<TextBlock>().ToArray();
        for (int i = 0; i < texts.Length; i++) Assert.Same(texts[i], after[i]);
        return true;
    });

    [Theory]
    [MemberData(nameof(Designs))]
    public void Cpu_and_gpu_use_load_alternative_without_a_ring(WidgetStyle style) => UiHost.Run(() =>
    {
        var (snap, history) = Render.Demo();
        foreach (bool dark in new[] { true, false })
        foreach (var kind in new[] { WidgetKind.Cpu, WidgetKind.Gpu })
        {
            var card = Cards.Create(kind, style, 0, Palette.For(dark, false), Metrics.For(Density.Fit), new Settings());
            ((IWidget)card).Update(snap, history);
            Assert.IsType<PureTile>(card);
            Assert.Single(card.GetLogicalDescendants().OfType<PureLoadIndicator>());
            Assert.Empty(card.GetLogicalDescendants().OfType<AppleRing>());
            foreach (double width in new[] { 280d, 375d, 520d })
            {
                card.Measure(new Size(width, double.PositiveInfinity));
                Assert.True(double.IsFinite(card.DesiredSize.Height) && card.DesiredSize.Height > 0);
            }
        }
        // The original Pure keeps its existing ring.
        var original = Cards.Create(WidgetKind.Cpu, WidgetStyle.Pure, 0, Palette.For(true, false), Metrics.For(Density.Fit), new Settings());
        Assert.Single(original.GetLogicalDescendants().OfType<AppleRing>());
        return true;
    });
}
