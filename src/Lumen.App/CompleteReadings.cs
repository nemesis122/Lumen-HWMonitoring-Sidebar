using Lumen.Core;

namespace Lumen.App;

public sealed record CompleteReading(string Key, string Label, string Value, double? Fraction = null, bool Address = false)
{
    public string SensorKey { get; init; } = Key;
    public bool Available => Value.Length > 0 && !Value.StartsWith("—");
}

// Canonical selected readings, shared by native designs and their completeness fallback.
public static class CompleteReadings
{
    public static List<CompleteReading> Body(WidgetKind kind, int index, Snapshot s, History h, Settings set, Palette p, WidgetStyle? style = null) =>
        For(kind, index, s, h, set, p).Where(r => kind != WidgetKind.Drives || !r.Key.EndsWith(":model")
            && !(style.HasValue && DriveTransferPairs.Applies(style.Value) && DriveTransferPairs.IsRate(r))).ToList();
    public static List<CompleteReading> For(WidgetKind kind, int index, Snapshot s, History h, Settings set, Palette p)
    {
        var rows = new List<CompleteReading>();
        void Add(string key, string label, string value, double? fraction = null, string? identity = null, bool address = false)
        {
            string id = identity ?? key;
            if (set.Shows(key) && rows.All(r => r.Key != id)) rows.Add(new(id, label, value, fraction.HasValue && double.IsFinite(fraction.Value) ? Math.Clamp(fraction.Value, 0, 1) : null, address) { SensorKey = key });
        }
        string Temp(double? value) => value.HasValue ? Format.Temperature(value, set.Fahrenheit) + " " + Format.TemperatureUnit(set.Fahrenheit) : "—";
        double? Peak(string key) => h.Read(key).Where(double.IsFinite).Select(x => (double?)x).DefaultIfEmpty(null).Max();
        switch (kind)
        {
            case WidgetKind.Media:
                Add("media.title", "Title", s.Media?.Title ?? "—");
                Add("media.artist", "Artist", s.Media?.Artist ?? "—");
                Add("media.source", "Application", s.Media?.App ?? "—");
                Add("media.progress", "Playback", s.Media?.PositionSeconds is double seconds ? Format.Duration(TimeSpan.FromSeconds(seconds)) + " / " + (s.Media.DurationSeconds is double duration ? Format.Duration(TimeSpan.FromSeconds(duration)) : "—") : "—");
                break;
            case WidgetKind.Calendar:
                Add("calendar.date", "Date", s.Time.ToString("D", L.Culture));
                Add("calendar.week", "Week", System.Globalization.ISOWeek.GetWeekOfYear(s.Time).ToString());
                break;
            case WidgetKind.Weather:
                Add("weather.temp", "Temperature", Temp(s.Weather?.TemperatureC));
                Add("weather.place", "Place", s.Weather?.Place ?? "—");
                Add("weather.sky", "Conditions", s.Weather == null ? "—" : Summaries.Sky(s.Weather.Code));
                Add("weather.feels", "Feels like", Temp(s.Weather?.FeelsLikeC));
                Add("weather.wind", "Wind", s.Weather == null ? "—" : Format.Number(s.Weather.WindKmh) + " km/h");
                Add("weather.humidity", "Humidity", Format.Percent(s.Weather?.Humidity));
                if (set.Shows("weather.forecast") && s.Weather != null) foreach (var day in s.Weather.Days)
                    rows.Add(new("weather:" + day.Date.ToString("yyyy-MM-dd"), day.Date.ToString("ddd", L.Culture), Temp(day.MinC) + " / " + Temp(day.MaxC)));
                break;
            case WidgetKind.Clock:
                Add("clock.time", "Time", s.Time.ToString("HH:mm", L.Culture));
                Add("clock.seconds", "Seconds", s.Time.ToString("ss", L.Culture));
                Add("clock.date", "Date", s.Time.ToString("D", L.Culture));
                Add("clock.week", "Week", System.Globalization.ISOWeek.GetWeekOfYear(s.Time).ToString());
                Add("clock.day", "Day progress", Format.Percent(s.Time.TimeOfDay.TotalHours / 24 * 100), s.Time.TimeOfDay.TotalHours / 24);
                break;
            case WidgetKind.WorldClocks:
                foreach (string zone in set.WorldClocks)
                    if (TimeZoneInfo.TryFindSystemTimeZoneById(zone, out var tz))
                    {
                        var time = TimeZoneInfo.ConvertTime(s.Time, tz);
                        Add("world.time", WorldClocksCard.City(zone), time.ToString("HH:mm", L.Culture), identity: "world:" + zone + ":time");
                        Add("world.offset", WorldClocksCard.City(zone) + " · Offset", (time - s.Time).TotalHours.ToString("+0.#;-0.#;0", L.Culture) + " h", identity: "world:" + zone + ":offset");
                    }
                break;
            case WidgetKind.Processes:
                for (int i = 0; i < s.Processes.Count; i++)
                {
                    var process = s.Processes[i];
                    Add("proc.cpu", process.Name + " · CPU", Format.Percent(process.CpuPercent), process.CpuPercent / 100, "process:" + i + ":cpu");
                    Add("proc.memory", process.Name + " · RAM", Format.Gigabytes(process.MemoryMb / 1024), identity: "process:" + i + ":ram");
                }
                break;
            case WidgetKind.System:
                Add("system.uptime", "Uptime", s.System == null ? "—" : Format.Duration(s.System.Uptime));
                Add("system.os", "Operating system", s.System == null ? "—" : s.System.Os + " " + s.System.Build);
                Add("system.boot", "Started", s.System?.BootTime.ToString("g", L.Culture) ?? "—");
                Add("system.processes", "Processes", Format.Number(s.System?.Processes));
                Add("system.threads", "Threads", Format.Number(s.System?.Threads));
                break;
            case WidgetKind.Cpu:
                var cpu = s.Cpu;
                Add("cpu.temp", "Temp", Temp(cpu?.Temperature));
                Add("cpu.load", "Load", Format.Percent(cpu?.Load), cpu?.Load / 100);
                Add("cpu.clock", "Clock", Format.Clock(cpu?.ClockMhz));
                Add("cpu.power", "Power", Format.Watts(cpu?.PowerW));
                Add("cpu.voltage", "Voltage", Format.Volts(cpu?.Voltage));
                Add("cpu.fan", "Fan", Format.Rpm(cpu?.FanRpm));
                break;
            case WidgetKind.Gpu:
                var gpu = GpuGroup.Visible(s, set).ElementAtOrDefault(index);
                Add("gpu.temp", "Temp", Temp(gpu?.Temperature));
                Add("gpu.load", "Load", Format.Percent(gpu?.Load), gpu?.Load / 100);
                Add("gpu.clock", "Clock", Format.Clock(gpu?.ClockMhz));
                Add("gpu.power", "Power", Format.Watts(gpu?.PowerW));
                Add("gpu.hotspot", "Hot spot", Temp(gpu?.HotSpot));
                Add("gpu.fan", "Fan", Format.Rpm(gpu?.FanRpm));
                    Add("gpu.vram.used", "VRAM used", Format.Gigabytes(gpu?.MemoryUsedMb / 1024));
                    Add("gpu.vram.total", "VRAM total", Format.Gigabytes(gpu?.MemoryTotalMb / 1024));
                Add("gpu.junction", "Memory junction", Temp(gpu?.MemoryJunction));
                break;
            case WidgetKind.Memory:
                Add("mem.load", "Load", Format.Percent(s.Memory?.Load), s.Memory?.Load / 100);
                Add("mem.used", "Used", Format.Gigabytes(s.Memory?.UsedGb));
                Add("mem.free", "Available", Format.Gigabytes(s.Memory?.AvailableGb));
                Add("mem.total", "Installed", Format.Gigabytes(s.Memory?.TotalGb));
                Add("mem.peak", "2-min peak", Format.Percent(Peak("mem.load")), Peak("mem.load") / 100);
                break;
            case WidgetKind.Network:
                Add("net.down", "DL", Format.Rate(s.Network?.DownloadBytesPerSecond));
                Add("net.up", "UP", Format.Rate(s.Network?.UploadBytesPerSecond));
                Add("net.link", "Link speed", Format.Link(s.Network?.LinkBitsPerSecond));
                Add("net.use", "Link use", Format.Bits((s.Network?.DownloadBytesPerSecond + s.Network?.UploadBytesPerSecond) * 8));
                Add("net.peak", "DL peak · 2 min", Format.Rate(Peak("net.down")));
                Add("net.address", "Internal IP", s.Network?.Address ?? "—", address: true);
                Add("net.external", "External IP", ExternalIp.Address ?? "—", address: true);
                // Same ping-derived status as the native network panel; throughput is not a quality test.
                Add("net.quality", "Connection quality", s.Ping?.Ms is double ms && double.IsFinite(ms)
                    ? L.T(s.Ping.LossPercent < 1 && ms < 60 ? "Working well" : "Unstable") : "—");
                break;
            case WidgetKind.Drives:
                var disks = DriveOrdering.Order(s.Disks, DriveOrdering.Mode(set)).Where(d => set.Shows("drive:" + d.Id)).ToArray();
                Add("drives.count", "Drives", disks.Length.ToString());
                foreach (var disk in disks)
                {
                    string name = disk.Letters.Length > 0 ? disk.Letters : Names.Short(disk.Name);
                    string id = "drive:" + disk.Id;
                    if (set.Shows("drives.model") && Naming.ShowHardware(set.ActiveWidget, set)) rows.Add(new(id + ":model", name + " · Model", disk.Name));
                    Add("drives.temp", name + " · Temperature", Temp(disk.Temperature), identity: id + ":temp");
                    Add("drives.used", name + " · Used", Format.Percent(disk.UsedPercent), disk.UsedPercent / 100, id + ":used");
                        Add("drives.read", name + " · Read", Format.Rate(disk.ReadBytesPerSecond), identity: id + ":read");
                        Add("drives.write", name + " · Write", Format.Rate(disk.WriteBytesPerSecond), identity: id + ":write");
                    Add("drives.readactivity", name + " · Read activity", Format.Percent(disk.ReadActivity), disk.ReadActivity / 100, id + ":readactivity");
                    Add("drives.writeactivity", name + " · Write activity", Format.Percent(disk.WriteActivity), disk.WriteActivity / 100, id + ":writeactivity");
                    Add("drives.totalactivity", name + " · Total activity", Format.Percent(disk.TotalActivity), disk.TotalActivity / 100, id + ":totalactivity");
                    var letters = disk.Letters.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(x => x.TrimEnd('\\')).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var volumes = s.Volumes.Where(v => letters.Contains(v.Name.TrimEnd('\\'))).ToArray();
                    double capacity = volumes.Sum(v => v.TotalGb);
                    Add("drives.free", name + " · Free space", volumes.Length > 0 ? Format.Capacity(volumes.Sum(v => v.FreeGb)) : "—", capacity > 0 ? volumes.Sum(v => v.FreeGb) / capacity : null, identity: id + ":free");
                    Add("drives.total", name + " · Total space", volumes.Length > 0 ? Format.Capacity(capacity) : "—", capacity > 0 ? 1 : null, identity: id + ":total");
                    Add("drives.powercount", name + " · Power-on count", Format.Number(disk.PowerOnCount, "0"), identity: id + ":powercount");
                    Add("drives.hours", name + " · Power-on hours", disk.PowerOnHours.HasValue ? Format.Number(disk.PowerOnHours, "0") + " h" : "—", identity: id + ":hours");
                    Add("drives.life", name + " · Life left", Format.Percent(disk.Life), disk.Life / 100, id + ":life");
                }
                break;
            case WidgetKind.Volumes:
                foreach (var volume in s.Volumes.Where(v => set.Shows("volume:" + v.Name)))
                {
                    string name = volume.Name + (set.Shows("volumes.label") ? " " + volume.Label : "");
                    Add("volumes.used", name + " · Used", Format.Percent(volume.UsedPercent), volume.UsedPercent / 100, "volume:" + volume.Name + ":used");
                    Add("volumes.free", name + " · Free", Format.Capacity(volume.FreeGb), identity: "volume:" + volume.Name + ":free");
                    Add("volumes.detail", name + " · Used / total", Format.Capacity(volume.TotalGb - volume.FreeGb) + " / " + Format.Capacity(volume.TotalGb), identity: "volume:" + volume.Name + ":total");
                }
                break;
            case WidgetKind.Cooling:
                if (set.Shows("cooling.fans")) foreach (var fan in s.Fans.Where(f => set.Shows("fan:" + f.Name)))
                {
                    rows.Add(new("fan:" + fan.Name, Names.Fan(fan.Name, set), Format.Rpm(fan.Rpm), fan.Percent / 100));
                    if (fan.Percent.HasValue) rows.Add(new("fan:" + fan.Name + ":percent", Names.Fan(fan.Name, set) + " · Speed", Format.Percent(fan.Percent), fan.Percent / 100));
                }
                if (set.Shows("cooling.board")) foreach (var temp in s.BoardTemps.Where(t => set.Shows("temp:" + t.Name))) rows.Add(new("temp:" + temp.Name, Names.Temp(temp.Name, set), Temp(temp.Celsius)));
                break;
            case WidgetKind.Battery:
                Add("battery.level", "Battery", Format.Percent(s.Battery?.Percent), s.Battery?.Percent / 100);
                Add("battery.state", "Charging state", s.Battery == null ? "—" : L.T(s.Battery.Charging ? "Charging" : s.Battery.OnAc ? "Plugged in" : "On battery"));
                Add("battery.time", "Time left", s.Battery?.MinutesLeft is double minutes ? Format.Duration(TimeSpan.FromMinutes(minutes)) : "—");
                Add("battery.rate", "Rate", Format.Watts(s.Battery?.PowerW));
                Add("battery.health", "Health", Format.Percent(100 - s.Battery?.WearPercent));
                break;
            case WidgetKind.Fps:
                Add("fps.value", "FPS", Format.Number(s.Fps?.Fps) + (s.Fps == null ? "" : " fps"));
                Add("fps.low", "1 % low", Format.Number(s.Fps?.Low1) + (s.Fps?.Low1 == null ? "" : " fps"));
                Add("fps.frametime", "Frame time", Format.Milliseconds(s.Fps?.FrameTimeMs));
                Add("fps.app", "Application", s.Fps?.App ?? "—");
                break;
            case WidgetKind.Ping:
                Add("ping.latency", "Ping", Format.Milliseconds(s.Ping?.Ms));
                Add("ping.router", "Router", Format.Milliseconds(s.Ping?.GatewayMs));
                Add("ping.jitter", "Jitter", Format.Milliseconds(s.Ping?.JitterMs));
                Add("ping.loss", "Loss", Format.Percent(s.Ping?.LossPercent));
                Add("ping.target", "Target", s.Ping?.Target ?? set.PingTarget);
                break;
            case WidgetKind.Power:
                Add("power.watts", "Power", Format.Watts(s.Usage?.WattsNow));
                Add("power.energy", "Energy today", s.Usage == null ? "—" : Format.Number(s.Usage.KwhToday, "0.00") + " kWh");
                Add("power.today", "Today", s.Usage == null ? "—" : Format.Money(s.Usage.KwhToday * set.PricePerKwh, set.Currency));
                Add("power.week", "This week", s.Usage == null ? "—" : Format.Money(s.Usage.KwhWeek * set.PricePerKwh, set.Currency));
                Add("power.month", "This month", s.Usage == null ? "—" : Format.Money(s.Usage.KwhMonth * set.PricePerKwh, set.Currency));
                break;
            case WidgetKind.DataUsage:
                Add("data.down", "Download", Format.Size(s.Usage?.DownToday));
                Add("data.up", "Upload", Format.Size(s.Usage?.UpToday));
                Add("data.period", "Period", L.T("Today"));
                Add("data.week.down", L.T("This week") + " ↓", Format.Size(s.Usage?.DownWeek));
                Add("data.week.up", L.T("This week") + " ↑", Format.Size(s.Usage?.UpWeek));
                Add("data.month.down", L.T("This month") + " ↓", Format.Size(s.Usage?.DownMonth));
                Add("data.month.up", L.T("This month") + " ↑", Format.Size(s.Usage?.UpMonth));
                break;
            case WidgetKind.DriveHealth:
                foreach (var disk in s.Disks.Where(d => set.Shows("drive:" + d.Id)))
                {
                    string name = disk.Letters.Length > 0 ? disk.Letters : Names.Short(disk.Name);
                    string id = "drive:" + disk.Id;
                    Add("health.life", name + " · Life left", Format.Percent(disk.Life), disk.Life / 100, id + ":life");
                    Add("health.hours", name + " · Power-on hours", disk.PowerOnHours.HasValue ? Format.Number(disk.PowerOnHours, "0") + " h" : "—", identity: id + ":hours");
                    Add("health.count", name + " · Power-on count", Format.Number(disk.PowerOnCount, "0"), identity: id + ":powercount");
                    Add("health.temp", name + " · Temperature", Temp(disk.Temperature), identity: id + ":temp");
                }
                break;
            default:
                var sum = Summaries.For(kind, index, s, h, set, p);
                Add(WidgetSensors.PrimaryKey(kind), sum.Title, (sum.Value + " " + sum.Unit).Trim(), sum.Fraction);
                if (sum.Second.HasValue) Add(WidgetSensors.SecondaryKey(kind), "Load", sum.SecondLabel, sum.Second);
                else if (sum.Caption.Length > 0) Add(WidgetSensors.CaptionKey(kind), "Details", sum.Caption);
                int n = 0;
                foreach (var extra in Summaries.Extras(kind, index, s, h, set)) rows.Add(new("extra:" + n++, extra.Label, extra.Value));
                if (kind == WidgetKind.Fps)
                {
                    Add("fps.low", "1% low", Format.Number(s.Fps?.Low1) + " fps");
                    Add("fps.app", "Application", s.Fps?.App ?? "—");
                }
                if (kind == WidgetKind.Ping) Add("ping.router", "Router", Format.Milliseconds(s.Ping?.GatewayMs));
                if (kind == WidgetKind.Clock && set.Shows("clock.seconds")) rows.Add(new("clock.seconds", "Seconds", s.Time.ToString("ss", L.Culture)));
                if (kind == WidgetKind.Weather && set.Shows("weather.forecast") && s.Weather != null) foreach (var day in s.Weather.Days)
                    rows.Add(new("weather:" + day.Date.ToString("yyyy-MM-dd"), day.Date.ToString("ddd", L.Culture), Temp(day.MinC) + " / " + Temp(day.MaxC)));
                if (kind == WidgetKind.Media && set.Shows("media.progress")) rows.Add(new("media.progress", "Playback", s.Media?.PositionSeconds is double sec ? Format.Duration(TimeSpan.FromSeconds(sec)) + " / " + (s.Media.DurationSeconds is double dur ? Format.Duration(TimeSpan.FromSeconds(dur)) : "—") : "—"));
                break;
        }
        // The names are written in English above; every part ("C: · Temperature") is shown in the
        // sidebar's language.
        return Steady.Keep("complete|" + kind + "|" + index, set, s, rows.Select(r => r with { Label = Translate(r.Label) }).ToList(),
            r => r.Key, r => r.Value.Length == 0 || r.Value.StartsWith("—"));
    }

    private static string Translate(string label) =>
        L.T(label) is var whole && whole != label ? whole : string.Join(" · ", label.Split(" · ").Select(part => L.T(part)));
}
