using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

/// <summary>Colours and surfaces of the Panel designs. The accent replaces the classic red; everything stays dark.</summary>
public sealed record PanelTheme(string Key, Color Accent, Color Hot, Color Section, Color Border, Color Side, string Surface, double Radius, bool Stripe, bool DarkOnHot)
{
    public static FontFamily Font => Ui.DesignFont(Ui.Orbitron);
    public Color Grid => Palette.Mix(Color.Parse("#0A0A0B"), Accent, .26);
    public Color Soft => Accent; // one colour throughout, no lighter (pink) variant (user request)
    public Color Dim => Palette.Mix(Color.Parse("#0A0A0B"), Accent, .14);

    public static PanelTheme For(string? key, Color? own = null)
    {
        var t = key switch
        {
            "cyan" => new PanelTheme("cyan", C("#00D9FF"), C("#00D9FF"), C("#0A1118"), C("#1C3A4A"), C("#070B10"), "plain", 8, false, false),
            "purple" => new PanelTheme("purple", C("#B44DFF"), C("#B44DFF"), C("#0E0B14"), C("#3A2650"), C("#09070D"), "glow", 10, false, false),
            "gold" => new PanelTheme("gold", C("#F2B33D"), C("#F2B33D"), C("#100E0A"), C("#4A3B1C"), C("#0A0907"), "gold", 3, false, true),
            "carbon" => new PanelTheme("carbon", C("#FF7A1A"), C("#FF7A1A"), C("#121214"), C("#7A4214"), C("#0A0A0B"), "carbon", 0, false, false),
            "glass" => new PanelTheme("glass", C("#FF3B5C"), C("#FF3B5C"), C("#8C140E12"), C("#33FFFFFF"), C("#59100A0E"), "glass", 12, false, false),
            "lime" => new PanelTheme("lime", C("#9DFF2E"), C("#9DFF2E"), C("#0D100B"), C("#2C3A22"), C("#080A07"), "plain", 4, true, true),
            "silver" => new PanelTheme("silver", C("#CFE3FF"), C("#E8F1FF"), C("#0F1114"), C("#353A42"), C("#0A0B0D"), "plain", 6, false, true),
            _ => new PanelTheme("red", C("#FF2238"), C("#FF1F4F"), C("#0E0E0F"), C("#3A3A3D"), C("#0B0B0C"), "plain", 3, false, false)
        };
        // A colour the user picked for this card replaces the set's accent (the surfaces stay).
        return own is { } c ? t with { Accent = c, Hot = c } : t;
    }
    private static Color C(string hex) => Color.Parse(hex);
}

internal static class PanelDraw
{
    public static IBrush B(Color c) => new SolidColorBrush(c);
    public static Pen P(Color c, double w) => new(B(c), w);
    public static Color A(Color c, double a) => Color.FromArgb((byte)Math.Round(Math.Clamp(a, 0, 1) * 255), c.R, c.G, c.B);
    public static Point Polar(Point c, double r, double a) => new(c.X + r * Math.Cos(a), c.Y + r * Math.Sin(a));
    public static void Glow(DrawingContext g, Point center, double radius, Color color, double strength = .45)
    {
        var brush = new RadialGradientBrush { GradientStops = { new(A(color, strength), 0), new(A(color, strength * .45), .55), new(A(color, 0), 1) } };
        g.DrawEllipse(brush, null, center, radius, radius);
    }
    public static StreamGeometry Arc(Point c, double r, double from, double sweep)
    {
        var geometry = new StreamGeometry();
        sweep = Math.Clamp(sweep, 0.0001, Math.PI * 2 - .0001);
        using var s = geometry.Open();
        s.BeginFigure(Polar(c, r, from), false);
        s.ArcTo(Polar(c, r, from + sweep), new Size(r, r), 0, sweep > Math.PI, SweepDirection.Clockwise);
        s.EndFigure(false);
        return geometry;
    }
    public static void Text(DrawingContext g, string text, double size, Color color, Point at, bool center = false, FontWeight weight = FontWeight.Bold)
    {
        var f = new FormattedText(text, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(Ui.GraphDesignFont(PanelTheme.Font), FontStyle.Normal, weight), size, B(color));
        g.DrawText(f, center ? new Point(at.X - f.Width / 2, at.Y - f.Height / 2) : at);
    }
}

