using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed class SplitTile : SummaryTile
{
	private readonly Grid grid = new Grid
	{
		Margin = new Thickness(0.0, 8.0, 0.0, 0.0)
	};

	private readonly List<(TextBlock Value, TextBlock Label)> cells = new List<(TextBlock, TextBlock)>();

	public SplitTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s)
		: base(kind, index, p, m, s, half: false)
	{
		StackPanel stackPanel = new StackPanel
		{
			Spacing = 2.0,
			Tag = "elastic"
		};
		stackPanel.Children.Add(base.Head);
		stackPanel.Children.Add(grid);
		stackPanel.Children.Add(Extras);
		base.Child = stackPanel;
	}

	protected override void Show(Summary sum)
	{
		List<Reading> list = Readings.For(Kind, Index, base.Last.Snap, base.Last.History, S, P).Take(3).ToList();
		if (cells.Count != list.Count)
		{
			grid.Children.Clear();
			cells.Clear();
			grid.ColumnDefinitions = new ColumnDefinitions(string.Join(",Auto,", Enumerable.Repeat("*", Math.Max(1, list.Count))));
			for (int i = 0; i < list.Count; i++)
			{
				if (i > 0)
				{
					Border border = new Border
					{
						Width = 1.0,
						Height = 34.0,
						Background = Palette.Brush(P.Track),
						VerticalAlignment = VerticalAlignment.Center
					};
					Grid.SetColumn(border, i * 2 - 1);
					grid.Children.Add(border);
				}
				TextBlock textBlock = Ui.Text("", M.Subtitles ? 26 : 22, P.Ink, FontWeight.ExtraBold, numbers: true);
				textBlock.LetterSpacing = -1.0;
				TextBlock textBlock2 = Ui.Text("", 10.0, P.Faint, FontWeight.DemiBold);
				textBlock2.LetterSpacing = 0.8;
				StackPanel stackPanel = Ui.Stack(1.0, SteadyHeight.Of(new Viewbox
				{
					Child = textBlock,
					StretchDirection = StretchDirection.DownOnly,
					HorizontalAlignment = HorizontalAlignment.Left
				}), textBlock2);
				stackPanel.Margin = new Thickness((i != 0) ? 12 : 0, 0.0, 6.0, 0.0);
				Grid.SetColumn(stackPanel, i * 2);
				grid.Children.Add(stackPanel);
				cells.Add((textBlock, textBlock2));
			}
		}
		for (int j = 0; j < list.Count; j++)
		{
            BindReading(cells[j].Value, list[j]);
			cells[j].Value.Text = list[j].Value.Replace(" °C", "°").Replace(" °F", "°F");
			cells[j].Value.Foreground = Palette.Brush((list[j].Color == P.Cool) ? P.Ink : list[j].Color);
			cells[j].Label.Text = list[j].Label.ToUpper(L.Culture);
		}
	}
}
