using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media;

namespace Lumen.App;

public sealed class FitPanel : Panel
{
	private readonly List<StackPanel> lists;

	private readonly double scale;

	private readonly bool fill;

	private double gap;

	private double applied = 1.0;

	private readonly Dictionary<Control, double> baseHeight = new Dictionary<Control, double>();

	private readonly Dictionary<StackPanel, double> baseSpacing = new Dictionary<StackPanel, double>();

	public event Action<double>? Fitted;

	public FitPanel(List<StackPanel> lists, double scale, bool fill, double gap)
	{
		this.lists = lists;
		this.scale = scale;
		this.fill = fill;
		this.gap = gap;
		base.ClipToBounds = fill;
		foreach (StackPanel list in lists)
		{
			base.Children.Add(list);
		}
	}

	public void SetGap(double value)
	{
		gap = value;
		InvalidateMeasure();
	}

	private List<StackPanel> Active()
	{
		List<StackPanel> list = lists.Where((StackPanel l) => l.Children.Any((Control c) => c.IsVisible)).ToList();
		if (list.Count <= 0)
		{
			return lists.Take(1).ToList();
		}
		return list;
	}

	private double ColumnWidth(double w)
	{
		int num = Math.Max(1, Active().Count);
		double num2 = (w - gap * (double)(num - 1)) / (double)num;
		if (!double.IsFinite(num2))
		{
			return 1.0;
		}
		return Math.Max(1.0, num2);
	}

	private static int Gaps(StackPanel l)
	{
		return Math.Max(1, l.Children.Count((Control c) => c.IsVisible) - 1);
	}

	// Natural height must count real gaps; Gaps() keeps a minimum of one
	// solely to make distribution denominators safe.
	private static int ActualGaps(StackPanel l)
	{
		return Math.Max(0, l.Children.Count((Control c) => c.IsVisible) - 1);
	}

	private List<Control> Growable(StackPanel l)
	{
		List<Control> list = l.GetLogicalDescendants().OfType<Control>().Where(delegate(Control c)
		{
			bool flag = ((c is AreaChart || c is MirrorChart) ? true : false);
			return flag && c.Tag as string != "fixed" && c.IsVisible && c.GetLogicalAncestors().OfType<Control>().All((Control a) => a.IsVisible);
		})
			.ToList();
		foreach (Control item in list)
		{
			if (!baseHeight.ContainsKey(item) && double.IsFinite(item.Height))
			{
				baseHeight[item] = item.Height;
			}
		}
		return list.Where(baseHeight.ContainsKey).ToList();
	}

	private List<StackPanel> Airy(StackPanel l)
	{
		List<StackPanel> list = (from c in l.GetLogicalDescendants().OfType<StackPanel>()
			where object.Equals(c.Tag, "elastic") && c.IsVisible && c.Children.Count((Control x) => x.IsVisible) > 1
			select c).ToList();
		foreach (StackPanel item in list)
		{
			if (!baseSpacing.ContainsKey(item))
			{
				baseSpacing[item] = item.Spacing;
			}
		}
		return list;
	}

	private static int InnerGaps(StackPanel s)
	{
		return s.Children.Count((Control x) => x.IsVisible) - 1;
	}

	public double NaturalHeight(Control card)
	{
		double num = card.DesiredSize.Height;
		foreach (Control item in from x in card.GetLogicalDescendants().OfType<Control>()
			where ((x is AreaChart || x is MirrorChart) ? true : false) && x.IsVisible
			select x)
		{
			if (baseHeight.TryGetValue(item, out var value))
			{
				num -= item.Height - value;
			}
		}
		foreach (StackPanel item2 in (from x in card.GetLogicalDescendants().OfType<StackPanel>()
			where object.Equals(x.Tag, "elastic")
			select x).Concat((!(card is StackPanel stackPanel) || !object.Equals(stackPanel.Tag, "elastic")) ? ((IEnumerable<StackPanel>)Array.Empty<StackPanel>()) : ((IEnumerable<StackPanel>)new StackPanel[1] { stackPanel })))
		{
			if (baseSpacing.TryGetValue(item2, out var value2) && item2.IsVisible)
			{
				num -= (item2.Spacing - value2) * (double)Math.Max(0, InnerGaps(item2));
			}
		}
		return num;
	}

	private double Grown(StackPanel l)
	{
		return Growable(l).Sum((Control c) => c.Height - baseHeight[c]) + Airy(l).Sum((StackPanel s) => (s.Spacing - baseSpacing[s]) * (double)InnerGaps(s));
	}

	private double Natural(StackPanel l)
	{
		return l.DesiredSize.Height - (double)ActualGaps(l) * (l.Spacing - gap) - Grown(l);
	}

	private double MeasureAll(double colWidth, double s)
	{
		foreach (StackPanel list in lists)
		{
			list.Measure(new Size(colWidth / s, double.PositiveInfinity));
		}
		if (lists.Count != 0)
		{
			return ((IEnumerable<StackPanel>)lists).Max((Func<StackPanel, double>)Natural);
		}
		return 0.0;
	}

