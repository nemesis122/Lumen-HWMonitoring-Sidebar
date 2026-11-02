using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Lumen.Core;

public static class SensorMerge
{
	public sealed record PhysicalDisk(int Number, string Model, string Serial);

	public static string Key(string? name)
	{
		return new string((name ?? "").Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
	}

	public static List<GpuInfo> Gpus(IReadOnlyList<GpuInfo> primary, IReadOnlyList<GpuInfo> extra, List<string>? log = null)
	{
		List<GpuInfo> list = new List<GpuInfo>();
		List<GpuInfo> list2 = extra.ToList();
		foreach (GpuInfo p in primary)
		{
			GpuInfo gpuInfo = list2.FirstOrDefault((GpuInfo e) => Key(e.Name) == Key(p.Name));
			if (gpuInfo == null && !p.Integrated && primary.Count((GpuInfo x) => !x.Integrated) == 1 && list2.Count == 1)
			{
				gpuInfo = list2[0];
			}
			if (gpuInfo == null)
			{
				list.Add(p);
				continue;
			}
			list2.Remove(gpuInfo);
			GpuInfo gpuInfo2 = p with
			{
				Temperature = (p.Temperature ?? gpuInfo.Temperature),
				HotSpot = (p.HotSpot ?? gpuInfo.HotSpot),
				Load = (p.Load ?? gpuInfo.Load),
				ClockMhz = (p.ClockMhz ?? gpuInfo.ClockMhz),
				PowerW = (p.PowerW ?? gpuInfo.PowerW),
				FanRpm = (p.FanRpm ?? gpuInfo.FanRpm),
				MemoryUsedMb = (p.MemoryUsedMb ?? gpuInfo.MemoryUsedMb),
				MemoryTotalMb = (p.MemoryTotalMb ?? gpuInfo.MemoryTotalMb)
			};
			if (log != null && gpuInfo2 != p)
			{
				log.Add(p.Name + ": " + Filled(p, gpuInfo2) + " from Open Hardware Monitor");
			}
			list.Add(gpuInfo2);
		}
		foreach (GpuInfo item in list2)
		{
			log?.Add(item.Name + ": only found by Open Hardware Monitor");
			list.Add(item);
		}
		return list;
	}

	public static List<DiskInfo> Disks(IReadOnlyList<DiskInfo> primary, IReadOnlyList<DiskInfo> extra, List<string>? log = null)
	{
		List<DiskInfo> list = new List<DiskInfo>();
		List<DiskInfo> list2 = extra.ToList();
		foreach (DiskInfo p in primary)
		{
			DiskInfo diskInfo = list2.FirstOrDefault((DiskInfo e) => Key(e.Name) == Key(p.Name));
			if (diskInfo == null)
			{
				list.Add(p);
				continue;
			}
			list2.Remove(diskInfo);
			DiskInfo diskInfo2 = p with
			{
				Temperature = (p.Temperature ?? diskInfo.Temperature),
				UsedPercent = (p.UsedPercent ?? diskInfo.UsedPercent),
				ReadBytesPerSecond = (p.ReadBytesPerSecond ?? diskInfo.ReadBytesPerSecond),
				WriteBytesPerSecond = (p.WriteBytesPerSecond ?? diskInfo.WriteBytesPerSecond)
			};
			if (log != null && diskInfo2 != p)
			{
				log.Add(p.Name + ": " + Filled(p, diskInfo2) + " from Open Hardware Monitor");
			}
			list.Add(diskInfo2);
		}
		foreach (DiskInfo item in list2.Where((DiskInfo e) => e.Temperature.HasValue || e.UsedPercent.HasValue))
		{
			log?.Add(item.Name + ": only found by Open Hardware Monitor");
			list.Add(item);
		}
		return list;
	}

	public static CpuInfo? PreferCpu(CpuInfo? ext, CpuInfo? local)
	{
		if ((object)ext == null)
		{
			return local;
		}
		if ((object)local == null)
		{
			return ext;
		}
		return local with
		{
			Temperature = (ext.Temperature ?? local.Temperature),
			Load = (ext.Load ?? local.Load),
			CoreLoads = ((ext.CoreLoads.Count > 0) ? ext.CoreLoads : local.CoreLoads),
			ClockMhz = (ext.ClockMhz ?? local.ClockMhz),
			PowerW = (ext.PowerW ?? local.PowerW),
			Voltage = (ext.Voltage ?? local.Voltage),
			FanRpm = (ext.FanRpm ?? local.FanRpm)
		};
	}

	private static int Dedicated(GpuInfo g)
	{
		if (!g.Integrated)
		{
			return (g.FanRpm.HasValue ? 1 : 0) + (g.PowerW.HasValue ? 1 : 0) + (g.HotSpot.HasValue ? 1 : 0) + ((g.MemoryUsedMb > 1024.0) ? 1 : 0);
		}
		return 0;
	}

	public static List<GpuInfo> PreferGpus(IReadOnlyList<GpuInfo> ext, IReadOnlyList<GpuInfo> local, string source, List<string>? log = null)
	{
		List<GpuInfo> list = ext.ToList();
		List<GpuInfo> list2 = new List<GpuInfo>();
		foreach (GpuInfo p in local)
		{
			GpuInfo gpuInfo = list.FirstOrDefault((GpuInfo e) => Key(e.Name) == Key(p.Name));
			if ((object)gpuInfo == null)
			{
				gpuInfo = (p.Integrated ? (list.FirstOrDefault((GpuInfo e) => e.Integrated) ?? ((local.Count((GpuInfo x) => !x.Integrated) == 0) ? list.FirstOrDefault() : null)) : list.Where((GpuInfo e) => !e.Integrated).OrderByDescending(Dedicated).FirstOrDefault());
			}
			if (gpuInfo == null)
			{
				list2.Add(p);
				continue;
			}
			list.Remove(gpuInfo);
			GpuInfo item = p with
			{
				Temperature = (gpuInfo.Temperature ?? p.Temperature),
				HotSpot = (gpuInfo.HotSpot ?? p.HotSpot),
				Load = (gpuInfo.Load ?? p.Load),
				ClockMhz = (gpuInfo.ClockMhz ?? p.ClockMhz),
				PowerW = (gpuInfo.PowerW ?? p.PowerW),
				FanRpm = (gpuInfo.FanRpm ?? p.FanRpm),
				MemoryUsedMb = (gpuInfo.MemoryUsedMb ?? p.MemoryUsedMb),
				MemoryTotalMb = (gpuInfo.MemoryTotalMb ?? p.MemoryTotalMb)
			};
			log?.Add(p.Name + ": readings from " + source);
			list2.Add(item);
		}
		foreach (GpuInfo item2 in list.Where((GpuInfo e) => e.Temperature.HasValue || e.Load.HasValue))
		{
			log?.Add(item2.Name + ": only found by " + source);
			list2.Add(item2);
		}
		return list2;
	}

	private static bool SameDrive(string a, string b)
	{
		string text = Key(a);
		string text2 = Key(b);
		if (text.Length >= 6 && text2.Length >= 6)
		{
			if (!text.Contains(text2))
			{
				return text2.Contains(text);
			}
			return true;
		}
		return false;
	}

	public static List<DiskInfo> PreferDisks(IReadOnlyList<DiskInfo> ext, IReadOnlyList<DiskInfo> local, string source, List<string>? log = null)
	{
		List<DiskInfo> list = ext.ToList();
		List<DiskInfo> list2 = new List<DiskInfo>();
		foreach (DiskInfo p in local)
		{
			int? number = SensorPicker.DiskNumber(p.Id);
			DiskInfo diskInfo = list.FirstOrDefault((DiskInfo e) => number.HasValue && SensorPicker.DiskNumber(e.Id) == number) ?? list.FirstOrDefault((DiskInfo e) => (!number.HasValue || !SensorPicker.DiskNumber(e.Id).HasValue) && SameDrive(e.Name, p.Name));
			if (diskInfo == null)
			{
				list2.Add(p);
				continue;
			}
			list.Remove(diskInfo);
			DiskInfo item = p with
			{
				Temperature = (diskInfo.Temperature ?? p.Temperature),
				UsedPercent = (p.UsedPercent ?? diskInfo.UsedPercent),
				ReadBytesPerSecond = (diskInfo.ReadBytesPerSecond ?? p.ReadBytesPerSecond),
				WriteBytesPerSecond = (diskInfo.WriteBytesPerSecond ?? p.WriteBytesPerSecond)
			};
			log?.Add(p.Name + ": readings from " + source);
			list2.Add(item);
		}
		foreach (DiskInfo item2 in list.Where((DiskInfo e) => e.Temperature.HasValue))
		{
			log?.Add(item2.Name + ": only found by " + source);
			list2.Add(item2);
		}
		return list2;
	}

	public static int? MatchPhysical(string name, IReadOnlyList<PhysicalDisk> disks, ICollection<int> taken)
	{
		string text = Key(name);
		foreach (PhysicalDisk item in disks.Where((PhysicalDisk d) => !taken.Contains(d.Number)))
		{
			string text2 = Key(item.Serial);
			if (text2.Length >= 6)
			{
				string value = new string(text2.Chunk(2).SelectMany((char[] c) => c.Reverse()).ToArray());
				if (text.Contains(text2) || text.Contains(value))
				{
					return item.Number;
				}
			}
		}
		string model = WithoutSerial(name);
		return disks.Where((PhysicalDisk d) => !taken.Contains(d.Number) && SameDrive(d.Model, model)).Select((Func<PhysicalDisk, int?>)((PhysicalDisk d) => d.Number)).FirstOrDefault();
	}

	public static string WithoutSerial(string name)
	{
		return Regex.Replace(name, "\\s*\\([A-Za-z0-9_.\\-]{6,}\\)\\s*$", "").Trim();
	}

	private static string Filled<T>(T before, T after)
	{
		IEnumerable<string> values = from prop in typeof(T).GetProperties()
			where prop.PropertyType == typeof(double?) && prop.GetValue(before) == null && prop.GetValue(after) != null
			select prop.Name;
		return string.Join(", ", values);
	}
}
