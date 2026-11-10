using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Lumen.Core;

namespace Lumen.App;

// "Overview": the whole PC on one card, one line per part with a bar, for small screens or as a summary
// above the detailed widgets.
public sealed class OverviewCard : Card
{
	private readonly Dictionary<string, (TextBlock Value, FillBack Bar)> rows = new();

	public static readonly (string Key, string Label)[] Parts =
	{
		("overview.cpu", "Processor"), ("overview.gpu", "Graphics"), ("overview.memory", "Memory"),
		("overview.vram", "Video memory"), ("overview.network", "Network"), ("overview.drive", "Drives")
	};

	public OverviewCard(Palette p, Metrics m, Settings s)
		: base(p, m, s, Glyphs.Overview, L.T("Overview"))
	{
		var grid = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("Auto,*,Auto"), RowSpacing = 9, ColumnSpacing = 10 };
		int row = 0;
		foreach (var (key, label) in Parts)
		{
			if (!Shows(key))
			{
				continue;
			}
			grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
			var name = Label(L.T(label), p);
			name.VerticalAlignment = VerticalAlignment.Center;
			var bar = new FillBack { Height = 6, Radius = 3, Track = p.Track, VerticalAlignment = VerticalAlignment.Center };
			var value = Ui.Text("—", 12.5, p.Ink, FontWeight.DemiBold, numbers: true);
			value.HorizontalAlignment = HorizontalAlignment.Right;
			value.MinWidth = 92;
			value.TextAlignment = TextAlignment.Right;
			Grid.SetRow(name, row);
			Grid.SetRow(bar, row);
			Grid.SetColumn(bar, 1);
			Grid.SetRow(value, row);
			Grid.SetColumn(value, 2);
			grid.Children.Add(name);
			grid.Children.Add(bar);
			grid.Children.Add(value);
			rows[key] = (value, bar);
			row++;
		}
		Body.Children.Add(grid);
	}

	private void Put(string key, string text, double? fraction, Color color)
	{
		if (!rows.TryGetValue(key, out var r))
		{
			return;
		}
		r.Value.Text = text;
		r.Bar.Animate(fraction is double f && double.IsFinite(f) ? Math.Clamp(f, 0, 1) : 0, color);
	}

	private string Temp(double? c) => c is double v ? Format.Temperature(v, S.Fahrenheit) + " " + TempUnit : "—";

	private Color Heat(double? c) => c is double v ? Palette.Alpha(P.At(Format.Heat(v)), 1) : P.Cool;

	protected override void UpdateReadings(Snapshot s, History h)
	{
		CpuInfo? cpu = s.Cpu;
		Put("overview.cpu", Join(Temp(cpu?.Temperature), cpu?.Load is double l ? Format.Percent(l) : ""), cpu?.Load / 100.0, Heat(cpu?.Temperature));
		GpuInfo? gpu = s.Gpus.FirstOrDefault(g => !g.Integrated) ?? s.Gpus.FirstOrDefault();
		Put("overview.gpu", gpu == null ? "—" : Join(Temp(gpu.Temperature), gpu.Load is double gl ? Format.Percent(gl) : ""), gpu?.Load / 100.0, Heat(gpu?.Temperature));
		Put("overview.memory", s.Memory?.Load is double ml ? Format.Percent(ml) + " · " + Format.Number(s.Memory.UsedGb, "0.0") + " GB" : "—", s.Memory?.Load / 100.0, P.Cool);
		double? vram = gpu?.MemoryUsedMb is double u && gpu.MemoryTotalMb is double t && t > 0 ? u / t : null;
		Put("overview.vram", gpu?.MemoryUsedMb is double used && gpu.MemoryTotalMb is double total && total > 0 ? Format.Number(used / 1024.0, "0.0") + " / " + Format.Number(total / 1024.0, "0") + " GB" : "—", vram, P.Cool);
		NetworkInfo? n = s.Network;
		Put("overview.network", n == null ? "—" : "↓ " + Format.Rate(n.DownloadBytesPerSecond) + "  ↑ " + Format.Rate(n.UploadBytesPerSecond), Summaries.NetFill(n?.DownloadBytesPerSecond, n?.LinkBitsPerSecond), P.Cool);
		DiskInfo? hot = s.Disks.Where(d => d.Temperature.HasValue).OrderByDescending(d => d.Temperature).FirstOrDefault();
		Put("overview.drive", hot == null ? "—" : (hot.Letters.Length > 0 ? hot.Letters + " " : "") + Temp(hot.Temperature), hot?.UsedPercent / 100.0, Heat(hot?.Temperature));
	}

	private static string Join(string a, string b) => b.Length == 0 ? a : a + " · " + b;

	/// <summary>
	/// The lines of the overview for the shown parts: processor, graphics, memory, video memory, network and
	/// one line per drive (none of them singled out as the hottest).
	/// </summary>
	public static List<OverviewRow> Rows(Snapshot s, Settings set, Palette p)
	{
		var rows = new List<OverviewRow>();
		string Temp(double? c) => c is double v ? Format.Temperature(v, set.Fahrenheit) + " " + Format.TemperatureUnit(set.Fahrenheit) : "—";
		Color Heat(double? c) => c is double v ? Palette.Alpha(p.At(Format.Heat(v)), 1) : p.Cool;
		double F(double? v) => v is double x && double.IsFinite(x) ? Math.Clamp(x, 0, 1) : 0;
		void Add(string key, string label, string main, string sub, double? fraction, double? second, Color color, string? spark = null)
		{
			if (set.Shows(key))
			{
				rows.Add(new OverviewRow(key, label, main, sub, F(fraction), second is double x ? F(x) : null, color, spark));
			}
		}
		CpuInfo? cpu = s.Cpu;
		Add("overview.cpu", L.T("Processor"), Temp(cpu?.Temperature), cpu?.Load is double l ? Format.Percent(l) : "", Format.Heat(cpu?.Temperature), cpu?.Load / 100.0, Heat(cpu?.Temperature), "cpu.temp");
		GpuInfo? gpu = s.Gpus.FirstOrDefault(g => !g.Integrated) ?? s.Gpus.FirstOrDefault();
		Add("overview.gpu", L.T("Graphics"), Temp(gpu?.Temperature), gpu?.Load is double gl ? Format.Percent(gl) : "", Format.Heat(gpu?.Temperature), gpu?.Load / 100.0, Heat(gpu?.Temperature), gpu == null ? null : "gpu." + gpu.Id + ".temp");
		Add("overview.memory", L.T("Memory"), Format.Percent(s.Memory?.Load), s.Memory is MemoryInfo mem ? Format.Number(mem.UsedGb, "0.0") + " / " + Format.Number(mem.TotalGb, "0") + " GB" : "", s.Memory?.Load / 100.0, null, p.Cool, "mem.load");
		bool vram = gpu?.MemoryUsedMb is double && gpu.MemoryTotalMb is double t && t > 0;
		Add("overview.vram", L.T("Video memory"), vram ? Format.Number(gpu!.MemoryUsedMb!.Value / 1024.0, "0.0") + " GB" : "—", vram ? "/ " + Format.Number(gpu!.MemoryTotalMb!.Value / 1024.0, "0") + " GB" : "", vram ? gpu!.MemoryUsedMb / gpu.MemoryTotalMb : null, null, p.Cool);
		NetworkInfo? n = s.Network;
		Add("overview.network", L.T("Network"), n == null ? "—" : "↓ " + Format.Rate(n.DownloadBytesPerSecond), n == null ? "" : "↑ " + Format.Rate(n.UploadBytesPerSecond), Summaries.NetFill(n?.DownloadBytesPerSecond, n?.LinkBitsPerSecond), Summaries.NetFill(n?.UploadBytesPerSecond, n?.LinkBitsPerSecond), p.Cool, "net.down");
		if (set.Shows("overview.drive"))
		{
			foreach (DiskInfo d in s.Disks.Where(d => set.Shows("drive:" + d.Id)))
			{
				rows.Add(new OverviewRow("overview.drive", d.Letters.Length > 0 ? d.Letters : Names.Short(d.Name), Temp(d.Temperature), "", Format.Heat(d.Temperature), d.UsedPercent is double u ? F(u / 100) : null, Heat(d.Temperature)));
			}
		}
		return rows;
	}
}

