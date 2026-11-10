using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

// "Dial": a round gauge with the temperature (or main value), small tiles for the other
// readings (value and a short curve) and a detail strip at the bottom: load per thread, video
// memory, the other drives or fans. The network card shows both directions with a chart and the connection.
public sealed class DialTile : SummaryTile
{
	private sealed record Spec(string Key, string Label, string Value, double[]? Series, double? Fraction, Color? Color);

	private readonly TextBlock chip;

	private readonly Border chipBox;

	private readonly TextBlock device;

	private readonly Grid hero;

	private readonly DialArc dial = new DialArc();

	private readonly Panel dialPanel;

	private readonly Viewbox dialBox = new Viewbox { Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 190 };

	private readonly TextBlock dialValue;

	private readonly TextBlock dialLabel;

	private readonly UniformGrid grid = new UniformGrid { Columns = 2, VerticalAlignment = VerticalAlignment.Top };

	private readonly List<TileView> tiles = new();

	private readonly StackPanel bottom = new StackPanel { Spacing = 5 };

	// Tiles that do not fit next to the gauge in a narrow card, in three columns below it.
	private readonly UniformGrid more = new UniformGrid { Columns = 3 };

	// Drive switcher in the header: "‹ 1/3 ›".
	private readonly TextBlock pager;

	private readonly Border pagerBox;

	private static readonly Dictionary<int, int> drivePage = new();

	private readonly StackPanel network = new StackPanel { Spacing = 6 };

	private readonly DriveRows drives;
    private readonly DesignValueFaces driveFaces = new();

