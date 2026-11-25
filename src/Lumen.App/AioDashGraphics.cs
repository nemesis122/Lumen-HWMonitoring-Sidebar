using Avalonia;
using Avalonia.Media;

namespace Lumen.App;

/// <summary>Race telemetry: the shift lights of a steering wheel, lit along a reading (green, red, blue).</summary>
public sealed class ArtShiftLights : ArtDecoration
{
    private double? level;
    public ArtShiftLights() : base(Color.Parse("#3DFF8B")) { Height = 12; }
    public void Set(double? value) { level = value; InvalidateVisual(); }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; const int n = 12; if (w < 40) return;
        double gap = 4, cell = (w - gap * (n - 1)) / n; int lit = (int)Math.Round(Math.Clamp(level ?? 0, 0, 1) * n);
        for (int i = 0; i < n; i++)
        {
            var colour = Color.Parse(i < 4 ? "#3DFF8B" : i < 8 ? "#FF3B3B" : "#5AA9FF");
            var rect = new Rect(i * (cell + gap), 0, cell, h);
            if (i < lit) c.DrawRectangle(BlackDraw.B(BlackDraw.A(colour, .35)), null, rect.Inflate(2), 4, 4);
            c.DrawRectangle(BlackDraw.B(BlackDraw.A(colour, i < lit ? 1 : .16)), null, rect, 3, 3);
        }
    }
}

/// <summary>Race telemetry: the round buttons at the bottom of the wheel.</summary>
public sealed class ArtWheelButtons : ArtDecoration
{
    public ArtWheelButtons() : base(Colors.White) { Height = 30; }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width; if (w < 60) return;
        string[] colours = { "#FF3B3B", "#FFD400", "#3DFF8B", "#5AA9FF", "#F2F2F2" };
        for (int i = 0; i < colours.Length; i++)
        {
            var at = new Point(15 + i * (w - 30) / (colours.Length - 1), 15);
            c.DrawEllipse(BlackDraw.B(Color.Parse(colours[i])), null, at, 13, 13);
            c.DrawEllipse(BlackDraw.B(Color.FromArgb(90, 0, 0, 0)), null, new Point(at.X, at.Y + 4), 11, 8);
        }
    }
}

/// <summary>Control room: a tank filled to its level, with a scale and pipe stubs left and right.</summary>
public sealed class ArtTank : ArtDecoration
{
    private double? level; private readonly Color pipe;
    public ArtTank(Color color, Color pipe) : base(color) { this.pipe = pipe; }
    public void Set(double? value) { level = value; InvalidateVisual(); }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 30 || h < 40) return;
        double tw = Math.Min(54, w - 16), x = (w - tw) / 2;
        var pipePen = new Pen(BlackDraw.B(pipe), 6);
        c.DrawLine(pipePen, new Point(0, h * .5), new Point(x, h * .5)); c.DrawLine(pipePen, new Point(x + tw, h * .5), new Point(w, h * .5));
        var body = new Rect(x, 0, tw, h);
        c.DrawRectangle(BlackDraw.B(Color.Parse("#0C1014")), new Pen(BlackDraw.B(Color.Parse("#3A4450")), 1.2), body, 6, 6);
        double f = Math.Clamp(level ?? 0, 0, 1);
        if (f > 0) c.DrawRectangle(BlackDraw.B(BlackDraw.A(Color, .78)), null, new Rect(x + 3, 3 + (h - 6) * (1 - f), tw - 6, (h - 6) * f), 4, 4);
        var tick = new Pen(BlackDraw.B(Color.Parse("#3A4450")), 1);
        for (int k = 1; k < 10; k++) c.DrawLine(tick, new Point(x + tw - 7, h * k / 10), new Point(x + tw - 2, h * k / 10));
    }
}

