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

/// <summary>Activity rings (5.7.0): one ring per part, inside one another, each closed along its reading.</summary>
public sealed class ArtRings : ArtDecoration
{
    private readonly Color[] colours; private readonly double?[] levels;
    public ArtRings(Color[] colours) : base(colours.Length > 0 ? colours[0] : Colors.White) { this.colours = colours; levels = new double?[colours.Length]; }
    public void Set(int index, double? value) { levels[index] = value; InvalidateVisual(); }
    public override void Render(DrawingContext c)
    {
        double size = Math.Min(Bounds.Width, Bounds.Height); int n = colours.Length; if (size < 40 || n == 0) return;
        var centre = new Point(Bounds.Width / 2, Bounds.Height / 2);
        double outer = size / 2 - 1, stroke = outer / (n * 1.14 + .45), gap = stroke * .14;
        for (int k = 0; k < n; k++)
        {
            double r = outer - stroke / 2 - k * (stroke + gap);
            c.DrawEllipse(null, new Pen(BlackDraw.B(BlackDraw.A(colours[k], .2)), stroke), centre, r, r);
            double f = Math.Clamp(levels[k] ?? 0, 0, 1);
            if (f <= .005) continue;
            var pen = new Pen(BlackDraw.B(colours[k]), stroke) { LineCap = PenLineCap.Round };
            if (f >= .999) c.DrawEllipse(null, new Pen(BlackDraw.B(colours[k]), stroke), centre, r, r);
            else c.DrawGeometry(null, pen, BlackDraw.Arc(centre, r, -Math.PI / 2, Math.PI * 2 * f));
        }
    }
}

/// <summary>Live timing (5.7.0): the white position box and the team colour stripe at the start of a row.</summary>
public sealed class ArtTimingTag : ArtDecoration
{
    private readonly int position;
    public ArtTimingTag(int position, Color team) : base(team) { this.position = position; Width = 40; }
    public override void Render(DrawingContext c)
    {
        double h = Bounds.Height; if (h < 10) return;
        c.DrawRectangle(BlackDraw.B(Color.Parse("#F2F2F2")), null, new Rect(0, 0, 32, h));
        c.DrawRectangle(BlackDraw.B(Color), null, new Rect(32, 0, 5, h));
        var text = new FormattedText(position.ToString(System.Globalization.CultureInfo.InvariantCulture), System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(Ui.DesignFont(Ui.FamilyFor("barlow-condensed")), FontStyle.Normal, FontWeight.Bold), Math.Min(18, h * .5), BlackDraw.B(Color.Parse("#0A0A0A")));
        c.DrawText(text, new Point(16 - text.Width / 2, (h - text.Height) / 2));
    }
}

/// <summary>Glass tube (5.7.0): a clear tube with a bright highlight, filled with glowing liquid along a reading.</summary>
public sealed class ArtGlassTube : ArtDecoration
{
    private double? level;
    public ArtGlassTube(Color color) : base(color) { }
    public void Set(double? value) { level = value; InvalidateVisual(); }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 16 || h < 40) return;
        double r = w / 2;
        var glass = BlackDraw.H(Color.FromArgb(46, 255, 255, 255), Color.FromArgb(10, 255, 255, 255));
        c.DrawRectangle(glass, new Pen(BlackDraw.B(Color.FromArgb(76, 255, 255, 255)), 1), new Rect(.5, .5, w - 1, h - 1), r, r);
        double f = Math.Clamp(level ?? 0, 0, 1), inner = w - 10, fill = Math.Max(inner, (h - 10) * f);
        if (f > 0)
        {
            var rect = new Rect(5, h - 5 - fill, inner, fill);
            c.DrawRectangle(BlackDraw.B(BlackDraw.A(Color, .3)), null, rect.Inflate(4), inner / 2 + 4, inner / 2 + 4);
            var liquid = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = { new(Palette.Mix(Color, Colors.White, .25), 0), new(Color, .3), new(BlackDraw.A(Color, .6), 1) }
            };
            c.DrawRectangle(liquid, null, rect, inner / 2, inner / 2);
        }
        c.DrawRectangle(BlackDraw.B(Color.FromArgb(110, 255, 255, 255)), null, new Rect(w * .2, h * .05, Math.Max(2, w * .13), h * .62), w * .07, w * .07);
    }
}

