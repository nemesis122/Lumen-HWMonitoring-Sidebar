using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Lumen.App;

public sealed class Rings : Control
{
	private double[] shown = Array.Empty<double>();

	private Color[] colors = Array.Empty<Color>();

	public Palette? Palette { get; set; }

	public double Thickness { get; set; } = 9.0;

	public void Animate(double[] fractions, Color[] ringColors)
	{
		colors = ringColors;
		double[] target = fractions.Select((double f) => (!double.IsFinite(f)) ? 0.0 : Math.Clamp(f, 0.0, 1.0)).ToArray();
		double[] from = ((shown.Length == target.Length) ? shown.ToArray() : target);
		Motion.Run(this, Motion.Duration, delegate(double t)
		{
			shown = target.Select((double v, int i) => from[i] + (v - from[i]) * t).ToArray();
			InvalidateVisual();
		});
	}

	public override void Render(DrawingContext ctx)
	{
		if (Palette == null)
		{
			return;
		}
		double num = Math.Min(base.Bounds.Width, base.Bounds.Height);
		Point c = new Point(base.Bounds.Width / 2.0, base.Bounds.Height / 2.0);
		for (int i = 0; i < shown.Length; i++)
		{
			double r = (num - Thickness) / 2.0 - (double)i * (Thickness + 3.0);
			if (r < Thickness)
			{
				break;
			}
			Color color = colors.ElementAtOrDefault(i);
			ctx.DrawEllipse(null, new Pen(new SolidColorBrush(Lumen.App.Palette.Mix(Palette.Track, color, 0.18)), Thickness), c, r, r);
			double num2 = shown[i];
			if (num2 < 0.005)
			{
				continue;
			}
			if (num2 > 0.999)
			{
				ctx.DrawEllipse(null, new Pen(new SolidColorBrush(color), Thickness), c, r, r);
				continue;
			}
			StreamGeometry streamGeometry = new StreamGeometry();
			using (StreamGeometryContext streamGeometryContext = streamGeometry.Open())
			{
				streamGeometryContext.BeginFigure(At(-90.0), isFilled: false);
				streamGeometryContext.ArcTo(At(-90.0 + 360.0 * num2), new Size(r, r), 0.0, num2 > 0.5, SweepDirection.Clockwise);
				streamGeometryContext.EndFigure(isClosed: false);
			}
			ctx.DrawGeometry(null, new Pen(new SolidColorBrush(color), Thickness, null, PenLineCap.Round), streamGeometry);
			Point At(double deg)
			{
				return new Point(c.X + r * Math.Cos(deg * Math.PI / 180.0), c.Y + r * Math.Sin(deg * Math.PI / 180.0));
			}
		}
	}
}
