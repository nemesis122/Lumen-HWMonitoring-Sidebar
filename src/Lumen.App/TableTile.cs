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

	public TableTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s)
		: base(kind, index, p, m, s, half: false)
	{
		StackPanel stackPanel = new StackPanel
		{
			Spacing = 4.0,
			Tag = "elastic"
		};
		stackPanel.Children.Add(base.Head);
		stackPanel.Children.Add(table);
		stackPanel.Children.Add(Extras);
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
		Shape(list.Count);
		for (int i = 0; i < list.Count; i++)
		{
			rows[i].Label.Text = list[i].Label;
			rows[i].Bar.Set(list[i].Fraction, list[i].Color);
			rows[i].Value.Text = list[i].Value;
			rows[i].Value.Foreground = Palette.Brush((list[i].Color == P.Cool) ? P.Ink : list[i].Color);
		}
	}
}
