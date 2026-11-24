using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Lumen.App;

/// <summary>
/// Decorations of the art designs (5.2.0). They draw lines and shapes only (no text a reader could mistake for a value),
/// take their colour from the design accent, so a colour chosen in Appearance reaches them too, and never catch the mouse.
/// </summary>
public abstract class ArtDecoration : Control
{
    protected ArtDecoration(Color color) { Color = color; IsHitTestVisible = false; Tag = "graphic"; }
    public Color Color { get; }
}

/// <summary>Oscilloscope graticule: a fine grid with a brighter centre cross.</summary>
public sealed class ArtGraticule : ArtDecoration
{
    public ArtGraticule(Color color) : base(color) { ClipToBounds = true; }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 2 || h < 2) return;
        var thin = new Pen(BlackDraw.B(BlackDraw.A(Color, .13)), 1);
        double step = Math.Max(18, w / 10);
        for (double x = step; x < w; x += step) c.DrawLine(thin, new Point(x, 0), new Point(x, h));
        double rows = Math.Max(2, Math.Round(h / step));
        for (int i = 1; i < rows; i++) c.DrawLine(thin, new Point(0, h * i / rows), new Point(w, h * i / rows));
        var mid = new Pen(BlackDraw.B(BlackDraw.A(Color, .24)), 1);
        c.DrawLine(mid, new Point(0, h / 2), new Point(w, h / 2)); c.DrawLine(mid, new Point(w / 2, 0), new Point(w / 2, h));
    }
}

/// <summary>Tron floor: a perspective grid running to a glowing horizon at the bottom of its box.</summary>
public sealed class ArtTronFloor : ArtDecoration
{
    public ArtTronFloor(Color color) : base(color) { ClipToBounds = true; }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 2 || h < 2) return;
        double horizon = h * .42;
        var line = new Pen(BlackDraw.B(BlackDraw.A(Color, .22)), 1);
        for (int i = -12; i <= 12; i++) c.DrawLine(line, new Point(w / 2 + i * w / 24, horizon), new Point(w / 2 + i * w / 4, h));
        for (int i = 1; i <= 7; i++) { double y = horizon + (h - horizon) * Math.Pow(i / 7d, 1.8); c.DrawLine(line, new Point(0, y), new Point(w, y)); }
        c.DrawLine(new Pen(BlackDraw.B(BlackDraw.A(Color, .55)), 1.5), new Point(0, horizon), new Point(w, horizon));
    }
}

/// <summary>Code rain: faint columns of falling glyphs, fixed (no animation), the same picture every time.</summary>
public sealed class ArtCodeRain : ArtDecoration
{
    private static readonly Typeface Mono = new(Ui.FamilyFor("jetbrains-mono"));
    public ArtCodeRain(Color color) : base(color) { ClipToBounds = true; }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 2 || h < 2) return;
        var random = new Random(7); const string glyphs = "01ABCDEF23456789<>/*+=$#";
        for (double x = 2; x < w; x += 15)
        {
            double top = random.Next(-60, (int)Math.Max(1, h * .6)); int count = random.Next(4, 14); double alpha = .05 + random.NextDouble() * .12;
            for (int i = 0; i < count; i++)
            {
                double y = top + i * 14; if (y < -14 || y > h) continue;
                var text = new FormattedText(glyphs[random.Next(glyphs.Length)].ToString(), System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Mono, 11,
                    BlackDraw.B(BlackDraw.A(Color, i == count - 1 ? alpha * 2.2 : alpha)));
                c.DrawText(text, new Point(x, y));
            }
        }
    }
}

/// <summary>A watch bezel: thirty-one ticks on a 270° arc, every fifth one longer.</summary>
public sealed class ArtTicks : ArtDecoration
{
    public ArtTicks(Color color) : base(color) { }
    public override void Render(DrawingContext c)
    {
        double r = Math.Min(Bounds.Width, Bounds.Height) / 2 - 1; if (r < 8) return;
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        for (int i = 0; i <= 30; i++)
        {
            double a = (135 + i * 9) * Math.PI / 180, len = i % 5 == 0 ? r * .12 : r * .06;
            var pen = new Pen(BlackDraw.B(BlackDraw.A(Color, i % 5 == 0 ? .9 : .55)), i % 5 == 0 ? 1.6 : .9);
            c.DrawLine(pen, new Point(center.X + (r - len) * Math.Cos(a), center.Y + (r - len) * Math.Sin(a)), new Point(center.X + r * Math.Cos(a), center.Y + r * Math.Sin(a)));
        }
    }
}

