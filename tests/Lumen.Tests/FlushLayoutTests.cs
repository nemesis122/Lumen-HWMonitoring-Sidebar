using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lumen.App;
using Lumen.Core;
using Xunit;

// Every design in two columns filling the screen ("Einpassen"): both columns end at the bottom on one
// line, and the widgets do not jump up and down from one update to the next.
public class FlushLayoutTests
{
    public static IEnumerable<object[]> Styles() => Settings.Selectable.Select(s => new object[] { s });

    [Theory]
    [MemberData(nameof(Styles))]
    public void Two_fitted_columns_end_at_the_bottom_and_stay_still(WidgetStyle style)
    {
        var (bottoms, move, height, trace) = UiHost.Run(() =>
        {
            L.Use("de");
            var source = new DemoSource();
            var history = new History();
            var settings = new Settings { Language = "de", Columns = 2, Density = Density.Fit, Width = 600 };
            var kinds = new[] { WidgetKind.Cpu, WidgetKind.Gpu, WidgetKind.Memory, WidgetKind.Drives, WidgetKind.Network, WidgetKind.Cooling, WidgetKind.Overview };
            foreach (var w in settings.Widgets) { w.Visible = kinds.Contains(w.Kind); if (Settings.HasStyles(w.Kind)) w.Style = style; }
            var view = new SidebarView(settings, Palette.For(true, false));
            var window = new Window { Content = view, Width = 600, Height = 1025 };
            window.Show();
            var first = new Dictionary<int, (double Top, double Bottom)>();
            double most = 0;
            var log = new List<string>();
            List<double> ends = new();
            for (int i = 0; i < 30; i++)
            {
                var snap = source.Read();
                history.Record(snap);
                view.Update(snap with { Gpus = snap.Gpus.Take(1).ToArray(), Battery = null }, history);
                for (int k = 0; k < 3; k++) { Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame(); }
                if (i < 8) continue;
                var cards = view.GetVisualDescendants().OfType<Control>().Where(c => (c is Card || c is SummaryTile || c is OverviewTile || c is AioTile) && c.IsVisible).ToList();
                for (int n = 0; n < cards.Count; n++)
                {
                    var top = cards[n].TranslatePoint(default, view)!.Value.Y;
                    var bottom = top + cards[n].Bounds.Height;
                    if (!first.TryGetValue(n, out var f)) first[n] = f = (top, bottom);
                    double d = Math.Max(Math.Abs(top - f.Top), Math.Abs(bottom - f.Bottom));
                    if (d > 4.0 && log.Count < 12) log.Add($"update {i}, {cards[n].GetType().Name} #{n}: {f.Top:0}-{f.Bottom:0} -> {top:0}-{bottom:0}");
                    most = Math.Max(most, d);
                }
                ends = cards.GroupBy(c => Math.Round(c.TranslatePoint(default, view)!.Value.X))
                    .Select(g => g.Max(c => c.TranslatePoint(new Point(0, c.Bounds.Height), view)!.Value.Y)).ToList();
            }
            double h = view.Bounds.Height;
            window.Close();
            return (ends, most, h, string.Join("\n", log));
        });
        Assert.Equal(AioDesigns.Contains(style)?1:2, bottoms.Count);
        if(bottoms.Count==2)Assert.True(Math.Abs(bottoms[0] - bottoms[1]) <= 4.0, $"{style}: bottoms {bottoms[0]:0.0} / {bottoms[1]:0.0}");
        Assert.True(bottoms.Min() >= height - 20, $"{style}: columns end at {bottoms.Min():0.0} of {height:0}");
        Assert.True(move <= 4.0, $"{style}: widgets moved by {move:0.0} px between updates\n{trace}");
    }
}

// The overview alone in the sidebar fills it from top to bottom in every design, and stays still.
public class OverviewFillTests
{
    public static IEnumerable<object[]> Styles() => Settings.Selectable.Select(s => new object[] { s });

    [Theory]
    [MemberData(nameof(Styles))]
    public void Overview_alone_fills_the_sidebar(WidgetStyle style)
    {
        var (bottom, height, move) = UiHost.Run(() =>
        {
            L.Use("de");
            var source = new DemoSource();
            var history = new History();
            var settings = new Settings { Language = "de", Columns = 1, Density = Density.Fit, Width = 330 };
            foreach (var w in settings.Widgets) { w.Visible = w.Kind == WidgetKind.Overview; if (Settings.HasStyles(w.Kind)) w.Style = style; }
            var view = new SidebarView(settings, Palette.For(true, false));
            var window = new Window { Content = view, Width = 330, Height = 1025 };
            window.Show();
            double first = double.NaN, most = 0, end = 0;
            for (int i = 0; i < 20; i++)
            {
                var snap = source.Read();
                history.Record(snap);
                view.Update(snap, history);
                for (int k = 0; k < 3; k++) { Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame(); }
                var card = view.GetVisualDescendants().OfType<Control>().Single(c=>c is OverviewTile or AioTile);
                end = card.TranslatePoint(new Point(0, card.Bounds.Height), view)!.Value.Y;
                // The rows inside the card must not move either.
                var rows = card.GetVisualDescendants().OfType<Grid>().Where(g => g.ColumnDefinitions.Count == 2 && g.Parent is StackPanel).ToList();
                double last = rows.Count > 0 ? rows[^1].TranslatePoint(default, view)!.Value.Y : 0;
                if (i < 6) continue;
                if (double.IsNaN(first)) first = last;
                most = Math.Max(most, Math.Abs(last - first));
            }
            double h = view.Bounds.Height;
            window.Close();
            return (end, h, most);
        });
        Assert.True(bottom >= height - 20, $"{style}: overview ends at {bottom:0} of {height:0}");
        Assert.True(move <= 4.0, $"{style}: rows moved by {move:0.0} px");
    }
}

// Evolution turns a card red when it gets too hot. That must not change the size of anything inside,
// otherwise the values below the hero drop from three columns to two and the sidebar jumps.
public class WarningStaysStillTests
{
    [Fact]
    public void A_red_card_keeps_its_layout()
    {
        var (cool, hot) = UiHost.Run(() =>
        {
            L.Use("de");
            var (snap, history) = Render.Demo();
            var card = Cards.Create(WidgetKind.Cpu, WidgetStyle.Evolution, 0, Palette.For(true, false), Metrics.For(Density.Fit), new Settings { Language = "de" });
            var window = new Window { Content = card, Width = 300, Height = 800 };
            window.Show();
            (double, double) Measure(double temp)
            {
                ((IWidget)card).Update(snap with { Cpu = snap.Cpu! with { Temperature = temp } }, history);
                for (int i = 0; i < 3; i++) { Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame(); }
                var inner = ((Decorator)card).Child!;
                return (inner.Bounds.Width, card.Bounds.Height);
            }
            var a = Measure(45);
            var b = Measure(97);
            window.Close();
            return (a, b);
        });
        Assert.Equal(cool.Item1, hot.Item1, 1);
        Assert.Equal(cool.Item2, hot.Item2, 1);
    }
}