	protected override Size MeasureOverride(Size available)
	{
		Diagnostics.RecordMeasure(available.Width, available.Height, applied, lists.Count);
		double num = (double.IsFinite(available.Width) ? available.Width : 300.0);
		double height = available.Height;
		double num2 = scale;
		double num3 = ColumnWidth(num);
		double num4 = MeasureAll(num3, num2);
		bool flag = fill && double.IsFinite(height) && height > 0.0;
		if (flag)
		{
			for (int i = 0; i < 4; i++)
			{
				if (!(num4 * num2 > height + 0.5))
				{
					break;
				}
				num2 = height / num4;
				num4 = MeasureAll(num3, num2);
			}
			double num5 = scale * 1.08;
			for (int j = 0; j < 4; j++)
			{
				if (!(num4 * num2 < height - 0.5))
				{
					break;
				}
				if (!(num2 < num5 - 0.001))
				{
					break;
				}
				num2 = Math.Min(num5, height / num4);
				num4 = MeasureAll(num3, num2);
			}
			if (num4 * num2 > height)
			{
				num2 = height / Math.Max(1.0, num4);
			}
			if (!double.IsFinite(num2) || num2 < 0.05)
			{
				num2 = 0.05;
			}
		}
		foreach (StackPanel list3 in lists)
		{
			List<Control> list = Growable(list3);
			double num6 = gap;
			double num7 = 0.0;
			bool flag2 = false;
			if (flag)
			{
				double num8 = Math.Max(0.0, height / num2 - Natural(list3));
				double num9 = Math.Min(num8, (double)Gaps(list3) * gap * 0.6);
				num8 -= num9;
				if (list.Count > 0)
				{
					double val = list.Sum((Control c) => baseHeight[c] * 2.5);
					num7 = Math.Min(num8, val);
					num8 -= num7;
				}
				List<StackPanel> list2 = Airy(list3);
				int num10 = ((IEnumerable<StackPanel>)list2).Sum((Func<StackPanel, int>)InnerGaps);
				double num11 = ((num10 > 0) ? (num8 / (double)num10) : 0.0);
				num8 -= num11 * (double)num10;
				foreach (StackPanel item in list2)
				{
					double num12 = baseSpacing[item] + num11;
					if (Math.Abs(item.Spacing - num12) > 0.5)
					{
						item.Spacing = num12;
						flag2 = true;
					}
				}
				num6 = ((list3.Children.Count((Control c) => c.IsVisible) > 1 && list3.GetLogicalDescendants().OfType<IWidget>().Any() && list3.GetLogicalDescendants().OfType<IWidget>().All((IWidget x) => (x is StatusRow || x is CompactRow) ? true : false)) ? gap : (gap + (num9 + num8) / (double)Gaps(list3)));
			}
			if (!flag)
			{
				foreach (StackPanel item2 in Airy(list3))
				{
					if (item2.Spacing != baseSpacing[item2])
					{
						item2.Spacing = baseSpacing[item2];
						flag2 = true;
					}
				}
			}
			bool flag3 = Math.Abs(list3.Spacing - num6) > 0.5 || flag2;
			if (flag3)
			{
				list3.Spacing = num6;
			}
			double num13 = list.Sum((Control c) => baseHeight[c]);
			foreach (Control item3 in list)
			{
				double num14 = baseHeight[item3] + ((num13 > 0.0) ? (num7 * baseHeight[item3] / num13) : 0.0);
				if (Math.Abs(item3.Height - num14) > 0.5)
				{
					item3.Height = num14;
					flag3 = true;
				}
			}
			if (flag3)
			{
				list3.Measure(new Size(num3 / num2, double.PositiveInfinity));
			}
		}
		Diagnostics.RecordMeasure(available.Width, available.Height, num2, lists.Count);
		if (Math.Abs(num2 - applied) > 0.001)
		{
			applied = num2;
			this.Fitted?.Invoke(num2);
		}
		if (!flag)
		{
			return new Size(num, lists.Select((StackPanel l) => l.DesiredSize.Height).DefaultIfEmpty(0.0).Max() * num2);
		}
		return new Size(num, height);
	}

	protected override Size ArrangeOverride(Size final)
	{
		double num = ColumnWidth(final.Width);
		List<StackPanel> list = Active();
		foreach (StackPanel item in lists.Except(list))
		{
			item.Arrange(new Rect(0.0, 0.0, 0.0, 0.0));
		}
		for (int i = 0; i < list.Count; i++)
		{
			StackPanel stackPanel = list[i];
			stackPanel.RenderTransformOrigin = new RelativePoint(0.0, 0.0, RelativeUnit.Absolute);
			stackPanel.RenderTransform = new ScaleTransform(applied, applied);
			double num2 = ((double.IsFinite(applied) && applied > 0.01) ? applied : 1.0);
			double height = (double.IsFinite(stackPanel.DesiredSize.Height) ? Math.Max(0.0, stackPanel.DesiredSize.Height) : 0.0);
			stackPanel.Arrange(new Rect((double)i * (num + gap), 0.0, num / num2, height));
		}
		return final;
	}
}