/// <summary>A sumi-e brush stroke: thick in the middle, fading thin at both ends.</summary>
public sealed class ArtBrushStroke : ArtDecoration
{
    public ArtBrushStroke(Color color) : base(color) { Height = 6; }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width; if (w < 4) return;
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(new Point(0, 3), true);
            g.CubicBezierTo(new Point(w * .3, .4), new Point(w * .7, .8), new Point(w, 2.6));
            g.CubicBezierTo(new Point(w * .7, 4.2), new Point(w * .3, 5.6), new Point(0, 3));
            g.EndFigure(true);
        }
        c.DrawGeometry(BlackDraw.B(BlackDraw.A(Color, .38)), null, geo);
    }
}

/// <summary>The red seal of an ink painting: a slightly turned square with a carved mark.</summary>
public sealed class ArtStamp : ArtDecoration
{
    public ArtStamp(Color color) : base(color) { Width = 24; Height = 24; RenderTransform = new RotateTransform(-6); }
    public override void Render(DrawingContext c)
    {
        c.DrawRectangle(BlackDraw.B(Color), null, new RoundedRect(new Rect(0, 0, 24, 24), 3));
        var pen = new Pen(BlackDraw.B(Color.Parse("#0B0B0A")), 2.2) { LineCap = PenLineCap.Round };
        c.DrawLine(pen, new Point(8, 6), new Point(8, 17)); c.DrawLine(pen, new Point(8, 17), new Point(17, 17)); c.DrawLine(pen, new Point(13, 6), new Point(13, 12));
    }
}

/// <summary>The crosshair of a thermal camera spot meter.</summary>
public sealed class ArtCrosshair : ArtDecoration
{
    public ArtCrosshair(Color color) : base(color) { Width = 26; Height = 26; }
    public override void Render(DrawingContext c)
    {
        var pen = new Pen(BlackDraw.B(BlackDraw.A(Color, .75)), 1.5);
        c.DrawEllipse(null, pen, new Point(13, 13), 7, 7);
        c.DrawLine(pen, new Point(13, 0), new Point(13, 8)); c.DrawLine(pen, new Point(13, 18), new Point(13, 26));
        c.DrawLine(pen, new Point(0, 13), new Point(8, 13)); c.DrawLine(pen, new Point(18, 13), new Point(26, 13));
    }
}

/// <summary>Quiet background of a gallery panel: grid, scan lines, a soft glow, dots or diagonal stripes.</summary>
public sealed class ArtPattern : ArtDecoration
{
    private readonly GalleryKind kind;
    public enum GalleryKind { Grid, Scan, Glow, Dots, Stripes }
    public ArtPattern(GalleryKind kind, Color color) : base(color) { this.kind = kind; ClipToBounds = true; }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 2 || h < 2) return;
        switch (kind)
        {
            case GalleryKind.Grid:
            {
                var pen = new Pen(BlackDraw.B(BlackDraw.A(Color, .07)), 1);
                for (double x = 14; x < w; x += 28) c.DrawLine(pen, new Point(x, 0), new Point(x, h));
                for (double y = 14; y < h; y += 28) c.DrawLine(pen, new Point(0, y), new Point(w, y));
                break;
            }
            case GalleryKind.Scan:
            {
                var pen = new Pen(BlackDraw.B(Color.FromArgb(12, 255, 255, 255)), 1);
                for (double y = 1; y < h; y += 3) c.DrawLine(pen, new Point(0, y), new Point(w, y));
                break;
            }
            case GalleryKind.Glow:
                c.DrawRectangle(new RadialGradientBrush
                {
                    Center = new RelativePoint(0, 0, RelativeUnit.Relative), GradientOrigin = new RelativePoint(0, 0, RelativeUnit.Relative),
                    RadiusX = new RelativeScalar(.9, RelativeUnit.Relative), RadiusY = new RelativeScalar(.9, RelativeUnit.Relative),
                    GradientStops = { new(BlackDraw.A(Color, .2), 0), new(BlackDraw.A(Color, 0), 1) }
                }, null, new Rect(0, 0, w, h));
                break;
            case GalleryKind.Dots:
            {
                var brush = BlackDraw.B(BlackDraw.A(Color, .12));
                for (double y = 8; y < h; y += 16) for (double x = 8; x < w; x += 16) c.DrawEllipse(brush, null, new Point(x, y), 1, 1);
                break;
            }
            case GalleryKind.Stripes:
            {
                var pen = new Pen(BlackDraw.B(BlackDraw.A(Color, .045)), 10);
                for (double x = -h; x < w; x += 24) c.DrawLine(pen, new Point(x, h), new Point(x + h, 0));
                break;
            }
        }
    }
}

