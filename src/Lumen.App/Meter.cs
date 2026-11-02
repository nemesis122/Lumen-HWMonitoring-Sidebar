using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Lumen.App;

public sealed class Meter : Control
{
	private bool started;

	public double Value { get; set; }

	public Color Color { get; set; } = Colors.DeepSkyBlue;

	public Palette? Palette { get; set; }

	public int Segments { get; set; }

	public bool RampBySegment { get; set; }

	public bool Vertical { get; set; }

	public void Set(double value, Color color)
	{
		Color = color;
		double from = (started ? Value : value);
		started = true;
		if (!double.IsFinite(value))
		{
			value = 0.0;
		}
		if (!double.IsFinite(from))
		{
			from = value;
		}
		Motion.Run(this, Motion.Duration, delegate(double t)
		{
			Value = from + (value - from) * t;
			InvalidateVisual();
		});
	}

	public override void Render(DrawingContext context)
	{
		Rect bounds = base.Bounds;
		if (Palette == null || bounds.Width < 4.0)
		{
			return;
		}
		double num = (double.IsFinite(Value) ? Math.Clamp(Value, 0.0, 1.0) : 0.0);
		double num2 = bounds.Height / 2.0;
		SolidColorBrush brush = new SolidColorBrush(Palette.Track);
		if (Vertical)
		{
			double radius = Math.Min(bounds.Width / 2.0, 6.0);
			context.DrawRectangle(brush, null, new RoundedRect(new Rect(bounds.Size), radius));
			if (num > 0.0)
			{
				double num3 = Math.Max(bounds.Width * 0.6, num * bounds.Height);
				context.DrawRectangle(new SolidColorBrush(Color), null, new RoundedRect(new Rect(0.0, bounds.Height - num3, bounds.Width, num3), radius));
			}
			return;
		}
		if (Segments <= 0)
		{
			context.DrawRectangle(brush, null, new RoundedRect(new Rect(bounds.Size), num2));
			if (num > 0.0)
			{
				context.DrawRectangle(new SolidColorBrush(Color), null, new RoundedRect(new Rect(0.0, 0.0, Math.Max(bounds.Height, num * bounds.Width), bounds.Height), num2));
			}
			return;
		}
		double num4 = 2.0;
		double num5 = (bounds.Width - num4 * (double)(Segments - 1)) / (double)Segments;
		for (int i = 0; i < Segments; i++)
		{
			double num6 = (double)i / (double)Segments;
			double num7 = Math.Clamp((num - num6) * (double)Segments, 0.0, 1.0);
			Rect rect = new Rect((double)i * (num5 + num4), 0.0, num5, bounds.Height);
			context.DrawRectangle(brush, null, new RoundedRect(rect, Math.Min(num2, 2.0)));
			if (num7 > 0.0)
			{
				Color color = (RampBySegment ? Palette.At(((double)i + 0.5) / (double)Segments) : Color);
				context.DrawRectangle(new SolidColorBrush(color), null, new RoundedRect(rect.WithWidth(num5 * num7), Math.Min(num2, 2.0)));
			}
		}
	}
}
