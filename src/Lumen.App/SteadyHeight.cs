using Avalonia;
using Avalonia.Controls;

namespace Lumen.App;

/// <summary>
/// Holds a shrink-to-fit value (a Viewbox) at the height of its unshrunk content. A wider value
/// ("893 KB/s" after "1.1 MB/s") is drawn smaller but keeps its row height, so the widget and every
/// widget below it stay where they are instead of jumping by a few pixels with each update.
/// </summary>
public sealed class SteadyHeight : Decorator
{
	public static SteadyHeight Of(Viewbox box)
	{
		var steady = new SteadyHeight { HorizontalAlignment = box.HorizontalAlignment, Margin = box.Margin };
		box.Margin = default;
		steady.Child = box;
		return steady;
	}

	protected override Size MeasureOverride(Size available)
	{
		if (Child is not Viewbox box)
		{
			return base.MeasureOverride(available);
		}
		box.Measure(available);
		// The Viewbox measures its content without limits, so this is the natural height.
		double natural = box.Child?.DesiredSize.Height ?? 0.0;
		double height = Math.Max(box.DesiredSize.Height, natural);
		if (double.IsFinite(available.Height))
		{
			height = Math.Min(height, available.Height);
		}
		return new Size(box.DesiredSize.Width, height);
	}

	protected override Size ArrangeOverride(Size final)
	{
		if (Child is Control child)
		{
			double h = Math.Min(final.Height, child.DesiredSize.Height);
			child.Arrange(new Rect(0.0, (final.Height - h) / 2.0, final.Width, h));
		}
		return final;
	}
}
