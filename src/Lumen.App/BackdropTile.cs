using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Lumen.Core;

namespace Lumen.App;

public sealed class BackdropTile : SummaryTile
{
	private readonly Hero hero;

	private readonly AreaChart chart;

	private readonly FillBack fill;

	public BackdropTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s)
		: base(kind, index, p, m, s, half: false)
	{
		base.ClipToBounds = true;
		hero = new Hero(m.Subtitles ? 34 : 28, p)
		{
			Margin = new Thickness(0.0, 6.0, 0.0, 0.0)
		};
		Caption.HorizontalAlignment = HorizontalAlignment.Right;
		chart = new AreaChart
		{
			LineWidth = 2.0,
			FillOpacity = 0.26,
			VerticalAlignment = VerticalAlignment.Bottom,
			Tag = "fixed",
			IsHitTestVisible = false
		};
		fill = new FillBack
		{
			Height = 6.0,
			Radius = 3.0,
			Track = p.Track,
			VerticalAlignment = VerticalAlignment.Bottom
		};
		StackPanel stackPanel = new StackPanel
		{
			Spacing = 0.0,
			Tag = "elastic"
		};
		stackPanel.Children.Add(base.Head);
		stackPanel.Children.Add(hero);
		stackPanel.Children.Add(new Border
		{
			Height = (m.Subtitles ? 40 : 32)
		});
		Panel item = new Panel
		{
			Margin = new Thickness(0.0 - m.Pad, 0.0, 0.0 - m.Pad, 0.0 - (m.Pad - 2.0)),
			Children = { (Control)chart }
		};
		base.Child = new Panel
		{
			Children = 
			{
				(Control)item,
				(Control)fill,
				(Control)stackPanel
			}
		};
	}

	protected override Size ArrangeOverride(Size finalSize)
	{
		chart.Height = Math.Max(20.0, finalSize.Height * 0.55);
		return base.ArrangeOverride(finalSize);
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
		fill.IsVisible = !flag;
		if (flag)
		{
			chart.Max = sum.SeriesMax;
			chart.Set(sum.Series, Shade(sum));
		}
		else
		{
			fill.Animate(sum.Fraction, Shade(sum));
		}
	}
}
