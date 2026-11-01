using System.Globalization;
using System.Text.RegularExpressions;

namespace Lumen.Core;

// A library-neutral picture of one hardware item: LibreHardwareMonitor, Open Hardware Monitor and the
// text of a sensor report all become these nodes, so the choice of readings can be tested without Windows.
// Type names follow LibreHardwareMonitor ("Cpu", "GpuNvidia", "Temperature", "Fan" …).
public sealed record SensorValue(string Type, string Name, double? Value, int Index = 0, string Id = "");

public sealed record SensorNode(string Type, string Name, string Id, IReadOnlyList<SensorValue> Sensors, IReadOnlyList<SensorNode> Sub)
{
    public IEnumerable<SensorValue> All => Sensors.Concat(Sub.SelectMany(s => s.All));
    public bool IsBoard => Type is "Motherboard";
    public bool IsGpu => Type is "GpuNvidia" or "GpuAmd" or "GpuIntel";
}

// Which sensor stands for what, across makers and generations. Names come from the LibreHardwareMonitor
// 0.9.x sources: Intel "CPU Package"/"P-Core #1", AMD "Core (Tctl/Tdie)"/"Core #1", mainboard configs from
// dozens of vendors ("VRM MOS", "PCH", "System #1", "CPU Optional Fan", "AIO Pump" …).
public static class SensorPicker
{
    private const StringComparison I = StringComparison.OrdinalIgnoreCase;

    // One sensor per core: "Core #3", "CPU Core #3", "P-Core #3", "E-Core #3" – not "(Effective)", "Max" or "Thread".
    public static readonly Regex PerCore = new(@"^(CPU |P-|E-)?Core #\d+$", RegexOptions.IgnoreCase);

    private static bool Ok(SensorValue s) => s.Value is double v && double.IsFinite(v);

    public static double? Find(IEnumerable<SensorValue> sensors, string type, params string[] names)
    {
        var list = sensors.Where(s => s.Type == type && Ok(s)).ToList();
        foreach (var n in names)
        {
            var hit = list.FirstOrDefault(s => s.Name.Trim().Equals(n, I));
            if (hit != null) return hit.Value;
        }
        return null;
    }

    private static bool Contains(string name, params string[] parts) => parts.Any(p => name.Contains(p, I));

    // --- Processor ---------------------------------------------------------------------------------