/// <summary>The gauge drawing of a Panel value (12 forms). Its numbers are separate live text placed by the layout.</summary>
public sealed class PanelGauge : Control
{
    public string Mode { get; init; } = "dots";
    public PanelTheme Theme { get; init; } = PanelTheme.For("red");
    /// <summary>A second value (inner ring of the double ring).</summary>
    public bool Twin { get; init; }
    private double? first, second;
    public bool Hot => (first ?? 0) >= .9 || (second ?? 0) >= .9;
    public void Set(double? value, double? other = null)
    {
        if (Equals(first, value) && Equals(second, other)) return;
        first = value is double v && double.IsFinite(v) ? Math.Clamp(v, 0, 1) : null;
        second = other is double o && double.IsFinite(o) ? Math.Clamp(o, 0, 1) : null;
        InvalidateVisual();
    }
    public override void Render(DrawingContext g)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 4 || h < 4) return;
        var t = Theme; double f = first ?? 0; bool hot = Hot;
        Color accent = t.Accent, off = Color.Parse("#2A2A2E"), hotFill = t.Hot;
        var center = new Point(w / 2, h / 2); double r = Math.Min(w, h) / 2 - 2;
        switch (Mode)
        {
            case "segments":
            {
                if (hot) PanelDraw.Glow(g, center, r * 1.15, hotFill, .35);
                int n = 24;
                for (int k = 0; k < n; k++)
                {
                    double a0 = -Math.PI / 2 + k / (double)n * Math.PI * 2 + .035, a1 = a0 + Math.PI * 2 / n - .09;
                    var seg = new StreamGeometry();
                    using (var s = seg.Open())
                    {
                        s.BeginFigure(PanelDraw.Polar(center, r * .7, a0), true);
                        s.LineTo(PanelDraw.Polar(center, r, a0));
                        s.ArcTo(PanelDraw.Polar(center, r, a1), new Size(r, r), 0, false, SweepDirection.Clockwise);
                        s.LineTo(PanelDraw.Polar(center, r * .7, a1));
                        s.ArcTo(PanelDraw.Polar(center, r * .7, a0), new Size(r * .7, r * .7), 0, false, SweepDirection.CounterClockwise);
                        s.EndFigure(true);
                    }
                    g.DrawGeometry(PanelDraw.B(k / (double)n < f ? accent : Color.Parse("#26262A")), null, seg);
                }
                break;
            }
            case "twin":
            {
                if (hot) PanelDraw.Glow(g, center, r * 1.12, hotFill, .3);
                double ro = r - 4, ri = r - 15, sw = Math.Max(4, r * .16);
                g.DrawEllipse(null, PanelDraw.P(Color.Parse("#26262A"), sw), center, ro, ro);
                g.DrawEllipse(null, PanelDraw.P(Color.Parse("#26262A"), sw), center, ri, ri);
                if (first is double a) g.DrawGeometry(null, new Pen(PanelDraw.B(accent), sw, lineCap: PenLineCap.Round), PanelDraw.Arc(center, ro, -Math.PI / 2, a * Math.PI * 2));
                if (second is double b) g.DrawGeometry(null, new Pen(PanelDraw.B(b >= .9 ? t.Soft : Color.Parse("#E8E8EA")), sw, lineCap: PenLineCap.Round), PanelDraw.Arc(center, ri, -Math.PI / 2, b * Math.PI * 2));
                break;
            }
            case "hex":
            {
                var hex = new StreamGeometry();
                using (var s = hex.Open())
                {
                    for (int k = 0; k < 6; k++) { var p = PanelDraw.Polar(center, r, Math.PI / 3 * k); if (k == 0) s.BeginFigure(p, true); else s.LineTo(p); }
                    s.EndFigure(true);
                }
                g.DrawGeometry(PanelDraw.B(Color.Parse("#151516")), null, hex);
                using (g.PushGeometryClip(hex))
                    g.DrawRectangle(PanelDraw.B(PanelDraw.A(accent, hot ? .75 : .35)), null, new Rect(0, center.Y + r - 2 * r * f, w, 2 * r * f + 1));
                g.DrawGeometry(null, PanelDraw.P(accent, 1.5), hex);
                break;
            }
            case "rays":
            {
                if (hot) PanelDraw.Glow(g, center, r * .9, hotFill, .4);
                int n = 48;
                for (int k = 0; k < n; k++)
                {
                    double a = -Math.PI / 2 + k / (double)n * Math.PI * 2; bool on = k / (double)n < f;
                    g.DrawLine(new Pen(PanelDraw.B(on ? accent : Color.Parse("#2E2E32")), Math.Max(1.4, r * .045), lineCap: PenLineCap.Round),
                        PanelDraw.Polar(center, r * .6, a), PanelDraw.Polar(center, on ? r : r * .86, a));
                }
                break;
            }
            case "diamonds":
            {
                var pts = new[] { new Point(center.X, center.Y - r), new Point(center.X + r, center.Y), new Point(center.X, center.Y + r), new Point(center.X - r, center.Y), new Point(center.X, center.Y - r) };
                var frame = new StreamGeometry();
                using (var s = frame.Open()) { s.BeginFigure(pts[0], true); for (int k = 1; k < 4; k++) s.LineTo(pts[k]); s.EndFigure(true); }
                g.DrawGeometry(PanelDraw.B(hot ? PanelDraw.A(hotFill, .2) : Color.Parse("#151516")), PanelDraw.P(Color.Parse("#2E2E32"), r * .13), frame);
                var progress = new StreamGeometry(); double p4 = f * 4;
                using (var s = progress.Open())
                {
                    s.BeginFigure(pts[0], false);
                    for (int k = 0; k < 4 && p4 > k; k++) { double q = Math.Min(1, p4 - k); s.LineTo(new Point(pts[k].X + (pts[k + 1].X - pts[k].X) * q, pts[k].Y + (pts[k + 1].Y - pts[k].Y) * q)); }
                    s.EndFigure(false);
                }
                g.DrawGeometry(null, new Pen(PanelDraw.B(accent), r * .13, lineJoin: PenLineJoin.Round), progress);
                break;
            }
            case "grid":
            {
                double cell = Math.Min(w, h) / 10;
                for (int k = 0; k < 100; k++)
                {
                    bool on = 99 - k < f * 100;
                    g.DrawRectangle(PanelDraw.B(on ? accent : Color.Parse("#232326")), null, new Rect(k % 10 * cell, k / 10 * cell, cell * .8, cell * .8), 1, 1);
                }
                break;
            }
            case "leds":
            {
                int n = 30; double gap = w / n;
                for (int k = 0; k < n; k++)
                    g.DrawRectangle(PanelDraw.B(k / (double)n < f ? (hot && k > n * .82 ? t.Soft : accent) : Color.Parse("#222225")), null, new Rect(k * gap, 0, gap * .74, h), 1, 1);
                break;
            }
            case "columns":
            {
                int n = 14; double step = h / n, dot = Math.Min(w, step) * .36;
                if (hot) g.DrawRectangle(PanelDraw.B(PanelDraw.A(hotFill, .16)), null, new Rect(w / 2 - dot * 3, 0, dot * 6, h), 4, 4);
                for (int k = 0; k < n; k++)
                    g.DrawEllipse(PanelDraw.B(k / (double)n < f ? accent : Color.Parse("#2A2A2E")), null, new Point(w / 2, h - step * (k + .5)), dot, dot);
                break;
            }
            case "tubes":
            {
                double tw = Math.Min(w * .5, 20), x = (w - tw) / 2;
                if (hot) g.DrawRectangle(PanelDraw.B(PanelDraw.A(hotFill, .25)), null, new Rect(x - 4, 0, tw + 8, h), tw / 2 + 4, tw / 2 + 4);
                g.DrawRectangle(PanelDraw.B(Color.Parse("#1A1A1C")), PanelDraw.P(Color.Parse("#2E2E32"), 1), new Rect(x, 0, tw, h), tw / 2, tw / 2);
                double inner = tw - 6, fill = (h - 6) * f;
                if (fill > 1) g.DrawRectangle(PanelDraw.B(accent), null, new Rect(x + 3, h - 3 - fill, inner, fill), inner / 2, inner / 2);
                break;
            }
            case "tiles":
            {
                var box = new Rect(0, 0, w, h);
                g.DrawRectangle(PanelDraw.B(hot ? hotFill : Color.Parse("#151516")), PanelDraw.P(hot ? t.Soft : Color.Parse("#2E2E32"), 1), box);
                double y = h - 9, x0 = 8, len = w - 16;
                g.DrawRectangle(PanelDraw.B(hot ? PanelDraw.A(Colors.White, .4) : Color.Parse("#2A2A2E")), null, new Rect(x0, y, len, 3.5));
                g.DrawRectangle(PanelDraw.B(hot ? Colors.White : accent), null, new Rect(x0, y, len * f, 3.5));
                break;
            }
            default: // dots (and the compact strip)
            {
                double ring = r * .8;
                if (hot) PanelDraw.Glow(g, center, r * 1.25, hotFill, .55);
                for (int i = 0; i < 60; i++)
                {
                    double a = i / 60.0 * Math.PI * 2, r0 = ring + r * .08, r1 = ring + (i % 5 == 0 ? r * .2 : r * .15);
                    if (Mode == "strip" && i % 5 != 0) continue;
                    g.DrawLine(PanelDraw.P(hot ? t.Soft : Color.Parse("#6A6A70"), i % 5 == 0 ? 1.4 : .9), PanelDraw.Polar(center, r0, a), PanelDraw.Polar(center, r1, a));
                }
                g.DrawEllipse(PanelDraw.B(hot ? hotFill : Color.Parse("#151516")), PanelDraw.P(hot ? t.Soft : Color.Parse("#8A8A90"), Math.Max(1.5, r * .06)), center, ring, ring);
                for (int band = 0; band < 4; band++)
                {
                    double rr = ring * (.84 - band * .115); int n = (int)Math.Max(8, Math.Round(rr * 1.15));
                    double dot = Math.Max(.7, ring * .03);
                    for (int i = 0; i < n; i++)
                    {
                        double a = -Math.PI / 2 + i / (double)n * Math.PI * 2; bool on = i / (double)n < f;
                        var c = hot ? (on ? Colors.White : PanelDraw.A(t.Soft, .55)) : on ? accent : Color.Parse("#4A4A4F");
                        g.DrawEllipse(PanelDraw.B(c), null, PanelDraw.Polar(center, rr, a), dot, dot);
                    }
                }
                g.DrawEllipse(PanelDraw.B(hot ? hotFill : Color.Parse("#151516")), null, center, ring * .38, ring * .38);
                break;
            }
        }
    }
}

