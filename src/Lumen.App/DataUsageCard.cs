using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed class DataUsageCard : Card
{
	private readonly Hero down;

	private readonly Hero up;

	private readonly Dictionary<string, (TextBlock down, TextBlock up)> rows = new Dictionary<string, (TextBlock, TextBlock)>();

	public DataUsageCard(Palette p, Metrics m, Settings s)
: base(p, m, s, "M8,2 V10 M5,7 L8,10 L11,7 M3,13.5 H13", L.T("Data usage"))
	{
		Palette p2 = p;
		down = new Hero(Math.Round(m.Hero * 0.58), p2);
		up = new Hero(Math.Round(m.Hero * 0.58), p2);
		Control control = Labelled(L.T("Upload"), p2.Calm, up);
		control.HorizontalAlignment = HorizontalAlignment.Right;
		Body.Children.Add(Ui.Columns("*,Auto", Labelled(L.T("Download"), p2.Cool, down), control));
		StackPanel stackPanel = new StackPanel
		{
			Spacing = 4.0
		};
		(string, string)[] array = new(string, string)[2]
		{
			("data.week", L.T("This week")),
			("data.month", L.T("This month"))
		};
		for (int i = 0; i < array.Length; i++)
		{
			var (key, text) = array[i];
			if (Shows(key))
			{
				TextBlock textBlock = Ui.Text("", 12.0, p2.Ink, FontWeight.Medium, numbers: true);
				TextBlock textBlock2 = Ui.Text("", 12.0, p2.Soft, FontWeight.Normal, numbers: true);
                WidgetPresentation.Sensor(textBlock, key + ".down");
                WidgetPresentation.Sensor(textBlock2, key + ".up");
				textBlock.HorizontalAlignment = HorizontalAlignment.Right;
				textBlock2.HorizontalAlignment = HorizontalAlignment.Right;
				textBlock2.Width = 82.0;
				textBlock2.TextAlignment = TextAlignment.Right;
				stackPanel.Children.Add(Ui.Columns("*,Auto,Auto", Card.Label(text, p2), textBlock, textBlock2));
				rows[key] = (textBlock, textBlock2);
			}
		}
		if (stackPanel.Children.Count > 0)
		{
			Body.Children.Add(stackPanel);
		}
		Control Labelled(string text2, Color color, Hero hero)
		{
			Border border = new Border
			{
				Width = 6.0,
				Height = 6.0,
				CornerRadius = new CornerRadius(3.0),
				Background = Palette.Brush(color),
				VerticalAlignment = VerticalAlignment.Center
			};
			return Ui.Stack(1.0, Ui.Row(5.0, border, Card.Label(text2, p2)), hero);
		}
        WidgetPresentation.Sensor((Control)down.Parent!, "data.down");
        WidgetPresentation.Sensor((Control)up.Parent!, "data.up");
	}

	protected override void UpdateReadings(Snapshot s, History h)
	{
		UsageInfo usage = s.Usage;
		base.Aside = L.T("Today");
		var (value, unitText) = Format.Split(Format.Size(usage?.DownToday));
		var (value2, unitText2) = Format.Split(Format.Size(usage?.UpToday));
		down.Set(value, unitText, P.Ink);
		up.Set(value2, unitText2, P.Ink);
		if (!(usage == null))
		{
			if (rows.TryGetValue("data.week", out (TextBlock, TextBlock) value3))
			{
				value3.Item1.Text = "↓ " + Format.Size(usage.DownWeek);
				value3.Item2.Text = "↑ " + Format.Size(usage.UpWeek);
			}
			if (rows.TryGetValue("data.month", out (TextBlock, TextBlock) value4))
			{
				value4.Item1.Text = "↓ " + Format.Size(usage.DownMonth);
				value4.Item2.Text = "↑ " + Format.Size(usage.UpMonth);
			}
		}
	}
}

