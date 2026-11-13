using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

// "Evolution": accent colour per widget, the temperature (or main value) as the big number with the sensor
// it comes from underneath, thin bars for the other readings and every value exactly once.
public sealed class EvolutionTile : SummaryTile
{
	private readonly Border dot = new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(3), VerticalAlignment = VerticalAlignment.Center };

	private readonly TextBlock heading;

	private readonly TextBlock device;

	private readonly TextBlock big;

	private readonly TextBlock bigUnit;

	private readonly TextBlock sensor;

	private readonly TextBlock second;

	private readonly TextBlock secondLabel;

	private readonly CoreBars cores = new CoreBars { Height = 20, Width = 112, HorizontalAlignment = HorizontalAlignment.Right };

	private readonly AreaChart spark = new AreaChart { Height = 22, Width = 112, FillOpacity = 0.22, LineWidth = 1.6, HorizontalAlignment = HorizontalAlignment.Right };

	private readonly FillBack heroBar;

	private readonly Grid hero;

	private readonly StackPanel side;

	private readonly ResponsiveGrid net = new ResponsiveGrid { ColumnDefinitions = ColumnDefinitions.Parse("*,14,*") };

	private readonly (TextBlock Value, TextBlock Unit, FillBack Bar, AreaChart Chart)[] lanes = new (TextBlock, TextBlock, FillBack, AreaChart)[2];

	private readonly Grid rows = new ResponsiveGrid { ColumnDefinitions = ColumnDefinitions.Parse("Auto,*,Auto") };

	private readonly List<(TextBlock Label, FillBack Bar, TextBlock Value)> rowItems = new();

	private readonly TextBlock caption;

	// Integrated graphics that idle: one slim line instead of a full card.
	private readonly TextBlock slim;

	// Click on the card: a larger chart of the last ten minutes.
	private readonly StackPanel detail;

	private readonly AreaChart longChart = new AreaChart { Height = 64, FillOpacity = 0.2, LineWidth = 1.8 };

	// Drawn over the first chart and as tall as it, so the sidebar grows one chart, not two.
	private readonly AreaChart longChart2 = new AreaChart { FillOpacity = 0.0, LineWidth = 1.4 };

	private readonly TextBlock detailText;

	private static readonly HashSet<string> expanded = new();

	private readonly Dictionary<string, double> laneWidth = new();

	private static readonly Dictionary<string, double> fanPeaks = new();

	private IBrush? normalBackground;

	private bool compact;

	public EvolutionTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s)
		: base(kind, index, p, m, s, half: false)
	{
		Padding = new Thickness(14, 10, 14, 10);
		heading = Ui.Text("", 13, p.Ink, FontWeight.DemiBold);
		heading.Margin = new Thickness(8, 0, 8, 0);
		device = Ui.Text("", 11, p.Soft);
        HardwareWidgetTitle.Register(this, heading, device);
		device.HorizontalAlignment = HorizontalAlignment.Right;
		device.MaxWidth = 150;
		Grid header = Ui.Columns("Auto,*,Auto", dot, heading, device);

		double size = m.Subtitles ? 40 : 32;
		big = Ui.Text("", size, p.Ink, FontWeight.DemiBold, numbers: true);
		big.LetterSpacing = -1.5;
		big.VerticalAlignment = VerticalAlignment.Bottom;
		bigUnit = Ui.Text("", size * 0.38, p.Soft, FontWeight.Medium);
		bigUnit.VerticalAlignment = VerticalAlignment.Bottom;
		bigUnit.Margin = new Thickness(3, 0, 0, size * 0.1);
		sensor = Ui.Text("", 10, p.Soft);
		sensor.Margin = new Thickness(0, 3, 0, 0);
		StackPanel left = Ui.Row(0, big, bigUnit);
		left.VerticalAlignment = VerticalAlignment.Bottom;

		second = Ui.Text("", 22, p.Ink, FontWeight.DemiBold, numbers: true);
		second.VerticalAlignment = VerticalAlignment.Bottom;
		secondLabel = Ui.Text("", 11.5, p.Soft);
		secondLabel.VerticalAlignment = VerticalAlignment.Bottom;
		secondLabel.Margin = new Thickness(3, 0, 0, 3);
		StackPanel secondRow = Ui.Row(0, second, secondLabel);
		secondRow.HorizontalAlignment = HorizontalAlignment.Right;
		heroBar = new FillBack { Height = 8, Radius = 4, Track = Track(p), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
		side = Ui.Stack(5, secondRow, cores, spark);
		side.VerticalAlignment = VerticalAlignment.Bottom;
		side.HorizontalAlignment = HorizontalAlignment.Right;
		hero = Ui.Columns("Auto,*", left, new Panel { Children = { side, heroBar } });
		// The sensor line runs under the whole row, so a long sensor name never runs into the bars on the right.
		hero.RowDefinitions = RowDefinitions.Parse("Auto,Auto");
		Grid.SetRow(sensor, 1);
		Grid.SetColumnSpan(sensor, 2);
		hero.Children.Add(sensor);

		for (int i = 0; i < 2; i++)
		{
			TextBlock arrow = Ui.Text(i == 0 ? "↓" : "↑", 11, p.Soft);
			arrow.VerticalAlignment = VerticalAlignment.Bottom;
			arrow.Margin = new Thickness(0, 0, 4, 3);
			TextBlock value = Ui.Text("", 22, p.Ink, FontWeight.DemiBold, numbers: true);
			value.VerticalAlignment = VerticalAlignment.Bottom;
			TextBlock unit = Ui.Text("", 11, p.Soft);
			unit.VerticalAlignment = VerticalAlignment.Bottom;
			unit.Margin = new Thickness(4, 0, 0, 3);
			FillBack bar = new FillBack { Height = 5, Radius = 2.5, Track = Track(p) };
			AreaChart chart = new AreaChart { Height = 20, FillOpacity = 0.18, LineWidth = 1.6 };
			StackPanel lane = Ui.Stack(5, Ui.Row(0, arrow, value, unit), bar, chart);
			Grid.SetColumn(lane, i * 2);
			net.Children.Add(lane);
			lanes[i] = (value, unit, bar, chart);
		}

		rows.RowSpacing = 6;
		rows.ColumnSpacing = 8;
		caption = Ui.Text("", 11.5, p.Soft);
		Extras.Margin = new Thickness(0, 2, 0, 0);
		slim = Ui.Text("", 12.5, p.Ink, FontWeight.Medium);
		slim.IsVisible = false;
		detailText = Ui.Text("", 10.5, p.Soft);
		detail = Ui.Stack(3, new Panel { Children = { longChart, longChart2 } }, detailText);
		detail.IsVisible = false;
		StackPanel root = Ui.Stack(7, header, slim, hero, net, rows, caption, Extras, detail);
		root.Tag = "elastic";
		Child = root;
		Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);
		ToolTip.SetTip(this, L.T("Click for the last 10 minutes"));
		PointerReleased += (_, e) =>
		{
			if (e.InitialPressMouseButton != Avalonia.Input.MouseButton.Left || (e.KeyModifiers & Avalonia.Input.KeyModifiers.Control) != 0)
			{
				return;
			}
			if (!expanded.Add(Key))
			{
				expanded.Remove(Key);
			}
			ShowDetail();
			e.Handled = true;
		};
	}

	private string Key => Kind + "#" + Index;

	public static void Collapse() => expanded.Clear();

	private static Color Track(Palette p) => Palette.Mix(p.Card, p.Ink, p.Dark ? 0.09 : 0.08);

	private WidgetSlot? Own => Slot ?? S.ActiveWidget;

	protected override bool ShowsEmpty => false;

	protected override void Show(Summary sum)
	{
		var (snap, history) = Last;
		Color accent = Accents.For(Kind, Own, P);
		bool warn = Warning(snap);
		Warn(warn);
		dot.Background = Palette.Brush(warn ? P.Ramp[^1] : accent);
		Record(sum, snap);
		ShowDetail();
		heading.Text = Naming.Title(Own, Index, sum.Title);
		heading.IsVisible = heading.Text.Length > 0;
		device.Text = Naming.Hardware(Own, Index, sum.Name);
		device.IsVisible = Naming.ShowHardware(Own, S) && device.Text.Length > 0;

		bool network = Kind == WidgetKind.Network;
		net.IsVisible = network;
		hero.IsVisible = !network && Kind != WidgetKind.Drives && (sum.Value.Length > 0 && sum.Value != "—" || Kind is WidgetKind.Cpu or WidgetKind.Gpu);
		List<Reading> readings = Readings.For(Kind, Index, snap, history, S, P);

		GpuInfo? idleGpu = Kind == WidgetKind.Gpu ? GpuGroup.Visible(snap, S).ElementAtOrDefault(Index) : null;
		compact = idleGpu != null && idleGpu.Integrated && (idleGpu.Load ?? 0) < 20 && GpuGroup.Visible(snap, S).Count() > 1;
		slim.IsVisible = compact;
		if (compact)
		{
			var parts = new List<string> { (sum.Value + " " + sum.Unit).Trim() };
			if (idleGpu!.Load is double l) parts.Add(Format.Percent(l) + " " + L.T("Load"));
			if (WidgetSensors.Vram(idleGpu, S) is { Length: > 0 } vram && idleGpu.MemoryTotalMb > 0) parts.Add(vram);
			slim.Text = string.Join("  ·  ", parts);
			hero.IsVisible = false;
			ShowRows(new List<Reading>(), accent);
			caption.IsVisible = false;
			return;
		}

		if (network)
		{
			ShowNetwork(snap, history, accent);
			ShowRows(new List<Reading>(), accent);
			caption.IsVisible = false;
			return;
		}

		big.Text = sum.Value;

		BindPrimary(big);
		// The big number keeps its widest width (right-aligned), so the unit and the bar beside it stay in
		// place when it goes from "35" to "9".
		big.TextAlignment = TextAlignment.Right;
		big.MinWidth = 0;
		big.Measure(Size.Infinity);
		string bkey = "big|" + big.FontSize.ToString("0.0");
		laneWidth[bkey] = Math.Max(laneWidth.GetValueOrDefault(bkey), Math.Ceiling(big.DesiredSize.Width));
		big.MinWidth = laneWidth[bkey];
		big.Foreground = Palette.Brush(Heats(Kind) ? Shade(sum) : accent);
		bigUnit.Text = sum.Value == "—" ? "" : sum.Unit;
		// Drives and cooling name what the big number is (for example the board); no sensor names.
		sensor.Text = sum.HeroLabel;
		sensor.IsVisible = sensor.Text.Length > 0;

		// Right of the big number: the load for processors and graphics, a level bar for memory and the like.
		bool load = Kind is WidgetKind.Cpu or WidgetKind.Gpu && sum.Second.HasValue && S.Shows(WidgetSensors.SecondaryKey(Kind));
		second.Text = load ? Format.Number(sum.Second * 100.0) : ""; BindSecondary(second);
		second.Foreground = Palette.Brush(accent);
		secondLabel.Text = load ? "% " + L.T("Load") : "";
		if (Kind == WidgetKind.Clock && S.Shows("clock.week"))
		{
			// The clock has room on the right: calendar week.
			load = true;
			second.Text = L.T("Week") + " " + System.Globalization.ISOWeek.GetWeekOfYear(snap.Time);
			secondLabel.Text = "";
		}
		((Control)second.Parent!).IsVisible = load;
		double[] coreLoads = Kind == WidgetKind.Cpu ? (snap.Cpu?.CoreLoads ?? Array.Empty<double>()).ToArray() : Array.Empty<double>();
		cores.IsVisible = coreLoads.Length > 1;
		if (cores.IsVisible)
		{
			cores.Set(coreLoads.ToArray(), accent, Palette.Alpha(P.Ramp[^1], 1));
		}
		bool sparkline = !cores.IsVisible && Kind is not WidgetKind.Cpu and not WidgetKind.Gpu and not WidgetKind.Memory && sum.Series.Length > 1 && (Summaries.ChartKey(Kind) is not string chart || S.Shows(chart));
		spark.IsVisible = sparkline;
		if (sparkline)
		{
			spark.Max = sum.SeriesMax;
			spark.Set(sum.Series, accent);
		}
		bool level = Kind is WidgetKind.Memory or WidgetKind.Volumes or WidgetKind.Battery;
		heroBar.IsVisible = level;
		if (level)
		{
			heroBar.Animate(sum.Fraction, accent);
		}
		side.IsVisible = load || cores.IsVisible || sparkline;

		ShowRows(RowsFor(sum, readings, snap), accent);
		bool hardware = Kind is WidgetKind.Cpu or WidgetKind.Gpu or WidgetKind.Memory or WidgetKind.Drives or WidgetKind.Cooling or WidgetKind.Volumes;
		caption.Text = hardware ? "" : sum.Caption;
		caption.IsVisible = caption.Text.Length > 0 && caption.Text != sum.Value;
	}

	private static bool Heats(WidgetKind kind) => kind is WidgetKind.Cpu or WidgetKind.Gpu or WidgetKind.Drives or WidgetKind.Cooling;

	// Readings that are not already in the big number or next to it.
	private List<Reading> RowsFor(Summary sum, List<Reading> readings, Snapshot snap)
	{
		string heroText = (sum.Value + " " + sum.Unit).Trim();
		switch (Kind)
		{
		case WidgetKind.Cpu:
			return readings.Where(r => r.Label != L.T("Temp") && r.Label != L.T("Load")).ToList();
		case WidgetKind.Gpu:
		{
			List<Reading> list = readings.Where(r => r.Label != L.T("Temp") && r.Label != L.T("Load")).ToList();
			GpuInfo? gpu = GpuGroup.Visible(snap, S).ElementAtOrDefault(Index);
			if (gpu?.MemoryUsedMb is double used && gpu.MemoryTotalMb is double total && total > 0 && (S.Shows("gpu.vram.used") || S.Shows("gpu.vram.total")))
			{
				Color c = Accents.For(Kind, Own, P);
				list.Add(new Reading("VRAM", WidgetSensors.Vram(gpu, S), Math.Clamp(used / total, 0, 1), c));
			}
			return list;
		}
		case WidgetKind.Memory:
			return new List<Reading>();
		case WidgetKind.Drives:
			// All drives side by side, each with its temperature; none of them is special.
			return readings.ToList();
		case WidgetKind.Cooling:
			// Fans when there are any, in their own order (a fan that speeds up does not swap places with
			// another); a stopped fan reads "off". Each bar is measured against
			// the highest speed that fan has reached, so a slow case fan is not a short stub next to the pump.
			if (snap.Fans.Count > 0)
			{
				Color accent = Accents.For(Kind, Own, P);
				return snap.Fans.Take(8).Select(f =>
				{
					string name = Names.Fan(f.Name, S);
					double peak = Math.Max(1500, Math.Max(f.Rpm, fanPeaks.GetValueOrDefault(f.Name)));
					fanPeaks[f.Name] = peak;
					if (f.Rpm <= 0)
					{
						return new Reading(name, L.T("Off"), 0, P.Faint) { Key = "fan:" + f.Name };
					}
					double fraction = f.Percent is double pc ? Math.Clamp(pc / 100.0, 0, 1) : Math.Clamp(f.Rpm / peak, 0, 1);
					return new Reading(name, Format.Rpm(f.Rpm), fraction, accent) { Key = "fan:" + f.Name };
				}).ToList();
			}
			return readings.Where(r => r.Value != heroText).ToList();
		default:
		{
			List<Reading> list = readings.Where(r => r.Label.Length > 0 && r.Label != L.T("Load")).ToList();
			int skip = list.FindIndex(r => r.Value == heroText);
			if (skip >= 0)
			{
				list.RemoveAt(skip);
			}
			return list;
		}
		}
	}

	protected override List<(string Label, string Value)> AdjustExtras(List<(string Label, string Value)> items)
	{
		if (compact)
		{
			return new List<(string Label, string Value)>();
		}
		var snap = Last.Snap;
		// The CPU fan is usually also one of the fans of the cooling widget; show it there only.
		if (Kind == WidgetKind.Cpu && snap.Cpu?.FanRpm is double rpm && S.Widgets.Any(w => w.Kind == WidgetKind.Cooling && w.Visible)
			&& snap.Fans.Any(f => Math.Abs(f.Rpm - rpm) < 1))
		{
			items = items.Where(i => i.Label != L.T("Fan")).ToList();
			double[] loads = Last.History.Read("cpu.load");
			// It takes the place of the fan, so it follows the fan and load switches.
			if (S.Shows("cpu.fan") && S.Shows("cpu.load") && loads.Length > 0 && loads.Where(double.IsFinite).DefaultIfEmpty().Max() is double peak)
			{
				items.Add((L.T("2-min peak"), Format.Percent(peak)));
			}
		}
		return items;
	}

	// The hottest temperature this card is about, compared with the warning limit.
	private bool Warning(Snapshot snap)
	{
		double limit = S.TemperatureWarning;
		double Hot(IEnumerable<double?> values) => values.Where(v => v.HasValue).Select(v => v!.Value).DefaultIfEmpty(0).Max();
		switch (Kind)
		{
		case WidgetKind.Cpu:
			return Hot(new[] { snap.Cpu?.Temperature }) >= limit;
		case WidgetKind.Gpu:
			// The hot spot runs about ten degrees above the core by design.
			return GpuGroup.Visible(snap, S).ElementAtOrDefault(Index) is GpuInfo g && (Hot(new[] { g.Temperature }) >= limit || Hot(new[] { g.HotSpot }) >= limit + 10);
		case WidgetKind.Drives:
			// SSDs start to slow down around 70 °C.
			return Hot(snap.Disks.Where(d => S.Shows("drive:" + d.Id)).Select(d => d.Temperature)) >= Math.Min(70, limit);
		case WidgetKind.Cooling:
			return Hot(snap.BoardTemps.Select(t => (double?)t.Celsius)) >= limit;
		default:
			return false;
		}
	}

	private Thickness? normalPadding;

	private void Warn(bool on)
	{
		normalBackground ??= Background;
		normalPadding ??= Padding;
		Color red = P.Ramp[^1];
		Background = on ? Palette.Brush(Palette.Mix(((normalBackground as ISolidColorBrush)?.Color ?? P.Card), red, P.Dark ? 0.16 : 0.10)) : normalBackground;
		BorderBrush = Palette.Brush(on ? red : P.Stroke);
		// The thicker red edge is drawn as an inner shadow: border and padding stay exactly the same, so
		// neither the content nor the card's height change when the card turns red (changing them made the
		// sidebar jump by a few pixels).
		BoxShadow = on ? BoxShadows.Parse($"inset 0 0 0 1.5 #{red.A:X2}{red.R:X2}{red.G:X2}{red.B:X2}") : default;
	}

	private string[] LongKeys => Kind == WidgetKind.Network ? new[] { Key + ".down", Key + ".up" } : new[] { Key };

	private void Record(Summary sum, Snapshot snap)
	{
		double? value = Kind switch
		{
			WidgetKind.Cpu => snap.Cpu?.Temperature,
			WidgetKind.Gpu => GpuGroup.Visible(snap, S).ElementAtOrDefault(Index)?.Temperature,
			WidgetKind.Memory => snap.Memory?.Load,
			WidgetKind.Drives => snap.Disks.Where(d => d.Temperature.HasValue).Select(d => d.Temperature).DefaultIfEmpty().Max(),
			WidgetKind.Network => snap.Network?.DownloadBytesPerSecond,
			_ => double.TryParse(sum.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double x) ? x : null
		};
		LongHistory.Add(LongKeys[0], snap.Time, value);
		if (Kind == WidgetKind.Network)
		{
			LongHistory.Add(LongKeys[1], snap.Time, snap.Network?.UploadBytesPerSecond);
		}
	}

	private void ShowDetail()
	{
		bool open = expanded.Contains(Key) && !compact;
		detail.IsVisible = open;
		if (!open)
		{
			return;
		}
		Color accent = Accents.For(Kind, Own, P);
		double[] a = LongHistory.Read(LongKeys[0]);
		longChart.Max = Kind == WidgetKind.Memory ? 100 : null;
		longChart.Set(a, accent);
		longChart2.IsVisible = Kind == WidgetKind.Network;
		double[] b = Kind == WidgetKind.Network ? LongHistory.Read(LongKeys[1]) : Array.Empty<double>();
		if (longChart2.IsVisible)
		{
			longChart.Max = longChart2.Max = Math.Max(1, a.Concat(b).Where(double.IsFinite).DefaultIfEmpty(1).Max() * 1.15);
			longChart2.Set(b, Accents.Readable(Color.Parse(Accents.Upload), P));
		}
		var finite = a.Where(double.IsFinite).ToArray();
		string Text(double v) => Kind switch
		{
			WidgetKind.Network => Format.Rate(v),
			WidgetKind.Memory => Format.Percent(v),
			WidgetKind.Cpu or WidgetKind.Gpu or WidgetKind.Drives or WidgetKind.Cooling => Format.Temperature(v, S.Fahrenheit) + " " + Format.TemperatureUnit(S.Fahrenheit),
			_ => Format.Number(v, "0.#")
		};
		detailText.Text = finite.Length == 0 ? L.T("10 minutes") : L.T("10 minutes") + " · " + L.T("min.") + " " + Text(finite.Min()) + " · " + L.T("max.") + " " + Text(finite.Max());
	}

	private void ShowRows(List<Reading> list, Color accent)
	{
		if (rowItems.Count != list.Count)
		{
			rows.Children.Clear();
			rows.RowDefinitions.Clear();
			rowItems.Clear();
			for (int i = 0; i < list.Count; i++)
			{
				rows.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
				TextBlock label = Ui.Text("", 11.5, P.Ink);
				label.Opacity = 0.8;
				label.MaxWidth = 96;
				FillBack bar = new FillBack { Height = 5, Radius = 2.5, Track = Track(P), VerticalAlignment = VerticalAlignment.Center };
				TextBlock value = Ui.Text("", 12.5, P.Ink, FontWeight.DemiBold, numbers: true);
				value.HorizontalAlignment = HorizontalAlignment.Right;
				value.MinWidth = 44;
				Grid.SetRow(label, i);
				Grid.SetRow(bar, i);
				Grid.SetColumn(bar, 1);
				Grid.SetRow(value, i);
				Grid.SetColumn(value, 2);
				rows.Children.Add(label);
				rows.Children.Add(bar);
				rows.Children.Add(value);
				rowItems.Add((label, bar, value));
			}
		}
		rows.IsVisible = list.Count > 0;
		for (int i = 0; i < list.Count; i++)
		{
			Reading r = list[i];
			bool heat = r.Value.EndsWith("°C") || r.Value.EndsWith("°F");
			Color fill = heat ? Opaque(r.Color) : accent;
			rowItems[i].Label.Text = r.Label;
			rowItems[i].Value.Text = r.Value;
            BindReading(rowItems[i].Value, r);
			rowItems[i].Value.Foreground = Palette.Brush(heat ? fill : P.Ink);
			rowItems[i].Bar.Animate(r.Fraction, fill);
		}
	}

	private void ShowNetwork(Snapshot snap, History history, Color accent)
	{
        net.RestoreForUpdate();
		NetworkInfo? n = snap.Network;
		double? link = n?.LinkBitsPerSecond;
		Color up = Kind == WidgetKind.Network && (Own?.Accent.Length ?? 0) == 0 ? Accents.Readable(Color.Parse(Accents.Upload), P) : Palette.Mix(accent, P.Ink, 0.35);
		bool showDown = S.Shows("net.down");
		bool showUp = S.Shows("net.up");
		(double? Rate, string Key, Color Color, bool Show)[] dirs =
		{
			(n?.DownloadBytesPerSecond, "net.down", accent, showDown),
			(n?.UploadBytesPerSecond, "net.up", up, showUp)
		};
		for (int i = 0; i < 2; i++)
		{
			var (rate, key, color, show) = dirs[i];
			var lane = lanes[i];
			((Control)lane.Value.Parent!.Parent!).IsVisible = show;
			var (number, unit) = Format.Split(Format.Rate(rate));
			lane.Value.Text = number;
			SensorDetails.Value(lane.Value, key);
			lane.Value.Foreground = Palette.Brush(color);
			// The number keeps the widest width it has had and stands right-aligned in it, so the unit
			// beside it stays in place when the rate changes from "292" to "1.9".
			lane.Value.TextAlignment = TextAlignment.Right;
			lane.Value.MinWidth = 0;
			lane.Value.Measure(Size.Infinity);
			string wkey = key + "|" + lane.Value.FontSize.ToString("0.0");
			laneWidth[wkey] = Math.Max(laneWidth.GetValueOrDefault(wkey), Math.Ceiling(lane.Value.DesiredSize.Width));
			lane.Value.MinWidth = laneWidth[wkey];
			lane.Unit.Text = unit;
			lane.Bar.Animate(Summaries.NetFill(rate, link), color);
			double[] series = history.Read(key);
			lane.Chart.IsVisible = series.Length > 1;
			lane.Chart.Set(series, color);
		}
		net.ColumnDefinitions[0].Width = showDown ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
		net.ColumnDefinitions[1].Width = showDown && showUp ? new GridLength(14) : new GridLength(0);
		net.ColumnDefinitions[2].Width = showUp ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
		net.IsVisible = showDown || showUp;
	}

	private static Color Opaque(Color c) => Color.FromArgb(255, c.R, c.G, c.B);
}

// Small vertical bars, one per core; a core under heavy load turns into the warm colour.
public sealed class CoreBars : Control
{
	private double[] values = Array.Empty<double>();

	private Color color = Colors.DeepSkyBlue;

	private Color hot = Colors.Orange;

	public void Set(double[] loads, Color normal, Color warm)
	{
		values = loads;
		color = normal;
		hot = warm;
		InvalidateVisual();
	}

	public override void Render(DrawingContext context)
	{
		Rect b = Bounds;
		if (values.Length == 0 || b.Width < 4 || b.Height < 4)
		{
			return;
		}
		double gap = Math.Min(2, b.Width / (values.Length * 3.0));
		double w = (b.Width - gap * (values.Length - 1)) / values.Length;
		for (int i = 0; i < values.Length; i++)
		{
			double v = double.IsFinite(values[i]) ? Math.Clamp(values[i] / 100.0, 0, 1) : 0;
			double h = Math.Max(2, v * b.Height);
			var rect = new Rect(i * (w + gap), b.Height - h, w, h);
			context.DrawRectangle(Palette.Brush(v >= 0.85 ? hot : color), null, rect, 2, 2);
		}
	}
}