/// <summary>A half-circle speedometer: eleven ticks (lit up to the level), a track and a needle.</summary>
public sealed class ArtGauge : ArtDecoration
{
    private readonly Color track; private double? level;
    public ArtGauge(Color color, Color track) : base(color) { this.track = track; }
    public void Set(double? value) { level = value; InvalidateVisual(); }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 10 || h < 10) return;
        double r = Math.Min(w / 2, h) - 4, cx = w / 2, cy = Math.Min(h, r + 4);
        double f = Math.Clamp(level ?? 0, 0, 1);
        for (int i = 0; i <= 10; i++)
        {
            double a = Math.PI + Math.PI * i / 10;
            var pen = new Pen(BlackDraw.B(level.HasValue && i / 10d <= f + 1e-6 ? Color : Color.FromArgb(70, 255, 255, 255)), 2) { LineCap = PenLineCap.Round };
            c.DrawLine(pen, new Point(cx + (r - 8) * Math.Cos(a), cy + (r - 8) * Math.Sin(a)), new Point(cx + r * Math.Cos(a), cy + r * Math.Sin(a)));
        }
        double inner = r - 15;
        c.DrawGeometry(null, new Pen(BlackDraw.B(track), 5), Rings.Arc(new Point(cx, cy), inner, 180, 180));
        if (!level.HasValue) return;
        double n = Math.PI + Math.PI * f;
        c.DrawLine(new Pen(BlackDraw.B(Color), 3) { LineCap = PenLineCap.Round }, new Point(cx, cy), new Point(cx + (inner - 4) * Math.Cos(n), cy + (inner - 4) * Math.Sin(n)));
        c.DrawEllipse(BlackDraw.B(Color), null, new Point(cx, cy), 5, 5);
    }
}

/// <summary>A vertical column filled from the bottom to its level, with a soft fade.</summary>
public sealed class ArtColumn : ArtDecoration
{
    private readonly Color track; private double? level;
    public ArtColumn(Color color, Color track) : base(color) { this.track = track; }
    public void Set(double? value) { level = value; InvalidateVisual(); }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 2 || h < 2) return;
        c.DrawRectangle(BlackDraw.B(track), null, new RoundedRect(new Rect(0, 0, w, h), 5));
        if (level is not double f) return;
        double fill = h * Math.Clamp(f, 0, 1); if (fill < 1) return;
        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = { new(Color, 0), new(BlackDraw.A(Color, .35), 1) }
        };
        c.DrawRectangle(brush, null, new RoundedRect(new Rect(0, h - fill, w, fill), 5));
    }
}

/// <summary>The orbits of the Orbit layout: three thin circles around the centre.</summary>
public sealed class ArtOrbits : ArtDecoration
{
    private readonly double[] radii; private readonly Color[] colors;
    public ArtOrbits(double[] radii, Color[] colors) : base(colors[0]) { this.radii = radii; this.colors = colors; }
    public override void Render(DrawingContext c)
    {
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        for (int i = 0; i < radii.Length; i++) c.DrawEllipse(null, new Pen(BlackDraw.B(BlackDraw.A(colors[i % colors.Length], .25)), 1), center, radii[i], radii[i]);
    }
}
