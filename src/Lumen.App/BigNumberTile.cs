using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed class BigNumberTile : SummaryTile
{
	private readonly TextBlock label;

	private readonly TextBlock value;

	private readonly TextBlock unit;

	public BigNumberTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s)
		: base(kind, index, p, m, s, half: true)
	{
		label = Ui.Text("", 11.0, p.Soft, FontWeight.DemiBold);
		label.LetterSpacing = 0.8;
		double num = m.Hero * 1.75;
		value = Ui.Text("", num, p.Ink, FontWeight.Bold, numbers: true);
		value.LetterSpacing = (0.0 - num) * 0.03;
		unit = Ui.Text("", Math.Round(num * 0.32), p.Soft, FontWeight.Medium);
		unit.VerticalAlignment = VerticalAlignment.Bottom;
		unit.Margin = new Thickness(2.0, 0.0, 0.0, num * 0.14);
		Viewbox item = new Viewbox
		{
			Child = Ui.Row(0.0, value, unit),
			StretchDirection = StretchDirection.DownOnly,
			HorizontalAlignment = HorizontalAlignment.Left
		};
		StackPanel stackPanel = new StackPanel
		{
			Spacing = 2.0,
			Tag = "elastic"
		};
		stackPanel.Children.Add(label);
		stackPanel.Children.Add(item);
		stackPanel.Children.Add(Caption);
		base.Child = stackPanel;
	}

	protected override void Show(Summary sum)
	{
		label.Text = Naming.Title(base.Slot, Index, sum.Title).ToUpper(L.Culture);
		Motion.Text(value, sum.Value);
		value.Foreground = Palette.Brush(sum.Color);
		unit.Text = ((sum.Value == "—") ? "" : sum.Unit);
	}
}
