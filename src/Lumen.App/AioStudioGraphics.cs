using Avalonia;
using Avalonia.Media;

namespace Lumen.App;

/// <summary>Synthwave: a striped sunset over a perspective grid floor.</summary>
public sealed class ArtSynthSun : ArtDecoration
{
    private readonly Color top, gap;
    public ArtSynthSun(Color color, Color top, Color gap) : base(color) { this.top = top; this.gap = gap; ClipToBounds = true; }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 20 || h < 20) return;
        double horizon = h * .66, r = Math.Min(w * .3, horizon * .92), cx = w / 2, cy = horizon;
        var sun = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = { new(BlackDraw.A(top, 1), 0), new(BlackDraw.A(Color, 1), 1) }
        };
        using (c.PushClip(new Rect(0, 0, w, horizon)))
        {
            c.DrawEllipse(sun, null, new Point(cx, cy), r, r);
            // The stripes of the sunset: gaps that widen towards the horizon.
            var stripe = BlackDraw.B(gap);
            for (int i = 0; i < 6; i++)
            {
                double y = cy - r * .55 + i * r * .1, height = 1.5 + i * 1.3;
                c.DrawRectangle(stripe, null, new Rect(cx - r, y, r * 2, height));
            }
        }
        var line = new Pen(BlackDraw.B(BlackDraw.A(Color, .5)), 1);
        c.DrawLine(new Pen(BlackDraw.B(BlackDraw.A(Color, .9)), 2), new Point(0, horizon), new Point(w, horizon));
        for (int i = -10; i <= 10; i++) c.DrawLine(line, new Point(cx + i * w / 20, horizon), new Point(cx + i * w / 3, h));
        for (int i = 1; i <= 5; i++) { double y = horizon + (h - horizon) * Math.Pow(i / 5d, 1.7); c.DrawLine(line, new Point(0, y), new Point(w, y)); }
    }
}

/// <summary>Radar: range rings, a cross, the sweep and one blip per part (nearer the edge the hotter it runs).</summary>
public sealed class ArtRadar : ArtDecoration
{
    private static readonly Typeface Mono = new(Ui.FamilyFor("share-tech-mono"));
    private readonly List<(string Caption, double? Level)> blips = new();
    public ArtRadar(Color color) : base(color) { }
    public int Add(string caption, double? level) { blips.Add((caption, level)); return blips.Count - 1; }
    public void Set(int index, double? level) { blips[index] = (blips[index].Caption, level); InvalidateVisual(); }
    public override void Render(DrawingContext c)
    {
        double size = Math.Min(Bounds.Width, Bounds.Height); if (size < 40) return;
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2); double r = size / 2 - 2;
        c.DrawEllipse(BlackDraw.B(BlackDraw.A(Color, .05)), new Pen(BlackDraw.B(BlackDraw.A(Color, .7)), 1.5), center, r, r);
        var thin = new Pen(BlackDraw.B(BlackDraw.A(Color, .25)), 1);
        for (int i = 1; i < 4; i++) c.DrawEllipse(null, thin, center, r * i / 4, r * i / 4);
        c.DrawLine(thin, new Point(center.X - r, center.Y), new Point(center.X + r, center.Y));
        c.DrawLine(thin, new Point(center.X, center.Y - r), new Point(center.X, center.Y + r));
        // The sweep: a wedge fading behind its leading edge.
        for (int i = 0; i < 12; i++)
        {
            double a0 = (-48 + i * 4) * Math.PI / 180, a1 = (-44 + i * 4) * Math.PI / 180;
            var wedge = new StreamGeometry();
            using (var g = wedge.Open())
            {
                g.BeginFigure(center, true);
                g.LineTo(new Point(center.X + r * Math.Cos(a0), center.Y + r * Math.Sin(a0)));
                g.ArcTo(new Point(center.X + r * Math.Cos(a1), center.Y + r * Math.Sin(a1)), new Size(r, r), 0, false, SweepDirection.Clockwise);
                g.EndFigure(true);
            }
            c.DrawGeometry(BlackDraw.B(BlackDraw.A(Color, .02 + i * .022)), null, wedge);
        }
        c.DrawLine(new Pen(BlackDraw.B(BlackDraw.A(Color, .9)), 1.5), center, new Point(center.X + r * Math.Cos(Math.PI * 0 / 180), center.Y));
        for (int i = 0; i < blips.Count; i++)
        {
            var (caption, level) = blips[i];
            double a = (-110 + i * 360.0 / Math.Max(1, blips.Count)) * Math.PI / 180, d = r * (.18 + .72 * Math.Clamp(level ?? 0, 0, 1));
            var at = new Point(center.X + d * Math.Cos(a), center.Y + d * Math.Sin(a));
            c.DrawEllipse(BlackDraw.B(BlackDraw.A(Color, .3)), null, at, 7, 7);
            c.DrawEllipse(BlackDraw.B(Color), null, at, 3.5, 3.5);
            var text = new FormattedText(caption, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Mono, Math.Max(8, size / 26), BlackDraw.B(BlackDraw.A(Color, .85)));
            double x = at.X + 7; if (x + text.Width > Bounds.Width) x = at.X - 7 - text.Width;
            c.DrawText(text, new Point(x, at.Y - text.Height / 2));
        }
    }
}

