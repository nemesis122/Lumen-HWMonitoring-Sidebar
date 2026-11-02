using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Lumen.App;

public sealed class RingGauge : Control
{
	private bool started;

	public double Fraction { get; set; }

	public Color Color { get; set; }

	public Palette? Palette { get; set; }

	public double Thickness { get; set; } = 7.0;

	public void Animate(double fraction, Color color)
	{
		Color = color;
		double from = (started ? Fraction : fraction);
		started = true;
		if (!double.IsFinite(fraction))
		{
			fraction = 0.0;
		}
		Motion.Run(this, Motion.Duration, delegate(double t)
		{
			Fraction = from + (fraction - from) * t;
			InvalidateVisual();
		});
	}

	public override void Render(DrawingContext ctx)
	{
		if (Palette != null)
		{
			double r = (Math.Min(base.Bounds.Width, base.Bounds.Height) - Thickness) / 2.0;
			Point c = new Point(base.Bounds.Width / 2.0, base.Bounds.Height / 2.0);
			Arc(ctx, c, r, 135.0, 270.0, new Pen(new SolidColorBrush(Palette.Track), Thickness, null, PenLineCap.Round));
			double num = (double.IsFinite(Fraction) ? Math.Clamp(Fraction, 0.0, 1.0) : 0.0);
			if (num > 0.005)
			{
				Arc(ctx, c, r, 135.0, 270.0 * num, new Pen(new SolidColorBrush(Color), Thickness, null, PenLineCap.Round));
			}
		}
	}

	private static void Arc(DrawingContext ctx, Point c, double r, double start, double sweep, Pen pen)
	{
		StreamGeometry streamGeometry = new StreamGeometry();
		using (StreamGeometryContext streamGeometryContext = streamGeometry.Open())
		{
			streamGeometryContext.BeginFigure(At(start), isFilled: false);
			streamGeometryContext.ArcTo(At(start + sweep), new Size(r, r), 0.0, sweep > 180.0, SweepDirection.Clockwise);
			streamGeometryContext.EndFigure(isClosed: false);
		}
		ctx.DrawGeometry(null, pen, streamGeometry);
		Point At(double deg)
		{
			return new Point(c.X + r * Math.Cos(deg * Math.PI / 180.0), c.Y + r * Math.Sin(deg * Math.PI / 180.0));
		}
	}
}
