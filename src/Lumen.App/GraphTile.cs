using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Lumen.Core;

namespace Lumen.App;

public sealed class GraphTile : SummaryTile
{
	private readonly Hero hero;

	private readonly AreaChart chart;

	private readonly Meter meter;

	public GraphTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s)
		: base(kind, index, p, m, s, half: false)
	{
		hero = new Hero(m.Hero * 0.8, p);
		chart = new AreaChart
		{
			Height = m.Chart * 1.5,
			LineWidth = 1.8,
			FillOpacity = 0.28
		};
		meter = new Meter
		{
			Height = 6.0,
			Palette = p,
			VerticalAlignment = VerticalAlignment.Bottom,
			IsVisible = false
		};
		Panel item = new Panel
		{
			Children = 
			{
				(Control)chart,
				(Control)meter
			},
			Margin = new Thickness(0.0, (0.0 - m.Hero) * 0.2, 0.0, 0.0)
		};
		StackPanel stackPanel = new StackPanel
		{
			Spacing = 6.0,
			Tag = "elastic"
		};
		stackPanel.Children.Add(base.Head);
		stackPanel.Children.Add(hero);
		stackPanel.Children.Add(item);
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
		chart.Opacity = (flag ? 1 : 0);
		meter.IsVisible = !flag;
		if (flag)
		{
			chart.Max = sum.SeriesMax;
			chart.Set(sum.Series, Shade(sum));
		}
		else
		{
			meter.Set(sum.Fraction, Shade(sum));
		}
	}
}
