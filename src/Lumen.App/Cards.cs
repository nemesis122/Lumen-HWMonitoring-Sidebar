using System;
using Avalonia.Controls;
using Lumen.Core;

namespace Lumen.App;

public static class Cards
{
	public static Control Create(WidgetKind kind, WidgetStyle style, int index, Palette p, Metrics m, Settings s)
	{
        var control = CreateNative(kind, style, index, p, m, s);
        SensorDetails.Design(control, Settings.Successor(style));
        if (InstrumentDesigns.Contains(style) && control is Border frame && control is not InstrumentTile)
            InstrumentDesigns.Frame(frame, style, p, m);
        return control;
    }

    private static Control CreateNative(WidgetKind kind, WidgetStyle style, int index, Palette p, Metrics m, Settings s)
	{
        s = s.ForWidget(kind);
        if (AioDesigns.Contains(style))
            return kind == WidgetKind.Overview ? new AioTile(style,p,m,s)
                : InfoTile.Handles(kind) ? new InfoTile(kind,index,WidgetStyle.Standard,p,m,s)
                : CreateNative(kind,WidgetStyle.Standard,index,p,m,s);
		// The overview is a list in every design (name, bar, value), drawn in that design's look.
		if (kind == WidgetKind.Overview)
		{
			return new OverviewTile(Settings.Successor(style), p, m, s);
		}
		if (Settings.HasStyles(kind))
		{
            if (InstrumentDesigns.Contains(style) && InstrumentTile.Fits(kind))
                return new InstrumentTile(kind, index, p, m, s, style);
			if (LocalDesigns.Contains(style) && !InfoTile.Handles(kind))
				return new LocalDesignTile(kind, index, p, m, s, style);
			// Clock, world clocks, calendar, weather, now playing and the top apps: drawn in the design's frame,
			// fonts and colours, but with content that fits them (no bar for a time, a list of apps).
			if (InfoTile.Handles(kind) && Settings.Successor(style) != WidgetStyle.Standard)
			{
				return new InfoTile(kind, index, Settings.Successor(style), p, m, s);
			}
			switch (Settings.Successor(style))
			{
            case WidgetStyle.CleanDark:
            case WidgetStyle.CompactPro:
            case WidgetStyle.PerformanceView:
                return new FavoriteTile(kind, index, p, m, s, style);
            case WidgetStyle.Clarity:
            case WidgetStyle.ClarityColor:
            case WidgetStyle.ClarityRows:
            case WidgetStyle.ClarityTiles:
            case WidgetStyle.ControlDeck:
                return new InsightTile(kind,index,p,m,s,style);
            case WidgetStyle.Duplex:
            case WidgetStyle.SignalRows:
            case WidgetStyle.OpenPanel:
                return new ComparisonTile(kind,index,p,m,s,style);
			case WidgetStyle.Segments:
				return new SegmentTile(kind, index, p, m, s);
			case WidgetStyle.SegmentGraph:
				return new SegmentTile(kind, index, p, m, s, graph: true);
			case WidgetStyle.Table:
				return new TableTile(kind, index, p, m, s);
			case WidgetStyle.TableGraph:
				return new TableTile(kind, index, p, m, s, graph: true);
			case WidgetStyle.Pill:
				return new PillTile(kind, index, p, m, s);
			case WidgetStyle.FillBars:
				return new FillBarsTile(kind, index, p, m, s);
			case WidgetStyle.FillBarsGraph:
				return new FillBarsTile(kind, index, p, m, s, graph: true);
			case WidgetStyle.Split:
				return new SplitTile(kind, index, p, m, s);
			case WidgetStyle.PillRow:
				return new PillRowTile(kind, index, p, m, s);
			case WidgetStyle.PillRowGraph:
				return new PillRowTile(kind, index, p, m, s, graph: true);
			case WidgetStyle.KeyFigures:
				return new KeyFiguresTile(kind, index, p, m, s);
			case WidgetStyle.BadgeList:
				return new BadgeListTile(kind, index, p, m, s);
			case WidgetStyle.BadgeListGraph:
				return new BadgeListTile(kind, index, p, m, s, graph: true);
			case WidgetStyle.Evolution:
				return new EvolutionTile(kind, index, p, m, s);
			case WidgetStyle.Dial:
				// Widgets without a measured value (clock, calendar, weather, media …) have nothing for a
				// gauge; they use the Evolution layout in the same colours.
				return DialTile.Fits(kind) ? new DialTile(kind, index, p, m, s) : new EvolutionTile(kind, index, p, m, s);
			case WidgetStyle.TwinFocus:
				return new TwinFocusTile(kind, index, p, m, s);
			// Widgets without a measured value (clock, calendar, weather, media …) have no curve, ring or
			// level; in these designs they use the Evolution layout.
			case WidgetStyle.HistoryGraph:
				return DialTile.Fits(kind) ? new GraphTile(kind, index, p, m, s) : new EvolutionTile(kind, index, p, m, s);
			case WidgetStyle.RingPair:
				return DialTile.Fits(kind) ? new RingsTile(kind, index, p, m, s) : new EvolutionTile(kind, index, p, m, s);
			case WidgetStyle.LevelColumns:
				return DialTile.Fits(kind) ? new ColumnsTile(kind, index, p, m, s) : new EvolutionTile(kind, index, p, m, s);
			case WidgetStyle.NeonHud:
				return DialTile.Fits(kind) ? new NeonHudTile(kind, index, p, m, s) : new EvolutionTile(kind, index, p, m, s);
			case WidgetStyle.NeonHudGraph:
				return DialTile.Fits(kind) ? new NeonHudTile(kind, index, p, m, s, graph: true) : new EvolutionTile(kind, index, p, m, s);
			case WidgetStyle.NeonTiles:
				return DialTile.Fits(kind) ? new NeonTilesTile(kind, index, p, m, s) : new EvolutionTile(kind, index, p, m, s);
			case WidgetStyle.Thermostat:
				return DialTile.Fits(kind) ? new ThermostatTile(kind, index, p, m, s) : new EvolutionTile(kind, index, p, m, s);
			case WidgetStyle.SmartPanel:
				return DialTile.Fits(kind) ? new SmartPanelTile(kind, index, p, m, s) : new EvolutionTile(kind, index, p, m, s);
			case WidgetStyle.PurpleDash:
				return DialTile.Fits(kind) ? new PurpleTile(kind, index, p, m, s) : new EvolutionTile(kind, index, p, m, s);
			case WidgetStyle.PurpleList:
				return DialTile.Fits(kind) ? new PurpleTile(kind, index, p, m, s, list: true) : new EvolutionTile(kind, index, p, m, s);
			case WidgetStyle.Pure:
				return DialTile.Fits(kind) ? new PureTile(kind, index, p, m, s) : new EvolutionTile(kind, index, p, m, s);
			case WidgetStyle.PureBar:
			case WidgetStyle.PureNumber:
			case WidgetStyle.PureSegments:
				return new PureTile(kind, index, p, m, s, style);
			case WidgetStyle.PureWidgets:
				return DialTile.Fits(kind) ? new WidgetsTile(kind, index, p, m, s) : new EvolutionTile(kind, index, p, m, s);
			case WidgetStyle.PureWidgetsRight:
				return kind is WidgetKind.Cpu or WidgetKind.Gpu ? new RightWidgetsTile(kind, index, p, m, s) : new WidgetsTile(kind, index, p, m, s);
			case WidgetStyle.SciFi:
				return DialTile.Fits(kind) ? new SciFiTile(kind, index, p, m, s) : new EvolutionTile(kind, index, p, m, s);
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
			WidgetKind.Overview => new OverviewCard(p, m, s),
			WidgetKind.DriveHealth => new DriveHealthCard(p, m, s),
			WidgetKind.Timer => new TimerCard(p, m, s),
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
			WidgetKind.Overview => L.T("Overview"),
			WidgetKind.DriveHealth => L.T("Drive health"),
			WidgetKind.Timer => L.T("Timer"),
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
			WidgetKind.Cooling => new(string, string)[3]
			{
				("cooling.fans", L.T("Fans")),
				("cooling.fancount", L.T("Running fans")),
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
			WidgetKind.Overview => OverviewCard.Parts.Select(((string Key, string Label) x) => (x.Key, L.T(x.Label))).ToArray(),
			WidgetKind.DriveHealth => new(string, string)[4]
			{
				("health.life", L.T("Life left")),
				("health.hours", L.T("Power-on hours")),
				("health.count", L.T("Power-on count")),
				("health.temp", L.T("Temperature"))
			},
			_ => Array.Empty<(string, string)>(),
		};
	}
}



