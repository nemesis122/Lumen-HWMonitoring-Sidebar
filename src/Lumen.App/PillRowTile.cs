using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed class PillRowTile : SummaryTile
{
	private readonly Grid row = new Grid
	{
		Margin = new Thickness(0.0, 8.0, 0.0, 0.0)
	};

	private readonly List<(FillBack Back, TextBlock Label, TextBlock Value)> pills = new List<(FillBack, TextBlock, TextBlock)>();

	public PillRowTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s)
		: base(kind, index, p, m, s, half: false)
	{
		StackPanel stackPanel = new StackPanel
		{
			Spacing = 2.0,
			Tag = "elastic"
		};
		stackPanel.Children.Add(base.Head);
		stackPanel.Children.Add(row);
		stackPanel.Children.Add(Extras);
		base.Child = stackPanel;
	}

	protected override void Show(Summary sum)
	{
		List<Reading> list = Readings.For(Kind, Index, base.Last.Snap, base.Last.History, S, P).Take(4).ToList();
		double num = (M.Subtitles ? 44 : 38);
		if (pills.Count != list.Count)
		{
			row.Children.Clear();
			pills.Clear();
			row.ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("*", Math.Max(1, list.Count))));
			for (int i = 0; i < list.Count; i++)
			{
				FillBack fillBack = new FillBack
				{
					Radius = num / 2.0,
					Track = Palette.Mix(P.Card, P.Ink, P.Dark ? 0.06 : 0.05)
				};
				TextBlock textBlock = Ui.Text("", 13.5, P.Ink, FontWeight.Bold, numbers: true);
				TextBlock textBlock2 = Ui.Text("", 9.5, P.Soft, FontWeight.DemiBold);
				textBlock2.LetterSpacing = 0.5;
				TextBlock[] array = new TextBlock[2] { textBlock, textBlock2 };
				for (int j = 0; j < array.Length; j++)
				{
					array[j].HorizontalAlignment = HorizontalAlignment.Center;
				}
				StackPanel stackPanel = Ui.Stack(0.0, textBlock, textBlock2);
				stackPanel.VerticalAlignment = VerticalAlignment.Center;
				Panel panel = new Panel
				{
					Height = num,
					Children = 
					{
						(Control)fillBack,
						(Control)stackPanel
					},
					Margin = new Thickness((i != 0) ? 3 : 0, 0.0, (i != list.Count - 1) ? 3 : 0, 0.0)
				};
				Grid.SetColumn(panel, i);
				row.Children.Add(panel);
				pills.Add((fillBack, textBlock2, textBlock));
			}
		}
		for (int k = 0; k < list.Count; k++)
		{
			Reading reading = list[k];
			pills[k].Back.Animate(reading.Fraction, Palette.Alpha(reading.Color, P.Dark ? 0.34 : 0.28));
			pills[k].Value.Text = reading.Value.Replace(" °C", "°").Replace(" °F", "°");
			pills[k].Value.Foreground = Palette.Brush((reading.Color == P.Cool) ? P.Ink : reading.Color);
			pills[k].Label.Text = reading.Label.ToUpper(L.Culture);
			pills[k].Label.IsVisible = reading.Label.Length > 0;
		}
	}
}
