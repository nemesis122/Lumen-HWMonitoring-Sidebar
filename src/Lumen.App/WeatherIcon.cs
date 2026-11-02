using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Lumen.App;

public sealed class WeatherIcon : Control
{
	private string group = "clear";

	private bool day = true;

	public Palette? Palette { get; set; }

	public void Set(string weatherGroup, bool isDay)
	{
		group = weatherGroup;
		day = isDay;
		InvalidateVisual();
	}

	public override void Render(DrawingContext ctx)
	{
		if (Palette == null)
		{
			return;
		}
		Palette palette = Palette;
		double num = Math.Min(base.Bounds.Width, base.Bounds.Height) / 24.0;
		Color sun;
		SolidColorBrush cloudFill;
		Pen rain;
		using (ctx.PushTransform(Matrix.CreateScale(num, num)))
		{
			sun = Color.Parse(palette.Dark ? "#F5C451" : "#E0A100");
			cloudFill = new SolidColorBrush(palette.Dark ? Color.Parse("#C9D2DE") : Color.Parse("#AAB5C3"));
			rain = new Pen(new SolidColorBrush(palette.Dark ? Color.Parse("#5DB6FF") : Color.Parse("#1F7AE0")), 1.6, null, PenLineCap.Round);
			switch (group)
			{
			case "clear":
				Sun(12.0, 12.0, 4.6);
				break;
			case "mostly-clear":
				Sun(10.0, 10.0, 4.0);
				Cloud(9.0, 12.0, 13.0);
				break;
			case "partly-cloudy":
				Sun(9.0, 8.5, 3.6);
				Cloud(5.0, 9.0, 17.0);
				break;
			case "fog":
			{
				Cloud(4.0, 3.0, 16.0);
				Pen pen = new Pen(cloudFill, 1.6, null, PenLineCap.Round);
				ctx.DrawLine(pen, new Point(4.0, 17.0), new Point(20.0, 17.0));
				ctx.DrawLine(pen, new Point(6.0, 20.5), new Point(18.0, 20.5));
				break;
			}
			case "drizzle":
				Cloud(4.0, 3.0, 16.0);
				Drops(3, dots: true);
				break;
			case "rain":
				Cloud(4.0, 3.0, 16.0);
				Drops(3, dots: false);
				break;
			case "showers":
				Sun(8.0, 6.5, 3.2);
				Cloud(5.0, 5.0, 16.0);
				Drops(3, dots: false);
				break;
			case "freezing":
				Cloud(4.0, 3.0, 16.0);
				Drops(2, dots: false);
				ctx.DrawEllipse(Brushes.White, null, new Point(18.0, 20.0), 1.2, 1.2);
				break;
			case "snow":
			{
				Cloud(4.0, 3.0, 16.0);
				SolidColorBrush brush = new SolidColorBrush(palette.Dark ? Colors.White : Color.Parse("#7FA6D6"));
				(double, double)[] array = new(double, double)[3]
				{
					(8.0, 18.5),
					(12.0, 20.5),
					(16.0, 18.5)
				};
				for (int i = 0; i < array.Length; i++)
				{
					var (x, y) = array[i];
					ctx.DrawEllipse(brush, null, new Point(x, y), 1.4, 1.4);
				}
				break;
			}
			case "storm":
			{
				Cloud(4.0, 3.0, 16.0);
				StreamGeometry streamGeometry = new StreamGeometry();
				using (StreamGeometryContext streamGeometryContext = streamGeometry.Open())
				{
					streamGeometryContext.BeginFigure(new Point(13.0, 14.0), isFilled: true);
					streamGeometryContext.LineTo(new Point(9.5, 19.0));
					streamGeometryContext.LineTo(new Point(12.0, 19.0));
					streamGeometryContext.LineTo(new Point(10.5, 23.5));
					streamGeometryContext.LineTo(new Point(15.0, 17.5));
					streamGeometryContext.LineTo(new Point(12.5, 17.5));
					streamGeometryContext.LineTo(new Point(14.0, 14.0));
					streamGeometryContext.EndFigure(isClosed: true);
				}
				ctx.DrawGeometry(new SolidColorBrush(sun), null, streamGeometry);
				break;
			}
			default:
				Cloud(3.0, 6.0, 18.0);
				break;
			}
		}
		void Cloud(double num3, double num4, double w)
		{
			double num2 = w / 16.0;
			GeometryGroup geometryGroup = new GeometryGroup
			{
				FillRule = FillRule.NonZero
			};
			geometryGroup.Children.Add(new EllipseGeometry(new Rect(num3 + 1.0 * num2, num4 + 4.0 * num2, 7.0 * num2, 7.0 * num2)));
			geometryGroup.Children.Add(new EllipseGeometry(new Rect(num3 + 4.0 * num2, num4, 9.0 * num2, 9.0 * num2)));
			geometryGroup.Children.Add(new EllipseGeometry(new Rect(num3 + 9.0 * num2, num4 + 3.5 * num2, 7.0 * num2, 7.0 * num2)));
			geometryGroup.Children.Add(new RectangleGeometry(new Rect(num3 + 4.5 * num2, num4 + 5.0 * num2, 8.0 * num2, 6.0 * num2)));
			ctx.DrawGeometry(cloudFill, null, geometryGroup);
		}
		void Drops(int n, bool dots)
		{
			for (int j = 0; j < n; j++)
			{
				double num2 = 8 + j * 4;
				if (dots)
				{
					ctx.DrawEllipse(rain.Brush, null, new Point(num2, 19.5 + (double)(j % 2) * 1.5), 1.1, 1.1);
				}
				else
				{
					ctx.DrawLine(rain, new Point(num2 + 1.0, 17.0), new Point(num2 - 0.5, 21.0));
				}
			}
		}
		void Sun(double cx, double cy, double r)
		{
			if (!day)
			{
				StreamGeometry streamGeometry2 = new StreamGeometry();
				using (StreamGeometryContext streamGeometryContext2 = streamGeometry2.Open())
				{
					streamGeometryContext2.BeginFigure(new Point(cx + r * 0.2, cy - r), isFilled: true);
					streamGeometryContext2.ArcTo(new Point(cx + r * 0.2, cy + r), new Size(r, r), 0.0, isLargeArc: true, SweepDirection.CounterClockwise);
					streamGeometryContext2.ArcTo(new Point(cx + r * 0.2, cy - r), new Size(r * 0.75, r * 0.75), 0.0, isLargeArc: false, SweepDirection.Clockwise);
					streamGeometryContext2.EndFigure(isClosed: true);
				}
				ctx.DrawGeometry(new SolidColorBrush(Lumen.App.Palette.Mix(sun, Colors.White, 0.45)), null, streamGeometry2);
			}
			else
			{
				Pen pen2 = new Pen(new SolidColorBrush(sun), 1.5, null, PenLineCap.Round);
				for (int j = 0; j < 8; j++)
				{
					double num2 = (double)j * Math.PI / 4.0;
					double num3 = Math.Cos(num2);
					double num4 = Math.Sin(num2);
					ctx.DrawLine(pen2, new Point(cx + num3 * r * 1.45, cy + num4 * r * 1.45), new Point(cx + num3 * r * 1.9, cy + num4 * r * 1.9));
				}
				ctx.DrawEllipse(new SolidColorBrush(sun), null, new Point(cx, cy), r, r);
			}
		}
	}
}
