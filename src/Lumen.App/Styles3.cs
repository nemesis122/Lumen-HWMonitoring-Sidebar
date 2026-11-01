using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

// The compact styles. All of them read the same Summary as Ring, Bar, Graph and Tile, so every
// widget that has a summary offers every style – at the side and in the bar alike.

// "Compact": one line of about 64 px – name on the left, the big value, then caption, a slim
// bar and a sparkline. Made for a low bar and for many widgets down the side.
public sealed class CompactRow : SummaryTile
{
    private readonly Hero hero; private readonly Meter meter; private readonly AreaChart spark;

    public CompactRow(WidgetKind kind, int index, Palette p, Metrics m, Settings s) : base(kind, index, p, m, s, true)
    {
        Padding = new Thickness(12, 8, 12, 8);
        hero = new Hero(m.Subtitles ? 28 : 24, p) { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 14, 0) };
        meter = new Meter { Height = 4, Palette = p, Margin = new Thickness(0, 4, 0, 3) };
        // A fixed height: in a tall bar the row stays a row instead of growing a big chart.
        spark = new AreaChart { Height = 18, LineWidth = 1.4, FillOpacity = .18, Tag = "fixed" };
        Caption.HorizontalAlignment = HorizontalAlignment.Left;
        Caption.TextTrimming = TextTrimming.CharacterEllipsis;
        var right = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { Caption, meter, spark } };
        Head.VerticalAlignment = VerticalAlignment.Center;
        Head.MaxWidth = 150;
        Child = Ui.Columns("Auto,Auto,*", Head, hero, right);
        Grid.SetColumn(right, 2);
    }

    private bool narrow;

    // Narrow (many widgets in a bar): the caption gives way instead of being cut off.
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        double w = e.NewSize.Width;
        // Two switch points with room between them, so a rescaling bar cannot flip it back and forth.
        bool now = w <= 0 ? narrow : narrow ? w < 305 : w < 275;
        if (now == narrow) return;
        narrow = now;
        // Only faded out, not removed: the card keeps its size, so the bar's rescaling cannot feed back.
        Caption.Opacity = narrow ? 0 : 1;
        hero.Margin = narrow ? new Thickness(8, 0, 8, 0) : new Thickness(12, 0, 14, 0);
    }

    protected override void Show(Summary sum)
    {
        hero.Set(sum.Value, sum.Unit, sum.Color);
        meter.Set(sum.Second ?? sum.Fraction, sum.Second is null ? Shade(sum) : P.Cool);
        bool series = sum.Series.Length > 1 && (Summaries.ChartKey(Kind) is not string key || S.Shows(key));
        spark.IsVisible = series;
        if (series) { spark.Max = sum.SeriesMax; spark.Set(sum.Series, Shade(sum)); }
    }
}

// "Status": a single slim line of about 32 px, like a status bar: dot, name, value, a tiny bar, caption.
public sealed class StatusRow : Border, IWidget, INamed
{
    private WidgetSlot? slot;
    public void Own(WidgetSlot slot, int index) => this.slot = slot;
    private readonly WidgetKind kind; private readonly int index; private readonly Settings s; private readonly Palette p;
    private readonly Avalonia.Controls.Shapes.Ellipse dot = new() { Width = 7, Height = 7, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock title, value, unit, caption;
    private readonly Meter meter;

    public StatusRow(WidgetKind kind, int index, Palette p, Metrics m, Settings s)
    {
        this.kind = kind; this.index = index; this.s = s; this.p = p;
        Background = Palette.Brush(p.Card);
        BorderBrush = Palette.Brush(p.Stroke);
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(Math.Min(m.Radius, 10));
        Padding = new Thickness(10, 6, 12, 6);
        title = Ui.Text("", 12, p.Soft, FontWeight.SemiBold);
        value = Ui.Text("", 14, p.Ink, FontWeight.SemiBold, true);
        unit = Ui.Text("", 11, p.Soft, FontWeight.Medium);
        unit.VerticalAlignment = VerticalAlignment.Bottom;
        unit.Margin = new Thickness(2, 0, 0, 1);
        caption = Ui.Text("", 11.5, p.Soft, numbers: true);
        caption.TextTrimming = TextTrimming.CharacterEllipsis;
        meter = new Meter { Width = 44, Height = 4, Palette = p, VerticalAlignment = VerticalAlignment.Center };
        foreach (var c in new Control[] { title, value, caption }) c.VerticalAlignment = VerticalAlignment.Center;
        var row = Ui.Columns("Auto,Auto,Auto,Auto,Auto,*", dot, title, Ui.Row(0, value, unit), meter, new Control(), caption);
        title.Margin = new Thickness(7, 0, 8, 0);
        meter.Margin = new Thickness(8, 0, 8, 0);
        Grid.SetColumn(caption, 5);
        Child = row;
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        double w = e.NewSize.Width;
        if (w > 0) caption.Opacity = caption.Opacity > 0 ? (w >= 235 ? 1 : 0) : (w >= 255 ? 1 : 0);
    }

    public void Update(Snapshot snap, History h)
    {
        var sum = Summaries.For(kind, index, snap, h, s, p);
        title.Text = Naming.Title(slot, index, sum.Title);
        Motion.Text(value, sum.Value);
        value.Foreground = Palette.Brush(sum.Color);
        unit.Text = sum.Value == Format.Missing ? "" : sum.Unit;
        dot.Fill = Palette.Brush(sum.Color == p.Ink ? p.Cool : sum.Color);
        meter.Set(sum.Second ?? sum.Fraction, sum.Second is null ? (sum.Color == p.Ink ? p.Cool : sum.Color) : p.Cool);
        caption.Text = sum.Second is not null && sum.SecondLabel.Length > 0 ? sum.SecondLabel : sum.Caption;
    }
}

// A half circle that fills from left to right, rounded ends.
public sealed class HalfGauge : Control
{
    private bool started;
    public double Fraction { get; set; }
    public Color Color { get; set; }
    public Palette? Palette { get; set; }
    public double Thickness { get; set; } = 9;

