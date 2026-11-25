using Avalonia;
using Avalonia.Media;

namespace Lumen.App;

/// <summary>Shared drawing for the 3D designs (5.5.0): isometric boxes in three shades of one colour.</summary>
internal static class Iso
{
    public const double Cos = .866, Sin = .5;
    public static Point P(double x, double y, double z, Point origin, double s) => new(origin.X + (x - y) * Cos * s, origin.Y + (x + y) * Sin * s - z * s);

    public static Geometry Poly(params Point[] points)
    {
        var g = new StreamGeometry();
        using (var c = g.Open()) { c.BeginFigure(points[0], true); foreach (var p in points.Skip(1)) c.LineTo(p); c.EndFigure(true); }
        return g;
    }

    /// <summary>A box standing on the floor: left face, right face and top, darkest to lightest.</summary>
    public static void Box(DrawingContext c, double x, double y, double w, double d, double h, Color colour, Point o, double s, double alpha = 1, Pen? edge = null)
    {
        Point Q(double a, double b, double z) => P(a, b, z, o, s);
        c.DrawGeometry(BlackDraw.B(BlackDraw.A(colour, .55 * alpha)), edge, Poly(Q(x, y + d, 0), Q(x + w, y + d, 0), Q(x + w, y + d, h), Q(x, y + d, h)));
        c.DrawGeometry(BlackDraw.B(BlackDraw.A(colour, .32 * alpha)), edge, Poly(Q(x + w, y, 0), Q(x + w, y + d, 0), Q(x + w, y + d, h), Q(x + w, y, h)));
        c.DrawGeometry(BlackDraw.B(BlackDraw.A(colour, .95 * alpha)), edge, Poly(Q(x, y, h), Q(x + w, y, h), Q(x + w, y + d, h), Q(x, y + d, h)));
    }
}

/// <summary>Iso skyline: one tower per part on a floor grid, as tall as its reading.</summary>
public sealed class ArtSkyline : ArtDecoration
{
    private readonly List<(Color Colour, double? Level)> towers = new();
    public ArtSkyline(Color grid) : base(grid) { ClipToBounds = true; }
    public int Add(Color colour, double? level) { towers.Add((colour, level)); return towers.Count - 1; }
    public void Set(int index, double? level) { towers[index] = (towers[index].Colour, level); InvalidateVisual(); }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; int n = towers.Count; if (n == 0 || w < 40 || h < 40) return;
        // The towers stand along one diagonal; the scale makes floor and row fit the width, the floor sits on the bottom edge.
        double step = 62, size = 44, span = (n - 1) * step + size;
        double s = Math.Min(1.1, (w - 16) / ((span + 120) * Iso.Cos));
        var o = new Point(w / 2 - (span - 140) / 2 * Iso.Cos * s, h - 8 - (span + 130) * Iso.Sin * s);
        var line = new Pen(BlackDraw.B(BlackDraw.A(Color, .13)), 1);
        for (double x = -20; x <= span + 20; x += 32) c.DrawLine(line, Iso.P(x, 30, 0, o, s), Iso.P(x, 110, 0, o, s));
        for (int i = 0; i <= 6; i++) c.DrawLine(line, Iso.P(-20, 30 + i * 13.3, 0, o, s), Iso.P(span + 20, 30 + i * 13.3, 0, o, s));
        double tallest = (o.Y + 48 * Iso.Sin * s - 8) / s;
        for (int i = 0; i < n; i++)
        {
            var (colour, level) = towers[i];
            double height = 8 + Math.Clamp(level ?? 0, 0, 1) * Math.Max(20, tallest - 10);
            Iso.Box(c, i * step, 48, size, size, height, colour, o, s);
        }
    }
}

