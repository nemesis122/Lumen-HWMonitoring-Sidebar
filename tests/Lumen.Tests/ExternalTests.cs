using System.Text;
using Lumen.Core;
using Xunit;

public class ExternalTests
{
    // Shortened from a real AIDA64 shared-memory block (Ryzen + RTX, two NVMe drives).
    private const string Aida =
        "<sys><id>SDATE</id><label>Date</label><value>01.11.2026</value></sys>" +
        "<sys><id>SCPUCLK</id><label>CPU Clock</label><value>4941</value></sys>" +
        "<sys><id>SCPUUTI</id><label>CPU Utilization</label><value>4</value></sys>" +
        "<sys><id>SCPU1UTI</id><label>CPU1 Utilization</label><value>10</value></sys>" +
        "<sys><id>SCPU2UTI</id><label>CPU2 Utilization</label><value>2</value></sys>" +
        "<sys><id>SGPU1UTI</id><label>GPU1 Utilization</label><value>82</value></sys>" +
        "<sys><id>SGPU1CLK</id><label>GPU1 Clock</label><value>2610</value></sys>" +
        "<sys><id>SGPU1USEDDEMEM</id><label>GPU1 Used Dedicated Memory</label><value>7240</value></sys>" +
        "<temp><id>TMOBO</id><label>Motherboard</label><value>38</value></temp>" +
        "<temp><id>TCHIP</id><label>Chipset</label><value>52</value></temp>" +
        "<temp><id>TCPU</id><label>CPU</label><value>45</value></temp>" +
        "<temp><id>TCPUDIO</id><label>CPU Diode</label><value>50</value></temp>" +
        "<temp><id>TGPU1</id><label>GPU Diode</label><value>64</value></temp>" +
        "<temp><id>TGPU1HOT</id><label>GPU Hotspot</label><value>78</value></temp>" +
        "<temp><id>THDD1</id><label>Samsung SSD 970 EVO Plus 1TB</label><value>59</value></temp>" +
        "<temp><id>THDD2</id><label>Samsung SSD 870 EVO 4TB</label><value>42</value></temp>" +
        "<fan><id>FCPU</id><label>CPU</label><value>731</value></fan>" +
        "<fan><id>FCHA1</id><label>Chassis #1</label><value>860</value></fan>" +
        "<fan><id>FGPU1</id><label>GPU</label><value>1480</value></fan>" +
        "<volt><id>VCPU</id><label>CPU Core</label><value>1.144</value></volt>" +
        "<pwr><id>PCPUPKG</id><label>CPU Package</label><value>55.01</value></pwr>" +
        "<pwr><id>PGPU1</id><label>GPU</label><value>214.3</value></pwr>";

    [Fact] public void AidaParsesEveryItem()
    {
        var items = External.ParseAida(Aida);
        Assert.Equal(22, items.Count);
        Assert.Equal(1.144, items.Single(i => i.Id == "VCPU").Value);
        Assert.Null(items.Single(i => i.Id == "SDATE").Value);
    }

    [Fact] public void AidaBecomesCpuGpuDisksAndBoard()
    {
        var nodes = External.AidaNodes(External.ParseAida(Aida));
        var cpu = SensorPicker.Cpu(nodes)!;
        Assert.Equal(50, cpu.Temperature);          // die probe before the socket probe
        Assert.Equal(4, cpu.Load);
        Assert.Equal(new[] { 10d, 2d }, cpu.CoreLoads);
        Assert.Equal(4941, cpu.ClockMhz);
        Assert.Equal(55.01, cpu.PowerW);
        Assert.Equal(1.144, cpu.Voltage);
        Assert.Equal(731, cpu.FanRpm);

        var gpu = SensorPicker.Gpu(nodes.Single(n => n.IsGpu));
        Assert.Equal(64, gpu.Temperature);
        Assert.Equal(78, gpu.HotSpot);
        Assert.Equal(82, gpu.Load);
        Assert.Equal(1480, gpu.FanRpm);
        Assert.Equal(7240, gpu.MemoryUsedMb);

        var disks = nodes.Where(n => n.Type == "Storage").Select(SensorPicker.Disk).ToList();
        Assert.Equal(new[] { "Samsung SSD 970 EVO Plus 1TB", "Samsung SSD 870 EVO 4TB" }, disks.Select(d => d.Name));
        Assert.Equal(59, disks[0].Temperature);

        var board = nodes.Single(n => n.IsBoard);
        Assert.Equal(new[] { "Board", "Chipset" }, SensorPicker.BoardTemps(board).Select(t => t.Name));
        Assert.Equal(new[] { "CPU", "Chassis #1" }, SensorPicker.Fans(nodes).Select(f => f.Name));
    }