/// <summary>A history graph in a red grid (the panel's "graph paper").</summary>
public sealed class PanelGraph : Control
{
    public PanelTheme Theme { get; init; } = PanelTheme.For("red");
    public int Cells { get; init; } = 6;
    private double[] values = Array.Empty<double>();
    private double maximum = 100;
    public void Set(double[] series, double max) { values = series; maximum = max; InvalidateVisual(); }
    public override void Render(DrawingContext g)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 4 || h < 4) return;
        g.DrawRectangle(PanelDraw.B(Color.Parse("#0A0A0A")), PanelDraw.P(Color.Parse("#4A4A4E"), .8), new Rect(.4, .4, w - .8, h - .8));
        var line = PanelDraw.P(Theme.Grid, .7);
        for (int i = 1; i < Cells; i++) g.DrawLine(line, new Point(w * i / Cells, 1), new Point(w * i / Cells, h - 1));
        for (int i = 1; i < 4; i++) g.DrawLine(line, new Point(1, h * i / 4), new Point(w - 1, h * i / 4));
        var data = values.Where(double.IsFinite).ToArray(); if (data.Length < 2) return;
        double max = Math.Max(maximum, data.Max() * 1.05);
        var stroke = new StreamGeometry(); var area = new StreamGeometry();
        using (var s = stroke.Open()) using (var a = area.Open())
        {
            a.BeginFigure(new Point(1, h - 1), true);
            for (int i = 0; i < data.Length; i++)
            {
                var p = new Point(1 + (w - 2) * i / (data.Length - 1), h - 1 - (h - 3) * Math.Clamp(data[i] / max, 0, 1));
                if (i == 0) s.BeginFigure(p, false); else s.LineTo(p);
                a.LineTo(p);
            }
            s.EndFigure(false); a.LineTo(new Point(w - 1, h - 1)); a.EndFigure(true);
        }
        g.DrawGeometry(PanelDraw.B(PanelDraw.A(Theme.Accent, .18)), null, area);
        g.DrawGeometry(null, PanelDraw.P(Theme.Accent, 1.2), stroke);
    }
}