/// <summary>Gauge wall: a glowing half ring filled along the reading.</summary>
public sealed class ArtHalfGauge : ArtDecoration
{
    private double? level; private readonly Color track;
    public ArtHalfGauge(Color color, Color track) : base(color) { this.track = track; }
    public void Set(double? value) { level = value; InvalidateVisual(); }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 30 || h < 20) return;
        double t = Math.Max(6, w * .085), r = Math.Min(w / 2, h) - t / 2 - 2;
        var centre = new Point(w / 2, h - 2);
        Geometry Arc(double share)
        {
            var g = new StreamGeometry();
            using var s = g.Open();
            s.BeginFigure(new Point(centre.X - r, centre.Y), false);
            double a = Math.PI + Math.PI * share;
            s.ArcTo(new Point(centre.X + r * Math.Cos(a), centre.Y + r * Math.Sin(a)), new Size(r, r), 0, false, SweepDirection.Clockwise);
            s.EndFigure(false);
            return g;
        }
        c.DrawGeometry(null, new Pen(BlackDraw.B(track), t) { LineCap = PenLineCap.Round }, Arc(.9999));
        double f = Math.Clamp(level ?? 0, 0, 1);
        if (f > .005)
        {
            c.DrawGeometry(null, new Pen(BlackDraw.B(BlackDraw.A(Color, .3)), t + 6) { LineCap = PenLineCap.Round }, Arc(f));
            c.DrawGeometry(null, new Pen(BlackDraw.B(Color), t) { LineCap = PenLineCap.Round }, Arc(f));
        }
    }
}

/// <summary>Studio mixer: a VU meter with a cream face, a scale with a red end and a needle.</summary>
public sealed class ArtVu : ArtDecoration
{
    private double? level;
    public ArtVu() : base(Color.Parse("#E3D3A6")) { }
    public void Set(double? value) { level = value; InvalidateVisual(); }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 40 || h < 30) return;
        var face = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = { new(Color.Parse("#F4E9C8"), 0), new(Color.Parse("#E3D3A6"), 1) }
        };
        c.DrawRectangle(face, new Pen(BlackDraw.B(Color.Parse("#2A2A2A")), 3), new Rect(1.5, 1.5, w - 3, h - 3), 6, 6);
        var pivot = new Point(w / 2, h * 1.08); double r = h * .86;
        Point At(double a, double radius) => new(pivot.X + radius * Math.Cos(a * Math.PI / 180), pivot.Y + radius * Math.Sin(a * Math.PI / 180));
        var ink = new Pen(BlackDraw.B(Color.Parse("#222222")), 1.2);
        for (int k = 0; k <= 10; k++) c.DrawLine(ink, At(-140 + k * 10, r), At(-140 + k * 10, r - (k % 5 == 0 ? 10 : 6)));
        var arc = new StreamGeometry();
        using (var s = arc.Open()) { s.BeginFigure(At(-62, r), false); s.ArcTo(At(-40, r), new Size(r, r), 0, false, SweepDirection.Clockwise); s.EndFigure(false); }
        c.DrawGeometry(null, new Pen(BlackDraw.B(Color.Parse("#C8102E")), 4), arc);
        double a = -140 + 100 * Math.Clamp(level ?? 0, 0, 1);
        c.DrawLine(new Pen(BlackDraw.B(Color.Parse("#111111")), 1.8), pivot, At(a, r + 2));
    }
}

/// <summary>Studio mixer: a fader slot with its knob at the reading.</summary>
public sealed class ArtFader : ArtDecoration
{
    private double? level;
    public ArtFader() : base(Colors.White) { }
    public void Set(double? value) { level = value; InvalidateVisual(); }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 20 || h < 40) return;
        double cx = w / 2, knob = 20;
        c.DrawRectangle(BlackDraw.B(Color.Parse("#1A1A1A")), new Pen(BlackDraw.B(Color.Parse("#000000")), 1), new Rect(cx - 4, 0, 8, h), 4, 4);
        var mark = new Pen(BlackDraw.B(Color.Parse("#3A3A3A")), 1);
        for (int k = 0; k <= 10; k++) c.DrawLine(mark, new Point(cx + 8, knob / 2 + (h - knob) * k / 10), new Point(cx + 13, knob / 2 + (h - knob) * k / 10));
        double y = (h - knob) * (1 - Math.Clamp(level ?? 0, 0, 1));
        var cap = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = { new(Color.Parse("#CFCFCF"), 0), new(Color.Parse("#6B6B6B"), 1) }
        };
        c.DrawRectangle(BlackDraw.B(Color.FromArgb(160, 0, 0, 0)), null, new Rect(cx - 18, y + 2, 36, knob), 3, 3);
        c.DrawRectangle(cap, null, new Rect(cx - 18, y, 36, knob), 3, 3);
        c.DrawLine(new Pen(BlackDraw.B(Color.Parse("#222222")), 1.5), new Point(cx - 14, y + knob / 2), new Point(cx + 14, y + knob / 2));
    }
}

