using Lumen.Core;

/// <summary>
/// Different PCs for the sensor tests (user request 5.0.2: "it has to work on all hardware, not only here"). Each variant
/// is the demo PC changed into another kind of machine, so every reading the app knows is exercised with and without data.
/// </summary>
public static class HardwareVariants
{
    public static readonly string[] Names = { "demo", "intel-nvidia", "apu-laptop", "big-rig", "minimal" };

    public static (Snapshot Snap, History History) For(string name)
    {
        var (demo, h) = Render.Demo();
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
                Gpus = new[] { new GpuInfo("igpu", "AMD Radeon 780M Graphics", 52, null, 14, 800, null, null, 512, 4096) { Integrated = true } },
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
                    new GpuInfo("igpu", "Intel(R) UHD Graphics 770", 40, null, 3, 300, null, null, 128, 2048) { Integrated = true },
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
        return (snap, h);
    }
}
