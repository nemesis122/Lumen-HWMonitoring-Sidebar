using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Lumen.App;

// Custom-drawn visuals. Each one draws only what it is given; a NaN in a series is a gap,
// never a zero, so a missing sensor does not draw a fake dip.

public sealed class AreaChart : Control
{
    public double[] Values { get; set; } = Array.Empty<double>();
    public double? Max { get; set; }
    public Color Color { get; set; } = Colors.DeepSkyBlue;
    public double FillOpacity { get; set; } = .30;
    public double LineWidth { get; set; } = 1.6;

    public AreaChart() => ClipToBounds = true;

    // 0..1 of one step: the new point starts just outside on the right and slides in.
    private double shift;
    private double? shownMax;

    public void Set(double[] values, Color color)
    {
        bool scrolled = Values.Length > 0 && values.Length > 0 && !ReferenceEquals(values, Values);
        Values = values;
        Color = color;
        double target = Max ?? Math.Max(1e-9, values.Where(double.IsFinite).DefaultIfEmpty(1).Max() * 1.15);
        double from = shownMax ?? target, fromShift = scrolled ? 1 : shift;
        Motion.Run(this, scrolled ? Motion.ScrollDuration : Motion.Duration, t =>
        {
            shownMax = from + (target - from) * t;
            shift = fromShift * (1 - t);
            InvalidateVisual();
        });
    }

    public override void Render(DrawingContext context)
    {
        var b = Bounds;
        if (b.Width < 4 || b.Height < 4) return;
        var v = Values;
        double max = shownMax ?? Max ?? Math.Max(1e-9, v.Where(double.IsFinite).DefaultIfEmpty(1).Max() * 1.15);
        int slots = Lumen.Core.History.Capacity - 1;
        double step = b.Width / slots, offset = b.Width - (v.Length - 1) * step + shift * step;
        // Keep a sliver visible at zero so an idle machine still shows the line.
        Point P(int i) => new(offset + i * step, b.Height - Math.Clamp(v[i] / max, .02, 1) * (b.Height - LineWidth) - LineWidth / 2);

        var fill = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Palette.Alpha(Color, FillOpacity), 0), new GradientStop(Palette.Alpha(Color, 0), 1) }
        };
        var pen = new Pen(new SolidColorBrush(Color), LineWidth, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);

        // Draw each unbroken run separately.
        int start = -1;
        for (int i = 0; i <= v.Length; i++)
        {
            bool ok = i < v.Length && double.IsFinite(v[i]);
            if (ok && start < 0) start = i;
            if (!ok && start >= 0) { DrawRun(context, start, i - 1, P, b.Height, fill, pen); start = -1; }
        }
    }

    private static void DrawRun(DrawingContext ctx, int from, int to, Func<int, Point> p, double h, IBrush fill, Pen pen)
    {
        if (to <= from) return;
        var area = new StreamGeometry();
        using (var g = area.Open())
        {
            g.BeginFigure(new Point(p(from).X, h), true);
            for (int i = from; i <= to; i++) g.LineTo(p(i));
            g.LineTo(new Point(p(to).X, h));
            g.EndFigure(true);
        }
        ctx.DrawGeometry(fill, null, area);
        var line = new StreamGeometry();
        using (var g = line.Open())
        {
            g.BeginFigure(p(from), false);
            for (int i = from + 1; i <= to; i++) g.LineTo(p(i));
            g.EndFigure(false);
        }
        ctx.DrawGeometry(null, pen, line);
    }
}

// One slim column per logical core, coloured by that core's own load.
public sealed class Skyline : Control
{
    private double[] shown = Array.Empty<double>();
    private IReadOnlyList<double> loads = Array.Empty<double>();

    // Each column glides from its last height to the new one.
    public IReadOnlyList<double> Loads
    {
        get => loads;
        set
        {
            loads = value;
            var from = shown.Length == value.Count ? shown.ToArray() : value.ToArray();
            var to = value.ToArray();
            shown = from.ToArray();
            Motion.Run(this, Motion.Duration, t =>
            {
                for (int i = 0; i < to.Length; i++) shown[i] = from[i] + (to[i] - from[i]) * t;
                InvalidateVisual();
            });
        }
    }