/// <summary>A glass cube in oblique view: front face, top and side, the front face left free for the value.</summary>
public sealed class ArtCube : ArtDecoration
{
    public ArtCube(Color color) : base(color) { }
    /// <summary>Depth of top and side as a share of the size.</summary>
    public const double Depth = .26;
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 20 || h < 20) return;
        double d = Math.Min(w, h) * Depth, fw = w - d, fh = h - d;
        var edge = new Pen(BlackDraw.B(BlackDraw.A(Color, .7)), 1.2);
        var back = new Pen(BlackDraw.B(BlackDraw.A(Color, .25)), 1);
        // Hidden edges first, then the faces.
        c.DrawLine(back, new Point(d, 0), new Point(d, fh)); c.DrawLine(back, new Point(d, fh), new Point(w, fh)); c.DrawLine(back, new Point(0, h), new Point(d, fh));
        c.DrawGeometry(BlackDraw.B(BlackDraw.A(Color, .22)), edge, Iso.Poly(new(0, d), new(d, 0), new(w, 0), new(fw, d)));
        c.DrawGeometry(BlackDraw.B(BlackDraw.A(Color, .1)), edge, Iso.Poly(new(fw, d), new(w, 0), new(w, fh), new(fw, h)));
        var front = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = { new(BlackDraw.A(Color, .24), 0), new(BlackDraw.A(Color, .05), 1) }
        };
        c.DrawRectangle(front, edge, new Rect(0, d, fw, fh));
        c.DrawEllipse(BlackDraw.B(BlackDraw.A(Color, .18)), null, new Point(w / 2, h + 1), w * .4, 3);
    }
}

/// <summary>A thick ring seen from above at an angle, filled along its reading.</summary>
public sealed class ArtTorus : ArtDecoration
{
    private readonly Color track; private double? level;
    public ArtTorus(Color color, Color track) : base(color) { this.track = track; }
    public void Set(double? value) { level = value; InvalidateVisual(); }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 30 || h < 20) return;
        double t = Math.Max(6, w * .1), rx = w / 2 - t / 2 - 1, depth = Math.Max(4, h * .1), ry = (h - depth) / 2 - t / 2 - 1;
        var centre = new Point(w / 2, (h - depth) / 2);
        Geometry Arc(double from, double share, double dy)
        {
            var g = new StreamGeometry(); int n = Math.Max(2, (int)(64 * share));
            using var s = g.Open();
            for (int i = 0; i <= n; i++)
            {
                double a = (from + 360 * share * i / n) * Math.PI / 180;
                var p = new Point(centre.X + rx * Math.Cos(a), centre.Y + dy + ry * Math.Sin(a));
                if (i == 0) s.BeginFigure(p, false); else s.LineTo(p);
            }
            s.EndFigure(false);
            return g;
        }
        double f = Math.Clamp(level ?? 0, 0, 1);
        for (int k = (int)depth; k > 0; k -= 2) c.DrawGeometry(null, new Pen(BlackDraw.B(Palette.Mix(track, Colors.Black, .4)), t), Arc(0, 1, k));
        c.DrawGeometry(null, new Pen(BlackDraw.B(track), t), Arc(0, 1, 0));
        if (f > 0)
        {
            for (int k = (int)depth; k > 0; k -= 2) c.DrawGeometry(null, new Pen(BlackDraw.B(Palette.Mix(Color, Colors.Black, .45)), t) { LineCap = PenLineCap.Round }, Arc(90, f, k));
            var shine = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = { new(Palette.Mix(Color, Colors.White, .45), 0), new(Color, .45), new(Palette.Mix(Color, Colors.Black, .1), 1) }
            };
            c.DrawGeometry(null, new Pen(shine, t) { LineCap = PenLineCap.Round }, Arc(90, f, 0));
        }
    }
}

