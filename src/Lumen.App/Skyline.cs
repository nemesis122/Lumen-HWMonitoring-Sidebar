using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Lumen.App;

public sealed class Skyline : Control
{
	private double[] shown = Array.Empty<double>();

	private IReadOnlyList<double> loads = Array.Empty<double>();

	public IReadOnlyList<double> Loads
	{
		get
		{
			return loads;
		}
		set
		{
			loads = value;
			double[] from = ((shown.Length == value.Count) ? shown.ToArray() : value.ToArray());
			double[] to = value.ToArray();
			shown = from.ToArray();
			Motion.Run(this, Motion.Duration, delegate(double t)
			{
				for (int i = 0; i < to.Length; i++)
				{
					shown[i] = from[i] + (to[i] - from[i]) * t;
				}
				InvalidateVisual();
			});
		}
	}

	public Palette? Palette { get; set; }

	public override void Render(DrawingContext context)
	{
		Rect bounds = base.Bounds;
		if (Palette != null && shown.Length != 0 && !(bounds.Width < 4.0))
		{
			int num = shown.Length;
			double num2 = ((num > 24) ? 1.5 : 2.5);
			double num3 = (bounds.Width - num2 * (double)(num - 1)) / (double)num;
			double radius = Math.Min(2.0, num3 / 2.0);
			SolidColorBrush brush = new SolidColorBrush(Palette.Track);
			for (int i = 0; i < num; i++)
			{
				double x = (double)i * (num3 + num2);
				double num4 = Math.Clamp(shown[i], 0.0, 100.0) / 100.0;
				context.DrawRectangle(brush, null, new RoundedRect(new Rect(x, 0.0, num3, bounds.Height), radius));
				double num5 = Math.Max(2.0, num4 * bounds.Height);
				context.DrawRectangle(new SolidColorBrush(Palette.At(num4)), null, new RoundedRect(new Rect(x, bounds.Height - num5, num3, num5), radius));
			}
		}
	}
}
