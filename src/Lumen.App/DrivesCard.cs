using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed class DrivesCard : Card
{
	private readonly StackPanel rows = new StackPanel();

	private readonly Dictionary<string, (TextBlock name, TextBlock temp, Meter used, TextBlock usedText, TextBlock read, TextBlock write)> map = new Dictionary<string, (TextBlock, TextBlock, Meter, TextBlock, TextBlock, TextBlock)>();

	public DrivesCard(Palette p, Metrics m, Settings s)
		: base(p, m, s, "M2,4 A1.5,1.5 0 0 1 3.5,2.5 H12.5 A1.5,1.5 0 0 1 14,4 V12 A1.5,1.5 0 0 1 12.5,13.5 H3.5 A1.5,1.5 0 0 1 2,12 Z M2,9.5 H14 M11,11.5 H12", L.T("Drives"))
	{
		rows.Spacing = m.Gap + 2.0;
		Body.Children.Add(rows);
	}

	public override void Update(Snapshot s, History h)
	{
		List<DiskInfo> list = s.Disks.Where((DiskInfo d) => S.Shows("drive:" + d.Id)).ToList();
		base.Aside = $"{list.Count} {((list.Count == 1) ? L.T("drive") : L.T("drives"))}";
		foreach (string item in map.Keys.Except(list.Select((DiskInfo d) => d.Id)).ToList())
		{
			rows.Children.Remove(map[item].name.Parent.Parent as Control);
			map.Remove(item);
		}
		foreach (DiskInfo item2 in list)
		{
			if (!map.TryGetValue(item2.Id, out (TextBlock, TextBlock, Meter, TextBlock, TextBlock, TextBlock) value))
			{
				TextBlock textBlock = Ui.Text("", 12.0, P.Ink, FontWeight.Medium);
				TextBlock textBlock2 = Ui.Text("", 12.0, P.Ink, FontWeight.DemiBold, numbers: true);
				Meter meter = new Meter
				{
					Height = 5.0,
					Palette = P
				};
				TextBlock textBlock3 = Ui.Text("", 11.5, P.Soft, FontWeight.Normal, numbers: true);
				TextBlock textBlock4 = Rate();
				TextBlock textBlock5 = Rate();
				Grid grid = Ui.Columns("Auto,Auto,Auto,Auto", Letter("R", 0.0), textBlock4, Letter("W", 10.0), textBlock5);
				grid.IsVisible = Shows("drives.io");
				textBlock2.IsVisible = Shows("drives.temp");
				meter.IsVisible = Shows("drives.bar");
				textBlock3.IsVisible = Shows("drives.used");
				Grid grid2 = Ui.Columns("*,Auto", textBlock3, grid);
				grid2.IsVisible = textBlock3.IsVisible || grid.IsVisible;
				rows.Children.Add(Ui.Stack(5.0, Ui.Columns("*,Auto", textBlock, textBlock2), meter, grid2));
				value = (map[item2.Id] = (textBlock, textBlock2, meter, textBlock3, textBlock4, textBlock5));
			}
			string text = ((Shows("drives.model") || item2.Letters.Length == 0) ? ShortName(item2.Name) : "");
			value.Item1.Text = ((item2.Letters.Length > 0) ? ((text.Length > 0) ? (text + " · " + item2.Letters) : item2.Letters) : text);
			Motion.Text(value.Item2, TempWithUnit(item2.Temperature));
			value.Item2.Foreground = Palette.Brush((!item2.Temperature.HasValue) ? P.Faint : HeatColor(item2.Temperature + 10.0));
			double num = Format.Share(item2.UsedPercent);
			value.Item3.Set(num, (num >= 0.9) ? P.Ramp[^1] : ((num >= 0.8) ? P.Ramp[2] : P.Cool));
			value.Item4.Text = ((!item2.UsedPercent.HasValue) ? "" : (Format.Percent(item2.UsedPercent) + " " + L.T("used")));
			value.Item5.Text = Format.Rate(Recent(h.Read("disk." + item2.Id + ".read"), item2.ReadBytesPerSecond));
			value.Item6.Text = Format.Rate(Recent(h.Read("disk." + item2.Id + ".write"), item2.WriteBytesPerSecond));
		}
		TextBlock Letter(string text2, double left)
		{
			TextBlock textBlock6 = Ui.Text(text2, 11.5, P.Faint, FontWeight.DemiBold);
			textBlock6.Margin = new Thickness(left, 0.0, 4.0, 0.0);
			return textBlock6;
		}
		TextBlock Rate()
		{
			TextBlock textBlock6 = Ui.Text("", 11.5, P.Soft, FontWeight.Normal, numbers: true);
			textBlock6.Width = 64.0;
			textBlock6.TextAlignment = TextAlignment.Right;
			return textBlock6;
		}
	}

	private static double? Recent(double[] series, double? current)
	{
		double[] array = series.Skip(Math.Max(0, series.Length - 3)).Where(double.IsFinite).ToArray();
		if (array.Length == 0)
		{
			return current;
		}
		return array.Average();
	}

	private static string ShortName(string name)
	{
		string[] array = new string[6] { "Samsung SSD ", "Samsung ", "WDC ", "WD ", "KINGSTON ", "Crucial " };
		foreach (string text in array)
		{
			if (name.StartsWith(text, StringComparison.OrdinalIgnoreCase))
			{
				int length = text.Length;
				return name.Substring(length, name.Length - length);
			}
		}
		return name;
	}
}
