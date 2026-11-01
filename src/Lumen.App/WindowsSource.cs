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
    private readonly Func<SensorSource> sourceChoice;
    private readonly ExternalSensors external = new();
    private string? externalName;

    public WindowsSource(bool admin, Func<SensorSource>? sourceChoice = null)
    {
        this.admin = admin;
        this.sourceChoice = sourceChoice ?? (() => SensorSource.Builtin);
        computer = new Computer { IsCpuEnabled = true, IsGpuEnabled = true, IsStorageEnabled = true, IsMotherboardEnabled = true, IsBatteryEnabled = true, IsControllerEnabled = true };
        try { computer.Open(); } catch { /* without the driver some groups stay empty */ }
#if WINRT
        try { ohm = new OpenHardwareMonitor.Hardware.DriverlessComputer(); ohm.Open(); }
        catch (Exception e) { ohmError = e.Message; ohm = null; }
#endif
    }

    // Puts LibreHardwareMonitor, Open Hardware Monitor and AIDA64/HWiNFO together (see HardwareCombiner).
    private readonly HardwareCombiner combiner = new();
    public bool Paused { get; private set; }

    public Snapshot Read()
    {
        mergeLog.Clear();
        var extRead = external.Read(sourceChoice());
        // While AIDA64 or HWiNFO answers, the built-in libraries rest, so only one program talks to the sensor chips.
        Paused = extRead != null && combiner.HasIdentity;
        List<SensorNode>? builtin = null;
        var ohmNodes = new List<SensorNode>();
        if (!Paused)
        {
            builtin = new List<SensorNode>();
            foreach (var h in computer.Hardware)
            {
                try { h.Update(); foreach (var sub in h.SubHardware) sub.Update(); } catch { }
                try { builtin.Add(Node(h)); } catch { }
            }
#if WINRT
            if (ohm != null) ohmNodes = ReadOhm();
#endif
        }
        // Uptime, process counts and volumes change slowly; every five seconds is plenty.
        if (clock.Elapsed.TotalSeconds - lastSlow > 5 || systemInfo is null)
        {
            lastSlow = clock.Elapsed.TotalSeconds;
            try { systemInfo = SystemReader.Read(); volumes = SystemReader.Volumes(); } catch { }
        }
        var view = combiner.Combine(new HardwareInput(builtin, ohmNodes,
            extRead is { } e ? (e.Name, e.Nodes) : null,
            PhysicalDisks(), DiskLetters(), volumes, SystemLoad(), AcpiTemperature));
        mergeLog.AddRange(combiner.Log);
        externalName = extRead?.Name;
        var cpu = view.Cpu;
        var gpus = view.Gpus;
        // Throughput from Windows' own disk counters where no library reported it (always while paused).
        var disks = view.Disks.Select(d => d.ReadBytesPerSecond is null && d.WriteBytesPerSecond is null && DiskNumber(d.Id) is int n && DiskRates(n) is var (r, w)
            ? d with { ReadBytesPerSecond = r, WriteBytesPerSecond = w } : d).ToList();
        var access = cpu?.Temperature is null ? SensorAccess.Limited : SensorAccess.Full;
        // AIDA64 or HWiNFO read the hardware with their own driver; Lumen itself then needs no administrator rights.
        if (!admin && externalName is null) access = SensorAccess.Limited;
        var nodes = view.Nodes.ToList();
        IReadOnlyList<ProcessInfo> top;
        try { top = processes.Sample(); } catch { top = Array.Empty<ProcessInfo>(); }
        return new Snapshot(DateTime.Now, cpu, gpus, ReadMemory(), disks, ReadNetwork(), access)
        {
            System = systemInfo is null ? null : systemInfo with { Uptime = TimeSpan.FromMilliseconds(Environment.TickCount64) },
            Volumes = volumes,
            BoardName = view.BoardName,
            Fans = Spinning(view.Fans),
            BoardTemps = view.BoardTemps,
            Battery = ReadBattery(nodes),
            Processes = top
        };
    }

    // LibreHardwareMonitor's hardware as library-neutral nodes; SensorPicker decides what each reading means.
    private static SensorNode Node(IHardware h) => new(h.HardwareType.ToString(), h.Name, h.Identifier.ToString(),
        h.Sensors.Select(s => new Lumen.Core.SensorValue(s.SensorType.ToString(), s.Name, s.Value is float v && float.IsFinite(v) ? v : null, s.Index, s.Identifier.ToString())).ToList(),
        h.SubHardware.Select(Node).ToList());

    // Kept for the tests and older callers.
    public static System.Text.RegularExpressions.Regex PerCore => SensorPicker.PerCore;

    private IReadOnlyList<FanInfo> Spinning(IReadOnlyList<SensorPicker.FanReading> fans)
    {
        var result = new List<FanInfo>();
        // Fans that have ever turned; empty headers stay hidden.
        foreach (var f in fans)
        {
            if (f.Rpm > 0) spinning.Add(f.Key);
            if (spinning.Contains(f.Key)) result.Add(new FanInfo(f.Name, f.Rpm, f.Percent));
        }
        return result;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PowerStatus { public byte ACLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag; public int BatteryLifeTime, BatteryFullLifeTime; }

    [DllImport("kernel32.dll")] private static extern bool GetSystemPowerStatus(out PowerStatus status);

    private static BatteryInfo? ReadBattery(List<SensorNode> hardware)
    {
        if (!GetSystemPowerStatus(out var ps) || (ps.BatteryFlag & 128) != 0 || ps.BatteryFlag == 255 || ps.BatteryLifePercent > 100) return null;
        bool onAc = ps.ACLineStatus == 1, charging = (ps.BatteryFlag & 8) != 0;
        var b = hardware.FirstOrDefault(h => h.Type == "Battery");
        double? power = null, wear = null, minutes = ps.BatteryLifeTime > 0 ? ps.BatteryLifeTime / 60.0 : null;
        if (b != null)
        {
            var all = b.All.ToList();
            if (SensorPicker.Find(all, "Power", "Charge Rate") is double c) power = c;
            else if (SensorPicker.Find(all, "Power", "Discharge Rate") is double d) power = -Math.Abs(d);
            else if (SensorPicker.Find(all, "Power", "Charge/Discharge Rate") is double r) power = charging || onAc ? Math.Abs(r) : -Math.Abs(r);
            wear = SensorPicker.Find(all, "Level", "Degradation Level");
            if (minutes is null && SensorPicker.Find(all, "TimeSpan", "Remaining Time (Estimated)") is double secs && secs > 0) minutes = secs / 60;
        }
        return new BatteryInfo(ps.BatteryLifePercent, onAc, charging, power, minutes, wear);
    }

    // Laptops and boards without a sensor driver: the ACPI thermal zone is the only temperature Windows gives.
    // It needs administrator rights, is coarse and on many desktops a fixed dummy value, so it is a last resort.
    private double? acpi;
    private double acpiAt = -100;
    private double? AcpiTemperature()
    {
        if (!admin || !OperatingSystem.IsWindows()) return null;
        if (clock.Elapsed.TotalSeconds - acpiAt < 5) return acpi;
        acpiAt = clock.Elapsed.TotalSeconds;
        try
        {
            using var search = new System.Management.ManagementObjectSearcher(@"root\WMI", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");
            double? best = null;
            foreach (var o in search.Get())
            {
                double c = Convert.ToDouble(o["CurrentTemperature"]) / 10 - 273.15;
                // 27.8 °C is the classic placeholder some firmware reports forever.
                if (c is > 30 and < 105 && (best is null || c > best)) best = Math.Round(c, 1);
            }
            acpi = best;
        }
        catch { acpi = null; acpiAt = double.MaxValue / 2; }
        return acpi;
    }

    // Drive letters per physical disk number, from each volume's disk extents; refreshed every 30 s.
    private Dictionary<int, string> letters = new();
    private double lettersAt = -100;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(IntPtr device, uint code, IntPtr inBuffer, int inSize, byte[] outBuffer, int outSize, out int returned, IntPtr overlapped);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);

    private Dictionary<int, string> DiskLetters()
    {
        if (clock.Elapsed.TotalSeconds - lettersAt < 30) return letters;
        lettersAt = clock.Elapsed.TotalSeconds;
        var map = new Dictionary<int, List<string>>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.DriveType is not (DriveType.Fixed or DriveType.Removable)) continue;
                string letter = drive.Name.TrimEnd('\\');
                IntPtr h = CreateFile(@"\\.\" + letter, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
                if (h == new IntPtr(-1)) continue;
                try
                {
                    var buffer = new byte[256];
                    // IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS: count, then (disk number, offset, length) per extent.
                    if (!DeviceIoControl(h, 0x00560000, IntPtr.Zero, 0, buffer, buffer.Length, out _, IntPtr.Zero)) continue;
                    int count = BitConverter.ToInt32(buffer, 0);
                    for (int i = 0; i < count && 8 + i * 24 + 4 <= buffer.Length; i++)
                    {
                        int disk = BitConverter.ToInt32(buffer, 8 + i * 24);
                        if (!map.TryGetValue(disk, out var list)) map[disk] = list = new();
                        if (!list.Contains(letter)) list.Add(letter);
                    }
                }
                finally { CloseHandle(h); }
            }
            catch { }
        }
        letters = map.ToDictionary(kv => kv.Key, kv => string.Join(" ", kv.Value.OrderBy(x => x)));
        return letters;
    }

    private static int? DiskNumber(string id) => SensorPicker.DiskNumber(id);

    [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "DeviceIoControl")]
    private static extern bool DeviceIoControlIn(IntPtr device, uint code, byte[] inBuffer, int inSize, byte[] outBuffer, int outSize, out int returned, IntPtr overlapped);

    // Model and serial number of every physical disk (IOCTL_STORAGE_QUERY_PROPERTY, no administrator rights needed); every 60 s.
    private List<SensorMerge.PhysicalDisk> physical = new();
    private double physicalAt = -100;
    private List<SensorMerge.PhysicalDisk> PhysicalDisks()
    {
        if (!OperatingSystem.IsWindows() || clock.Elapsed.TotalSeconds - physicalAt < 60) return physical;
        physicalAt = clock.Elapsed.TotalSeconds;
        var list = new List<SensorMerge.PhysicalDisk>();
        for (int n = 0; n < 32; n++)
        {
            IntPtr h = CreateFile(@"\\.\PhysicalDrive" + n, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
            if (h == new IntPtr(-1)) continue;
            try
            {
                var query = new byte[12]; // StorageDeviceProperty, PropertyStandardQuery
                var buffer = new byte[1024];
                if (!DeviceIoControlIn(h, 0x002D1400, query, query.Length, buffer, buffer.Length, out int got, IntPtr.Zero) || got < 32) continue;
                string Text(int offsetAt)
                {
                    int at = BitConverter.ToInt32(buffer, offsetAt);
                    if (at <= 0 || at >= got) return "";
                    int end = Array.IndexOf(buffer, (byte)0, at);
                    return System.Text.Encoding.ASCII.GetString(buffer, at, (end < 0 ? got : end) - at).Trim();
                }
                list.Add(new SensorMerge.PhysicalDisk(n, (Text(12) + " " + Text(16)).Trim(), Text(24)));
            }
            catch { }
            finally { CloseHandle(h); }
        }
        physical = list;
        return physical;
    }

    // Bytes read and written per second for one physical disk, from IOCTL_DISK_PERFORMANCE.
    private readonly Dictionary<int, (long read, long write, double at)> diskCounters = new();
    private (double?, double?)? DiskRates(int disk)
    {
        if (!OperatingSystem.IsWindows()) return null;
        IntPtr h = CreateFile(@"\\.\PhysicalDrive" + disk, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
        if (h == new IntPtr(-1)) return null;
        try
        {
            var buffer = new byte[88];
            if (!DeviceIoControl(h, 0x00070020, IntPtr.Zero, 0, buffer, buffer.Length, out _, IntPtr.Zero)) return null;
            long read = BitConverter.ToInt64(buffer, 0), write = BitConverter.ToInt64(buffer, 8);
            double now = clock.Elapsed.TotalSeconds;
            bool had = diskCounters.TryGetValue(disk, out var last);
            diskCounters[disk] = (read, write, now);
            if (!had || now <= last.at) return (null, null);
            return (Math.Max(0, read - last.read) / (now - last.at), Math.Max(0, write - last.write) / (now - last.at));
        }
        finally { CloseHandle(h); }
    }

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
    // The same GPU and drive readings from the driverless Open Hardware Monitor build, read by the same rules.
    private static SensorNode Node(OpenHardwareMonitor.Hardware.IHardware h) => new(h.HardwareType.ToString(), h.Name, "ohm:" + h.Identifier,
        h.Sensors.Select(s => new Lumen.Core.SensorValue(s.SensorType.ToString(), s.Name, s.Value is float v && float.IsFinite(v) ? v : null, s.Index, s.Identifier.ToString())).ToList(),
        (h.SubHardware ?? Array.Empty<OpenHardwareMonitor.Hardware.IHardware>()).Select(Node).ToList());

    private List<SensorNode> ReadOhm()
    {
        var nodes = new List<SensorNode>();
        foreach (var h in ohm!.Hardware)
        {
            try { h.Update(); nodes.Add(Node(h)); } catch { }
        }
        return nodes;
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
                b.AppendLine($"{indent}[{x.HardwareType}] {x.Name}  ({x.Identifier})");
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
                b.AppendLine($"[{h.HardwareType}] {h.Name}  ({h.Identifier})");
                foreach (var s in h.Sensors) b.AppendLine($"    {s.SensorType,-12} {s.Name,-34} {(s.Value is float v ? v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) : "—")}");
            }
        }
#endif
        b.AppendLine();
        b.AppendLine($"=== External source (setting: {sourceChoice()}) ===");
        if (sourceChoice() == SensorSource.Builtin) b.AppendLine("Not used.");
        else if (externalName == null) b.AppendLine("Not reachable – AIDA64/HWiNFO not running or shared memory switched off; built-in readings are shown.");
        else
        {
            b.AppendLine("Reading from " + externalName + (Paused ? " – LibreHardwareMonitor and Open Hardware Monitor are paused" : ""));
            if (external.LastText is { } raw) b.AppendLine(System.Text.RegularExpressions.Regex.Replace(raw, @"(</(sys|temp|fan|duty|volt|curr|pwr)>)", "$1" + Environment.NewLine));
        }
        b.AppendLine();
        b.AppendLine("=== Combined ===");
        if (mergeLog.Count == 0) b.AppendLine("Nothing needed from Open Hardware Monitor or an external source.");
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
            var pick = SensorPicker.Network(up.Select(n =>
            {
                bool gateway = false; long rx = 0;
                try { gateway = n.GetIPProperties().GatewayAddresses.Any(g => !g.Address.Equals(System.Net.IPAddress.Any) && !g.Address.Equals(System.Net.IPAddress.IPv6Any)); } catch { }
                try { rx = n.GetIPStatistics().BytesReceived; } catch { }
                return new SensorPicker.Nic(n.Id, n.Description, gateway, rx);
            }).ToList(), nicId);
            nic = pick is null ? null : up.First(n => n.Id == pick.Id);
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