/// <summary>An isometric plate under a part, with a dashed leader line to the right edge.</summary>
public sealed class ArtSlab : ArtDecoration
{
    private readonly double plate;
    public ArtSlab(Color color, double plate) : base(color) { this.plate = plate; }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 20 || h < 10) return;
        double cx = plate / 2, cy = h / 2 + 8, rx = plate / 2 - 2, ry = Math.Min(h / 2 - 2, rx * .5);
        c.DrawGeometry(BlackDraw.B(BlackDraw.A(Color, .1)), new Pen(BlackDraw.B(BlackDraw.A(Color, .55)), 1.2),
            Iso.Poly(new(cx, cy - ry), new(cx + rx, cy), new(cx, cy + ry), new(cx - rx, cy)));
        var dash = new Pen(BlackDraw.B(BlackDraw.A(Color, .5)), 1) { DashStyle = new DashStyle(new double[] { 3, 3 }, 0) };
        c.DrawLine(dash, new Point(cx + rx * .6, cy - 6), new Point(w, cy - 6));
    }
}

/// <summary>A hologram emitter: a light cone rising from a glowing round pad.</summary>
public sealed class ArtHoloPad : ArtDecoration
{
    public ArtHoloPad(Color color) : base(color) { }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 20 || h < 30) return;
        double padY = h - 14, rx = w * .36, ry = Math.Max(5, w * .08);
        var cone = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 1, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            GradientStops = { new(BlackDraw.A(Color, .42), 0), new(BlackDraw.A(Color, 0), 1) }
        };
        c.DrawGeometry(cone, null, Iso.Poly(new(w / 2 - rx, padY), new(w / 2 + rx, padY), new(w - 2, h * .18), new(2, h * .18)));
        c.DrawEllipse(BlackDraw.B(BlackDraw.A(Color, .35)), new Pen(BlackDraw.B(Color), 2), new Point(w / 2, padY), rx, ry);
        c.DrawEllipse(null, new Pen(BlackDraw.B(BlackDraw.A(Color, .6)), 1.5), new Point(w / 2, padY + 4), rx * 1.08, ry * 1.1);
    }
}

/// <summary>A glass cylinder filled to its level.</summary>
public sealed class ArtCylinder : ArtDecoration
{
    private double? level;
    public ArtCylinder(Color color) : base(color) { }
    public void Set(double? value) { level = value; InvalidateVisual(); }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 12 || h < 30) return;
        double rx = w / 2 - 1, ry = Math.Min(w * .22, h * .1), top = ry + 1, bottom = h - ry - 1, cx = w / 2;
        var glass = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops = { new(Color.FromArgb(46, 255, 255, 255), 0), new(Color.FromArgb(10, 255, 255, 255), .25), new(Color.FromArgb(5, 255, 255, 255), .8), new(Color.FromArgb(36, 255, 255, 255), 1) }
        };
        c.DrawRectangle(glass, null, new Rect(1, top, w - 2, bottom - top));
        double f = Math.Clamp(level ?? 0, 0, 1);
        if (f > 0)
        {
            double y = bottom - (bottom - top) * f;
            var liquid = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                GradientStops = { new(BlackDraw.A(Color, .55), 0), new(Color, .35), new(BlackDraw.A(Color, .8), .7), new(BlackDraw.A(Color, .45), 1) }
            };
            c.DrawRectangle(liquid, null, new Rect(1, y, w - 2, bottom - y));
            c.DrawEllipse(BlackDraw.B(BlackDraw.A(Color, .8)), null, new Point(cx, bottom), rx, ry);
            c.DrawEllipse(BlackDraw.B(Palette.Mix(Color, Colors.White, .35)), null, new Point(cx, y), rx, ry);
        }
        var rim = new Pen(BlackDraw.B(Color.FromArgb(110, 255, 255, 255)), 1);
        c.DrawEllipse(null, rim, new Point(cx, top), rx, ry);
        c.DrawLine(rim, new Point(1, top), new Point(1, bottom)); c.DrawLine(rim, new Point(w - 1, top), new Point(w - 1, bottom));
        var g = new StreamGeometry();
        using (var s = g.Open()) { s.BeginFigure(new Point(1, bottom), false); s.ArcTo(new Point(w - 1, bottom), new Size(rx, ry), 0, false, SweepDirection.CounterClockwise); s.EndFigure(false); }
        c.DrawGeometry(null, rim, g);
    }
}

