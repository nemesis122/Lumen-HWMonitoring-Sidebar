namespace Lumen.Core;

// Plausible moving readings for screenshots and for machines without sensor access.
// Deterministic for a given seed so the same screenshot renders the same way every time.
public sealed class DemoSource : ISnapshotSource
{
    private readonly Random random;
    private int tick;
    private double down = 2.4e6, up = 3.1e5;
    private int burst;
    private readonly double[] cores = new double[16];
    private static readonly DateTime Start = new(2026, 10, 31, 10, 8, 0);

    public DemoSource(int seed = 7)
    {
        random = new Random(seed);
        for (int i = 0; i < cores.Length; i++) cores[i] = 10 + random.NextDouble() * 20;
    }

    public Snapshot Read()
    {
        tick++;
        double wave = Math.Sin(tick / 9.0), slow = Math.Sin(tick / 23.0);
        for (int i = 0; i < cores.Length; i++)
        {
            double target = (i % 5 == 0 ? 62 : 18) + 14 * Math.Sin(tick / 6.0 + i) + 10 * slow;
            cores[i] = Math.Clamp(cores[i] + (target - cores[i]) * .35 + (random.NextDouble() - .5) * 10, 1, 100);
        }
        double load = cores.Average();
        // Mostly light traffic with the occasional download burst that ramps up and fades out.
        if (burst <= 0 && random.NextDouble() < .03) burst = 14 + random.Next(16);
        double goal = burst > 0 ? 6.5e7 + random.NextDouble() * 3e7 : 4e5 + random.NextDouble() * 1.6e6;
        if (burst > 0) burst--;
        down = Math.Max(2e4, down + (goal - down) * .45);
        up = Math.Clamp(up * (.75 + random.NextDouble() * .5) + (burst > 0 ? 6e4 : 0), 2e4, 4e6);
        double cpuTemp = 44 + load * .42 + wave * 2;
        var cpu = new CpuInfo("AMD Ryzen 9 5900X", cpuTemp, load, cores.ToArray(), 3750 + load * 11 + wave * 60, 42 + load * 1.05, 1.25 + load * .0012, 820 + load * 9);
        double gpuLoad = Math.Clamp(38 + 26 * slow + 8 * wave, 2, 100);
        var gpus = new[]
        {
            new GpuInfo("gpu0", "NVIDIA GeForce RTX 3080", 41 + gpuLoad * .36, 52 + gpuLoad * .4, gpuLoad, 1830 + gpuLoad * 2, 34 + gpuLoad * 2.6, gpuLoad > 30 ? 1150 + gpuLoad * 8 : 0, 3100 + gpuLoad * 52, 10240),
            new GpuInfo("gpu1", "AMD Radeon Graphics", 39 + wave * 1.5, null, 3 + Math.Abs(wave) * 4, 400, 3.1, null, 380, 512) { Integrated = true }
        };
        var memory = new MemoryInfo(14.6 + slow * .6, 31.9);
        var disks = new List<DiskInfo>
        {
            new("nvme0", "Samsung SSD 970 EVO Plus 2TB", 47 + wave, 33, random.NextDouble() < .3 ? random.NextDouble() * 9e7 : 4e4, random.NextDouble() < .25 ? random.NextDouble() * 4e7 : 1.2e5),
            new("sata0", "Samsung SSD 870 EVO 4TB", 33, 57, random.NextDouble() < .1 ? random.NextDouble() * 2e7 : 0, 0)
        };
        var network = new NetworkInfo("Intel Ethernet X520-2", down, up, 10e9, "192.168.1.42");
        var time = Start.AddSeconds(tick);
        return new Snapshot(time, cpu, gpus, memory, disks, network, SensorAccess.Demo)
        {
            BoardName = "ASUS ROG STRIX X570-E",
            Fans = new[]
            {
                new FanInfo("CPU", 820 + load * 9, null),
                new FanInfo("Pump", 2350 + wave * 20, null),
                new FanInfo("Front", 760 + gpuLoad * 3, null),
                new FanInfo("Rear", 690 + load * 2, null),
                new FanInfo("Top", 0, null)
            },
            BoardTemps = new[] { new TempReading("Board", 36 + slow), new TempReading("VRM", 49 + load * .2), new TempReading("Chipset", 58 + wave) },
            Battery = new BatteryInfo(76 + slow * 2, true, true, 18.4, 47, 7.5),
            Processes = new[]
            {
                new ProcessInfo("Cyberpunk2077", 18 + load * .3, 9420),
                new ProcessInfo("chrome", 6.2 + wave * 2, 2310),
                new ProcessInfo("Discord", 1.8, 610),
                new ProcessInfo("explorer", .7, 184)
            },
            Weather = new WeatherInfo("Zürich", 9.4, 7.1, 2, true, 11, 78, new[]
            {
                new WeatherDay(time.Date, 5.2, 11.8, 2),
                new WeatherDay(time.Date.AddDays(1), 4.1, 9.6, 61),
                new WeatherDay(time.Date.AddDays(2), 2.8, 8.3, 3),
                new WeatherDay(time.Date.AddDays(3), 1.0, 10.4, 0)
            }, time),
            Ping = new PingInfo("1.1.1.1", 9 + Math.Abs(wave) * 4 + (tick % 17 == 0 ? 18 : 0), 0.6, 1.8, tick % 40 < 2 ? 3.3 : 0),
            Media = new MediaInfo("Midnight City", "M83", "Spotify", true, 74 + tick % 170, 244, null),
            Usage = new UsageInfo(42 + load * 1.05 + 34 + gpuLoad * 2.6 + 50, 1.84, 9.6, 31.2, 18.4e9, 1.2e9, 96e9, 7.4e9, 412e9, 31e9),
            System = new SystemInfo("Windows 11 Pro", "24H2 · 26100.2161", TimeSpan.FromHours(51.3) + TimeSpan.FromSeconds(tick), time.AddHours(-51.3), 284, 3912),
            Volumes = new[]
            {
                new VolumeInfo("C:", "System", 1863, 1248),
                new VolumeInfo("D:", "Games", 3726, 1602),
                new VolumeInfo("E:", "Backup", 7452, 520)
            }
        };
    }

    public void Dispose() { }
}
