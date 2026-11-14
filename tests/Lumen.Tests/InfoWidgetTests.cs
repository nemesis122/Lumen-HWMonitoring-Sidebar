using Avalonia;
using Avalonia.VisualTree;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Lumen.App;
using Lumen.Core;
using Xunit;
using Xunit.Abstractions;

// Clock, world clocks, calendar, weather, now playing and the top apps make sense in every design: no bar for
// a time or a date, the list of apps with their names, German texts in a German sidebar, no word broken in
// the middle.
public class InfoWidgetTests
{
    private readonly ITestOutputHelper output;

    public InfoWidgetTests(ITestOutputHelper output) => this.output = output;

    public static IEnumerable<object[]> Styles() => Settings.Selectable.Select(s => new object[] { s });

    private static (Control Card, Window Window) Show(WidgetKind kind, WidgetStyle style, Snapshot snap, History history)
    {
        var settings = new Settings { Language = "de", WorldClocks = new List<string> { "America/New_York", "Asia/Tokyo" } };
        var card = Cards.Create(kind, style, 0, Palette.For(true, false), Metrics.For(Density.Fit), settings);
        ((IWidget)card).Update(snap, history);
        var window = new Window { Content = card, Width = 340, Height = 1200 };
        window.Show();
        for (int i = 0; i < 3; i++) { Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame(); }
        return (card, window);
    }

