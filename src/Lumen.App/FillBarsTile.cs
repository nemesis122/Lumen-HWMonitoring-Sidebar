using System;
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

	// "Fill bars graph": the further values (clock, voltage, fan, free …) become fill bars too.
	private readonly bool graph;

	private List<Reading> shown = new List<Reading>();

	public FillBarsTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s, bool graph = false)
		: base(kind, index, p, m, s, half: false)
	{
		this.graph = graph;
		StackPanel stackPanel = new StackPanel
		{
			Spacing = 2.0,
			Tag = "elastic"
		};
		stackPanel.Children.Add(base.Head);
		stackPanel.Children.Add(bars);
		stackPanel.Children.Add(Extras);
		// Graph designs: the free height goes below the lines, not between the title and the first line,
		// so in a bar the lines of all cards start right under their titles.
		if (graph)
		{
			stackPanel.Tag = null;
		}
		base.Child = stackPanel;
	}

	private void Ensure(int n)
	{
		double height = (M.Subtitles ? 30 : 26);
		if (rows.Count == n)
		{
			return;
		}
		bars.Children.Clear();
		rows.Clear();
		for (int i = 0; i < n; i++)
		{
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

	private void Fill(int i, string label, string value, double fraction, Color c)
	{
		Color color = Color.FromArgb(byte.MaxValue, c.R, c.G, c.B);
		rows[i].Back.Animate(fraction, color);
		rows[i].Name.Text = label;
		rows[i].Value.Text = value;
		rows[i].Name.Foreground = Palette.Brush((fraction > 0.22) ? Readings.TextOn(color, P) : P.Ink);
		rows[i].Value.Foreground = Palette.Brush((fraction > 0.85) ? Readings.TextOn(color, P) : P.Ink);
	}

	protected override void Show(Summary sum)
	{
		List<Reading> list = Readings.For(Kind, Index, base.Last.Snap, base.Last.History, S, P);
		shown = list;
		if (graph)
		{
			// Bars of further values from the last update are emptied first, so they do not count as
			// "already shown" when the values are picked again; AdjustExtras fills them.
			Ensure(Math.Max(rows.Count, list.Count));
			for (int i = list.Count; i < rows.Count; i++)
			{
				rows[i].Name.Text = rows[i].Value.Text = "";
			}
		}
		else
		{
			Ensure(list.Count);
		}
		for (int i = 0; i < list.Count; i++)
		{
			Fill(i, list[i].Label, list[i].Value, list[i].Fraction, list[i].Color);
            BindReading(rows[i].Value, list[i]);
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
			Fill(shown.Count + k, label, value, fraction ?? 0, color);
		}
		return new List<(string Label, string Value)>();
	}
}
