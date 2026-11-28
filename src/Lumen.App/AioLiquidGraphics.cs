using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;

namespace Lumen.App;

/// <summary>
/// Liquid Glass (5.9.0, draft A-02): a glass tube filled up to the reading. The surface waves and bubbles rise through the
/// liquid; a new reading lets the level glide there instead of jumping. The tube carries no number: the value above it does.
/// All tubes share one clock that runs only while a tube is on screen, and they stand still when motion is switched off.
/// </summary>
public sealed class ArtLiquidTube : ArtDecoration
{
    public const int FramesPerSecond = 30;
    private static readonly List<ArtLiquidTube> shown = new();
    private static DispatcherTimer? clock;
    private static DateTime last = DateTime.MinValue;

    private sealed class Bubble { public double X, Y, Speed, Radius; }
    private readonly Bubble[] bubbles;
    private readonly Random random;
    private double level, target;
    private bool first = true;
    private double phase;
    private readonly double speed;

    public ArtLiquidTube(Color colour, int seed) : base(colour)
    {
        random = new Random(seed * 31 + 7); phase = random.NextDouble() * Math.PI * 2; speed = 2.2 + random.NextDouble() * 1.4;
        bubbles = new Bubble[6];
        for (int i = 0; i < bubbles.Length; i++) bubbles[i] = new Bubble { X = .2 + .6 * random.NextDouble(), Y = random.NextDouble(), Speed = .5 + random.NextDouble() * .7, Radius = 1.4 + random.NextDouble() * 1.8 };
        AttachedToVisualTree += (_, _) => Join(this);
        DetachedFromVisualTree += (_, _) => Leave(this);
    }

    /// <summary>The level the liquid shows now (0..1), for tests.</summary>
    public double Level => level;
    /// <summary>The level it is gliding to (0..1), for tests.</summary>
    public double Target => target;

    /// <summary>A new reading: the level glides to it (the first one is taken at once, so a design opens filled).</summary>
    public void Set(double? fraction)
    {
        target = Math.Clamp(fraction is double f && double.IsFinite(f) ? f : 0, 0, 1);
        if (first || !Motion.Enabled) { level = target; first = false; }
        InvalidateVisual();
    }

    private static void Join(ArtLiquidTube tube)
    {
        if (!shown.Contains(tube)) shown.Add(tube);
        clock ??= new DispatcherTimer(TimeSpan.FromMilliseconds(1000.0 / FramesPerSecond), DispatcherPriority.Render, (_, _) => Tick());
        if (!clock.IsEnabled) { last = DateTime.MinValue; clock.Start(); }
    }
    private static void Leave(ArtLiquidTube tube)
    {
        shown.Remove(tube);
        if (shown.Count == 0) clock?.Stop();
    }
    private static void Tick()
    {
        var now = DateTime.UtcNow; double dt = last == DateTime.MinValue ? 0 : Math.Min(.25, (now - last).TotalSeconds); last = now;
        if (!Motion.Enabled) return;
        foreach (var tube in shown.ToArray()) if (tube.IsEffectivelyVisible) tube.Advance(dt);
    }

    /// <summary>Moves the liquid on by <paramref name="dt"/> seconds (the clock calls it; tests call it directly).</summary>
    public void Advance(double dt)
    {
        if (!(dt > 0)) return;
        level += (target - level) * Math.Min(1, dt * 2.5);
        phase += dt * speed;
        foreach (var b in bubbles)
        {
            b.Y += dt * b.Speed * .22;
            if (b.Y > 1) { b.Y -= 1; b.X = .2 + .6 * random.NextDouble(); }
        }
        InvalidateVisual();
    }

    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 12 || h < 40) return;
        var glass = new Rect(1, 1, w - 2, h - 2); double r = glass.Width / 2;
        c.DrawRectangle(BlackDraw.V(Color.Parse("#0A0D12"), Color.Parse("#05070A")), new Pen(BlackDraw.B(Color.Parse("#2A3038")), 1.6), glass, r, r);
        var inside = glass.Deflate(3); double ir = inside.Width / 2;
        double f = Math.Max(.06, level), top = inside.Bottom - inside.Height * f, amp = Math.Min(3.2, inside.Width * .08);
        using (c.PushClip(new RoundedRect(inside, ir)))
        {
            // The liquid with a waving surface.
            var body = new StreamGeometry();
            using (var s = body.Open())
            {
                s.BeginFigure(new Point(inside.X, top), true);
                const int steps = 16;
                for (int i = 1; i <= steps; i++)
                {
                    double x = inside.X + inside.Width * i / steps;
                    s.LineTo(new Point(x, top + Math.Sin(phase + i * .55) * amp));
                }
                s.LineTo(new Point(inside.Right, inside.Bottom + 2)); s.LineTo(new Point(inside.X, inside.Bottom + 2)); s.EndFigure(true);
            }
            c.DrawGeometry(BlackDraw.V(Palette.Mix(Color, Colors.White, .12), BlackDraw.A(Color, .45)), null, body);
            // A glow over the surface.
            c.DrawRectangle(BlackDraw.V(BlackDraw.A(Color, 0), BlackDraw.A(Color, .22)), null, new Rect(inside.X, top - inside.Width * 1.2, inside.Width, inside.Width * 1.2));
            // Bubbles rising from the bottom to the surface.
            double depth = inside.Bottom - top;
            if (depth > 10)
                foreach (var b in bubbles)
                {
                    double y = inside.Bottom - b.Y * depth, br = b.Radius * Math.Clamp(inside.Width / 40, .6, 1.6);
                    if (y < top + br + 2) continue;
                    c.DrawEllipse(BlackDraw.B(Color.FromArgb(120, 255, 255, 255)), null, new Point(inside.X + b.X * inside.Width, y), br, br);
                }
        }
        // The shine of the glass.
        c.DrawRectangle(BlackDraw.V(Color.FromArgb(60, 255, 255, 255), Color.FromArgb(10, 255, 255, 255)), null,
            new Rect(inside.X + inside.Width * .14, inside.Y + 8, Math.Max(2, inside.Width * .11), inside.Height - 16), 2, 2);
    }
}
