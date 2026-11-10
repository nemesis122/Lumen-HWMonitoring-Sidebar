using Lumen.Core;

namespace Lumen.App;

public static class WidgetSensors
{
    private static IReadOnlyDictionary<string, string[]> Groups => SensorDefaults.Groups;

    public static void Select(Settings settings, WidgetSlot slot, string key, bool selected, Snapshot? snapshot)
    {
        settings.SensorSelectionVersion = SensorDefaults.SelectionVersion;
        void Set(string sensor, bool show)
        {
            settings.HiddenValues.Remove(sensor);
            slot.HiddenSensors.Remove(sensor);
            if (!show) slot.HiddenSensors.Add(sensor);
        }
        Set(key, selected);
        if (Groups.TryGetValue(key, out var children)) foreach (string child in children) Set(child, selected);
        if (selected) foreach (var group in Groups.Where(g => g.Value.Contains(key))) Set(group.Key, true);
        string? prefix = key == "cooling.fans" ? "fan:" : key == "cooling.board" ? "temp:" : null;
        if (prefix != null) foreach (var option in Options(slot.Kind, snapshot).Where(o => o.Key.StartsWith(prefix))) Set(option.Key, selected);
        if (selected && key.StartsWith("fan:")) Set("cooling.fans", true);
        if (selected && key.StartsWith("temp:")) Set("cooling.board", true);
    }

    public static string PrimaryKey(WidgetKind kind) => kind switch
    {
        WidgetKind.Cpu => "cpu.temp", WidgetKind.Gpu => "gpu.temp", WidgetKind.Memory => "mem.load",
        WidgetKind.Network => "net.down", WidgetKind.Cooling => "cooling.board", WidgetKind.Fps => "fps.value",
        WidgetKind.Power => "power.watts", WidgetKind.Ping => "ping.latency", WidgetKind.Battery => "battery.level",
        WidgetKind.Weather => "weather.temp", WidgetKind.System => "system.uptime", WidgetKind.Processes => "proc.cpu",
        WidgetKind.Drives => "drives.temp", WidgetKind.Volumes => "volumes.used", WidgetKind.DataUsage => "data.down",
        WidgetKind.Clock => "clock.time", WidgetKind.WorldClocks => "world.time", WidgetKind.Calendar => "calendar.date",
        WidgetKind.Media => "media.title", _ => ""
    };

    public static string SecondaryKey(WidgetKind kind) => kind switch
    {
        WidgetKind.Cpu => "cpu.load", WidgetKind.Gpu => "gpu.load", WidgetKind.Network => "net.up",
        WidgetKind.Memory => "mem.free", WidgetKind.Power => "power.energy", WidgetKind.Ping => "ping.router",
        WidgetKind.Fps => "fps.low", WidgetKind.Battery => "battery.state", WidgetKind.Cooling => "cooling.fans",
        WidgetKind.Weather => "weather.sky", WidgetKind.System => "system.os", WidgetKind.Processes => "proc.memory",
        WidgetKind.DataUsage => "data.up", WidgetKind.Clock => "clock.date", WidgetKind.Media => "media.artist", _ => ""
    };

    public static string CaptionKey(WidgetKind kind) => kind switch
    {
        WidgetKind.Memory => "mem.used", WidgetKind.Ping => "ping.jitter",
        WidgetKind.Volumes => "volumes.free", WidgetKind.WorldClocks => "world.offset",
        WidgetKind.Calendar => "calendar.week", WidgetKind.Fps => "fps.app", WidgetKind.Drives => "drives.count", _ => SecondaryKey(kind)
    };