    public void Animate(double fraction, Color color)
    {
        Color = color;
        double from = started ? Fraction : fraction;
        started = true;
        if (!double.IsFinite(fraction)) fraction = 0;
        Motion.Run(this, Motion.Duration, t => { Fraction = from + (fraction - from) * t; InvalidateVisual(); });
    }

    public override void Render(DrawingContext ctx)
    {
        if (Palette is null) return;
        double r = Math.Min(Bounds.Width / 2, Bounds.Height) - Thickness / 2;
        if (r <= 2) return;
        var c = new Point(Bounds.Width / 2, Bounds.Height - Thickness / 2);
        Arc(ctx, c, r, 1, new Pen(new SolidColorBrush(Palette.Track), Thickness, lineCap: PenLineCap.Round));
        double f = double.IsFinite(Fraction) ? Math.Clamp(Fraction, 0, 1) : 0;
        if (f > .005) Arc(ctx, c, r, f, new Pen(new SolidColorBrush(Color), Thickness, lineCap: PenLineCap.Round));
    }

    private static void Arc(DrawingContext ctx, Point c, double r, double f, Pen pen)
    {
        Point At(double deg) => new(c.X + r * Math.Cos(deg * Math.PI / 180), c.Y + r * Math.Sin(deg * Math.PI / 180));
        var g = new StreamGeometry();
        using (var s = g.Open())
        {
            s.BeginFigure(At(180), false);
            s.ArcTo(At(180 + 180 * f), new Size(r, r), 0, false, SweepDirection.Clockwise);
            s.EndFigure(false);
        }
        ctx.DrawGeometry(null, pen, g);
    }
}

// "Gauge": a half-circle gauge on the left with the value under its arc, details on the right.
public sealed class GaugeTile : SummaryTile
{
    private readonly HalfGauge gauge; private readonly TextBlock value, unit;

    public GaugeTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s) : base(kind, index, p, m, s, true)
    {
        double w = m.Subtitles ? 116 : 100;
        gauge = new HalfGauge { Width = w, Height = w / 2 + 6, Palette = p, Thickness = m.Subtitles ? 10 : 8 };
        value = Ui.Text("", m.Subtitles ? 24 : 20, p.Ink, FontWeight.SemiBold, true);
        unit = Ui.Text("", 11, p.Soft, FontWeight.Medium);
        unit.VerticalAlignment = VerticalAlignment.Bottom;
        unit.Margin = new Thickness(1, 0, 0, 3);
        var center = Ui.Row(0, value, unit);
        center.HorizontalAlignment = HorizontalAlignment.Center;
        center.VerticalAlignment = VerticalAlignment.Bottom;
        var dial = new Panel { Width = w, Height = w / 2 + 6, Children = { gauge, center }, VerticalAlignment = VerticalAlignment.Center };
        Caption.HorizontalAlignment = HorizontalAlignment.Left;
        this.dial = dial;
        right = Ui.Stack(3, Head, Caption, Extras);
        right.VerticalAlignment = VerticalAlignment.Center;
        // In a bar the gaps open up a little so all cards of a row end at the same line.
        right.Tag = "elastic";
        grid = new Grid { Children = { dial, right } };
        // In the bar the dial sits on top with the name under it; at the side the details stand beside it.
        // Fixed by where the sidebar is, not by the card's width: a bar rescales its cards to fit the
        // row's height, so a width-driven switch could keep flipping the layout.
        Arrange(s.Horizontal);
        Child = grid;
    }

