using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

/// <summary>Colours, cards, graphics and font of the Black designs (Dashboard, Rings, Graph, Classic, Tiles, X-Ray).</summary>
public sealed record BlackTheme(Color A, Color B, string Cards, string Art, FontFamily Font, Color Muted, Color Track)
{
    public static readonly FontFamily Orbitron = new("avares://Lumen/Assets/Fonts#Orbitron");
    public static readonly FontFamily Mono = new("Consolas, Cascadia Mono, Courier New, monospace");

    public static BlackTheme For(AioDesigns.BlackLook look, Color? own = null)
    {
        var colours = AioDesigns.BlackColours.FirstOrDefault(c => c.Key == look.Colour);
        if (colours.Key == null) colours = AioDesigns.BlackColours[0];
        var a = own ?? Color.Parse(colours.A); var b = own is { } o ? Palette.Mix(o, Colors.White, .35) : Color.Parse(colours.B);
        var font = Ui.DesignFont(look.Font switch { "orbitron" => Orbitron, "mono" => Mono, _ => Ui.Inter });
        return new BlackTheme(a, b, look.Cards, look.Art, font, Color.Parse("#A8A8B2"), Color.FromArgb(32, 255, 255, 255));
    }
}

internal static class BlackDraw
{
    public static IBrush B(Color c) => new SolidColorBrush(c);
    public static Pen P(Color c, double w) => new(B(c), w) { LineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
    public static Pen P(IBrush b, double w) => new(b, w) { LineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
    public static Color A(Color c, double a) => Color.FromArgb((byte)Math.Round(Math.Clamp(a, 0, 1) * 255), c.R, c.G, c.B);
    public static IBrush V(Color top, Color bottom) => new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
        GradientStops = { new(top, 0), new(bottom, 1) }
    };
    public static IBrush D(params (Color C, double At)[] stops)
    {
        var brush = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative) };
        foreach (var (c, at) in stops) brush.GradientStops.Add(new GradientStop(c, at));
        return brush;
    }
    public static IBrush H(Color left, Color right) => new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
        GradientStops = { new(left, 0), new(right, 1) }
    };
    public static Point Polar(Point c, double r, double a) => new(c.X + r * Math.Cos(a), c.Y + r * Math.Sin(a));
    public static StreamGeometry Arc(Point c, double r, double from, double sweep)
    {
        var g = new StreamGeometry();
        sweep = Math.Clamp(sweep, 0.0001, Math.PI * 2 - .0001);
        using var s = g.Open();
        s.BeginFigure(Polar(c, r, from), false);
        s.ArcTo(Polar(c, r, from + sweep), new Size(r, r), 0, sweep > Math.PI, SweepDirection.Clockwise);
        s.EndFigure(false);
        return g;
    }
    public static StreamGeometry Poly(params Point[] points)
    {
        var g = new StreamGeometry();
        using var s = g.Open();
        s.BeginFigure(points[0], true);
        foreach (var p in points.Skip(1)) s.LineTo(p);
        s.EndFigure(true);
        return g;
    }
    public static void Glow(DrawingContext g, Point center, double radius, Color color, double strength = .4)
    {
        var brush = new RadialGradientBrush { GradientStops = { new(A(color, strength), 0), new(A(color, strength * .4), .55), new(A(color, 0), 1) } };
        g.DrawEllipse(brush, null, center, radius, radius);
    }
    public static void Label(DrawingContext g, string text, double size, Color color, Point at, bool center = true)
    {
        var f = new FormattedText(text, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(BlackTheme.Orbitron, FontStyle.Normal, FontWeight.Bold), size, B(color));
        g.DrawText(f, center ? new Point(at.X - f.Width / 2, at.Y - f.Height / 2) : at);
    }
}

