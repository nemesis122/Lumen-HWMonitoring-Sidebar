using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed class BentoTile : SummaryTile
{
	private readonly TextBlock label;

	private readonly TextBlock value;

	private readonly TextBlock unit;

	private readonly TextBlock second;

	private readonly AreaChart chart;

	private readonly Meter meter;

	public BentoTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s)
		: base(kind, index, p, m, s, half: true)
	{
		base.ClipToBounds = true;
		label = Ui.Text("", 11.5, p.Soft, FontWeight.Medium);
		second = Ui.Text("", 11.5, p.Soft, FontWeight.DemiBold, numbers: true);
		second.HorizontalAlignment = HorizontalAlignment.Right;
		double num = (m.Subtitles ? 40 : 34);
		value = Ui.Text("", num, p.Ink, FontWeight.ExtraBold, numbers: true);
		value.LetterSpacing = (0.0 - num) * 0.05;
		unit = Ui.Text("", 14.0, p.Soft, FontWeight.DemiBold);
		unit.VerticalAlignment = VerticalAlignment.Bottom;
		unit.Margin = new Thickness(2.0, 0.0, 0.0, num * 0.14);
		Viewbox item = new Viewbox
		{
			Child = Ui.Row(0.0, value, unit),
			StretchDirection = StretchDirection.DownOnly,
			HorizontalAlignment = HorizontalAlignment.Left,
			Margin = new Thickness(0.0, 2.0, 0.0, 0.0)
		};
		chart = new AreaChart
		{
			Height = (m.Subtitles ? 40 : 32),
			LineWidth = 2.0,
			FillOpacity = 0.22,
			Margin = new Thickness(-12.0, 6.0, -12.0, 0.0 - (m.Pad - 4.0))
		};
		meter = new Meter
		{
			Height = 5.0,
			Palette = p,
			Margin = new Thickness(0.0, 8.0, 0.0, 2.0)
		};
		Grid item2 = Ui.Columns("*,Auto", label, second);
		Grid.SetColumn(second, 1);
		StackPanel stackPanel = new StackPanel
		{
			Spacing = 0.0,
			Tag = "elastic"
		};
		stackPanel.Children.Add(item2);
		stackPanel.Children.Add(item);
		stackPanel.Children.Add(meter);
		stackPanel.Children.Add(chart);
		base.Child = stackPanel;
	}

	protected override void Show(Summary sum)
	{
		label.Text = Naming.Title(base.Slot, Index, sum.Title);
		second.Text = (sum.Second.HasValue ? sum.SecondLabel : "");
		Motion.Text(value, sum.Value);
		value.Foreground = Palette.Brush((sum.Color == P.Ink) ? P.Ink : sum.Color);
		unit.Text = ((sum.Value == "—") ? "" : sum.Unit);
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
		}
		else
		{
			meter.Set(sum.Fraction, Shade(sum));
		}
	}
}