    // Builds a block in HWiNFO's packed layout: header, sensor elements, reading elements.
    private static byte[] Hwinfo(params (string sensor, int type, string label, string unit, double value)[] rows)
    {
        var sensors = rows.Select(r => r.sensor).Distinct().ToList();
        const int header = 48, sensorSize = 264, readingSize = 316;
        int readingOffset = header + sensors.Count * sensorSize;
        var data = new byte[readingOffset + rows.Length * readingSize];
        void U(int at, uint v) => BitConverter.GetBytes(v).CopyTo(data, at);
        void S(int at, string text) => Encoding.Latin1.GetBytes(text).CopyTo(data, at);
        U(0, External.HwinfoSignature); U(4, 2); U(8, 1);
        U(20, header); U(24, sensorSize); U(28, (uint)sensors.Count);
        U(32, (uint)readingOffset); U(36, readingSize); U(40, (uint)rows.Length);
        for (int i = 0; i < sensors.Count; i++) S(header + i * sensorSize + 8, sensors[i]);
        for (int i = 0; i < rows.Length; i++)
        {
            int at = readingOffset + i * readingSize;
            U(at, (uint)rows[i].type); U(at + 4, (uint)sensors.IndexOf(rows[i].sensor)); U(at + 8, (uint)i);
            S(at + 12, rows[i].label); S(at + 268, rows[i].unit);
            BitConverter.GetBytes(rows[i].value).CopyTo(data, at + 284);
        }
        return data;
    }

    [Fact] public void HwinfoBecomesCpuGpuDisksAndBoard()
    {
        const string cpuA = "CPU [#0]: AMD Ryzen 9 3900X", cpuB = "CPU [#0]: AMD Ryzen 9 3900X: Enhanced";
        const string gpu = "GPU [#0]: NVIDIA GeForce RTX 3070 Ti: ASUS TUF", disk = "S.M.A.R.T.: Samsung SSD 970 EVO Plus 1TB (S4EWNX0)";
        const string drive = "Drive: Samsung SSD 970 EVO Plus 1TB (S4EWNX0)", board = "ASUS ROG STRIX X570-E GAMING (Nuvoton NCT6798D)";
        var data = Hwinfo(
            (cpuA, 7, "Core 0 T0 Usage", "%", 12), (cpuA, 7, "Core 0 T1 Usage", "%", 4), (cpuA, 7, "Total CPU Usage", "%", 8),
            (cpuA, 6, "Core 0 Clock (perf #1/1)", "MHz", 4500), (cpuA, 6, "Core 0 Effective Clock", "MHz", 300),
            (cpuB, 1, "CPU (Tctl/Tdie)", "°C", 51.5), (cpuB, 5, "CPU Package Power", "W", 61), (cpuB, 2, "CPU Core Voltage (SVI2 TFN)", "V", 1.2),
            (gpu, 1, "GPU Temperature", "°C", 63), (gpu, 1, "GPU Hot Spot Temperature", "°C", 77), (gpu, 7, "GPU Core Load", "%", 90),
            (gpu, 6, "GPU Clock", "MHz", 1905), (gpu, 5, "GPU Power", "W", 250), (gpu, 3, "GPU Fan1", "RPM", 1600),
            (gpu, 8, "GPU Memory Allocated", "MB", 4096), (gpu, 7, "GPU Memory Usage", "%", 50),
            (disk, 1, "Drive Temperature", "°C", 59), (drive, 8, "Read Rate", "MB/s", 2),
            (board, 1, "Motherboard", "°C", 38), (board, 1, "Chipset", "°C", 55), (board, 3, "CPU", "RPM", 730), (board, 3, "Chassis1", "RPM", 0));

        var readings = External.ParseHwinfo(data);
        Assert.Equal(22, readings.Count);
        var nodes = External.HwinfoNodes(readings);

        var c = SensorPicker.Cpu(nodes)!;
        Assert.Equal("AMD Ryzen 9 3900X", c.Name);
        Assert.Equal(51.5, c.Temperature);
        Assert.Equal(8, c.Load);
        Assert.Equal(new[] { 12d, 4d }, c.CoreLoads);
        Assert.Equal(4500, c.ClockMhz);
        Assert.Equal(61, c.PowerW);
        Assert.Equal(1.2, c.Voltage);
        Assert.Equal(730, c.FanRpm);

        var g = SensorPicker.Gpu(nodes.Single(n => n.IsGpu));
        Assert.Equal("NVIDIA GeForce RTX 3070 Ti", g.Name);
        Assert.False(g.Integrated);
        Assert.Equal((63d, 77d, 90d, 1905d, 250d, 1600d), (g.Temperature, g.HotSpot, g.Load, g.ClockMhz, g.PowerW, g.FanRpm));
        Assert.Equal(4096, g.MemoryUsedMb);
        Assert.Equal(8192, g.MemoryTotalMb);

        var d = SensorPicker.Disk(nodes.Single(n => n.Type == "Storage"));
        Assert.Equal("Samsung SSD 970 EVO Plus 1TB (S4EWNX0)", d.Name);   // the serial finds the Windows disk
        Assert.Equal("Samsung SSD 970 EVO Plus 1TB", SensorMerge.WithoutSerial(d.Name));
        Assert.Equal(59, d.Temperature);
        Assert.Equal(2d * 1024 * 1024, d.ReadBytesPerSecond);

        Assert.Equal(new[] { "Board", "Chipset" }, SensorPicker.BoardTemps(nodes.Single(n => n.IsBoard)).Select(t => t.Name));
    }