/// <summary>Capsule glass: a tall glass capsule filled from the bottom, with a highlight.</summary>
public sealed class ArtCapsule : ArtDecoration
{
    private double? level;
    public ArtCapsule(Color color) : base(color) { }
    public void Set(double? value) { level = value; InvalidateVisual(); }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 16 || h < 40) return;
        double r = w / 2;
        var glass = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops = { new(Color.FromArgb(26, 255, 255, 255), 0), new(Color.FromArgb(8, 255, 255, 255), .4), new(Color.FromArgb(20, 255, 255, 255), 1) }
        };
        c.DrawRectangle(glass, new Pen(BlackDraw.B(Color.FromArgb(36, 255, 255, 255)), 1), new Rect(.5, .5, w - 1, h - 1), r, r);
        double f = Math.Clamp(level ?? 0, 0, 1), inner = w - 8, fill = Math.Max(inner, (h - 8) * f);
        if (f > 0)
        {
            var liquid = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = { new(Color, 0), new(BlackDraw.A(Color, .55), 1) }
            };
            var rect = new Rect(4, h - 4 - fill, inner, fill);
            c.DrawRectangle(BlackDraw.B(BlackDraw.A(Color, .25)), null, rect.Inflate(3), inner / 2 + 3, inner / 2 + 3);
            c.DrawRectangle(liquid, null, rect, inner / 2, inner / 2);
        }
        c.DrawRectangle(BlackDraw.B(Color.FromArgb(46, 255, 255, 255)), null, new Rect(w * .18, h * .08, Math.Max(2, w * .11), h * .58), w * .06, w * .06);
    }
}

/// <summary>Living PC: the colour of a temperature (30 °C blue, 50 °C teal, 65 °C orange, 85 °C red).</summary>
internal static class HeatColour
{
    private static readonly (double At, Color C)[] Stops =
        { (30, Color.FromRgb(60, 140, 255)), (50, Color.FromRgb(40, 220, 200)), (65, Color.FromRgb(255, 170, 60)), (85, Color.FromRgb(255, 60, 60)) };
    public static Color Of(double? celsius)
    {
        double t = Math.Clamp(celsius ?? 30, 30, 85);
        for (int i = 1; i < Stops.Length; i++)
            if (t <= Stops[i].At) return Palette.Mix(Stops[i - 1].C, Stops[i].C, (t - Stops[i - 1].At) / (Stops[i].At - Stops[i - 1].At));
        return Stops[^1].C;
    }
}

/// <summary>Living PC: a soft round glow in the colour of the part's temperature, and its shadow on the floor.</summary>
public sealed class ArtHeatGlow : ArtDecoration
{
    private Color heat = HeatColour.Of(null);
    private readonly bool floor;
    public ArtHeatGlow(bool floor = true) : base(Colors.White) { this.floor = floor; }
    public void Set(double? celsius) { heat = HeatColour.Of(celsius); InvalidateVisual(); }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 10 || h < 10) return;
        // As strong as in the drafts: a bright core that fades out well beyond the part.
        var glow = new RadialGradientBrush { GradientStops = { new(BlackDraw.A(heat, .8), 0), new(BlackDraw.A(heat, .34), .5), new(BlackDraw.A(heat, 0), 1) } };
        double r = Math.Min(w, h) * .7;
        c.DrawEllipse(glow, null, new Point(w / 2, h * .46), r, r * .9);
        if (floor) c.DrawEllipse(BlackDraw.B(BlackDraw.A(heat, .5)), null, new Point(w / 2, h - 5), w * .32, 4.5);
    }
}

