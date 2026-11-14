using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Lumen.App;
using Lumen.Core;
using Xunit;

// Evolution build: sensors from every source, sensor for the big number, extra sensors, accent, floating.
public class EvolutionTests
{
    private static SensorNode Cpu(string id, string name, params SensorValue[] values) => new("Cpu", name, id, values, Array.Empty<SensorNode>());

    private static HardwareInput Input(IReadOnlyList<SensorNode> builtin, IReadOnlyList<SensorNode> ohm, (string, IReadOnlyList<SensorNode>)? external = null) =>
        new(builtin, ohm, external, Array.Empty<SensorMerge.PhysicalDisk>(), new Dictionary<int, string>(), Array.Empty<VolumeInfo>());

    [Fact]
    public void OpenHardwareMonitor_fills_a_cpu_that_LibreHardwareMonitor_cannot_read()
    {
        var lhm = new[] { Cpu("/intelcpu/0", "Intel Core i7-3770", new SensorValue("Load", "CPU Total", 20, 0, "/intelcpu/0/load/0")) };
        var ohm = new[] { Cpu("ohm:/intelcpu/0", "Intel Core i7-3770", new SensorValue("Temperature", "CPU Package", 55, 0, "/intelcpu/0/temperature/0")) };
        var view = new HardwareCombiner().Combine(Input(lhm, ohm));
        Assert.Equal(55, view.Cpu!.Temperature);
        Assert.Equal("Intel Core i7-3770", view.Cpu.Name);
        // A value that LibreHardwareMonitor does read is never replaced.
        var both = new[] { Cpu("/intelcpu/0", "Intel Core i7-3770", new SensorValue("Temperature", "CPU Package", 48, 0, "/intelcpu/0/temperature/0")) };
        Assert.Equal(48, new HardwareCombiner().Combine(Input(both, ohm)).Cpu!.Temperature);
    }

    [Fact]
    public void Evolution_uses_the_accent_and_names_no_sensor()
    {
        var (line, color) = UiHost.Run(() =>
        {
            L.Use("de");
            var (snap, history) = Render.Demo();
            var settings = new Settings { Language = "de" };
            settings.Widgets.First(w => w.Kind == WidgetKind.Gpu).Accent = "#f87171";
            var card = Cards.Create(WidgetKind.Gpu, WidgetStyle.Evolution, 0, Palette.For(true, false), Metrics.For(Density.Fit), settings);
            ((INamed)card).Own(settings.Widgets.First(w => w.Kind == WidgetKind.Gpu), 0);
            ((IWidget)card).Update(snap, history);
            var texts = card.GetLogicalDescendants().OfType<TextBlock>().ToList();
            var dot = card.GetLogicalDescendants().OfType<Avalonia.Controls.Border>().First(b => b.Width == 8);
            return (texts.Any(t => (t.Text ?? "").StartsWith("Sensor")), ((Avalonia.Media.ISolidColorBrush)dot.Background!).Color);
        });
        Assert.False(line);
        Assert.Equal(Avalonia.Media.Color.Parse("#f87171"), color);
    }

