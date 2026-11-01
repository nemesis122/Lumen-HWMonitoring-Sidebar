using Lumen.Core;
using Xunit;

// The same selection rules against real and typical sensor layouts. Synthetic layouts use the sensor
// names LibreHardwareMonitor 0.9.x gives these chips; the first one is a real report from a user.
public class HardwareTests
{
    private static List<SensorNode> Report(string text) => SensorPicker.ParseReport("=== LibreHardwareMonitor ===\n" + text.Replace("\r", ""));

    private static List<SensorNode> Fixture(string name) =>
        SensorPicker.ParseReport(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name)));

    [Fact]
    public void Ryzen5900XWithRtx3080()
    {
        var hw = Fixture("ryzen-5900x-rtx-3080.txt");
        var cpu = SensorPicker.Cpu(hw)!;
        Assert.Equal("AMD Ryzen 9 5900X", cpu.Name);
        Assert.Equal(75.125, cpu.Temperature!.Value, 2);
        Assert.Equal(24, cpu.CoreLoads.Count);
        Assert.InRange(cpu.ClockMhz!.Value, 2000, 5000);      // real core clocks, not "(Effective)"
        Assert.Equal(1.425, cpu.Voltage!.Value, 3);
        Assert.Equal(897.606, cpu.FanRpm!.Value, 2);
        Assert.InRange(cpu.PowerW!.Value, 20, 200);

        var gpu = SensorPicker.Gpu(hw.Single(h => h.IsGpu));
        Assert.False(gpu.Integrated);
        Assert.Equal(46, gpu.Temperature);
        Assert.Equal(63.063, gpu.HotSpot!.Value, 2);
        Assert.Equal(9, gpu.Load);
        Assert.Equal(68.673, gpu.PowerW!.Value, 2);
        Assert.Equal(0, gpu.FanRpm);                          // stopped fans: "0 rpm", not missing
        Assert.Equal(10240, gpu.MemoryTotalMb);

        var disks = hw.Where(h => h.Type == "Storage").Select(SensorPicker.Disk).ToList();
        Assert.Equal(3, disks.Count);
        Assert.Equal(58, disks[0].Temperature);               // "Composite", never the 84 °C warning limit
        Assert.Equal("Samsung SSD 870 EVO 4TB", disks[2].Name);

        var board = SensorPicker.BoardTemps(hw.First(h => h.IsBoard));
        Assert.Equal(new[] { "Board", "Chipset" }, board.Select(t => t.Name));  // VRM reads 20 °C: unconnected
        Assert.Equal(67, board.Single(t => t.Name == "Chipset").Celsius);

        var fans = SensorPicker.Fans(hw).Where(f => f.Rpm > 0).ToList();
        Assert.Equal(new[] { "Fan #2", "Fan #3", "Chipset Fan" }, fans.Select(f => f.Name));
        Assert.Equal(65.098, fans[0].Percent!.Value, 2);
        Assert.Null(fans[2].Percent);
    }

    [Fact]
    public void IntelHybridDesktop()
    {
        var hw = Report("""
[Motherboard] MSI MAG Z790 TOMAHAWK WIFI
  [SuperIO] Nuvoton NCT6687D
      Voltage      Vcore                              1.21
      Temperature  CPU                                48
      Temperature  System                             33
      Temperature  VRM MOS                            44
      Temperature  PCH                                46
      Temperature  Temperature #5                     —
      Fan          CPU Fan                            1180
      Fan          Pump Fan                           2400
      Fan          System Fan #1                      750
      Fan          System Fan #2                      0
      Control      CPU Fan                            42
      Control      Pump Fan                           100
      Control      System Fan #1                      35
      Control      System Fan #2                      0
[Cpu] Intel Core i7-14700K
    Load         CPU Total                          12.5
    Load         P-Core #1 Thread #1                20
    Load         P-Core #1 Thread #2                10
    Load         E-Core #1                          5
    Load         CPU Core Max                       60
    Temperature  P-Core #1                          52
    Temperature  E-Core #1                          44
    Temperature  P-Core #1 Distance to TjMax        48
    Temperature  Core Max                           55
    Temperature  Core Average                       47
    Temperature  CPU Package                        56
    Clock        P-Core #1                          5500
    Clock        E-Core #1                          4300
    Clock        Bus Speed                          100
    Power        CPU Package                        64.2
    Power        CPU Cores                          55.1
    Power        CPU Graphics                       0
    Voltage      P-Core #1                          1.22
    Voltage      E-Core #1                          1.18
[GpuIntel] Intel(R) UHD Graphics 770
    Temperature  GPU Core                           40
    Load         D3D 3D                             2
    Power        GPU Power                          1.2
    SmallData    D3D Dedicated Memory Used          0
    SmallData    D3D Shared Memory Used             410
    SmallData    D3D Shared Memory Total            16304
""");
        var cpu = SensorPicker.Cpu(hw)!;
        Assert.Equal(56, cpu.Temperature);                   // package, not the distance to TjMax
        Assert.Equal(12.5, cpu.Load);
        Assert.Equal(2, cpu.CoreLoads.Count);                // the threads, not "CPU Core Max"
        Assert.Equal(4900, cpu.ClockMhz);                    // P- and E-cores, without the bus clock
        Assert.Equal(64.2, cpu.PowerW);
        Assert.Equal(1.22, cpu.Voltage);
        Assert.Equal(1180, cpu.FanRpm);

        var igpu = SensorPicker.Gpu(hw.Single(h => h.IsGpu));
        Assert.True(igpu.Integrated);
        Assert.Equal(2, igpu.Load);
        Assert.Equal(16304, igpu.MemoryTotalMb);              // shared system memory counts as its VRAM
        Assert.Equal(410, igpu.MemoryUsedMb);

        var board = SensorPicker.BoardTemps(hw.First(h => h.IsBoard));
        Assert.Equal(new[] { "Board", "VRM", "Chipset" }, board.Select(t => t.Name));
        var fans = SensorPicker.Fans(hw);
        Assert.Equal(42, fans.Single(f => f.Name == "CPU Fan").Percent);
        Assert.Equal(100, fans.Single(f => f.Name == "Pump Fan").Percent);
    }

    [Fact]
    public void FirstGenerationRyzenAndAmdCard()
    {
        var hw = Report("""
[Motherboard] Gigabyte B450 AORUS ELITE
  [SuperIO] ITE IT8686E
      Temperature  System #1                          31
      Temperature  PCH                                41
      Temperature  CPU                                49
      Temperature  PCIe x16                           36
      Temperature  VRM MOS                            47
      Fan          CPU Fan                            0
      Fan          System Fan #1                      640
      Fan          System Fan #5 / Pump               1900
      Fan          CPU Optional Fan                   1300
[Cpu] AMD Ryzen 7 1700X
    Load         CPU Total                          8
    Load         CPU Core #1                        9
    Load         CPU Core #2                        7
    Temperature  Core (Tctl)                        70
    Temperature  Core (Tdie)                        50
    Clock        Core #1                            3400
    Clock        Core #1 (Effective)                120
    Clock        Core #2                            3600
    Power        Package                            31
    Power        Core #1 (SMU)                      3
    Voltage      Core #1 VID                        1.1
    Voltage      Core #2 VID                        1.15
[GpuAmd] AMD Radeon RX 7900 XTX
    Temperature  GPU Core                           51
    Temperature  GPU Hot Spot                       68
    Temperature  GPU Memory                         60
    Load         GPU Core                           30
    Clock        GPU Core                           2300
    Power        GPU Package                        210
    Fan          GPU Fan                            1100
    SmallData    GPU Memory Used                    3000
    SmallData    GPU Memory Total                   24560
""");
        var cpu = SensorPicker.Cpu(hw)!;
        Assert.Equal(50, cpu.Temperature);                   // Tdie: without the 20 °C Tctl offset of these chips
        Assert.Equal(3500, cpu.ClockMhz);
        Assert.Equal(31, cpu.PowerW);
        Assert.Equal(1.15, cpu.Voltage);                     // highest requested core voltage
        Assert.Equal(1300, cpu.FanRpm);                      // the turning CPU header

        var gpu = SensorPicker.Gpu(hw.Single(h => h.IsGpu));
        Assert.False(gpu.Integrated);
        Assert.Equal(51, gpu.Temperature);
        Assert.Equal(68, gpu.HotSpot);
        Assert.Equal(210, gpu.PowerW);
        Assert.Equal(1100, gpu.FanRpm);

        Assert.Equal(new[] { "Board", "VRM", "Chipset" }, SensorPicker.BoardTemps(hw.First(h => h.IsBoard)).Select(t => t.Name));
        Assert.Equal(("Pump", "5"), SensorPicker.FanName("System Fan #5 / Pump"));
    }

    [Fact]
    public void LaptopWithoutSuperIo()
    {
        var hw = Report("""
[Cpu] AMD Ryzen 7 7840HS w/ Radeon 780M Graphics
    Load         CPU Total                          4
    Load         CPU Core #1 Thread #1              3
    Load         CPU Core #1 Thread #2              5
    Temperature  Core (Tctl/Tdie)                   58
    Clock        Core #1                            4100
    Power        Package                            11
    Voltage      Core #1 VID                        0.9
[GpuAmd] AMD Radeon 780M Graphics
    Temperature  GPU Core                           49
    Load         GPU Core                           3
    SmallData    GPU Memory Used                    310
    SmallData    GPU Memory Total                   512
    SmallData    D3D Shared Memory Used             700
    SmallData    D3D Shared Memory Total            7600
[GpuNvidia] NVIDIA GeForce RTX 4060 Laptop GPU
    Load         GPU Core                           0
    Clock        GPU Core                           210
    SmallData    GPU Memory Total                   8188
[Battery] Primary
    Level        Charge Level                       77
    Power        Charge/Discharge Rate              14.2
[Storage] WD PC SN740 1TB
    Temperature  Composite Temperature              41
    Load         Used Space                         61
    Throughput   Read Rate                          0
    Throughput   Write Rate                         2048
""");
        var cpu = SensorPicker.Cpu(hw)!;
        Assert.Equal(58, cpu.Temperature);
        Assert.Null(cpu.FanRpm);                             // no mainboard chip: no fan, no crash
        Assert.Equal(0.9, cpu.Voltage);
        var gpus = hw.Where(h => h.IsGpu).Select(SensorPicker.Gpu).ToList();
        Assert.True(gpus[0].Integrated);
        Assert.Equal(7600, gpus[0].MemoryTotalMb);
        Assert.Equal(1010, gpus[0].MemoryUsedMb);
        Assert.False(gpus[1].Integrated);
        Assert.Null(gpus[1].Temperature);                    // sleeping dGPU reports no temperature
        Assert.Empty(SensorPicker.BoardTemps(null));
        Assert.Empty(SensorPicker.Fans(hw));
    }

    [Fact]
    public void NoDriverFallsBackToBoardProbe()
    {
        var hw = Report("""
[Motherboard] ASUS PRIME B550-PLUS
  [EmbeddedController] Embedded Controller
      Temperature  CPU                                57
      Temperature  Water In                           29
      Temperature  Water Out                          33
      Temperature  Motherboard                        30
      Fan          AIO Pump                           2650
[Cpu] AMD Ryzen 5 5600
    Load         CPU Total                          —
""");
        var cpu = SensorPicker.Cpu(hw, systemLoad: 17)!;
        Assert.Equal(57, cpu.Temperature);
        Assert.Equal(17, cpu.Load);                          // Windows' own counter
        Assert.Equal(2650, cpu.FanRpm);                      // the pump stands in for the CPU fan
        var board = SensorPicker.BoardTemps(hw.First(h => h.IsBoard));
        Assert.Equal(new[] { "Board", "Coolant" }, board.Select(t => t.Name));
        Assert.Equal(33, board.Single(t => t.Name == "Coolant").Celsius);
    }

    [Fact]
    public void IntelArcAndDualFanCard()
    {
        var hw = Report("""
[GpuIntel] Intel(R) Arc(TM) A770 Graphics
    Temperature  GPU Core                           55
    Temperature  GPU Memory                         62
    Load         GPU Render/Compute                 40
    Load         GPU Media                          0
    Power        GPU Total                          120
    Fan          GPU Fan                            1500
    SmallData    D3D Dedicated Memory Used          4000
    SmallData    GPU Memory Total                   16384
[GpuNvidia] NVIDIA GeForce RTX 4090
    Temperature  GPU Core                           60
    Fan          GPU Fan 1                          1200
    Fan          GPU Fan 2                          1350
    Fan          GPU Fan 3                          99999
    Load         GPU Core                           90
    Power        GPU Package                        420
""");
        var arc = SensorPicker.Gpu(hw[0]);
        Assert.False(arc.Integrated);
        Assert.Equal(40, arc.Load);
        Assert.Equal(120, arc.PowerW);
        Assert.Equal(16384, arc.MemoryTotalMb);
        var rtx = SensorPicker.Gpu(hw[1]);
        Assert.Equal(1350, rtx.FanRpm);                      // fastest real fan, the glitch reading ignored
    }

    [Theory]
    [InlineData("Fan #2", "Fan", "2")]
    [InlineData("System Fan #1", "System", "1")]
    [InlineData("Chassis Fan", "Chassis", "")]
    [InlineData("Chipset Fan", "Chipset", "")]
    [InlineData("CPU Optional Fan", "CPU Opt", "")]
    [InlineData("AIO Pump", "AIO Pump", "")]
    [InlineData("Power Fan", "Power Fan", "")]
    [InlineData("GPU Fan 1", "GPU", "1")]
    public void FanNames(string raw, string word, string number) => Assert.Equal((word, number), SensorPicker.FanName(raw));

    [Theory]
    [InlineData("VRM MOS", "VRM")]
    [InlineData("VSoC MOS", "VRM")]
    [InlineData("PCH", "Chipset")]
    [InlineData("Southbridge", "Chipset")]
    [InlineData("System #1", "Board")]
    [InlineData("Motherboard", "Board")]
    [InlineData("Water Out", "Coolant")]
    [InlineData("Temperature #3", null)]
    [InlineData("PCIe x16", null)]
    [InlineData("CPU", null)]
    public void BoardLabels(string name, string? label) => Assert.Equal(label, SensorPicker.BoardLabel(name));

    [Fact]
    public void NetworkPrefersRealAdapter()
    {
        var nics = new List<SensorPicker.Nic>
        {
            new("vpn", "WireGuard Tunnel", true, 900_000),
            new("hv", "Hyper-V Virtual Ethernet Adapter", false, 50),
            new("lan", "Realtek PCIe 2.5GbE Family Controller", true, 400_000),
        };
        Assert.Equal("lan", SensorPicker.Network(nics, null)!.Id);
        Assert.Equal("vpn", SensorPicker.Network(nics, "vpn")!.Id);   // stays on the one already shown
        // Hyper-V external switch: the address moved onto vEthernet, the physical adapter has none.
        var hyperV = new List<SensorPicker.Nic> { new("v", "vEthernet (External)", true, 10), new("p", "Intel(R) I225-V", false, 10) };
        Assert.Equal("v", SensorPicker.Network(hyperV, null)!.Id);
    }

    [Fact]
    public void EmptyDrivesAreHidden()
    {
        Assert.False(SensorPicker.HasReadings(new DiskInfo("/hdd/3", "Generic Card Reader", null, null, null, null)));
        Assert.Equal(2, SensorPicker.DiskNumber("/nvme/2"));
        Assert.Equal(0, SensorPicker.DiskNumber("/hdd/0"));
    }
}

public class RegisterNameTests
{
    [Fact]
    public void NamesFallBackToNumbers()
    {
        var s = new Settings { Pages = 3, PageNames = new() { " Gaming ", "", "A very long register name" } };
        Settings.Normalize(s);
        Assert.Equal(6, s.PageNames.Count);
        Assert.Equal("Gaming", s.PageName(1));
        Assert.Equal("2", s.PageName(2));
        Assert.Equal(16, s.PageName(3).Length);
        Assert.True(s.HasPageNames);
        Assert.False(new Settings { Pages = 2 }.HasPageNames);
    }
}