/// <summary>
/// A drawn hardware picture of the Black designs: processor, graphics card, integrated graphics, memory, NVMe drive,
/// fan, power supply and network card, each as a realistic picture, as glowing line art or in an isometric 3D view.
/// </summary>
public sealed class BlackArt : Control
{
    public string Kind { get; init; } = "cpu";
    public BlackTheme Theme { get; init; } = BlackTheme.For(new AioDesigns.BlackLook("red", "plain", "real", "inter"));
    /// <summary>A stopped fan is drawn grey.</summary>
    public bool Active { get => active; set { if (active == value) return; active = value; InvalidateVisual(); } }
    private bool active = true;

    private Size Box => Theme.Art switch
    {
        "line" => new Size(100, Kind is "gpu" or "ram" or "ssd" or "net" ? 80 : 100),
        "iso" => new Size(120, 110),
        _ => Kind switch
        {
            "gpu" => new Size(230, 100), "ram" => new Size(200, 110), "ssd" => new Size(150, 70), "net" => new Size(140, 110),
            _ => new Size(120, 120)
        }
    };
    protected override Size MeasureOverride(Size available)
    {
        var box = Box;
        double w = Width is double width && double.IsFinite(width) ? width : double.IsFinite(available.Width) ? available.Width : box.Width;
        if (double.IsFinite(available.Width)) w = Math.Min(w, available.Width);
        double h = w * box.Height / box.Width;
        if (double.IsFinite(available.Height) && h > available.Height) { h = available.Height; w = h * box.Width / box.Height; }
        return new Size(w, h);
    }
    public override void Render(DrawingContext g)
    {
        var box = Box;
        double s = Math.Min(Bounds.Width / box.Width, Bounds.Height / box.Height); if (s <= 0) return;
        using var _ = g.PushTransform(Matrix.CreateScale(s, s) * Matrix.CreateTranslation((Bounds.Width - box.Width * s) / 2, (Bounds.Height - box.Height * s) / 2));
        switch (Theme.Art)
        {
            case "line": Line(g); break;
            case "iso": Iso(g); break;
            default: Real(g); break;
        }
    }