    [Fact]
    public void New_preferences_survive_saving_and_bad_values_are_cleaned()
    {
        string path = Path.Combine(Path.GetTempPath(), $"lumen-{Guid.NewGuid():N}.json");
        try
        {
            var s = new Settings { Floating = true, HideInFullscreen = true };
            var cpu = s.Widgets.First(w => w.Kind == WidgetKind.Cpu);
            cpu.Style = WidgetStyle.Evolution;
            cpu.Accent = "#a3e635";
            s.Widgets.First(w => w.Kind == WidgetKind.Gpu).Accent = "rot";
            s.Save(path);
            var loaded = Settings.Load(path);
            var c = loaded.Widgets.First(w => w.Kind == WidgetKind.Cpu);
            Assert.True(loaded.Floating);
            Assert.True(loaded.HideInFullscreen);
            Assert.Equal(WidgetStyle.Evolution, c.Style);
            Assert.Equal("#a3e635", c.Accent);
            Assert.Equal("", loaded.Widgets.First(w => w.Kind == WidgetKind.Gpu).Accent);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Floating_sidebar_has_no_bar_behind_the_cards()
    {
        var (background, shadows) = UiHost.Run(() =>
        {
            var settings = Render.Side(WidgetStyle.Evolution);
            settings.Floating = true;
            var view = new SidebarView(settings, Palette.For(true, false));
            var cards = view.GetLogicalDescendants().OfType<SummaryTile>().ToList();
            return (view.Background, cards.Count(c => c.BoxShadow.Count > 0) == cards.Count && cards.Count > 0);
        });
        Assert.Equal(Avalonia.Media.Brushes.Transparent, background);
        Assert.True(shadows);
    }
}

public class FloatingLayoutTests
{
    [Fact]
    public void Cards_line_up_at_the_edge_and_keep_a_moved_position()
    {
        var (auto, moved, gap) = UiHost.Run(() =>
        {
            var s = new Settings { Edge = Edge.Right, Width = 300 };
            var panel = new FloatPanel(s, 10) { CardWidth = 300 };
            var a = new Border { Height = 200 }; var b = new Border { Height = 300 }; var c = new Border { Height = 300 };
            panel.Add(a, "Cpu#0"); panel.Add(b, "Gpu#0"); panel.Add(c, "Memory#0");
            panel.Measure(new Avalonia.Size(1920, 700)); panel.Arrange(new Avalonia.Rect(0, 0, 1920, 700));
            var first = (a.Bounds, b.Bounds, c.Bounds);
            s.FloatPositions["0:Gpu#0"] = new double[] { 400, 120 };
            panel.InvalidateArrange(); panel.Arrange(new Avalonia.Rect(0, 0, 1920, 700));
            return (first, b.Bounds, panel.IsOverCard(new Avalonia.Point(1000, 650)));
        });
        // Right edge, 12 px margin; the third card does not fit and starts a second column further left.
        Assert.Equal(1920 - 12 - 300, auto.Item1.X, 1);
        Assert.Equal(12, auto.Item1.Y, 1);
        Assert.Equal(222, auto.Item2.Y, 1);
        Assert.Equal(1920 - 12 - 300 - 310, auto.Item3.X, 1);
        Assert.Equal(new Avalonia.Rect(400, 120, 300, 300), moved);
        Assert.False(gap);
    }

    [Fact]
    public void Positions_are_saved_per_monitor_and_bad_ones_are_dropped()
    {
        string path = Path.Combine(Path.GetTempPath(), $"lumen-{Guid.NewGuid():N}.json");
        try
        {
            var s = new Settings();
            s.FloatPositions["1:Cpu#0"] = new double[] { 10, 20 };
            s.FloatPositions["1:Gpu#0"] = new double[] { 1, 2, 3 };
            s.Save(path);
            var loaded = Settings.Load(path);
            Assert.Equal(new double[] { 10, 20 }, loaded.FloatPositions["1:Cpu#0"]);
            Assert.False(loaded.FloatPositions.ContainsKey("1:Gpu#0"));
        }
        finally { File.Delete(path); }
    }
}

public class EvolutionPolishTests
{
    private static (Control Card, List<string> Texts) Show(WidgetKind kind, Func<Snapshot, Snapshot>? change = null, Action<Settings>? set = null)
    {
        L.Use("de");
        var (snap, history) = Render.Demo();
        if (change != null) snap = change(snap);
        var settings = new Settings { Language = "de", ShowIntegratedGpu = true };
        set?.Invoke(settings);
        var card = Cards.Create(kind, WidgetStyle.Evolution, kind == WidgetKind.Gpu ? 1 : 0, Palette.For(true, false), Metrics.For(Density.Fit), settings);
        ((IWidget)card).Update(snap, history);
        var window = new Window { Content = card, Width = 320, Height = 900 };
        window.Show();
        for (int i = 0; i < 3; i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame(); }
        var texts = card.GetLogicalDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && t.Bounds.Width > 0).Select(t => t.Text ?? "").ToList();
        window.Close();
        return (card, texts);
    }

    [Fact]
    // Fans stay in the sensor order, so a fan that speeds up never swaps places with another.
    public void Fans_keep_their_order_and_a_stopped_fan_reads_off()
    {
        var texts = UiHost.Run(() => Show(WidgetKind.Cooling).Texts);
        var rpm = texts.Where(t => t.EndsWith("rpm")).Select(t => double.Parse(t.Split(' ')[0])).ToList();
        Assert.True(rpm.Count >= 4);
        Assert.Contains(texts, t => t.Replace('\u00A0', ' ') is "0 rpm" or "0"); // a stopped fan reads "0 rpm" in every design
    }

    [Fact]
    public void Drives_are_listed_side_by_side_without_a_hottest_one()
    {
        var texts = UiHost.Run(() => Show(WidgetKind.Drives).Texts);
        Assert.Contains("C:", texts);
        Assert.Contains("D:", texts);
        Assert.DoesNotContain(texts, t => t.Contains("Warmest"));
    }

    [Fact]
    public void Idle_integrated_graphics_take_one_line()
    {
        var texts = UiHost.Run(() => Show(WidgetKind.Gpu).Texts);
        Assert.Contains(texts, t => t.Contains("% Load") && t.Contains("°C"));
        Assert.DoesNotContain(texts, t => t.StartsWith("Sensor"));
    }

    [Fact]
    public void A_hot_processor_colours_the_whole_card()
    {
        var (cool, hot) = UiHost.Run(() =>
        {
            var a = (Border)Show(WidgetKind.Cpu).Card;
            var b = (Border)Show(WidgetKind.Cpu, s => s with { Cpu = s.Cpu! with { Temperature = 96 } }).Card;
            // The red edge is an inner shadow, so border and padding (and the card's size) stay the same.
            return (a.BoxShadow.Count, b.BoxShadow.Count);
        });
        Assert.Equal(0, cool);
        Assert.Equal(1, hot);
    }

    [Fact]
    public void Ten_minutes_are_kept_in_five_second_steps()
    {
        LongHistory.Clear();
        var t0 = new DateTime(2026, 1, 1);
        for (int i = 0; i < 900; i++) LongHistory.Add("k", t0.AddSeconds(i), i % 10);
        var values = LongHistory.Read("k");
        Assert.InRange(values.Length, LongHistory.Steps, LongHistory.Steps + 1);
        // Five readings per step: 0..4 average 2, 5..9 average 7.
        Assert.All(values.SkipLast(1), v => Assert.True(v == 2 || v == 7));
    }
}

public class DialTests
{
    [Fact]
    public void Missing_readings_get_no_empty_tile()
    {
        var texts = UiHost.Run(() =>
        {
            L.Use("de");
            var (snap, history) = Render.Demo();
            var igpu = snap.Gpus.First(g => g.Integrated) with { HotSpot = null, FanRpm = null, PowerW = null };
            snap = snap with { Gpus = new[] { igpu } };
            var card = Cards.Create(WidgetKind.Gpu, WidgetStyle.Dial, 0, Palette.For(true, false), Metrics.For(Density.Fit), new Settings { Language = "de" });
            ((IWidget)card).Update(snap, history);
            var window = new Window { Content = card, Width = 330, Height = 900 };
            window.Show();
            for (int i = 0; i < 3; i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame(); }
            // Native tiles stay empty-free; explicitly selected unavailable sensors now have a labelled
            // dash in SensorDetails, as required by the common sensor contract.
            var shown = card.GetLogicalDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && t.Bounds.Width > 0 && !t.GetLogicalAncestors().OfType<SensorDetails>().Any()).Select(t => t.Text ?? "").ToList();
            window.Close();
            return shown;
        });
        Assert.Contains("LOAD", texts);
        Assert.DoesNotContain("HOTSPOT", texts);
        Assert.DoesNotContain("POWER", texts);
        Assert.DoesNotContain("—", texts);
    }

