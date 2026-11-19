using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Lumen.App;

internal sealed class InstrumentDialFace : Grid
{
    private readonly Control dial;
    private readonly Viewbox number;

    public InstrumentDialFace(Control dial, Control face, Viewbox number)
    {
        this.dial = dial;
        this.number = number;
        Children.Add(dial);
        Children.Add(face);
        ClipToBounds = true;
    }

    protected override Size MeasureOverride(Size available)
    {
        number.MaxWidth = Math.Max(12, Math.Min(available.Width, dial.MaxWidth) * .65);
        return base.MeasureOverride(available);
    }
}

internal sealed class InstrumentHousing(bool amber, Color edge, Color face, Color recess, Color primary, Color marker) : Control
{
    public override void Render(DrawingContext c)
    {
        var b = new Rect(Bounds.Size);
        if (b.Width < 45 || b.Height < 30) return;
        if (face.A == 255)
        {
            Plate(c, b.Deflate(1), 11, edge, recess, edge);
            Plate(c, b.Deflate(4), 8, recess, edge, recess);
            Plate(c, b.Deflate(7), 6, face, recess, edge);
        }
        else
        {
            Plate(c, b.Deflate(1), 11, face, recess, edge);
            Plate(c, b.Deflate(4), 8, Colors.Transparent, Colors.Transparent, recess);
            Plate(c, b.Deflate(7), 6, Colors.Transparent, Colors.Transparent, edge);
        }
        foreach (double x in new[] { 12d, b.Width - 12 })
        {
            double top = Math.Min(15, b.Height / 3);
            double bottom = b.Height - top;
            if (amber)
            {
                foreach (double y in new[] { top, bottom })
                {
                    var center = new Point(x, y);
                    c.DrawEllipse(Palette.Brush(recess), null, center, 7, 7);
                    c.DrawEllipse(Palette.Brush(edge), null, center, 4.5, 4.5);
                    c.DrawEllipse(Palette.Brush(Palette.Mix(edge, Colors.White, .43)), null, center - new Point(1, 1), 1.5, 1.5);
                    c.DrawLine(new Pen(Palette.Brush(recess), 1), center + new Point(-2, 2), center + new Point(2, -2));
                }
            }
            else
            {
                foreach (double y in new[] { top, bottom })
                {
                    Rect(c, recess, x - 3, y - 4, 6, 8);
                    Rect(c, marker, x - 1, y - 2.5, 2, 5);
                }
                double h = bottom - top - 26;
                if (h <= 0) continue;
                Rect(c, recess, x - 3, top + 13, 6, h);
                Rect(c, Fade(primary, 44), x - 2.5, top + 14, 5, h - 2);
                Rect(c, primary, x - .85, top + 14, 1.7, h - 2);
            }
        }
    }

    static void Plate(DrawingContext c, Rect b, double bevel, Color top, Color bottom, Color line)
    {
        var figure = new StreamGeometry();
        using (var path = figure.Open())
        {
            path.BeginFigure(new Point(b.X + bevel, b.Y), true);
            path.LineTo(new Point(b.Right - bevel, b.Y));
            path.LineTo(new Point(b.Right, b.Y + bevel));
            path.LineTo(new Point(b.Right, b.Bottom - bevel));
            path.LineTo(new Point(b.Right - bevel, b.Bottom));
            path.LineTo(new Point(b.X + bevel, b.Bottom));
            path.LineTo(new Point(b.X, b.Bottom - bevel));
            path.LineTo(new Point(b.X, b.Y + bevel));
            path.EndFigure(true);
        }
        c.DrawGeometry(new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = { new(top, 0), new(bottom, 1) } }, new Pen(Palette.Brush(line), 1), figure);
    }

    internal static Color Fade(Color c, byte alpha) => Color.FromArgb((byte)(c.A * alpha / 255), c.R, c.G, c.B);
    internal static void Rect(DrawingContext c, Color fill, double x, double y, double w, double h)
    {
        if (w > 0 && h > 0) c.DrawRectangle(Palette.Brush(fill), null, new Rect(x, y, w, h));
    }
}

internal sealed class InstrumentGauge(bool amber, bool column, Color color, Color back, Color track, bool bar = false) : Control, IColourGraph
{
    private double? level;

