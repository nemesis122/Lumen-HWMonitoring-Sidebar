using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed class RingsTile : SummaryTile
{
	private readonly Rings rings;

	private readonly StackPanel legend = new StackPanel
	{
		Spacing = 8.0,
		VerticalAlignment = VerticalAlignment.Center,
		Margin = new Thickness(14.0, 0.0, 0.0, 0.0)
	};

	private readonly List<(Ellipse Dot, TextBlock Label, TextBlock Value)> rows = new List<(Ellipse, TextBlock, TextBlock)>();

	public RingsTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s)
		: base(kind, index, p, m, s, half: false)
	{
		double num = (m.Subtitles ? 112 : 92);
		rings = new Rings
		{
			Width = num,
			Height = num,
			Palette = p,
			Thickness = (m.Subtitles ? 10 : 8),
			VerticalAlignment = VerticalAlignment.Center
		};
		Grid grid = Ui.Columns("Auto,*", rings, legend);
		Grid.SetColumn(legend, 1);
		grid.Margin = new Thickness(0.0, 6.0, 0.0, 0.0);
		StackPanel stackPanel = new StackPanel
		{
			Spacing = 4.0,
			Tag = "elastic"
		};
		stackPanel.Children.Add(base.Head);
		stackPanel.Children.Add(grid);
		stackPanel.Children.Add(Extras);
		base.Child = stackPanel;
	}

	private Color RingColor(int i, Reading r)
	{
		return i switch
		{
			1 => P.Cool, 
			0 => r.Color, 
			_ => Color.Parse(P.Dark ? "#A78BFA" : "#7C5CDB"), 
		};
	}

	protected override void Show(Summary sum)
	{
		List<Reading> list = Readings.For(Kind, Index, base.Last.Snap, base.Last.History, S, P).Take(3).ToList();
		if (rows.Count != list.Count)
		{
			legend.Children.Clear();
			rows.Clear();
			foreach (Reading item in list)
			{
				_ = item;
				Ellipse ellipse = new Ellipse
				{
					Width = 8.0,
					Height = 8.0,
					VerticalAlignment = VerticalAlignment.Center
				};
				TextBlock textBlock = Ui.Text("", 11.0, P.Soft);
				TextBlock textBlock2 = Ui.Text("", M.Subtitles ? 16 : 14, P.Ink, FontWeight.DemiBold, numbers: true);
				textBlock.Margin = new Thickness(6.0, 0.0, 0.0, 0.0);
				legend.Children.Add(Ui.Stack(0.0, Ui.Row(0.0, ellipse, textBlock), textBlock2));
				rows.Add((ellipse, textBlock, textBlock2));
			}
		}
		Color[] array = list.Select((Reading r, int i) => RingColor(i, r)).ToArray();
		for (int num = 0; num < list.Count; num++)
		{
			rows[num].Dot.Fill = Palette.Brush(array[num]);
			rows[num].Label.Text = list[num].Label;
			rows[num].Value.Text = list[num].Value;
		}
		rings.Animate(list.Select((Reading r) => r.Fraction).ToArray(), array);
	}
}
