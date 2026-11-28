using Avalonia.Controls;
using Avalonia;
using Avalonia.Media;

namespace Lumen.App;

/// <summary>Vital Signs (5.9.0): a heart-rate trace; its beats come faster and higher with the reading.</summary>
public sealed class ArtEcg : ArtDecoration
{
    private double? level; private readonly int seed;
    public ArtEcg(Color colour, int seed) : base(colour) { this.seed = seed; ClipToBounds = true; }
    public void Set(double? value) { level = value; InvalidateVisual(); }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 40 || h < 16) return;
        double f = Math.Clamp(level ?? 0, 0, 1), beats = 4 + f * 6, amp = .25 + .2 * f;
        var line = new StreamGeometry();
        using (var s = line.Open())
        {
            const int n = 360;
            for (int i = 0; i < n; i++)
            {
                double x = i * w / (n - 1), ph = (i / (double)n * beats + seed * .13) % 1;
                double y = ph switch { > .48 and < .52 => -amp * 2.1, >= .52 and < .56 => amp * .7, < .3 => -.06 * Math.Sin(ph * Math.PI * 6), _ => 0 };
                var p = new Point(x, h * (.55 + y * .9));
                if (i == 0) s.BeginFigure(p, false); else s.LineTo(p);
            }
        }
        c.DrawGeometry(null, new Pen(BlackDraw.B(BlackDraw.A(Color, .28)), 7) { LineJoin = PenLineJoin.Round }, line);
        c.DrawGeometry(null, new Pen(BlackDraw.B(Color), 2.4) { LineJoin = PenLineJoin.Round }, line);
    }
}

/// <summary>Network Globe (5.9.0): a dotted globe with data arcs leaving the centre: download arcs in the first colour, upload
/// arcs in the second; more traffic, more arcs.</summary>
public sealed class ArtGlobe : ArtDecoration
{
    private readonly Color upColour; private double down, up;
    public ArtGlobe(Color downColour, Color upColour) : base(downColour) { this.upColour = upColour; }
    public void Set(double? downFraction, double? upFraction) { down = Math.Clamp(downFraction ?? 0, 0, 1); up = Math.Clamp(upFraction ?? 0, 0, 1); InvalidateVisual(); }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height, r = Math.Min(w, h) / 2 - 6; if (r < 30) return;
        var centre = new Point(w / 2, h / 2);
        c.DrawEllipse(new RadialGradientBrush { GradientStops = { new(Color.FromArgb(60, Color.R, Color.G, Color.B), 0), new(Color.FromArgb(10, 0, 0, 0), 1) } },
            new Pen(BlackDraw.B(BlackDraw.A(Color, .25)), 1.5), centre, r, r);
        var rnd = new Random(3);
        for (int i = 0; i < 900; i++)
        {
            double lat = Math.Asin(rnd.NextDouble() * 2 - 1), lon = rnd.NextDouble() * Math.PI * 2 - Math.PI;
            double x = Math.Cos(lat) * Math.Sin(lon), z = Math.Cos(lat) * Math.Cos(lon);
            if (z < 0) continue;
            bool land = Math.Sin(lon * 3 + 1) + Math.Cos(lat * 4) > .3;
            c.DrawEllipse(BlackDraw.B(BlackDraw.A(land ? Color : Palette.Mix(Color, Colors.Black, .6), .3 + z * .6)), null, new Point(centre.X + x * r, centre.Y - Math.Sin(lat) * r), land ? r * .008 + 1 : r * .004 + .5, land ? r * .008 + 1 : r * .004 + .5);
        }
        int downs = 1 + (int)Math.Round(down * 5), ups = 1 + (int)Math.Round(up * 4);
        double[] angles = { -2.6, -.9, .2, 1.3, 2.3, -1.8, .8, 2.8, -.3 };
        for (int k = 0; k < downs + ups; k++)
        {
            double a = angles[k % angles.Length], d = r * (.55 + .4 * ((k * 37) % 10) / 10.0);
            var end = new Point(centre.X + Math.Cos(a) * d, centre.Y + Math.Sin(a) * d * .8);
            var mid = new Point((centre.X + end.X) / 2, Math.Min(centre.Y, end.Y) - r * .45);
            var arc = new StreamGeometry();
            using (var s = arc.Open()) { s.BeginFigure(centre, false); s.QuadraticBezierTo(mid, end); }
            var colour = k < downs ? Color : upColour;
            c.DrawGeometry(null, new Pen(BlackDraw.B(BlackDraw.A(colour, .9)), 2.2) { DashStyle = new DashStyle(new double[] { 3, 4 }, 0) }, arc);
            c.DrawEllipse(BlackDraw.B(colour), null, end, 4, 4);
        }
        c.DrawEllipse(BlackDraw.B(Colors.White), null, centre, 6, 6);
    }
}

