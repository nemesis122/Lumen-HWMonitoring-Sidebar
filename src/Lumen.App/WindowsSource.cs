using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using LibreHardwareMonitor.Hardware;
using Lumen.Core;

namespace Lumen.App;

public interface ISensorReport
{
    string Report();
}

// Real readings on Windows. LibreHardwareMonitor reads CPU, GPU, drives, mainboard, fan
// controllers and battery; the driverless Open Hardware Monitor build (vendor GPU APIs and
// Windows SMART) fills in whatever Libre misses and adds GPUs or drives only it can see.
// Memory, network, CPU load and the rest come straight from Windows, so they work even
// without administrator rights.
public sealed class WindowsSource : ISnapshotSource, ISensorReport
{
    private readonly Computer computer;
#if WINRT
    private readonly OpenHardwareMonitor.Hardware.DriverlessComputer? ohm;
    private string? ohmError;
#endif
    private readonly List<string> mergeLog = new();
    private ulong lastIdle, lastKernel, lastUser;
    private readonly bool admin;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private string? nicId;
    private long lastRx, lastTx;
    private double lastTime;
    private readonly ProcessSampler processes = new();
    private double lastSlow;
    private SystemInfo? systemInfo;
    private IReadOnlyList<VolumeInfo> volumes = Array.Empty<VolumeInfo>();
    private readonly HashSet<string> spinning = new();

    public WindowsSource(bool admin)
    {
        this.admin = admin;
        computer = new Computer { IsCpuEnabled = true, IsGpuEnabled = true, IsStorageEnabled = true, IsMotherboardEnabled = true, IsBatteryEnabled = true, IsControllerEnabled = true };
        try { computer.Open(); } catch { /* without the driver some groups stay empty */ }
#if WINRT
        try { ohm = new OpenHardwareMonitor.Hardware.DriverlessComputer(); ohm.Open(); }
        catch (Exception e) { ohmError = e.Message; ohm = null; }
#endif
    }