    public void Set(double? value)
    {
        if (level == value) return;
        level = value;
        InvalidateVisual();
    }

    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height;
        if (w < 2 || h < 2) return;
        if (bar || column)
        {
            InstrumentHousing.Rect(c, back, 0, 0, w, h);
            if (bar && amber)
            {
                double width = w * level.GetValueOrDefault();
                InstrumentHousing.Rect(c, InstrumentHousing.Fade(color, 70), 0, 0, width, h);
                InstrumentHousing.Rect(c, color, 0, h * .38, width, h * .34);
                return;
            }
            int count = column ? Math.Clamp((int)(h / 5), 3, 12) : 12;
            double step = (column ? h : w) / count;
            for (int i = 0; i < count; i++)
            {
                var tint = i < count * level.GetValueOrDefault() ? color : track;
                if (column) InstrumentHousing.Rect(c, tint, w * .18, h - (i + 1) * step + .8, w * .64, step * .55);
                else InstrumentHousing.Rect(c, tint, i * step, h * .25, step * .78, h * .5);
            }
            return;
        }
        double radius = Math.Min(w, h) / 2 - 2;
        if (radius < 8) return;
        var center = new Point(w / 2, h / 2);
        c.DrawEllipse(Palette.Brush(back), null, center, radius + 1, radius + 1);
        c.DrawEllipse(null, new Pen(Palette.Brush(track), 2.1), center, radius - 2, radius - 2);
        c.DrawEllipse(null, new Pen(Palette.Brush(track), .6), center, radius - 5, radius - 5);
        if (amber)
        {
            var ring = level.HasValue ? color : InstrumentHousing.Fade(color, 112);
            c.DrawEllipse(null, new Pen(Palette.Brush(InstrumentHousing.Fade(ring, 59)), 6), center, radius * .75, radius * .75);
            c.DrawEllipse(null, new Pen(Palette.Brush(ring), 2.3), center, radius * .75, radius * .75);
            return;
        }
        for (int i = 0; i < 9; i++)
        {
            bool active = i < 9 * level.GetValueOrDefault();
            double start = 132 + i * 30;
            DrawArc(back, 5, radius * .82, start, 23);
            if (!active) continue;
            DrawArc(InstrumentHousing.Fade(color, 76), 5.5, radius * .82, start, 23);
            DrawArc(color, 2.1, radius * .82, start, 23);
        }
        for (int i = 0; i <= 30; i++)
        {
            double angle = 132 + i * 9;
            c.DrawLine(new Pen(Palette.Brush(InstrumentHousing.Fade(color, i % 5 == 0 ? (byte)165 : (byte)75)), .9),
                At(radius * .62, angle), At(radius * (i % 5 == 0 ? .54 : .58), angle));
        }

        Point At(double r, double angle)
        {
            double a = angle * Math.PI / 180;
            return new Point(center.X + Math.Cos(a) * r, center.Y + Math.Sin(a) * r);
        }
        void DrawArc(Color tint, double stroke, double r, double start, double sweep)
        {
            var arc = new StreamGeometry();
            using (var path = arc.Open())
            {
                path.BeginFigure(At(r, start), false);
                path.ArcTo(At(r, start + sweep), new Size(r, r), 0, false, SweepDirection.Clockwise);
                path.EndFigure(false);
            }
            c.DrawGeometry(null, new Pen(Palette.Brush(tint), stroke), arc);
        }
    }
}

internal sealed class InstrumentHistory(Color color, Color second, Color back, Color grid) : Control, IColourGraph
{
    private double[] primary = Array.Empty<double>(), secondary = Array.Empty<double>();
    public void Set(double[] firstValues, double[] secondValues)
    {
        primary = firstValues;
        secondary = secondValues;
        InvalidateVisual();
    }

    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height;
        if (w < 2 || h < 2) return;
        InstrumentHousing.Rect(c, back, 0, 0, w, h);
        var pen = new Pen(Palette.Brush(grid), .6);
        for (int i = 0; i <= 10; i++) c.DrawLine(pen, new Point(i * w / 10, 0), new Point(i * w / 10, h));
        for (int i = 0; i <= 4; i++) c.DrawLine(pen, new Point(0, i * h / 4), new Point(w, i * h / 4));
        Draw(primary, color);
        Draw(secondary, second);

        void Draw(double[] values, Color tint)
        {
            if (values.Length == 0) return;
            IEnumerable<Point> points = values.Select((v, i) => new Point(i * (w - 2) / Math.Max(1, values.Length - 1) + 1,
                double.IsFinite(v) ? h - 1 - (h - 2) * Math.Clamp(v / 100, 0, 1) : double.NaN));
            var segment = new List<Point>();
            foreach (var point in points.Append(new Point(0, double.NaN)))
            {
                if (double.IsFinite(point.Y)) { segment.Add(point); continue; }
                if (segment.Count == 1) c.DrawEllipse(Palette.Brush(tint), null, segment[0], 1.2, 1.2);
                if (segment.Count > 1)
                {
                    var fill = new StreamGeometry();
                    using (var path = fill.Open())
                    {
                        path.BeginFigure(new Point(segment[0].X, h), true);
                        foreach (var p in segment) path.LineTo(p);
                        path.LineTo(new Point(segment[^1].X, h));
                        path.EndFigure(true);
                    }
                    c.DrawGeometry(Palette.Brush(InstrumentHousing.Fade(tint, 55)), null, fill);
                    var trace = new Pen(Palette.Brush(tint), 1.3);
                    for (int i = 1; i < segment.Count; i++) c.DrawLine(trace, segment[i - 1], segment[i]);
                }
                segment.Clear();
            }
        }
    }
}
