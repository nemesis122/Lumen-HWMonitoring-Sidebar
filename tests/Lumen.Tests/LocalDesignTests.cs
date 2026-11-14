using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Lumen.App;
using Lumen.Core;
using Xunit;

public class LocalDesignTests
{
    public static IEnumerable<object[]> Designs() => LocalDesigns.Styles.Select(s => new object[] { s });
    private static readonly WidgetKind[] Kinds =
        { WidgetKind.Cpu, WidgetKind.Gpu, WidgetKind.Memory, WidgetKind.Drives, WidgetKind.Network, WidgetKind.Cooling, WidgetKind.Power };

    [Fact]
    public void All_thirteen_previous_prototypes_are_removed_from_selection()
    {
        var removed = new[] { WidgetStyle.Atelier, WidgetStyle.RailLocal, WidgetStyle.Observatory, WidgetStyle.Canvas,
            WidgetStyle.Ledger, WidgetStyle.Instrument, WidgetStyle.Matrix, WidgetStyle.Solace, WidgetStyle.Forma,
            WidgetStyle.Axis, WidgetStyle.Aurora, WidgetStyle.Flux, WidgetStyle.Halo,
            WidgetStyle.AioMainboardType,WidgetStyle.AioMainboardMatrix,WidgetStyle.AioMainboardGauges,WidgetStyle.AioMainboardStrips,
            WidgetStyle.AioPopConsole,WidgetStyle.AioChromatic,WidgetStyle.Aperture };
        Assert.Equal(82, Settings.Selectable.Count());
        Assert.All(removed, style => { Assert.DoesNotContain(style, Settings.Selectable); Assert.Equal(WidgetStyle.Standard, Settings.Successor(style)); });
    }

    [Theory]
    [MemberData(nameof(Designs))]
    public void Cpu_and_gpu_stay_compact_with_all_selected_readings(WidgetStyle style) => UiHost.Run(() =>
    {
        var (snapshot, history) = Render.Demo();
        foreach (var kind in new[] { WidgetKind.Cpu, WidgetKind.Gpu })
        {
            var card = Cards.Create(kind, style, 0, Palette.For(true, false), Metrics.For(Density.Fit), new Settings());
            ((IWidget)card).Update(snapshot, history);
            card.Measure(new Size(375, double.PositiveInfinity));
            Assert.True(card.DesiredSize.Height <= 280, $"{style} {kind}: {card.DesiredSize.Height:0}px");
        }
        return true;
    });

    [Theory]
    [MemberData(nameof(Designs))]
    public void Every_selected_reading_is_visible_in_both_themes_and_orientations(WidgetStyle style) => UiHost.Run(() =>
    {
        L.Use("de");
        var (snapshot, history) = Render.Demo();
        foreach (bool dark in new[] { true, false })
        foreach (var edge in new[] { Edge.Left, Edge.Right, Edge.Top, Edge.Bottom })
        foreach (var kind in Kinds)
        {
            var settings = new Settings { Language = "de", Edge = edge };
            var palette = Palette.For(dark, false);
            var card = Assert.IsType<LocalDesignTile>(Cards.Create(kind, style, 0, palette, Metrics.For(Density.Fit), settings));
            card.Update(snapshot, history);
            Assert.Equal(CompleteReadings.Body(kind, 0, snapshot, history, settings.ForWidget(kind), palette, style).Select(r => r.Key), card.RenderedKeys);
            var numbers = card.GetLogicalDescendants().OfType<TextBlock>().Where(t => t.Tag is string tag && tag.StartsWith("local-value:")).ToArray();
            Assert.Equal(card.RenderedKeys.Count, numbers.Length);
            Assert.All(numbers, t => Assert.False(string.IsNullOrWhiteSpace(t.Text)));
            // ScrollViewer templates need an attached visual root before their children have bounds.
            var window = new Window { Content = card, Width = 520, Height = 1200 };
            window.Show(); Dispatcher.UIThread.RunJobs();
            foreach (double width in new[] { 280d, 375d, 520d })
            {
                card.Measure(new Size(width, double.PositiveInfinity));
                card.Arrange(new Rect(0, 0, width, card.DesiredSize.Height));
                Assert.True(card.DesiredSize.Height > 0 && double.IsFinite(card.DesiredSize.Height));
                Assert.All(numbers, t => Assert.True(t.Bounds.Width > 0));
            }
            window.Close();
        }
        return true;
    });