    [Fact] public void BrokenHwinfoBlockGivesNothing()
    {
        Assert.Empty(External.ParseHwinfo(new byte[100]));
        Assert.Null(External.HwinfoSize(new byte[10]));
    }

    [Fact] public void ExternalWinsButNamesStayBuiltIn()
    {
        var local = new CpuInfo("AMD Ryzen 9 3900X", 49, 6, new[] { 1d, 2d }, 4300, null, null, 700);
        var ext = new CpuInfo("CPU", 50, null, Array.Empty<double>(), 4941, 55, 1.14, null);
        var m = SensorMerge.PreferCpu(ext, local)!;
        Assert.Equal("AMD Ryzen 9 3900X", m.Name);
        Assert.Equal((50d, 6d, 4941d, 55d, 1.14, 700d), (m.Temperature!.Value, m.Load!.Value, m.ClockMhz!.Value, m.PowerW!.Value, m.Voltage!.Value, m.FanRpm!.Value));
        Assert.Equal(new[] { 1d, 2d }, m.CoreLoads);
        Assert.Same(local, SensorMerge.PreferCpu(null, local));
    }

    [Fact] public void UnnamedAidaCardPairsWithTheDedicatedOne()
    {
        var local = new List<GpuInfo>
        {
            new("/gpu-nvidia/0", "NVIDIA GeForce RTX 3070 Ti", 60, null, 80, 1900, 240, 1500, 4000, 8192),
            new("/gpu-intel/0", "Intel(R) UHD Graphics 770", 40, null, 3, null, null, null, null, null) { Integrated = true }
        };
        var ext = new List<GpuInfo> { new("aida/gpu/1", "GPU 1", 64, 78, 82, 2610, 214, 1480, 7240, null) };
        var log = new List<string>();
        var m = SensorMerge.PreferGpus(ext, local, "AIDA64", log);
        Assert.Equal(2, m.Count);
        Assert.Equal("NVIDIA GeForce RTX 3070 Ti", m[0].Name);
        Assert.Equal((64d, 78d, 8192d), (m[0].Temperature!.Value, m[0].HotSpot!.Value, m[0].MemoryTotalMb!.Value));
        Assert.Equal(40, m[1].Temperature);
        Assert.Single(log);
    }

