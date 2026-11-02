using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Lumen.App;

public sealed class HistoryBars : Control
{
	private double[] values = Array.Empty<double>();

	private double max = 1.0;

	public Color Color { get; set; }

	public int Count { get; set; } = 24;

	public void Set(double[] series, double? seriesMax, Color color)
	{
		values = series.TakeLast(Count).ToArray();
		double num = values.DefaultIfEmpty(0.0).Max() * 1.1;
		max = Math.Max(1E-09, Math.Max(num, (seriesMax ?? num) * 0.25));
		Color = color;
		InvalidateVisual();
	}

	public override void Render(DrawingContext ctx)
	{
		if (values.Length == 0 || base.Bounds.Width < 10.0)
		{
			return;
		}
		double num = 3.0;
		double num2 = (base.Bounds.Width - num * (double)(Count - 1)) / (double)Count;
		double height = base.Bounds.Height;
		if (!(num2 < 1.5))
		{
			SolidColorBrush solidColorBrush = new SolidColorBrush(Palette.Alpha(Color, 0.42));
			SolidColorBrush solidColorBrush2 = new SolidColorBrush(Color);
			int num3 = Count - values.Length;
			for (int i = 0; i < values.Length; i++)
			{
				double num4 = Math.Clamp(values[i] / max, 0.0, 1.0);
				double num5 = Math.Max(num2, num4 * height);
				ctx.DrawRectangle(rrect: new RoundedRect(new Rect((double)(num3 + i) * (num2 + num), height - num5, num2, num5), Math.Min(num2 / 2.0, 3.0)), brush: (i == values.Length - 1) ? solidColorBrush2 : solidColorBrush, pen: null);
			}
		}
	}
}
