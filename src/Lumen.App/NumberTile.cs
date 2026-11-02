using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed class NumberTile : SummaryTile
{
	private readonly TextBlock value;

	private readonly TextBlock unit;

	private readonly Meter meter;

	public NumberTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s)
		: base(kind, index, p, m, s, half: true)
	{
		double num = (m.Subtitles ? 40 : 32);
		value = Ui.Text("", num, p.Ink, FontWeight.DemiBold, numbers: true);
		value.LetterSpacing = (0.0 - num) * 0.03;
		unit = Ui.Text("", 12.0, p.Soft, FontWeight.Medium);
		unit.VerticalAlignment = VerticalAlignment.Bottom;
		unit.Margin = new Thickness(3.0, 0.0, 0.0, num * 0.16);
		meter = new Meter
		{
			Height = 5.0,
			Palette = p
		};
		StackPanel stackPanel = new StackPanel
		{
			Spacing = 4.0,
			Tag = "elastic"
		};
		stackPanel.Children.Add(base.Head);
		stackPanel.Children.Add(Ui.Row(0.0, value, unit));
		stackPanel.Children.Add(meter);
		stackPanel.Children.Add(Caption);
		stackPanel.Children.Add(Extras);
		base.Child = stackPanel;
	}

	protected override void Show(Summary sum)
	{
		Motion.Text(value, sum.Value);
		value.Foreground = Palette.Brush(sum.Color);
		unit.Text = ((sum.Value == "—") ? "" : sum.Unit);
		meter.Set(sum.Fraction, Shade(sum));
	}
}