    public Snapshot Read()
    {
        var hardware = new List<IHardware>();
        foreach (var h in computer.Hardware)
        {
            try { h.Update(); foreach (var sub in h.SubHardware) sub.Update(); } catch { }
            hardware.Add(h);
        }
        var cpu = ReadCpu(hardware);
        var gpus = hardware.Where(h => h.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel)
            .Select(ReadGpu).ToList();
        var disks = hardware.Where(h => h.HardwareType == HardwareType.Storage).Select(ReadDisk).ToList();
        mergeLog.Clear();
#if WINRT
        if (ohm != null)
        {
            var (og, od) = ReadOhm();
            gpus = SensorMerge.Gpus(gpus, og, mergeLog);
            disks = SensorMerge.Disks(disks, od, mergeLog);
        }
#endif
        gpus = gpus.OrderBy(g => g.Integrated ? 1 : 0).ToList();
        var access = cpu?.Temperature is null ? SensorAccess.Limited : SensorAccess.Full;
        if (!admin) access = SensorAccess.Limited;
        var board = hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Motherboard);
        IReadOnlyList<ProcessInfo> top;
        try { top = processes.Sample(); } catch { top = Array.Empty<ProcessInfo>(); }
        // Uptime, process counts and volumes change slowly; every five seconds is plenty.
        if (clock.Elapsed.TotalSeconds - lastSlow > 5 || systemInfo is null)
        {
            lastSlow = clock.Elapsed.TotalSeconds;
            try { systemInfo = SystemReader.Read(); volumes = SystemReader.Volumes(); } catch { }
        }
        return new Snapshot(DateTime.Now, cpu, gpus, ReadMemory(), disks, ReadNetwork(), access)
        {
            System = systemInfo is null ? null : systemInfo with { Uptime = TimeSpan.FromMilliseconds(Environment.TickCount64) },
            Volumes = volumes,
            BoardName = board?.Name,
            Fans = ReadFans(hardware),
            BoardTemps = board is null ? Array.Empty<TempReading>() : ReadBoardTemps(board),
            Battery = ReadBattery(hardware),
            Processes = top
        };
    }

    private static IEnumerable<ISensor> All(IHardware h) => h.Sensors.Concat(h.SubHardware.SelectMany(s => s.Sensors));

    private static double? Find(IHardware h, SensorType type, params string[] names)
    {
        var sensors = All(h).Where(s => s.SensorType == type && s.Value is float v && float.IsFinite(v)).ToList();
        foreach (var n in names)
        {
            var hit = sensors.FirstOrDefault(s => s.Name.Equals(n, StringComparison.OrdinalIgnoreCase));
            if (hit != null) return hit.Value;
        }
        return null;
    }

    private CpuInfo? ReadCpu(List<IHardware> hardware)
    {
        var h = hardware.FirstOrDefault(x => x.HardwareType == HardwareType.Cpu);
        if (h is null) return null;
        var sensors = All(h).ToList();
        double? temp = Find(h, SensorType.Temperature, "Core (Tctl/Tdie)", "CPU Package", "Package", "Core (Tctl)", "Core Max", "Core Average", "Tdie", "Tctl", "CCD1 (Tdie)", "CPU Cores");
        // Unknown naming: the hottest core or die reading the CPU reports.
        temp ??= sensors.Where(s => s.SensorType == SensorType.Temperature && s.Value is > 0 and < 125
                && !s.Name.Contains("Distance", StringComparison.OrdinalIgnoreCase) && !s.Name.Contains("TjMax", StringComparison.OrdinalIgnoreCase))
            .Select(s => (double?)s.Value).Max();
        // Still nothing (no driver): the mainboard's own CPU probe is better than no reading.
        temp ??= hardware.Where(x => x.HardwareType == HardwareType.Motherboard).SelectMany(All)
            .Where(s => s.SensorType == SensorType.Temperature && s.Value is > 0 and < 125 && s.Name.Contains("CPU", StringComparison.OrdinalIgnoreCase))
            .Select(s => (double?)s.Value).FirstOrDefault();
        double? load = Find(h, SensorType.Load, "CPU Total") ?? SystemLoad();
        // One bar per logical processor where the library reports threads, otherwise per core.
        var threads = sensors.Where(s => s.SensorType == SensorType.Load && s.Name.Contains("Thread #") && s.Value.HasValue).ToList();
        var coreSensors = threads.Count > 0 ? threads : sensors.Where(s => s.SensorType == SensorType.Load && s.Name.StartsWith("CPU Core #") && s.Value.HasValue).ToList();
        var cores = coreSensors.OrderBy(s => s.Index).Select(s => (double)s.Value!.Value).ToArray();
        var clocks = sensors.Where(s => s.SensorType == SensorType.Clock && s.Name.StartsWith("Core #") && s.Value > 0).Select(s => (double)s.Value!.Value).ToList();
        double? power = Find(h, SensorType.Power, "Package", "CPU Package") ?? sensors.Where(s => s.SensorType == SensorType.Power && s.Value > 0).Select(s => (double?)s.Value).FirstOrDefault();
        double? volts = Find(h, SensorType.Voltage, "Core (SVI2 TFN)", "Core (SVI3 TFN)", "CPU Core", "Core #1", "Core")
            ?? hardware.Where(x => x.HardwareType == HardwareType.Motherboard).SelectMany(All)
                .Where(s => s.SensorType == SensorType.Voltage && s.Value is > .3f and < 2f && (s.Name.Contains("Vcore", StringComparison.OrdinalIgnoreCase) || s.Name.Equals("CPU Core", StringComparison.OrdinalIgnoreCase)))
                .Select(s => (double?)s.Value).FirstOrDefault();
        double? fan = null;
        foreach (var board in hardware.Where(x => x.HardwareType == HardwareType.Motherboard))
        {
            var fans = All(board).Where(s => s.SensorType == SensorType.Fan && s.Value > 0).ToList();
            var cpuFan = fans.FirstOrDefault(s => s.Name.Contains("CPU", StringComparison.OrdinalIgnoreCase)) ?? fans.FirstOrDefault();
            if (cpuFan != null) { fan = cpuFan.Value; break; }
        }
        return new CpuInfo(h.Name, temp, load, cores, clocks.Count > 0 ? clocks.Average() : null, power, volts, fan);
    }

    // Fans on the mainboard that have ever turned; empty headers stay hidden.
    private IReadOnlyList<FanInfo> ReadFans(List<IHardware> hardware)
    {
        var result = new List<FanInfo>();
        foreach (var board in hardware.Where(x => x.HardwareType is HardwareType.Motherboard or HardwareType.Cooler))
            foreach (var s in All(board).Where(s => s.SensorType == SensorType.Fan && s.Value.HasValue))
            {
                string key = s.Identifier.ToString();
                if (s.Value > 0) spinning.Add(key);
                if (!spinning.Contains(key)) continue;
                var control = All(board).FirstOrDefault(c => c.SensorType == SensorType.Control && c.Index == s.Index && c.Value.HasValue);
                result.Add(new FanInfo(s.Name, s.Value!.Value, control?.Value));
            }
        return result;
    }

    // Only temperatures with a clear meaning; generic "Temperature #3" probes are left out.
    private static IReadOnlyList<TempReading> ReadBoardTemps(IHardware board)
    {
        var picked = new Dictionary<string, double>();
        foreach (var s in All(board).Where(s => s.SensorType == SensorType.Temperature && s.Value is > 0 and < 150))
        {
            string n = s.Name;
            string? label = n.Contains("Chipset", StringComparison.OrdinalIgnoreCase) || n.Contains("PCH", StringComparison.OrdinalIgnoreCase) ? "Chipset"
                : n.Contains("VRM", StringComparison.OrdinalIgnoreCase) || n.Contains("MOS", StringComparison.OrdinalIgnoreCase) ? "VRM"
                : n.Contains("System", StringComparison.OrdinalIgnoreCase) || n.Contains("Motherboard", StringComparison.OrdinalIgnoreCase) ? "Board"
                : null;
            if (label != null && !picked.ContainsKey(label)) picked[label] = s.Value!.Value;
        }
        return new[] { "Board", "VRM", "Chipset" }.Where(picked.ContainsKey).Select(k => new TempReading(k, picked[k])).ToList();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PowerStatus { public byte ACLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag; public int BatteryLifeTime, BatteryFullLifeTime; }

    [DllImport("kernel32.dll")] private static extern bool GetSystemPowerStatus(out PowerStatus status);

    private static BatteryInfo? ReadBattery(List<IHardware> hardware)
    {
        if (!GetSystemPowerStatus(out var ps) || (ps.BatteryFlag & 128) != 0 || ps.BatteryFlag == 255 || ps.BatteryLifePercent > 100) return null;
        bool onAc = ps.ACLineStatus == 1, charging = (ps.BatteryFlag & 8) != 0;
        var b = hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Battery);
        double? power = null, wear = null, minutes = ps.BatteryLifeTime > 0 ? ps.BatteryLifeTime / 60.0 : null;
        if (b != null)
        {
            if (Find(b, SensorType.Power, "Charge Rate") is double c) power = c;
            else if (Find(b, SensorType.Power, "Discharge Rate") is double d) power = -Math.Abs(d);
            else power = Find(b, SensorType.Power, "Charge/Discharge Rate");
            wear = Find(b, SensorType.Level, "Degradation Level");
            if (minutes is null && Find(b, SensorType.TimeSpan, "Remaining Time (Estimated)") is double secs && secs > 0) minutes = secs / 60;
        }
        return new BatteryInfo(ps.BatteryLifePercent, onAc, charging, power, minutes, wear);
    }

    private static bool IsIntegrated(IHardware h) => h.HardwareType == HardwareType.GpuIntel && !h.Name.Contains("Arc", StringComparison.OrdinalIgnoreCase)
        || h.HardwareType == HardwareType.GpuAmd && System.Text.RegularExpressions.Regex.IsMatch(h.Name, @"Radeon(\(TM\))? (Graphics|Vega)|Radeon \d{3}M", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private static GpuInfo ReadGpu(IHardware h)
    {
        double? fan = All(h).Where(s => s.SensorType == SensorType.Fan && s.Value.HasValue).Select(s => (double?)s.Value).FirstOrDefault();
        return new GpuInfo(h.Identifier.ToString(), h.Name,
            Find(h, SensorType.Temperature, "GPU Core", "GPU Temperature", "GPU VR SoC")
                ?? All(h).Where(s => s.SensorType == SensorType.Temperature && s.Value is > 0 and < 125 && !s.Name.Contains("Hot", StringComparison.OrdinalIgnoreCase)).Select(s => (double?)s.Value).FirstOrDefault(),
            Find(h, SensorType.Temperature, "GPU Hot Spot", "GPU Hotspot", "GPU Junction"),
            Find(h, SensorType.Load, "GPU Core", "D3D 3D", "GPU"),
            Find(h, SensorType.Clock, "GPU Core"),
            Find(h, SensorType.Power, "GPU Package", "GPU Power", "GPU Core", "GPU PPT")
                ?? All(h).Where(s => s.SensorType == SensorType.Power && s.Value > 0).Select(s => (double?)s.Value).FirstOrDefault(),
            fan,
            Find(h, SensorType.SmallData, "GPU Memory Used", "D3D Dedicated Memory Used"),
            Find(h, SensorType.SmallData, "GPU Memory Total", "D3D Dedicated Memory Total")) { Integrated = IsIntegrated(h) };
    }

    private static DiskInfo ReadDisk(IHardware h) => new(h.Identifier.ToString(), h.Name,
        All(h).Where(s => s.SensorType == SensorType.Temperature && s.Value > 0).Select(s => (double?)s.Value).FirstOrDefault(),
        Find(h, SensorType.Load, "Used Space"),
        Find(h, SensorType.Throughput, "Read Rate"),
        Find(h, SensorType.Throughput, "Write Rate"));

    [DllImport("kernel32.dll")] private static extern bool GetSystemTimes(out ulong idle, out ulong kernel, out ulong user);

    // Total CPU load straight from Windows, for when the sensor library cannot provide it.
    private double? SystemLoad()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user)) return null;
        ulong di = idle - lastIdle, dk = kernel - lastKernel, du = user - lastUser;
        bool first = lastKernel == 0;
        lastIdle = idle; lastKernel = kernel; lastUser = user;
        ulong total = dk + du;
        return first || total == 0 ? null : Math.Clamp(100.0 * (total - di) / total, 0, 100);
    }

