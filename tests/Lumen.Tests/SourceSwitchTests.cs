using Lumen.Core;
using Xunit;

// Every way the sensor source can change, played through with a real report (Ryzen 9 5900X, RTX 3080,
// ASUS X570, two 970 EVO Plus and an 870 EVO; Lumen without administrator rights, AIDA64 running).
public class SourceSwitchTests
{
    private static readonly string Report = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "aida-5900x-rtx3080.txt"));
    private static List<SensorNode> Libre => SensorPicker.ParseReport(Report, "LibreHardwareMonitor");
    private static List<SensorNode> Ohm => SensorPicker.ParseReport(Report, "Open Hardware Monitor");

    private static List<SensorNode> Aida
    {
        get
        {
            int from = Report.IndexOf("=== External source", StringComparison.Ordinal);
            int to = Report.IndexOf("=== Combined", from, StringComparison.Ordinal);
            return External.AidaNodes(External.ParseAida(Report[from..to]));
        }
    }

    // How Windows numbers the drives on that machine, and what lives on them.
    private static readonly List<SensorMerge.PhysicalDisk> Physical = new()
    {
        new(0, "Samsung SSD 870 EVO 4TB", "S6BCNJ0R900003C"),
        new(1, "NVMe Samsung SSD 970 EVO Plus 2TB", "S4J4NJ0N100001A"),
        new(2, "NVMe Samsung SSD 970 EVO Plus 2TB", "S4J4_NJ0N_1000_02B."),
    };
    private static readonly Dictionary<int, string> Letters = new() { [0] = "F:", [1] = "C:", [2] = "G:" };
    private static readonly List<VolumeInfo> Volumes = new()
    {
        new("C:", "", 1863, 1182), new("F:", "Backup", 3726, 1518), new("G:", "Games", 1863, 584)
    };

    // LibreHardwareMonitor with administrator rights: real values and the three drives, numbered like Windows.
    private static List<SensorNode> LibreAsAdmin()
    {
        var nodes = Libre.Select(n => n.Type == "Cpu"
            ? n with { Sensors = n.Sensors.Select(s => s.Type == "Temperature" ? s with { Value = 52 } : s).ToList() }
            : n).ToList();
        SensorNode Drive(string id, string name, double temp, double used) => new("Storage", name, id,
            new[] { new SensorValue("Temperature", "Temperature", temp), new SensorValue("Load", "Used Space", used), new SensorValue("Throughput", "Read Rate", 1000), new SensorValue("Throughput", "Write Rate", 2000) },
            Array.Empty<SensorNode>());
        nodes.Add(Drive("/nvme/1", "Samsung SSD 970 EVO Plus 2TB", 57, 37));
        nodes.Add(Drive("/nvme/2", "Samsung SSD 970 EVO Plus 2TB", 60, 69));
        nodes.Add(Drive("/hdd/0", "Samsung SSD 870 EVO 4TB", 30, 59));
        return nodes;
    }

    private static HardwareInput Input(IReadOnlyList<SensorNode>? builtin, IReadOnlyList<SensorNode>? ohm = null, (string, IReadOnlyList<SensorNode>)? ext = null) =>
        new(builtin, ohm ?? Ohm, ext, Physical, Letters, Volumes, 7);

    [Fact] public void BuiltInWithoutAdministratorShowsWhatWindowsAndTheGpuApisGive()
    {
        var v = new HardwareCombiner().Combine(Input(Libre));
        Assert.Equal("AMD Ryzen 9 5900X", v.Cpu!.Name);
        Assert.Null(v.Cpu.Temperature);                     // 0 °C without the driver is not a reading
        var gpu = Assert.Single(v.Gpus);
        Assert.Equal(("NVIDIA GeForce RTX 3080", 44d, 55.688, 10240d), (gpu.Name, gpu.Temperature!.Value, gpu.HotSpot!.Value, gpu.MemoryTotalMb!.Value));
        Assert.Empty(v.Disks);
        Assert.Empty(v.BoardTemps);
    }

    [Fact] public void Aida64FillsEverythingAndKeepsTheBuiltInNames()
    {
        var v = new HardwareCombiner().Combine(Input(Libre, ext: ("AIDA64", Aida)));
        var c = v.Cpu!;
        Assert.Equal("AMD Ryzen 9 5900X", c.Name);
        Assert.Equal((53d, 1d, 3726d, 40.45, 1.224, 667d), (c.Temperature!.Value, c.Load!.Value, c.ClockMhz!.Value, c.PowerW!.Value, c.Voltage!.Value, c.FanRpm!.Value));
        Assert.Equal(24, c.CoreLoads.Count);
        // Average of AIDA64's twelve per-core clocks, not its "CPU Clock" (core 1 only, 4791 MHz).

        var g = Assert.Single(v.Gpus);
        Assert.Equal("/gpu-nvidia/0", g.Id);
        Assert.Equal((46d, 58d, 0d, 210d, 40.35, 1473d, 10240d), (g.Temperature!.Value, g.HotSpot!.Value, g.Load!.Value, g.ClockMhz!.Value, g.PowerW!.Value, g.MemoryUsedMb!.Value, g.MemoryTotalMb!.Value));

        Assert.Equal(new[] { "/disk/1", "/disk/2", "/disk/0" }, v.Disks.Select(d => d.Id));
        Assert.Equal(new[] { "Samsung SSD 970 EVO Plus 2TB", "Samsung SSD 970 EVO Plus 2TB", "Samsung SSD 870 EVO 4TB" }, v.Disks.Select(d => d.Name));
        Assert.Equal(new[] { "C:", "G:", "F:" }, v.Disks.Select(d => d.Letters));
        Assert.Equal(new[] { 58d, 59d, 31d }, v.Disks.Select(d => d.Temperature!.Value));
        Assert.All(v.Disks, d => Assert.InRange(d.UsedPercent!.Value, 30, 70));

        Assert.Equal(new[] { "Board", "Chipset" }, v.BoardTemps.Select(t => t.Name));
        Assert.Equal(new[] { "CPU", "PCH", "Chassis #2" }, v.Fans.Select(f => f.Name));
        Assert.Equal("ASUS ROG STRIX X570-E GAMING", v.BoardName);
    }

    [Fact] public void PausedLibrariesChangeNothingOnScreen()
    {
        var combiner = new HardwareCombiner();
        Assert.False(combiner.HasIdentity);
        var first = combiner.Combine(Input(Libre, ext: ("AIDA64", Aida)));
        Assert.True(combiner.HasIdentity);
        var paused = combiner.Combine(Input(null, Array.Empty<SensorNode>(), ("AIDA64", Aida)));
        Assert.Equal(first.Cpu!.Name, paused.Cpu!.Name);
        Assert.Equal(first.Cpu.Temperature, paused.Cpu.Temperature);
        Assert.Equal(first.Gpus.Select(g => (g.Id, g.Name, g.Temperature, g.MemoryTotalMb)), paused.Gpus.Select(g => (g.Id, g.Name, g.Temperature, g.MemoryTotalMb)));
        Assert.Equal(first.Disks.Select(d => (d.Id, d.Name, d.Letters, d.Temperature)), paused.Disks.Select(d => (d.Id, d.Name, d.Letters, d.Temperature)));
        Assert.Equal(first.BoardTemps, paused.BoardTemps);
        Assert.Equal(first.Fans.Select(f => f.Name), paused.Fans.Select(f => f.Name));
        Assert.Equal(first.BoardName, paused.BoardName);
    }

    [Fact] public void AidaStopsAndTheBuiltInLibrariesTakeOver()
    {
        var combiner = new HardwareCombiner();
        combiner.Combine(Input(Libre, ext: ("AIDA64", Aida)));
        combiner.Combine(Input(null, Array.Empty<SensorNode>(), ("AIDA64", Aida)));
        var back = combiner.Combine(Input(Libre));          // AIDA64 closed: Libre is read again
        Assert.Null(back.Cpu!.Temperature);
        Assert.Equal(44, back.Gpus[0].Temperature);
        Assert.All(back.Fans, f => Assert.Equal(0, f.Rpm));  // Libre without the driver: headers at 0 rpm, which the sidebar hides
    }

    [Fact] public void DrivesKeepTheirIdsWhicheverSourceIsChosen()
    {
        var aida = new HardwareCombiner().Combine(Input(Libre, ext: ("AIDA64", Aida)));
        var builtIn = new HardwareCombiner().Combine(Input(LibreAsAdmin()));
        var both = new HardwareCombiner().Combine(Input(LibreAsAdmin(), ext: ("AIDA64", Aida)));
        string Key(DiskInfo d) => $"{d.Id} {d.Letters} {d.Name}";
        Assert.Equal(aida.Disks.Select(Key).OrderBy(x => x), builtIn.Disks.Select(Key).OrderBy(x => x));
        Assert.Equal(aida.Disks.Select(Key).OrderBy(x => x), both.Disks.Select(Key).OrderBy(x => x));
        // With both: AIDA64's temperatures on the right drive, Libre's throughput and space where AIDA64 has none.
        var c = both.Disks.Single(d => d.Letters == "C:");
        Assert.Equal((58d, 37d, 1000d), (c.Temperature!.Value, c.UsedPercent!.Value, c.ReadBytesPerSecond!.Value));
        Assert.Equal(59, both.Disks.Single(d => d.Letters == "G:").Temperature);
        Assert.Equal(31, both.Disks.Single(d => d.Letters == "F:").Temperature);
        Assert.Equal(3, both.Disks.Count);
        Assert.Equal(52, builtIn.Cpu!.Temperature);
        Assert.Equal(53, both.Cpu!.Temperature);
    }

    [Fact] public void GpuKeepsItsIdWithAndWithoutOpenHardwareMonitor()
    {
        var withOhm = new HardwareCombiner().Combine(Input(Libre, ext: ("AIDA64", Aida)));
        var withoutOhm = new HardwareCombiner().Combine(Input(Libre, Array.Empty<SensorNode>(), ("AIDA64", Aida)));
        var noLibreGpu = new HardwareCombiner().Combine(Input(Libre.Where(n => !n.IsGpu).ToList(), ext: ("AIDA64", Aida)));
        Assert.Equal(withOhm.Gpus[0].Id, withoutOhm.Gpus[0].Id);
        Assert.Equal("/gpu-nvidia/0", noLibreGpu.Gpus[0].Id.Replace("ohm:", "")); // found only by Open Hardware Monitor
        Assert.Single(noLibreGpu.Gpus);
    }

    [Fact] public void HwinfoDrivesGetTheSameIdsAsAida()
    {
        var readings = new List<HwinfoReading>
        {
            new("S.M.A.R.T.: Samsung SSD 970 EVO Plus 2TB (S4J4NJ0N100002B)", 0, 1, "Drive Temperature", "°C", 59),
            new("S.M.A.R.T.: Samsung SSD 970 EVO Plus 2TB (S4J4NJ0N100001A)", 1, 1, "Drive Temperature", "°C", 58),
            new("S.M.A.R.T.: Samsung SSD 870 EVO 4TB (S6BCNJ0R900003C)", 2, 1, "Drive Temperature", "°C", 31),
            new("CPU [#0]: AMD Ryzen 9 5900X: Enhanced", 3, 1, "CPU (Tctl/Tdie)", "°C", 54),
        };
        var hw = new HardwareCombiner().Combine(Input(Libre, ext: ("HWiNFO", External.HwinfoNodes(readings))));
        var aida = new HardwareCombiner().Combine(Input(Libre, ext: ("AIDA64", Aida)));
        Assert.Equal(aida.Disks.Select(d => (d.Id, d.Letters, d.Temperature)).OrderBy(x => x.Id), hw.Disks.Select(d => (d.Id, d.Letters, d.Temperature)).OrderBy(x => x.Id));
        Assert.Equal(54, hw.Cpu!.Temperature);
        Assert.Equal("AMD Ryzen 9 5900X", hw.Cpu.Name);
    }

    [Fact] public void ReportIsFreeOfPersonalData()
    {
        foreach (var original in new[] { "84.74.16.223", "192.168.0.119", "S4J4NJ0N808240F", "S4J4NJ0N808988T" })
            Assert.DoesNotContain(original, Report);
    }

    [Fact] public void HiddenDrivesFromOlderVersionsStayHidden()
    {
        var s = Settings.Normalize(new Settings { HiddenValues = new() { "drive:/nvme/1", "drive:ohm:/hdd/0", "drive:/disk/2", "gpu:x" } });
        Assert.Equal(new[] { "drive:/disk/1", "drive:/disk/0", "drive:/disk/2", "gpu:x" }, s.HiddenValues);
    }
}
