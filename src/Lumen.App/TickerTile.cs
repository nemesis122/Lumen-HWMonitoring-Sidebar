using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed class TickerTile : SummaryTile
{
	private readonly TextBlock name;

	private readonly TextBlock sub;

	private readonly TextBlock value;

	private readonly AreaChart spark;

	private readonly Border badge;

	private readonly TextBlock badgeText;

	public TickerTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s)
		: base(kind, index, p, m, s, half: false)
	{
		base.Padding = new Thickness(16.0, 10.0, 14.0, 10.0);
		name = Ui.Text("", 13.5, p.Ink, FontWeight.DemiBold);
		sub = Ui.Text("", 11.0, p.Soft);
		spark = new AreaChart
		{
			Width = 84.0,
			Height = 28.0,
			LineWidth = 1.8,
			FillOpacity = 0.0,
			Tag = "fixed",
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(10.0, 0.0, 12.0, 0.0)
		};
		value = Ui.Text("", 15.5, p.Ink, FontWeight.Bold, numbers: true);
		value.HorizontalAlignment = HorizontalAlignment.Right;
		badgeText = Ui.Text("", 10.5, p.Ink, FontWeight.DemiBold, numbers: true);
		badge = new Border
		{
			CornerRadius = new CornerRadius(6.0),
			Padding = new Thickness(6.0, 1.0),
			HorizontalAlignment = HorizontalAlignment.Right,
			Child = badgeText,
			Margin = new Thickness(0.0, 3.0, 0.0, 0.0)
		};
		StackPanel stackPanel = Ui.Stack(0.0, name, sub);
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel2 = Ui.Stack(0.0, value, badge);
		stackPanel2.VerticalAlignment = VerticalAlignment.Center;
		Grid child = Ui.Columns("*,Auto,Auto", stackPanel, spark, stackPanel2);
		Grid.SetColumn(spark, 1);
		Grid.SetColumn(stackPanel2, 2);
		base.Child = child;
	}

	protected override void Show(Summary sum)
	{
		name.Text = Naming.Title(base.Slot, Index, sum.Title);
		string text = Naming.Hardware(base.Slot, Index, sum.Name);
		sub.Text = ((text.Length > 0 && Naming.ShowHardware(base.Slot, S)) ? text : sum.Caption);
		value.Text = ((sum.Value == "—") ? sum.Value : (sum.Value + " " + sum.Unit).Trim());
		Color color = Shade(sum);
		int num;
		if (sum.Series.Length > 1)
		{
			string text2 = Summaries.ChartKey(Kind);
			num = ((text2 == null || S.Shows(text2)) ? 1 : 0);
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
			spark.Set(sum.Series, color);
		}
		if (sum.Series.Length > 10)
		{
			double num2 = sum.Series[^1];
			double num3 = sum.Series[Math.Max(0, sum.Series.Length - 60)];
			double num4 = num2 - num3;
			string text3 = Readings.SeriesText(Kind, Math.Abs(num4));
			badgeText.Text = ((num4 >= 0.0) ? "+" : "−") + text3;
			badge.Background = Palette.Brush(color);
			badgeText.Foreground = Palette.Brush(Readings.TextOn(color, P));
			badge.IsVisible = true;
		}
		else if (sum.Second.HasValue)
		{
			badgeText.Text = sum.SecondLabel;
			badge.Background = Palette.Brush(P.Track);
			badgeText.Foreground = Palette.Brush(P.Soft);
			badge.IsVisible = true;
		}
		else
		{
			badge.IsVisible = false;
		}
	}
}
