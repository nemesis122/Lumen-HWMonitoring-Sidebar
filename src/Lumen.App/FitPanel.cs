using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Lumen.App;

public sealed class FitPanel : Panel
{
	// A new scale must beat the one in use by more than this share of the height before the panel
	// switches. Without it two nearly equal scales (a tile that wraps at one width but not at the
	// other) take turns forever and Avalonia reports a layout cycle.
	private const double ScaleHysteresis = 0.012;

	// Extra height that may go into the gaps between widgets, as a multiple of the normal gap. Anything
	// beyond stays below the last widget instead of opening large holes between them.
	private const double MaxGapGrowth = 1.5;

	// Spacing and chart heights only change by more than this many pixels, so rounding never re-triggers layout.
	private const double Tolerance = 1.0;

	private readonly List<StackPanel> lists;

	private readonly double scale;

	private readonly bool fill;

	private double gap;

	private double applied = double.NaN;

	private readonly Dictionary<StackPanel, ScaleTransform> transforms = new Dictionary<StackPanel, ScaleTransform>();

	private readonly Dictionary<Control, double> baseHeight = new Dictionary<Control, double>();

	private readonly Dictionary<StackPanel, double> baseSpacing = new Dictionary<StackPanel, double>();

	private readonly Dictionary<StatPanel, double> baseRows = new Dictionary<StatPanel, double>();

	// Bottom padding of each widget before the panel lengthened it (designs without elastic rows).
	private readonly Dictionary<Border, double> basePad = new Dictionary<Border, double>();

	public event Action<double>? Fitted;

	/// <summary>The height the panel was last asked to fill (0 when it does not fill).</summary>
	public double LastHeight { get; private set; }

	public FitPanel(List<StackPanel> lists, double scale, bool fill, double gap)
	{
		this.lists = lists;
		this.scale = scale;
		this.fill = fill;
		this.gap = gap;
		base.ClipToBounds = fill;
		foreach (StackPanel list in lists)
		{
			ScaleTransform transform = new ScaleTransform(1.0, 1.0);
			transforms[list] = transform;
			list.RenderTransformOrigin = new RelativePoint(0.0, 0.0, RelativeUnit.Absolute);
			list.RenderTransform = transform;
			base.Children.Add(list);
		}
	}

	public void SetGap(double value)
	{
		gap = value;
		InvalidateMeasure();
	}

