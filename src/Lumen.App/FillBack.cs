using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Lumen.App;

public sealed class FillBack : Control
{
	private bool started;

	public double Fraction { get; set; }

	public Color Fill { get; set; }

	public Color Track { get; set; }

	public double Radius { get; set; } = 12.0;

	public void Animate(double fraction, Color fill)
	{
		Fill = fill;
		if (!double.IsFinite(fraction))
		{
			fraction = 0.0;
		}
		double from = (started ? Fraction : fraction);
		started = true;
		Motion.Run(this, Motion.Duration, delegate(double t)
		{
			Fraction = from + (fraction - from) * t;
			InvalidateVisual();
		});
	}

	public override void Render(DrawingContext ctx)
	{
		Rect rect = new Rect(base.Bounds.Size);
		if (rect.Width < 2.0)
		{
			return;
		}
		double radius = Math.Min(Radius, rect.Height / 2.0);
		ctx.DrawRectangle(new SolidColorBrush(Track), null, new RoundedRect(rect, radius));
		double num = Math.Clamp(double.IsFinite(Fraction) ? Fraction : 0.0, 0.0, 1.0) * rect.Width;
		if (num < 1.0)
		{
			return;
		}
		using (ctx.PushClip(new RoundedRect(rect, radius)))
		{
			ctx.DrawRectangle(new SolidColorBrush(Fill), null, new Rect(0.0, 0.0, num, rect.Height));
		}
	}
}