// "Drive health": remaining life, operating hours, power-on count and temperature of every drive, with a
// plain verdict, from the values the drives report themselves.
public sealed class DriveHealthCard : Card
{
	private readonly StackPanel list = new StackPanel { Spacing = 10 };

	public DriveHealthCard(Palette p, Metrics m, Settings s)
		: base(p, m, s, Glyphs.Health, L.T("Drive health"))
	{
		Body.Children.Add(list);
	}

	protected override void UpdateReadings(Snapshot s, History h)
	{
		list.Children.Clear();
		int warnings = 0;
		foreach (DiskInfo d in s.Disks)
		{
			var (verdict, color) = Verdict(d);
			if (color != P.Calm)
			{
				warnings++;
			}
			var name = Ui.Text((d.Letters.Length > 0 ? d.Letters + "  " : "") + d.Name, 12.5, P.Ink, FontWeight.DemiBold);
			name.TextTrimming = TextTrimming.CharacterEllipsis;
			var chip = new Border
			{
				CornerRadius = new CornerRadius(6),
				Padding = new Thickness(7, 1),
				Background = Palette.Brush(Palette.Alpha(color, 0.18)),
				Child = Ui.Text(verdict, 10.5, color, FontWeight.Bold),
				VerticalAlignment = VerticalAlignment.Center
			};
			var block = new StackPanel { Spacing = 4 };
			block.Children.Add(Ui.Columns("*,Auto", name, chip));
			if (Shows("health.life") && d.Life is double life)
			{
				var bar = new FillBack { Height = 6, Radius = 3, Track = P.Track, VerticalAlignment = VerticalAlignment.Center };
				bar.Animate(Math.Clamp(life / 100.0, 0, 1), life < 10 ? Palette.Alpha(P.Ramp[^1], 1) : life < 30 ? Palette.Alpha(P.Ramp[2], 1) : P.Calm);
				var lifeText = Ui.Text(Format.Number(life, "0") + " %", 12, P.Ink, FontWeight.DemiBold, numbers: true);
                SensorDetails.Value(lifeText, "drive:" + d.Id + ":life");
				lifeText.Margin = new Thickness(8, 0, 0, 0);
				block.Children.Add(Ui.Columns("Auto,*,Auto", Label(L.T("Life left"), P), new Border { Padding = new Thickness(8, 0, 0, 0), Child = bar }, lifeText));
			}
			var facts = new List<string>();
			if (Shows("health.hours") && d.PowerOnHours is double hours) facts.Add(Format.Number(hours, "0") + " h");
			if (Shows("health.count") && d.PowerOnCount is double count) facts.Add(Format.Number(count, "0") + "× " + L.T("started"));
			if (Shows("health.temp") && d.Temperature is double t) facts.Add(Format.Temperature(t, S.Fahrenheit) + " " + TempUnit);
			if (facts.Count > 0)
			{
                var factsText = Ui.Text(string.Join("  ·  ", facts), 11.5, P.Soft, FontWeight.Medium, numbers: true);
                var keys = new List<string>();
                if (Shows("health.hours") && d.PowerOnHours.HasValue) keys.Add("drive:" + d.Id + ":hours");
                if (Shows("health.count") && d.PowerOnCount.HasValue) keys.Add("drive:" + d.Id + ":powercount");
                if (Shows("health.temp") && d.Temperature.HasValue) keys.Add("drive:" + d.Id + ":temp");
                SensorDetails.Value(factsText, string.Join('|', keys));
                block.Children.Add(factsText);
			}
			else if (!d.Life.HasValue)
			{
				block.Children.Add(Ui.Text(L.T("This drive reports no health values."), 11, P.Soft));
			}
			list.Children.Add(block);
		}
		if (s.Disks.Count == 0)
		{
			list.Children.Add(Ui.Text(L.T("No drives found."), 11.5, P.Soft));
		}
		Aside = s.Disks.Count == 0 || !new[] { "health.life", "health.hours", "health.count", "health.temp" }.Any(Shows) ? "" : warnings == 0 ? L.T("All good") : L.F("{0} to check", warnings);
	}

