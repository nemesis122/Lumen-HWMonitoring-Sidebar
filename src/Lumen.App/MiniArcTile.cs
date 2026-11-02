using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed class MiniArcTile : SummaryTile
{
	private readonly RingGauge arc;

	private readonly TextBlock value;

	private readonly TextBlock unit;

	private readonly TextBlock label;

	public MiniArcTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s)
		: base(kind, index, p, m, s, half: true)
	{
		double num = (m.Subtitles ? 70 : 60);
		arc = new RingGauge
		{
			Width = num,
			Height = num,
			Palette = p,
			Thickness = (m.Subtitles ? 6 : 5)
		};
		value = Ui.Text("", num * 0.28, p.Ink, FontWeight.Bold, numbers: true);
		unit = Ui.Text("", 10.0, p.Soft, FontWeight.DemiBold);
		unit.VerticalAlignment = VerticalAlignment.Bottom;
		unit.Margin = new Thickness(1.0, 0.0, 0.0, 2.0);
		Viewbox item = new Viewbox
		{
			Child = Ui.Row(0.0, value, unit),
			StretchDirection = StretchDirection.DownOnly,
			MaxWidth = num * 0.7,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		Panel item2 = new Panel
		{
			Width = num,
			Height = num,
			HorizontalAlignment = HorizontalAlignment.Center,
			Children = 
			{
				(Control)arc,
				(Control)item
			}
		};
		label = Ui.Text("", 11.5, p.Soft, FontWeight.Medium);
		label.HorizontalAlignment = HorizontalAlignment.Center;
		StackPanel stackPanel = new StackPanel
		{
			Spacing = 4.0,
			Tag = "elastic",
			HorizontalAlignment = HorizontalAlignment.Stretch
		};
		stackPanel.Children.Add(item2);
		stackPanel.Children.Add(label);
		base.Child = stackPanel;
		base.Padding = new Thickness(8.0, m.Pad - 4.0, 8.0, m.Pad - 6.0);
	}

	protected override void Show(Summary sum)
	{
		Motion.Text(value, sum.Value);
		unit.Text = ((sum.Value == "—") ? "" : sum.Unit);
		label.Text = Naming.Title(base.Slot, Index, sum.Title);
		arc.Animate(sum.Fraction, Shade(sum));
	}
}
