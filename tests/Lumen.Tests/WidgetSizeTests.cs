using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Lumen.App;
using Lumen.Core;
using Xunit;

// "Widget size" makes the whole widget larger or smaller (values, labels and graphics together) in every
// design; "Value size" and "Label size" keep working on their own.
public class WidgetSizeTests
{
    public static IEnumerable<object[]> Cases() =>
        Settings.Selectable.SelectMany(s => new[] { WidgetKind.Cpu, WidgetKind.Network, WidgetKind.Drives, WidgetKind.Overview }.Select(k => new object[] { s, k }));

    private static (double Value, double Label) Fonts(WidgetStyle style, WidgetKind kind, Action<WidgetSlot> size) => UiHost.Run(() =>
    {
        L.Use("de");
        var (snap, history) = Render.Demo();
        var settings = new Settings { Language = "de" };
        size(settings.Widgets.Single(w => w.Kind == kind));
        var card = Cards.Create(kind, style, 0, Palette.For(true, false), Metrics.For(Density.Fit), settings);
        ((IWidget)card).Update(snap, history);
        var window = new Window { Content = card, Width = 340, Height = 1400 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var blocks = card.GetLogicalDescendants().OfType<TextBlock>().Where(t => !string.IsNullOrEmpty(t.Text)).ToList();
        double value = blocks.Where(t => t.FontFeatures != null).Select(t => t.FontSize).DefaultIfEmpty(0).Max();
        double label = blocks.Where(t => t.FontFeatures == null).Select(t => t.FontSize).DefaultIfEmpty(0).Max();
        window.Close();
        return (value, label);
    });

    [Theory]
    [MemberData(nameof(Cases))]
    public void Widget_size_scales_values_and_labels(WidgetStyle style, WidgetKind kind)
    {
        var normal = Fonts(style, kind, _ => { });
        var large = Fonts(style, kind, w => w.WidgetScale = 1.5);
        var small = Fonts(style, kind, w => w.WidgetScale = 0.6);
        Assert.Equal(normal.Value * 1.5, large.Value, 1);
        Assert.Equal(normal.Value * 0.6, small.Value, 1);
        if (normal.Label > 0) Assert.Equal(normal.Label * 1.5, large.Label, 1);
    }

    [Fact]
    public void Widget_size_is_saved_and_kept_in_range()
    {
        string path = Path.Combine(Path.GetTempPath(), "lumen-size-" + Guid.NewGuid() + ".json");
        try
        {
            var s = new Settings();
            s.Widgets[0].WidgetScale = 1.4;
            s.Widgets[1].WidgetScale = 9;
            s.Save(path);
            var back = Settings.Load(path);
            Assert.Equal(1.4, back.Widgets[0].WidgetScale, 3);
            Assert.Equal(2.0, back.Widgets[1].WidgetScale, 3);
        }
        finally { File.Delete(path); }
    }
}

// English widgets say "Temp" (every design); German keeps "Temperatur".
public class ShortTempTests
{
    public static IEnumerable<object[]> Styles() => Settings.Selectable.Select(s => new object[] { s });

    [Theory]
    [MemberData(nameof(Styles))]
    public void English_widgets_say_Temp(WidgetStyle style)
    {
        var texts = UiHost.Run(() =>
        {
            L.Use("en");
            var (snap, history) = Render.Demo();
            var all = new List<string>();
            foreach (var kind in new[] { WidgetKind.Cpu, WidgetKind.Gpu, WidgetKind.Drives, WidgetKind.Overview })
            {
                var card = Cards.Create(kind, style, 0, Palette.For(true, false), Metrics.For(Density.Fit), new Settings { Language = "en" });
                ((IWidget)card).Update(snap, history);
                var window = new Window { Content = card, Width = 340, Height = 1000 };
                window.Show();
                Dispatcher.UIThread.RunJobs();
                ((IWidget)card).Update(snap, history);
                all.AddRange(card.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? ""));
                window.Close();
            }
            return all;
        });
        Assert.DoesNotContain(texts, t => t.Contains("emperature", StringComparison.OrdinalIgnoreCase));
    }
}