    public static CpuInfo? Cpu(IReadOnlyList<SensorNode> hardware, double? systemLoad = null)
    {
        var h = hardware.FirstOrDefault(x => x.Type == "Cpu");
        if (h is null) return null;
        var sensors = h.All.ToList();
        var boards = hardware.Where(x => x.IsBoard).SelectMany(x => x.All).ToList();

        // The CPU temperature is the mainboard's "CPU" probe, as AIDA64 and the BIOS label it – not the die
        // or package diode, which jumps with every short burst of load. Without such a probe, the processor's own.
        double? temp = Find(boards.Where(s => s.Value is > 0 and < 125), "Temperature", "CPU", "CPU Socket", "CPU (PECI)");
        // 0 °C is what the libraries report without the driver – never a real processor temperature.
        temp ??= Find(sensors.Where(s => s.Value is > 0 and < 125), "Temperature", "Core (Tctl/Tdie)", "CPU Package", "Package", "Core (Tdie)", "Core (Tctl)",
            "Core Max", "Core Average", "Tdie", "Tctl", "CCDs Max (Tdie)", "CCD1 (Tdie)", "CPU Cores", "CPU Core");
        // Unknown naming: the hottest core or die reading the CPU reports (never "Distance to TjMax").
        temp ??= sensors.Where(s => s.Type == "Temperature" && s.Value is > 0 and < 125 && !Contains(s.Name, "Distance", "TjMax", "SoC"))
            .Select(s => s.Value).Max();
        // Still nothing (no driver): the mainboard's own CPU probe is better than no reading.
        temp ??= Find(boards.Where(s => s.Value is > 0 and < 125), "Temperature", "CPU", "CPU Package", "CPU (PECI)", "CPU Core", "CPU Socket", "CPU Die");
        temp ??= boards.Where(s => s.Type == "Temperature" && s.Value is > 0 and < 125 && s.Name.Contains("CPU", I)).Select(s => s.Value).FirstOrDefault();

        // One bar per logical processor where the library reports threads, otherwise per core.
        var threads = sensors.Where(s => s.Type == "Load" && s.Name.Contains("Thread #", I) && Ok(s)).ToList();
        var coreSensors = threads.Count > 0 ? threads : sensors.Where(s => s.Type == "Load" && PerCore.IsMatch(s.Name.Trim()) && Ok(s)).ToList();
        var cores = coreSensors.OrderBy(s => s.Index).Select(s => Math.Clamp(s.Value!.Value, 0, 100)).ToArray();
        double? load = Find(sensors, "Load", "CPU Total") ?? (cores.Length > 0 ? cores.Average() : (double?)null) ?? systemLoad;

        // Per-core clocks only, never the "(Effective)" ones (time-averaged, far lower at idle).
        var clocks = sensors.Where(s => s.Type == "Clock" && PerCore.IsMatch(s.Name.Trim()) && s.Value is > 50 and < 10000).Select(s => s.Value!.Value).ToList();
        double? clock = Find(sensors.Where(s => s.Value > 50), "Clock", "Cores (Average)") ?? (clocks.Count > 0 ? clocks.Average() : (double?)null);

        double? power = Find(sensors.Where(s => s.Value > 0), "Power", "Package", "CPU Package", "CPU Cores", "Core (SVI2 TFN)", "CPU PPT")
            ?? sensors.Where(s => s.Type == "Power" && s.Value > 0 && !Contains(s.Name, "#", "Graphics", "Memory", "Platform", "SoC")).Select(s => s.Value).FirstOrDefault();

        var sane = sensors.Where(s => s.Value is > .3 and < 2).ToList();
        double? volts = Find(sane, "Voltage", "Core (SVI2 TFN)", "Core (SVI3 TFN)", "CPU Core", "Core", "CPU Cores", "Vcore")
            // Intel: one voltage per core ("P-Core #1"); AMD: the requested voltage per core ("Core #1 VID").
            ?? sane.Where(s => s.Type == "Voltage" && PerCore.IsMatch(s.Name.Trim())).Select(s => s.Value).Max()
            ?? sane.Where(s => s.Type == "Voltage" && s.Name.EndsWith(" VID", I)).Select(s => s.Value).Max()
            ?? boards.Where(s => s.Type == "Voltage" && s.Value is > .3 and < 2 && (Contains(s.Name, "Vcore", "CPU VCORE") || s.Name.Equals("CPU Core", I)))
                .Select(s => s.Value).FirstOrDefault();

        return new CpuInfo(h.Name.Trim(), temp, load, cores, clock, power, volts, CpuFan(hardware));
    }

    // The fan that cools the processor: a header named for the CPU, else the pump of a liquid cooler,
    // else the first turning fan (on many boards the CPU header is simply "Fan #1" or "Fan #2").
    public static double? CpuFan(IReadOnlyList<SensorNode> hardware)
    {
        var fans = hardware.Where(x => x.IsBoard || x.Type == "Cooler").SelectMany(x => x.All)
            .Where(s => s.Type == "Fan" && s.Value is > 0 and < 20000).ToList();
        var pick = fans.FirstOrDefault(s => s.Name.Contains("CPU", I) && !s.Name.Contains("Opt", I))
                   ?? fans.FirstOrDefault(s => s.Name.Contains("CPU", I))
                   ?? fans.FirstOrDefault(s => Contains(s.Name, "Pump", "AIO"))
                   ?? fans.FirstOrDefault(s => !Contains(s.Name, "Chipset", "PCH", "VRM", "Power"));
        return pick?.Value;
    }

    // --- Mainboard -----------------------------------------------------------------------------------

    // Board probes grouped into what they measure; generic "Temperature #3" probes have no meaning and are left out.
    public static string? BoardLabel(string name)
    {
        string n = name.Trim();
        if (Contains(n, "Water", "Liquid", "Coolant")) return "Coolant";
        if (Contains(n, "Chipset", "PCH", "Southbridge", "Northbridge")) return "Chipset";
        if (Contains(n, "VRM", "MOS", "VSoC", "VCore")) return "VRM";
        if (Contains(n, "Motherboard", "Mainboard", "System") || n.Equals("Board", I)) return "Board";
        return null;
    }

