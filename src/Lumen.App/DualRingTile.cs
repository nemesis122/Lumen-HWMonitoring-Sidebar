using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed class DualRingTile : SummaryTile
{
	private readonly DualRing rings;

	private readonly Hero hero;

	private readonly TextBlock legend;

	public DualRingTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s)
		: base(kind, index, p, m, s, half: true)
	{
		double num = (m.Subtitles ? 64 : 54);
		rings = new DualRing
		{
			Width = num,
			Height = num,
			Palette = p,
			Thickness = (m.Subtitles ? 6 : 5),
			VerticalAlignment = VerticalAlignment.Center
		};
		hero = new Hero(m.Subtitles ? 26 : 22, p);
		legend = Ui.Text("", 10.5, p.Soft, FontWeight.Normal, numbers: true);
		legend.TextTrimming = TextTrimming.CharacterEllipsis;
		Caption.TextTrimming = TextTrimming.CharacterEllipsis;
		StackPanel stackPanel = Ui.Stack(1.0, hero, legend);
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		stackPanel.Margin = new Thickness(10.0, 0.0, 0.0, 0.0);
		Grid grid = Ui.Columns("Auto,*", rings, stackPanel);
		Grid.SetColumn(stackPanel, 1);
		grid.Margin = new Thickness(0.0, 4.0, 0.0, 0.0);
		StackPanel stackPanel2 = new StackPanel
		{
			Spacing = 4.0,
			Tag = "elastic"
		};
		stackPanel2.Children.Add(base.Head);
		stackPanel2.Children.Add(grid);
		stackPanel2.Children.Add(Caption);
		stackPanel2.Children.Add(Extras);
		base.Child = stackPanel2;
	}

	protected override void Show(Summary sum)
	{
		hero.Set(sum.Value, sum.Unit, sum.Color);
		rings.Animate(sum.Second, sum.Fraction, P.Cool, Shade(sum));
		legend.IsVisible = sum.Second.HasValue;
		legend.Text = (sum.Second.HasValue ? (L.T("Load") + " " + sum.SecondLabel) : "");
		Caption.IsVisible = !sum.Second.HasValue;
	}
}
