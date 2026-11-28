using Avalonia;
using Avalonia.Media;

namespace Lumen.App;

/// <summary>Liquid Metal (5.9.0): a dark groove filled with polished mercury up to the reading.</summary>
public sealed class ArtMetalTube : ArtDecoration
{
    private double? level;
    public ArtMetalTube() : base(Colors.White) { Height = 12; }
    public void Set(double? value) { level = value; InvalidateVisual(); }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 20 || h < 4) return;
        double r = h / 2;
        c.DrawRectangle(BlackDraw.V(Color.Parse("#0B0C0E"), Color.Parse("#1C1E22")), new Pen(BlackDraw.B(Color.Parse("#2A2D32")), 1), new Rect(.5, .5, w - 1, h - 1), r, r);
        double f = Math.Clamp(level ?? 0, 0, 1); if (f <= 0) return;
        double fill = Math.Max(h, (w - 2) * f);
        var mercury = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = { new(Color.Parse("#FFFFFF"), 0), new(Color.Parse("#AEB4BC"), .3), new(Color.Parse("#5D636B"), .55), new(Color.Parse("#D9DDE2"), .78), new(Color.Parse("#8B9199"), 1) }
        };
        c.DrawRectangle(mercury, null, new Rect(1, 1, fill, h - 2), r - 1, r - 1);
    }
}

/// <summary>Power Flow (5.9.0): bands from one source on the left to every part on the right, each as thick as its share.</summary>
public sealed class ArtPowerFlow : ArtDecoration
{
    private readonly Color[] colours; private readonly double?[] watts; private readonly bool[] metered;
    public ArtPowerFlow(Color[] colours, bool[] metered) : base(Colors.White)
    {
        this.colours = colours; this.metered = metered; watts = new double?[colours.Length];
    }
    public void Set(int index, double? value) { watts[index] = value; InvalidateVisual(); }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; int n = colours.Length; if (w < 20 || h < 20 || n == 0) return;
        double row = h / n, total = 0;
        for (int k = 0; k < n; k++) if (metered[k]) total += Math.Max(0, watts[k] ?? 0);
        // Thickness: metered parts share most of the height by their watts (at least a fine line), unmetered parts a fine line.
        double most = 0; for (int k = 0; k < n; k++) if (metered[k]) most = Math.Max(most, watts[k] ?? 0);
        // No band thicker than its row, all of them together within the height: the flow stays inside the card.
        double room = h * .72, scale = Math.Min(room / Math.Max(total, 60), row * .8 / Math.Max(most, 1));
        var thick = new double[n];
        for (int k = 0; k < n; k++) thick[k] = metered[k] ? Math.Max(3, Math.Max(0, watts[k] ?? 0) * scale) : 2;
        double stack = thick.Sum(), y = (h - stack) / 2;
        c.DrawRectangle(BlackDraw.B(Color.FromArgb(235, 255, 255, 255)), null, new Rect(0, y - 2, 4, stack + 4), 2, 2);
        for (int k = 0; k < n; k++)
        {
            double a0 = y, a1 = y + thick[k], mid = (k + .5) * row, b0 = mid - thick[k] / 2, b1 = mid + thick[k] / 2, x0 = 4, x1 = w - 4, cx = (x0 + x1) / 2;
            var band = new StreamGeometry();
            using (var s = band.Open())
            {
                s.BeginFigure(new Point(x0, a0), true);
                s.CubicBezierTo(new Point(cx, a0), new Point(cx, b0), new Point(x1, b0));
                s.LineTo(new Point(x1, b1));
                s.CubicBezierTo(new Point(cx, b1), new Point(cx, a1), new Point(x0, a1));
                s.EndFigure(true);
            }
            var fill = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                GradientStops = { new(Color.FromArgb(120, 255, 255, 255), 0), new(BlackDraw.A(colours[k], metered[k] ? .9 : .5), 1) }
            };
            c.DrawGeometry(fill, null, band);
            c.DrawRectangle(BlackDraw.B(colours[k]), null, new Rect(x1, b0, 4, Math.Max(2, b1 - b0)), 1.5, 1.5);
            y = a1;
        }
    }
}

