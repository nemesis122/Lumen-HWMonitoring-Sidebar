using Avalonia;
using Avalonia.Controls;
using Lumen.Core;

namespace Lumen.App;

public sealed class BarTile : SummaryTile
{
	private readonly Hero hero;

	private readonly Meter meter;

	public BarTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s)
		: base(kind, index, p, m, s, half: false)
	{
		hero = new Hero(m.Hero * 0.85, p);
		meter = new Meter
		{
			Height = (m.Subtitles ? 10 : 8),
			Palette = p,
			Margin = new Thickness(0.0, 2.0, 0.0, 0.0)
		};
		StackPanel stackPanel = new StackPanel
		{
			Spacing = 6.0,
			Tag = "elastic"
		};
		stackPanel.Children.Add(base.Head);
		stackPanel.Children.Add(hero);
		stackPanel.Children.Add(meter);
		stackPanel.Children.Add(Extras);
		base.Child = stackPanel;
	}

	protected override void Show(Summary sum)
	{
		hero.Set(sum.Value, sum.Unit, sum.Color);
		meter.Set(sum.Fraction, Shade(sum));
	}
}