    [Fact] public void DrivesMatchByModelAndKeepLetters()
    {
        var local = new List<DiskInfo>
        {
            new("/nvme/0", "Samsung SSD 970 EVO Plus 1TB", null, 62, 100, 200) { Letters = "C:" },
            new("/nvme/1", "Samsung SSD 970 EVO Plus 1TB", 55, 30, null, null) { Letters = "D:" },
            new("/hdd/2", "Samsung SSD 870 EVO 4TB", 40, 50, null, null) { Letters = "E:" }
        };
        var ext = new List<DiskInfo>
        {
            new("aida/drive/1", "Samsung SSD 970 EVO Plus 1TB", 59, null, null, null),
            new("aida/drive/2", "Samsung SSD 970 EVO Plus 1TB", 62, null, null, null),
            new("aida/drive/3", "Samsung SSD 870 EVO 4TB", 42, null, null, null)
        };
        var m = SensorMerge.PreferDisks(ext, local, "AIDA64");
        Assert.Equal(new[] { 59d, 62d, 42d }, m.Select(d => d.Temperature!.Value));
        Assert.Equal(new[] { "C:", "D:", "E:" }, m.Select(d => d.Letters));
        Assert.Equal(62, m[0].UsedPercent);
        Assert.Equal(100, m[0].ReadBytesPerSecond);
    }

    [Fact] public void ExternalDrivesFindTheirWindowsDiskBySerial()
    {
        // Disk numbers as Windows had them on a real X570 system: the 4 TB SATA SSD first.
        var physical = new List<SensorMerge.PhysicalDisk>
        {
            new(0, "Samsung SSD 870 EVO 4TB", "S6BCNG0R123456A"),
            new(1, "NVMe Samsung SSD 970 EVO Plus 2TB", "S4J4NJ0N808988T"),
            new(2, "NVMe Samsung SSD 970 EVO Plus 2TB", "S4J4_NJ0N_8082_40F."),
        };
        var taken = new HashSet<int>();
        int? Match(string name) { var n = SensorMerge.MatchPhysical(name, physical, taken); if (n is int k) taken.Add(k); return n; }
        Assert.Equal(2, Match("Samsung SSD 970 EVO Plus 2TB (S4J4NJ0N808240F)"));
        Assert.Equal(1, Match("Samsung SSD 970 EVO Plus 2TB (S4J4NJ0N808988T)"));
        Assert.Equal(0, Match("Samsung SSD 870 EVO 4TB"));
        Assert.Null(Match("WDC WD40EFRX"));
        Assert.Equal("Samsung SSD 970 EVO Plus 2TB", SensorMerge.WithoutSerial("Samsung SSD 970 EVO Plus 2TB (S4J4NJ0N808240F)"));
    }

    [Fact] public void SwappedSataSerialStillMatches()
    {
        var physical = new List<SensorMerge.PhysicalDisk> { new(3, "Samsung SSD 860 EVO 1TB", "4SBCN0R1234567") };
        Assert.Equal(3, SensorMerge.MatchPhysical("Samsung SSD 860 EVO 1TB (S4CBN0R1234567)", physical, new HashSet<int>()));
    }

    [Fact] public void SecondDriveProbeIsNotASecondDrive()
    {
        var nodes = External.AidaNodes(External.ParseAida(
            "<temp><id>THDD1</id><label>Samsung SSD 970 EVO Plus 2TB (S4J4NJ0N808240F)</label><value>58</value></temp>" +
            "<temp><id>THDD1TS2</id><label>Samsung SSD 970 EVO Plus 2TB (S4J4NJ0N808240F) #2</label><value>58</value></temp>" +
            "<temp><id>TGPU1MEM</id><label>GPU Memory</label><value>52</value></temp>" +
            "<temp><id>TGPU1HOT</id><label>GPU Hotspot</label><value>58</value></temp>" +
            "<temp><id>TGPU1</id><label>GPU</label><value>46</value></temp>"));
        Assert.Single(nodes, n => n.Type == "Storage");
        var g = SensorPicker.Gpu(nodes.Single(n => n.IsGpu));
        Assert.Equal((46d, 58d), (g.Temperature!.Value, g.HotSpot!.Value));
    }

    [Fact] public void SourceSurvivesSaveAndLoad()
    {
        string path = Path.Combine(Path.GetTempPath(), "lumen-source-" + Guid.NewGuid() + ".json");
        try
        {
            var s = new Settings { SensorSource = SensorSource.HwInfo };
            s.Save(path);
            Assert.Equal(SensorSource.HwInfo, Settings.Load(path).SensorSource);
            Assert.Equal(SensorSource.Builtin, new Settings().SensorSource);
        }
        finally { File.Delete(path); }
    }
}