/// <summary>Holo Cards (5.9.0): the foil sheen across a card, a soft diagonal band of light.</summary>
public sealed class ArtHoloSheen : ArtDecoration
{
    public ArtHoloSheen() : base(Colors.White) { }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 10 || h < 10) return;
        var sheen = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = { new(Color.FromArgb(0, 255, 255, 255), .3), new(Color.FromArgb(34, 255, 255, 255), .45), new(Color.FromArgb(30, 160, 120, 255), .52), new(Color.FromArgb(0, 255, 255, 255), .66) }
        };
        c.DrawRectangle(sheen, null, new Rect(0, 0, w, h), 12, 12);
    }
}

/// <summary>Holo Cards (5.9.0): a thin bar in the foil colours, filled along the reading.</summary>
public sealed class ArtHoloBar : ArtDecoration
{
    private double? level;
    public ArtHoloBar() : base(Colors.White) { Height = 6; }
    public void Set(double? value) { level = value; InvalidateVisual(); }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 10) return;
        c.DrawRectangle(BlackDraw.B(Color.Parse("#1C2130")), null, new Rect(0, 0, w, h), h / 2, h / 2);
        double f = Math.Clamp(level ?? 0, 0, 1); if (f <= 0) return;
        var foil = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Absolute), EndPoint = new RelativePoint(w, 0, RelativeUnit.Absolute),
            GradientStops = { new(Color.Parse("#4FC8FF"), 0), new(Color.Parse("#A64FFF"), .55), new(Color.Parse("#FF4FD8"), 1) }
        };
        c.DrawRectangle(foil, null, new Rect(0, 0, Math.Max(h, w * f), h), h / 2, h / 2);
    }

    /// <summary>The rainbow foil around a card.</summary>
    public static IBrush Foil() => new ConicGradientBrush
    {
        Center = new RelativePoint(.5, .5, RelativeUnit.Relative), Angle = 210,
        GradientStops = { new(Color.Parse("#FF4FD8"), 0), new(Color.Parse("#FFD24F"), .2), new(Color.Parse("#4FFFB0"), .4), new(Color.Parse("#4FC8FF"), .6), new(Color.Parse("#A64FFF"), .8), new(Color.Parse("#FF4FD8"), 1) }
    };
}

/// <summary>Loadout (5.9.0): the frame of the equipped item, with cut corners, in the colour of its temperature.</summary>
public sealed class ArtLoadoutFrame : ArtDecoration
{
    private Color heat = HeatColour.Of(null);
    public ArtLoadoutFrame() : base(Colors.White) { }
    public void Set(double? celsius) { heat = HeatColour.Of(celsius); InvalidateVisual(); }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 30 || h < 30) return;
        double cut = Math.Min(18, h * .14);
        var shape = new StreamGeometry();
        using (var s = shape.Open())
        {
            s.BeginFigure(new Point(.5, .5), true); s.LineTo(new Point(w - cut, .5)); s.LineTo(new Point(w - .5, cut)); s.LineTo(new Point(w - .5, h - .5));
            s.LineTo(new Point(cut, h - .5)); s.LineTo(new Point(.5, h - cut)); s.EndFigure(true);
        }
        var fill = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, .6, RelativeUnit.Relative),
            GradientStops = { new(BlackDraw.A(heat, .24), 0), new(BlackDraw.A(heat, .02), .75) }
        };
        c.DrawGeometry(fill, new Pen(BlackDraw.B(heat), 1.2), shape);
    }
}

