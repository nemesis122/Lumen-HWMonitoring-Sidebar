using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Lumen.Core;

// Where the hardware readings come from. Built-in is LibreHardwareMonitor plus the driverless
// Open Hardware Monitor; AIDA64 and HWiNFO are read from the shared memory they publish while
// they run. An external source only takes precedence where it has a value: everything it does
// not report still comes from the built-in libraries, and if it stops, Lumen falls back on its own.
public enum SensorSource { Builtin, Auto, Aida64, HwInfo }

// One item of AIDA64's shared memory ("External Applications" → "Enable shared memory").
public sealed record AidaItem(string Category, string Id, string Label, string Text)
{
    public double? Value => External.Number(Text);
}

// One reading of HWiNFO's shared memory ("Settings" → "Shared Memory Support"). Type follows HWiNFO:
// 1 temperature, 2 voltage, 3 fan, 4 current, 5 power, 6 clock, 7 usage, 8 other.
public sealed record HwinfoReading(string Sensor, int SensorIndex, int Type, string Label, string Unit, double Value);

public static class External
{
    private const StringComparison I = StringComparison.OrdinalIgnoreCase;

    public static double? Number(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        string t = text.Trim();
        if (!t.Contains('.') && t.Count(c => c == ',') == 1) t = t.Replace(',', '.');
        return double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && double.IsFinite(v) ? v : null;
    }

    // ---------------------------------------------------------------------------------------------
    // AIDA64: a flat run of <cat><id>…</id><label>…</label><value>…</value></cat> without a root element.

    private static readonly Regex AidaPattern = new(@"<(\w+)><id>(.*?)</id><label>(.*?)</label><value>(.*?)</value></\1>", RegexOptions.Singleline);

    public static List<AidaItem> ParseAida(string text) =>
        AidaPattern.Matches(text).Select(m => new AidaItem(m.Groups[1].Value, m.Groups[2].Value.Trim(), m.Groups[3].Value.Trim(), m.Groups[4].Value.Trim())).ToList();

    private static readonly Regex AidaGpu = new(@"^[A-Z]GPU(\d+)", RegexOptions.IgnoreCase);
    private static readonly Regex AidaCore = new(@"^SCPU(\d+)UTI$", RegexOptions.IgnoreCase);
    private static readonly Regex AidaDisk = new(@"^THDD(\d+)$", RegexOptions.IgnoreCase);

    // AIDA64's sensor ids become the same nodes LibreHardwareMonitor produces, named so that
    // SensorPicker reads them by its usual rules.
    public static List<SensorNode> AidaNodes(IReadOnlyList<AidaItem> items)
    {
        var byId = new Dictionary<string, AidaItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var i in items) byId.TryAdd(i.Id, i);
        double? V(params string[] ids)
        {
            foreach (var id in ids) if (byId.TryGetValue(id, out var it) && it.Value is double v) return v;
            return null;
        }
        var nodes = new List<SensorNode>();

        // Processor: the package or die probe first; AIDA64's plain "CPU" is often the board's socket probe.
        var cpu = new List<SensorValue>();
        void Add(List<SensorValue> list, string type, string name, double? value, int index = 0) { if (value is double v) list.Add(new SensorValue(type, name, v, index, "aida/" + type + "/" + name)); }
        Add(cpu, "Temperature", "CPU Package", V("TCPUPKG", "TCPUDIO", "TCPU"));
        Add(cpu, "Load", "CPU Total", V("SCPUUTI"));
        foreach (var core in items.Select(i => (i, m: AidaCore.Match(i.Id))).Where(x => x.m.Success && x.i.Value is not null)
                     .OrderBy(x => int.Parse(x.m.Groups[1].Value, CultureInfo.InvariantCulture)))
            Add(cpu, "Load", "Core #" + core.m.Groups[1].Value, core.i.Value, int.Parse(core.m.Groups[1].Value, CultureInfo.InvariantCulture));
        Add(cpu, "Clock", "Cores (Average)", V("SCPUCLK"));
        Add(cpu, "Power", "Package", V("PCPUPKG", "PCPU"));
        Add(cpu, "Voltage", "Vcore", V("VCPU"));
        if (cpu.Count > 0) nodes.Add(new SensorNode("Cpu", "CPU", "aida/cpu", cpu, Array.Empty<SensorNode>()));