/// <summary>Ambient Clock (5.9.0): the time of day, drawn large and thin (the clock of the computer, not a sensor).</summary>
public sealed class ArtClock : ArtDecoration
{
    private static readonly Typeface Face = new(Ui.Inter, FontStyle.Normal, FontWeight.Light);
    private static readonly Typeface Small = new(Ui.Inter, FontStyle.Normal, FontWeight.SemiBold);
    private readonly Color label;
    public ArtClock(Color ink, Color label) : base(ink) { this.label = label; }
    public void Tick() => InvalidateVisual();
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 60 || h < 30) return;
        var now = DateTime.Now;
        var time = new FormattedText(now.ToString("HH:mm"), System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Face, 100, BlackDraw.B(Color));
        double scale = Math.Min(w * .92 / time.Width, h * .74 / time.Height);
        time = new FormattedText(now.ToString("HH:mm"), System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Face, 100 * scale, BlackDraw.B(Color));
        c.DrawText(time, new Point((w - time.Width) / 2, (h - time.Height) / 2 - h * .06));
        var date = new FormattedText(now.ToString("dddd · d MMMM", System.Globalization.CultureInfo.InvariantCulture).ToUpperInvariant(), System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Small, Math.Clamp(h * .07, 12, 26), BlackDraw.B(label));
        c.DrawText(date, new Point((w - date.Width) / 2, (h + time.Height) / 2 - h * .02));
    }
}

/// <summary>EV Console (5.9.0): the case seen from above like a car on a screen; every part a box in the colour of its temperature,
/// named but without numbers.</summary>
public sealed class ArtCaseTop : ArtDecoration
{
    private static readonly Typeface Face = new(Ui.Inter, FontStyle.Normal, FontWeight.Bold);
    private readonly List<(string Name, Rect Box, Color Heat)> parts = new();
    public ArtCaseTop() : base(Colors.White) { }
    public int Add(string name, Rect box) { parts.Add((name, box, HeatColour.Of(null))); return parts.Count - 1; }
    public void Set(int index, double? celsius) { parts[index] = parts[index] with { Heat = HeatColour.Of(celsius) }; InvalidateVisual(); }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 60 || h < 60) return;
        // In a wide, low space the case lies on its side: the same layout, turned (x and y swapped).
        bool lying = w > h * 1.2;
        double cw, ch;
        if (lying) { ch = h - 8; cw = ch * 1.56; if (cw > w - 8) { cw = w - 8; ch = cw / 1.56; } }
        else { cw = Math.Min(w * .8, h * .64); ch = cw * 1.56; if (ch > h - 8) { ch = h - 8; cw = ch / 1.56; } }
        var body = new Rect((w - cw) / 2, (h - ch) / 2, cw, ch);
        c.DrawRectangle(BlackDraw.V(Color.Parse("#252A31"), Color.Parse("#16191E")), new Pen(BlackDraw.B(Color.Parse("#3A424C")), 2), body, cw * .12, cw * .12);
        var inside = body.Deflate(cw * .08);
        c.DrawRectangle(BlackDraw.B(Color.Parse("#0B0E12")), null, inside, cw * .08, cw * .08);
        double size = Math.Clamp(Math.Min(cw, ch) * .07, 10, 18);
        foreach (var (name, box, heat) in parts)
        {
            var b = lying ? new Rect(box.Y, box.X, box.Height, box.Width) : box;
            var r = new Rect(inside.X + b.X * inside.Width, inside.Y + b.Y * inside.Height, b.Width * inside.Width, b.Height * inside.Height);
            c.DrawRectangle(BlackDraw.B(BlackDraw.A(heat, .16)), new Pen(BlackDraw.B(heat), 2), r, 8, 8);
            var text = new FormattedText(name, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Face, size, BlackDraw.B(heat));
            if (text.Width < r.Width - 6) c.DrawText(text, new Point(r.X + (r.Width - text.Width) / 2, r.Y + (r.Height - text.Height) / 2));
        }
    }
}

