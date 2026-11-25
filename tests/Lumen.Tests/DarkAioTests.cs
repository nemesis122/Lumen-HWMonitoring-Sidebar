using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Lumen.App;
using Lumen.Core;
using Xunit;

public class DarkAioTests
{
    [Fact]
    public void New_designs_keep_their_number_and_round_trip_via_normal_settings()
    {
        Assert.Equal(new[] { 62, 66, 80, 92, 93, 94, 95, 96, 97, 98, 99, 100 }, DarkAioDesigns.All.Select(d => d.Number));
        foreach (var design in DarkAioDesigns.All)
        {
            var settings = Settings.Starter();
            settings.Pages = 2;
            var overview = settings.Widgets.Single(w => w.Kind == WidgetKind.Overview);
            overview.Page = 2;
            overview.Column = 2;
            overview.Name = "My hardware";
            overview.ValueScale = 1.3;
            overview.Visible = true;
            DesignWidgetSelection.Apply(settings, design.Style, only: overview);
            var json = System.Text.Json.JsonSerializer.Serialize(settings, Settings.Json);
            var loaded = Settings.Normalize(System.Text.Json.JsonSerializer.Deserialize<Settings>(json, Settings.Json)!);
            var result = loaded.Widgets.Single(w => w.Kind == WidgetKind.Overview);
            Assert.Equal(design.Style, result.Style);
            Assert.Equal(2, result.Page);
            Assert.Equal(2, result.Column);
            Assert.Equal("My hardware", result.Name);
            Assert.Equal(1.3, result.ValueScale);
        }
    }

    [Fact]
    public void Titanium_rack_tracks_real_drives_and_their_sensor_switches() => UiHost.Run(() =>
    {
        var (snapshot, history) = Render.Demo();
        snapshot = snapshot with { Disks = new[]
        {
            new DiskInfo("disk-one", "First drive", null, 36, 260000, 127000) { Letters = "C:" },
            new DiskInfo("disk-two", "Second drive", 33, 51, 584, 0) { Letters = "E:" }
        } };
        var settings = Settings.Starter();
        settings.Glass = true;
        settings.Opacity = .45;
        var slot = settings.Widgets.Single(w => w.Kind == WidgetKind.Overview);
        slot.Style = WidgetStyle.AioTitaniumRack;
        AioDesigns.ShowAllParts(settings);
        var p = Palette.For(settings, true, null);
        var card = new AioTile(slot.Style, p, Metrics.For(Density.Fit), settings);
        card.Own(slot, 0);
        card.Update(snapshot, history);
        var rows = card.GetLogicalDescendants().OfType<Border>().Where(b => Equals(b.Tag, "dashboard-part:ssd")).ToList();
        Assert.Equal(3, rows.Count); // the section and one rack per physical drive
        Assert.All(rows.Skip(1), b => Assert.Equal(p.Card.A, Assert.IsAssignableFrom<ISolidColorBrush>(b.Background).Color.A));
        Assert.Contains(rows.SelectMany(b => b.GetLogicalDescendants()).OfType<TextBlock>(), t => t.Text == "C:");
        Assert.Contains(rows.SelectMany(b => b.GetLogicalDescendants()).OfType<TextBlock>(), t => t.Text == "E:");
        Assert.Contains("aio.drives.disk-one.read", SensorDetails.VisibleKeys(card));
        Assert.Contains("aio.drives.disk-two.write", SensorDetails.VisibleKeys(card));
        WidgetSensors.Select(settings, slot, "aio.drives.disk-two.write", false, snapshot);
        card.Update(snapshot, history);
        Assert.DoesNotContain("aio.drives.disk-two.write", SensorDetails.VisibleKeys(card));
        return true;
    });

