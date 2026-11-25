using Lumen.Core;

/// <summary>
/// Different PCs for the sensor tests (user request 5.0.2: "it has to work on all hardware, not only here"). Each variant
/// is the demo PC changed into another kind of machine, so every reading the app knows is exercised with and without data.
/// </summary>
public static class HardwareVariants
{
    public static readonly string[] Names = { "demo", "intel-nvidia", "apu-laptop", "big-rig", "minimal" };

    /// <summary>
    /// The variant PC with a history of its own: every demo reading of the last 160 updates turned into the variant, so the
    /// graphics cards of the variant (nv0, igpu, gpu7) have history curves like on a real PC. Until 5.5.0 the variants carried
    /// the demo history, which has no curves for those cards (found by the release check: a history switch of the laptop's
    /// integrated graphics changed nothing because there was nothing to draw).
    /// </summary>
    public static (Snapshot Snap, History History) For(string name)
    {
        if (name == "demo") return Render.Demo();
        var source = new DemoSource(); var h = new History(); var frame = source.Read();
        Snapshot snap = Variant(name, frame with { Battery = null }, 0);
        for (int i = 0; i < 160; i++) { frame = source.Read(); snap = Variant(name, frame with { Battery = null }, i - 159); h.Record(snap); }
        return (snap, h);
    }

    /// <summary>The demo reading turned into the variant; <paramref name="phase"/> moves the fixed readings of the variant's
    /// own cards a little over time (0 for the last update, which keeps the values the tests expect).</summary>
    private static Snapshot Variant(string name, Snapshot demo, int phase)
    {
        double Wave(double value, double amplitude, double period) => value + amplitude * Math.Sin(phase / period);
        var main = demo.Gpus.First(g => !g.Integrated);
        Snapshot snap = name switch
        {
            // Intel processor with 8 cores, one NVIDIA card, no voltage rails or board sensors, one drive.
            "intel-nvidia" => demo with
            {
                Cpu = demo.Cpu! with { Name = "13th Gen Intel(R) Core(TM) i7-13700K", CoreLoads = demo.Cpu!.CoreLoads.Take(8).ToArray() },
                Gpus = new[] { main with { Id = "nv0", Name = "NVIDIA GeForce RTX 4070 Ti", HotSpot = null } },
                Disks = demo.Disks.Take(1).ToArray(),
                HardwareSensors = Array.Empty<SensorNode>(), BoardTemps = Array.Empty<TempReading>(), BoardName = null,
            },
            // Laptop: AMD processor with integrated graphics only, battery, one drive, fans that stand still.
            "apu-laptop" => demo with
            {
                Cpu = demo.Cpu! with { Name = "AMD Ryzen 7 7840U w/ Radeon 780M Graphics", PowerW = null },
                Gpus = new[] { new GpuInfo("igpu", "AMD Radeon 780M Graphics", Wave(52, 3, 8), null, Wave(14, 8, 5), 800, null, null, 512, 4096) { Integrated = true } },
                Disks = demo.Disks.Take(1).ToArray(),
                Fans = new[] { new FanInfo("CPU Fan", 0, 0) },
                Battery = new BatteryInfo(76, false, false, 9.5, 240, 4),
                HardwareSensors = Array.Empty<SensorNode>(), BoardTemps = Array.Empty<TempReading>(),
            },
            // Big rig: two dedicated cards and integrated graphics, four drives, eight fans, no CPU voltage or power.
            "big-rig" => demo with
            {
                Cpu = demo.Cpu! with { PowerW = null, Voltage = null, CoreLoads = demo.Cpu!.CoreLoads.Concat(demo.Cpu!.CoreLoads).ToArray() },
                Gpus = new[]
                {
                    main,
                    main with { Id = "gpu7", Name = "AMD Radeon RX 7900 XTX", MemoryJunction = 74 },
                    new GpuInfo("igpu", "Intel(R) UHD Graphics 770", Wave(40, 2, 8), null, Wave(3, 2.5, 5), 300, null, null, 128, 2048) { Integrated = true },
                },
                Disks = demo.Disks.Concat(demo.Disks.Select((d, i) => d with { Id = d.Id + "b", Name = d.Name + " B", Letters = "X" + i + ":" })).ToArray(),
                Fans = Enumerable.Range(1, 8).Select(i => new FanInfo("Fan " + i, 600 + i * 90, 30 + i * 5)).ToArray(),
            },
            // Minimal: no administrator rights — no temperatures, no fans, no hardware tree, no network.
            "minimal" => demo with
            {
                Cpu = demo.Cpu! with { Temperature = null, PowerW = null, Voltage = null, FanRpm = null },
                Gpus = new[] { main with { Temperature = null, HotSpot = null, PowerW = null, FanRpm = null, MemoryJunction = null } },
                Disks = demo.Disks.Select(d => d with { Temperature = null, Life = null, PowerOnHours = null, PowerOnCount = null }).ToArray(),
                Fans = Array.Empty<FanInfo>(), BoardTemps = Array.Empty<TempReading>(), BoardName = null,
                HardwareSensors = Array.Empty<SensorNode>(), Network = null,
            },
            _ => demo
        };
        return snap;
    }
}
