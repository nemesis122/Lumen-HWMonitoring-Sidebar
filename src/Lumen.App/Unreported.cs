using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Lumen.Core;

namespace Lumen.App;

/// <summary>
/// Values a processor or graphics card does not have at all – integrated graphics without a temperature,
/// hot spot, fan or own memory size; a laptop processor without fan or voltage. Such a value is left out
/// in every design like an unticked value (no "—" row, the load becomes the big number) instead of an
/// empty placeholder. A value that has been reported once stays, so a short gap never moves the layout.
/// </summary>
public static class Unreported
{
    private static readonly ConcurrentDictionary<string, byte> seen = new();
    // The snapshot a widget showed at its last update; the settings object belongs to that one widget.
    private static readonly ConditionalWeakTable<Settings, StrongBox<Snapshot?>> latest = new();

    /// <summary>Gives a CPU or GPU widget its own rule; other widgets are left as they are.</summary>
    public static void Attach(Settings scoped, WidgetKind kind, int index)
    {
        if (kind is not (WidgetKind.Cpu or WidgetKind.Gpu)) return;
        scoped.Unreported = key => Missing(scoped, kind, index, key);
    }

    /// <summary>Called whenever a widget reads its values, so the rule follows the current snapshot.</summary>
    public static void Note(Settings scoped, Snapshot snapshot)
    {
        if (scoped.Unreported != null) latest.GetOrCreateValue(scoped).Value = snapshot;
    }

    // The snapshot the sidebar showed last: a widget that is built (or rebuilt) before its first update
    // already lays out only the values its device has.
    private static Snapshot? current;

    /// <summary>The missing values of every processor and graphics card shown; the sidebar rebuilds its
    /// widgets when this changes (at most a few times after start, never back and forth).</summary>
    public static string Signature(Snapshot snapshot, Settings settings)
    {
        current = snapshot;
        var parts = new List<string>();
        if (snapshot.Cpu is CpuInfo c) parts.Add("cpu:" + string.Join(",", CpuKeys.Where(k => Missing(c, k))));
        foreach (var g in GpuGroup.Listed(snapshot, settings)) parts.Add(g.Id + ":" + string.Join(",", GpuKeys.Where(k => Missing(g, k))));
        return string.Join("|", parts);
    }

    private static readonly string[] CpuKeys = { "cpu.fan", "cpu.voltage", "cpu.power", "cpu.clock" };
    private static readonly string[] GpuKeys = { "gpu.temp", "gpu.hotspot", "gpu.junction", "gpu.fan", "gpu.clock", "gpu.power", "gpu.load", "gpu.vram.used", "gpu.vram.total" };

    private static bool Missing(Settings scoped, WidgetKind kind, int index, string key)
    {
        Snapshot? snap = latest.TryGetValue(scoped, out var box) ? box.Value : null;
        snap ??= current;
        if (snap == null) return false;
        if (kind == WidgetKind.Gpu)
            return key.StartsWith("gpu.") && GpuGroup.Listed(snap, scoped).ElementAtOrDefault(index) is GpuInfo g && Missing(g, key);
        return key.StartsWith("cpu.") && snap.Cpu is CpuInfo c && Missing(c, key);
    }

    /// <summary>Whether a device has ever reported this value (remembered for the whole session).</summary>
    public static bool Reported(string device, string name, double? value)
    {
        string id = "aio|" + device + "|" + name;
        if (value.HasValue && double.IsFinite(value.Value)) seen[id] = 0;
        return seen.ContainsKey(id);
    }

    public static bool Missing(GpuInfo g, string key)
    {
        // A card that reports nothing yet (first seconds after start) is not judged.
        if (!(g.Temperature ?? g.Load ?? g.ClockMhz ?? g.PowerW ?? g.MemoryUsedMb).HasValue) return false;
        string id = "gpu|" + g.Id + "|";
        bool Has(string name, double? value) { if (value.HasValue) seen[id + name] = 0; return seen.ContainsKey(id + name); }
        return key switch
        {
            "gpu.temp" or "gpu.temp.history" => !Has("temp", g.Temperature),
            "gpu.hotspot" => !Has("hotspot", g.HotSpot),
            "gpu.junction" => !Has("junction", g.MemoryJunction),
            "gpu.fan" => !Has("fan", g.FanRpm),
            "gpu.clock" => !Has("clock", g.ClockMhz),
            "gpu.power" => !Has("power", g.PowerW),
            "gpu.load" or "gpu.load.history" => !Has("load", g.Load),
            "gpu.vram.used" => !Has("vramused", g.MemoryUsedMb),
            "gpu.vram.total" => !Has("vramtotal", g.MemoryTotalMb),
            "gpu.vram" => !Has("vramused", g.MemoryUsedMb) && !Has("vramtotal", g.MemoryTotalMb),
            _ => false
        };
    }

    public static bool Missing(CpuInfo c, string key)
    {
        if (!(c.Temperature ?? c.Load ?? c.ClockMhz).HasValue) return false;
        string id = "cpu|" + c.Name + "|";
        bool Has(string name, double? value) { if (value.HasValue) seen[id + name] = 0; return seen.ContainsKey(id + name); }
        return key switch
        {
            "cpu.fan" => !Has("fan", c.FanRpm),
            "cpu.voltage" => !Has("voltage", c.Voltage),
            "cpu.power" => !Has("power", c.PowerW),
            "cpu.clock" => !Has("clock", c.ClockMhz),
            _ => false
        };
    }
}
