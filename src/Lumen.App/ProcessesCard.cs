using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed class ProcessesCard : Card
{
	private readonly StackPanel list = new StackPanel
	{
		Spacing = 7.0
	};

	private readonly List<(TextBlock name, TextBlock mem, TextBlock cpu, Meter bar)> rows = new List<(TextBlock, TextBlock, TextBlock, Meter)>();

	public ProcessesCard(Palette p, Metrics m, Settings s)
		: base(p, m, s, "M2,2.5 H6.5 V7 H2 Z M9.5,2.5 H14 V7 H9.5 Z M2,9 H6.5 V13.5 H2 Z M9.5,9 H14 V13.5 H9.5 Z", L.T("Top apps"))
	{
		Grid grid = Ui.Columns("*,Auto,Auto", Card.Label("", p), Card.Label("RAM", p), Card.Label("CPU", p));
		grid.Children[1].IsVisible = Shows("proc.memory");
        grid.Children[2].IsVisible = Shows("proc.cpu");
		((TextBlock)grid.Children[1]).Width = 64.0;
		((TextBlock)grid.Children[1]).TextAlignment = TextAlignment.Right;
		((TextBlock)grid.Children[2]).Width = 52.0;
		((TextBlock)grid.Children[2]).TextAlignment = TextAlignment.Right;
		Body.Children.Add(grid);
		Body.Children.Add(list);
	}

	protected override void UpdateReadings(Snapshot s, History h)
	{
		List<ProcessInfo> list = s.Processes.Take(5).ToList();
		while (rows.Count < list.Count)
		{
			TextBlock textBlock = Ui.Text("", 12.0, P.Ink, FontWeight.Medium);
			TextBlock textBlock2 = Ui.Text("", 11.5, P.Soft, FontWeight.Normal, numbers: true);
			textBlock2.Width = 64.0;
			textBlock2.TextAlignment = TextAlignment.Right;
			textBlock2.IsVisible = Shows("proc.memory");
			TextBlock textBlock3 = Ui.Text("", 12.0, P.Ink, FontWeight.DemiBold, numbers: true);
			textBlock3.Width = 52.0;
			textBlock3.TextAlignment = TextAlignment.Right;
			Meter meter = new Meter
			{
				Height = 3.0,
				Palette = P
			};
			WidgetPresentation.Sensor(textBlock3, "proc.cpu");
            WidgetPresentation.Sensor(meter, "proc.cpu");
			this.list.Children.Add(Ui.Stack(4.0, Ui.Columns("*,Auto,Auto", textBlock, textBlock2, textBlock3), meter));
			rows.Add((textBlock, textBlock2, textBlock3, meter));
		}
		double num = Math.Max(25.0, list.Select((ProcessInfo p) => p.CpuPercent).DefaultIfEmpty(0.0).Max());
		for (int num2 = 0; num2 < rows.Count; num2++)
		{
			(TextBlock, TextBlock, TextBlock, Meter) tuple = rows[num2];
			this.list.Children[num2].IsVisible = num2 < list.Count;
			if (num2 < list.Count)
			{
				ProcessInfo processInfo = list[num2];
				tuple.Item1.Text = processInfo.Name;
				Motion.Text(tuple.Item2, (processInfo.MemoryMb >= 1024.0) ? (Format.Number(processInfo.MemoryMb / 1024.0, "0.0") + " GB") : (Format.Number(processInfo.MemoryMb) + " MB"));
				Motion.Text(tuple.Item3, Format.Number(processInfo.CpuPercent, (processInfo.CpuPercent < 10.0) ? "0.0" : "0") + " %");
				tuple.Item3.Foreground = Palette.Brush(P.At(processInfo.CpuPercent / 100.0 * 0.9 + 0.1));
				tuple.Item4.Set(processInfo.CpuPercent / num, P.At(processInfo.CpuPercent / 100.0 * 0.9 + 0.1));
			}
		}
	}
}
