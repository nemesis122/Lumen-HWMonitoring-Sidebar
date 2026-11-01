using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

// Shared frame for the extra styles: a card with glyph, title, model name and caption on top.
public abstract class SummaryTile : Border, IWidget, INamed
{
    private WidgetSlot? slot;
    public void Own(WidgetSlot slot, int index) => this.slot = slot;
    protected readonly WidgetKind Kind; protected readonly int Index; protected readonly Settings S; protected readonly Palette P; protected readonly Metrics M;
    private readonly TextBlock title, name;
    protected readonly TextBlock Caption;
    protected readonly ExtrasView Extras;
    private readonly ContentControl glyph = new();

    protected SummaryTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s, bool half)
    {
        Kind = kind; Index = index; S = s; P = p; M = m;
        Background = Palette.Brush(p.Card);
        BorderBrush = Palette.Brush(p.Stroke);
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(m.Radius);
        Padding = half ? new Thickness(12, m.Pad - 6, 12, m.Pad - 4) : new Thickness(m.Pad, m.Pad - 4, m.Pad, m.Pad - 2);
        Extras = new ExtrasView(p, !half) { Margin = new Thickness(0, half ? 2 : 4, 0, 0) };
        title = Ui.Text("", half ? 11.5 : 12.5, p.Ink, FontWeight.SemiBold);
        title.Margin = new Thickness(7, 0, 0, 0);
        name = Ui.Text("", 10.5, p.Soft);
        Caption = Ui.Text("", 11.5, p.Soft, numbers: true);
        Caption.HorizontalAlignment = half ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        Caption.VerticalAlignment = VerticalAlignment.Center;
        Head = half ? Ui.Stack(0, Ui.Columns("Auto,*", glyph, title), name) : Ui.Stack(0, Ui.Columns("Auto,Auto,*", glyph, title, Caption), name);
        if (!half) Grid.SetColumn(Caption, 2);
    }

    protected Control Head { get; }

    public void Update(Snapshot snap, History h)
    {
        var sum = Summaries.For(Kind, Index, snap, h, S, P);
        if (glyph.Tag as string != sum.Glyph) { glyph.Content = Ui.Glyph(sum.Glyph, P.Soft, 13); glyph.Tag = sum.Glyph; }
        title.Text = Naming.Title(slot, Index, sum.Title);
        name.Text = Naming.Hardware(slot, Index, sum.Name);
        name.IsVisible = Naming.ShowHardware(slot, S) && name.Text.Length > 0;
        Caption.Text = sum.Caption;
        Extras.Set(Summaries.Extras(Kind, Index, snap, h, S));
        Show(sum);
    }

    protected abstract void Show(Summary sum);

    protected Color Shade(Summary sum) => sum.Color == P.Ink ? P.Cool : sum.Color;
}

// "Bar": the big value and one thick bar across the card.
public sealed class BarTile : SummaryTile
{
    private readonly Hero hero; private readonly Meter meter;

    public BarTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s) : base(kind, index, p, m, s, false)
    {
        hero = new Hero(m.Hero * .85, p);
        meter = new Meter { Height = m.Subtitles ? 10 : 8, Palette = p, Margin = new Thickness(0, 2, 0, 0) };
        var body = new StackPanel { Spacing = 6, Tag = "elastic" };
        body.Children.Add(Head);
        body.Children.Add(hero);
        body.Children.Add(meter);
        body.Children.Add(Extras);
        Child = body;
    }

    protected override void Show(Summary sum)
    {
        hero.Set(sum.Value, sum.Unit, sum.Color);
        meter.Set(sum.Fraction, Shade(sum));
    }
}

// "Graph": the history fills the card, the value sits on top of it.
public sealed class GraphTile : SummaryTile
{
    private readonly Hero hero; private readonly AreaChart chart; private readonly Meter meter;

    public GraphTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s) : base(kind, index, p, m, s, false)
    {
        hero = new Hero(m.Hero * .8, p);
        chart = new AreaChart { Height = m.Chart * 1.5, LineWidth = 1.8, FillOpacity = .28 };
        meter = new Meter { Height = 6, Palette = p, VerticalAlignment = VerticalAlignment.Bottom, IsVisible = false };
        // The chart starts just under the number, never through it, and may reach up beside the unit.
        var stage = new Panel { Children = { chart, meter }, Margin = new Thickness(0, -m.Hero * .2, 0, 0) };
        var body = new StackPanel { Spacing = 6, Tag = "elastic" };
        body.Children.Add(Head);
        body.Children.Add(hero);
        body.Children.Add(stage);
        body.Children.Add(Extras);
        Child = body;
    }

    protected override void Show(Summary sum)
    {
        hero.Set(sum.Value, sum.Unit, sum.Color);
        // The history shows unless the user switched the widget's chart off.
        bool series = sum.Series.Length > 1 && (Summaries.ChartKey(Kind) is not string key || S.Shows(key));
        // Readings without a history (weather, battery) show their level as a bar under the value.
        chart.Opacity = series ? 1 : 0;
        meter.IsVisible = !series;
        if (series) { chart.Max = sum.SeriesMax; chart.Set(sum.Series, Shade(sum)); }
        else meter.Set(sum.Fraction, Shade(sum));
    }
}

