using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Lumen.App;

public sealed class DualRing : Control
{
	private bool started;

	public double Outer { get; set; }

	public double Inner { get; set; }

	public bool HasOuter { get; set; }

	public Color OuterColor { get; set; }

	public Color InnerColor { get; set; }

	public Palette? Palette { get; set; }

	public double Thickness { get; set; } = 6.0;

	public void Animate(double? outer, double inner, Color outerColor, Color innerColor)
	{
		OuterColor = outerColor;
		InnerColor = innerColor;
		HasOuter = outer.HasValue;
		double num;
		if (outer.HasValue)
		{
			double valueOrDefault = outer.GetValueOrDefault();
			if (double.IsFinite(valueOrDefault))
			{
				num = valueOrDefault;
				goto IL_004f;
			}
		}
		num = 0.0;
		goto IL_004f;
		IL_004f:
		double o = num;
		double i = (double.IsFinite(inner) ? inner : 0.0);
		double fo = (started ? Outer : o);
		double fi = (started ? Inner : i);
		started = true;
		Motion.Run(this, Motion.Duration, delegate(double t)
		{
			Outer = fo + (o - fo) * t;
			Inner = fi + (i - fi) * t;
			InvalidateVisual();
		});
	}

	public override void Render(DrawingContext ctx)
	{
		Point c;
		SolidColorBrush track;
		if (Palette != null)
		{
			double num = Math.Min(base.Bounds.Width, base.Bounds.Height);
			c = new Point(base.Bounds.Width / 2.0, base.Bounds.Height / 2.0);
			track = new SolidColorBrush(Palette.Track);
			double num2 = (num - Thickness) / 2.0;
			double r = num2 - Thickness - 3.0;
			if (!HasOuter)
			{
				r = num2;
			}
			if (HasOuter)
			{
				Ring(num2, Outer, OuterColor, Thickness);
			}
			Ring(r, Inner, InnerColor, HasOuter ? Thickness : (Thickness + 2.0));
		}
		void Ring(double num3, double f, Color color, double thick)
		{
			if (!(num3 <= 2.0))
			{
				ctx.DrawEllipse(null, new Pen(track, thick), c, num3, num3);
				f = Math.Clamp(f, 0.0, 1.0);
				if (!(f < 0.005))
				{
					if (f > 0.999)
					{
						ctx.DrawEllipse(null, new Pen(new SolidColorBrush(color), thick), c, num3, num3);
					}
					else
					{
						StreamGeometry streamGeometry = new StreamGeometry();
						using (StreamGeometryContext streamGeometryContext = streamGeometry.Open())
						{
							streamGeometryContext.BeginFigure(At(-90.0), isFilled: false);
							streamGeometryContext.ArcTo(At(-90.0 + 360.0 * f), new Size(num3, num3), 0.0, f > 0.5, SweepDirection.Clockwise);
							streamGeometryContext.EndFigure(isClosed: false);
						}
						ctx.DrawGeometry(null, new Pen(new SolidColorBrush(color), thick, null, PenLineCap.Round), streamGeometry);
					}
				}
			}
			Point At(double deg)
			{
				return new Point(c.X + num3 * Math.Cos(deg * Math.PI / 180.0), c.Y + num3 * Math.Sin(deg * Math.PI / 180.0));
			}
		}
	}
}