    // ------------------------------------------------------------------ realistic pictures
    private static readonly Color Gold = Color.Parse("#D9AE4A"), GoldDark = Color.Parse("#A9802B"), Pcb = Color.Parse("#1B4D24");
    private void Rect(DrawingContext g, double x, double y, double w, double h, IBrush? fill, Pen? pen = null, double r = 0) => g.DrawRectangle(fill, pen, new Rect(x, y, w, h), r, r);
    private void Real(DrawingContext g)
    {
        var a = Theme.A; var b = Theme.B;
        var edge = BlackDraw.P(Color.Parse("#4A5059"), 1);
        switch (Kind)
        {
            case "cpu":
            case "igpu":
            {
                var pins = BlackDraw.V(Color.Parse("#F7D580"), GoldDark);
                for (int i = 0; i < 11; i++)
                {
                    Rect(g, 14 + i * 9, 2, 4, 10, pins, null, 1); Rect(g, 14 + i * 9, 108, 4, 10, pins, null, 1);
                    Rect(g, 2, 14 + i * 9, 10, 4, pins, null, 1); Rect(g, 108, 14 + i * 9, 10, 4, pins, null, 1);
                }
                Rect(g, 10, 10, 100, 100, BlackDraw.D((Color.Parse("#2B2F36"), 0), (Color.Parse("#0D0F12"), 1)), BlackDraw.P(Color.Parse("#3A3F47"), 1), 8);
                Rect(g, 24, 27, 72, 72, BlackDraw.B(Color.FromArgb(120, 0, 0, 0)), null, 6);
                Rect(g, 24, 24, 72, 72, BlackDraw.D((Color.Parse("#F4F6F8"), 0), (Color.Parse("#B9C0C8"), .45), (Color.Parse("#D9DEE3"), .55), (Color.Parse("#8A929B"), 1)),
                    BlackDraw.P(Color.FromArgb(150, 255, 255, 255), 1), 6);
                BlackDraw.Label(g, Kind == "igpu" ? "iGPU" : "CORE", 10, Color.Parse("#3B4048"), new Point(60, 54));
                Rect(g, 40, 64, 40, 3, BlackDraw.B(a), null, 1.5);
                g.DrawEllipse(BlackDraw.B(a), null, new Point(17, 103), 2.5, 2.5);
                break;
            }
            case "gpu":
            {
                Rect(g, 6, 84, 200, 10, BlackDraw.B(Pcb)); Rect(g, 40, 90, 120, 6, BlackDraw.B(Gold));
                g.DrawGeometry(BlackDraw.V(Color.Parse("#3A3F46"), Color.Parse("#0C0D0F")), edge,
                    BlackDraw.Poly(new(4, 14), new(210, 14), new(224, 26), new(224, 86), new(4, 86)));
                g.DrawLine(new Pen(BlackDraw.H(BlackDraw.A(a, 0), a), 2), new Point(10, 18), new Point(206, 18));
                foreach (var cx in new[] { 46.0, 114, 182 }) Fan(g, new Point(cx, 50), 28, a, true);
                Rect(g, 214, 30, 6, 40, BlackDraw.B(Color.Parse("#2A2E33")), null, 2);
                break;
            }
            case "ram":
            {
                var rgb = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = { new(a, 0), new(Color.Parse("#FFD600"), .5), new(b, 1) }
                };
                foreach (var (y, o) in new[] { (4.0, 22.0), (54.0, 6.0) })
                {
                    Rect(g, o, y, 170, 12, rgb, null, 3);
                    Rect(g, o, y + 10, 170, 30, BlackDraw.V(Color.Parse("#3B4047"), Color.Parse("#121417")), edge, 2);
                    g.DrawLine(BlackDraw.P(Color.FromArgb(60, 255, 255, 255), 1), new Point(o + 8, y + 18), new Point(o + 162, y + 18));
                    Rect(g, o + 6, y + 40, 158, 8, BlackDraw.B(Pcb));
                    for (int i = 0; i < 20; i++) Rect(g, o + 10 + i * 7.6, y + 42, 4, 6, BlackDraw.B(Gold));
                }
                break;
            }
            case "ssd":
            {
                Rect(g, 4, 18, 142, 34, BlackDraw.B(Pcb), null, 3);
                Rect(g, 12, 10, 112, 44, BlackDraw.B(Color.Parse("#24282D")), edge, 4);
                for (int i = 0; i < 13; i++) Rect(g, 20 + i * 8, 14, 5, 34, BlackDraw.V(Color.Parse("#5A616B"), Color.Parse("#1C1F23")), null, 1);
                Rect(g, 12, 50, 112, 3, BlackDraw.H(a, b));
                Rect(g, 128, 26, 14, 18, BlackDraw.B(Gold));
                g.DrawEllipse(null, BlackDraw.P(Color.Parse("#999999"), 1), new Point(8, 35), 3, 3);
                break;
            }
            case "fan":
            {
                var ring = active ? a : Color.Parse("#4B5160");
                Rect(g, 4, 4, 112, 112, BlackDraw.B(Color.Parse("#15171B")), BlackDraw.P(Color.Parse("#3A3F46"), 1), 14);
                foreach (var (x, y) in new[] { (15.0, 15.0), (105.0, 15.0), (15.0, 105.0), (105.0, 105.0) })
                    g.DrawEllipse(BlackDraw.B(Color.Parse("#0B0C0F")), BlackDraw.P(Color.Parse("#555555"), 1), new Point(x, y), 4, 4);
                if (active) BlackDraw.Glow(g, new Point(60, 60), 56, a, .3);
                Fan(g, new Point(60, 60), 47, ring, active, 7);
                break;
            }
            case "psu":
            {
                Rect(g, 6, 14, 108, 92, BlackDraw.D((Color.Parse("#3B4047"), 0), (Color.Parse("#121417"), 1)), edge, 8);
                BlackDraw.Glow(g, new Point(60, 60), 44, a, .3);
                Fan(g, new Point(60, 60), 36, a, true, 8);
                Rect(g, 14, 20, 16, 6, BlackDraw.B(a), null, 2);
                break;
            }
            case "net":
            {
                Rect(g, 4, 16, 132, 46, BlackDraw.V(Color.Parse("#3A4048"), Color.Parse("#16191C")), edge, 4);
                for (int i = 0; i < 4; i++)
                {
                    double x = 14 + i * 30;
                    Rect(g, x, 30, 24, 22, BlackDraw.B(Color.Parse("#0B0C0F")), BlackDraw.P(Color.Parse("#888888"), 1), 2);
                    Rect(g, x + 6, 44, 12, 6, BlackDraw.B(Color.Parse("#222222")));
                    g.DrawEllipse(BlackDraw.B(i < 2 ? b : Color.Parse("#FFB300")), null, new Point(x + 4, 25), 2, 2);
                }
                Rect(g, 10, 62, 120, 30, BlackDraw.B(Pcb)); Rect(g, 40, 66, 34, 22, BlackDraw.B(Color.Parse("#111111")), BlackDraw.P(Color.Parse("#333333"), 1));
                for (int i = 0; i < 18; i++) Rect(g, 18 + i * 5.6, 92, 3, 10, BlackDraw.B(Gold));
                BlackDraw.Label(g, "LAN", 8, a, new Point(100, 77));
                break;
            }
        }
    }
    private static void Fan(DrawingContext g, Point c, double r, Color ring, bool lit, int blades = 9)
    {
        g.DrawEllipse(BlackDraw.B(Color.Parse("#0B0C0E")), BlackDraw.P(lit ? ring : Color.Parse("#3A3F46"), 2.5), c, r, r);
        var blade = BlackDraw.D((Color.Parse("#4A5059"), 0), (Color.Parse("#1C1F23"), 1));
        for (int k = 0; k < blades; k++)
        {
            double a0 = k * Math.PI * 2 / blades;
            var geo = new StreamGeometry();
            using (var s = geo.Open())
            {
                s.BeginFigure(BlackDraw.Polar(c, r * .25, a0), true);
                s.QuadraticBezierTo(BlackDraw.Polar(c, r * .7, a0 - .15), BlackDraw.Polar(c, r * .9, a0 + .35));
                s.QuadraticBezierTo(BlackDraw.Polar(c, r * .55, a0 + .55), BlackDraw.Polar(c, r * .25, a0 + .5));
                s.EndFigure(true);
            }
            g.DrawGeometry(blade, BlackDraw.P(Color.Parse("#4A5059"), .6), geo);
        }
        var hub = new RadialGradientBrush { GradientStops = { new(Color.Parse("#D0D4D9"), 0), new(Color.Parse("#5D636B"), 1) } };
        g.DrawEllipse(hub, BlackDraw.P(lit ? ring : Color.Parse("#3A3F46"), 1), c, r * .26, r * .26);
    }

    // ------------------------------------------------------------------ glowing line art
    private void Line(DrawingContext g)
    {
        var col = Kind is "cpu" or "igpu" or "ram" or "psu" ? Theme.A : Theme.B;
        if (Kind == "fan" && !active) col = Color.Parse("#4B5160");
        var pen = BlackDraw.P(col, 2.2);
        var glow = BlackDraw.P(BlackDraw.A(col, .22), 6);
        void Both(Action<Pen> draw) { draw(glow); draw(pen); }
        void R(double x, double y, double w, double h, double r = 0) => Both(p => g.DrawRectangle(null, p, new Rect(x, y, w, h), r, r));
        void L(double x1, double y1, double x2, double y2) => Both(p => g.DrawLine(p, new Point(x1, y1), new Point(x2, y2)));
        void C(double x, double y, double r) => Both(p => g.DrawEllipse(null, p, new Point(x, y), r, r));
        switch (Kind)
        {
            case "cpu":
            case "igpu":
                R(22, 22, 56, 56, 6); R(34, 34, 32, 32, 3);
                for (int i = 0; i < 5; i++) { double v = 30 + i * 10; L(v, 22, v, 12); L(v, 78, v, 88); L(22, v, 12, v); L(78, v, 88, v); }
                if (Kind == "igpu") BlackDraw.Label(g, "GPU", 8, col, new Point(50, 50));
                break;
            case "gpu":
                Both(p => g.DrawGeometry(null, p, BlackDraw.Poly(new(6, 14), new(86, 14), new(94, 22), new(94, 62), new(6, 62))));
                C(30, 38, 15); C(66, 38, 15); C(30, 38, 4); C(66, 38, 4); L(14, 62, 14, 70); L(14, 70, 64, 70); L(64, 70, 64, 62);
                break;
            case "ram":
                R(6, 22, 88, 30, 3); for (int i = 0; i < 4; i++) R(14 + i * 20, 28, 12, 16); L(10, 52, 10, 60); L(90, 52, 90, 60); L(48, 52, 48, 60);
                break;
            case "ssd":
                R(6, 26, 88, 28, 3); R(14, 32, 40, 16); R(60, 32, 10, 16); L(80, 32, 80, 48);
                break;
            case "fan":
                R(12, 12, 76, 76, 10); C(50, 50, 30); C(50, 50, 6);
                for (int k = 0; k < 5; k++)
                {
                    double ang = k * Math.PI * 2 / 5;
                    var geo = new StreamGeometry();
                    using (var s = geo.Open()) { s.BeginFigure(BlackDraw.Polar(new(50, 50), 7, ang), false); s.QuadraticBezierTo(BlackDraw.Polar(new(50, 50), 18, ang - .5), BlackDraw.Polar(new(50, 50), 27, ang + .3)); s.EndFigure(false); }
                    Both(p => g.DrawGeometry(null, p, geo));
                }
                break;
            case "psu":
                R(8, 18, 84, 64, 5); C(44, 50, 22); L(44, 30, 44, 70); L(24, 50, 64, 50); L(76, 32, 76, 42); L(76, 50, 76, 60);
                break;
            case "net":
                R(8, 16, 84, 30, 3); for (int i = 0; i < 4; i++) R(14 + i * 19, 22, 14, 14);
                L(14, 46, 14, 62); L(14, 62, 86, 62); L(86, 62, 86, 46); L(30, 62, 30, 70); L(70, 62, 70, 70);
                break;
        }
    }

    // ------------------------------------------------------------------ isometric 3D
    private void Iso(DrawingContext g)
    {
        var a = Theme.A; var b = Theme.B;
        var top = BlackDraw.D((a, 0), (b, 1));
        void Box(double x, double y, double wx, double wy, double h, IBrush topFill, Color side1, Color side2)
        {
            Point P(double X, double Y, double Z) => new(x + (X - Y) * .866, y + (X + Y) * .5 - Z);
            var edge = BlackDraw.P(Color.FromArgb(34, 255, 255, 255), .8);
            g.DrawGeometry(BlackDraw.B(side1), edge, BlackDraw.Poly(P(0, wy, h), P(wx, wy, h), P(wx, wy, 0), P(0, wy, 0)));
            g.DrawGeometry(BlackDraw.B(side2), edge, BlackDraw.Poly(P(wx, 0, h), P(wx, wy, h), P(wx, wy, 0), P(wx, 0, 0)));
            g.DrawGeometry(topFill, edge, BlackDraw.Poly(P(0, 0, h), P(wx, 0, h), P(wx, wy, h), P(0, wy, h)));
        }
        IBrush S(string hex) => BlackDraw.B(Color.Parse(hex));
        Color C(string hex) => Color.Parse(hex);
        switch (Kind)
        {
            case "cpu":
            case "igpu":
                Box(60, 18, 60, 60, 8, S("#2B2F36"), C("#15181B"), C("#0E1012"));
                Box(60, 30, 40, 40, 16, S("#D7DCE1"), C("#8A929B"), C("#A9B0B8"));
                Box(60, 36, 28, 28, 18, top, Colors.Transparent, Colors.Transparent);
                break;
            case "gpu":
                Box(30, 10, 110, 40, 22, S("#2A2F36"), a, C("#16191C"));
                for (int i = 0; i < 3; i++) g.DrawEllipse(S("#0B0C0F"), BlackDraw.P(a, 1), new Point(58 + i * 30, 36 + i * 15), 13, 7);
                break;
            case "ram":
                Box(36, 20, 100, 12, 34, top, C("#23272C"), C("#15181B"));
                Box(30, 40, 100, 12, 34, top, C("#23272C"), C("#15181B"));
                break;
            case "ssd":
                Box(40, 30, 100, 26, 6, S("#1B4D24"), C("#123318"), C("#0F2A14"));
                Box(46, 36, 70, 20, 12, S("#30353C"), a, C("#15181B"));
                break;
            case "fan":
            {
                var ring = active ? a : C("#4B5160");
                Box(48, 18, 70, 70, 18, S("#20242A"), C("#121417"), C("#0E1012"));
                g.DrawEllipse(S("#0A0B0D"), BlackDraw.P(ring, 2), new Point(60, 62), 40, 23);
                g.DrawEllipse(BlackDraw.B(active ? b : ring), null, new Point(60, 62), 8, 5);
                break;
            }
            case "psu":
                Box(48, 14, 70, 60, 50, S("#2A2F36"), C("#16191C"), C("#1F2328"));
                g.DrawEllipse(S("#0A0B0D"), BlackDraw.P(a, 2), new Point(60, 48), 26, 15);
                break;
            case "net":
                Box(40, 24, 100, 30, 18, S("#2A2F36"), C("#16191C"), C("#1F2328"));
                for (int i = 0; i < 4; i++)
                    g.DrawGeometry(S("#0A0B0D"), BlackDraw.P(a, 1), BlackDraw.Poly(new(48 + i * 16, 58 + i * 8), new(60 + i * 16, 64 + i * 8), new(60 + i * 16, 74 + i * 8), new(48 + i * 16, 68 + i * 8)));
                break;
        }
    }
}