// "Main value size" changes only the big number; "Value size" only the smaller values in the list.
public class MainValueSizeTests
{
    private static (double Big, double Small) Sizes(WidgetStyle style, Action<WidgetSlot> size) => UiHost.Run(() =>
    {
        L.Use("de");
        var (snap, history) = Render.Demo();
        var settings = new Settings { Language = "de" };
        size(settings.Widgets.Single(w => w.Kind == WidgetKind.Cpu));
        var card = Cards.Create(WidgetKind.Cpu, style, 0, Palette.For(true, false), Metrics.For(Density.Fit), settings);
        ((IWidget)card).Update(snap, history);
        var numbers = card.GetLogicalDescendants().OfType<TextBlock>().Where(t => t.FontFeatures != null && !string.IsNullOrEmpty(t.Text)).Select(t => t.FontSize).ToList();
        return (numbers.Max(), numbers.Min());
    });

    [Theory]
    [InlineData(WidgetStyle.SciFi)]
    [InlineData(WidgetStyle.Thermostat)]
    [InlineData(WidgetStyle.Dial)]
    [InlineData(WidgetStyle.Standard)]
    public void Main_value_and_list_values_size_separately(WidgetStyle style)
    {
        var normal = Sizes(style, _ => { });
        var hero = Sizes(style, w => w.HeroScale = 1.5);
        var list = Sizes(style, w => w.ValueScale = 1.5);
        Assert.Equal(normal.Big * 1.5, hero.Big, 1);
        Assert.Equal(normal.Small, hero.Small, 1);
        Assert.Equal(normal.Big, list.Big, 1);
        Assert.Equal(normal.Small * 1.5, list.Small, 1);
    }
}

// Segment graph: every value of the widget is a segment line, and the sensor checkboxes still decide which.
public class SegmentGraphTests
{
    private static List<string> Lines(WidgetKind kind, Action<WidgetSlot> change) => UiHost.Run(() =>
    {
        L.Use("de");
        var (snap, history) = Render.Demo();
        var settings = new Settings { Language = "de" };
        change(settings.Widgets.Single(w => w.Kind == kind));
        var card = Cards.Create(kind, WidgetStyle.SegmentGraph, 0, Palette.For(true, false), Metrics.For(Density.Fit), settings);
        var window = new Window { Content = card, Width = 320, Height = 1000 };
        window.Show();
        for (int i = 0; i < 3; i++) { ((IWidget)card).Update(snap, history); Dispatcher.UIThread.RunJobs(); }
        var texts = card.GetLogicalDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && !string.IsNullOrEmpty(t.Text)).Select(t => t.Text!).ToList();
        int meters = card.GetLogicalDescendants().OfType<Meter>().Count(m => m.IsEffectivelyVisible);
        window.Close();
        texts.Add("meters=" + meters);
        return texts;
    });

    [Fact]
    public void Every_value_has_a_segment_line_and_hidden_ones_stay_away()
    {
        var all = Lines(WidgetKind.Cpu, _ => { });
        Assert.Contains("Clock", all);
        Assert.Contains("Volt", all);
        int meters = int.Parse(all.Single(t => t.StartsWith("meters=")).Split('=')[1]);
        Assert.True(meters >= 5, string.Join(" | ", all));
        var options = WidgetSensors.Options(WidgetKind.Cpu, Render.Demo().Item1).ToList();
        string voltage = options.First(o => o.Label.Contains("Voltage")).Key;
        var fewer = Lines(WidgetKind.Cpu, w => w.HiddenSensors.Add(voltage));
        Assert.DoesNotContain("Volt", fewer);
        Assert.Contains("Clock", fewer);
    }
}

// First start: register 1 "HW" (processor, graphics, memory, drives, network), register 2 "INFO" (clock,
// world clocks, calendar, weather), everything else hidden.
public class StarterLayoutTests
{
    [Fact]
    public void First_start_has_HW_and_INFO()
    {
        string path = Path.Combine(Path.GetTempPath(), "lumen-first-" + Guid.NewGuid() + ".json");
        var s = Settings.Load(path);
        Assert.Equal(2, s.Pages);
        Assert.Equal(new[] { "HW", "INFO" }, s.PageNames.Take(2));
        var hw = s.Widgets.Where(w => w.Visible && s.EffectivePage(w) == 1).Select(w => w.Kind).ToArray();
        var info = s.Widgets.Where(w => w.Visible && s.EffectivePage(w) == 2).Select(w => w.Kind).ToArray();
        Assert.Equal(new[] { WidgetKind.Cpu, WidgetKind.Gpu, WidgetKind.Memory, WidgetKind.Drives, WidgetKind.Network }, hw);
        Assert.Equal(new[] { WidgetKind.Clock, WidgetKind.WorldClocks, WidgetKind.Calendar, WidgetKind.Weather }, info);
        Assert.Equal(9, s.Widgets.Count(w => w.Visible));
        Assert.Equal("en", s.Language);
    }
}
