using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lumen.App;
using Lumen.Core;
using Xunit;

public class DashboardTests
{
    private sealed class TestPlatform : Lumen.App.Platform
    {
        public override bool IsAdministrator => false;
        public override bool AutostartEnabled => false;
    }
    public static IEnumerable<object[]> Designs() => AioDesigns.DashboardStyles.Select(s => new object[] { s });
    public static IEnumerable<object[]> Layouts() => AioDesigns.DashboardStyles.SelectMany(s => new[] { 440d, 880d }.Select(w => new object[] { s, w }));

    static (Settings Settings, WidgetSlot Slot, AioTile Card, Snapshot Snapshot, History History) Create(WidgetStyle design, int columns = 1)
    {
        var (snapshot, history) = Render.Demo(); var settings = Settings.Starter();
        settings.Columns = columns;
        var slot = settings.Widgets.Single(w => w.Kind == WidgetKind.Overview); slot.Style = design; slot.Visible = true;
        AioDesigns.ShowAllParts(settings);
        var card = new AioTile(design, Palette.For(true, false), Metrics.For(Density.Fit), settings); card.Own(slot, 0);
        card.Update(snapshot, history);
        return (settings, slot, card, snapshot, history);
    }

    [Fact]
    public void Four_retained_dashboards_have_unique_persisted_ids_and_unnumbered_picker_names()
    {
        Assert.Equal(4, AioDesigns.DashboardStyles.Length);
        Assert.Equal(4, AioDesigns.DashboardStyles.Distinct().Count());
        Assert.DoesNotContain(WidgetStyle.AioMinMax, Settings.Selectable);
        Assert.Equal(WidgetStyle.CaseCyanGlass, Settings.Successor(WidgetStyle.AioMinMax));
        foreach (var design in AioDesigns.DashboardStyles)
        {
            Assert.Contains(design, Settings.Selectable); Assert.True(AioDesigns.Contains(design));
            Assert.DoesNotMatch(@"^\d", AioDesigns.Name(design));
            Assert.True((int)design > (int)WidgetStyle.AioBlackCopperRingsRed);
            var settings = Settings.Starter(); settings.Widgets[0].Style = design;
            var roundTrip = Settings.Normalize(JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(settings, Settings.Json), Settings.Json)!);
            Assert.Equal(design, roundTrip.Widgets[0].Style);
        }
    }

    [Theory]
    [InlineData(WidgetStyle.AioBoardCard)]
    public void Hardware_artwork_has_component_labels_and_no_illustration_footer(WidgetStyle design) => UiHost.Run(() =>
    {
        var (_, _, card, _, _) = Create(design, 2);
        var descendants = card.GetLogicalDescendants().ToArray();
        Assert.Equal(new[] { "CPU", "GPU", "RAM" }, Assert.Single(descendants.OfType<DashboardBoardPicture>()).ComponentLabels);
        Assert.DoesNotContain(descendants.OfType<TextBlock>(), t => (t.Text ?? "").Contains("Illustration", StringComparison.OrdinalIgnoreCase) || (t.Text ?? "").Contains("models may differ", StringComparison.OrdinalIgnoreCase));
        return true;
    });

    [Theory, MemberData(nameof(Designs))]
    public void Picking_a_design_preserves_all_existing_placement_settings(WidgetStyle design) => UiHost.Run(() =>
    {
        var settings = Settings.Starter(); settings.Width = 270; settings.Columns = 3; settings.FullScreen = true;
        settings.Density = Density.Fit; settings.Pages = 2; settings.PageNames = new List<string> { "HW", "INFO" };
        var window = new SettingsWindow(settings, Palette.For(true, false), () => { }, () => new[] { "Demo" }, new TestPlatform());
        try
        {
            typeof(SettingsWindow).GetMethod("ApplyMainboardPlacement", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(window, new object[] { design, false });
            Assert.Equal(270, settings.Width); Assert.Equal(3, settings.Columns); Assert.True(settings.FullScreen);
            Assert.Equal(Density.Fit, settings.Density); Assert.Equal(2, settings.Pages); Assert.Equal(new[] { "HW", "INFO" }, settings.PageNames);
        }
        finally { window.Close(); }
        return true;
    });

    [Theory, MemberData(nameof(Designs))]
    public void Every_selected_sensor_has_one_binding_and_updates_without_rebuilding(WidgetStyle design) => UiHost.Run(() =>
    {
        var (s, slot, card, snapshot, h) = Create(design);
        var expected = AioReadings.For(snapshot, s.ForSlot(slot)).Where(r => r.Group != "Cores").Select(r => r.Key).Order().ToArray();
        Assert.Equal(expected, SensorDetails.VisibleKeys(card).Order().ToArray());
        var first = card.GetLogicalDescendants().OfType<TextBlock>().Single(t => SensorDetails.KeyOf(t) == "aio.cpu.temp");
        var newer = snapshot with { Cpu = snapshot.Cpu! with { Temperature = 91 } }; card.Update(newer, h);
        Assert.Contains("91", first.Text!);
        Assert.Same(first, card.GetLogicalDescendants().OfType<TextBlock>().Single(t => SensorDetails.KeyOf(t) == "aio.cpu.temp"));
        WidgetSensors.Select(s, slot, "aio.cpu", false, snapshot); card.Update(newer, h);
        Assert.DoesNotContain(SensorDetails.VisibleKeys(card), key => key.StartsWith("aio.cpu."));
        Assert.Contains("aio.system.ram.used", SensorDetails.VisibleKeys(card));
        WidgetSensors.Select(s, slot, "aio.system.ram.free", false, snapshot); card.Update(newer, h);
        Assert.DoesNotContain("aio.system.ram.free", SensorDetails.VisibleKeys(card));
        return true;
    });

    [Fact]
    public void Network_chart_obeys_graph_and_individual_sensor_switches() => UiHost.Run(() =>
    {
        var (s, slot, card, snapshot, h) = Create(WidgetStyle.AioNetworkCenter);
        Assert.Single(card.GetLogicalDescendants().OfType<DashboardTrafficPlot>());
        WidgetSensors.Select(s, slot, "aio.graphs", false, snapshot); card.Update(snapshot, h);
        Assert.Empty(card.GetLogicalDescendants().OfType<DashboardTrafficPlot>());
        Assert.Contains("aio.network.down", SensorDetails.VisibleKeys(card));
        WidgetSensors.Select(s, slot, "aio.network", false, snapshot); card.Update(snapshot, h);
        Assert.DoesNotContain(SensorDetails.VisibleKeys(card), k => k.StartsWith("aio.network."));
        return true;
    });

    [Theory, MemberData(nameof(Layouts))]
    public void Layouts_render_at_real_sidebar_width_and_reflow_when_narrow(WidgetStyle design, double width) => UiHost.Run(() =>
    {
        var (_, _, card, _, _) = Create(design, width >= 600 ? 2 : 1);
        var root = new Border { Background = Brush.Parse("#090E14"), Padding = new Thickness(8), Child = card };
        root.Measure(new Size(width, double.PositiveInfinity));
        var window = new Window { Width = width, Height = Math.Ceiling(root.DesiredSize.Height), Content = root };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs(); using var image = window.CaptureRenderedFrame();
            Assert.NotNull(image); Assert.Equal((int)width, image!.PixelSize.Width);
            foreach (var text in card.GetLogicalDescendants().OfType<TextBlock>().Where(t => SensorDetails.Visible(t, card)))
            {
                var point = text.TranslatePoint(default, card); if (point == null) continue;
                Assert.True(point.Value.X >= -1 && point.Value.X + text.Bounds.Width <= card.Bounds.Width + 2,
                    $"{design}: {text.Text} outside {card.Bounds.Width} at {point.Value.X}+{text.Bounds.Width}");
            }
            foreach (var pair in card.GetLogicalDescendants().OfType<DashboardColumns>())
                Assert.Equal(width < 600 ? 1 : 2, pair.ColumnDefinitions.Count);
            var path = Environment.GetEnvironmentVariable("LUMEN_DASHBOARD_OUTPUT");
            if (!string.IsNullOrEmpty(path) && (width == 880) == AioDesigns.DashboardWide(design))
            { Directory.CreateDirectory(path); image.Save(System.IO.Path.Combine(path, design + ".png")); }
        }
        finally { window.Close(); }
        return true;
    });

    [Theory]
    [InlineData(Density.Fit)]
    [InlineData(Density.Compact)]
    public void Sidebar_uses_two_column_width_and_respects_the_existing_fit_or_scroll_setting(Density density) => UiHost.Run(() =>
    {
        var settings = Settings.Starter(); settings.Width = 440; settings.Columns = 2; settings.Density = density;
        DesignWidgetSelection.Apply(settings, WidgetStyle.AioDoubleTable);
        Assert.Equal(880 + Metrics.For(settings.Density).Gap, SidebarView.WindowWidth(settings));
        var (snap, h) = Render.Demo();
        var sidebar = new SidebarView(settings, Palette.For(true, false)); sidebar.Update(snap, h);
        var window = new Window { Content = sidebar, Width = 880, Height = 800 };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs(); using var frame = window.CaptureRenderedFrame();
            var card = Assert.Single(sidebar.GetLogicalDescendants().OfType<AioTile>());
            Assert.True(card.Bounds.Width > 800);
            var scrolling = sidebar.GetLogicalDescendants().OfType<ScrollViewer>().Where(s => s.Content is FitPanel).ToArray();
            if (density == Density.Fit) Assert.Empty(scrolling); else Assert.Single(scrolling);
            Assert.Contains(card.GetLogicalDescendants().OfType<DashboardColumns>(), p => p.ColumnDefinitions.Count == 2);
            var transform = card.GetLogicalAncestors().OfType<StackPanel>().Select(p => p.RenderTransform).OfType<ScaleTransform>().FirstOrDefault();
            if (density != Density.Fit && transform != null) Assert.True(transform.ScaleX >= .99);
        }
        finally { window.Close(); }
        return true;
    });
}
