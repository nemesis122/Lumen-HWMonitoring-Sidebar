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