/// <summary>Iso tower: a PC case with a side window; processor, graphics, memory and drive lit inside.</summary>
public sealed class ArtTower : ArtDecoration
{
    private readonly Color cpu, gpu, ram, ssd;
    public ArtTower(Color cpu, Color gpu, Color ram, Color ssd) : base(cpu) { this.cpu = cpu; this.gpu = gpu; this.ram = ram; this.ssd = ssd; }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 60 || h < 80) return;
        double s = Math.Min(w / ((110 + 60) * Iso.Cos + 10), h / (220 + 85 * Iso.Sin + 20));
        var o = new Point(w / 2 + 25 * Iso.Cos * s, h - 6 - (60 + 110) * Iso.Sin * s + 12 * s);
        Iso.Box(c, -10, -10, 130, 80, 1, Color.Parse("#2A2A2E"), o, s);
        Iso.Box(c, 0, 0, 110, 60, 220, Color.Parse("#26282E"), o, s);
        Point Q(double x, double z) => Iso.P(x, 60, z, o, s);
        c.DrawGeometry(BlackDraw.B(Color.Parse("#0B0C10")), new Pen(BlackDraw.B(BlackDraw.A(cpu, .6)), 1), Iso.Poly(Q(10, 25), Q(100, 25), Q(100, 205), Q(10, 205)));
        void Glow(Color col, double x0, double x1, double z0, double z1)
        {
            var shape = Iso.Poly(Q(x0, z0), Q(x1, z0), Q(x1, z1), Q(x0, z1));
            c.DrawGeometry(BlackDraw.B(BlackDraw.A(col, .25)), new Pen(BlackDraw.B(BlackDraw.A(col, .35)), 6), shape);
            c.DrawGeometry(BlackDraw.B(col), null, shape);
        }
        Glow(gpu, 20, 95, 90, 105);
        Glow(cpu, 35, 60, 145, 170);
        for (int k = 0; k < 4; k++) Glow(ram, 68 + k * 6, 71 + k * 6, 140, 177);
        Glow(ssd, 20, 50, 40, 53);
        var fan = new Pen(BlackDraw.B(Color.FromArgb(120, 180, 186, 196)), 1);
        for (int k = 0; k < 3; k++)
        {
            var at = Iso.P(110, 30, 55 + k * 55, o, s);
            using (c.PushTransform(Matrix.CreateTranslation(-at.X, -at.Y) * new Matrix(1, -.577, 0, 1, 0, 0) * Matrix.CreateTranslation(at.X, at.Y)))
                c.DrawEllipse(null, fan, at, 13 * s, 18 * s);
        }
    }
}

/// <summary>Perspective HUD: a panel outline narrowing towards the top, as if the panel leaned back.</summary>
public sealed class ArtLean : ArtDecoration
{
    public ArtLean(Color color) : base(color) { }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 20 || h < 10) return;
        double inset = Math.Min(14, w * .04);
        var shape = Iso.Poly(new(inset, 1), new(w - inset, 1), new(w - 1, h - 1), new(1, h - 1));
        var fill = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = { new(BlackDraw.A(Color, .04), 0), new(BlackDraw.A(Color, .13), 1) }
        };
        c.DrawGeometry(fill, new Pen(BlackDraw.B(BlackDraw.A(Color, .5)), 1.2), shape);
    }
}

/// <summary>Perspective HUD: a grid floor running away to the horizon.</summary>
public sealed class ArtFloor : ArtDecoration
{
    public ArtFloor(Color color) : base(color) { ClipToBounds = true; }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 20 || h < 10) return;
        var line = new Pen(BlackDraw.B(BlackDraw.A(Color, .22)), 1);
        for (int i = -12; i <= 12; i++) c.DrawLine(line, new Point(w / 2 + i * w / 24, 0), new Point(w / 2 + i * w / 5, h));
        for (int i = 0; i <= 5; i++) { double y = h * Math.Pow(i / 5d, 1.6); c.DrawLine(line, new Point(0, y), new Point(w, y)); }
    }
}