// "Tile": half width, one large number and a slim bar; two sit side by side like rings.
public sealed class NumberTile : SummaryTile
{
    private readonly TextBlock value, unit; private readonly Meter meter;

    public NumberTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s) : base(kind, index, p, m, s, true)
    {
        double size = m.Subtitles ? 40 : 32;
        value = Ui.Text("", size, p.Ink, FontWeight.SemiBold, true);
        value.LetterSpacing = -size * .03;
        unit = Ui.Text("", 12, p.Soft, FontWeight.Medium);
        unit.VerticalAlignment = VerticalAlignment.Bottom;
        unit.Margin = new Thickness(3, 0, 0, size * .16);
        meter = new Meter { Height = 5, Palette = p };
        var body = new StackPanel { Spacing = 4, Tag = "elastic" };
        body.Children.Add(Head);
        body.Children.Add(Ui.Row(0, value, unit));
        body.Children.Add(meter);
        body.Children.Add(Caption);
        body.Children.Add(Extras);
        Child = body;
    }

    protected override void Show(Summary sum)
    {
        Motion.Text(value, sum.Value);
        value.Foreground = Palette.Brush(sum.Color);
        unit.Text = sum.Value == Format.Missing ? "" : sum.Unit;
        meter.Set(sum.Fraction, Shade(sum));
    }
}

// The small figures under a compact widget: side by side in full-width styles, one per line in half-width tiles.
public sealed class ExtrasView : ContentControl
{
    private readonly Palette p; private readonly bool wide;
    private string shape = "";
    // Three figures side by side, two in a narrow card (a bar with many widgets), so labels are not cut off.
    private int columns = 3;
    private List<(string Label, string Value)> last = new();
    private readonly List<Stat> stats = new();
    private readonly List<TextBlock> values = new();

    public ExtrasView(Palette p, bool wide) { this.p = p; this.wide = wide; }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        // A gap between the two switch points: switching changes the card's height, a bar then
        // rescales, and without the gap the columns could flip back and forth for ever.
        double w = e.NewSize.Width;
        int want = w <= 0 ? columns : columns == 3 ? (w < 240 ? 2 : 3) : (w > 275 ? 3 : 2);
        if (!wide || want == columns) return;
        columns = want;
        Avalonia.Threading.Dispatcher.UIThread.Post(() => Set(last));
    }

    public void Set(List<(string Label, string Value)> items)
    {
        last = items;
        IsVisible = items.Count > 0;
        // Wide: three per line (like the Standard cards), up to two lines; tiles: three lines,
        // so the gauge stays the main thing.
        var shown = items.Take(wide ? columns * 2 : 3).ToList();
        // Long values (an IP address, a date) get a line of their own instead of being cut off.
        static bool Long(string v) => v.Length > 14 || System.Net.IPAddress.TryParse(v, out _);
        string now = columns + ":" + string.Join("|", shown.Select(i => i.Label + (Long(i.Value) ? "+" : "")));
        if (now != shape)
        {
            shape = now;
            stats.Clear(); values.Clear();
            if (wide)
            {
                foreach (var (label, _) in shown) stats.Add(new Stat(label, p));
                // Three equal columns; a long value starts a new line and spans all three.
                var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("*", columns))) };
                int row = 0, col = 0;
                for (int i = 0; i < stats.Count; i++)
                {
                    bool span = Long(shown[i].Value);
                    if (span && col > 0) { row++; col = 0; }
                    if (grid.RowDefinitions.Count <= row) grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                    var st = stats[i];
                    st.Margin = new Thickness(0, 0, 6, 4);
                    Grid.SetRow(st, row); Grid.SetColumn(st, col);
                    if (span) Grid.SetColumnSpan(st, columns);
                    grid.Children.Add(st);
                    col += span ? columns : 1;
                    if (col >= columns) { row++; col = 0; }
                }
                Content = shown.Count == 0 ? null : grid;
            }
            else
            {
                var rows = new StackPanel { Spacing = 1 };
                foreach (var (label, _) in shown)
                {
                    var l = Ui.Text(label, 10.5, p.Faint, FontWeight.Medium);
                    var v = Ui.Text("", 10.5, p.Ink, FontWeight.Medium, true);
                    v.HorizontalAlignment = HorizontalAlignment.Right;
                    values.Add(v);
                    var row = Ui.Columns("*,Auto", l, v);
                    rows.Children.Add(row);
                }
                Content = rows;
            }
        }
        for (int i = 0; i < shown.Count; i++)
        {
            if (wide) stats[i].Value = shown[i].Value;
            else values[i].Text = shown[i].Value;
        }
    }
}