    private readonly Panel dial;
    private readonly StackPanel right;
    private readonly Grid grid;
    // Side by side, or (in the bar) the dial on top and the name and caption under it.
    private void Arrange(bool small)
    {
        grid.ColumnDefinitions = new ColumnDefinitions(small ? "*" : "Auto,*");
        grid.RowDefinitions = new RowDefinitions(small ? "Auto,Auto" : "Auto");
        Grid.SetColumn(right, small ? 0 : 1);
        Grid.SetRow(right, small ? 1 : 0);
        dial.HorizontalAlignment = small ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        right.Margin = small ? new Thickness(0, 4, 0, 0) : new Thickness(14, 0, 0, 0);
        right.HorizontalAlignment = small ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
        Caption.HorizontalAlignment = small ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        Extras.Opacity = small ? 0 : 1;
        Extras.MaxHeight = small ? 0 : double.PositiveInfinity;
    }

    protected override void Show(Summary sum)
    {
        Motion.Text(value, sum.Value);
        value.Foreground = Palette.Brush(sum.Color);
        unit.Text = sum.Value == Format.Missing ? "" : sum.Unit;
        gauge.Animate(sum.Fraction, Shade(sum));
    }
}

// "Segments": LED-style bars – the reading, and where there is one the load – that turn amber and red near the top.
public sealed class SegmentTile : SummaryTile
{
    private readonly Meter first, second; private readonly TextBlock firstValue, secondValue, secondLabel, firstLabel;
    private readonly Grid secondRow;

    public SegmentTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s) : base(kind, index, p, m, s, false)
    {
        Grid Line(string label, out TextBlock l, out Meter meter, out TextBlock v)
        {
            l = Ui.Text(label, 11, p.Soft, FontWeight.Medium);
            l.Width = 46;
            l.VerticalAlignment = VerticalAlignment.Center;
            meter = new Meter { Height = 12, Palette = p, Segments = 20, VerticalAlignment = VerticalAlignment.Center };
            v = Ui.Text("", 13, p.Ink, FontWeight.SemiBold, true);
            v.MinWidth = 58;
            v.TextAlignment = TextAlignment.Right;
            v.VerticalAlignment = VerticalAlignment.Center;
            var g = Ui.Columns("Auto,*,Auto", l, meter, v);
            meter.Margin = new Thickness(0, 0, 10, 0);
            return g;
        }
        var row1 = Line("", out firstLabel, out first, out firstValue);
        secondRow = Line(L.T("Load"), out secondLabel, out second, out secondValue);
        // Head and bars stay together; spare height in a bar row opens up between them and the figures.
        var top = Ui.Stack(8, Head, row1, secondRow);
        var body = new StackPanel { Spacing = 8, Tag = "elastic" };
        body.Children.Add(top);
        // An empty spacer: with or without figures the card can open up to the row's height,
        // the bars staying under the head and the figures (if any) going to the bottom.
        body.Children.Add(new Border { Height = 0 });
        body.Children.Add(Extras);
        Child = body;
    }

    protected override void Show(Summary sum)
    {
        firstLabel.Text = sum.Unit is "°C" or "°F" ? L.T("Temp") : "";
        firstLabel.IsVisible = firstLabel.Text.Length > 0;
        firstValue.Text = sum.Value == Format.Missing ? sum.Value : $"{sum.Value} {sum.Unit}".Trim();
        firstValue.Foreground = Palette.Brush(sum.Color);
        first.RampBySegment = true;
        first.Set(sum.Fraction, Shade(sum));
        secondRow.IsVisible = sum.Second is not null;
        if (sum.Second is double load)
        {
            second.RampBySegment = false;
            second.Set(load, P.Cool);
            secondValue.Text = sum.SecondLabel.Length > 0 ? sum.SecondLabel : Format.Percent(load * 100);
            secondLabel.Text = L.T("Load");
        }
    }
}

