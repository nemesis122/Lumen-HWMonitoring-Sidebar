using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Lumen.App;

public sealed class LinearScale : Control
{
	private bool started;

	public double Fraction { get; set; }

	public double RedFrom { get; set; } = 0.85;

	public Palette? Palette { get; set; }

	public void Animate(double fraction)
	{
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
		if (Palette != null && !(base.Bounds.Width < 20.0))
		{
			double num = 6.0;
			double num2 = 12.0;
			double num3 = (base.Bounds.Height - num) / 2.0;
			double num4 = base.Bounds.Width - num2;
			double num5 = num2 / 2.0;
			Rect rect = new Rect(num5, num3, num4, num);
			ctx.DrawRectangle(Ramp(0.22), null, new RoundedRect(rect, num / 2.0));
			double num6 = Math.Clamp(double.IsFinite(Fraction) ? Fraction : 0.0, 0.0, 1.0);
			using (ctx.PushClip(new RoundedRect(new Rect(num5, num3, Math.Max(num, num4 * num6), num), num / 2.0)))
			{
				ctx.DrawRectangle(Ramp(1.0), null, new RoundedRect(rect, num / 2.0));
			}
			double x = num5 + num4 * Math.Clamp(RedFrom, 0.0, 1.0);
			ctx.DrawLine(new Pen(new SolidColorBrush(Palette.Ramp[^1]), 1.5), new Point(x, num3 - 4.0), new Point(x, num3 + num + 4.0));
			Point center = new Point(num5 + num4 * num6, base.Bounds.Height / 2.0);
			ctx.DrawEllipse(new SolidColorBrush(Palette.Ink), null, center, num2 / 2.0, num2 / 2.0);
			ctx.DrawEllipse(new SolidColorBrush(Palette.At(num6)), null, center, num2 / 2.0 - 3.0, num2 / 2.0 - 3.0);
		}
		LinearGradientBrush Ramp(double alpha)
		{
			LinearGradientBrush linearGradientBrush = new LinearGradientBrush
			{
				StartPoint = new RelativePoint(0.0, 0.0, RelativeUnit.Relative),
				EndPoint = new RelativePoint(1.0, 0.0, RelativeUnit.Relative)
			};
			(double, Color)[] array = new(double, Color)[4]
			{
				(0.0, Palette.Ramp[0]),
				(0.5, Palette.Ramp[1]),
				(0.75, Palette.Ramp[2]),
				(1.0, Palette.Ramp[3])
			};
			for (int i = 0; i < array.Length; i++)
			{
				var (offset, c) = array[i];
				linearGradientBrush.GradientStops.Add(new GradientStop(Lumen.App.Palette.Alpha(c, alpha), offset));
			}
			return linearGradientBrush;
		}
	}
}