	public DialTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s)
		: base(kind, index, p, m, s, half: false)
	{
		Padding = new Thickness(12, 10, 12, 12);
		chip = Ui.Text("", 10, p.Ink, FontWeight.Bold);
		chipBox = new Border { CornerRadius = new CornerRadius(5), Padding = new Thickness(6, 2), Child = chip, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
		device = Ui.Text("", 13, p.Ink, FontWeight.DemiBold);
        HardwareWidgetTitle.Register(this, device);
		pager = Ui.Text("", 11, p.Soft, FontWeight.Medium, numbers: true);
		pagerBox = new Border { Child = pager, Padding = new Thickness(8, 2), CornerRadius = new CornerRadius(6), Background = Brushes.Transparent, IsVisible = false, Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand) };
		ToolTip.SetTip(pagerBox, L.T("Next drive"));
		pagerBox.PointerReleased += (_, e) =>
		{
			drivePage[Index] = drivePage.GetValueOrDefault(Index) + 1;
			Update(Last.Snap, Last.History);
			e.Handled = true;
		};
		Grid header = Ui.Columns("Auto,*,Auto", chipBox, device, pagerBox);

		dialValue = Ui.Text("", 40, p.Ink, FontWeight.DemiBold, numbers: true);
		dialValue.LetterSpacing = -1;
		dialValue.HorizontalAlignment = HorizontalAlignment.Center;
		dialLabel = Ui.Text("", 10, p.Soft, FontWeight.Medium);
		dialLabel.HorizontalAlignment = HorizontalAlignment.Center;
		StackPanel center = Ui.Stack(0, dialValue, dialLabel);
		center.VerticalAlignment = VerticalAlignment.Center;
		center.HorizontalAlignment = HorizontalAlignment.Center;
		center.Margin = new Thickness(0, 6, 0, 0);
		// Like a dashboard panel: the gauge on the left takes two fifths of the width, the reading tiles two by
		// two on the right. The gauge is drawn at one size and scaled with its column, so it keeps its
		// proportions; the size follows from the width alone and never feeds back into the layout.
		dialPanel = new Panel { Width = 150, Height = 150, Children = { dial, center } };
		dial.Width = 150;
		dial.Height = 150;
		dialBox.Child = dialPanel;
		dialBox.Margin = new Thickness(0, 0, 8, 0);
		// "Graphic size" also widens the gauge's share of the row.
		double g = Math.Clamp(s.ActiveWidget?.Graphics ?? 1, 0.5, 2);
		hero = new Grid { ColumnDefinitions = new ColumnDefinitions { new ColumnDefinition(2 * g, GridUnitType.Star), new ColumnDefinition(3, GridUnitType.Star) } };
		hero.Children.Add(dialBox);
		Grid.SetColumn(grid, 1);
		hero.Children.Add(grid);
		grid.Margin = new Thickness(0, 0, -6, 0);

		more.Margin = new Thickness(0, -4, -6, 0);
		// Drives: every drive as a line (letter, bar, temperature) at the top; the tiles below belong to the
		// drive named in the switcher.
		drives = new DriveRows(9, 11.5, 13, p.Soft, () => new FillBack { Height = 6, Radius = 3, Track = TileBack },
			(c, r) => ((FillBack)c).Animate(r.Fraction, r.Color), r => r.Color) { Tag = null };
		// Free height goes between the parts below the header, never between the header and the gauge:
		// a widget with only a gauge (memory) then keeps it at the top like the others, with the space
		// below, instead of pushing it to the bottom of the card.
		StackPanel body = Ui.Stack(10, drives, driveFaces, hero, more, bottom, network);
		body.Tag = "elastic";
		StackPanel root = Ui.Stack(10, header, body);
		Child = root;
	}

	public static bool Fits(WidgetKind kind) => kind is WidgetKind.Cpu or WidgetKind.Gpu or WidgetKind.Memory or WidgetKind.Drives or WidgetKind.Network
		or WidgetKind.Cooling or WidgetKind.Power or WidgetKind.Fps or WidgetKind.Ping or WidgetKind.Battery or WidgetKind.Volumes or WidgetKind.Processes;

	private WidgetSlot? Own => Slot ?? S.ActiveWidget;

	// Colours of the reference dashboard: near-black card, dark tiles, blue curves for the processor, teal
	// for graphics, cyan and violet for download and upload; temperatures blue (cool), green, amber, red.
	private Color Tint => Own?.Accent.Length > 0 ? Accents.For(Kind, Own, P) : Accents.Design(Kind switch
	{
		WidgetKind.Cpu => "#5b7cfa",
		WidgetKind.Gpu => "#1cb5c9",
		WidgetKind.Network => "#3ec6f0",
		WidgetKind.Memory => "#5b7cfa",
		WidgetKind.Drives => "#9aa0a6",
		_ => Accents.Default(Kind)
	}, P);

	private Color ChipColor => Accents.Design("#29b6d8", P);

	private Color CardBack => P.DesignBack("#111213");

	private Color TrackColor => P.Dark ? Color.Parse("#2a2b2d") : Palette.Mix(P.Card, P.Ink, 0.1);

	private Color TileBack => P.DesignTile("#1a1b1d");

	private Color Heat(double? celsius) => celsius is double c ? Accents.Readable(Color.Parse(c < 45 ? "#33b5f5" : c < 80 ? "#3ddc97" : c < 90 ? "#f5b33b" : "#f2555a"), P) : P.Ink;

	private static Color Opaque(Color c) => Color.FromArgb(255, c.R, c.G, c.B);

	private string Deg(double v) => Format.Temperature(v, S.Fahrenheit) + "°";

	// Records a value for the short curve; the key is per card so two GPUs keep apart.
	private string Track(string key, DateTime time, double? value)
	{
		string full = Kind + "#" + Index + ":" + key;
		DayStats.Add(full, time, value);
		return full;
	}

	protected override void Show(Summary sum)
	{
		var (snap, history) = Last;
		Color tint = Tint;
		chip.Text = Naming.Title(Own, Index, string.IsNullOrWhiteSpace(Own?.Name) ? Chip(Kind) : sum.Title);
		chip.Foreground = Palette.Brush(ChipColor);
		chipBox.Background = Palette.Brush(P.Dark && P.Chosen == null ? Color.Parse("#0e2f3a") : Color.FromArgb(40, ChipColor.R, ChipColor.G, ChipColor.B));
		Background = Palette.Brush(CardBack);
		string name = Naming.Hardware(Own, Index, sum.Name);
		string title = Naming.Title(Own, Index, sum.Title);
		device.Text = Naming.ShowHardware(Own, S) && name.Length > 0 ? name : title;

		drives.IsVisible = false;
        driveFaces.IsVisible = false;
        if (Kind == WidgetKind.Drives)
        {
            drives.Show(DriveRows.Of(Index, snap, history, S, P)); drives.IsVisible = false;
            hero.IsVisible = more.IsVisible = bottom.IsVisible = network.IsVisible = pagerBox.IsVisible = false;
            bottom.Children.Clear(); grid.Children.Clear(); more.Children.Clear();
            driveFaces.Show(WidgetStyle.Dial, DesignValueFaces.Readings(Kind, Index, snap, history, S, P), P);
            return;
        }
		bool net = Kind == WidgetKind.Network;
		network.IsVisible = net;
		hero.IsVisible = !net;
		if (net)
		{
			grid.IsVisible = false;
			bottom.IsVisible = false;
			ShowNetwork(snap, history, tint);
			return;
		}
		var specs = new List<Spec?>();
		bottom.Children.Clear();
		switch (Kind)
		{
		case WidgetKind.Cpu:
			Cpu(snap, specs);
			break;
		case WidgetKind.Gpu:
			Gpu(snap, specs);
			break;
		case WidgetKind.Memory:
			Memory(snap, history, specs);
			break;
		case WidgetKind.Drives:
			Drives(snap, specs);
			break;
		case WidgetKind.Cooling:
			Cooling(snap, specs);
			break;
		default:
			Generic(sum, snap, history, specs);
			break;
		}
		// The first two tiles stand next to the dial, the others in two columns below.
		Fill(specs);
		bottom.IsVisible = bottom.Children.Count > 0;
	}

	private static string Chip(WidgetKind kind) => kind switch
	{
		WidgetKind.Cpu => "CPU",
		WidgetKind.Gpu => "GPU",
		WidgetKind.Memory => "RAM",
		WidgetKind.Drives => "SSD",
		WidgetKind.Network => "NET",
		WidgetKind.Cooling => "FAN",
		WidgetKind.Power => "PWR",
		WidgetKind.Battery => "BAT",
		WidgetKind.Fps => "FPS",
		WidgetKind.Ping => "PING",
		WidgetKind.Volumes => "DISK",
		WidgetKind.DataUsage => "DATA",
		WidgetKind.System => "SYS",
		_ => Cards.Name(kind).ToUpperInvariant().Substring(0, Math.Min(4, Cards.Name(kind).Length))
	};

	private void SetDial(double? value, string text, string label, double fraction, Color color)
	{
		// A hidden or missing main value leaves out the whole gauge rather than showing an empty ring.
		dialBox.IsVisible = value.HasValue;
		dialValue.Text = value.HasValue ? text : "—";
		dialValue.Foreground = Palette.Brush(value.HasValue ? color : P.Soft);
		dialLabel.Text = label.ToUpper(L.Culture);

		dial.Set(value.HasValue ? Math.Clamp(fraction, 0, 1) : 0, color, TrackColor);
	}

	private void TempDial(string key, double? celsius, DateTime time, string label)
	{
		string k = Track(key, time, celsius);
		SetDial(celsius, celsius is double c ? Deg(c) : "", label, celsius is double t ? (t - 20) / 80.0 : 0, Heat(celsius));
	}

	private Spec? Value(string key, string label, double? value, DateTime time, Func<double, string> text, Color? color = null)
	{
		string k = Track(key, time, value);
		if (value is not double v)
		{
			return null;
		}
		return new Spec(key, label, text(v), DayStats.Series(k), null, color);
	}

	private void Cpu(Snapshot snap, List<Spec?> specs)
	{
		CpuInfo? cpu = snap.Cpu;
		TempDial("temp", S.Shows("cpu.temp") ? cpu?.Temperature : null, snap.Time, L.T("Temperature"));
		if (S.Shows("cpu.load")) specs.Add(Value("load", L.T("Load"), cpu?.Load, snap.Time, v => Format.Percent(v)));
		if (S.Shows("cpu.power")) specs.Add(Value("power", L.T("Power"), cpu?.PowerW, snap.Time, v => Format.Watts(v)));
		if (S.Shows("cpu.clock")) specs.Add(Value("clock", L.T("Average clock"), cpu?.ClockMhz, snap.Time, v => Format.Clock(v)));
		if (S.Shows("cpu.voltage")) specs.Add(Value("voltage", L.T("Core voltage"), cpu?.Voltage, snap.Time, v => Format.Volts(v)));
		if (S.Shows("cpu.cores") && cpu != null && cpu.CoreLoads.Count > 1)
		{
			bottom.Children.Add(Caption(L.T("Load per thread")));
			var bars = new ThreadBars { Height = 52 };
			bars.Set(cpu.CoreLoads.ToArray(), Tint, TileBack, Opaque(P.Ramp[^1]));
			bottom.Children.Add(bars);
		}
	}

	private void Gpu(Snapshot snap, List<Spec?> specs)
	{
		GpuInfo? gpu = GpuGroup.Visible(snap, S).ElementAtOrDefault(Index);
		TempDial("temp", S.Shows("gpu.temp") ? gpu?.Temperature : null, snap.Time, L.T("Temperature"));
		if (gpu == null)
		{
			return;
		}
		if (S.Shows("gpu.load")) specs.Add(Value("load", L.T("Load"), gpu.Load, snap.Time, v => Format.Percent(v)));
		if (S.Shows("gpu.power")) specs.Add(Value("power", L.T("Power"), gpu.PowerW, snap.Time, v => Format.Watts(v)));
		if (S.Shows("gpu.hotspot")) specs.Add(Value("hotspot", L.T("Hot spot"), gpu.HotSpot, snap.Time, v => Format.Temperature(v, S.Fahrenheit) + " " + Format.TemperatureUnit(S.Fahrenheit), Heat(gpu.HotSpot)));
		// Fan as the fourth tile; a fan that stands still when idle reads "off".
		if (S.Shows("gpu.fan") && gpu.FanRpm.HasValue)
		{
			specs.Add(gpu.FanRpm > 0 ? Value("fan", L.T("Fan"), gpu.FanRpm, snap.Time, v => Format.Rpm(v)) : new Spec("fan", L.T("Fan"), L.T("Off"), null, null, P.Soft));
		}
		if (S.Shows("gpu.clock") && gpu.ClockMhz is > 0)
		{
			bottom.Children.Add(Ui.Columns("*,Auto", Caption(L.T("Core clock")), Small(Format.Clock(gpu.ClockMhz))));
		}
		if (S.Shows("gpu.junction") && gpu.MemoryJunction is double junction)
		{
			bottom.Children.Add(Ui.Columns("*,Auto", Caption(L.T("Memory junction")), Small(Format.Temperature(junction, S.Fahrenheit) + " " + Format.TemperatureUnit(S.Fahrenheit), Heat(junction))));
		}
	}

	private void Memory(Snapshot snap, History history, List<Spec?> specs)
	{
		MemoryInfo? mem = snap.Memory;
		double? load = mem?.Load;
		string k = Track("load", snap.Time, load);
		if (!S.Shows("mem.load"))
		{
			load = null;
		}
		SetDial(load, load is double l ? Format.Number(l) + "%" : "", L.T("Used"), (load ?? 0) / 100.0, Tint);
		if (mem == null)
		{
			return;
		}
		if (S.Shows("mem.used")) specs.Add(Value("used", L.T("Used"), mem.UsedGb, snap.Time, v => Format.Number(v, "0.0") + " GB"));
		if (S.Shows("mem.free")) specs.Add(Value("free", L.T("Available"), mem.AvailableGb, snap.Time, v => Format.Number(v, "0.0") + " GB"));
		if (S.Shows("mem.total")) specs.Add(new Spec("total", L.T("Installed"), Format.Number(mem.TotalGb, "0") + " GB", null, null, null));
	}

	// One drive at a time, like a drive panel; the switcher at the top right moves to the next one.
	private void Drives(Snapshot snap, List<Spec?> specs)
	{
		var disks = snap.Disks.Where(d => S.Shows("drive:" + d.Id)).ToList();
		pagerBox.IsVisible = disks.Count > 1;
		if (disks.Count == 0)
		{
			TempDial("temp", null, snap.Time, L.T("Temperature"));
			return;
		}
		// All drives the same way, coloured like the dial's temperatures; no gauge for one of them.
        // Bind the drive rows to their source IDs, including equal temperatures on different disks.
        drives.Show(disks.Select(x => new Reading(x.Letters.Length > 0 ? x.Letters : Names.Short(x.Name),
            S.Shows("drives.temp") ? Format.Temperature(x.Temperature, S.Fahrenheit) + " " + Format.TemperatureUnit(S.Fahrenheit) : "",
            Format.Heat(x.Temperature), Heat(x.Temperature)) { Key = "drive:" + x.Id + ":temp" }).ToList());
		int page = ((drivePage.GetValueOrDefault(Index) % disks.Count) + disks.Count) % disks.Count;
		DiskInfo d = disks[page];
		pager.Text = (d.Letters.Length > 0 ? d.Letters + "  " : "") + (page + 1) + "/" + disks.Count + "  ›";
        device.Text = Naming.Title(Own, Index, L.T("Drives"));
        SensorDetails.Value(device, "");
		string id = "disk:" + d.Id + ":";
		TempDial(id + "temp", null, snap.Time, L.T("Temperature"));
		Func<double, string> pct = v => Format.Number(v, "0.0") + " %";
		if (S.Shows("drives.used")) specs.Add(Value(id + "used", L.T("Used space"), d.UsedPercent, snap.Time, pct));
		if (S.Shows("drives.readactivity")) specs.Add(Value(id + "ra", L.T("Read activity"), d.ReadActivity, snap.Time, pct));
		if (S.Shows("drives.writeactivity")) specs.Add(Value(id + "wa", L.T("Write activity"), d.WriteActivity, snap.Time, pct));
		if (S.Shows("drives.totalactivity")) specs.Add(Value(id + "ta", L.T("Total activity"), d.TotalActivity, snap.Time, pct));
		var volumes = snap.Volumes.Where(v => d.Letters.Length > 0 && d.Letters.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(v.Name.TrimEnd('\\'))).ToList();
		if (volumes.Count > 0)
		{
			double free = volumes.Sum(v => v.FreeGb);
			double total = volumes.Sum(v => v.TotalGb);
			if (S.Shows("drives.free")) specs.Add(Value(id + "free", L.T("Free space"), free, snap.Time, v => Format.Size(v * 1e9)));
			if (S.Shows("drives.total")) specs.Add(new Spec(id + "total", L.T("Total space"), Format.Size(total * 1e9), null, null, null));
		}
        // Transfer values are shown once per device in the compact read/write pair below.
		if (S.Shows("drives.powercount") && d.PowerOnCount is double count) specs.Add(new Spec(id + "count", L.T("Power-on count"), Format.Number(count, "0"), null, null, null));
		if (S.Shows("drives.hours") && d.PowerOnHours is double hours) specs.Add(new Spec(id + "hours", L.T("Power-on hours"), Format.Number(hours, "0"), null, null, null));
		if (S.Shows("drives.life") && d.Life is double life) specs.Add(new Spec(id + "life", L.T("Life left"), Format.Number(life, "0") + " %", null, null, life < 20 ? Opaque(P.Ramp[^1]) : null));
	}

	private void Cooling(Snapshot snap, List<Spec?> specs)
	{
		TempReading? board = Summaries.CoolingHero(snap);
		TempDial("board", S.Shows("cooling.board") ? board?.Celsius : null, snap.Time, board != null ? Names.Temp(board.Name, S) : L.T("Temperature"));
		// Fans keep their order (not sorted by speed): a fan that speeds up or stops stays in its place.
		var fans = S.Shows("cooling.fans") ? snap.Fans.ToList() : new List<FanInfo>();
		foreach (FanInfo fan in fans.Take(4))
		{
			specs.Add(fan.Rpm > 0 ? Value("fan:" + fan.Name, Names.Fan(fan.Name, S), fan.Rpm, snap.Time, v => Format.Rpm(v)) : new Spec("fan:" + fan.Name, Names.Fan(fan.Name, S), L.T("Off"), null, null, P.Soft));
		}
		foreach (FanInfo fan in fans.Skip(4))
		{
			bottom.Children.Add(Ui.Columns("*,Auto", Small(Names.Fan(fan.Name, S), P.Ink), Small(fan.Rpm > 0 ? Format.Rpm(fan.Rpm) : L.T("Off"))));
		}
		foreach (TempReading t in snap.BoardTemps.Where(t => board == null || t.Name != board.Name))
		{
			bottom.Children.Add(Ui.Columns("*,Auto", Small(Names.Temp(t.Name, S), P.Ink), Small(Format.Temperature(t.Celsius, S.Fahrenheit) + " " + Format.TemperatureUnit(S.Fahrenheit), Heat(t.Celsius))));
		}
	}

	private void Generic(Summary sum, Snapshot snap, History history, List<Spec?> specs)
	{
		double? number = double.TryParse(sum.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double x) ? x : null;
		string k = Track("main", snap.Time, number);
		SetDial(sum.Value.Length > 0 && sum.Value != "—" ? 0 : null, sum.Value + (sum.Unit.Length <= 2 ? sum.Unit : ""), sum.Unit.Length > 2 ? sum.Unit : Naming.Title(Own, Index, sum.Title), sum.Fraction, Tint);
		foreach (var (label, value) in Summaries.Extras(Kind, Index, snap, history, S).Where(e => e.Value != "—").Take(6))
		{
			specs.Add(new Spec(label, label, value, null, null, null));
		}
	}

	private void Fill(List<Spec?> list)
	{
		var specs = list.Where(x => x != null).Cast<Spec>().ToList();
		while (tiles.Count < specs.Count)
		{
			tiles.Add(new TileView(P));
		}
		grid.Children.Clear();
		more.Children.Clear();
		for (int i = 0; i < specs.Count; i++)
		{
			TileView t = tiles[i];
			t.Show(specs[i], Tint, TileBack, P);
			t.Margin = new Thickness(0, 0, 6, 6);
			// Drives have no gauge, so the tiles run in three columns from the top.
			(i < 4 && Kind != WidgetKind.Drives ? grid : more).Children.Add(t);
		}
		grid.IsVisible = grid.Children.Count > 0;
		more.IsVisible = more.Children.Count > 0;
		hero.IsVisible = dialBox.IsVisible || grid.IsVisible;
	}

	private TextBlock Caption(string text)
	{
		TextBlock t = Ui.Text(text.ToUpper(L.Culture), 9, P.Soft, FontWeight.Medium);
		t.Margin = new Thickness(0, 2, 0, 0);
		return t;
	}

	private TextBlock Small(string text, Color? color = null) => Ui.Text(text, 11, color ?? P.Soft, FontWeight.Medium, numbers: true);

	// Network: both directions as large numbers with a shared chart, then the connection.
	private readonly AreaChart downChart = new AreaChart { Height = 96, FillOpacity = 0.18, LineWidth = 1.6 };

	// Upload is drawn over download and takes its height, so the sidebar grows one chart, not two.
	private readonly AreaChart upChart = new AreaChart { FillOpacity = 0.0, LineWidth = 1.6 };

	private readonly GridLines lines = new GridLines();

	private void ShowNetwork(Snapshot snap, History history, Color tint)
	{
		network.Children.Clear();
		NetworkInfo? n = snap.Network;
		Color up = Accents.Design("#a78bfa", P);
		bool down = S.Shows("net.down");
		bool upShown = S.Shows("net.up");
		if (down || upShown)
		{

		}
		var legend = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("*,*") };
		void Lane(int col, string label, double? rate, Color color)
		{
			var dot = new Border { Width = 9, Height = 9, CornerRadius = new CornerRadius(2.5), Background = Palette.Brush(color), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 5, 0) };
			var (number, unit) = Format.Split(Format.Rate(rate));
			var value = Ui.Row(0, Ui.Text(number, 24, P.Ink, FontWeight.DemiBold, numbers: true), Ui.Text(" " + unit, 11, P.Soft));
			((Control)value.Children[1]).VerticalAlignment = VerticalAlignment.Bottom;
			((Control)value.Children[1]).Margin = new Thickness(0, 0, 0, 3);
			var lane = Ui.Stack(1, Ui.Row(0, dot, Ui.Text(label, 11, P.Soft)), value);
			Grid.SetColumn(lane, col);
			legend.Children.Add(lane);
		}
		if (down) Lane(0, L.T("Download"), n?.DownloadBytesPerSecond, tint);
		if (upShown) Lane(down ? 1 : 0, L.T("Upload"), n?.UploadBytesPerSecond, up);
		if (down || upShown)
		{
			network.Children.Add(legend);
		}
		// The last 60 seconds, like a live network view.
		double[] a = down ? history.Read("net.down").TakeLast(60).ToArray() : Array.Empty<double>();
		double[] b = upShown ? history.Read("net.up").TakeLast(60).ToArray() : Array.Empty<double>();
		double max = Math.Max(1024, a.Concat(b).Where(double.IsFinite).DefaultIfEmpty(0).Max());
		double top = Nice(max);
		downChart.Max = top;
		upChart.Max = top;
		downChart.Set(a, tint);
		upChart.Set(b, up);
		// Scale in four steps; the labels are drawn with the dashed lines, so they stay on them when the
		// chart grows with the sidebar.
		lines.Labels = Enumerable.Range(0, 5).Select(i => i == 4 ? "0" : Format.Rate(top * (4 - i) / 4.0)).ToArray();
		downChart.Margin = upChart.Margin = new Thickness(GridLines.LabelWidth, 0, 0, 0);
		(downChart.Parent as Panel)?.Children.Remove(downChart);
		(upChart.Parent as Panel)?.Children.Remove(upChart);
		(lines.Parent as Panel)?.Children.Remove(lines);
		lines.Color = TrackColor;
		lines.Text = P.Soft;
		var charts = new Panel { Children = { lines, downChart, upChart } };
		bool chart = (down && S.Shows("net.down.history")) || (upShown && S.Shows("net.up.history"));
		if (chart && S.Shows("net.chart"))
		{
			network.Children.Add(charts);

		}
		int before = network.Children.Count;
		network.Children.Add(Caption(L.T("Connection")));
		void Row(string label, string value, Color? color = null) => network.Children.Add(Ui.Columns("*,Auto", Ui.Text(label, 12, P.Soft), Ui.Text(value, 12, color ?? P.Ink, FontWeight.DemiBold, numbers: true)));
		if (snap.Ping is PingInfo ping && ping.Ms.HasValue && S.Shows("net.quality"))
		{
			bool good = ping.LossPercent < 1 && ping.Ms < 60;
			var dot = new Border { Width = 9, Height = 9, CornerRadius = new CornerRadius(4.5), Background = Palette.Brush(good ? Color.Parse("#34d399") : Opaque(P.Ramp[^1])), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
			network.Children.Add(Ui.Row(0, dot, Ui.Text(L.T(good ? "Working well" : "Unstable"), 16, P.Ink, FontWeight.DemiBold)));
			Row(L.T("Ping"), Format.Number(ping.Ms, "0") + " ms");
			if (ping.JitterMs.HasValue) Row(L.T("Jitter"), Format.Number(ping.JitterMs, "0") + " ms");
			Row(L.T("Packet loss"), Format.Number(ping.LossPercent, "0") + " %");
		}
		if (n != null && S.Shows("net.quality"))
		{
			var (medium, vpn) = Connection.Probe(n.Name);
			if (medium.Length > 0) Row(L.T("Connected by"), L.T(medium));
			Row("VPN", L.T(vpn ? "On" : "Off"));
		}
		if (n?.LinkBitsPerSecond is double link && link > 0 && S.Shows("net.link")) Row(L.T("Link speed"), Format.Link(link));
		if (n != null && n.Name.Length > 0 && Naming.ShowHardware(Own, S)) device.Text = Naming.Hardware(Own, Index, n.Name);
		if (!string.IsNullOrEmpty(n?.Address) && S.Shows("net.address")) Row(L.T("Address"), n!.Address!);
		if (network.Children.Count == before + 1)
		{
			network.Children.RemoveAt(before);
		}
	}

	private static double Nice(double max)
	{
		double step = Math.Pow(10, Math.Floor(Math.Log10(max)));
		foreach (double f in new[] { 1, 2, 4, 5, 8, 10 })
		{
			if (f * step >= max) return f * step;
		}
		return 10 * step;
	}

	// One reading as a small tile: label, value and a short curve.
	private sealed class TileView : Border
	{
		private readonly TextBlock label;
		private readonly TextBlock value;
		// Fixed height: tiles sit next to the gauge, so a taller curve would not make the card taller and the
		// sidebar could not plan the height (cards jumping from update to update).
		private readonly AreaChart chart = new AreaChart { Height = 16, FillOpacity = 0.22, LineWidth = 1.5, Tag = "fixed" };

		private static readonly HashSet<string> hadCurve = new();

		public TileView(Palette p)
		{
			CornerRadius = new CornerRadius(8);
			Padding = new Thickness(8, 7, 8, 7);
			CornerRadius = new CornerRadius(8);
			label = Ui.Text("", 8, p.Soft, FontWeight.Medium);
			label.TextTrimming = TextTrimming.CharacterEllipsis;
			value = Ui.Text("", 14, p.Ink, FontWeight.DemiBold, numbers: true);
			value.Margin = new Thickness(0, 2, 0, 0);
			chart.Margin = new Thickness(0, 5, 0, 0);
			// A long value ("1538 U/min" in a narrow tile) shrinks to fit instead of being cut off.
			var fit = SteadyHeight.Of(new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = HorizontalAlignment.Left, Child = value });
			Child = Ui.Stack(1, label, fit, chart);
		}

		public void Show(Spec s, Color tint, Color back, Palette p)
		{
			Background = Palette.Brush(back);
			label.Text = s.Label.ToUpper(L.Culture);
			value.Text = s.Value;
            if (s.Key.StartsWith("drive:")) SensorDetails.Value(value, s.Key);
			value.Foreground = Palette.Brush(s.Color ?? p.Ink);
			bool curve = s.Series != null && s.Series.Count(double.IsFinite) > 1;
			// A tile of a value that has a curve keeps the curve's room while it has none for a moment (a fan
			// that stops), so the tiles below do not move up and down.
			if (curve) hadCurve.Add(s.Key);
			// Reserve the native trend lane from the first sample, including static/missing curves.
            // Otherwise the second sample inserts 22 pixels and moves the whole sidebar.
            chart.IsVisible = true;
			chart.Opacity = curve ? 1 : 0;
			if (curve)
			{
				chart.Max = null;
				chart.Set(s.Series!, s.Color ?? tint);
			}
		}
	}
}