    private static List<TextBlock> Visible(Control card) =>
        card.GetLogicalDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && t.Bounds.Width > 0 && t.Opacity > 0).ToList();

    private static bool Bar(Control c) => c is FillBack or Meter or GradientBar or SegmentBar or LevelBar or RingBar
        || c.GetType().Name is "DialArc" or "RingPair" or "KnobRing" or "SweepRing" or "TickRing";

    [Theory]
    [MemberData(nameof(Styles))]
    public void Clock_and_calendar_have_no_bar(WidgetStyle style)
    {
        var bars = UiHost.Run(() =>
        {
            L.Use("de");
            var (snap, history) = Render.Demo();
            var found = new List<string>();
            foreach (var kind in new[] { WidgetKind.Clock, WidgetKind.Calendar, WidgetKind.WorldClocks })
            {
                if (style == WidgetStyle.Standard) continue;
                var (card, window) = Show(kind, style, snap, history);
                found.AddRange(card.GetLogicalDescendants().OfType<Control>().Where(c => c.IsEffectivelyVisible && Bar(c)).Select(c => kind + ":" + c.GetType().Name));
                window.Close();
            }
            return found;
        });
        Assert.True(bars.Count == 0, style + ": " + string.Join(", ", bars));
    }

    [Theory]
    [MemberData(nameof(Styles))]
    public void Top_apps_list_every_app_by_name(WidgetStyle style)
    {
        var (all, missing) = UiHost.Run(() =>
        {
            L.Use("de");
            var (snap, history) = Render.Demo();
            var (card, window) = Show(WidgetKind.Processes, style, snap, history);
            string text = string.Join(" | ", Visible(card).Select(t => t.Text));
            window.Close();
            var names = snap.Processes.OrderByDescending(p => p.CpuPercent).Take(4).Select(p => p.Name).ToList();
            return (text, names.Where(n => !text.Contains(n)).ToList());
        });
        output.WriteLine(all);
        Assert.True(missing.Count == 0, style + ": missing " + string.Join(",", missing) + "\n" + all);
    }

    private static readonly string[] English = { "Today", "Feels like", "Humidity", "Now playing", "Top apps", "partly-cloudy", "Week", "Offset", "Conditions", "Place", "Temperature", "Application", "Playback", "Seconds", "Day progress" };

    public static IEnumerable<object[]> InfoKinds() =>
        from style in Settings.Selectable
        from kind in new[] { WidgetKind.Clock, WidgetKind.WorldClocks, WidgetKind.Calendar, WidgetKind.Weather, WidgetKind.Media, WidgetKind.Processes, WidgetKind.DataUsage, WidgetKind.DriveHealth, WidgetKind.System }
        select new object[] { style, kind };


    [Theory]
    [MemberData(nameof(InfoKinds))]
    public void No_word_is_broken_in_the_middle(WidgetStyle style, WidgetKind kind)
    {
        var broken = UiHost.Run(() =>
        {
            L.Use("de");
            var (snap, history) = Render.Demo();
            var (card, window) = Show(kind, style, snap, history);
            var list = new List<string>();
            foreach (var t in Visible(card).Where(t => t.TextWrapping != Avalonia.Media.TextWrapping.NoWrap && !string.IsNullOrEmpty(t.Text)))
            {
                var lines = t.TextLayout.TextLines;
                if (lines.Count < 2) continue;
                string text = t.Text!;
                foreach (var line in lines.Skip(1))
                {
                    int at = line.FirstTextSourceIndex;
                    if (at > 0 && at < text.Length && char.IsLetterOrDigit(text[at - 1]) && char.IsLetterOrDigit(text[at])) list.Add(text);
                }
            }
            window.Close();
            return list;
        });
        L.Use("en");
        Assert.True(broken.Count == 0, style + "/" + kind + ": " + string.Join(", ", broken));
    }

    // In the horizontal bar the overview's rings sit side by side; before, a wide card made every ring as
    // tall as the card was wide and the whole row of widgets shrank to fit it.
    public static IEnumerable<object[]> RingStyles() => new[] { WidgetStyle.Thermostat, WidgetStyle.Dial, WidgetStyle.RingPair, WidgetStyle.PurpleDash, WidgetStyle.SciFi }.Select(s => new object[] { s });

    [Theory]
    [MemberData(nameof(RingStyles))]
    public void Overview_fits_a_low_bar_row(WidgetStyle style)
    {
        double height = UiHost.Run(() =>
        {
            L.Use("de");
            var (snap, history) = Render.Demo();
            var settings = new Settings { Language = "de", Edge = Edge.Top };
            var card = Cards.Create(WidgetKind.Overview, style, 0, Palette.For(true, false), Metrics.For(Density.Fit), settings);
            ((IWidget)card).Update(snap, history);
            card.Measure(new Avalonia.Size(380, double.PositiveInfinity));
            return card.DesiredSize.Height;
        });
        L.Use("en");
        Assert.True(height < 330, style + ": " + height);
    }

    [Theory]
    [MemberData(nameof(Styles))]
    public void Drive_health_follows_the_design(WidgetStyle style)
    {
        var type = UiHost.Run(() => Cards.Create(WidgetKind.DriveHealth, style, 0, Palette.For(true, false), Metrics.For(Density.Fit), new Settings()).GetType());
        Assert.Equal(style == WidgetStyle.Standard ? "DriveHealthCard" : "InfoTile", type.Name);
    }

    // A row of the horizontal bar with the overview and drive health (as in the screenshot's second row):
    // every card keeps its place from one update to the next and the row is not shrunk to fit them.
    [Theory]
    [MemberData(nameof(Styles))]
    public void Bar_rows_stay_still_and_full_size(WidgetStyle style)
    {
        var (move, smallest, trace) = UiHost.Run(() =>
        {
            L.Use("de");
            var source = new DemoSource();
            var history = new History();
            var settings = new Settings { Language = "de", Edge = Edge.Top, BarHeight = 270, Columns = 1, Density = Density.Fit };
            var kinds = new[] { WidgetKind.Clock, WidgetKind.Weather, WidgetKind.DataUsage, WidgetKind.Overview, WidgetKind.DriveHealth };
            foreach (var w in settings.Widgets) { w.Visible = kinds.Contains(w.Kind); if (Settings.HasStyles(w.Kind)) w.Style = style; }
            var view = new SidebarView(settings, Palette.For(true, false));
            var window = new Window { Content = view, Width = 1900, Height = 310 };
            window.Show();
            var first = new Dictionary<int, Rect>();
            double most = 0, least = double.MaxValue;
            string small = "";
            var log = new List<string>();
            for (int i = 0; i < 16; i++)
            {
                var snap = source.Read();
                history.Record(snap);
                view.Update(snap with { Gpus = snap.Gpus.Take(1).ToArray(), Battery = null }, history);
                for (int k = 0; k < 3; k++) { Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame(); }
                if (i < 6) continue;
                var cards = view.GetVisualDescendants().OfType<Control>().Where(c => c is IWidget && c.IsVisible).ToList();
                for (int n = 0; n < cards.Count; n++)
                {
                    var at = cards[n].TranslatePoint(default, view)!.Value;
                    var end = cards[n].TranslatePoint(new Avalonia.Point(cards[n].Bounds.Width, cards[n].Bounds.Height), view)!.Value;
                    var box = new Rect(at, end);
                    if (!first.TryGetValue(n, out var f)) first[n] = f = box;
                    double d = Math.Max(Math.Abs(box.Top - f.Top), Math.Abs(box.Bottom - f.Bottom));
                    if (d > 4.0 && log.Count < 8) log.Add($"update {i}, {cards[n].GetType().Name}: {f.Top:0}-{f.Bottom:0} -> {box.Top:0}-{box.Bottom:0}");
                    most = Math.Max(most, d);
                    // A card drawn at a fraction of its size (the row scaled down to fit one tall card).
                    double scale = box.Width / Math.Max(1, cards[n].Bounds.Width);
                    if (scale < least) { least = scale; small = cards[n].GetType().Name + " " + n; }
                }
            }
            window.Close();
            return (most, least, string.Join("\n", log) + "\nsmallest: " + small);
        });
        L.Use("en");
        Assert.True(move <= 4.0, $"{style}: cards moved by {move:0.0} px\n{trace}");
        Assert.True(smallest >= 0.8, $"{style}: a row is drawn at {smallest:0.00} of its size; {trace}");
    }

    // Designs with their own card and accent colours follow a picked background and colour scheme.
    [Fact]
    public void Designs_follow_the_picked_background_and_colour()
    {
        var plain = Palette.For(new Settings(), true, null);
        Assert.Equal(Avalonia.Media.Color.Parse("#111213"), plain.DesignBack("#111213"));
        var picked = Palette.For(new Settings { Background = "black", Colors = "#22c55e" }, true, null);
        Assert.Equal(picked.Card, picked.DesignBack("#111213"));
        Assert.Equal(Avalonia.Media.Color.Parse("#22c55e"), Accents.Design("#ec4899", picked));
        Assert.Equal(Avalonia.Media.Color.Parse("#22c55e"), Accents.For(WidgetKind.Cpu, null, picked));
    }

    // Every widget in every design, German sidebar: no visible word that has a German translation is shown
    // in English ("Used", "Free", "Link speed" ...).
    public static IEnumerable<object[]> AllKinds() => Enum.GetValues<WidgetKind>().Select(k => new object[] { k });