/// <summary>A test tube filled to its level, with a meniscus and a scale.</summary>
public sealed class ArtTube : ArtDecoration
{
    private readonly Color track; private double? level;
    public ArtTube(Color color, Color track) : base(color) { this.track = track; }
    public void Set(double? value) { level = value; InvalidateVisual(); }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 10 || h < 30) return;
        double r = w / 2 - 2, left = 2, right = w - 2, top = 2, bottom = h - 2;
        Geometry Tube(double from)
        {
            var g = new StreamGeometry();
            using var s = g.Open();
            s.BeginFigure(new Point(left, from), true);
            s.LineTo(new Point(left, bottom - r));
            s.ArcTo(new Point(right, bottom - r), new Size(r, r), 0, false, SweepDirection.CounterClockwise);
            s.LineTo(new Point(right, from));
            s.EndFigure(true);
            return g;
        }
        c.DrawGeometry(BlackDraw.B(track), null, Tube(top));
        if (level is double f && f > 0)
        {
            double y = bottom - (bottom - top - 6) * Math.Clamp(f, 0, 1);
            var liquid = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                GradientStops = { new(BlackDraw.A(Color, .75), 0), new(BlackDraw.A(Color, 1), .45), new(BlackDraw.A(Color, .7), 1) }
            };
            c.DrawGeometry(liquid, null, Tube(y));
            c.DrawEllipse(BlackDraw.B(Palette.Mix(Color, Colors.White, .35)), null, new Point(w / 2, y), r, Math.Min(3, r / 3));
        }
        var glass = new Pen(BlackDraw.B(Color.FromArgb(110, 255, 255, 255)), 1.2);
        c.DrawGeometry(null, glass, Tube(top));
        var tick = new Pen(BlackDraw.B(Color.FromArgb(70, 255, 255, 255)), 1);
        for (int i = 1; i < 10; i++) { double y = bottom - r - (bottom - r - top) * i / 10; c.DrawLine(tick, new Point(left, y), new Point(left + (i % 5 == 0 ? r : r / 2), y)); }
    }
}

/// <summary>8-bit hearts in a row.</summary>
public sealed class ArtHearts : ArtDecoration
{
    public ArtHearts(Color color) : base(color) { Height = 14; }
    public override void Render(DrawingContext c)
    {
        string[] heart = { ".XX.XX.", "XXXXXXX", "XXXXXXX", ".XXXXX.", "..XXX..", "...X..." };
        double cell = 2; var brush = BlackDraw.B(Color);
        for (int n = 0; n < 5; n++)
            for (int y = 0; y < heart.Length; y++)
                for (int x = 0; x < heart[y].Length; x++)
                    if (heart[y][x] == 'X') c.DrawRectangle(brush, null, new Rect(n * 18 + x * cell, 1 + y * cell, cell, cell));
    }
}