// 270° ring: grey track and a coloured part for the value, open at the bottom.
public sealed class DialArc : Control
{
	private double fraction;

	private Color color = Colors.MediumSeaGreen;

	private Color track = Colors.DimGray;

	public void Set(double value, Color c, Color back)
	{
		fraction = value;
		color = c;
		track = back;
		InvalidateVisual();
	}

	public override void Render(DrawingContext ctx)
	{
		double size = Math.Min(Bounds.Width, Bounds.Height);
		if (size < 10)
		{
			return;
		}
		double thick = Math.Max(5, size * 0.085);
		var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
		double r = size / 2 - thick / 2 - 1;
		ctx.DrawGeometry(null, new Pen(Palette.Brush(track), thick, lineCap: PenLineCap.Round), Arc(center, r, 135, 270));
		if (fraction > 0.004)
		{
			ctx.DrawGeometry(null, new Pen(Palette.Brush(Color.FromArgb(50, color.R, color.G, color.B)), thick + 6, lineCap: PenLineCap.Round), Arc(center, r, 135, 270 * fraction));
			ctx.DrawGeometry(null, new Pen(Palette.Brush(color), thick, lineCap: PenLineCap.Round), Arc(center, r, 135, 270 * fraction));
		}
	}

	private static Geometry Arc(Point c, double r, double start, double sweep)
	{
		var g = new StreamGeometry();
		using (var x = g.Open())
		{
			Point P(double deg) => new Point(c.X + r * Math.Cos(deg * Math.PI / 180), c.Y + r * Math.Sin(deg * Math.PI / 180));
			x.BeginFigure(P(start), false);
			x.ArcTo(P(start + sweep), new Size(r, r), 0, sweep > 180, SweepDirection.Clockwise);
			x.EndFigure(false);
		}
		return g;
	}
}

