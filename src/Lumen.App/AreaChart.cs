using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Lumen.App;

public sealed class AreaChart : Control
{
	private double shift;

	private double? shownMax;

	public double[] Values { get; set; } = Array.Empty<double>();

	public double? Max { get; set; }

	public Color Color { get; set; } = Colors.DeepSkyBlue;

	public double FillOpacity { get; set; } = 0.3;

	public double LineWidth { get; set; } = 1.6;

	public AreaChart()
	{
		base.ClipToBounds = true;
	}

	public void Set(double[] values, Color color)
	{
		bool flag = Values.Length != 0 && values.Length != 0 && values != Values;
		Values = values;
		Color = color;
		double target = Max ?? Math.Max(1E-09, values.Where(double.IsFinite).DefaultIfEmpty(1.0).Max() * 1.15);
		double from = shownMax.GetValueOrDefault(target);
		double fromShift = (flag ? 1.0 : shift);
		Motion.Run(this, flag ? Motion.ScrollDuration : Motion.Duration, delegate(double t)
		{
			shownMax = from + (target - from) * t;
			shift = fromShift * (1.0 - t);
			InvalidateVisual();
		});
	}

	public override void Render(DrawingContext context)
	{
		Rect b = base.Bounds;
		if (b.Width < 4.0 || b.Height < 4.0)
		{
			return;
		}
		double[] v = Values;
		double max = shownMax ?? Max ?? Math.Max(1E-09, v.Where(double.IsFinite).DefaultIfEmpty(1.0).Max() * 1.15);
		int num = 119;
		double step = b.Width / (double)num;
		double offset = b.Width - (double)(v.Length - 1) * step + shift * step;
		LinearGradientBrush fill = new LinearGradientBrush
		{
			StartPoint = new RelativePoint(0.0, 0.0, RelativeUnit.Relative),
			EndPoint = new RelativePoint(0.0, 1.0, RelativeUnit.Relative),
			GradientStops = 
			{
				new GradientStop(Palette.Alpha(Color, FillOpacity), 0.0),
				new GradientStop(Palette.Alpha(Color, 0.0), 1.0)
			}
		};
		Pen pen = new Pen(new SolidColorBrush(Color), LineWidth, null, PenLineCap.Round, PenLineJoin.Round);
		int num2 = -1;
		for (int i = 0; i <= v.Length; i++)
		{
			int num3;
			if (i < v.Length)
			{
				num3 = (double.IsFinite(v[i]) ? 1 : 0);
				if (num3 != 0 && num2 < 0)
				{
					num2 = i;
				}
			}
			else
			{
				num3 = 0;
			}
			if (num3 == 0 && num2 >= 0)
			{
				DrawRun(context, num2, i - 1, P, b.Height, fill, pen);
				num2 = -1;
			}
		}
		Point P(int num4)
		{
			return new Point(offset + (double)num4 * step, b.Height - Math.Clamp(v[num4] / max, 0.02, 1.0) * (b.Height - LineWidth) - LineWidth / 2.0);
		}
	}

	private static void DrawRun(DrawingContext ctx, int from, int to, Func<int, Point> p, double h, IBrush fill, Pen pen)
	{
		if (to <= from)
		{
			return;
		}
		StreamGeometry streamGeometry = new StreamGeometry();
		using (StreamGeometryContext streamGeometryContext = streamGeometry.Open())
		{
			streamGeometryContext.BeginFigure(new Point(p(from).X, h), isFilled: true);
			for (int i = from; i <= to; i++)
			{
				streamGeometryContext.LineTo(p(i));
			}
			streamGeometryContext.LineTo(new Point(p(to).X, h));
			streamGeometryContext.EndFigure(isClosed: true);
		}
		ctx.DrawGeometry(fill, null, streamGeometry);
		StreamGeometry streamGeometry2 = new StreamGeometry();
		using (StreamGeometryContext streamGeometryContext2 = streamGeometry2.Open())
		{
			streamGeometryContext2.BeginFigure(p(from), isFilled: false);
			for (int j = from + 1; j <= to; j++)
			{
				streamGeometryContext2.LineTo(p(j));
			}
			streamGeometryContext2.EndFigure(isClosed: false);
		}
		ctx.DrawGeometry(null, pen, streamGeometry2);
	}
}