    public static IReadOnlyList<(string Key, string Label)> Options(WidgetKind kind, Snapshot? snap)
    {
        var primary = PrimaryKey(kind);
        var result = new List<(string Key, string Label)>();
        void Add(string key, string label) { if (key.Length > 0 && result.All(x => x.Key != key)) result.Add((key, label)); }
        Add(primary, PrimaryLabel(kind));
        Add(SecondaryKey(kind), SecondaryLabel(kind));
        foreach (var option in Cards.Values(kind)) Add(option.Key, option.Label);
        if (Summaries.ChartKey(kind) is string history)
        {
            Add(history, L.T("History chart"));
            if (kind is WidgetKind.Cpu or WidgetKind.Gpu or WidgetKind.Network)
            {
                Add(primary + ".history", L.T("History chart") + " · " + L.T(kind == WidgetKind.Network ? "Download" : "Temperature"));
                Add(SecondaryKey(kind) + ".history", L.T("History chart") + " · " + L.T(kind == WidgetKind.Network ? "Upload" : "Load"));
            }
        }
        if (kind == WidgetKind.Memory) Add("mem.used", L.T("Used"));
        if (kind == WidgetKind.Gpu) Add("gpu.clock", L.T("Clock speed"));
        if (kind == WidgetKind.Gpu) { Add("gpu.vram.used", L.T("VRAM used")); Add("gpu.vram.total", L.T("VRAM total")); }
        if (kind == WidgetKind.Drives) { Add("drives.read", L.T("Read")); Add("drives.write", L.T("Write")); Add("drives.count", L.T("Drive count")); }
        if (kind == WidgetKind.Drives)
        {
            Add("drives.readactivity", L.T("Read activity")); Add("drives.writeactivity", L.T("Write activity")); Add("drives.totalactivity", L.T("Total activity"));
            Add("drives.free", L.T("Free space")); Add("drives.total", L.T("Total space"));
            Add("drives.powercount", L.T("Power-on count")); Add("drives.hours", L.T("Power-on hours")); Add("drives.life", L.T("Life left"));
        }
        if (kind == WidgetKind.Gpu) Add("gpu.junction", L.T("Memory junction"));
        if (kind == WidgetKind.Network) Add("net.quality", L.T("Connection quality"));
        if (kind == WidgetKind.Clock) Add("clock.week", L.T("Week"));
        if (kind == WidgetKind.Calendar) Add("calendar.week", L.T("Week"));
        if (kind == WidgetKind.WorldClocks) Add("world.offset", L.T("Time difference"));
        if (kind == WidgetKind.Weather) Add("weather.place", L.T("Place"));
        if (kind == WidgetKind.Fps) Add("fps.app", L.T("Application"));
        if (kind == WidgetKind.Power)
            foreach (var period in new[] { ("today", "Today"), ("week", "This week"), ("month", "This month") })
                Add("power." + period.Item1, L.T("Costs") + " · " + L.T(period.Item2));
        if (kind == WidgetKind.Media) { Add("media.progress", L.T("Progress")); Add("media.cover", L.T("Cover")); Add("media.source", L.T("Application")); }
        if (kind == WidgetKind.DataUsage) Add("data.period", L.T("Period"));
        if (kind == WidgetKind.DataUsage)
            foreach (string period in new[] { "week", "month" })
            {
                Add("data." + period + ".down", L.T(period == "week" ? "This week" : "This month") + " · " + L.T("Download"));
                Add("data." + period + ".up", L.T(period == "week" ? "This week" : "This month") + " · " + L.T("Upload"));
            }
        if (kind == WidgetKind.Cooling && snap != null)
        {
            foreach (var fan in snap.Fans) Add("fan:" + fan.Name, L.T("Fan") + " · " + Names.Fan(fan.Name, new Settings()));
            foreach (var temp in snap.BoardTemps) Add("temp:" + temp.Name, L.T("Temperature") + " · " + Names.Temp(temp.Name, new Settings()));
        }
        if (kind is WidgetKind.Drives or WidgetKind.DriveHealth && snap != null)
            foreach (var disk in snap.Disks) Add("drive:" + disk.Id, disk.Name + " " + disk.Letters);
        if (kind == WidgetKind.Volumes && snap != null)
            foreach (var volume in snap.Volumes) Add("volume:" + volume.Name, volume.Name + " " + volume.Label);
        return result;
    }