	/// <summary>
	/// Forgets the remembered natural sizes. Called whenever the widgets are rebuilt, so the panel
	/// neither keeps discarded cards alive nor compares new cards with old values.
	/// </summary>
	public void Reset()
	{
		baseHeight.Clear();
		baseSpacing.Clear();
		baseRows.Clear();
		basePad.Clear();
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

	private static bool Shown(Control c)
	{
		return c.IsVisible && c.GetLogicalAncestors().OfType<Control>().All((Control a) => a.IsVisible);
	}

	// Cards removed from the tree (page switch, GPU added, compact rebuild) must not stay referenced.
	private void Forget()
	{
		// A panel that is not shown yet (or measured on its own) has nothing to compare against.
		if (!((ILogical)this).IsAttachedToLogicalTree)
		{
			return;
		}
		Prune(baseHeight);
		Prune(baseSpacing);
		Prune(baseRows);
		Prune(basePad);
	}

	private static void Prune<T>(Dictionary<T, double> map) where T : Control
	{
		if (map.Count == 0)
		{
			return;
		}
		List<T>? gone = null;
		foreach (T key in map.Keys)
		{
			if (!((ILogical)key).IsAttachedToLogicalTree)
			{
				(gone ??= new List<T>()).Add(key);
			}
		}
		if (gone == null)
		{
			return;
		}
		foreach (T key in gone)
		{
			map.Remove(key);
		}
	}

	private sealed class Parts
	{
		public readonly List<Control> Charts = new List<Control>();

		public readonly List<StackPanel> Stacks = new List<StackPanel>();

		public readonly List<StatPanel> Rows = new List<StatPanel>();

		public readonly List<Border> Cards = new List<Border>();
	}

	// One walk over a column instead of one walk per kind of stretchable element.
	private Parts Collect(Control root)
	{
		Parts parts = new Parts();
		IEnumerable<ILogical> all = root.GetLogicalDescendants();
		if ((root is StackPanel self && object.Equals(self.Tag, "elastic")) || IsCard(root))
		{
			all = all.Prepend(root);
		}
		foreach (ILogical item in all)
		{
			if (item is Border card && IsCard(card))
			{
				if (!basePad.ContainsKey(card))
				{
					basePad[card] = card.Padding.Bottom;
				}
				parts.Cards.Add(card);
			}
			switch (item)
			{
			case AreaChart or MirrorChart:
			case Control when object.Equals(((Control)item).Tag, "grow"):
			{
				Control c = (Control)item;
				if (c.Tag as string != "fixed" && Shown(c))
				{
					if (!baseHeight.ContainsKey(c) && double.IsFinite(c.Height))
					{
						baseHeight[c] = c.Height;
					}
					if (baseHeight.ContainsKey(c))
					{
						parts.Charts.Add(c);
					}
				}
				break;
			}
			case StatPanel p when object.Equals(p.Tag, "elastic") && Shown(p):
				if (!baseRows.ContainsKey(p))
				{
					baseRows[p] = p.RowSpacing;
				}
				parts.Rows.Add(p);
				break;
			case StackPanel s when object.Equals(s.Tag, "elastic") && s.IsVisible && s.Children.Count((Control x) => x.IsVisible) > 1:
				if (!baseSpacing.ContainsKey(s))
				{
					baseSpacing[s] = s.Spacing;
				}
				parts.Stacks.Add(s);
				break;
			}
		}
		return parts;
	}

	// A widget is a visible Border placed directly in one of the columns.
	private bool IsCard(ILogical item)
	{
		return item is Border b && b.IsVisible && b.Parent is StackPanel column && lists.Contains(column);
	}

	private static int InnerGaps(StackPanel s)
	{
		return s.Children.Count((Control x) => x.IsVisible) - 1;
	}

	private double Grown(Parts parts)
	{
		return parts.Charts.Sum((Control c) => c.Height - baseHeight[c])
			+ parts.Stacks.Sum((StackPanel s) => (s.Spacing - baseSpacing[s]) * (double)InnerGaps(s))
			+ parts.Rows.Sum((StatPanel p) => (p.RowSpacing - baseRows[p]) * (double)p.RowGaps)
			+ parts.Cards.Sum((Border b) => b.Padding.Bottom - basePad[b]);
	}

	public double NaturalHeight(Control card)
	{
		return card.DesiredSize.Height - Grown(Collect(card));
	}

	private double Natural(StackPanel l, Parts parts)
	{
		return l.DesiredSize.Height - (double)ActualGaps(l) * (l.Spacing - gap) - Grown(parts);
	}

	// A widget may change a size of its own between passes (sizes are restored on every update). The
	// control is then invalid but its column still reports the old height, so the plan would start
	// from a stale size and the result would flip between two layouts on every update.
	private void Refresh()
	{
		foreach (StackPanel list in lists)
		{
			foreach (Visual v in list.GetVisualDescendants())
			{
				if (v is Layoutable item && !item.IsMeasureValid)
				{
					for (Layoutable? up = item.GetVisualParent<Layoutable>(); up != null; up = up.GetVisualParent<Layoutable>())
					{
						if (!up.IsMeasureValid && up != list)
						{
							break;
						}
						up.InvalidateMeasure();
						if (up == list)
						{
							break;
						}
					}
				}
			}
		}
	}

	private double MeasureAll(double colWidth, double s, out List<Parts> parts)
	{
		parts = new List<Parts>(lists.Count);
		double max = 0.0;
		foreach (StackPanel list in lists)
		{
			list.Measure(new Size(colWidth / s, double.PositiveInfinity));
			Parts p = Collect(list);
			parts.Add(p);
			max = Math.Max(max, Natural(list, p));
		}
		return max;
	}

	private static bool Fits(double natural, double s, double height)
	{
		return natural * s <= height + 0.5;
	}

	// Finds the scale for the available height. Starts from the scale in use and keeps it while the
	// content fits and the gain would be negligible, so ordinary sensor updates cost one measure.
	private double Search(double colWidth, double height, double start, ref double natural, ref List<Parts> parts)
	{
		double cap = scale * 1.08;
		double s = start;
		if (Fits(natural, s, height) && (s >= cap - 0.001 || height - natural * s <= height * ScaleHysteresis))
		{
			return s;
		}
		for (int i = 0; i < 4 && !Fits(natural, s, height); i++)
		{
			s = height / Math.Max(1.0, natural);
			natural = MeasureAll(colWidth, s, out parts);
		}
		double lastFit = Fits(natural, s, height) ? s : double.NaN;
		for (int j = 0; j < 4 && natural * s < height - 0.5 && s < cap - 0.001; j++)
		{
			double next = Math.Min(cap, height / Math.Max(1.0, natural));
			if (next <= s * (1.0 + ScaleHysteresis * 0.25))
			{
				break;
			}
			s = next;
			natural = MeasureAll(colWidth, s, out parts);
			if (Fits(natural, s, height))
			{
				lastFit = s;
			}
		}
		if (!Fits(natural, s, height))
		{
			// Growing made a tile wrap and overflow: go back to the last scale that fitted.
			s = double.IsFinite(lastFit) ? lastFit : height / Math.Max(1.0, natural);
			natural = MeasureAll(colWidth, s, out parts);
			if (!Fits(natural, s, height))
			{
				s = height / Math.Max(1.0, natural);
				natural = MeasureAll(colWidth, s, out parts);
			}
		}
		if (!double.IsFinite(s) || s < 0.05)
		{
			s = 0.05;
			natural = MeasureAll(colWidth, s, out parts);
		}
		return s;
	}

	protected override Size MeasureOverride(Size available)
	{
		Diagnostics.RecordMeasure(available.Width, available.Height, double.IsFinite(applied) ? applied : scale, lists.Count);
		Forget();
		double width = (double.IsFinite(available.Width) ? available.Width : 300.0);
		double height = available.Height;
		bool flag = fill && double.IsFinite(height) && height > 0.0;
		LastHeight = flag ? height : 0.0;
		double s = (flag && double.IsFinite(applied) && applied > 0.01) ? applied : scale;
		double colWidth = ColumnWidth(width);
		Refresh();
		double natural = MeasureAll(colWidth, s, out List<Parts> parts);
		if (flag)
		{
			s = Search(colWidth, height, s, ref natural, ref parts);
		}
		// Several columns that do not fill the height still end on one line: the shorter columns get the
		// difference to the tallest one, spread the same way as when filling the height.
		bool even = !flag && Active().Count > 1;
		double tallest = 0.0;
		if (even)
		{
			for (int k = 0; k < lists.Count; k++)
			{
				if (lists[k].Children.Any((Control c) => c.IsVisible))
				{
					tallest = Math.Max(tallest, Natural(lists[k], parts[k]));
				}
			}
		}
		for (int k = 0; k < lists.Count; k++)
		{
			StackPanel column = lists[k];
			Parts p = parts[k];
			bool stretch = flag || (even && column.Children.Any((Control c) => c.IsVisible));
			double goal = flag ? height / s : tallest;
			// A column that already ends on the line keeps its layout. Planning again from sizes that
			// differ only by rounding would move the widgets by a few pixels on every update.
			if (stretch && column.DesiredSize.Height - goal <= 1.5 && column.DesiredSize.Height - goal >= -2.5)
			{
				continue;
			}
			double spacing = gap;
			double chartGrowth = 0.0;
			double elastic = 0.0;
			double cardGrowth = 0.0;
			int elasticGaps = p.Stacks.Sum(InnerGaps) + p.Rows.Sum((StatPanel r) => r.RowGaps);
			if (stretch)
			{
				double extra = Math.Max(0.0, goal - Natural(column, p));
				double betweenCards = Math.Min(extra, (double)ActualGaps(column) * gap * 0.6);
				extra -= betweenCards;
				if (p.Charts.Count > 0)
				{
					chartGrowth = Math.Min(extra, p.Charts.Sum((Control c) => baseHeight[c] * 2.5));
					extra -= chartGrowth;
				}
				if (elasticGaps > 0)
				{
					elastic = extra / (double)elasticGaps;
					extra = 0.0;
				}
				// Whatever is left (designs without elastic rows) goes between the widgets only up to a
				// limit; the rest stays below the last widget instead of tearing the column apart.
				double perGap = (betweenCards + extra) / (double)Gaps(column);
				spacing = gap + Math.Min(perGap, gap * MaxGapGrowth);
				// Beyond that limit the widgets themselves get longer, so the column still ends exactly at
				// the bottom (and on one line with its neighbours) instead of leaving a hole below.
				double beyond = betweenCards + extra - (spacing - gap) * (double)ActualGaps(column);
				if (beyond > 0.5 && p.Cards.Count > 0)
				{
					cardGrowth = beyond / (double)p.Cards.Count;
				}
			}
			// Growing rows and charts is planned from measured sizes, but a card can come out a little
			// taller than planned (a row that reflows at the new spacing). Then the growth is taken back
			// by the overshoot and the column measured again, so the last card never ends below the panel.
			for (int attempt = 0; attempt < 8; attempt++)
			{
				List<Layoutable> changed = new List<Layoutable>();
				double tol = attempt == 0 ? Tolerance : 0.01;
				foreach (StackPanel item in p.Stacks)
				{
					Set(changed, item, StackPanel.SpacingProperty, item.Spacing, baseSpacing[item] + elastic, tol);
				}
				foreach (StatPanel item in p.Rows)
				{
					Set(changed, item, StatPanel.RowSpacingProperty, item.RowSpacing, baseRows[item] + elastic, tol);
				}
				Set(changed, column, StackPanel.SpacingProperty, column.Spacing, spacing, tol);
				foreach (Border card in p.Cards)
				{
					Thickness pad = card.Padding;
					double bottom = basePad[card] + cardGrowth;
					if (Math.Abs(pad.Bottom - bottom) > tol)
					{
						card.Padding = new Thickness(pad.Left, pad.Top, pad.Right, bottom);
						changed.Add(card);
					}
				}
				double total = p.Charts.Sum((Control c) => baseHeight[c]);
				foreach (Control item in p.Charts)
				{
					double target = baseHeight[item] + ((total > 0.0) ? (chartGrowth * baseHeight[item] / total) : 0.0);
					Set(changed, item, Layoutable.HeightProperty, item.Height, target, tol);
				}
				if (changed.Count > 0)
				{
					Remeasure(column, changed, colWidth / s);
				}
				double over = stretch ? column.DesiredSize.Height - goal : 0.0;
				if (over < -2.5 && p.Cards.Count > 0 && attempt < 7)
				{
					// Still short (rows that did not take their share, charts at their limit): the
					// widgets take the rest, so the column ends on the bottom line.
					cardGrowth += -over / (double)p.Cards.Count;
					continue;
				}
				if (over <= 1.5)
				{
					break;
				}
				if (cardGrowth > 0.0)
				{
					cardGrowth = Math.Max(0.0, cardGrowth - over / (double)Math.Max(1, p.Cards.Count));
				}
				else if (elasticGaps > 0 && elastic > 0.0)
				{
					elastic = Math.Max(0.0, elastic - over / (double)elasticGaps);
				}
				else if (chartGrowth > 0.0)
				{
					chartGrowth = Math.Max(0.0, chartGrowth - over);
				}
				else if (spacing > gap)
				{
					spacing = Math.Max(gap, spacing - over / Math.Max(1.0, (double)Gaps(column)));
				}
				else
				{
					break;
				}
			}
		}
		Diagnostics.RecordMeasure(available.Width, available.Height, s, lists.Count);
		if (!double.IsFinite(applied) || Math.Abs(s - applied) > 0.001)
		{
			applied = s;
			this.Fitted?.Invoke(s);
		}
		if (!flag)
		{
			return new Size(width, lists.Select((StackPanel l) => l.DesiredSize.Height).DefaultIfEmpty(0.0).Max() * s);
		}
		return new Size(width, height);
	}

	private static void Set(List<Layoutable> changed, Layoutable target, AvaloniaProperty<double> property, double current, double value, double tolerance = Tolerance)
	{
		if (!double.IsFinite(value) || Math.Abs(current - value) <= tolerance)
		{
			return;
		}
		target.SetValue(property, value);
		changed.Add(target);
	}

	// Avalonia's InvalidateMeasure does not travel upwards and Measure returns at once while a control
	// is still valid. After a spacing deep inside a card changed, measuring only the column would
	// therefore do nothing; the card would be measured later on its own, report a new size and
	// invalidate this panel again - a new pass every time, the layout never comes to rest.
	// Invalidating the chain from each changed element up to the column lets the whole column be
	// measured right here, inside this pass.
	private static void Remeasure(StackPanel column, List<Layoutable> changed, double width)
	{
		foreach (Layoutable item in changed)
		{
			for (Layoutable? v = item; v != null; v = v.GetVisualParent<Layoutable>())
			{
				v.InvalidateMeasure();
				if (v == column)
				{
					break;
				}
			}
		}
		column.InvalidateMeasure();
		column.Measure(new Size(width, double.PositiveInfinity));
	}

	protected override Size ArrangeOverride(Size final)
	{
		double num = ColumnWidth(final.Width);
		List<StackPanel> list = Active();
		double s = ((double.IsFinite(applied) && applied > 0.01) ? applied : 1.0);
		foreach (ScaleTransform transform in transforms.Values)
		{
			if (transform.ScaleX != s || transform.ScaleY != s)
			{
				transform.ScaleX = s;
				transform.ScaleY = s;
			}
		}
		foreach (StackPanel item in lists.Except(list))
		{
			item.Arrange(new Rect(0.0, 0.0, 0.0, 0.0));
		}
		for (int i = 0; i < list.Count; i++)
		{
			StackPanel stackPanel = list[i];
			double height = (double.IsFinite(stackPanel.DesiredSize.Height) ? Math.Max(0.0, stackPanel.DesiredSize.Height) : 0.0);
			stackPanel.Arrange(new Rect((double)i * (num + gap), 0.0, num / s, height));
		}
		return final;
	}
}