    [Theory]
    [InlineData(WidgetStyle.AioNeonBackplate, "backplate")]
    [InlineData(WidgetStyle.AioAirflowMap, "airflow")]
    public void Scene_art_has_a_visible_live_sensor_in_the_same_grid(WidgetStyle style, string scene) => UiHost.Run(() =>
    {
        var (snapshot, history) = Render.Demo();
        var settings = Settings.Starter();
        settings.Glass = true;
        settings.Opacity = .4;
        var slot = settings.Widgets.Single(w => w.Kind == WidgetKind.Overview);
        slot.Style = style;
        var card = new AioTile(style, Palette.For(settings, true, null), Metrics.For(Density.Fit), settings);
        card.Own(slot, 0);
        foreach (double width in new[] { 280d, 393d, 640d })
        {
            card.Update(snapshot, history);
            card.Measure(new Avalonia.Size(width, double.PositiveInfinity));
            card.Arrange(new Avalonia.Rect(0, 0, width, card.DesiredSize.Height));
            var artwork = Assert.Single(card.GetLogicalDescendants().OfType<Control>(), item => item.Name == "dark-art-" + scene);
            Assert.True(artwork.Bounds.Width >= width * .19);
            Assert.True(artwork.Bounds.Height > 30);
            var grid = artwork.GetLogicalAncestors().OfType<Grid>().First(g => g.ColumnDefinitions.Count == 2);
            var beside = grid.Children.Where(c => c != artwork && !c.GetLogicalDescendants().Contains(artwork))
                .SelectMany(c => c.GetLogicalDescendants()).OfType<TextBlock>().Where(t => SensorDetails.KeyOf(t).Length > 0);
            var art = artwork.TranslatePoint(default, grid)!.Value;
            Assert.Contains(beside, value => value.TranslatePoint(default, grid) is { } point && Math.Abs(point.X - art.X) > 24);
        }
        return true;
    });
    [Theory]
    [InlineData(WidgetStyle.AioTitaniumRack)]
    [InlineData(WidgetStyle.AioNeonBackplate)]
    [InlineData(WidgetStyle.AioAirflowMap)]
    [InlineData(WidgetStyle.AioTitaniumRackOriginal)]
    [InlineData(WidgetStyle.AioNeonBackplateOriginal)]
    [InlineData(WidgetStyle.AioAirflowMapOriginal)]
    [InlineData(WidgetStyle.AioTitaniumRackB)]
    [InlineData(WidgetStyle.AioNeonBackplateB)]
    [InlineData(WidgetStyle.AioAirflowMapB)]
    [InlineData(WidgetStyle.AioTitaniumRackC)]
    [InlineData(WidgetStyle.AioNeonBackplateC)]
    [InlineData(WidgetStyle.AioAirflowMapC)]
    public void Only_disk_capacity_has_a_meter_and_the_footer_cards_align(WidgetStyle style) => UiHost.Run(() =>
    {
        // A PC with voltage rails: since 5.7.0 a PC without rail sensors (the demo) has no power card at all.
        var (snapshot, history) = HardwareVariants.For("big-rig");
        var s = Settings.Starter();
        AioDesigns.ShowAllParts(s);
        var overview = s.Widgets.Single(w => w.Kind == WidgetKind.Overview);
        overview.Style = style;
        var card = new AioTile(style, Palette.For(s, true, null), Metrics.For(Density.Fit), s);
        card.Own(overview, 0);
        card.Update(snapshot, history);
        card.Measure(new Size(326, double.PositiveInfinity));
        card.Arrange(new Rect(0, 0, 326, card.DesiredSize.Height));
        var children = card.GetLogicalDescendants().OfType<Control>().ToList();
        Assert.NotEmpty(children.OfType<AioMeter>());
        Assert.All(children.OfType<AioMeter>(), meter => Assert.Contains(meter.GetLogicalAncestors(),
            element => element is Border b && Equals(b.Tag, "dashboard-part:ssd")));
        Assert.True(Assert.Single(children.OfType<AioCoreBars>()).NumbersOnly);
        var power = Assert.Single(children.OfType<Border>().Where(b => Equals(b.Tag, "dashboard-part:power")));
        var net = Assert.Single(children.OfType<Border>().Where(b => Equals(b.Tag, "dashboard-part:net")));
        Assert.Equal(power.Bounds.Height, net.Bounds.Height, 1);
        return true;
    });


}