/// <summary>History curve with a soft gradient below the line.</summary>
public sealed class BlackArea : Control
{
    public Color Color { get; init; } = Colors.Red;
    private double[] values = Array.Empty<double>();
    private double maximum = 100;
    public void Set(double[] series, double max) { values = series; maximum = max; InvalidateVisual(); }
    public override void Render(DrawingContext g)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 4 || h < 4) return;
        var data = values.Where(double.IsFinite).ToArray(); if (data.Length < 2) return;
        double max = Math.Max(maximum, data.Max() * 1.05); if (max <= 0) max = 1;
        var stroke = new StreamGeometry(); var area = new StreamGeometry();
        using (var s = stroke.Open()) using (var a = area.Open())
        {
            a.BeginFigure(new Point(0, h), true);
            for (int i = 0; i < data.Length; i++)
            {
                var p = new Point(w * i / (data.Length - 1), h - 1 - (h - 3) * Math.Clamp(data[i] / max, 0, 1));
                if (i == 0) s.BeginFigure(p, false); else s.LineTo(p);
                a.LineTo(p);
            }
            s.EndFigure(false); a.LineTo(new Point(w, h)); a.EndFigure(true);
        }
        g.DrawGeometry(BlackDraw.V(BlackDraw.A(Color, .5), BlackDraw.A(Color, 0)), null, area);
        g.DrawGeometry(null, BlackDraw.P(Color, 2), stroke);
    }
}

