using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;

namespace Lumen.App;

public sealed class StatPanel : Panel
{
	private readonly double[] weights;

	private const double MinColumn = 76.0;

	public int MaxColumns { get; init; } = int.MaxValue;

	public StatPanel(double[] weights, Control[] children)
	{
		this.weights = weights;
		base.Children.AddRange(children);
	}

	private int Columns(double width)
	{
		int val = Math.Max(1, base.Children.Count((Control c) => c.IsVisible));
		if (!double.IsFinite(width))
		{
			return Math.Min(val, MaxColumns);
		}
		int num = Math.Min(val, MaxColumns);
		while (num > 2 && width / (double)num < 76.0)
		{
			num--;
		}
		return num;
	}

	private double[] Widths(double width, int cols, int n)
	{
		double[] array = new double[n];
		if (cols == n)
		{
			double num = Enumerable.Range(0, n).Sum((int i) => (i >= weights.Length) ? 1.0 : weights[i]);
			for (int num2 = 0; num2 < n; num2++)
			{
				array[num2] = width * ((num2 < weights.Length) ? weights[num2] : 1.0) / num;
			}
		}
		else
		{
			for (int num3 = 0; num3 < n; num3++)
			{
				array[num3] = width / (double)cols;
			}
		}
		return array;
	}

	protected override Size MeasureOverride(Size available)
	{
		List<Control> list = base.Children.Where((Control c) => c.IsVisible).ToList();
		if (list.Count == 0)
		{
			return default(Size);
		}
		double num = (double.IsFinite(available.Width) ? available.Width : ((double)(list.Count * 90)));
		int num2 = Columns(num);
		double[] array = Widths(num, num2, list.Count);
		double num3 = 0.0;
		double num4 = 0.0;
		for (int num5 = 0; num5 < list.Count; num5++)
		{
			list[num5].Measure(new Size(array[num5], double.PositiveInfinity));
			num4 = Math.Max(num4, list[num5].DesiredSize.Height);
			if ((num5 + 1) % num2 == 0 || num5 == list.Count - 1)
			{
				num3 += num4;
				num4 = 0.0;
			}
		}
		return new Size(double.IsFinite(available.Width) ? available.Width : num, num3);
	}

	protected override Size ArrangeOverride(Size final)
	{
		List<Control> list = base.Children.Where((Control c) => c.IsVisible).ToList();
		if (list.Count == 0)
		{
			return final;
		}
		int num = Columns(final.Width);
		double[] array = Widths(final.Width, num, list.Count);
		double num2 = 0.0;
		double num3 = 0.0;
		double num4 = 0.0;
		for (int num5 = 0; num5 < list.Count; num5++)
		{
			num4 = Math.Max(num4, list[num5].DesiredSize.Height);
			list[num5].Arrange(new Rect(num3, num2, array[num5], list[num5].DesiredSize.Height));
			num3 += array[num5];
			if ((num5 + 1) % num == 0)
			{
				num2 += num4;
				num4 = 0.0;
				num3 = 0.0;
			}
		}
		return final;
	}
}
