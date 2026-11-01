using System.Globalization;
using System.Text;

namespace Lumen.Core;

// Combines readings of the same hardware from two libraries: the first source wins, the second
// only fills what the first could not read, and hardware only the second one sees is added.
// Identical models (two of the same SSD) are paired one by one, never collapsed.
public static class SensorMerge
{
    public static string Key(string? name) => new((name ?? "").Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    public static List<GpuInfo> Gpus(IReadOnlyList<GpuInfo> primary, IReadOnlyList<GpuInfo> extra, List<string>? log = null)
    {
        var result = new List<GpuInfo>();
        var unused = extra.ToList();
        foreach (var p in primary)
        {
            var m = unused.FirstOrDefault(e => Key(e.Name) == Key(p.Name));
            // One dedicated card on each side is the same card even if the names differ slightly.
            if (m == null && !p.Integrated && primary.Count(x => !x.Integrated) == 1 && unused.Count == 1) m = unused[0];
            if (m == null) { result.Add(p); continue; }
            unused.Remove(m);
            var filled = p with
            {
                Temperature = p.Temperature ?? m.Temperature,
                HotSpot = p.HotSpot ?? m.HotSpot,
                Load = p.Load ?? m.Load,
                ClockMhz = p.ClockMhz ?? m.ClockMhz,
                PowerW = p.PowerW ?? m.PowerW,
                FanRpm = p.FanRpm ?? m.FanRpm,
                MemoryUsedMb = p.MemoryUsedMb ?? m.MemoryUsedMb,
                MemoryTotalMb = p.MemoryTotalMb ?? m.MemoryTotalMb
            };
            if (log != null && filled != p) log.Add($"{p.Name}: {Filled(p, filled)} from Open Hardware Monitor");
            result.Add(filled);
        }
        foreach (var e in unused)
        {
            log?.Add($"{e.Name}: only found by Open Hardware Monitor");
            result.Add(e);
        }
        return result;
    }

    public static List<DiskInfo> Disks(IReadOnlyList<DiskInfo> primary, IReadOnlyList<DiskInfo> extra, List<string>? log = null)
    {
        var result = new List<DiskInfo>();
        var unused = extra.ToList();
        foreach (var p in primary)
        {
            var m = unused.FirstOrDefault(e => Key(e.Name) == Key(p.Name));
            if (m == null) { result.Add(p); continue; }
            unused.Remove(m);
            var filled = p with
            {
                Temperature = p.Temperature ?? m.Temperature,
                UsedPercent = p.UsedPercent ?? m.UsedPercent,
                ReadBytesPerSecond = p.ReadBytesPerSecond ?? m.ReadBytesPerSecond,
                WriteBytesPerSecond = p.WriteBytesPerSecond ?? m.WriteBytesPerSecond
            };
            if (log != null && filled != p) log.Add($"{p.Name}: {Filled(p, filled)} from Open Hardware Monitor");
            result.Add(filled);
        }
        foreach (var e in unused.Where(e => e.Temperature != null || e.UsedPercent != null))
        {
            log?.Add($"{e.Name}: only found by Open Hardware Monitor");
            result.Add(e);
        }
        return result;
    }

    // Names the fields that changed from missing to present, e.g. "temperature, hot spot".
    private static string Filled<T>(T before, T after)
    {
        var names = typeof(T).GetProperties()
            .Where(prop => prop.PropertyType == typeof(double?) && prop.GetValue(before) == null && prop.GetValue(after) != null)
            .Select(prop => prop.Name);
        return string.Join(", ", names);
    }
}

// A plain-text picture of what Lumen shows, for the sensor report.
public static class SnapshotText
{
    public static string Describe(Snapshot s)
    {
        var b = new StringBuilder();
        string N(double? v, string unit = "") => v is double x && double.IsFinite(x) ? x.ToString("0.##", CultureInfo.InvariantCulture) + unit : "—";
        b.AppendLine($"Access: {s.Access}");
        if (s.Cpu is { } c)
            b.AppendLine($"CPU  {c.Name}: temp {N(c.Temperature, " °C")}, load {N(c.Load, " %")}, cores {c.CoreLoads.Count}, clock {N(c.ClockMhz, " MHz")}, power {N(c.PowerW, " W")}, voltage {N(c.Voltage, " V")}, fan {N(c.FanRpm, " rpm")}");
        else b.AppendLine("CPU  not found");
        foreach (var g in s.Gpus)
            b.AppendLine($"GPU  {g.Name}{(g.Integrated ? " (integrated)" : "")}: temp {N(g.Temperature, " °C")}, hot spot {N(g.HotSpot, " °C")}, load {N(g.Load, " %")}, clock {N(g.ClockMhz, " MHz")}, power {N(g.PowerW, " W")}, fan {N(g.FanRpm, " rpm")}, VRAM {N(g.MemoryUsedMb, " MB")} / {N(g.MemoryTotalMb, " MB")}");
        if (s.Gpus.Count == 0) b.AppendLine("GPU  not found");
        if (s.Memory is { } m) b.AppendLine($"RAM  {N(m.UsedGb, " GB")} of {N(m.TotalGb, " GB")}");
        foreach (var d in s.Disks) b.AppendLine($"DISK {d.Name}{(d.Letters.Length > 0 ? " [" + d.Letters + "]" : " [no letter]")} ({d.Id}): temp {N(d.Temperature, " °C")}, used {N(d.UsedPercent, " %")}, read {N(d.ReadBytesPerSecond, " B/s")}, write {N(d.WriteBytesPerSecond, " B/s")}");
        if (s.Network is { } n) b.AppendLine($"NET  {n.Name}: down {N(n.DownloadBytesPerSecond, " B/s")}, up {N(n.UploadBytesPerSecond, " B/s")}, link {N(n.LinkBitsPerSecond, " bit/s")}, {n.Address}");
        if (s.BoardName != null) b.AppendLine($"BOARD {s.BoardName}: " + string.Join(", ", s.BoardTemps.Select(t => $"{t.Name} {N(t.Celsius, " °C")}")));
        foreach (var f in s.Fans) b.AppendLine($"FAN  {f.Name}: {N(f.Rpm, " rpm")}");
        if (s.Battery is { } bat) b.AppendLine($"BATTERY {N(bat.Percent, " %")}, AC {bat.OnAc}, charging {bat.Charging}, rate {N(bat.PowerW, " W")}, wear {N(bat.WearPercent, " %")}");
        return b.ToString();
    }
}