/// <summary>Rounded bar with a gradient fill.</summary>
public sealed class BlackBar : Control
{
    public BlackTheme Theme { get; init; } = null!;
    private double? value;
    public void Set(double? fraction) { var v = fraction is double f && double.IsFinite(f) ? Math.Clamp(f, 0, 1) : (double?)null; if (Equals(v, value)) return; value = v; InvalidateVisual(); }
    public override void Render(DrawingContext g)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 2 || h < 1) return;
        g.DrawRectangle(BlackDraw.B(Theme.Track), null, new Rect(0, 0, w, h), h / 2, h / 2);
        if (value is double v && v > 0) g.DrawRectangle(BlackDraw.H(Theme.B, Theme.A), null, new Rect(0, 0, Math.Max(h, w * v), h), h / 2, h / 2);
    }
}

/// <summary>A round progress ring (Rings layout); the picture or value sits inside.</summary>
public sealed class BlackRing : Control
{
    public BlackTheme Theme { get; init; } = null!;
    public Color? Color { get; init; }
    public double Thickness { get; init; } = 7;
    private double? value;
    public void Set(double? fraction) { var v = fraction is double f && double.IsFinite(f) ? Math.Clamp(f, 0, 1) : (double?)null; if (Equals(v, value)) return; value = v; InvalidateVisual(); }
    public override void Render(DrawingContext g)
    {
        double size = Math.Min(Bounds.Width, Bounds.Height); if (size < 10) return;
        var c = new Point(Bounds.Width / 2, Bounds.Height / 2); double r = size / 2 - Thickness / 2 - 1;
        var col = Color ?? Theme.A;
        g.DrawEllipse(null, new Pen(BlackDraw.B(Theme.Track), Thickness), c, r, r);
        if (value is double v && v > 0.001)
            g.DrawGeometry(null, new Pen(BlackDraw.B(col), Thickness) { LineCap = PenLineCap.Round }, BlackDraw.Arc(c, r, -Math.PI / 2, v * Math.PI * 2));
    }
}