    public Palette? Palette { get; set; }

    public override void Render(DrawingContext context)
    {
        var b = Bounds;
        if (Palette is null || shown.Length == 0 || b.Width < 4) return;
        int n = shown.Length;
        double gap = n > 24 ? 1.5 : 2.5, w = (b.Width - gap * (n - 1)) / n, r = Math.Min(2, w / 2);
        var track = new SolidColorBrush(Palette.Track);
        for (int i = 0; i < n; i++)
        {
            double x = i * (w + gap), load = Math.Clamp(shown[i], 0, 100) / 100;
            context.DrawRectangle(track, null, new RoundedRect(new Rect(x, 0, w, b.Height), r));
            double h = Math.Max(2, load * b.Height);
            context.DrawRectangle(new SolidColorBrush(Palette.At(load)), null, new RoundedRect(new Rect(x, b.Height - h, w, h), r));
        }
    }
}

// Download grows up from the centre line, upload grows down — one picture for both directions.
public sealed class MirrorChart : Control
{
    public double[] Down { get; set; } = Array.Empty<double>();
    public double[] Up { get; set; } = Array.Empty<double>();
    public Palette? Palette { get; set; }
    private double shift;

    public MirrorChart() => ClipToBounds = true;

    public void SetData(double[] down, double[] up)
    {
        bool scrolled = Down.Length > 0 && down.Length > 0;
        Down = down;
        Up = up;
        if (!scrolled) { InvalidateVisual(); return; }
        Motion.Run(this, Motion.ScrollDuration, t => { shift = 1 - t; InvalidateVisual(); });
    }

    public override void Render(DrawingContext context)
    {
        var b = Bounds;
        if (Palette is null || b.Width < 4) return;
        double mid = Math.Round(b.Height * .62);
        Half(context, Down, b.Width, mid, -1, Palette.Cool, 0, shift);
        Half(context, Up, b.Width, b.Height - mid, 1, Palette.Calm, mid, shift);
        context.DrawLine(new Pen(new SolidColorBrush(Palette.Stroke), 1), new Point(0, mid + .5), new Point(b.Width, mid + .5));
    }

    private static void Half(DrawingContext ctx, double[] v, double width, double height, int dir, Color color, double origin = 0, double shift = 0)
    {
        if (v.Length < 2) return;
        // Square-root scale: a 1 MB/s trickle stays visible next to a 100 MB/s burst,
        // yet the burst still clearly towers over it (a log scale flattens everything).
        double Scale(double x) => double.IsFinite(x) && x > 0 ? Math.Sqrt(x) : 0;
        double max = Math.Max(Scale(512 * 1024), v.Select(Scale).Max());
        double step = width / (Lumen.Core.History.Capacity - 1), offset = width - (v.Length - 1) * step + shift * step;
        double baseY = dir < 0 ? height : origin;
        var area = new StreamGeometry();
        var line = new StreamGeometry();
        Point P(int i) => new(offset + i * step, baseY + dir * Scale(v[i]) / max * (height - 1));
        using (var g = area.Open())
        {
            g.BeginFigure(new Point(offset, baseY), true);
            for (int i = 0; i < v.Length; i++) g.LineTo(P(i));
            g.LineTo(new Point(width, baseY));
            g.EndFigure(true);
        }
        using (var g = line.Open())
        {
            g.BeginFigure(P(0), false);
            for (int i = 1; i < v.Length; i++) g.LineTo(P(i));
            g.EndFigure(false);
        }
        var fill = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, dir < 0 ? 0 : 1, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, dir < 0 ? 1 : 0, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Palette.Alpha(color, .45), 0), new GradientStop(Palette.Alpha(color, .04), 1) }
        };
        ctx.DrawGeometry(fill, null, area);
        ctx.DrawGeometry(null, new Pen(new SolidColorBrush(color), 1.4, lineJoin: PenLineJoin.Round), line);
    }
}

