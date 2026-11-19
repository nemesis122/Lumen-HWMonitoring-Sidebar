using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Lumen.App;
using Lumen.Core;
using Xunit;

public class AioAtlasDriveTests
{
    [Fact]
    public void Each_requested_design_uses_its_drive_layout_with_the_correct_live_sensor_ids() => UiHost.Run(() =>
    {
        var (snapshot, history) = Render.Demo();
        snapshot = snapshot with
        {
            Disks = new[] { new DiskInfo("one", "Samsung NVMe", null, 36, 65_000, 0) { Letters = "C:" },
                new DiskInfo("two", "Backup SSD", 34, 57, 514, 125_000) { Letters = "E:" } }
        };
        var styles = Settings.Selectable.Where(AioDesigns.AtlasDriveLayout).ToArray();
        Assert.Equal(15, styles.Count(style => !AioDesigns.Red(style)));
        Assert.Equal(20, styles.Count(AioDesigns.CompactDriveLayout));
        Assert.DoesNotContain(WidgetStyle.AioPanel, styles);
        Assert.DoesNotContain(WidgetStyle.AioIsometricRingsRed, styles);
        foreach (var style in styles)
        {
            var settings = Settings.Starter();
            AioDesigns.ShowAllParts(settings);
            var slot = settings.Widgets.Single(w => w.Kind == WidgetKind.Overview);
            slot.Style = style;
            var card = new AioTile(style, Palette.For(true, false), Metrics.For(Density.Fit), settings);
            card.Own(slot, 0);
            card.Update(snapshot, history);

            var rows = card.GetLogicalDescendants().OfType<Border>().Where(b => b.Tag is string tag && tag.StartsWith("atlas-drive:")).ToArray();
            Assert.Equal(new[] { "atlas-drive:one", "atlas-drive:two" }, rows.Select(b => b.Tag));
            foreach (var (row, id, letter) in new[] { (rows[0], "one", "C:"), (rows[1], "two", "E:") })
            {
                var text = row.GetLogicalDescendants().OfType<TextBlock>().ToArray();
                Assert.Contains(text, t => t.Text == letter);
                var keys = new[] { "aio.system.disk." + id, $"aio.drives.{id}.read", $"aio.drives.{id}.write", $"aio.drives.{id}.used" };
                foreach (var key in keys.Where(settings.ForSlot(slot).Shows))
                {
                    Assert.Single(text, t => SensorDetails.KeyOf(t) == key);
                    Assert.Single(card.GetLogicalDescendants().OfType<TextBlock>(), t => SensorDetails.KeyOf(t) == key);
                }
                foreach (var key in keys.Where(key => !settings.ForSlot(slot).Shows(key)))
                    Assert.DoesNotContain(text, t => SensorDetails.KeyOf(t) == key);
                Assert.Single(row.GetLogicalDescendants().OfType<AioMeter>());
                if (AioDesigns.CompactDriveLayout(style) && AioDesigns.Red(style))
                    Assert.Single(row.GetLogicalDescendants().OfType<PanelArt>(), art => art.Kind == "ssd");
                else Assert.Single(row.GetLogicalDescendants().OfType<Image>());
            }

            WidgetSensors.Select(settings, slot, "aio.drives.two.used", false, snapshot);
            card.Update(snapshot, history);
            Assert.DoesNotContain("aio.drives.two.used", SensorDetails.VisibleKeys(card));
            card.Measure(new Size(260, double.PositiveInfinity));
            card.Arrange(new Rect(0, 0, 260, card.DesiredSize.Height));
            Assert.True(double.IsFinite(card.DesiredSize.Height));
            AssertNetworkBelowHardware(card, style);
            if (style is WidgetStyle.AioIsometricCompact or WidgetStyle.AioIsometricRig)
            {
                var tiles = card.GetLogicalDescendants().OfType<Border>().Where(b => b.Tag is string tag && tag.StartsWith("atlas-drive:")).ToArray();
                Assert.True(tiles[1].Bounds.Y > tiles[0].Bounds.Y);
                card.Measure(new Size(400, double.PositiveInfinity));
                card.Arrange(new Rect(0, 0, 400, card.DesiredSize.Height));
                if (style == WidgetStyle.AioIsometricCompact) Assert.True(tiles[1].Bounds.Y > tiles[0].Bounds.Y);
                else
                {
                    Assert.Equal(tiles[0].Bounds.Y, tiles[1].Bounds.Y);
                    Assert.Equal(tiles[0].Bounds.Height, tiles[1].Bounds.Height);
                }
            }
        }
        return true;
    }, seconds: 90);

