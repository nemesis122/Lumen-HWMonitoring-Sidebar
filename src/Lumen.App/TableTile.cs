using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed class TableTile : SummaryTile
{
	private readonly Grid table = new Grid
	{
		ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
		Margin = new Thickness(0.0, 6.0, 0.0, 0.0)
	};

	private readonly List<(TextBlock Label, Meter Bar, TextBlock Value)> rows = new List<(TextBlock, Meter, TextBlock)>();

	// "Table graph": the further values (clock, voltage, fan, free …) become table lines with bars too.
	private readonly bool graph;

	private List<Reading> shown = new List<Reading>();

	public TableTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s, bool graph = false)
		: base(kind, index, p, m, s, half: false)
	{
		this.graph = graph;
		StackPanel stackPanel = new StackPanel
		{
			Spacing = 4.0,
			Tag = "elastic"
		};
		stackPanel.Children.Add(base.Head);
		stackPanel.Children.Add(table);
		stackPanel.Children.Add(Extras);
		// Graph designs: the free height goes below the lines, not between the title and the first line,
		// so in a bar the lines of all cards start right under their titles.
		if (graph)
		{
			stackPanel.Tag = null;
		}
		base.Child = stackPanel;
	}

	private void Shape(int n)
	{
		if (rows.Count == n)
		{
			return;
		}
		table.Children.Clear();
		rows.Clear();
		table.RowDefinitions = new RowDefinitions(string.Join(",", Enumerable.Repeat("Auto", Math.Max(1, n))));
		for (int i = 0; i < n; i++)
		{
			TextBlock textBlock = Ui.Text("", 12.0, P.Soft, FontWeight.Medium);
			textBlock.MaxWidth = 110.0;
			Meter meter = new Meter
			{
				Height = 5.0,
				Palette = P,
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(10.0, 0.0, 10.0, 0.0)
			};
			TextBlock textBlock2 = Ui.Text("", 12.5, P.Ink, FontWeight.DemiBold, numbers: true);
			textBlock2.TextAlignment = TextAlignment.Right;
			textBlock2.MinWidth = 52.0;
			(Control, int)[] array = new(Control, int)[3]
			{
				(textBlock, 0),
				(meter, 1),
				(textBlock2, 2)
			};
			for (int j = 0; j < array.Length; j++)
			{
				var (control, num) = array[j];
				Grid.SetRow(control, i);
				Grid.SetColumn(control, num);
				control.Margin = ((num == 1) ? meter.Margin : new Thickness(0.0, 3.0, 0.0, 3.0));
				table.Children.Add(control);
			}
			rows.Add((textBlock, meter, textBlock2));
		}
	}

	protected override void Show(Summary sum)
	{
		List<Reading> list = Readings.For(Kind, Index, base.Last.Snap, base.Last.History, S, P);
		shown = list;
		if (graph)
		{
			// Lines of further values from the last update are emptied first, so they do not count as
			// "already shown" when the values are picked again; they are filled in AdjustExtras.
			Shape(Math.Max(rows.Count, list.Count));
			for (int i = list.Count; i < rows.Count; i++)
			{
				rows[i].Label.Text = rows[i].Value.Text = "";
			}
		}
		else
		{
			Shape(list.Count);
		}
		for (int i = 0; i < list.Count; i++)
		{
			Fill(i, list[i].Label, list[i].Value, list[i].Fraction, list[i].Color);
		}
	}

	private void Fill(int i, string label, string value, double? fraction, Color color)
	{
		rows[i].Label.Text = label;
		rows[i].Bar.IsVisible = fraction.HasValue;
		rows[i].Bar.Set(fraction ?? 0, color);
		rows[i].Value.Text = value;
		rows[i].Value.Foreground = Palette.Brush((color == P.Cool) ? P.Ink : color);
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
			Shape(n);
			for (int i = 0; i < shown.Count; i++)
			{
				Fill(i, shown[i].Label, shown[i].Value, shown[i].Fraction, shown[i].Color);
			}
		}
		for (int k = 0; k < items.Count; k++)
		{
			var (label, value) = items[k];
			double? fraction = SegmentScale.Fraction(Kind, label, value, Last.Snap);
			Color color = value.Contains('°') && fraction is double heat ? P.At(heat) : P.Cool;
			Fill(shown.Count + k, label, value, fraction, color);
		}
		return new List<(string Label, string Value)>();
	}
}