        // Graphics cards by AIDA64's own numbering (GPU1, GPU2 …).
        var gpuNumbers = items.Select(i => AidaGpu.Match(i.Id)).Where(m => m.Success).Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).Distinct().OrderBy(n => n);
        foreach (int n in gpuNumbers)
        {
            var g = new List<SensorValue>();
            string p = n.ToString(CultureInfo.InvariantCulture);
            Add(g, "Temperature", "GPU Core", V($"TGPU{p}DIO", $"TGPU{p}"));
            var hot = items.FirstOrDefault(i => i.Category == "temp" && i.Id.StartsWith($"TGPU{p}", I) && i.Value is not null
                && (i.Id.Contains("HOT", I) || i.Label.Contains("Hot", I) || i.Label.Contains("Junction", I)));
            Add(g, "Temperature", "GPU Hot Spot", hot?.Value);
            Add(g, "Load", "GPU Core", V($"SGPU{p}UTI"));
            Add(g, "Clock", "GPU Core", V($"SGPU{p}CLK"));
            Add(g, "Power", "GPU Power", V($"PGPU{p}"));
            Add(g, "Fan", "GPU Fan", V($"FGPU{p}"));
            double? used = V($"SGPU{p}USEDDEMEM"), free = V($"SGPU{p}FREEDEMEM");
            Add(g, "SmallData", "GPU Memory Used", used);
            if (used is double u && free is double f) Add(g, "SmallData", "GPU Memory Total", u + f);
            if (g.Count > 0) nodes.Add(new SensorNode("GpuNvidia", "GPU " + p, "aida/gpu/" + p, g, Array.Empty<SensorNode>()) );
        }

        // Drives: AIDA64 labels each drive temperature with the drive's model.
        foreach (var d in items.Where(i => AidaDisk.IsMatch(i.Id) && i.Value is not null))
            nodes.Add(new SensorNode("Storage", d.Label, "aida/hdd/" + AidaDisk.Match(d.Id).Groups[1].Value,
                new[] { new SensorValue("Temperature", "Temperature", d.Value, 0, "aida/" + d.Id) }, Array.Empty<SensorNode>()));

        // Mainboard: its probes and every fan header that is not on a graphics card.
        var board = new List<SensorValue>();
        foreach (var t in items.Where(i => i.Category == "temp" && i.Value is not null && !Regex.IsMatch(i.Id, @"^T(CPU|CC|GPU|HDD)", RegexOptions.IgnoreCase)))
        {
            string name = t.Id.ToUpperInvariant() switch
            {
                "TMOBO" => "Motherboard",
                "TCHIP" or "TPCH" => "Chipset",
                var id when id.StartsWith("TVRM") => "VRM",
                _ => t.Label
            };
            board.Add(new SensorValue("Temperature", name, t.Value, board.Count, "aida/" + t.Id));
        }
        int fanIndex = 0;
        foreach (var f in items.Where(i => i.Category == "fan" && i.Value is not null && !AidaGpu.IsMatch(i.Id)))
        {
            string name = f.Id.Equals("FCPU", I) && !f.Label.Contains("CPU", I) ? "CPU Fan" : f.Label;
            board.Add(new SensorValue("Fan", name, f.Value, fanIndex++, "aida/" + f.Id));
        }
        if (board.Count > 0) nodes.Add(new SensorNode("Motherboard", "Mainboard", "aida/board", board, Array.Empty<SensorNode>()));
        return nodes;
    }

    // ---------------------------------------------------------------------------------------------
    // HWiNFO: binary layout of HWiNFO_SENS_SM2, packed to one byte.

    public const uint HwinfoSignature = 0x53695748; // "HWiS"

    private static string Ansi(ReadOnlySpan<byte> bytes)
    {
        int end = bytes.IndexOf((byte)0);
        if (end >= 0) bytes = bytes[..end];
        return Encoding.Latin1.GetString(bytes).Trim();
    }

    // The size the whole block needs, from its header; null if the header is not HWiNFO's.
    public static int? HwinfoSize(ReadOnlySpan<byte> header)
    {
        if (header.Length < 44 || BitConverter.ToUInt32(header[..4]) != HwinfoSignature) return null;
        long readings = BitConverter.ToUInt32(header.Slice(32, 4)) + (long)BitConverter.ToUInt32(header.Slice(36, 4)) * BitConverter.ToUInt32(header.Slice(40, 4));
        long sensors = BitConverter.ToUInt32(header.Slice(20, 4)) + (long)BitConverter.ToUInt32(header.Slice(24, 4)) * BitConverter.ToUInt32(header.Slice(28, 4));
        long size = Math.Max(readings, sensors);
        return size is > 0 and < 64 * 1024 * 1024 ? (int)size : null;
    }

    public static List<HwinfoReading> ParseHwinfo(ReadOnlySpan<byte> data)
    {
        var result = new List<HwinfoReading>();
        if (HwinfoSize(data) is not int size || data.Length < size) return result;
        int U(ReadOnlySpan<byte> d, int at) => (int)BitConverter.ToUInt32(d.Slice(at, 4));
        int sensorOffset = U(data, 20), sensorSize = U(data, 24), sensorCount = U(data, 28);
        int readingOffset = U(data, 32), readingSize = U(data, 36), readingCount = U(data, 40);
        if (sensorSize < 136 || readingSize < 316) return result;
        var sensors = new List<string>();
        for (int i = 0; i < sensorCount; i++)
            sensors.Add(Ansi(data.Slice(sensorOffset + i * sensorSize + 8, 128)));
        for (int i = 0; i < readingCount; i++)
        {
            var r = data.Slice(readingOffset + i * readingSize, readingSize);
            int type = (int)BitConverter.ToUInt32(r[..4]);
            int sensor = (int)BitConverter.ToUInt32(r.Slice(4, 4));
            double value = BitConverter.ToDouble(r.Slice(284, 8));
            if (type == 0 || sensor < 0 || sensor >= sensors.Count || !double.IsFinite(value)) continue;
            result.Add(new HwinfoReading(sensors[sensor], sensor, type, Ansi(r.Slice(12, 128)), Ansi(r.Slice(268, 16)), value));
        }
        return result;
    }

    private static readonly Regex HwCoreUsage = new(@"^(P-core|E-core|Core)\s*(\d+)(?:\s*T(\d+))?\s+Usage$", RegexOptions.IgnoreCase);
    private static readonly Regex HwCoreClock = new(@"^(P-core|E-core|Core)\s*(\d+)\s+Clock(?:\s*\(.*\))?$", RegexOptions.IgnoreCase);
    private static readonly Regex HwCoreVid = new(@"^(P-core|E-core|Core)\s*(\d+)\s+VID$", RegexOptions.IgnoreCase);
    private static readonly Regex HwGroup = new(@"^(CPU|GPU)\s*\[#(\d+)\]", RegexOptions.IgnoreCase);

    // "GPU [#0]: NVIDIA GeForce RTX 3070 Ti: ASUS TUF …" → "NVIDIA GeForce RTX 3070 Ti";
    // "S.M.A.R.T.: Samsung SSD 970 EVO Plus 1TB (S4EW…)" → "Samsung SSD 970 EVO Plus 1TB".
    public static string HwinfoName(string sensor)
    {
        int colon = sensor.IndexOf(": ", StringComparison.Ordinal);
        string rest = colon >= 0 ? sensor[(colon + 2)..] : sensor;
        int next = rest.IndexOf(": ", StringComparison.Ordinal);
        if (next >= 0) rest = rest[..next];
        rest = Regex.Replace(rest, @"\s*\([^()]*\)\s*$", "");
        return rest.Trim();
    }

    private static double Convert(HwinfoReading r)
    {
        string u = r.Unit.Trim();
        return u switch
        {
            "°F" or "°F" => (r.Value - 32) * 5 / 9,
            "GB" when r.Type == 8 => r.Value * 1024,
            "KB/s" => r.Value * 1024,
            "MB/s" => r.Value * 1024 * 1024,
            "GB/s" => r.Value * 1024 * 1024 * 1024,
            "GHz" => r.Value * 1000,
            _ => r.Value
        };
    }

    public static List<SensorNode> HwinfoNodes(IReadOnlyList<HwinfoReading> readings)
    {
        var nodes = new List<SensorNode>();
        var groups = readings.GroupBy(r => r.Sensor).ToList();

        // Processor: HWiNFO splits it into several blocks ("CPU [#0]: …", "…: Enhanced", "C-State Residency").
        var cpuGroups = groups.Where(g => g.Key.StartsWith("CPU [#0]", I)).ToList();
        if (cpuGroups.Count > 0)
        {
            var list = new List<SensorValue>();
            int thread = 0;
            foreach (var r in cpuGroups.SelectMany(g => g))
            {
                double v = Convert(r);
                string label = r.Label;
                (string type, string name)? map = r.Type switch
                {
                    1 when label.Equals("CPU (Tctl/Tdie)", I) => ("Temperature", "Core (Tctl/Tdie)"),
                    1 when label.Equals("CPU Package", I) => ("Temperature", "CPU Package"),
                    1 when label.Equals("Core Max", I) => ("Temperature", "Core Max"),
                    1 when label.Equals("CPU Die (average)", I) => ("Temperature", "Core (Tdie)"),
                    1 => ("Temperature", label),
                    7 when label.Equals("Total CPU Usage", I) => ("Load", "CPU Total"),
                    7 when HwCoreUsage.Match(label) is { Success: true } m && m.Groups[3].Success => ("Load", "Thread #" + (++thread)),
                    7 when HwCoreUsage.Match(label) is { Success: true } m => ("Load", CoreName(m.Groups[1].Value, m.Groups[2].Value)),
                    6 when label.StartsWith("Core Clocks", I) && label.Contains("avg", I) => ("Clock", "Cores (Average)"),
                    6 when !label.Contains("Effective", I) && HwCoreClock.Match(label) is { Success: true } m => ("Clock", CoreName(m.Groups[1].Value, m.Groups[2].Value)),
                    5 when label.Equals("CPU Package Power", I) => ("Power", "Package"),
                    5 when label.Equals("CPU PPT", I) => ("Power", "CPU PPT"),
                    5 when label.StartsWith("CPU Core Power", I) => ("Power", "CPU Cores"),
                    2 when label.StartsWith("CPU Core Voltage (SVI2", I) => ("Voltage", "Core (SVI2 TFN)"),
                    2 when label.StartsWith("CPU Core Voltage (SVI3", I) => ("Voltage", "Core (SVI3 TFN)"),
                    2 when label.Equals("Vcore", I) || label.Equals("CPU Core Voltage", I) => ("Voltage", "Vcore"),
                    2 when HwCoreVid.Match(label) is { Success: true } m => ("Voltage", CoreName(m.Groups[1].Value, m.Groups[2].Value) + " VID"),
                    _ => null
                };
                if (map is { } x) list.Add(new SensorValue(x.type, x.name, v, list.Count(s => s.Type == x.type), "hwinfo/cpu/" + label));
            }
            string name = HwinfoName(cpuGroups[0].Key);
            nodes.Add(new SensorNode("Cpu", name.Length > 0 ? name : "CPU", "hwinfo/cpu/0", list, Array.Empty<SensorNode>()));
        }

        // Graphics cards: every "GPU [#n]" block of one card becomes one node.
        foreach (var card in groups.Where(g => HwGroup.Match(g.Key) is { Success: true } m && m.Groups[1].Value.Equals("GPU", I))
                     .GroupBy(g => HwGroup.Match(g.Key).Groups[2].Value))
        {
            var list = new List<SensorValue>();
            double? allocated = null, usagePercent = null;
            foreach (var r in card.SelectMany(g => g))
            {
                double v = Convert(r);
                string label = r.Label;
                (string type, string name)? map = r.Type switch
                {
                    1 when label.Equals("GPU Temperature", I) => ("Temperature", "GPU Core"),
                    1 when label.Contains("Hot Spot", I) || label.Contains("Hotspot", I) => ("Temperature", "GPU Hot Spot"),
                    1 => ("Temperature", label),
                    7 when label.Equals("GPU Core Load", I) || label.Equals("GPU Utilization", I) => ("Load", "GPU Core"),
                    7 when label.Equals("GPU Memory Usage", I) => ("Load", "GPU Memory"),
                    6 when label.Equals("GPU Clock", I) || label.Equals("GPU Core Clock", I) => ("Clock", "GPU Core"),
                    5 when label.Equals("GPU Power", I) || label.Equals("GPU ASIC Power", I) || label.Equals("Total Graphics Power", I) || label.Equals("GPU PPT", I) => ("Power", "GPU Power"),
                    3 => ("Fan", label),
                    8 when label.Equals("GPU Memory Allocated", I) => ("SmallData", "GPU Memory Used"),
                    8 when label.Equals("GPU Memory Available", I) => ("SmallData", "GPU Memory Free"),
                    8 when label.Equals("GPU D3D Memory Dedicated", I) => ("SmallData", "D3D Dedicated Memory Used"),
                    _ => null
                };
                if (map is not { } x) continue;
                if (x.name == "GPU Memory Used") allocated = v;
                if (x.name == "GPU Memory") usagePercent = v;
                list.Add(new SensorValue(x.type, x.name, v, list.Count(s => s.Type == x.type), "hwinfo/gpu/" + label));
            }
            // Only "Allocated" and "Usage %" given: the card's memory follows from the two.
            if (allocated is double a && usagePercent is > 1 and <= 100 && !list.Any(s => s.Name is "GPU Memory Free" or "GPU Memory Total"))
                list.Add(new SensorValue("SmallData", "GPU Memory Total", Math.Round(a / usagePercent.Value * 100), 0, "hwinfo/gpu/total"));
            string name = HwinfoName(card.First().Key);
            string type = name.Contains("NVIDIA", I) || name.Contains("GeForce", I) ? "GpuNvidia"
                : name.Contains("Intel", I) ? "GpuIntel" : "GpuAmd";
            nodes.Add(new SensorNode(type, name, "hwinfo/gpu/" + card.Key, list, Array.Empty<SensorNode>()));
        }

        // Drives: "S.M.A.R.T.: <model>" holds the temperature, "Drive: <model>" the throughput.
        foreach (var drive in groups.Where(g => g.Key.StartsWith("S.M.A.R.T.", I) || g.Key.StartsWith("Drive:", I)).GroupBy(g => HwinfoName(g.Key)))
        {
            var list = new List<SensorValue>();
            foreach (var r in drive.SelectMany(g => g))
            {
                (string type, string name)? map = r.Type switch
                {
                    1 when r.Label.StartsWith("Drive Temperature", I) && !r.Label.Contains("2") => ("Temperature", "Temperature"),
                    1 => ("Temperature", r.Label),
                    8 when r.Label.Equals("Read Rate", I) => ("Throughput", "Read Rate"),
                    8 when r.Label.Equals("Write Rate", I) => ("Throughput", "Write Rate"),
                    _ => null
                };
                if (map is { } x) list.Add(new SensorValue(x.type, x.name, Convert(r), list.Count(s => s.Type == x.type), "hwinfo/disk/" + r.Label));
            }
            if (list.Count > 0) nodes.Add(new SensorNode("Storage", drive.Key, "hwinfo/disk/" + drive.Key, list, Array.Empty<SensorNode>()));
        }

        // Mainboard: the sensor chips and embedded controllers – every remaining block with temperatures or fans.
        var skip = new[] { "CPU [", "GPU [", "S.M.A.R.T.", "Drive:", "Network:", "Memory Timings", "System:", "Windows Hardware Errors", "Battery", "DIMM", "RTSS" };
        var boardGroups = groups.Where(g => !skip.Any(s => g.Key.StartsWith(s, I)) && g.Any(r => r.Type is 1 or 3)).ToList();
        if (boardGroups.Count > 0)
        {
            var subs = boardGroups.Select(g => new SensorNode("SuperIO", g.Key, "hwinfo/board/" + g.Key,
                g.Where(r => r.Type is 1 or 3).Select((r, i) => new SensorValue(r.Type == 1 ? "Temperature" : "Fan", r.Label, Convert(r), i, "hwinfo/board/" + g.Key + "/" + r.Label)).ToList(),
                Array.Empty<SensorNode>())).ToList();
            nodes.Add(new SensorNode("Motherboard", HwinfoName(boardGroups[0].Key), "hwinfo/board", Array.Empty<SensorValue>(), subs));
        }
        return nodes;
    }

    private static string CoreName(string kind, string number)
    {
        int n = int.Parse(number, CultureInfo.InvariantCulture) + 1;
        return kind.ToLowerInvariant() switch { "p-core" => "P-Core #" + n, "e-core" => "E-Core #" + n, _ => "Core #" + n };
    }
}
