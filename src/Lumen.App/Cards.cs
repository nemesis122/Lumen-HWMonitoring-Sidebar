using System;
using Avalonia.Controls;
using Lumen.Core;

namespace Lumen.App;

public static class Cards
{
	public static Control Create(WidgetKind kind, WidgetStyle style, int index, Palette p, Metrics m, Settings s)
	{
		if (style == WidgetStyle.Ring && Settings.HasStyles(kind))
		{
			return new RingTile(kind, index, p, m, s);
		}
		if (style == WidgetStyle.Minimal && Settings.HasStyles(kind))
		{
			return new MinimalRow(kind, index, p, m, s);
		}
		if (style == WidgetStyle.Bar && Settings.HasStyles(kind))
		{
			return new BarTile(kind, index, p, m, s);
		}
		if (style == WidgetStyle.Graph && Settings.HasStyles(kind))
		{
			return new GraphTile(kind, index, p, m, s);
		}
		if (style == WidgetStyle.Tile && Settings.HasStyles(kind))
		{
			return new NumberTile(kind, index, p, m, s);
		}
		if (style == WidgetStyle.Compact && Settings.HasStyles(kind))
		{
			return new CompactRow(kind, index, p, m, s);
		}
		if (style == WidgetStyle.Status && Settings.HasStyles(kind))
		{
			return new StatusRow(kind, index, p, m, s);
		}
		if (style == WidgetStyle.Segments && Settings.HasStyles(kind))
		{
			return new SegmentTile(kind, index, p, m, s);
		}
		if (style == WidgetStyle.DualRing && Settings.HasStyles(kind))
		{
			return new DualRingTile(kind, index, p, m, s);
		}
		if (Settings.HasStyles(kind))
		{
			switch (style)
			{
			case WidgetStyle.Timeline:
				return new TimelineTile(kind, index, p, m, s);
			case WidgetStyle.Heatmap:
				return new HeatmapTile(kind, index, p, m, s);
			case WidgetStyle.Rings:
				return new RingsTile(kind, index, p, m, s);
			case WidgetStyle.BigNumber:
				return new BigNumberTile(kind, index, p, m, s);
			case WidgetStyle.Table:
				return new TableTile(kind, index, p, m, s);
			case WidgetStyle.Pill:
				return new PillTile(kind, index, p, m, s);
			case WidgetStyle.DotMatrix:
				return new DotsTile(kind, index, p, m, s);
			case WidgetStyle.Bento:
				return new BentoTile(kind, index, p, m, s);
			case WidgetStyle.Ticker:
				return new TickerTile(kind, index, p, m, s);
			case WidgetStyle.MiniArc:
				return new MiniArcTile(kind, index, p, m, s);
			case WidgetStyle.FillBars:
				return new FillBarsTile(kind, index, p, m, s);
			case WidgetStyle.Split:
				return new SplitTile(kind, index, p, m, s);
			case WidgetStyle.Chips:
				return new ChipTile(kind, index, p, m, s);
			case WidgetStyle.Scale:
				return new ScaleTile(kind, index, p, m, s);
			case WidgetStyle.HistoryBars:
				return new HistoryBarsTile(kind, index, p, m, s);
			case WidgetStyle.Backdrop:
				return new BackdropTile(kind, index, p, m, s);
			case WidgetStyle.PillDuo:
				return new PillDuoTile(kind, index, p, m, s);
			case WidgetStyle.PillRow:
				return new PillRowTile(kind, index, p, m, s);
			case WidgetStyle.KeyFigures:
				return new KeyFiguresTile(kind, index, p, m, s);
			case WidgetStyle.Liquid:
			case WidgetStyle.TwinFocus:
			case WidgetStyle.Contour:
			case WidgetStyle.SplitEdge:
			case WidgetStyle.Ribbon:
			case WidgetStyle.CoreGrid:
				return new NextTile(kind, index, p, m, s, style);
			case WidgetStyle.SignalPro:
			case WidgetStyle.RailPro:
			case WidgetStyle.OrbitPro:
			case WidgetStyle.Prism:
			case WidgetStyle.Spectrum:
				return new ProTile(kind, index, p, m, s, style);
			case WidgetStyle.PillDuoPlus:
				return new PillDuoPlusTile(kind, index, p, m, s);
			case WidgetStyle.SignalBand:
				return new SignalBandTile(kind, index, p, m, s);
			case WidgetStyle.Rail:
				return new RailTile(kind, index, p, m, s);
			case WidgetStyle.BadgeList:
				return new BadgeListTile(kind, index, p, m, s);
			}
		}
		return kind switch
		{
			WidgetKind.Clock => new ClockCard(p, m, s), 
			WidgetKind.WorldClocks => new WorldClocksCard(p, m, s), 
			WidgetKind.Calendar => new CalendarCard(p, m, s), 
			WidgetKind.Weather => new WeatherCard(p, m, s), 
			WidgetKind.Media => new MediaCard(p, m, s), 
			WidgetKind.Battery => new BatteryCard(p, m, s), 
			WidgetKind.Cpu => new CpuCard(p, m, s), 
			WidgetKind.Gpu => new GpuCard(p, m, s, index), 
			WidgetKind.Fps => new FpsCard(p, m, s), 
			WidgetKind.Cooling => new CoolingCard(p, m, s), 
			WidgetKind.Power => new PowerCard(p, m, s), 
			WidgetKind.Memory => new MemoryCard(p, m, s), 
			WidgetKind.Processes => new ProcessesCard(p, m, s), 
			WidgetKind.System => new SystemCard(p, m, s), 
			WidgetKind.Drives => new DrivesCard(p, m, s), 
			WidgetKind.Volumes => new VolumesCard(p, m, s), 
			WidgetKind.Ping => new PingCard(p, m, s), 
			WidgetKind.DataUsage => new DataUsageCard(p, m, s), 
			WidgetKind.Notes => new NotesCard(p, m, s), 
			_ => new NetworkCard(p, m, s), 
		};
	}