    [Theory]
    [MemberData(nameof(Designs))]
    public void Sensor_filters_and_existing_size_controls_are_respected(WidgetStyle style) => UiHost.Run(() =>
    {
        var (snapshot, history) = Render.Demo();
        var settings = new Settings();
        var cpu = settings.Widgets.Single(w => w.Kind == WidgetKind.Cpu);
        cpu.HiddenSensors.AddRange(new[] { "cpu.power", "cpu.load.history", "cpu.temp.history" });
        cpu.ValueScale = 1.3;
        cpu.LabelScale = 1.2;
        var card = Assert.IsType<LocalDesignTile>(Cards.Create(WidgetKind.Cpu, style, 0, Palette.For(true, false), Metrics.For(Density.Fit), settings));
        card.Update(snapshot, history);
        Assert.DoesNotContain("cpu.power", card.RenderedKeys);
        Assert.All(card.GetLogicalDescendants().OfType<AreaChart>(), c => Assert.False(c.IsVisible));
        var before = card.GetLogicalDescendants().OfType<TextBlock>().Where(t => t.Tag is string tag && tag.StartsWith("local-value:")).ToArray();
        Assert.Contains(before, t => Math.Abs(t.FontSize - 14 * 1.3) < .01);
        card.Update(snapshot, history);
        var after = card.GetLogicalDescendants().OfType<TextBlock>().Where(t => t.Tag is string tag && tag.StartsWith("local-value:")).ToArray();
        Assert.Equal(before.Length, after.Length);
        for (int i = 0; i < before.Length; i++) Assert.Same(before[i], after[i]);
        return true;
    });

    [Theory]
    [MemberData(nameof(Designs))]
    public void Two_drives_fit_compactly_without_losing_selected_values(WidgetStyle style) => UiHost.Run(() =>
    {
        L.Use("de");
        var (snapshot, history) = Render.Demo();
        var settings = new Settings { Language = "de" };
        var card = Assert.IsType<LocalDesignTile>(Cards.Create(WidgetKind.Drives, style, 0, Palette.For(true, false), Metrics.For(Density.Fit), settings));
        card.Update(snapshot, history);
        card.Measure(new Size(375, double.PositiveInfinity));
        card.Arrange(new Rect(0, 0, 375, card.DesiredSize.Height));
        Assert.True(card.DesiredSize.Height <= 260, $"{style}: two drives need {card.DesiredSize.Height:0}px");
        Assert.Equal(CompleteReadings.Body(WidgetKind.Drives, 0, snapshot, history, settings.ForWidget(WidgetKind.Drives), Palette.For(true, false), style).Count, card.RenderedKeys.Count);
        var values = card.GetLogicalDescendants().OfType<TextBlock>().Where(t => t.Tag is string tag && tag.StartsWith("local-value:")).ToArray();
        Assert.Equal(card.RenderedKeys.Count, values.Length);
        Assert.All(values, t => Assert.True(t.Bounds.Height > 0));
        return true;
    });

    [Theory]
    [InlineData(WidgetStyle.Panorama)]
    [InlineData(WidgetStyle.SplitLens)]
    public void Temperature_and_load_histories_can_be_switched_independently(WidgetStyle style) => UiHost.Run(() =>
    {
        L.Use("en");
        var (snapshot, history) = Render.Demo();
        var settings = new Settings();
        settings.Widgets.Single(w => w.Kind == WidgetKind.Cpu).HiddenSensors.Remove("cpu.chart");
        settings.Widgets.Single(w => w.Kind == WidgetKind.Cpu).HiddenSensors.Remove("cpu.temp.history");
        settings.Widgets.Single(w => w.Kind == WidgetKind.Cpu).HiddenSensors.Add("cpu.load.history");
        var card = Cards.Create(WidgetKind.Cpu, style, 0, Palette.For(true, false), Metrics.For(Density.Fit), settings);
        ((IWidget)card).Update(snapshot, history);
        var plots = card.GetLogicalDescendants().OfType<AreaChart>().ToArray();
        Assert.Contains(plots, p => p.IsVisible && (ToolTip.GetTip(p)?.ToString() ?? "").StartsWith("Temp"));
        Assert.DoesNotContain(plots, p => p.IsVisible && (ToolTip.GetTip(p)?.ToString() ?? "").StartsWith("Load"));
        return true;
    });

    [Fact]
    public void Split_lens_surfaces_and_main_figures_have_equal_sizes() => UiHost.Run(() =>
    {
        var (snapshot, history) = Render.Demo();
        foreach (var kind in new[] { WidgetKind.Cpu, WidgetKind.Gpu, WidgetKind.Memory, WidgetKind.Network, WidgetKind.Drives })
        {
            var card = Cards.Create(kind, WidgetStyle.SplitLens, 0, Palette.For(true, false), Metrics.For(Density.Fit), new Settings());
            ((IWidget)card).Update(snapshot, history);
            card.Measure(new Size(375, double.PositiveInfinity));
            card.Arrange(new Rect(0, 0, 375, card.DesiredSize.Height));
            var pairs = card.GetLogicalDescendants().OfType<EqualPairPanel>().ToArray();
            Assert.NotEmpty(pairs);
            foreach (var pair in pairs.Where(p => p.Children.Count == 2))
            {
                Assert.Equal(pair.Children[0].Bounds.Width, pair.Children[1].Bounds.Width, 1);
                Assert.Equal(pair.Children[0].Bounds.Height, pair.Children[1].Bounds.Height, 1);
            }
            if (kind is WidgetKind.Cpu or WidgetKind.Gpu)
            {
                var texts = card.GetLogicalDescendants().OfType<TextBlock>().ToArray();
                var first = texts.Single(t => Equals(t.Tag, "local-value:" + WidgetSensors.PrimaryKey(kind)));
                var second = texts.Single(t => Equals(t.Tag, "local-value:" + WidgetSensors.SecondaryKey(kind)));
                Assert.Equal(first.FontSize, second.FontSize);
            }
        }
        return true;
    });