    [Theory]
    [InlineData(WidgetStyle.AioBoardCard)]
    [InlineData(WidgetStyle.AioDoubleTable)]
    [InlineData(WidgetStyle.AioQuadrants)]
    [InlineData(WidgetStyle.AioNetworkCenter)]
    [InlineData(WidgetStyle.AioIsometricRings)]
    public void Restored_drive_layouts_keep_network_below_the_hardware(WidgetStyle style) => UiHost.Run(() =>
    {
        var (snapshot, history) = Render.Demo();
        var settings = Settings.Starter();
        AioDesigns.ShowAllParts(settings);
        var slot = settings.Widgets.Single(w => w.Kind == WidgetKind.Overview);
        slot.Style = style;
        var card = new AioTile(style, Palette.For(true, false), Metrics.For(Density.Fit), settings);
        card.Own(slot, 0);
        foreach (var (width, columns) in new[] { (280, 1), (880, 3) })
        {
            settings.Columns = columns;
            card.Update(snapshot, history);
            card.Measure(new Size(width, double.PositiveInfinity));
            card.Arrange(new Rect(0, 0, width, card.DesiredSize.Height));
            Assert.DoesNotContain(card.GetLogicalDescendants().OfType<Border>(), b => b.Tag is string tag && tag.StartsWith("atlas-drive:"));
            Assert.Contains("aio.system.disk." + snapshot.Disks.First().Id, SensorDetails.VisibleKeys(card));
            AssertNetworkBelowHardware(card, style);
        }
        return true;
    });

    [Fact]
    public void Rings_Red_uses_its_original_drive_instruments() => UiHost.Run(() =>
    {
        var (snapshot, history) = Render.Demo();
        var settings = Settings.Starter();
        AioDesigns.ShowAllParts(settings);
        var slot = settings.Widgets.Single(w => w.Kind == WidgetKind.Overview);
        slot.Style = WidgetStyle.AioIsometricRingsRed;
        var card = new AioTile(slot.Style, Palette.For(true, false), Metrics.For(Density.Fit), settings);
        card.Own(slot, 0);
        card.Update(snapshot, history);

        Assert.DoesNotContain(card.GetLogicalDescendants().OfType<Border>(), b => b.Tag is string tag && tag.StartsWith("atlas-drive:"));
        Assert.Contains("aio.system.disk." + snapshot.Disks.First().Id, SensorDetails.VisibleKeys(card));
        Assert.Contains(card.GetLogicalDescendants().OfType<AioRing>(), ring => ring.Height == 138);
        return true;
    });

    private static void AssertNetworkBelowHardware(AioTile card, WidgetStyle style)
    {
        var positions = card.GetLogicalDescendants().OfType<TextBlock>()
            .Where(t => SensorDetails.KeyOf(t).Length > 0)
            .Select(t => (Key: SensorDetails.KeyOf(t), Point: t.TranslatePoint(default, card), t.Bounds.Height))
            .Where(t => t.Point.HasValue).ToArray();
        var network = positions.Where(t => t.Key.StartsWith("aio.network.")).ToArray();
        Assert.NotEmpty(network);
        double firstNetwork = network.Min(t => t.Point!.Value.Y);
        double lastHardware = positions.Where(t => !t.Key.StartsWith("aio.network."))
            .Max(t => t.Point!.Value.Y + t.Height);
        Assert.True(firstNetwork >= lastHardware - 1, $"{style}: network at {firstNetwork}, hardware ends at {lastHardware}");
    }

    [Fact]
    public void Panel_uses_a_shared_storage_icon_for_multiple_drives() => UiHost.Run(() =>
    {
        var (snapshot, history) = Render.Demo();
        snapshot = snapshot with
        {
            Disks = new[] { new DiskInfo("one", "Samsung NVMe", null, 36, 65_000, 0) { Letters = "C:" },
                new DiskInfo("two", "Backup SSD", 34, 57, 514, 125_000) { Letters = "E:" } }
        };
        var settings = Settings.Starter();
        AioDesigns.ShowAllParts(settings);
        var slot = settings.Widgets.Single(w => w.Kind == WidgetKind.Overview);
        slot.Style = WidgetStyle.AioPanel;
        var card = new AioTile(slot.Style, Palette.For(true, false), Metrics.For(Density.Fit), settings);
        card.Own(slot, 0);
        card.Update(snapshot, history);

        Assert.Single(card.GetLogicalDescendants().OfType<PanelArt>(), art => art.Kind == "ssd");
        Assert.Contains("aio.drives.one.used", SensorDetails.VisibleKeys(card));
        Assert.Contains("aio.drives.two.used", SensorDetails.VisibleKeys(card));
        return true;
    });
}