	private (string, Color) Verdict(DiskInfo d)
	{
		Color red = Palette.Alpha(P.Ramp[^1], 1);
		Color amber = Palette.Alpha(P.Ramp[2], 1);
		if (d.Life is < 5 || d.Temperature is >= 80)
		{
			return (L.T("Critical"), red);
		}
		if (d.Life is < 20 || d.Temperature is >= 70)
		{
			return (L.T("Check"), amber);
		}
		return (L.T("Good"), P.Calm);
	}
}

// "Timer": a countdown with a ring. Click the ring to start or pause, the presets set the time, ↺ resets.
// When the time is up the card turns red and Windows plays its notification sound.
public sealed class TimerCard : Card
{
	// Kept outside the card so a running timer survives rebuilding the sidebar.
	private static TimeSpan length = TimeSpan.FromMinutes(25);

	private static DateTime? endsAt;

	private static TimeSpan left = TimeSpan.FromMinutes(25);

	private static bool rang;

	private readonly RingPair ring = new RingPair { Width = 120, Height = 120 };

	private readonly TextBlock time;

	private readonly TextBlock state;

	private readonly DispatcherTimer tick;

	private readonly IBrush normalBackground;

	public TimerCard(Palette p, Metrics m, Settings s)
		: base(p, m, s, Glyphs.Timer, L.T("Timer"))
	{
		normalBackground = Background ?? Brushes.Transparent;
		time = Ui.Text("", 28, p.Ink, FontWeight.DemiBold, numbers: true);
		time.HorizontalAlignment = HorizontalAlignment.Center;
		state = Ui.Text("", 10.5, p.Soft, FontWeight.Medium);
		state.HorizontalAlignment = HorizontalAlignment.Center;
		var center = Ui.Stack(0, time, state);
		center.VerticalAlignment = VerticalAlignment.Center;
		center.HorizontalAlignment = HorizontalAlignment.Center;
		var face = new Panel { Width = 120, Height = 120, Children = { ring, center }, HorizontalAlignment = HorizontalAlignment.Center, Cursor = new Cursor(StandardCursorType.Hand), Background = Brushes.Transparent };
		ToolTip.SetTip(face, L.T("Click to start or pause"));
		face.PointerReleased += (_, e) =>
		{
			Toggle();
			e.Handled = true;
		};
		var presets = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center };
		foreach (int minutes in new[] { 5, 15, 25, 45 })
		{
			presets.Children.Add(Chip(minutes + " min", () => Set(TimeSpan.FromMinutes(minutes))));
		}
		presets.Children.Add(Chip("↺", () => Set(length)));
		Body.Children.Add(face);
		Body.Children.Add(presets);
		tick = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Background, (_, _) => Refresh());
		AttachedToVisualTree += (_, _) => tick.Start();
		DetachedFromVisualTree += (_, _) => tick.Stop();
		Refresh();
	}

	private Button Chip(string text, Action click)
	{
		var b = new Button
		{
			Content = Ui.Text(text, 11, P.Ink, FontWeight.Medium),
			Padding = new Thickness(9, 3),
			CornerRadius = new CornerRadius(8),
			Background = Palette.Brush(P.Track),
			BorderThickness = new Thickness(0)
		};
		b.Click += (_, _) => click();
		return b;
	}

	private static TimeSpan Left => endsAt is DateTime end ? (end - DateTime.UtcNow < TimeSpan.Zero ? TimeSpan.Zero : end - DateTime.UtcNow) : left;

	private void Set(TimeSpan span)
	{
		length = span;
		left = span;
		endsAt = null;
		rang = false;
		Refresh();
	}

	private void Toggle()
	{
		if (endsAt != null)
		{
			left = Left;
			endsAt = null;
		}
		else
		{
			if (left <= TimeSpan.Zero)
			{
				left = length;
				rang = false;
			}
			endsAt = DateTime.UtcNow + left;
		}
		Refresh();
	}

	[DllImport("user32.dll")]
	private static extern bool MessageBeep(uint type);

	private void Refresh()
	{
		TimeSpan now = Left;
		bool running = endsAt != null;
		bool done = now <= TimeSpan.Zero && (running || rang);
		if (done && !rang)
		{
			rang = true;
			endsAt = null;
			left = TimeSpan.Zero;
			if (OperatingSystem.IsWindows())
			{
				try { MessageBeep(0x40); } catch { }
			}
		}
		time.Text = now.TotalHours >= 1 ? now.ToString(@"h\:mm\:ss") : now.ToString(@"mm\:ss");
		state.Text = done ? L.T("Time is up") : running ? L.T("Running") : now == length ? L.T("Ready") : L.T("Paused");
		Color red = Palette.Alpha(P.Ramp[^1], 1);
		double fraction = length.TotalSeconds > 0 ? now.TotalSeconds / length.TotalSeconds : 0;
		ring.Set(fraction, done ? red : P.Cool, null, P.Cool, P.Track);
		time.Foreground = Palette.Brush(done ? red : P.Ink);
		Background = done ? Palette.Brush(Palette.Mix(P.Card, red, 0.16)) : normalBackground;
		Aside = Format.Number(length.TotalMinutes, "0") + " min";
	}

	protected override void UpdateReadings(Snapshot s, History h)
	{
		Refresh();
	}
}
