using System;
using Avalonia;
using Avalonia.Controls;

namespace Lumen.App;

/// <summary>
/// A row of equal-width cells whose height is set by the first cell: the other cells are measured with that
/// height, so a ring drawn to fill its cell can never make the row taller than the first ring (the one the
/// panel grows), however wide the cell is.
/// </summary>
public sealed class LeadRow : Panel
{
	protected override Size MeasureOverride(Size availableSize)
	{
		if (Children.Count == 0)
		{
			return default;
		}
		int columns = Math.Max(1, Columns);
		double width = double.IsFinite(availableSize.Width) ? availableSize.Width / columns : double.PositiveInfinity;
		var lead = Children[0];
		lead.Measure(new Size(width, availableSize.Height));
		double height = lead.DesiredSize.Height;
		for (int i = 1; i < Children.Count; i++)
		{
			Children[i].Measure(new Size(width, height));
		}
		double total = double.IsFinite(width) ? width * columns : lead.DesiredSize.Width * columns;
		return new Size(total, height);
	}

	protected override Size ArrangeOverride(Size finalSize)
	{
		int columns = Math.Max(1, Columns);
		double width = finalSize.Width / columns;
		for (int i = 0; i < Children.Count; i++)
		{
			Children[i].Arrange(new Rect(i * width, 0, width, finalSize.Height));
		}
		return finalSize;
	}

	public int Columns { get; set; } = 2;
}