/// <summary>Server Rack (5.9.0): the ears of a rack unit with their holes.</summary>
public sealed class ArtRackEar : ArtDecoration
{
    public ArtRackEar() : base(Colors.White) { Width = 18; }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; if (h < 10) return;
        c.DrawRectangle(BlackDraw.V(Color.Parse("#2A2D32"), Color.Parse("#16181C")), null, new Rect(0, 0, w, h), 2, 2);
        for (double y = 10; y < h - 6; y += 16) c.DrawRectangle(BlackDraw.B(Color.Parse("#0B0C0E")), null, new Rect(w * .3, y, w * .4, 6), 2, 2);
    }
}

/// <summary>Server Rack (5.9.0): ten status lights, lit along the reading.</summary>
public sealed class ArtRackLeds : ArtDecoration
{
    private double? level;
    public ArtRackLeds(Color colour) : base(colour) { Height = 12; }
    public void Set(double? value) { level = value; InvalidateVisual(); }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; const int n = 10; if (w < 40) return;
        double gap = 4, cell = (w - gap * (n - 1)) / n; int lit = (int)Math.Round(Math.Clamp(level ?? 0, 0, 1) * n);
        for (int i = 0; i < n; i++)
        {
            var rect = new Rect(i * (cell + gap), 0, cell, h);
            if (i < lit) c.DrawRectangle(BlackDraw.B(BlackDraw.A(Color, .3)), null, rect.Inflate(2), 3, 3);
            c.DrawRectangle(BlackDraw.B(i < lit ? Color : Color.Parse("#1D2329")), null, rect, 2, 2);
        }
    }
}

/// <summary>Turntable (5.9.0): a fan as a record: grooves, a sheen and a label in its colour; the number goes on the label.</summary>
public sealed class ArtRecord : ArtDecoration
{
    public ArtRecord(Color label) : base(label) { }
    public override void Render(DrawingContext c)
    {
        double s = Math.Min(Bounds.Width, Bounds.Height); if (s < 30) return;
        var centre = new Point(Bounds.Width / 2, Bounds.Height / 2); double r = s / 2 - 1;
        c.DrawEllipse(BlackDraw.B(Color.Parse("#0B0B0D")), new Pen(BlackDraw.B(Color.Parse("#2A2A30")), 1), centre, r, r);
        for (int k = 4; k < 12; k++) c.DrawEllipse(null, new Pen(BlackDraw.B(Color.FromArgb(18, 255, 255, 255)), 1), centre, r * k / 12, r * k / 12);
        var sheen = new StreamGeometry();
        using (var g = sheen.Open())
        {
            g.BeginFigure(centre, true); g.LineTo(new Point(centre.X + r * Math.Cos(-.9), centre.Y + r * Math.Sin(-.9)));
            g.ArcTo(new Point(centre.X + r * Math.Cos(-.4), centre.Y + r * Math.Sin(-.4)), new Size(r, r), 0, false, SweepDirection.Clockwise); g.EndFigure(true);
        }
        c.DrawGeometry(BlackDraw.B(Color.FromArgb(22, 255, 255, 255)), null, sheen);
        c.DrawEllipse(BlackDraw.B(Color), null, centre, r * .36, r * .36);
    }
}