/// <summary>Grey bar (free space, used memory) and LED strip (header variant).</summary>
public sealed class PanelBar : Control
{
    public PanelTheme Theme { get; init; } = PanelTheme.For("red");
    public bool Leds { get; init; }
    private double fraction;
    public void Set(double? value) { var v = value is double d && double.IsFinite(d) ? Math.Clamp(d, 0, 1) : 0; if (v == fraction) return; fraction = v; InvalidateVisual(); }
    public override void Render(DrawingContext g)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 4 || h < 2) return;
        if (!Leds)
        {
            g.DrawRectangle(PanelDraw.B(Color.Parse("#2C2C2F")), null, new Rect(0, 0, w, h));
            g.DrawRectangle(PanelDraw.B(Color.Parse("#8E8E93")), null, new Rect(0, 0, w * fraction, h));
            return;
        }
        int n = 22; double step = w / n;
        for (int k = 0; k < n; k++)
            g.DrawRectangle(PanelDraw.B(k / (double)n < fraction ? (fraction >= .9 ? Theme.Soft : Theme.Accent) : Color.Parse("#232326")), null, new Rect(k * step, 0, step - 1.6, h), 1, 1);
    }
}

/// <summary>Usage per core as small boxes filled from the bottom.</summary>
public sealed class PanelCoreCells : Control
{
    public PanelTheme Theme { get; init; } = PanelTheme.For("red");
    private IReadOnlyList<double> loads = Array.Empty<double>();
    public IReadOnlyList<double> Loads { get => loads; set { loads = value; InvalidateVisual(); InvalidateMeasure(); } }
    protected override Size MeasureOverride(Size available)
    {
        double w = double.IsFinite(available.Width) ? available.Width : 220;
        int perRow = Math.Clamp(loads.Count, 1, 8), rows = (loads.Count + perRow - 1) / perRow;
        return new Size(w, Math.Max(1, rows) * 24);
    }
    public override void Render(DrawingContext g)
    {
        if (loads.Count == 0) return;
        int perRow = Math.Clamp(loads.Count, 1, 8); double w = Bounds.Width, cell = w / perRow;
        for (int i = 0; i < loads.Count; i++)
        {
            double x = i % perRow * cell, y = i / perRow * 24; var box = new Rect(x, y, cell - 4, 20);
            g.DrawRectangle(PanelDraw.B(Color.Parse("#0A0A0A")), PanelDraw.P(Color.Parse("#4A4A4E"), .8), box);
            g.DrawLine(PanelDraw.P(Theme.Grid, .6), new Point(x, y + 10), new Point(x + cell - 4, y + 10));
            double v = Math.Clamp(loads[i] / 100, 0, 1), height = Math.Max(v > 0 ? 1.5 : 0, 18 * v);
            if (height > 0) g.DrawRectangle(PanelDraw.B(PanelDraw.A(Theme.Accent, .9)), null, new Rect(x + 1, y + 19 - height, cell - 6, height));
        }
    }
}