// A thin bar. With Segments > 0 it is drawn as discrete cells, each coloured by its position.
public sealed class Meter : Control
{
    public double Value { get; set; }
    public Color Color { get; set; } = Colors.DeepSkyBlue;
    public Palette? Palette { get; set; }
    public int Segments { get; set; }
    public bool RampBySegment { get; set; }

    private bool started;

    // Glides from the shown value to the new one; the colour changes at once.
    public void Set(double value, Color color)
    {
        Color = color;
        double from = started ? Value : value;
        started = true;
        if (!double.IsFinite(value)) value = 0;
        if (!double.IsFinite(from)) from = value;
        Motion.Run(this, Motion.Duration, t => { Value = from + (value - from) * t; InvalidateVisual(); });
    }

    public override void Render(DrawingContext context)
    {
        var b = Bounds;
        if (Palette is null || b.Width < 4) return;
        double v = double.IsFinite(Value) ? Math.Clamp(Value, 0, 1) : 0, r = b.Height / 2;
        var track = new SolidColorBrush(Palette.Track);
        if (Segments <= 0)
        {
            context.DrawRectangle(track, null, new RoundedRect(new Rect(b.Size), r));
            if (v > 0) context.DrawRectangle(new SolidColorBrush(Color), null, new RoundedRect(new Rect(0, 0, Math.Max(b.Height, v * b.Width), b.Height), r));
            return;
        }
        double gap = 2, w = (b.Width - gap * (Segments - 1)) / Segments;
        for (int i = 0; i < Segments; i++)
        {
            double start = (double)i / Segments, fillPart = Math.Clamp((v - start) * Segments, 0, 1);
            var cell = new Rect(i * (w + gap), 0, w, b.Height);
            context.DrawRectangle(track, null, new RoundedRect(cell, Math.Min(r, 2)));
            if (fillPart > 0)
            {
                var c = RampBySegment ? Palette.At((i + .5) / Segments) : Color;
                context.DrawRectangle(new SolidColorBrush(c), null, new RoundedRect(cell.WithWidth(w * fillPart), Math.Min(r, 2)));
            }
        }
    }
}

// Small drawn weather symbols in the theme's colours, so they match the charts instead of emoji.
public sealed class WeatherIcon : Control
{
    private string group = "clear";
    private bool day = true;
    public Palette? Palette { get; set; }

    public void Set(string weatherGroup, bool isDay) { group = weatherGroup; day = isDay; InvalidateVisual(); }