/// <summary>Ocean Live (5.9.0): night sea behind the glass cards: sky and waves.</summary>
public sealed class ArtOceanNight : ArtDecoration
{
    public ArtOceanNight() : base(Colors.White) { ClipToBounds = true; }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 10 || h < 10) return;
        c.DrawRectangle(BlackDraw.V(Color.Parse("#020812"), Color.Parse("#0A3A5A")), null, new Rect(0, 0, w, h));
        for (int k = 0; k < 5; k++)
        {
            double y = h * (.62 + k * .08), amp = h * .02 * (1 + k * .3);
            var wave = new StreamGeometry();
            using (var s = wave.Open())
            {
                s.BeginFigure(new Point(0, y), true);
                for (double x = 0; x < w; x += 240) s.QuadraticBezierTo(new Point(x + 120, y - amp), new Point(x + 240, y));
                s.LineTo(new Point(w, h)); s.LineTo(new Point(0, h)); s.EndFigure(true);
            }
            c.DrawGeometry(BlackDraw.B(Color.FromArgb((byte)(50 + k * 28), 10, 58, 90)), null, wave);
        }
    }
}

/// <summary>Space Station (5.9.0): stars, a truss across the screen and four solar panels behind the modules.</summary>
public sealed class ArtStation : ArtDecoration
{
    public ArtStation() : base(Colors.White) { ClipToBounds = true; }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 10 || h < 10) return;
        c.DrawRectangle(new RadialGradientBrush { Center = new RelativePoint(.7, .3, RelativeUnit.Relative), GradientOrigin = new RelativePoint(.7, .3, RelativeUnit.Relative),
            GradientStops = { new(Color.Parse("#0B1530"), 0), new(Color.Parse("#000000"), 1) } }, null, new Rect(0, 0, w, h));
        var rnd = new Random(9);
        for (int i = 0; i < 260; i++) c.DrawEllipse(BlackDraw.B(Color.FromArgb((byte)rnd.Next(30, 200), 255, 255, 255)), null, new Point(rnd.NextDouble() * w, rnd.NextDouble() * h), .7, .7);
        double y = h * .5;
        c.DrawRectangle(BlackDraw.B(Color.Parse("#232938")), null, new Rect(w * .04, y - 6, w * .92, 12));
        for (double x = w * .04; x < w * .96; x += 34) c.DrawLine(new Pen(BlackDraw.B(Color.Parse("#3A4256")), 1), new Point(x, y - 6), new Point(x + 34, y + 6));
        double pw = w * .07, ph = h * .26;
        foreach (var (px, py) in new[] { (w * .01, y - ph - 18), (w * .01, y + 18), (w * .99 - pw, y - ph - 18), (w * .99 - pw, y + 18) })
        {
            c.DrawRectangle(BlackDraw.B(Color.Parse("#0C1A40")), new Pen(BlackDraw.B(Color.Parse("#2A3A6A")), 1), new Rect(px, py, pw, ph));
            for (double gx = px + pw / 4; gx < px + pw; gx += pw / 4) c.DrawLine(new Pen(BlackDraw.B(Color.Parse("#1E3070")), 1), new Point(gx, py), new Point(gx, py + ph));
            for (double gy = py + ph / 6; gy < py + ph; gy += ph / 6) c.DrawLine(new Pen(BlackDraw.B(Color.Parse("#1E3070")), 1), new Point(px, gy), new Point(px + pw, gy));
        }
    }
}

/// <summary>Versus (5.9.0): the screen split diagonally in the colours of the two rivals.</summary>
public sealed class ArtVersus : ArtDecoration
{
    private readonly Color right;
    public ArtVersus(Color left, Color right) : base(left) { this.right = right; ClipToBounds = true; }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 10 || h < 10) return;
        var leftShape = new StreamGeometry();
        using (var s = leftShape.Open()) { s.BeginFigure(new Point(0, 0), true); s.LineTo(new Point(w * .54, 0)); s.LineTo(new Point(w * .46, h)); s.LineTo(new Point(0, h)); s.EndFigure(true); }
        var rightShape = new StreamGeometry();
        using (var s = rightShape.Open()) { s.BeginFigure(new Point(w * .54, 0), true); s.LineTo(new Point(w, 0)); s.LineTo(new Point(w, h)); s.LineTo(new Point(w * .46, h)); s.EndFigure(true); }
        c.DrawGeometry(new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(.6, 0, RelativeUnit.Relative),
            GradientStops = { new(BlackDraw.A(Color, .22), 0), new(Color.FromArgb(0, 0, 0, 0), 1) } }, null, leftShape);
        c.DrawGeometry(new LinearGradientBrush { StartPoint = new RelativePoint(1, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(.4, 0, RelativeUnit.Relative),
            GradientStops = { new(BlackDraw.A(right, .22), 0), new(Color.FromArgb(0, 0, 0, 0), 1) } }, null, rightShape);
    }
}

