using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Lumen.App;

public sealed class MirrorChart : Control
{
	private double shift;

	public double[] Down { get; set; } = Array.Empty<double>();

	public double[] Up { get; set; } = Array.Empty<double>();

	public Palette? Palette { get; set; }

	public MirrorChart()
	{
		base.ClipToBounds = true;
	}

	public void SetData(double[] down, double[] up)
	{
		bool num = Down.Length != 0 && down.Length != 0;
		Down = down;
		Up = up;
		if (!num)
		{
			InvalidateVisual();
			return;
		}
		Motion.Run(this, Motion.ScrollDuration, delegate(double t)
		{
			shift = 1.0 - t;
			InvalidateVisual();
		});
	}

	public override void Render(DrawingContext context)
	{
		Rect bounds = base.Bounds;
		if (Palette != null && !(bounds.Width < 4.0))
		{
			double num = Math.Round(bounds.Height * 0.62);
			Half(context, Down, bounds.Width, num, -1, Palette.Cool, 0.0, shift);
			Half(context, Up, bounds.Width, bounds.Height - num, 1, Palette.Calm, num, shift);
			context.DrawLine(new Pen(new SolidColorBrush(Palette.Stroke)), new Point(0.0, num + 0.5), new Point(bounds.Width, num + 0.5));
		}
	}

	private static void Half(DrawingContext ctx, double[] v, double width, double height, int dir, Color color, double origin = 0.0, double shift = 0.0)
	{
		if (v.Length < 2)
		{
			return;
		}
		double max = Math.Max(Scale(524288.0), v.Select(Scale).Max());
		double step = width / 119.0;
		double offset = width - (double)(v.Length - 1) * step + shift * step;
		double baseY = ((dir < 0) ? height : origin);
		StreamGeometry streamGeometry = new StreamGeometry();
		StreamGeometry streamGeometry2 = new StreamGeometry();
		using (StreamGeometryContext streamGeometryContext = streamGeometry.Open())
		{
			streamGeometryContext.BeginFigure(new Point(offset, baseY), isFilled: true);
			for (int i = 0; i < v.Length; i++)
			{
				streamGeometryContext.LineTo(P(i));
			}
			streamGeometryContext.LineTo(new Point(width, baseY));
			streamGeometryContext.EndFigure(isClosed: true);
		}
		using (StreamGeometryContext streamGeometryContext2 = streamGeometry2.Open())
		{
			streamGeometryContext2.BeginFigure(P(0), isFilled: false);
			for (int j = 1; j < v.Length; j++)
			{
				streamGeometryContext2.LineTo(P(j));
			}
			streamGeometryContext2.EndFigure(isClosed: false);
		}
		LinearGradientBrush brush = new LinearGradientBrush
		{
			StartPoint = new RelativePoint(0.0, (dir >= 0) ? 1 : 0, RelativeUnit.Relative),
			EndPoint = new RelativePoint(0.0, (dir < 0) ? 1 : 0, RelativeUnit.Relative),
			GradientStops = 
			{
				new GradientStop(Lumen.App.Palette.Alpha(color, 0.45), 0.0),
				new GradientStop(Lumen.App.Palette.Alpha(color, 0.04), 1.0)
			}
		};
		ctx.DrawGeometry(brush, null, streamGeometry);
		ctx.DrawGeometry(null, new Pen(new SolidColorBrush(color), 1.4, null, PenLineCap.Flat, PenLineJoin.Round), streamGeometry2);
		Point P(int num)
		{
			return new Point(offset + (double)num * step, baseY + (double)dir * Scale(v[num]) / max * (height - 1.0));
		}
		static double Scale(double x)
		{
			if (!double.IsFinite(x) || !(x > 0.0))
			{
				return 0.0;
			}
			return Math.Sqrt(x);
		}
	}
}