	public static string Name(WidgetKind kind)
	{
		return kind switch
		{
			WidgetKind.Clock => L.T("Clock and date"), 
			WidgetKind.WorldClocks => L.T("World clocks"), 
			WidgetKind.Calendar => L.T("Calendar"), 
			WidgetKind.Weather => L.T("Weather"), 
			WidgetKind.Media => L.T("Now playing"), 
			WidgetKind.Battery => L.T("Battery"), 
			WidgetKind.Cpu => L.T("Processor"), 
			WidgetKind.Gpu => L.T("Graphics card"), 
			WidgetKind.Fps => L.T("FPS (games)"), 
			WidgetKind.Cooling => L.T("Cooling and mainboard"), 
			WidgetKind.Power => L.T("Power and cost"), 
			WidgetKind.Memory => L.T("Memory"), 
			WidgetKind.Processes => L.T("Top apps"), 
			WidgetKind.System => L.T("System"), 
			WidgetKind.Drives => L.T("Drives"), 
			WidgetKind.Volumes => L.T("Storage space"), 
			WidgetKind.Ping => "Ping", 
			WidgetKind.DataUsage => L.T("Data usage"), 
			WidgetKind.Notes => L.T("Notes"), 
			_ => L.T("Network"), 
		};
	}

	public static (string Key, string Label)[] Values(WidgetKind kind)
	{
		return kind switch
		{
			WidgetKind.Clock => new(string, string)[3]
			{
				("clock.seconds", L.T("Seconds")),
				("clock.date", L.T("Date")),
				("clock.day", L.T("Day progress"))
			}, 
			WidgetKind.Weather => new(string, string)[4]
			{
				("weather.forecast", L.T("Forecast")),
				("weather.feels", L.T("Feels like")),
				("weather.wind", L.T("Wind")),
				("weather.humidity", L.T("Humidity"))
			}, 
			WidgetKind.Battery => new(string, string)[3]
			{
				("battery.time", L.T("Time left")),
				("battery.rate", L.T("Rate")),
				("battery.health", L.T("Health"))
			}, 
			WidgetKind.Cpu => new(string, string)[6]
			{
				("cpu.chart", L.T("History chart")),
				("cpu.cores", L.T("Core bars")),
				("cpu.clock", L.T("Clock speed")),
				("cpu.power", L.T("Power")),
				("cpu.voltage", L.T("Voltage")),
				("cpu.fan", L.T("Fan"))
			}, 
			WidgetKind.Gpu => new(string, string)[5]
			{
				("gpu.chart", L.T("History chart")),
				("gpu.vram", "VRAM"),
				("gpu.power", L.T("Power")),
				("gpu.hotspot", L.T("Hot spot")),
				("gpu.fan", L.T("Fan"))
			}, 
			WidgetKind.Cooling => new(string, string)[2]
			{
				("cooling.fans", L.T("Fans")),
				("cooling.board", L.T("Board temperatures"))
			}, 
			WidgetKind.Memory => new(string, string)[4]
			{
				("mem.bar", L.T("Segment bar")),
				("mem.free", L.T("Available")),
				("mem.total", L.T("Installed")),
				("mem.peak", L.T("2-min peak"))
			}, 
			WidgetKind.Processes => new(string, string)[1] { ("proc.memory", "RAM") }, 
			WidgetKind.Drives => new(string, string)[6]
			{
				("drives.model", L.T("Model name")),
				("drives.temp", L.T("Temperature")),
				("drives.bar", L.T("Usage bar")),
				("drives.used", L.T("Used in %")),
				("drives.io", L.T("Read and write rates")),
				("drives.temps", L.T("Each drive"))
			}, 
			WidgetKind.Network => new(string, string)[6]
			{
				("net.link", L.T("Link speed")),
				("net.chart", L.T("History chart")),
				("net.use", L.T("Link use")),
				("net.peak", L.T("2-min peak")),
				("net.address", L.T("Address")),
				("net.external", L.T("External IP"))
			}, 
			WidgetKind.Fps => new(string, string)[2]
			{
				("fps.chart", L.T("History chart")),
				("fps.frametime", L.T("Frame time"))
			}, 
			WidgetKind.Ping => new(string, string)[4]
			{
				("ping.chart", L.T("History chart")),
				("ping.jitter", L.T("Jitter")),
				("ping.loss", L.T("Loss")),
				("ping.target", L.T("Target"))
			}, 
			WidgetKind.Power => new(string, string)[3]
			{
				("power.chart", L.T("History chart")),
				("power.cost", L.T("Costs")),
				("power.note", L.T("Note on the estimate"))
			}, 
			WidgetKind.DataUsage => new(string, string)[2]
			{
				("data.week", L.T("This week")),
				("data.month", L.T("This month"))
			}, 
			WidgetKind.System => new(string, string)[3]
			{
				("system.boot", L.T("Started")),
				("system.processes", L.T("Processes")),
				("system.threads", L.T("Threads"))
			}, 
			WidgetKind.Volumes => new(string, string)[5]
			{
				("volumes.label", L.T("Volume name")),
				("volumes.free", L.T("Free space")),
				("volumes.bar", L.T("Usage bar")),
				("volumes.detail", L.T("Used of total")),
				("volumes.each", L.T("Each volume"))
			}, 
			_ => Array.Empty<(string, string)>(), 
		};
	}
}