#if WINRT
    // The same GPU and drive readings from the driverless Open Hardware Monitor build.
    private (List<GpuInfo>, List<DiskInfo>) ReadOhm()
    {
        var gpus = new List<GpuInfo>();
        var disks = new List<DiskInfo>();
        foreach (var h in ohm!.Hardware)
        {
            try { h.Update(); } catch { continue; }
            double? F(OpenHardwareMonitor.Hardware.SensorType type, params string[] names)
            {
                var list = h.Sensors.Where(s => s.SensorType == type && s.Value is float v && float.IsFinite(v)).ToList();
                foreach (var n in names) { var hit = list.FirstOrDefault(s => s.Name.Equals(n, StringComparison.OrdinalIgnoreCase)); if (hit != null) return hit.Value; }
                return null;
            }
            double? First(OpenHardwareMonitor.Hardware.SensorType type) => h.Sensors.Where(s => s.SensorType == type && s.Value is float v && float.IsFinite(v) && v > 0).Select(s => (double?)s.Value).FirstOrDefault();
            switch (h.HardwareType)
            {
                case OpenHardwareMonitor.Hardware.HardwareType.GpuNvidia or OpenHardwareMonitor.Hardware.HardwareType.GpuAmd:
                    gpus.Add(new GpuInfo("ohm:" + h.Identifier, h.Name,
                        F(OpenHardwareMonitor.Hardware.SensorType.Temperature, "GPU Core") ?? First(OpenHardwareMonitor.Hardware.SensorType.Temperature),
                        F(OpenHardwareMonitor.Hardware.SensorType.Temperature, "GPU Hot Spot"),
                        F(OpenHardwareMonitor.Hardware.SensorType.Load, "GPU Core", "D3D 3D"),
                        F(OpenHardwareMonitor.Hardware.SensorType.Clock, "GPU Core"),
                        F(OpenHardwareMonitor.Hardware.SensorType.Power, "GPU Package", "GPU Power", "GPU PPT", "GPU Core"),
                        F(OpenHardwareMonitor.Hardware.SensorType.Fan, "GPU", "GPU Fan") ?? First(OpenHardwareMonitor.Hardware.SensorType.Fan),
                        F(OpenHardwareMonitor.Hardware.SensorType.SmallData, "GPU Memory Used", "D3D Dedicated Memory Used"),
                        F(OpenHardwareMonitor.Hardware.SensorType.SmallData, "GPU Memory Total")));
                    break;
                case OpenHardwareMonitor.Hardware.HardwareType.Storage:
                    disks.Add(new DiskInfo("ohm:" + h.Identifier, h.Name,
                        F(OpenHardwareMonitor.Hardware.SensorType.Temperature, "Temperature", "Composite Temperature") ?? First(OpenHardwareMonitor.Hardware.SensorType.Temperature),
                        F(OpenHardwareMonitor.Hardware.SensorType.Load, "Used Space"),
                        F(OpenHardwareMonitor.Hardware.SensorType.Throughput, "Read Rate"),
                        F(OpenHardwareMonitor.Hardware.SensorType.Throughput, "Write Rate")));
                    break;
            }
        }
        return (gpus, disks);
    }