// Load per thread: a column per thread with a coloured part from the bottom.
public sealed class ThreadBars : Control
{
	private double[] values = Array.Empty<double>();

	private Color fill = Colors.CornflowerBlue;

	private Color back = Colors.DimGray;

	private Color hot = Colors.OrangeRed;

	public void Set(double[] loads, Color color, Color cell, Color warm)
	{
		values = loads;
		fill = color;
		back = cell;
		hot = warm;
		InvalidateVisual();
	}

	public override void Render(DrawingContext ctx)
	{
		if (values.Length == 0 || Bounds.Width < 10)
		{
			return;
		}
		double gap = Math.Min(values.Length > 16 ? 2 : 3, Bounds.Width / (values.Length * 3.0));
		double w = (Bounds.Width - gap * (values.Length - 1)) / values.Length;
		double h = Bounds.Height;
		for (int i = 0; i < values.Length; i++)
		{
			double x = i * (w + gap);
			ctx.DrawRectangle(Palette.Brush(back), null, new Rect(x, 0, w, h), 3, 3);
			double v = double.IsFinite(values[i]) ? Math.Clamp(values[i] / 100.0, 0, 1) : 0;
			double bar = Math.Max(3, v * h);
			ctx.DrawRectangle(Palette.Brush(v >= 0.85 ? hot : fill), null, new Rect(x, h - bar, w, bar), 3, 3);
		}
	}
}

// Dashed lines at 0, ¼, ½, ¾ and the full height of a chart, with the scale labels on the left.
public sealed class GridLines : Control
{
	public const double LabelWidth = 52;

	public Color Color { get; set; } = Colors.Gray;

	public Color Text { get; set; } = Colors.Gray;

	public string[] Labels { get; set; } = Array.Empty<string>();

	public override void Render(DrawingContext ctx)
	{
		var pen = new Pen(Palette.Brush(Color), 1, new DashStyle(new double[] { 3, 3 }, 0));
		var face = new Typeface(Ui.Font);
		for (int i = 0; i <= 4; i++)
		{
			double y = Math.Round(i / 4.0 * (Bounds.Height - 1)) + 0.5;
			ctx.DrawLine(pen, new Point(LabelWidth, y), new Point(Bounds.Width, y));
			if (i < Labels.Length)
			{
				var text = new FormattedText(Labels[i], System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, face, 8.5, Palette.Brush(Text));
				double top = Math.Clamp(y - text.Height / 2, 0, Math.Max(0, Bounds.Height - text.Height));
				ctx.DrawText(text, new Point(0, top));
			}
		}
	}
}