    public static readonly string[] BoardOrder = { "Board", "VRM", "Chipset", "Coolant" };

    public static IReadOnlyList<TempReading> BoardTemps(SensorNode? board)
    {
        if (board is null) return Array.Empty<TempReading>();
        var picked = new Dictionary<string, double>();
        // Probes below 15 °C (colder than any working board) or above 115 °C are unconnected or misread;
        // VRM, chipset and board probes run at least at room temperature, so they need 22 °C.
        foreach (var s in board.All.Where(s => s.Type == "Temperature" && s.Value is >= 15 and <= 115))
        {
            string? label = BoardLabel(s.Name);
            if (label is null || (label != "Coolant" && s.Value < 22)) continue;
            // "Water In" and "Water Out": the warmer one is what the loop carries away.
            if (picked.TryGetValue(label, out var had)) { if (label == "Coolant") picked[label] = Math.Max(had, s.Value!.Value); continue; }
            picked[label] = s.Value!.Value;
        }
        return BoardOrder.Where(picked.ContainsKey).Select(k => new TempReading(k, picked[k])).ToList();
    }

    public sealed record FanReading(string Key, string Name, double Rpm, double? Percent);

    // Every fan the mainboard and attached controllers report, with its duty cycle where one exists.
    public static IReadOnlyList<FanReading> Fans(IReadOnlyList<SensorNode> hardware)
    {
        var result = new List<FanReading>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var board in hardware.Where(x => x.IsBoard || x.Type == "Cooler"))
            foreach (var node in new[] { board }.Concat(board.Sub))
                foreach (var s in node.Sensors.Where(s => s.Type == "Fan" && Ok(s)))
                {
                    // Disconnected headers on some chips read 65535 or similar.
                    if (s.Value is < 0 or > 20000) continue;
                    string name = s.Name.Trim();
                    // Two chips can expose the same header ("CPU Fan" on SuperIO and EC); show it once.
                    if (!names.Add(name)) continue;
                    var control = node.Sensors.FirstOrDefault(c => c.Type == "Control" && Ok(c) && (c.Index == s.Index && c.Name.Trim().Equals(name, I) || c.Name.Trim().Equals(name, I)))
                                  ?? node.Sensors.FirstOrDefault(c => c.Type == "Control" && Ok(c) && c.Index == s.Index);
                    result.Add(new FanReading(s.Id.Length > 0 ? s.Id : node.Name + "/" + name, name, s.Value!.Value, control?.Value is double p ? Math.Clamp(p, 0, 100) : null));
                }
        return result;
    }

    // --- Graphics ------------------------------------------------------------------------------------

    public static bool IsIntegrated(SensorNode h) =>
        h.Type == "GpuIntel" && !h.Name.Contains("Arc A", I) && !h.Name.Contains("Arc(TM) A", I) && !Regex.IsMatch(h.Name, @"Arc.*\bB\d{3}\b", RegexOptions.IgnoreCase)
        || h.Type == "GpuAmd" && Regex.IsMatch(h.Name, @"Radeon(\(TM\))? (Graphics|Vega)|Radeon \d{3}M|Radeon\(TM\) \d{3}M", RegexOptions.IgnoreCase);

    public static GpuInfo Gpu(SensorNode h)
    {
        var all = h.All.ToList();
        bool integrated = IsIntegrated(h);
        double? temp = Find(all, "Temperature", "GPU Core", "GPU Temperature", "GPU")
            ?? all.Where(s => s.Type == "Temperature" && s.Value is > 0 and < 125 && !Contains(s.Name, "Hot", "Junction", "Memory", "VR", "Mem")).Select(s => s.Value).FirstOrDefault();
        double? hot = Find(all, "Temperature", "GPU Hot Spot", "GPU Hotspot", "GPU Junction");
        double? load = Find(all, "Load", "GPU Core", "GPU Render/Compute", "D3D 3D", "GPU", "D3D Graphics_1");
        double? clock = Find(all.Where(s => s.Value > 0), "Clock", "GPU Core", "GPU Shader", "GPU Render", "GPU");
        double? power = Find(all.Where(s => s.Value >= 0), "Power", "GPU Package", "GPU Power", "GPU Total", "GPU PPT", "GPU Core", "GPU")
            ?? all.Where(s => s.Type == "Power" && s.Value > 0).Select(s => s.Value).FirstOrDefault();
        // Cards with two or three fans report each one; the fastest is the one to watch. 0 rpm means stopped.
        var fans = all.Where(s => s.Type == "Fan" && s.Value is >= 0 and < 20000).Select(s => s.Value!.Value).ToList();
        double? fan = fans.Count > 0 ? fans.Max() : null;
        double? used = Find(all, "SmallData", "GPU Memory Used", "D3D Dedicated Memory Used");
        double? total = Find(all.Where(s => s.Value > 0), "SmallData", "GPU Memory Total", "D3D Dedicated Memory Total");
        // Integrated graphics have no (or a token 128–512 MB) memory of their own; they borrow from RAM.
        if (integrated && (total is null or < 1024) && Find(all.Where(s => s.Value > 0), "SmallData", "D3D Shared Memory Total") is double shared)
        {
            total = shared;
            used = Find(all, "SmallData", "D3D Shared Memory Used") is double u ? u + (used ?? 0) : used;
        }
        if (total is null && used is double usedMb && Find(all, "SmallData", "GPU Memory Free") is double free) total = usedMb + free;
        return new GpuInfo(h.Id, h.Name.Trim(), temp, hot, load, clock, power, fan, used, total) { Integrated = integrated };
    }

    // --- Storage -------------------------------------------------------------------------------------

    // "Warning/Critical Temperature" are the drive's limits, not readings.
    public static DiskInfo Disk(SensorNode h)
    {
        var all = h.All.ToList();
        double? temp = Find(all.Where(s => s.Value is > 0 and < 100), "Temperature", "Composite Temperature", "Temperature", "Airflow Temperature", "Temperature #1")
            ?? all.Where(s => s.Type == "Temperature" && s.Value is > 0 and < 100 && !Contains(s.Name, "Warning", "Critical", "Threshold", "Limit")).Select(s => s.Value).FirstOrDefault();
        return new DiskInfo(h.Id, h.Name.Trim(), temp,
            Find(all, "Load", "Used Space"),
            Find(all, "Throughput", "Read Rate"),
            Find(all, "Throughput", "Write Rate"));
    }

    // A drive with nothing to show (card readers, some USB sticks) only takes up room.
    public static bool HasReadings(DiskInfo d) => d.Temperature != null || d.UsedPercent != null || d.ReadBytesPerSecond != null || d.WriteBytesPerSecond != null;

    // Both libraries name storage devices after the physical disk number: "/nvme/1", "/hdd/0"; Lumen's own ids are "/disk/1".
    public static int? DiskNumber(string id)
    {
        var m = Regex.Match(id, @"/(disk|nvme|hdd|ssd|ata|storage|scsi|usb)[^/]*/(\d+)", RegexOptions.IgnoreCase);
        return m.Success && int.TryParse(m.Groups[2].Value, out var n) ? n : null;
    }

    // --- Network -------------------------------------------------------------------------------------

    private static readonly string[] VirtualMarks =
    {
        "Virtual", "Hyper-V", "vEthernet", "VPN", "TAP-", "Tunnel", "WireGuard", "Tailscale", "ZeroTier", "OpenVPN",
        "VMware", "VirtualBox", "Npcap", "Loopback", "WAN Miniport", "Bluetooth", "Kernel Debug", "Wintun", "Fortinet",
        "Cisco AnyConnect", "NordLynx", "ProtonVPN", "Hamachi", "Teredo", "isatap", "Pseudo", "Microsoft Wi-Fi Direct"
    };

    public static bool IsVirtualAdapter(string description) => VirtualMarks.Any(m => description.Contains(m, I));

    public sealed record Nic(string Id, string Description, bool HasGateway, long BytesReceived);

    // The adapter that carries the traffic: a real one with a gateway, then any with a gateway
    // (a Hyper-V switch moves the address onto "vEthernet"), then any real adapter that is up.
    public static Nic? Network(IReadOnlyList<Nic> up, string? current)
    {
        if (up.FirstOrDefault(n => n.Id == current) is { } same) return same;
        return up.Where(n => n.HasGateway && !IsVirtualAdapter(n.Description)).OrderByDescending(n => n.BytesReceived).FirstOrDefault()
            ?? up.Where(n => n.HasGateway).OrderByDescending(n => n.BytesReceived).FirstOrDefault()
            ?? up.Where(n => !IsVirtualAdapter(n.Description)).OrderByDescending(n => n.BytesReceived).FirstOrDefault()
            ?? up.FirstOrDefault();
    }

    // --- Fan names -----------------------------------------------------------------------------------

    // "System Fan #2" → ("System", "2"), "Chassis Fan" → ("Chassis", ""), "Fan #3" → ("Fan", "3"),
    // "CPU Optional Fan" → ("CPU Opt", ""), "System Fan #5 / Pump" → ("Pump", "5"), "GPU Fan 1" → ("GPU", "1").
    public static (string Word, string Number) FanName(string raw)
    {
        string n = raw.Trim();
        bool pump = n.EndsWith("/ Pump", I);
        if (pump) n = n[..n.LastIndexOf('/')].Trim();
        var m = Regex.Match(n, @"^(.*?)\s*#?\s*(\d+)$");
        string number = m.Success ? m.Groups[2].Value : "";
        string word = m.Success ? m.Groups[1].Value.Trim() : n;
        word = Regex.Replace(word, @"\s*\bFan\b\s*$", "", RegexOptions.IgnoreCase).Trim();
        word = Regex.Replace(word, @"\bOptional\b", "Opt", RegexOptions.IgnoreCase);
        if (pump) word = "Pump";
        if (word.Equals("Power", StringComparison.OrdinalIgnoreCase)) word = "Power Fan";
        if (word.Length == 0) word = "Fan";
        return (word, number);
    }

    // --- Sensor report -------------------------------------------------------------------------------

    // Reads the LibreHardwareMonitor part of a Lumen sensor report back into nodes (for tests and support).
    public static List<SensorNode> ParseReport(string text, string section = "LibreHardwareMonitor")
    {
        var roots = new List<SensorNode>();
        var stack = new List<(int indent, string type, string name, string id, List<SensorValue> sensors, List<SensorNode> sub)>();
        bool inside = false;
        void Close(int depth)
        {
            while (stack.Count > depth)
            {
                var top = stack[^1];
                stack.RemoveAt(stack.Count - 1);
                var node = new SensorNode(top.type, top.name, top.id, top.sensors, top.sub);
                if (stack.Count == 0) roots.Add(node); else stack[^1].sub.Add(node);
            }
        }
        var counters = new Dictionary<string, int>();
        foreach (var raw in text.Replace("\r", "").Split('\n'))
        {
            if (raw.StartsWith("==="))
            {
                Close(0);
                inside = raw.Contains(section, I);
                continue;
            }
            if (!inside || raw.Trim().Length == 0) continue;
            int indent = raw.Length - raw.TrimStart().Length;
            string line = raw.Trim();
            var hw = Regex.Match(line, @"^\[(\w+)\]\s+(.*?)\s*(?:\(([^()]*/[^()]*)\))?$");
            if (hw.Success)
            {
                int depth = stack.Count(s => s.indent < indent);
                Close(depth);
                string id = hw.Groups[3].Success ? hw.Groups[3].Value : "/" + hw.Groups[1].Value.ToLowerInvariant() + "/" + roots.Count + "/" + stack.Count;
                stack.Add((indent, hw.Groups[1].Value, hw.Groups[2].Value, id, new(), new()));
                continue;
            }
            if (stack.Count == 0) continue;
            var sm = Regex.Match(line, @"^(\w+)\s+(.+?)\s+(—|-?[\d.]+(?:E[+-]?\d+)?)$");
            if (!sm.Success) continue;
            var target = stack[^1];
            string type = sm.Groups[1].Value, name = sm.Groups[2].Value;
            double? value = sm.Groups[3].Value == "—" ? null : double.Parse(sm.Groups[3].Value, CultureInfo.InvariantCulture);
            string counterKey = target.id + "|" + type;
            int index = counters.TryGetValue(counterKey, out var c) ? c : 0;
            counters[counterKey] = index + 1;
            target.sensors.Add(new SensorValue(type, name, value, index, target.id + "/" + type.ToLowerInvariant() + "/" + index));
        }
        Close(0);
        return roots;
    }
}
