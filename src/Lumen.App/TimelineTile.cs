using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed class TimelineTile : SummaryTile
{
	private readonly Hero hero;

	private readonly TextBlock stats;

	private readonly AreaChart chart;

	private readonly Meter meter;

	public TimelineTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s)
		: base(kind, index, p, m, s, half: false)
	{
		hero = new Hero(m.Subtitles ? 28 : 24, p);
		stats = Ui.Text("", 11.5, p.Soft, FontWeight.Normal, numbers: true);
		stats.HorizontalAlignment = HorizontalAlignment.Right;
		stats.VerticalAlignment = VerticalAlignment.Bottom;
		stats.Margin = new Thickness(10.0, 0.0, 0.0, 4.0);
		chart = new AreaChart
		{
			Height = m.Chart,
			LineWidth = 1.6,
			FillOpacity = 0.2,
			Margin = new Thickness(0.0, 4.0, 0.0, 0.0)
		};
		meter = new Meter
		{
			Height = 6.0,
			Palette = p,
			Margin = new Thickness(0.0, 6.0, 0.0, 0.0)
		};
		Grid item = Ui.Columns("Auto,*", hero, stats);
		Grid.SetColumn(stats, 1);
		StackPanel stackPanel = new StackPanel
		{
			Spacing = 2.0,
			Tag = "elastic"
		};
		stackPanel.Children.Add(base.Head);
		stackPanel.Children.Add(item);
		stackPanel.Children.Add(chart);
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
		chart.IsVisible = flag;
		meter.IsVisible = !flag;
		if (flag)
		{
			chart.Max = sum.SeriesMax;
			chart.Set(sum.Series, Shade(sum));
			stats.Text = $"{L.T("Max")} {Readings.SeriesText(Kind, sum.Series.Max())} · Ø {Readings.SeriesText(Kind, sum.Series.Average())}";
		}
		else
		{
			meter.Set(sum.Fraction, Shade(sum));
			stats.Text = "";
		}
	}
}