/// <summary>Glass dial (5.7.0): a glass disc with a sheen, two rings inside one another, each closed along its reading.</summary>
public sealed class ArtGlassDial : ArtDecoration
{
    private readonly Color inner; private double? outerLevel, innerLevel;
    public ArtGlassDial(Color outer, Color inner) : base(outer) { this.inner = inner; }
    public void Set(int ring, double? value) { if (ring == 0) outerLevel = value; else innerLevel = value; InvalidateVisual(); }
    public override void Render(DrawingContext c)
    {
        double s = Math.Min(Bounds.Width, Bounds.Height); if (s < 60) return;
        var centre = new Point(Bounds.Width / 2, Bounds.Height / 2); double R = s / 2 - 1;
        var disc = new RadialGradientBrush
        {
            Center = new RelativePoint(.35, .25, RelativeUnit.Relative), GradientOrigin = new RelativePoint(.35, .25, RelativeUnit.Relative), RadiusX = new RelativeScalar(.8, RelativeUnit.Relative), RadiusY = new RelativeScalar(.8, RelativeUnit.Relative),
            GradientStops = { new(Color.FromArgb(80, 255, 255, 255), 0), new(Color.FromArgb(14, 255, 255, 255), .5), new(Color.FromArgb(30, 255, 255, 255), 1) }
        };
        c.DrawEllipse(disc, new Pen(BlackDraw.B(Color.FromArgb(90, 255, 255, 255)), 1), centre, R, R);
        void Ring(double r, double t, Color colour, double? level)
        {
            c.DrawEllipse(null, new Pen(BlackDraw.B(Color.FromArgb(30, 255, 255, 255)), t), centre, r, r);
            double f = Math.Clamp(level ?? 0, 0, 1); if (f <= .005) return;
            c.DrawGeometry(null, new Pen(BlackDraw.B(BlackDraw.A(colour, .3)), t + 6) { LineCap = PenLineCap.Round }, BlackDraw.Arc(centre, r, -Math.PI / 2, Math.PI * 2 * f));
            c.DrawGeometry(null, new Pen(BlackDraw.B(colour), t) { LineCap = PenLineCap.Round }, BlackDraw.Arc(centre, r, -Math.PI / 2, Math.PI * 2 * f));
        }
        Ring(R * .84, R * .085, Color, outerLevel); Ring(R * .68, R * .07, inner, innerLevel);
        c.DrawGeometry(null, new Pen(BlackDraw.B(Color.FromArgb(100, 255, 255, 255)), 2.5) { LineCap = PenLineCap.Round }, BlackDraw.Arc(centre, R * .92, Math.PI * 1.15, Math.PI * .5));
    }
}

/// <summary>Chrome bezel (5.7.0): a polished chrome ring round a black face with a glass sheen and an arc along a reading.</summary>
public sealed class ArtChromeBezel : ArtDecoration
{
    private double? level;
    public ArtChromeBezel(Color color) : base(color) { }
    public void Set(double? value) { level = value; InvalidateVisual(); }
    public override void Render(DrawingContext c)
    {
        double s = Math.Min(Bounds.Width, Bounds.Height); if (s < 50) return;
        var centre = new Point(Bounds.Width / 2, Bounds.Height / 2); double R = s / 2 - 1;
        var chrome = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = { new(Color.Parse("#F4F4F4"), 0), new(Color.Parse("#7A7A7A"), .45), new(Color.Parse("#2A2A2A"), .55), new(Color.Parse("#CFCFCF"), 1) }
        };
        c.DrawEllipse(chrome, null, centre, R, R);
        c.DrawEllipse(BlackDraw.B(Color.Parse("#050608")), null, centre, R * .85, R * .85);
        double f = Math.Clamp(level ?? 0, 0, 1), r = R * .7, t = Math.Max(4, R * .075);
        c.DrawGeometry(null, new Pen(BlackDraw.B(Color.FromArgb(30, 255, 255, 255)), t) { LineCap = PenLineCap.Round }, BlackDraw.Arc(centre, r, Math.PI * .75, Math.PI * 1.5));
        if (f > .005) c.DrawGeometry(null, new Pen(BlackDraw.B(Color), t) { LineCap = PenLineCap.Round }, BlackDraw.Arc(centre, r, Math.PI * .75, Math.PI * 1.5 * f));
        var sheen = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = { new(Color.FromArgb(56, 255, 255, 255), 0), new(Color.FromArgb(5, 255, 255, 255), .45), new(Color.FromArgb(0, 255, 255, 255), 1) }
        };
        c.DrawEllipse(sheen, null, centre, R * .85, R * .85);
    }
}

/// <summary>Glossy pill (5.7.0): a glossy coloured capsule with a highlight, like a sweet; the number sits on it.</summary>
public sealed class ArtGlossPill : ArtDecoration
{
    public ArtGlossPill(Color color) : base(color) { }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 16 || h < 30) return;
        double r = w / 2;
        c.DrawRectangle(BlackDraw.B(BlackDraw.A(Color, .3)), null, new Rect(0, 4, w, h - 2).Inflate(2), r + 2, r + 2);
        var body = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops = { new(Palette.Mix(Color, Colors.Black, .35), 0), new(Palette.Mix(Color, Colors.White, .18), .35), new(Color, .6), new(Palette.Mix(Color, Colors.Black, .45), 1) }
        };
        c.DrawRectangle(body, null, new Rect(0, 0, w, h), r, r);
        var shine = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = { new(Color.FromArgb(210, 255, 255, 255), 0), new(Color.FromArgb(0, 255, 255, 255), 1) }
        };
        c.DrawRectangle(shine, null, new Rect(w * .16, h * .06, w * .26, h * .55), w * .13, w * .13);
    }
}
