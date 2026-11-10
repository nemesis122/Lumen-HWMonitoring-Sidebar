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

	// "Badge list graph": every line (also clock, voltage, fan, free …) gets a bar between name and badge.
	private readonly bool graph;

	private readonly List<FillBack> lineBars = new List<FillBack>();

	private List<Reading> shown = new List<Reading>();

	private string current = "";

	public BadgeListTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s, bool graph = false)
		: base(kind, index, p, m, s, half: false)
	{
		this.graph = graph;
		if (graph)
		{
			table.ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto");
		}
		StackPanel stackPanel = new StackPanel
		{
			Spacing = 2.0,
			Tag = "elastic"
		};
		stackPanel.Children.Add(base.Head);
		stackPanel.Children.Add(table);
		stackPanel.Children.Add(Extras);
		// The free height goes below the lines, not between the title and the first line,
		// so in a bar the lines of all cards start right under their titles.
		stackPanel.Tag = null;
		base.Child = stackPanel;
	}

	private void Ensure(int n)
	{
		if (rows.Count == n)
		{
			return;
		}
		table.Children.Clear();
		rows.Clear();
		lineBars.Clear();
		table.RowDefinitions = new RowDefinitions(string.Join(",", Enumerable.Repeat("Auto", Math.Max(1, n))));
		int badgeColumn = graph ? 2 : 1;
		for (int i = 0; i < n; i++)
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
				HorizontalAlignment = HorizontalAlignment.Right,
				Margin = new Thickness(8.0, 3.0, 0.0, 3.0)
			};
			Border border2 = new Border
			{
				Height = 1.0,
				Background = Palette.Brush(P.Track),
				VerticalAlignment = VerticalAlignment.Bottom,
				IsVisible = (i < n - 1)
			};
			Grid.SetRow(textBlock, i);
			Grid.SetRow(border, i);
			Grid.SetColumn(border, badgeColumn);
			Grid.SetRow(border2, i);
			Grid.SetColumnSpan(border2, badgeColumn + 1);
			table.Children.Add(border2);
			table.Children.Add(textBlock);
			table.Children.Add(border);
			if (graph)
			{
				var bar = new FillBack { Height = 5, Radius = 2.5, Track = Palette.Mix(P.Card, P.Ink, P.Dark ? 0.1 : 0.08), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
				Grid.SetRow(bar, i);
				Grid.SetColumn(bar, 1);
				table.Children.Add(bar);
				lineBars.Add(bar);
			}
			rows.Add((textBlock, border, textBlock2, border2));
		}
	}

	private void Fill(int i, string label, string value, double? fraction, Color color)
	{
		rows[i].Label.Text = label.Length > 0 ? label : current;
		rows[i].Value.Text = value;
		rows[i].Badge.Background = Palette.Brush(Palette.Alpha(color, P.Dark ? 0.2 : 0.16));
		rows[i].Value.Foreground = Palette.Brush((color == P.Cool) ? P.Ink : color);
		rows[i].Badge.IsVisible = true;
		if (graph)
		{
			lineBars[i].IsVisible = fraction.HasValue || Kind == WidgetKind.Drives && value == "—";
            lineBars[i].Opacity = value == "—" ? .3 : 1;
			lineBars[i].Animate(fraction ?? 0, Color.FromArgb(255, color.R, color.G, color.B));
		}
	}

	protected override void Show(Summary sum)
	{
		List<Reading> list = Readings.For(Kind, Index, base.Last.Snap, base.Last.History, S, P);
		shown = list;
		current = sum.Caption.Length > 0 ? sum.Caption : WidgetSensors.PrimaryLabel(Kind);
		if (graph)
		{
			// Lines of further values from the last update are emptied first, so they do not count as
			// "already shown" when the values are picked again; AdjustExtras fills them.
			Ensure(Math.Max(rows.Count, list.Count));
			for (int i = list.Count; i < rows.Count; i++)
			{
				rows[i].Label.Text = rows[i].Value.Text = "";
			}
		}
		else
		{
			Ensure(list.Count);
		}
		for (int j = 0; j < list.Count; j++)
		{
			Fill(j, list[j].Label, list[j].Value, list[j].Fraction, list[j].Color);
            BindReading(rows[j].Value, list[j]);
		}
	}

	protected override List<(string Label, string Value)> AdjustExtras(List<(string Label, string Value)> items)
	{
		if (!graph)
		{
			return items;
		}
		int n = shown.Count + items.Count;
		if (rows.Count != n)
		{
			Ensure(n);
			for (int i = 0; i < shown.Count; i++)
			{
				Fill(i, shown[i].Label, shown[i].Value, shown[i].Fraction, shown[i].Color);
                BindReading(rows[i].Value, shown[i]);
			}
		}
		for (int k = 0; k < items.Count; k++)
		{
			var (label, value) = items[k];
			double? fraction = ExtraFraction(label, value);
			Color color = value.Contains('°') && fraction is double heat ? P.At(heat) : P.Cool;
			Fill(shown.Count + k, label, value, fraction, color);
		}
		return new List<(string Label, string Value)>();
	}
}