    public override void Render(DrawingContext ctx)
    {
        if (Palette is null) return;
        var p = Palette;
        double s = Math.Min(Bounds.Width, Bounds.Height) / 24;
        using var _ = ctx.PushTransform(Matrix.CreateScale(s, s));
        var sun = Color.Parse(p.Dark ? "#F5C451" : "#E0A100");
        var cloudFill = new SolidColorBrush(p.Dark ? Color.Parse("#C9D2DE") : Color.Parse("#AAB5C3"));
        var rain = new Pen(new SolidColorBrush(p.Dark ? Color.Parse("#5DB6FF") : Color.Parse("#1F7AE0")), 1.6, lineCap: PenLineCap.Round);

        void Sun(double cx, double cy, double r)
        {
            if (!day)
            {
                var moon = new StreamGeometry();
                using (var g = moon.Open())
                {
                    g.BeginFigure(new Point(cx + r * .2, cy - r), true);
                    g.ArcTo(new Point(cx + r * .2, cy + r), new Size(r, r), 0, true, SweepDirection.CounterClockwise);
                    g.ArcTo(new Point(cx + r * .2, cy - r), new Size(r * .75, r * .75), 0, false, SweepDirection.Clockwise);
                    g.EndFigure(true);
                }
                ctx.DrawGeometry(new SolidColorBrush(Palette.Mix(sun, Colors.White, .45)), null, moon);
                return;
            }
            var pen = new Pen(new SolidColorBrush(sun), 1.5, lineCap: PenLineCap.Round);
            for (int i = 0; i < 8; i++)
            {
                double a = i * Math.PI / 4, c = Math.Cos(a), n = Math.Sin(a);
                ctx.DrawLine(pen, new Point(cx + c * r * 1.45, cy + n * r * 1.45), new Point(cx + c * r * 1.9, cy + n * r * 1.9));
            }
            ctx.DrawEllipse(new SolidColorBrush(sun), null, new Point(cx, cy), r, r);
        }

        void Cloud(double x, double y, double w)
        {
            double k = w / 16;
            var g = new GeometryGroup { FillRule = FillRule.NonZero };
            g.Children.Add(new EllipseGeometry(new Rect(x + 1 * k, y + 4 * k, 7 * k, 7 * k)));
            g.Children.Add(new EllipseGeometry(new Rect(x + 4 * k, y, 9 * k, 9 * k)));
            g.Children.Add(new EllipseGeometry(new Rect(x + 9 * k, y + 3.5 * k, 7 * k, 7 * k)));
            g.Children.Add(new RectangleGeometry(new Rect(x + 4.5 * k, y + 5 * k, 8 * k, 6 * k)));
            ctx.DrawGeometry(cloudFill, null, g);
        }

        void Drops(int n, bool dots)
        {
            for (int i = 0; i < n; i++)
            {
                double x = 8 + i * 4;
                if (dots) ctx.DrawEllipse(rain.Brush, null, new Point(x, 19.5 + (i % 2) * 1.5), 1.1, 1.1);
                else ctx.DrawLine(rain, new Point(x + 1, 17), new Point(x - .5, 21));
            }
        }

        switch (group)
        {
            case "clear": Sun(12, 12, 4.6); break;
            case "mostly-clear": Sun(10, 10, 4); Cloud(9, 12, 13); break;
            case "partly-cloudy": Sun(9, 8.5, 3.6); Cloud(5, 9, 17); break;
            case "fog":
                Cloud(4, 3, 16);
                var fog = new Pen(cloudFill, 1.6, lineCap: PenLineCap.Round);
                ctx.DrawLine(fog, new Point(4, 17), new Point(20, 17));
                ctx.DrawLine(fog, new Point(6, 20.5), new Point(18, 20.5));
                break;
            case "drizzle": Cloud(4, 3, 16); Drops(3, true); break;
            case "rain": Cloud(4, 3, 16); Drops(3, false); break;
            case "showers": Sun(8, 6.5, 3.2); Cloud(5, 5, 16); Drops(3, false); break;
            case "freezing": Cloud(4, 3, 16); Drops(2, false); ctx.DrawEllipse(Brushes.White, null, new Point(18, 20), 1.2, 1.2); break;
            case "snow":
                Cloud(4, 3, 16);
                var flake = new SolidColorBrush(p.Dark ? Colors.White : Color.Parse("#7FA6D6"));
                foreach (var (x, y) in new[] { (8.0, 18.5), (12.0, 20.5), (16.0, 18.5) }) ctx.DrawEllipse(flake, null, new Point(x, y), 1.4, 1.4);
                break;
            case "storm":
                Cloud(4, 3, 16);
                var bolt = new StreamGeometry();
                using (var g = bolt.Open())
                {
                    g.BeginFigure(new Point(13, 14), true);
                    g.LineTo(new Point(9.5, 19)); g.LineTo(new Point(12, 19)); g.LineTo(new Point(10.5, 23.5));
                    g.LineTo(new Point(15, 17.5)); g.LineTo(new Point(12.5, 17.5)); g.LineTo(new Point(14, 14));
                    g.EndFigure(true);
                }
                ctx.DrawGeometry(new SolidColorBrush(sun), null, bolt);
                break;
            default: Cloud(3, 6, 18); break;
        }
    }
}
