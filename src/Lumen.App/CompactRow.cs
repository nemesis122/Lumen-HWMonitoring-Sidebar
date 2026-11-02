using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed class CompactRow : SummaryTile
{
	private readonly Hero hero;

	private readonly Meter meter;

	private readonly AreaChart spark;

	private bool narrow;

	public CompactRow(WidgetKind kind, int index, Palette p, Metrics m, Settings s)
		: base(kind, index, p, m, s, half: true)
	{
		base.Padding = new Thickness(12.0, 8.0, 12.0, 8.0);
		hero = new Hero(m.Subtitles ? 28 : 24, p)
		{
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(12.0, 0.0, 14.0, 0.0)
		};
		meter = new Meter
		{
			Height = 4.0,
			Palette = p,
			Margin = new Thickness(0.0, 4.0, 0.0, 3.0)
		};
		spark = new AreaChart
		{
			Height = 18.0,
			LineWidth = 1.4,
			FillOpacity = 0.18,
			Tag = "fixed"
		};
		Caption.HorizontalAlignment = HorizontalAlignment.Left;
		Caption.TextTrimming = TextTrimming.CharacterEllipsis;
		StackPanel stackPanel = new StackPanel
		{
			VerticalAlignment = VerticalAlignment.Center,
			Children = 
			{
				(Control)Caption,
				(Control)meter,
				(Control)spark
			}
		};
		base.Head.VerticalAlignment = VerticalAlignment.Center;
		base.Head.MaxWidth = 150.0;
		base.Child = Ui.Columns("Auto,Auto,*", base.Head, hero, stackPanel);
		Grid.SetColumn(stackPanel, 2);
	}

	protected override void OnSizeChanged(SizeChangedEventArgs e)
	{
		base.OnSizeChanged(e);
		double width = e.NewSize.Width;
		bool flag = ((width <= 0.0) ? narrow : (narrow ? (width < 305.0) : (width < 275.0)));
		if (flag != narrow)
		{
			narrow = flag;
			Caption.Opacity = ((!narrow) ? 1 : 0);
			hero.Margin = (narrow ? new Thickness(8.0, 0.0, 8.0, 0.0) : new Thickness(12.0, 0.0, 14.0, 0.0));
		}
	}

	protected override void Show(Summary sum)
	{
		hero.Set(sum.Value, sum.Unit, sum.Color);
		meter.Set(sum.Second ?? sum.Fraction, (!sum.Second.HasValue) ? Shade(sum) : P.Cool);
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
		spark.IsVisible = flag;
		if (flag)
		{
			spark.Max = sum.SeriesMax;
			spark.Set(sum.Series, Shade(sum));
		}
	}
}