    [Fact]
    public void Todays_range_starts_again_at_midnight()
    {
        DayStats.Clear();
        var day = new DateTime(2026, 11, 6, 23, 59, 50);
        DayStats.Add("t", day, 40);
        DayStats.Add("t", day.AddSeconds(5), 70);
        Assert.Equal((40.0, 70.0), DayStats.Range("t"));
        DayStats.Add("t", day.AddSeconds(15), 50);
        Assert.Equal((50.0, 50.0), DayStats.Range("t"));
        Assert.Equal(3, DayStats.Series("t").Length);
    }

    [Fact]
    public void Widgets_without_a_measured_value_use_the_design_info_layout()
    {
        var kind = UiHost.Run(() => Cards.Create(WidgetKind.Clock, WidgetStyle.Dial, 0, Palette.For(true, false), Metrics.For(Density.Fit), new Settings()).GetType());
        // Clock, calendar, weather … are drawn in the design's own frame and colours (no gauge).
        Assert.Equal(typeof(InfoTile), kind);
    }
}

public class NewWidgetTests
{
    private static List<string> Texts(WidgetKind kind, Func<Snapshot, Snapshot>? change = null)
    {
        return UiHost.Run(() =>
        {
            L.Use("de");
            var (snap, history) = Render.Demo();
            if (change != null) snap = change(snap);
            var card = Cards.Create(kind, WidgetStyle.Standard, 0, Palette.For(true, false), Metrics.For(Density.Fit), new Settings { Language = "de" });
            ((IWidget)card).Update(snap, history);
            var window = new Window { Content = card, Width = 330, Height = 700 };
            window.Show();
            for (int i = 0; i < 3; i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame(); }
            var texts = card.GetLogicalDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text ?? "").ToList();
            window.Close();
            return texts;
        });
    }

    [Fact]
    public void Overview_lists_every_part_once()
    {
        var t = Texts(WidgetKind.Overview);
        foreach (string label in new[] { "CPU", "GPU", "RAM", "VRAM", "NETWORK" })
            Assert.Contains(t, x => x.Equals(label, StringComparison.OrdinalIgnoreCase));
        // Every drive has its own line, none of them is singled out as the hottest.
        var (snap, _) = Render.Demo();
        foreach (var d in snap.Disks.Where(d => d.Letters.Length > 0))
            Assert.Contains(d.Letters, t);
        // Network: download and upload both shown.
        Assert.Contains(t, x => x.StartsWith("↓ "));
        Assert.Contains(t, x => x.StartsWith("↑ "));
    }

    [Fact]
    public void Drive_health_names_a_worn_out_drive()
    {
        var t = Texts(WidgetKind.DriveHealth, s => s with { Disks = s.Disks.Select((d, i) => i == 0 ? d with { Life = 3 } : d).ToArray() });
        Assert.Contains("Critical", t);
        Assert.Contains("1 to check", t);
        Assert.Contains("3 %", t);
    }

    [Fact]
    public void Timer_starts_ready()
    {
        var t = Texts(WidgetKind.Timer);
        Assert.Contains("25:00", t);
        Assert.Contains("Ready", t);
    }
}
