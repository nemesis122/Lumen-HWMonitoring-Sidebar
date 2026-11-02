using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed class BadgeListTile : SummaryTile
{
	private readonly Grid table = new Grid
	{
		ColumnDefinitions = new ColumnDefinitions("*,Auto"),
		Margin = new Thickness(0.0, 6.0, 0.0, 0.0)
	};

	private readonly List<(TextBlock Label, Border Badge, TextBlock Value, Border Rule)> rows = new List<(TextBlock, Border, TextBlock, Border)>();

	public BadgeListTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s)
		: base(kind, index, p, m, s, half: false)
	{
		StackPanel stackPanel = new StackPanel
		{
			Spacing = 2.0,
			Tag = "elastic"
		};
		stackPanel.Children.Add(base.Head);
		stackPanel.Children.Add(table);
		stackPanel.Children.Add(Extras);
		base.Child = stackPanel;
	}

	protected override void Show(Summary sum)
	{
		List<Reading> list = Readings.For(Kind, Index, base.Last.Snap, base.Last.History, S, P);
		if (rows.Count != list.Count)
		{
			table.Children.Clear();
			rows.Clear();
			table.RowDefinitions = new RowDefinitions(string.Join(",", Enumerable.Repeat("Auto", Math.Max(1, list.Count))));
			for (int i = 0; i < list.Count; i++)
			{
				TextBlock textBlock = Ui.Text("", 12.5, P.Soft, FontWeight.Medium);
				textBlock.VerticalAlignment = VerticalAlignment.Center;
				TextBlock textBlock2 = Ui.Text("", 12.5, P.Ink, FontWeight.Bold, numbers: true);
				Border border = new Border
				{
					CornerRadius = new CornerRadius(9.0),
					Padding = new Thickness(9.0, 2.0),
					Child = textBlock2,
					VerticalAlignment = VerticalAlignment.Center,
					Margin = new Thickness(8.0, 3.0, 0.0, 3.0)
				};
				Border border2 = new Border
				{
					Height = 1.0,
					Background = Palette.Brush(P.Track),
					VerticalAlignment = VerticalAlignment.Bottom,
					IsVisible = (i < list.Count - 1)
				};
				Grid.SetRow(textBlock, i);
				Grid.SetRow(border, i);
				Grid.SetColumn(border, 1);
				Grid.SetRow(border2, i);
				Grid.SetColumnSpan(border2, 2);
				table.Children.Add(border2);
				table.Children.Add(textBlock);
				table.Children.Add(border);
				rows.Add((textBlock, border, textBlock2, border2));
			}
		}
		for (int j = 0; j < list.Count; j++)
		{
			Reading reading = list[j];
			rows[j].Label.Text = ((reading.Label.Length > 0) ? reading.Label : sum.Caption);
			rows[j].Value.Text = reading.Value;
			rows[j].Badge.Background = Palette.Brush(Palette.Alpha(reading.Color, P.Dark ? 0.2 : 0.16));
			rows[j].Value.Foreground = Palette.Brush((reading.Color == P.Cool) ? P.Ink : reading.Color);
		}
	}
}