// A sensor that reports nothing for a moment keeps its last value (so no line vanishes and nothing jumps);
// one that stays away is let go.
public class SensorHoldTests
{
    [Fact]
    public void Brief_gaps_keep_the_last_value_and_long_gaps_let_go()
    {
        var (snap, _) = Render.Demo();
        var hold = new SensorHold();
        var first = hold.Apply(snap with { Cpu = snap.Cpu! with { PowerW = 78 } });
        Assert.Equal(78, first.Cpu!.PowerW);
        var gap = hold.Apply(snap with { Cpu = snap.Cpu! with { PowerW = null }, Fans = snap.Fans.Skip(1).ToArray() });
        Assert.Equal(78, gap.Cpu!.PowerW);
        Assert.Equal(snap.Fans.Count, gap.Fans.Count);
        Assert.Equal(snap.Fans[0].Name, gap.Fans[0].Name);
        Snapshot later = gap;
        for (int i = 0; i < SensorHold.Patience + 1; i++) later = hold.Apply(snap with { Cpu = snap.Cpu! with { PowerW = null }, Fans = snap.Fans.Skip(1).ToArray() });
        Assert.Null(later.Cpu!.PowerW);
        Assert.Equal(snap.Fans.Count - 1, later.Fans.Count);
    }
}

/// <summary>Every bar line of a widget starts and ends at the same place (name and value columns are shared).</summary>
public class AlignedBarTests
{
    public static IEnumerable<object[]> Cases() =>
        from style in new[] { WidgetStyle.Segments, WidgetStyle.SegmentGraph, WidgetStyle.TableGraph, WidgetStyle.FillBarsGraph, WidgetStyle.PillRowGraph,
            WidgetStyle.BadgeListGraph, WidgetStyle.NeonHudGraph, WidgetStyle.Thermostat, WidgetStyle.RingPair }
        from kind in new[] { WidgetKind.Cpu, WidgetKind.Gpu, WidgetKind.Memory, WidgetKind.Drives, WidgetKind.Network }
        select new object[] { style, kind };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Bars_start_and_end_together(WidgetStyle style, WidgetKind kind)
    {
        var edges = UiHost.Run(() =>
        {
            L.Use("en");
            var (snap, history) = Render.Demo();
            var card = Cards.Create(kind, style, 0, Palette.For(true, false), Metrics.For(Density.Fit), new Settings { Language = "en" });
            ((IWidget)card).Update(snap, history);
            var window = new Window { Content = card, Width = 320, Height = 1200 };
            window.Show();
            for (int i = 0; i < 3; i++) { Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame(); }
            var bars = card.GetVisualDescendants().OfType<Control>()
                .Where(c => c is FillBack or Meter or GradientBar or SegmentBar or RingBar && c.IsEffectivelyVisible && c.Parent is Grid g && g.ColumnDefinitions.Count == 3)
                .Select(c => (c.TranslatePoint(default, card)!.Value.X, c.TranslatePoint(new Point(c.Bounds.Width, 0), card)!.Value.X)).ToList();
            window.Close();
            return bars;
        });
        if (edges.Count < 2) return;
        Assert.True(edges.Max(e => e.Item1) - edges.Min(e => e.Item1) <= 1.5, $"{style}/{kind}: starts {string.Join(", ", edges.Select(e => e.Item1.ToString("0")))}");
        Assert.True(edges.Max(e => e.Item2) - edges.Min(e => e.Item2) <= 1.5, $"{style}/{kind}: ends {string.Join(", ", edges.Select(e => e.Item2.ToString("0")))}");
    }
}
}
