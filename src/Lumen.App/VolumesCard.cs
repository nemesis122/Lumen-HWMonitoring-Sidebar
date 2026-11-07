using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed class VolumesCard : Card
{
	private readonly StackPanel rows = new StackPanel();

	private readonly Dictionary<string, (TextBlock name, TextBlock free, Meter bar, TextBlock detail)> map = new Dictionary<string, (TextBlock, TextBlock, Meter, TextBlock)>();

	public VolumesCard(Palette p, Metrics m, Settings s)
		: base(p, m, s, "M2,4 H14 V12 H2 Z M4.5,8 H8 M11.5,8 h0.01", L.T("Storage space"))
	{
		rows.Spacing = m.Gap;
		Body.Children.Add(rows);
	}

	protected override void UpdateReadings(Snapshot s, History h)
	{
		List<VolumeInfo> list = s.Volumes.Where((VolumeInfo v) => S.Shows("volume:" + v.Name)).ToList();
		foreach (string item in map.Keys.Except(list.Select((VolumeInfo v) => v.Name)).ToList())
		{
			rows.Children.Remove(map[item].name.Parent.Parent as Control);
			map.Remove(item);
		}
		foreach (VolumeInfo item2 in list)
		{
			if (!map.TryGetValue(item2.Name, out (TextBlock, TextBlock, Meter, TextBlock) value))
			{
				TextBlock textBlock = Ui.Text("", 12.0, P.Ink, FontWeight.Medium);
				TextBlock textBlock2 = Ui.Text("", 12.0, P.Ink, FontWeight.DemiBold, numbers: true);
				Meter meter = new Meter
				{
					Height = 5.0,
					Palette = P
				};
				TextBlock textBlock3 = Ui.Text("", 11.5, P.Soft, FontWeight.Normal, numbers: true);
				textBlock3.IsVisible = Shows("volumes.detail") && Shows("volumes.used");
				textBlock2.IsVisible = Shows("volumes.free");
				meter.IsVisible = Shows("volumes.bar") && Shows("volumes.used");
				rows.Children.Add(Ui.Stack(5.0, Ui.Columns("*,Auto", textBlock, textBlock2), meter, textBlock3));
				value = (map[item2.Name] = (textBlock, textBlock2, meter, textBlock3));
			}
			value.Item1.Text = ((string.IsNullOrWhiteSpace(item2.Label) || !Shows("volumes.label")) ? item2.Name : (item2.Name + "  " + item2.Label));
			Motion.Text(value.Item2, L.F("{0} free", Format.Capacity(item2.FreeGb)));
			double num = item2.UsedPercent / 100.0;
			Color color = ((num >= 0.9) ? P.Ramp[^1] : ((num >= 0.8) ? P.Ramp[2] : P.Cool));
			value.Item2.Foreground = Palette.Brush((num >= 0.8) ? color : P.Ink);
			value.Item3.Set(num, color);
			value.Item4.Text = L.F("{0} of {1} used", Format.Capacity(item2.TotalGb - item2.FreeGb), Format.Capacity(item2.TotalGb));
		}
	}
}