/// <summary>Living PC: a glass card with a border and a corner glow in the colour of the part's temperature.</summary>
public sealed class ArtHeatCard : ArtDecoration
{
    private Color heat = HeatColour.Of(null); private readonly Color surface;
    public ArtHeatCard(Color surface) : base(Colors.White) { this.surface = surface; ClipToBounds = true; }
    public void Set(double? celsius) { heat = HeatColour.Of(celsius); InvalidateVisual(); }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 20 || h < 20) return;
        var fill = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = { new(Palette.Mix(surface, Colors.White, .07), 0), new(Palette.Mix(surface, Colors.White, .02), 1) }
        };
        c.DrawRectangle(fill, null, new Rect(0, 0, w, h), 16, 16);
        var glow = new RadialGradientBrush { GradientStops = { new(BlackDraw.A(heat, .35), 0), new(BlackDraw.A(heat, 0), 1) } };
        using (c.PushClip(new RoundedRect(new Rect(0, 0, w, h), 16)))
            c.DrawEllipse(glow, null, new Point(w - 50, 30), 110, 110);
        c.DrawRectangle(null, new Pen(BlackDraw.B(BlackDraw.A(heat, .4)), 1), new Rect(.5, .5, w - 1, h - 1), 16, 16);
    }
}

/// <summary>Living PC: the temperature colours with their degrees.</summary>
public sealed class ArtHeatLegend : ArtDecoration
{
    private static readonly Typeface Face = new(Ui.Inter);
    public ArtHeatLegend() : base(Colors.White) { Height = 14; }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width; int[] marks = { 30, 45, 60, 75, 85 }; if (w < 100) return;
        double step = 52, x = (w - step * marks.Length) / 2;
        foreach (int m in marks)
        {
            c.DrawEllipse(BlackDraw.B(HeatColour.Of(m)), null, new Point(x + 5, 7), 5, 5);
            c.DrawText(new FormattedText(m + "°", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Face, 10, BlackDraw.B(Color.Parse("#8B939C"))), new Point(x + 14, 0));
            x += step;
        }
    }
}

/// <summary>Floating podiums: a glowing round pedestal in the colour of the part's temperature.</summary>
public sealed class ArtPodium : ArtDecoration
{
    private Color heat = HeatColour.Of(null);
    public ArtPodium() : base(Colors.White) { }
    public void Set(double? celsius) { heat = HeatColour.Of(celsius); InvalidateVisual(); }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 20 || h < 6) return;
        var centre = new Point(w / 2, h / 2); double rx = w / 2 - 3, ry = h / 2 - 3;
        var fill = new RadialGradientBrush { GradientStops = { new(BlackDraw.A(heat, .45), 0), new(Color.FromArgb(255, 11, 13, 16), .75) } };
        c.DrawEllipse(BlackDraw.B(BlackDraw.A(heat, .25)), null, centre, rx + 3, ry + 3);
        c.DrawEllipse(fill, new Pen(BlackDraw.B(heat), 2), centre, rx, ry);
    }
}

/// <summary>Part cards heat: the temperature scale (blue to red) with a marker at the part's temperature.</summary>
public sealed class ArtHeatScale : ArtDecoration
{
    private double? celsius;
    public ArtHeatScale() : base(Colors.White) { Height = 14; }
    public void Set(double? value) { celsius = value; InvalidateVisual(); }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 20) return;
        var scale = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops = { new(HeatColour.Of(30), 0), new(HeatColour.Of(50), 20 / 55d), new(HeatColour.Of(65), 35 / 55d), new(HeatColour.Of(85), 1) }
        };
        c.DrawRectangle(scale, null, new Rect(0, h / 2 - 3, w, 6), 3, 3);
        if (celsius is double t)
        {
            double x = Math.Clamp((t - 30) / 55, 0, 1) * (w - 3);
            c.DrawRectangle(BlackDraw.B(Color.FromArgb(90, 255, 255, 255)), null, new Rect(x - 2, 0, 7, h), 3, 3);
            c.DrawRectangle(BlackDraw.B(Colors.White), null, new Rect(x, 1, 3, h - 2), 1.5, 1.5);
        }
    }
}
