using System;
using System.Collections.Generic;
using System.Linq;

namespace Lumen.Core;

public sealed class DemoSource : ISnapshotSource, IDisposable
{
	private readonly Random random;

	private int tick;

	private double down = 2400000.0;

	private double up = 310000.0;

	private int burst;

	private readonly double[] cores = new double[16];

	private static readonly DateTime Start = new DateTime(2026, 10, 31, 10, 8, 0);

	public DemoSource(int seed = 7)
	{
		random = new Random(seed);
		for (int i = 0; i < cores.Length; i++)
		{
			cores[i] = 10.0 + random.NextDouble() * 20.0;
		}
	}

	public Snapshot Read()
	{
		tick++;
		double num = Math.Sin((double)tick / 9.0);
		double num2 = Math.Sin((double)tick / 23.0);
		for (int i = 0; i < cores.Length; i++)
		{
			double num3 = (double)((i % 5 == 0) ? 62 : 18) + 14.0 * Math.Sin((double)tick / 6.0 + (double)i) + 10.0 * num2;
			cores[i] = Math.Clamp(cores[i] + (num3 - cores[i]) * 0.35 + (random.NextDouble() - 0.5) * 10.0, 1.0, 100.0);
		}
		double num4 = cores.Average();
		if (burst <= 0 && random.NextDouble() < 0.03)
		{
			burst = 14 + random.Next(16);
		}
		double num5 = ((burst > 0) ? (65000000.0 + random.NextDouble() * 30000000.0) : (400000.0 + random.NextDouble() * 1600000.0));
		if (burst > 0)
		{
			burst--;
		}
		down = Math.Max(20000.0, down + (num5 - down) * 0.45);
		up = Math.Clamp(up * (0.75 + random.NextDouble() * 0.5) + ((burst > 0) ? 60000.0 : 0.0), 20000.0, 4000000.0);
		double value = 44.0 + num4 * 0.42 + num * 2.0;
		CpuInfo cpu = new CpuInfo("AMD Ryzen 9 5900X", value, num4, cores.ToArray(), 3750.0 + num4 * 11.0 + num * 60.0, 42.0 + num4 * 1.05, 1.25 + num4 * 0.0012, 820.0 + num4 * 9.0);
		double num6 = Math.Clamp(38.0 + 26.0 * num2 + 8.0 * num, 2.0, 100.0);
		GpuInfo[] gpus = new GpuInfo[2]
		{
			new GpuInfo("gpu0", "NVIDIA GeForce RTX 3080", 41.0 + num6 * 0.36, 52.0 + num6 * 0.4, num6, 1830.0 + num6 * 2.0, 34.0 + num6 * 2.6, (num6 > 30.0) ? (1150.0 + num6 * 8.0) : 0.0, 3100.0 + num6 * 52.0, 10240.0)
			{
				MemoryJunction = 54.0 + num6 * 0.3
			},
			new GpuInfo("gpu1", "AMD Radeon Graphics", 39.0 + num * 1.5, null, 3.0 + Math.Abs(num) * 4.0, 400.0, 3.1, null, 380.0, 512.0)
			{
				Integrated = true
			}
		};
		MemoryInfo memory = new MemoryInfo(14.6 + num2 * 0.6, 31.9);
		List<DiskInfo> disks = new List<DiskInfo>
		{
			new DiskInfo("nvme0", "Samsung SSD 970 EVO Plus 2TB", 47.0 + num, 33.0, (random.NextDouble() < 0.3) ? (random.NextDouble() * 90000000.0) : 40000.0, (random.NextDouble() < 0.25) ? (random.NextDouble() * 40000000.0) : 120000.0)
			{
				ReadActivity = 2.5,
				WriteActivity = 0.8,
				TotalActivity = 3.3,
				PowerOnHours = 13210.0,
				PowerOnCount = 812.0,
				Life = 97.0,
				Letters = "C:"
			},
			new DiskInfo("sata0", "Samsung SSD 870 EVO 4TB", 33.0, 57.0, (random.NextDouble() < 0.1) ? (random.NextDouble() * 20000000.0) : 0.0, 0.0)
			{
				Letters = "D:"
			}
		};
		NetworkInfo network = new NetworkInfo("Intel Ethernet X520-2", down, up, 10000000000.0, "192.168.1.42");
		DateTime dateTime = Start.AddSeconds(tick);
		Snapshot snapshot = new Snapshot(dateTime, cpu, gpus, memory, disks, network, SensorAccess.Demo);
		return snapshot with {
BoardName = "ASUS ROG STRIX X570-E",
		Fans = new FanInfo[6]
		{
			new FanInfo("CPU", 820.0 + num4 * 9.0, null),
			new FanInfo("Pump", 2350.0 + num * 20.0, null),
			new FanInfo("Front", 760.0 + num6 * 3.0, null),
			new FanInfo("Rear", 690.0 + num4 * 2.0, null),
			new FanInfo("Chipset Fan", 3000.0 + num * 30.0, null),
			new FanInfo("Top", 0.0, null)
		},
		BoardTemps = new TempReading[3]
		{
			new TempReading("Board", 36.0 + num2),
			new TempReading("VRM", 49.0 + num4 * 0.2),
			new TempReading("Chipset", 58.0 + num)
		},
		Battery = new BatteryInfo(76.0 + num2 * 2.0, OnAc: true, Charging: true, 18.4, 47.0, 7.5),
		Processes = new ProcessInfo[4]
		{
			new ProcessInfo("Cyberpunk2077", 18.0 + num4 * 0.3, 9420.0),
			new ProcessInfo("chrome", 6.2 + num * 2.0, 2310.0),
			new ProcessInfo("Discord", 1.8, 610.0),
			new ProcessInfo("explorer", 0.7, 184.0)
		},
		Weather = new WeatherInfo("Zürich", 9.4, 7.1, 2, IsDay: true, 11.0, 78.0, new WeatherDay[4]
		{
			new WeatherDay(dateTime.Date, 5.2, 11.8, 2),
			new WeatherDay(dateTime.Date.AddDays(1.0), 4.1, 9.6, 61),
			new WeatherDay(dateTime.Date.AddDays(2.0), 2.8, 8.3, 3),
			new WeatherDay(dateTime.Date.AddDays(3.0), 1.0, 10.4, 0)
		}, dateTime),
		Fps = new FpsInfo("Cyberpunk2077", 118.0 + num * 9.0, 1000.0 / (118.0 + num * 9.0), 86.0 + num * 3.0),
		Ping = new PingInfo("1.1.1.1", 9.0 + Math.Abs(num) * 4.0 + (double)((tick % 17 == 0) ? 18 : 0), 0.6, 1.8, (tick % 40 < 2) ? 3.3 : 0.0),
		Media = new MediaInfo("Midnight City", "M83", "Spotify", Playing: true, 74 + tick % 170, 244.0, null),
		Usage = new UsageInfo(42.0 + num4 * 1.05 + 34.0 + num6 * 2.6 + 50.0, 1.84, 9.6, 31.2, 18400000000.0, 1200000000.0, 96000000000.0, 7400000000.0, 412000000000.0, 31000000000.0),
		System = new SystemInfo("Windows 11 Pro", "24H2 · 26100.2161", TimeSpan.FromHours(51.3) + TimeSpan.FromSeconds(tick), dateTime.AddHours(-51.3), 284, 3912),
		Volumes = new VolumeInfo[3]
		{
			new VolumeInfo("C:", "System", 1863.0, 1248.0),
			new VolumeInfo("D:", "Games", 3726.0, 1602.0),
			new VolumeInfo("E:", "Backup", 7452.0, 520.0)
		},
		
		};
	}

	public void Dispose()
	{
	}
}

