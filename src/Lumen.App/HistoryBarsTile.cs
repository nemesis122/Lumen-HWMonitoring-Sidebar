using Avalonia;
using Avalonia.Controls;
using Lumen.Core;

namespace Lumen.App;

public sealed class HistoryBarsTile : SummaryTile
{
	private readonly Hero hero;

	private readonly HistoryBars bars;

	private readonly Meter meter;

	public HistoryBarsTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s)
		: base(kind, index, p, m, s, half: false)
	{
		hero = new Hero(m.Subtitles ? 26 : 22, p);
		bars = new HistoryBars
		{
			Height = (m.Subtitles ? 46 : 38),
			Margin = new Thickness(0.0, 8.0, 0.0, 2.0)
		};
		meter = new Meter
		{
			Height = 6.0,
			Palette = p,
			Margin = new Thickness(0.0, 8.0, 0.0, 2.0)
		};
		StackPanel stackPanel = new StackPanel
		{
			Spacing = 0.0,
			Tag = "elastic"
		};
		stackPanel.Children.Add(base.Head);
		stackPanel.Children.Add(hero);
		stackPanel.Children.Add(bars);
		stackPanel.Children.Add(meter);
		stackPanel.Children.Add(Extras);
		base.Child = stackPanel;
	}

	protected override void Show(Summary sum)
	{
		hero.Set(sum.Value, sum.Unit, sum.Color);
		int num;
		if (sum.Series.Length > 1)
		{
			string text = Summaries.ChartKey(Kind);
			num = ((text == null || S.Shows(text)) ? 1 : 0);
		}
		else
		{
			num = 0;
		}
		bool flag = (byte)num != 0;
		bars.IsVisible = flag;
		meter.IsVisible = !flag;
		if (flag)
		{
			bars.Set(sum.Series, sum.SeriesMax, Shade(sum));
		}
		else
		{
			meter.Set(sum.Fraction, Shade(sum));
		}
	}
}