/// <summary>Drawn hardware pictures for the left column of a Panel section (no product photos, no logos).</summary>
public sealed class PanelArt : Control
{
    public string Kind { get; init; } = "cpu";
    public PanelTheme Theme { get; init; } = PanelTheme.For("red");
    private static readonly Dictionary<string, Size> Boxes = new()
    {
        ["cpu"] = new(96, 80), ["gpu"] = new(104, 64), ["ssd"] = new(96, 64), ["ram"] = new(100, 70), ["aio"] = new(96, 80), ["net"] = new(104, 76), ["psu"] = new(96, 72)
    };
    protected override Size MeasureOverride(Size available)
    {
        var box = Boxes.GetValueOrDefault(Kind, new Size(96, 80));
        double w = double.IsFinite(available.Width) ? Math.Min(available.Width, box.Width) : box.Width;
        return new Size(w, w * box.Height / box.Width);
    }
    private IBrush Rgb => new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
        GradientStops = { new(Color.Parse("#FF2238"), 0), new(Color.Parse("#FF9A1F"), .33), new(Color.Parse("#2DD4BF"), .66), new(Color.Parse("#A78BFA"), 1) }
    };
    public override void Render(DrawingContext g)
    {
        var box = Boxes.GetValueOrDefault(Kind, new Size(96, 80));
        double s = Math.Min(Bounds.Width / box.Width, Bounds.Height / box.Height); if (s <= 0) return;
        using var _ = g.PushTransform(Matrix.CreateScale(s, s) * Matrix.CreateTranslation((Bounds.Width - box.Width * s) / 2, (Bounds.Height - box.Height * s) / 2));
        var a = Theme.Accent; var line = Color.Parse("#4A4A4E"); var light = Color.Parse("#E8E8E8");
        void R(double x, double y, double w, double h, Color? fill, Color? stroke = null, double sw = 1, double rad = 0)
            => g.DrawRectangle(fill is { } f ? PanelDraw.B(f) : null, stroke is { } k ? PanelDraw.P(k, sw) : null, new Rect(x, y, w, h), rad, rad);
        void L(double x1, double y1, double x2, double y2, Color c, double w = 1) => g.DrawLine(PanelDraw.P(c, w), new Point(x1, y1), new Point(x2, y2));
        void Fan(double cx, double cy, double r, bool rgb)
        {
            var c = new Point(cx, cy);
            if (rgb) g.DrawEllipse(null, new Pen(Rgb, 2.5), c, r + 1.5, r + 1.5);
            g.DrawEllipse(PanelDraw.B(Color.Parse("#0C0C0D")), PanelDraw.P(Color.Parse("#3A3A3E"), 1), c, r, r);
            for (int k = 0; k < 8; k++)
            {
                double ang = k * Math.PI / 4;
                var blade = new StreamGeometry();
                using (var b = blade.Open())
                {
                    b.BeginFigure(c, true);
                    b.QuadraticBezierTo(PanelDraw.Polar(c, r * .7, ang - .5), PanelDraw.Polar(c, r * .88, ang + .1));
                    b.QuadraticBezierTo(PanelDraw.Polar(c, r * .45, ang + .2), c);
                    b.EndFigure(true);
                }
                g.DrawGeometry(PanelDraw.B(Color.Parse("#26262A")), PanelDraw.P(Color.Parse("#3C3C40"), .4), blade);
            }
            g.DrawEllipse(PanelDraw.B(Color.Parse("#151517")), PanelDraw.P(a, .8), c, r * .28, r * .28);
        }
        switch (Kind)
        {
            case "cpu":
                for (int i = 0; i < 7; i++)
                {
                    double p = 26 + i * 7.5, q = 18 + i * 7.5;
                    L(p, 10, p, 4, light, 1.1); L(p, 70, p, 76, light, 1.1); L(16, q, 10, q, light, 1.1); L(80, q, 86, q, light, 1.1);
                }
                L(20, 26, 8, 26, a); L(8, 26, 8, 20, a); L(20, 52, 6, 52, a); L(6, 52, 6, 60, a); L(76, 30, 90, 30, a); L(76, 50, 88, 50, a); L(88, 50, 88, 58, a);
                R(18, 14, 60, 52, Color.Parse("#1B1B1D"), Color.Parse("#D8D8D8"), 1.3, 3);
                R(26, 22, 44, 36, Color.Parse("#2A2A2D"), a, 1, 2);
                g.DrawRectangle(new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                    GradientStops = { new(Color.Parse("#E9E9EA"), 0), new(Color.Parse("#B9BBBF"), .5), new(Color.Parse("#8F9297"), 1) } }, null, new Rect(28, 24, 40, 32), 2, 2);
                PanelDraw.Text(g, "CORE", 8, Color.Parse("#141416"), new Point(48, 38), true, FontWeight.Black);
                break;
            case "gpu":
            {
                var body = StreamGeometry.Parse("M4,14 L96,10 L100,46 L8,52 Z");
                g.DrawGeometry(PanelDraw.B(Color.Parse("#202023")), PanelDraw.P(line, 1), body);
                g.DrawGeometry(PanelDraw.B(Color.Parse("#2C2C30")), null, StreamGeometry.Parse("M4,14 L96,10 L97,18 L5,22 Z"));
                L(60, 12, 92, 10.5, a, 2);
                Fan(24, 34, 12, false); Fan(51, 32, 12, false); Fan(78, 31, 12, false);
                g.DrawGeometry(PanelDraw.B(Color.Parse("#111111")), null, StreamGeometry.Parse("M8,52 L100,46 L100,50 L9,56 Z"));
                L(14, 54, 40, 52.5, Color.Parse("#D4A74A"), 2.5);
                break;
            }
            case "ssd":
                R(6, 18, 84, 28, Color.Parse("#141416"), line, 1, 2);
                R(10, 22, 16, 20, Color.Parse("#26262A"), a, .8, 1);
                R(30, 22, 16, 20, Color.Parse("#26262A"), null); R(50, 22, 16, 20, Color.Parse("#26262A"), null);
                R(70, 24, 12, 16, Color.Parse("#1D1D20"), null);
                R(86, 26, 6, 12, Color.Parse("#C9A24A"), null);
                L(10, 50, 60, 50, a, 2);
                PanelDraw.Text(g, "NVMe", 6, Color.Parse("#BDBDC2"), new Point(48, 12), true);
                break;
            case "ram":
                for (int k = 0; k < 2; k++)
                {
                    double ox = 4 + k * 10, oy = 10 + k * 16;
                    g.DrawRectangle(Rgb, null, new Rect(ox, oy, 80, 6), 1.5, 1.5);
                    R(ox, oy + 6, 80, 20, Color.Parse("#2B2B2F"), line);
                    L(ox, oy + 6, ox + 30, oy + 6, Color.Parse("#8A8A90")); L(ox + 30, oy + 6, ox + 36, oy + 14, Color.Parse("#8A8A90")); L(ox + 36, oy + 14, ox + 80, oy + 14, Color.Parse("#8A8A90"));
                    R(ox + 4, oy + 26, 72, 4, Color.Parse("#C9A24A"), null);
                }
                break;
            case "aio":
                R(10, 4, 34, 72, Color.Parse("#1A1A1C"), line, 1, 3);
                Fan(27, 22, 13, true); Fan(27, 56, 13, true);
                g.DrawGeometry(null, PanelDraw.P(Color.Parse("#2A2A2E"), 4), StreamGeometry.Parse("M44,20 C60,20 62,34 66,40 M44,26 C56,28 58,40 62,44"));
                g.DrawEllipse(PanelDraw.B(Color.Parse("#151517")), PanelDraw.P(Color.Parse("#8A8A90"), 1.5), new Point(72, 52), 16, 16);
                g.DrawEllipse(null, new Pen(Rgb, 3), new Point(72, 52), 12, 12);
                break;
            case "psu":
                R(6, 10, 84, 54, Color.Parse("#1C1C1F"), line, 1, 3);
                g.DrawEllipse(PanelDraw.B(Color.Parse("#0C0C0D")), PanelDraw.P(Color.Parse("#3A3A3E"), 1), new Point(44, 37), 21, 21);
                for (int k = 0; k < 6; k++) { double ang = k * Math.PI / 3; L(44 + 6 * Math.Cos(ang), 37 + 6 * Math.Sin(ang), 44 + 19 * Math.Cos(ang), 37 + 19 * Math.Sin(ang), Color.Parse("#3A3A3E"), 1.2); }
                R(72, 18, 12, 38, Color.Parse("#26262A"), null, 1, 2);
                for (int k = 0; k < 4; k++) R(74, 22 + k * 8, 8, 4, k == 0 ? a : Color.Parse("#3A3A3E"), null);
                break;
            case "net":
            {
                R(4, 6, 66, 42, Color.Parse("#0A0A0B"), line, 2, 2);
                g.DrawRectangle(new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                    GradientStops = { new(Palette.Mix(Colors.Black, a, .25), 0), new(PanelDraw.A(a, .85), .6), new(Palette.Mix(Colors.Black, a, .15), 1) } }, null, new Rect(7, 9, 60, 36));
                g.DrawGeometry(PanelDraw.B(Color.Parse("#2A2A2E")), null, StreamGeometry.Parse("M32,48 L42,48 L44,60 L30,60 Z"));
                R(22, 60, 30, 3, Color.Parse("#3A3A3E"), null, 1, 1);
                g.DrawGeometry(PanelDraw.B(Color.Parse("#1D1D20")), PanelDraw.P(line, 1), StreamGeometry.Parse("M66,44 L98,40 L100,64 L68,68 Z"));
                L(68, 66, 100, 62, a, 2);
                L(72, 44, 68, 22, Color.Parse("#3A3A3E"), 2.5); L(82, 43, 80, 18, Color.Parse("#3A3A3E"), 2.5); L(92, 41, 96, 20, Color.Parse("#3A3A3E"), 2.5);
                foreach (var x in new[] { 76.0, 82, 88 }) g.DrawEllipse(PanelDraw.B(a), null, new Point(x, 58 - (x - 76) / 6), 1.2, 1.2);
                break;
            }
        }
    }
}
