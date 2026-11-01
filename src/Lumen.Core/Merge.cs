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

    // --- AIDA64 / HWiNFO -----------------------------------------------------------------------------
    // The chosen external program wins wherever it has a reading; names, ids and drive letters stay
    // the built-in ones, so widgets keep pointing at the same hardware when the source changes.

    public static CpuInfo? PreferCpu(CpuInfo? ext, CpuInfo? local)
    {
        if (ext is null) return local;
        if (local is null) return ext;
        return local with
        {
            Temperature = ext.Temperature ?? local.Temperature,
            Load = ext.Load ?? local.Load,
            CoreLoads = ext.CoreLoads.Count > 0 ? ext.CoreLoads : local.CoreLoads,
            ClockMhz = ext.ClockMhz ?? local.ClockMhz,
            PowerW = ext.PowerW ?? local.PowerW,
            Voltage = ext.Voltage ?? local.Voltage,
            FanRpm = ext.FanRpm ?? local.FanRpm
        };
    }

    // A card with its own fan, power or hot spot reading is a dedicated one.
    private static int Dedicated(GpuInfo g) => g.Integrated ? 0 : (g.FanRpm != null ? 1 : 0) + (g.PowerW != null ? 1 : 0) + (g.HotSpot != null ? 1 : 0) + (g.MemoryUsedMb > 1024 ? 1 : 0);

    public static List<GpuInfo> PreferGpus(IReadOnlyList<GpuInfo> ext, IReadOnlyList<GpuInfo> local, string source, List<string>? log = null)
    {
        var unused = ext.ToList();
        var result = new List<GpuInfo>();
        foreach (var p in local)
        {
            var m = unused.FirstOrDefault(e => Key(e.Name) == Key(p.Name));
            // AIDA64 does not name its cards: pair them by kind and order, dedicated cards first.
            m ??= p.Integrated
                ? unused.FirstOrDefault(e => e.Integrated) ?? (local.Count(x => !x.Integrated) == 0 ? unused.FirstOrDefault() : null)
                : unused.Where(e => !e.Integrated).OrderByDescending(Dedicated).FirstOrDefault();
            if (m == null) { result.Add(p); continue; }
            unused.Remove(m);
            var preferred = p with
            {
                Temperature = m.Temperature ?? p.Temperature,
                HotSpot = m.HotSpot ?? p.HotSpot,
                Load = m.Load ?? p.Load,
                ClockMhz = m.ClockMhz ?? p.ClockMhz,
                PowerW = m.PowerW ?? p.PowerW,
                FanRpm = m.FanRpm ?? p.FanRpm,
                MemoryUsedMb = m.MemoryUsedMb ?? p.MemoryUsedMb,
                MemoryTotalMb = m.MemoryTotalMb ?? p.MemoryTotalMb
            };
            log?.Add($"{p.Name}: readings from {source}");
            result.Add(preferred);
        }
        // Cards the built-in libraries missed entirely.
        foreach (var e in unused.Where(e => e.Temperature != null || e.Load != null))
        {
            log?.Add($"{e.Name}: only found by {source}");
            result.Add(e);
        }
        return result;
    }

    // "Samsung SSD 970 EVO Plus 1TB" matches "Samsung SSD 970 EVO Plus 1TB NVMe" and the like.
    private static bool SameDrive(string a, string b)
    {
        string x = Key(a), y = Key(b);
        return x.Length >= 6 && y.Length >= 6 && (x.Contains(y) || y.Contains(x));
    }

    public static List<DiskInfo> PreferDisks(IReadOnlyList<DiskInfo> ext, IReadOnlyList<DiskInfo> local, string source, List<string>? log = null)
    {
        var unused = ext.ToList();
        var result = new List<DiskInfo>();
        foreach (var p in local)
        {
            // The same Windows disk number first; then the model, identical models paired one by one.
            int? number = SensorPicker.DiskNumber(p.Id);
            var m = unused.FirstOrDefault(e => number != null && SensorPicker.DiskNumber(e.Id) == number)
                    ?? unused.FirstOrDefault(e => (number is null || SensorPicker.DiskNumber(e.Id) is null) && SameDrive(e.Name, p.Name));
            if (m == null) { result.Add(p); continue; }
            unused.Remove(m);
            var preferred = p with
            {
                Temperature = m.Temperature ?? p.Temperature,
                UsedPercent = p.UsedPercent ?? m.UsedPercent,
                ReadBytesPerSecond = m.ReadBytesPerSecond ?? p.ReadBytesPerSecond,
                WriteBytesPerSecond = m.WriteBytesPerSecond ?? p.WriteBytesPerSecond
            };
            log?.Add($"{p.Name}: readings from {source}");
            result.Add(preferred);
        }
        foreach (var e in unused.Where(e => e.Temperature != null))
        {
            log?.Add($"{e.Name}: only found by {source}");
            result.Add(e);
        }
        return result;
    }

    // A physical disk as Windows numbers it, with the model and serial number the drive reports.
    public sealed record PhysicalDisk(int Number, string Model, string Serial);

    // AIDA64 and HWiNFO name drives "<model> (<serial>)" but number them their own way. The serial number
    // finds the matching Windows disk (and so its drive letters); identical models without a usable serial
    // fall back to the first free disk of that model.
    public static int? MatchPhysical(string name, IReadOnlyList<PhysicalDisk> disks, ICollection<int> taken)
    {
        string key = Key(name);
        foreach (var d in disks.Where(d => !taken.Contains(d.Number)))
        {
            string serial = Key(d.Serial);
            if (serial.Length < 6) continue;
            // Older Windows versions report SATA serials with each pair of characters swapped.
            string swapped = new(serial.Chunk(2).SelectMany(c => c.Reverse()).ToArray());
            if (key.Contains(serial) || key.Contains(swapped)) return d.Number;
        }
        string model = WithoutSerial(name);
        return disks.Where(d => !taken.Contains(d.Number) && SameDrive(d.Model, model)).Select(d => (int?)d.Number).FirstOrDefault();
    }

    // "Samsung SSD 970 EVO Plus 2TB (S4J4NJ0N808240F)" → "Samsung SSD 970 EVO Plus 2TB"; "(USB)" and the like stay.
    public static string WithoutSerial(string name) => System.Text.RegularExpressions.Regex.Replace(name, @"\s*\([A-Za-z0-9_.\-]{6,}\)\s*$", "").Trim();

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