/// <summary>Half-round gauge with ticks, like a car instrument (Dashboard layout); value and caption are live text below.</summary>
public sealed class BlackArc : Control
{
    public BlackTheme Theme { get; init; } = null!;
    public Color? Color { get; init; }
    private double? value;
    public void Set(double? fraction) { var v = fraction is double f && double.IsFinite(f) ? Math.Clamp(f, 0, 1) : (double?)null; if (Equals(v, value)) return; value = v; InvalidateVisual(); }
    protected override Size MeasureOverride(Size available)
    {
        double w = double.IsFinite(available.Width) ? Math.Min(available.Width, 190) : 150;
        return new Size(w, w * .52);
    }
    public override void Render(DrawingContext g)
    {
        // The gauge keeps its proportions inside a wider cell (wide sidebar): it never grows past its own height.
        double w = Math.Min(Bounds.Width, Bounds.Height / .52); if (w < 20) return;
        double r = w * .36; var c = new Point(Bounds.Width / 2, Bounds.Height - 4);
        var col = Color ?? Theme.A; double thick = w * .07;
        for (int i = 0; i <= 10; i++)
        {
            double a = Math.PI + i * Math.PI / 10;
            g.DrawLine(BlackDraw.P(Theme.Muted, 1.3), BlackDraw.Polar(c, r + thick * .9, a), BlackDraw.Polar(c, r + thick * 1.6, a));
        }
        g.DrawGeometry(null, new Pen(BlackDraw.B(Theme.Track), thick) { LineCap = PenLineCap.Round }, BlackDraw.Arc(c, r, Math.PI, Math.PI));
        if (value is double v && v > 0.001)
            g.DrawGeometry(null, new Pen(BlackDraw.B(col), thick) { LineCap = PenLineCap.Round }, BlackDraw.Arc(c, r, Math.PI, v * Math.PI));
    }
}