/// <summary>Pit Wall (5.9.0): the coloured position tag of a timing tower row (a rank, not a reading).</summary>
public sealed class ArtTimingRank : ArtDecoration
{
    private static readonly Typeface Face = new(Ui.Inter, FontStyle.Normal, FontWeight.ExtraBold);
    private readonly int rank;
    public ArtTimingRank(int rank, Color colour) : base(colour) { this.rank = rank; Width = 34; }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; if (h < 10) return;
        c.DrawRectangle(BlackDraw.B(Color), null, new Rect(0, 0, w, h), 2, 2);
        var t = new FormattedText(rank.ToString(), System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Face, Math.Min(22, h * .55), BlackDraw.B(Colors.Black));
        c.DrawText(t, new Point((w - t.Width) / 2, (h - t.Height) / 2));
    }
}

/// <summary>
/// Side by side where there is room (weights like grid columns, 0 = as wide as the part needs), one above the other
/// below <see cref="From"/> wide (a sidebar): Versus and the broadcast scoreboard keep their names whole either way.
/// </summary>
public sealed class DeskSplit : Panel
{
    public double From { get; set; } = 560;
    public double Gap { get; set; } = 16;
    private readonly List<double> weights = new();
    private bool across;
    public DeskSplit Add(Control part, double weight) { Children.Add(part); weights.Add(weight); return this; }
    protected override Size MeasureOverride(Size available)
    {
        double w = double.IsFinite(available.Width) ? available.Width : 1200;
        across = w >= From;
        if (!across)
        {
            double need = 0;
            foreach (var c in Children) { c.Measure(new Size(w, double.PositiveInfinity)); need += c.DesiredSize.Height; }
            return new Size(w, need + Gap * Math.Max(0, Children.Count - 1));
        }
        var widths = Widths(w, available.Height); double high = 0;
        for (int i = 0; i < Children.Count; i++) { Children[i].Measure(new Size(widths[i], available.Height)); high = Math.Max(high, Children[i].DesiredSize.Height); }
        return new Size(w, high);
    }
    private double[] Widths(double w, double h)
    {
        var widths = new double[Children.Count]; double fixedWidth = 0, total = 0;
        for (int i = 0; i < Children.Count; i++)
            if (weights[i] <= 0) { Children[i].Measure(new Size(double.PositiveInfinity, h)); widths[i] = Children[i].DesiredSize.Width; fixedWidth += widths[i]; }
            else total += weights[i];
        double rest = Math.Max(0, w - fixedWidth - Gap * Math.Max(0, Children.Count - 1));
        for (int i = 0; i < Children.Count; i++) if (weights[i] > 0) widths[i] = rest * weights[i] / Math.Max(total, 1e-6);
        return widths;
    }
    protected override Size ArrangeOverride(Size final)
    {
        if (!across)
        {
            double need = Children.Sum(c => c.DesiredSize.Height) + Gap * Math.Max(0, Children.Count - 1);
            double extra = Math.Max(0, final.Height - need) / Math.Max(1, Children.Count), y = 0;
            foreach (var c in Children) { double h = c.DesiredSize.Height + extra; c.Arrange(new Rect(0, y, final.Width, h)); y += h + Gap; }
            return final;
        }
        var widths = Widths(final.Width, final.Height); double x = 0;
        for (int i = 0; i < Children.Count; i++) { Children[i].Arrange(new Rect(x, 0, widths[i], final.Height)); x += widths[i] + Gap; }
        return final;
    }
}