/// <summary>Loadout (5.9.0): a stat bar of twelve slanted segments, lit along the reading.</summary>
public sealed class ArtSegmentStat : ArtDecoration
{
    private double? level; private Color colour;
    public ArtSegmentStat(Color colour) : base(colour) { this.colour = colour; Height = 10; }
    public void Set(double? value, Color? tint = null) { level = value; if (tint is Color t) colour = t; InvalidateVisual(); }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; const int n = 12; if (w < 40) return;
        double gap = 3, cell = (w - gap * (n - 1) - h * .4) / n; int lit = (int)Math.Round(Math.Clamp(level ?? 0, 0, 1) * n);
        for (int i = 0; i < n; i++)
        {
            double x = i * (cell + gap);
            var seg = new StreamGeometry();
            using (var s = seg.Open())
            {
                s.BeginFigure(new Point(x + h * .4, 0), true); s.LineTo(new Point(x + h * .4 + cell, 0)); s.LineTo(new Point(x + cell, h)); s.LineTo(new Point(x, h)); s.EndFigure(true);
            }
            c.DrawGeometry(BlackDraw.B(i < lit ? colour : Color.Parse("#1B1F2A")), null, seg);
        }
    }
}

/// <summary>Ambilight (5.9.0): a glowing LED strip at the left edge and the light it throws into the card, in the colour of the temperature.</summary>
public sealed class ArtAmbilight : ArtDecoration
{
    private Color heat = HeatColour.Of(null);
    public ArtAmbilight() : base(Colors.White) { ClipToBounds = true; }
    public void Set(double? celsius) { heat = HeatColour.Of(celsius); InvalidateVisual(); }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 20 || h < 20) return;
        var spill = new RadialGradientBrush
        {
            Center = new RelativePoint(0, .3, RelativeUnit.Relative), GradientOrigin = new RelativePoint(0, .3, RelativeUnit.Relative),
            RadiusX = new RelativeScalar(.75, RelativeUnit.Relative), RadiusY = new RelativeScalar(1.1, RelativeUnit.Relative),
            GradientStops = { new(BlackDraw.A(heat, .30), 0), new(BlackDraw.A(heat, .08), .55), new(BlackDraw.A(heat, 0), 1) }
        };
        c.DrawRectangle(spill, null, new Rect(0, 0, w, h));
        c.DrawRectangle(BlackDraw.B(BlackDraw.A(heat, .35)), null, new Rect(0, 4, 12, h - 8), 6, 6);
        c.DrawRectangle(BlackDraw.B(heat), null, new Rect(2, 6, 6, h - 12), 3, 3);
        c.DrawRectangle(BlackDraw.B(Color.FromArgb(150, 255, 255, 255)), null, new Rect(4, 8, 2, h - 16), 1, 1);
    }
}

/// <summary>Cover Story (5.9.0): the bar code on a magazine cover (a drawing, no number in it).</summary>
public sealed class ArtBarcode : ArtDecoration
{
    public ArtBarcode() : base(Colors.Black) { Width = 74; Height = 30; }
    public override void Render(DrawingContext c)
    {
        double h = Bounds.Height; int[] bars = { 2, 1, 3, 1, 2, 4, 1, 2, 1, 3, 2, 1, 1, 3, 2, 4, 1, 2, 1, 2, 3, 1 };
        double x = 0, unit = Bounds.Width / (bars.Sum() * 2);
        for (int i = 0; i < bars.Length; i++) { if (i % 2 == 0) c.DrawRectangle(BlackDraw.B(Colors.Black), null, new Rect(x, 0, bars[i] * unit * 1.4, h)); x += bars[i] * unit * 2; }
    }
}

