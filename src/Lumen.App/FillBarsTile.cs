using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed class FillBarsTile : SummaryTile
{
	private readonly StackPanel bars = new StackPanel
	{
		Spacing = 6.0,
		Margin = new Thickness(0.0, 8.0, 0.0, 0.0)
	};

	private readonly List<(FillBack Back, TextBlock Name, TextBlock Value)> rows = new List<(FillBack, TextBlock, TextBlock)>();

	public FillBarsTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s)
		: base(kind, index, p, m, s, half: false)
	{
		StackPanel stackPanel = new StackPanel
		{
			Spacing = 2.0,
			Tag = "elastic"
		};
		stackPanel.Children.Add(base.Head);
		stackPanel.Children.Add(bars);
		stackPanel.Children.Add(Extras);
		base.Child = stackPanel;
	}

	protected override void Show(Summary sum)
	{
		List<Reading> list = Readings.For(Kind, Index, base.Last.Snap, base.Last.History, S, P);
		double height = (M.Subtitles ? 30 : 26);
		if (rows.Count != list.Count)
		{
			bars.Children.Clear();
			rows.Clear();
			foreach (Reading item in list)
			{
				_ = item;
				FillBack fillBack = new FillBack
				{
					Radius = 10.0,
					Track = Palette.Mix(P.Card, P.Ink, P.Dark ? 0.06 : 0.05)
				};
				TextBlock textBlock = Ui.Text("", 12.5, P.Ink, FontWeight.DemiBold);
				TextBlock textBlock2 = Ui.Text("", 12.5, P.Ink, FontWeight.DemiBold, numbers: true);
				textBlock2.HorizontalAlignment = HorizontalAlignment.Right;
				Grid grid = Ui.Columns("*,Auto", textBlock, textBlock2);
				Grid.SetColumn(textBlock2, 1);
				grid.Margin = new Thickness(11.0, 0.0, 11.0, 0.0);
				bars.Children.Add(new Panel
				{
					Height = height,
					Children = 
					{
						(Control)fillBack,
						(Control)grid
					}
				});
				rows.Add((fillBack, textBlock, textBlock2));
			}
		}
		for (int i = 0; i < list.Count; i++)
		{
			Reading reading = list[i];
			Color color = Color.FromArgb(byte.MaxValue, reading.Color.R, reading.Color.G, reading.Color.B);
			rows[i].Back.Animate(reading.Fraction, color);
			rows[i].Name.Text = reading.Label;
			rows[i].Value.Text = reading.Value;
			rows[i].Name.Foreground = Palette.Brush((reading.Fraction > 0.22) ? Readings.TextOn(color, P) : P.Ink);
			rows[i].Value.Foreground = Palette.Brush((reading.Fraction > 0.85) ? Readings.TextOn(color, P) : P.Ink);
		}
	}
}