#endif

    // Every hardware item and sensor both libraries found, plus what Lumen made of it.
    public string Report()
    {
        var b = new System.Text.StringBuilder();
        b.AppendLine($"Lumen {typeof(WindowsSource).Assembly.GetName().Version?.ToString(3)} · {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        b.AppendLine($"Windows: {Environment.OSVersion.VersionString} · administrator: {admin} · LibreHardwareMonitor {typeof(Computer).Assembly.GetName().Version}");
        b.AppendLine();
        b.AppendLine("=== LibreHardwareMonitor ===");
        foreach (var h in computer.Hardware)
        {
            void Dump(IHardware x, string indent)
            {
                b.AppendLine($"{indent}[{x.HardwareType}] {x.Name}");
                foreach (var s in x.Sensors) b.AppendLine($"{indent}    {s.SensorType,-12} {s.Name,-34} {(s.Value is float v ? v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) : "—")}");
                foreach (var sub in x.SubHardware) Dump(sub, indent + "  ");
            }
            Dump(h, "");
        }
#if WINRT
        b.AppendLine();
        b.AppendLine("=== Open Hardware Monitor (driverless: vendor GPU APIs, SMART) ===");
        if (ohmError != null) b.AppendLine("Not available: " + ohmError);
        if (ohm != null)
        {
            foreach (var d in ohm.Diagnostics) b.AppendLine("Note: " + d);
            foreach (var h in ohm.Hardware)
            {
                b.AppendLine($"[{h.HardwareType}] {h.Name}");
                foreach (var s in h.Sensors) b.AppendLine($"    {s.SensorType,-12} {s.Name,-34} {(s.Value is float v ? v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) : "—")}");
            }
        }
#endif
        b.AppendLine();
        b.AppendLine("=== Combined ===");
        if (mergeLog.Count == 0) b.AppendLine("Nothing needed from Open Hardware Monitor.");
        foreach (var line in mergeLog) b.AppendLine(line);
        return b.ToString();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length, MemoryLoad; public ulong TotalPhys, AvailPhys, TotalPageFile, AvailPageFile, TotalVirtual, AvailVirtual, AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);

    private static MemoryInfo? ReadMemory()
    {
        var m = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        if (!GlobalMemoryStatusEx(ref m)) return null;
        const double GiB = 1024d * 1024 * 1024;
        return new MemoryInfo((m.TotalPhys - m.AvailPhys) / GiB, m.TotalPhys / GiB);
    }

    // Follows the adapter that carries the default route; rates come from byte-counter deltas.
    private NetworkInfo? ReadNetwork()
    {
        NetworkInterface? nic;
        try
        {
            var up = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
                .ToList();
            nic = up.FirstOrDefault(n => n.Id == nicId)
                  ?? up.Where(n => n.GetIPProperties().GatewayAddresses.Any(g => !g.Address.Equals(System.Net.IPAddress.Any)))
                       .OrderByDescending(n => n.GetIPStatistics().BytesReceived).FirstOrDefault()
                  ?? up.FirstOrDefault();
        }
        catch { return null; }
        if (nic is null) return null;
        var stats = nic.GetIPStatistics();
        double now = clock.Elapsed.TotalSeconds;
        double? down = null, upRate = null;
        if (nic.Id == nicId && now > lastTime)
        {
            down = Math.Max(0, stats.BytesReceived - lastRx) / (now - lastTime);
            upRate = Math.Max(0, stats.BytesSent - lastTx) / (now - lastTime);
        }
        nicId = nic.Id; lastRx = stats.BytesReceived; lastTx = stats.BytesSent; lastTime = now;
        string? address = nic.GetIPProperties().UnicastAddresses.FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork)?.Address.ToString();
        return new NetworkInfo(nic.Description, down, upRate, nic.Speed > 0 ? nic.Speed : null, address);
    }

    public void Dispose()
    {
        try { computer.Close(); } catch { }
#if WINRT
        try { ohm?.Dispose(); } catch { }
#endif
    }
}