// Two rings in one: outside the load, inside the main reading (temperature, fill level …).
public sealed class DualRing : Control
{
    private bool started;
    public double Outer { get; set; }
    public double Inner { get; set; }
    public bool HasOuter { get; set; }
    public Color OuterColor { get; set; }
    public Color InnerColor { get; set; }
    public Palette? Palette { get; set; }
    public double Thickness { get; set; } = 6;

    public void Animate(double? outer, double inner, Color outerColor, Color innerColor)
    {
        OuterColor = outerColor; InnerColor = innerColor; HasOuter = outer is not null;
        double o = outer is double x && double.IsFinite(x) ? x : 0, i = double.IsFinite(inner) ? inner : 0;
        double fo = started ? Outer : o, fi = started ? Inner : i;
        started = true;
        Motion.Run(this, Motion.Duration, t => { Outer = fo + (o - fo) * t; Inner = fi + (i - fi) * t; InvalidateVisual(); });
    }

    public override void Render(DrawingContext ctx)
    {
        if (Palette is null) return;
        double size = Math.Min(Bounds.Width, Bounds.Height);
        var c = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var track = new SolidColorBrush(Palette.Track);
        double rOuter = (size - Thickness) / 2, rInner = rOuter - Thickness - 3;
        if (!HasOuter) { rInner = rOuter; }
        void Ring(double r, double f, Color color, double thick)
        {
            if (r <= 2) return;
            ctx.DrawEllipse(null, new Pen(track, thick), c, r, r);
            f = Math.Clamp(f, 0, 1);
            if (f < .005) return;
            if (f > .999) { ctx.DrawEllipse(null, new Pen(new SolidColorBrush(color), thick), c, r, r); return; }
            Point At(double deg) => new(c.X + r * Math.Cos(deg * Math.PI / 180), c.Y + r * Math.Sin(deg * Math.PI / 180));
            var g = new StreamGeometry();
            using (var s = g.Open())
            {
                s.BeginFigure(At(-90), false);
                s.ArcTo(At(-90 + 360 * f), new Size(r, r), 0, f > .5, SweepDirection.Clockwise);
                s.EndFigure(false);
            }
            ctx.DrawGeometry(null, new Pen(new SolidColorBrush(color), thick, lineCap: PenLineCap.Round), g);
        }
        if (HasOuter) Ring(rOuter, Outer, OuterColor, Thickness);
        Ring(rInner, Inner, InnerColor, HasOuter ? Thickness : Thickness + 2);
    }
}

// "Dual ring": half width like Ring, two can sit side by side; the value sits beside the rings.
public sealed class DualRingTile : SummaryTile
{
    private readonly DualRing rings; private readonly Hero hero; private readonly TextBlock legend;

    public DualRingTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s) : base(kind, index, p, m, s, true)
    {
        double size = m.Subtitles ? 64 : 54;
        rings = new DualRing { Width = size, Height = size, Palette = p, Thickness = m.Subtitles ? 6 : 5, VerticalAlignment = VerticalAlignment.Center };
        hero = new Hero(m.Subtitles ? 26 : 22, p);
        legend = Ui.Text("", 10.5, p.Soft, numbers: true);
        legend.TextTrimming = TextTrimming.CharacterEllipsis;
        Caption.TextTrimming = TextTrimming.CharacterEllipsis;
        var side = Ui.Stack(1, hero, legend);
        side.VerticalAlignment = VerticalAlignment.Center;
        side.Margin = new Thickness(10, 0, 0, 0);
        var row = Ui.Columns("Auto,*", rings, side);
        Grid.SetColumn(side, 1);
        row.Margin = new Thickness(0, 4, 0, 0);
        var body = new StackPanel { Spacing = 4, Tag = "elastic" };
        body.Children.Add(Head);
        body.Children.Add(row);
        body.Children.Add(Caption);
        body.Children.Add(Extras);
        Child = body;
    }

    protected override void Show(Summary sum)
    {
        hero.Set(sum.Value, sum.Unit, sum.Color);
        rings.Animate(sum.Second, sum.Fraction, P.Cool, Shade(sum));
        legend.IsVisible = sum.Second is not null;
        legend.Text = sum.Second is not null ? $"{L.T("Load")} {sum.SecondLabel}" : "";
        Caption.IsVisible = sum.Second is null;
    }
}