/// <summary>Lava Lamp (5.9.0): a lamp of glass between a metal cap and base; the lava rises with the reading and glows from
/// violet (cool) through pink and orange to red (hot); a few drops float above it.</summary>
public sealed class ArtLavaLamp : ArtDecoration
{
    private static readonly (double At, Color C)[] Stops =
        { (30, Color.FromRgb(138, 91, 255)), (48, Color.FromRgb(255, 79, 216)), (65, Color.FromRgb(255, 138, 60)), (85, Color.FromRgb(255, 59, 59)) };
    private double? level; private Color lava = Glow(null); private readonly int seed;
    public ArtLavaLamp(int seed) : base(Colors.White) { this.seed = seed; }
    public void Set(double? fraction, double? celsius) { level = fraction; lava = Glow(celsius); InvalidateVisual(); }
    public static Color Glow(double? celsius)
    {
        double t = Math.Clamp(celsius ?? 30, 30, 85);
        for (int i = 1; i < Stops.Length; i++)
            if (t <= Stops[i].At) return Palette.Mix(Stops[i - 1].C, Stops[i].C, (t - Stops[i - 1].At) / (Stops[i].At - Stops[i - 1].At));
        return Stops[^1].C;
    }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 20 || h < 60) return;
        double capH = h * .07, baseH = h * .17, gx = w * .12, gw = w - 2 * gx, gy = capH, gh = h - capH - baseH, r = gw / 2;
        var metal = BlackDraw.V(Color.Parse("#3A3F4A"), Color.Parse("#15171C"));
        // Cap and base: tapered metal.
        var cap = new StreamGeometry();
        using (var s = cap.Open()) { s.BeginFigure(new Point(w * .3, 0), true); s.LineTo(new Point(w * .7, 0)); s.LineTo(new Point(gx + gw * .92, capH + 2)); s.LineTo(new Point(gx + gw * .08, capH + 2)); s.EndFigure(true); }
        c.DrawGeometry(metal, null, cap);
        var foot = new StreamGeometry();
        using (var s = foot.Open()) { s.BeginFigure(new Point(gx + gw * .05, h - baseH - 2), true); s.LineTo(new Point(gx + gw * .95, h - baseH - 2)); s.LineTo(new Point(w - 1, h)); s.LineTo(new Point(1, h)); s.EndFigure(true); }
        c.DrawGeometry(metal, null, foot);
        // Glass with the lava inside.
        var glass = new Rect(gx, gy, gw, gh);
        c.DrawRectangle(BlackDraw.V(Color.Parse("#14121C"), Color.Parse("#0B0A10")), new Pen(BlackDraw.B(Color.Parse("#2E2A3A")), 1), glass, r * .7, r * .7);
        double f = Math.Clamp(level ?? 0, 0, 1), top = gy + gh * (1 - Math.Max(.08, f));
        var hot = BlackDraw.V(Palette.Mix(lava, Colors.White, .28), Palette.Mix(lava, Colors.Black, .2));
        using (c.PushClip(new RoundedRect(glass.Deflate(2), r * .7)))
        {
            c.DrawRectangle(new RadialGradientBrush { GradientStops = { new(BlackDraw.A(lava, .32), 0), new(BlackDraw.A(lava, 0), 1) } }, null, new Rect(gx - gw, top - gh * .3, gw * 3, gh));
            c.DrawRectangle(hot, null, new Rect(gx, top + 4, gw, gh));
            var rnd = new Random(seed);
            for (int k = 0; k < 3; k++)
            {
                double bx = gx + gw * (.2 + .6 * rnd.NextDouble()), br = gw * (.2 + .1 * rnd.NextDouble());
                c.DrawEllipse(hot, null, new Point(bx, top + 4), br, br * .9);
            }
            for (int k = 0; k < 3; k++)
            {
                double room = top - gy - 12; if (room < 16) break;
                double br = gw * (.12 + .1 * rnd.NextDouble()), by = gy + 6 + br + (room - br) * rnd.NextDouble(), bx = gx + gw * (.3 + .4 * rnd.NextDouble());
                c.DrawEllipse(hot, null, new Point(bx, by), br, br * 1.25);
            }
        }
        c.DrawRectangle(BlackDraw.B(Color.FromArgb(40, 255, 255, 255)), null, new Rect(gx + gw * .16, gy + gh * .06, Math.Max(2, gw * .1), gh * .6), gw * .05, gw * .05);
        c.DrawRectangle(BlackDraw.B(BlackDraw.A(lava, .5)), null, new Rect(w * .25, h - baseH * .45, w * .5, 2), 1, 1);
    }
}
