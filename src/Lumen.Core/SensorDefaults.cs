namespace Lumen.Core;

// New settings and recommended selection follow the user's hardware preset.
// Once migrated, individually enabled advanced readings survive subsequent loads.
public static class SensorDefaults
{
    public const int SelectionVersion = 2;
    public static readonly IReadOnlyDictionary<string, string[]> Groups = new Dictionary<string, string[]>
    {
        ["gpu.vram"] = new[] { "gpu.vram.used", "gpu.vram.total" },
        ["drives.io"] = new[] { "drives.read", "drives.write" },
        ["data.week"] = new[] { "data.week.down", "data.week.up" },
        ["data.month"] = new[] { "data.month.down", "data.month.up" },
        ["power.cost"] = new[] { "power.today", "power.week", "power.month" }
    };
    public static readonly string[] Optional =
    {
        "cpu.chart", "cpu.cores", "cpu.temp.history", "cpu.load.history",
        "mem.peak", "drives.count", "drives.used",
        "drives.readactivity", "drives.writeactivity", "drives.totalactivity",
        "drives.free", "drives.total", "drives.powercount", "drives.hours", "drives.life",
        "gpu.junction", "net.use", "net.peak", "net.address", "net.external", "net.quality"
    };

    public static bool Recommended(string key) => !Optional.Contains(key);
    public static List<string> For(WidgetKind kind)
    {
        string prefix = kind switch { WidgetKind.Cpu => "cpu.", WidgetKind.Memory => "mem.", WidgetKind.Drives => "drives.", WidgetKind.Gpu => "gpu.", WidgetKind.Network => "net.", _ => "" };
        return prefix.Length == 0 ? new() : Optional.Where(k => k.StartsWith(prefix)).ToList();
    }
}