/// <summary>One small cell per core with its load filled from below.</summary>
public sealed class BlackCores : Control
{
    public BlackTheme Theme { get; init; } = null!;
    private IReadOnlyList<double> loads = Array.Empty<double>();
    public IReadOnlyList<double> Loads { get => loads; set { loads = value ?? Array.Empty<double>(); InvalidateMeasure(); InvalidateVisual(); } }
    private int Columns => Math.Min(8, Math.Max(1, loads.Count));
    private int Rows => (loads.Count + Columns - 1) / Columns;
    protected override Size MeasureOverride(Size available)
    {
        double w = double.IsFinite(available.Width) ? available.Width : 260;
        return new Size(w, Math.Max(1, Rows) * 24 - 4);
    }
    public override void Render(DrawingContext g)
    {
        if (loads.Count == 0) return;
        double gap = 4, w = (Bounds.Width - gap * (Columns - 1)) / Columns, h = 20;
        var fill = BlackDraw.V(Theme.B, Theme.A);
        for (int i = 0; i < loads.Count; i++)
        {
            double x = (i % Columns) * (w + gap), y = (i / Columns) * (h + gap);
            g.DrawRectangle(BlackDraw.B(Theme.Track), null, new Rect(x, y, w, h), 4, 4);
            double f = Math.Clamp(loads[i] / 100, .06, 1);
            using (g.PushClip(new RoundedRect(new Rect(x, y, w, h), 4)))
                g.DrawRectangle(fill, null, new Rect(x, y + h * (1 - f), w, h * f));
        }
    }
}

