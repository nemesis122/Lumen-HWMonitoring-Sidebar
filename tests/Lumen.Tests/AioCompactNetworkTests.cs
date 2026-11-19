using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Lumen.App;
using Lumen.Core;
using Xunit;

public class AioCompactNetworkTests
{
    [Fact]
    public void Confirmed_designs_show_three_live_network_columns_at_the_bottom() => UiHost.Run(() =>
    {
        var (snapshot, history) = Render.Demo();
        var styles = Settings.Selectable.Where(AioDesigns.CompactNetworkLayout).ToArray();
        Assert.Equal(18, styles.Length);
        Assert.DoesNotContain(WidgetStyle.AioSignalRackRed, styles);
        Assert.DoesNotContain(WidgetStyle.AioIsometricCompactRed, styles);
        foreach (var style in styles)
        {
            var settings = Settings.Starter();
            AioDesigns.ShowAllParts(settings);
            var slot = settings.Widgets.Single(w => w.Kind == WidgetKind.Overview);
            slot.Style = style;
            var card = new AioTile(style, Palette.For(true, false), Metrics.For(Density.Fit), settings);
            card.Own(slot, 0);
            card.Update(snapshot, history);

            Grid Columns() => Assert.Single(card.GetLogicalDescendants().OfType<Grid>(), g => Equals(g.Tag, "compact-network-values"));
            TextBlock[] Values(Grid grid) => grid.GetLogicalDescendants().OfType<TextBlock>()
                .Where(t => SensorDetails.KeyOf(t).Length > 0).ToArray();
            var columns = Columns();
            Assert.Equal(3, columns.ColumnDefinitions.Count);
            var values = Values(columns);
            Assert.Equal(new[] { "aio.network.down", "aio.network.up", "aio.network.link" }, values.Select(SensorDetails.KeyOf));
            Assert.All(values, value => Assert.Single(card.GetLogicalDescendants().OfType<TextBlock>(), t => SensorDetails.KeyOf(t) == SensorDetails.KeyOf(value)));

            card.Measure(new Size(240, double.PositiveInfinity));
            card.Arrange(new Rect(0, 0, 240, card.DesiredSize.Height));
            var positions = values.Select(v => v.TranslatePoint(default, card)!.Value).ToArray();
            Assert.True(positions[0].X < positions[1].X && positions[1].X < positions[2].X);
            double lastHardware = card.GetLogicalDescendants().OfType<TextBlock>()
                .Where(t => SensorDetails.KeyOf(t) is { Length: > 0 } key && !key.StartsWith("aio.network."))
                .Max(t => t.TranslatePoint(new Point(0, t.Bounds.Height), card)!.Value.Y);
            Assert.All(positions, p => Assert.True(p.Y >= lastHardware - 1, style.ToString()));

            WidgetSensors.Select(settings, slot, "aio.network.up", false, snapshot);
            card.Update(snapshot, history);
            Assert.Equal(new[] { "aio.network.down", "aio.network.link" }, Values(Columns()).Select(SensorDetails.KeyOf));
            WidgetSensors.Select(settings, slot, "aio.network", false, snapshot);
            card.Update(snapshot, history);
            Assert.DoesNotContain(card.GetLogicalDescendants().OfType<Grid>(), g => Equals(g.Tag, "compact-network-values"));
        }
        return true;
    }, seconds: 90);
}