    [Fact]
    public void Signature_designs_render_missing_sensor_histories_without_inventing_values() => UiHost.Run(() =>
    {
        var (demo, _) = Render.Demo();
        var snapshot = demo with { Cpu = demo.Cpu! with { Temperature = null, Load = null } };
        var history = new History();
        for (int i = 0; i < 3; i++) history.Record(snapshot with { Time = snapshot.Time.AddSeconds(i) });
        foreach (var style in LocalDesigns.Signature)
        {
            var card = Cards.Create(WidgetKind.Cpu, style, 0, Palette.For(true, false), Metrics.For(Density.Fit), new Settings());
            ((IWidget)card).Update(snapshot, history);
            var window = new Window { Width = 375, Height = 500, Content = card };
            window.Show();
            try
            {
                Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame();
                var readings = card.GetLogicalDescendants().OfType<TextBlock>().Where(t => Equals(t.Tag, "local-value:cpu.temp") || Equals(t.Tag, "local-value:cpu.load")).ToArray();
                Assert.Equal(2, readings.Length);
                Assert.All(readings, t => Assert.Equal("—", t.Text));
            }
            finally { window.Close(); }
        }
        return true;
    });

    [Fact]
    public void Orbit_readout_and_label_stay_centered_for_one_to_three_digits() => UiHost.Run(() =>
    {
        L.Use("en");
        var (snapshot, history) = Render.Demo();
        var card = Cards.Create(WidgetKind.Cpu, WidgetStyle.Orbit, 0, Palette.For(true, false), Metrics.For(Density.Fit), new Settings());
        var window = new Window { Width = 375, Height = 500, Content = card };
        window.Show();
        try
        {
            int tick = 0;
            foreach (double load in new[] { 4d, 10d, 100d, 0d })
            {
                var changed = snapshot with { Time = snapshot.Time.AddSeconds(++tick), Cpu = snapshot.Cpu! with { Load = load } };
                history.Record(changed); ((IWidget)card).Update(changed, history);
                for (int i = 0; i < 3; i++) { Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame(); }
                var dial = card.GetLogicalDescendants().OfType<Control>().Single(c => c.GetType().Name == "CompactOrbit");
                var value = card.GetLogicalDescendants().OfType<TextBlock>().Single(t => Equals(t.Tag, "local-value:cpu.load"));
                var readout = (Control)value.Parent!;
                var label = ((StackPanel)readout.Parent!).Children.OfType<TextBlock>().Single();
                double center = dial.TranslatePoint(new Point(dial.Bounds.Width / 2, 0), card)!.Value.X;
                double numberCenter = readout.TranslatePoint(new Point(readout.Bounds.Width / 2, 0), card)!.Value.X;
                double digitCenter = value.TranslatePoint(new Point(value.Bounds.Width / 2, 0), card)!.Value.X;
                double labelCenter = label.TranslatePoint(new Point(label.Bounds.Width / 2, 0), card)!.Value.X;
                Assert.InRange(Math.Abs(center - numberCenter), 0, 1);
                Assert.InRange(Math.Abs(center - digitCenter), 0, 1);
                Assert.InRange(Math.Abs(center - labelCenter), 0, 1);
            }
        }
        finally { window.Close(); }
        return true;
    });

    [Theory]
    [MemberData(nameof(Designs))]
    public void Info_overview_and_interactive_widgets_keep_their_existing_renderers(WidgetStyle style) => UiHost.Run(() =>
    {
        var settings = new Settings();
        var palette = Palette.For(true, false);
        var metrics = Metrics.For(Density.Fit);
        foreach (var kind in new[] { WidgetKind.Clock, WidgetKind.Weather, WidgetKind.Media, WidgetKind.DriveHealth, WidgetKind.System })
            Assert.IsType<InfoTile>(Cards.Create(kind, style, 0, palette, metrics, settings));
        Assert.IsType<OverviewTile>(Cards.Create(WidgetKind.Overview, style, 0, palette, metrics, settings));
        Assert.IsType<NotesCard>(Cards.Create(WidgetKind.Notes, style, 0, palette, metrics, settings));
        Assert.IsType<TimerCard>(Cards.Create(WidgetKind.Timer, style, 0, palette, metrics, settings));
        return true;
    });
}