/// <summary>The PC seen from the side, with its parts where the X-Ray layout puts their values.</summary>
public sealed class BlackTower : Control
{
    public BlackTheme Theme { get; init; } = null!;
    public static readonly Size Box = new(320, 280);
    /// <summary>Where each part sits (callout start) in the 320 × 280 drawing.</summary>
    public static readonly Dictionary<string, Point> Spots = new()
    {
        ["cpu"] = new(132, 64), ["ram"] = new(186, 62), ["gpu"] = new(150, 130), ["ssd"] = new(130, 176),
        ["fan"] = new(206, 196), ["psu"] = new(132, 240), ["net"] = new(212, 236)
    };
    public override void Render(DrawingContext g)
    {
        double s = Math.Min(Bounds.Width / Box.Width, Bounds.Height / Box.Height); if (s <= 0) return;
        using var _ = g.PushTransform(Matrix.CreateScale(s, s) * Matrix.CreateTranslation((Bounds.Width - Box.Width * s) / 2, 0));
        var a = Theme.A; var b = Theme.B;
        void R(double x, double y, double w, double h, IBrush? fill, Pen? pen, double r = 0) => g.DrawRectangle(fill, pen, new Rect(x, y, w, h), r, r);
        R(85, 6, 150, 268, BlackDraw.B(Color.Parse("#0D0F14")), BlackDraw.P(BlackDraw.A(a, .5), 2), 10);
        R(97, 18, 126, 244, BlackDraw.B(Color.Parse("#07080C")), BlackDraw.P(Color.FromArgb(36, 255, 255, 255), 1), 6);
        // processor with its socket
        R(112, 44, 40, 40, BlackDraw.D((Color.Parse("#2B2F36"), 0), (Color.Parse("#0D0F12"), 1)), BlackDraw.P(Color.Parse("#3A3F47"), 1), 4);
        R(120, 52, 24, 24, BlackDraw.D((Color.Parse("#F4F6F8"), 0), (Color.Parse("#8A929B"), 1)), null, 3);
        // memory
        R(176, 40, 6, 50, BlackDraw.B(a), null); R(186, 40, 6, 50, BlackDraw.B(b), null);
        // graphics card
        R(104, 116, 112, 32, BlackDraw.V(Color.Parse("#2A2E34"), Color.Parse("#111316")), BlackDraw.P(BlackDraw.A(b, .6), 1), 4);
        for (int i = 0; i < 3; i++) g.DrawEllipse(BlackDraw.B(Color.Parse("#0B0C0F")), BlackDraw.P(b, 1.4), new Point(128 + i * 30, 132), 11, 11);
        // drive
        R(104, 170, 60, 12, BlackDraw.B(Color.Parse("#1B4D24")), BlackDraw.P(a, 1), 2);
        // case fan
        g.DrawEllipse(BlackDraw.B(Color.Parse("#0B0C0F")), BlackDraw.P(a, 2), Spots["fan"], 12, 12);
        // power supply and network
        R(104, 218, 112, 42, BlackDraw.B(Color.Parse("#15171B")), BlackDraw.P(Color.FromArgb(36, 255, 255, 255), 1), 4);
        g.DrawEllipse(BlackDraw.B(Color.Parse("#0B0C0F")), BlackDraw.P(a, 1.5), Spots["psu"], 14, 14);
        R(198, 228, 14, 18, BlackDraw.B(Color.Parse("#222222")), BlackDraw.P(b, 1), 2);
    }
}