    // Name of the main value and of the second value of a widget ("Temperature", "Load", …).
    public static string PrimaryLabel(WidgetKind kind) => L.T(kind switch
        {
            WidgetKind.Cpu or WidgetKind.Gpu or WidgetKind.Weather or WidgetKind.Drives => "Temperature",
            WidgetKind.Network or WidgetKind.DataUsage => "Download", WidgetKind.Memory or WidgetKind.Processes => "Load",
            WidgetKind.Cooling => "Board temperatures", WidgetKind.Fps => "FPS", WidgetKind.Power => "Power",
            WidgetKind.Ping => "Ping", WidgetKind.Battery => "Battery", WidgetKind.System => "Uptime",
            WidgetKind.Volumes => "Used in %", WidgetKind.Clock or WidgetKind.WorldClocks => "Time",
            WidgetKind.Calendar => "Calendar", WidgetKind.Media => "Title", _ => ""
        });

    public static string SecondaryLabel(WidgetKind kind) => L.T(kind switch
        {
            WidgetKind.Cpu or WidgetKind.Gpu => "Load", WidgetKind.Network or WidgetKind.DataUsage => "Upload",
            WidgetKind.Memory => "Available", WidgetKind.Power => "Energy today", WidgetKind.Ping => "Router",
            WidgetKind.Fps => "1 % low", WidgetKind.Battery => "Charging state", WidgetKind.Cooling => "Fans",
            WidgetKind.Weather => "Weather condition", WidgetKind.System => "Operating system", WidgetKind.Processes => "RAM",
            WidgetKind.Clock => "Date", WidgetKind.Media => "Artist", _ => ""
        });

    public static Snapshot FilterSnapshot(Snapshot snap, Settings settings) => snap with
    {
        Disks = settings.ActiveWidget?.Kind == WidgetKind.Drives ? DriveOrdering.Order(snap.Disks, settings.ActiveWidget.DriveOrder) : snap.Disks,
        Fans = settings.Shows("cooling.fans") ? snap.Fans.Where(f => settings.Shows("fan:" + f.Name)).ToArray() : Array.Empty<FanInfo>(),
        BoardTemps = settings.Shows("cooling.board") ? snap.BoardTemps.Where(t => settings.Shows("temp:" + t.Name)).ToArray() : Array.Empty<TempReading>(),
    };

    public static Summary FilterSummary(WidgetKind kind, Summary summary, Settings settings)
    {
        if (kind == WidgetKind.Clock && !settings.Shows("clock.seconds")) summary = summary with { Unit = "" };
        if (!settings.Shows(PrimaryKey(kind))) summary = summary with { Value = "", Unit = "", Fraction = 0 };
        if (!settings.Shows(SecondaryKey(kind))) summary = summary with { Second = null, SecondLabel = "" };
        if (!settings.Shows(CaptionKey(kind))) summary = summary with { Caption = "" };
        if (Summaries.ChartKey(kind) is string chart && !settings.Shows(chart)) summary = summary with { Series = Array.Empty<double>() };
        return summary;
    }

    public static string MemoryCaption(MemoryInfo? memory, Settings settings)
    {
        var parts = new List<string>();
        if (settings.Shows("mem.used")) parts.Add(Format.Gigabytes(memory?.UsedGb));
        if (settings.Shows("mem.total")) parts.Add(Format.Gigabytes(memory?.TotalGb));
        return string.Join(" / ", parts);
    }

    public static string Vram(GpuInfo? gpu, Settings settings)
    {
        var parts = new List<string>();
        if (settings.Shows("gpu.vram.used")) parts.Add(Format.Gigabytes(gpu?.MemoryUsedMb / 1024.0));
        if (settings.Shows("gpu.vram.total")) parts.Add(Format.Gigabytes(gpu?.MemoryTotalMb / 1024.0));
        return string.Join(" / ", parts);
    }

    public static string ReadingKey(WidgetKind kind, string label)
    {
        if (kind is WidgetKind.Cpu or WidgetKind.Gpu)
        {
            string prefix = kind == WidgetKind.Cpu ? "cpu." : "gpu.";
            if (label == L.T("Temp")) return prefix + "temp";
            if (label == L.T("Load")) return prefix + "load";
            if (label == L.T("Power")) return prefix + "power";
            if (label == L.T("Hot spot")) return prefix + "hotspot";
        }
        if (kind == WidgetKind.Memory) return label == L.T("2-min peak") ? "mem.peak" : "mem.load";
        if (kind == WidgetKind.Network) return label.StartsWith("↑") ? "net.up" : "net.down";
        return PrimaryKey(kind);
    }
}
