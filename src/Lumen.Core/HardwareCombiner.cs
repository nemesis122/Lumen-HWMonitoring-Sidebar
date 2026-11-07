using System;
using System.Collections.Generic;
using System.Linq;

namespace Lumen.Core;

public sealed class HardwareCombiner
{
	private List<SensorNode> idNodes = new List<SensorNode>();

	private CpuInfo? idCpu;

	private List<GpuInfo>? idGpus;

	private List<DiskInfo> idDisks = new List<DiskInfo>();

	public bool HasIdentity => idGpus != null;

	public List<string> Log { get; } = new List<string>();

	public HardwareView Combine(HardwareInput input)
	{
		Log.Clear();
		bool paused = input.Builtin == null;
		IReadOnlyList<SensorNode> builtin;
		CpuInfo cpuInfo;
		List<GpuInfo> list;
		List<DiskInfo> list2;
		if (paused)
		{
			builtin = idNodes;
			cpuInfo = (((object)idCpu == null) ? null : idCpu with
			{
				Load = input.SystemLoad
			});
			list = (idGpus ?? new List<GpuInfo>()).ToList();
			list2 = idDisks.ToList();
		}
		else
		{
			builtin = input.Builtin;
			cpuInfo = SensorPicker.Cpu(builtin, input.SystemLoad);
			if (input.Ohm.Count > 0)
			{
				// OpenHardwareMonitor knows some older CPUs and boards that LibreHardwareMonitor misses.
				cpuInfo = SensorMerge.FillCpu(cpuInfo, SensorPicker.Cpu(input.Ohm, input.SystemLoad), Log);
			}
			if ((object)cpuInfo != null && !cpuInfo.Temperature.HasValue)
			{
				double? num = input.FallbackCpuTemperature?.Invoke();
				if (num.HasValue)
				{
					double valueOrDefault = num.GetValueOrDefault();
					cpuInfo = cpuInfo with
					{
						Temperature = valueOrDefault
					};
				}
			}
			list = builtin.Where((SensorNode n) => n.IsGpu).Select(SensorPicker.Gpu).ToList();
			list2 = builtin.Where((SensorNode n) => n.Type == "Storage").Select(SensorPicker.Disk).ToList();
			if (input.Ohm.Count > 0)
			{
				list = SensorMerge.Gpus(list, input.Ohm.Where((SensorNode n) => n.IsGpu).Select(SensorPicker.Gpu).ToList(), Log);
				list2 = SensorMerge.Disks(list2, input.Ohm.Where((SensorNode n) => n.Type == "Storage").Select(SensorPicker.Disk).ToList(), Log);
			}
			list2 = Number(list2, input.Physical);
			idNodes = builtin.Select((SensorNode n) => n with
			{
				Sensors = Array.Empty<SensorValue>(),
				Sub = Array.Empty<SensorNode>()
			}).ToList();
			idCpu = (((object)cpuInfo == null) ? null : new CpuInfo(cpuInfo.Name, null, null, Array.Empty<double>(), null, null, null, null));
			idGpus = list.Select((GpuInfo g) => new GpuInfo(g.Id, g.Name, null, null, null, null, null, null, null, g.MemoryTotalMb)
			{
				Integrated = g.Integrated
			}).ToList();
			idDisks = (from d in list2.Where(SensorPicker.HasReadings)
				select new DiskInfo(d.Id, d.Name, null, d.UsedPercent, null, null)).ToList();
		}
		IReadOnlyList<SensorPicker.FanReading> readOnlyList = null;
		IReadOnlyList<TempReading> readOnlyList2 = null;
		string text = builtin.FirstOrDefault((SensorNode n) => n.IsBoard)?.Name;
		(string, IReadOnlyList<SensorNode>)? external = input.External;
		if (external.HasValue)
		{
			(string, IReadOnlyList<SensorNode>) valueOrDefault2 = external.GetValueOrDefault();
			cpuInfo = SensorMerge.PreferCpu(SensorPicker.Cpu(valueOrDefault2.Item2), cpuInfo);
			list = SensorMerge.PreferGpus(valueOrDefault2.Item2.Where((SensorNode n) => n.IsGpu).Select(SensorPicker.Gpu).ToList(), list, valueOrDefault2.Item1, Log);
			list2 = SensorMerge.PreferDisks(Number(valueOrDefault2.Item2.Where((SensorNode n) => n.Type == "Storage").Select(SensorPicker.Disk).ToList(), input.Physical), list2, valueOrDefault2.Item1, Log);
			SensorNode sensorNode = valueOrDefault2.Item2.FirstOrDefault((SensorNode n) => n.IsBoard);
			if ((object)sensorNode != null)
			{
				IReadOnlyList<TempReading> readOnlyList3 = SensorPicker.BoardTemps(sensorNode);
				if (readOnlyList3.Count > 0)
				{
					readOnlyList2 = readOnlyList3;
				}
				IReadOnlyList<SensorPicker.FanReading> readOnlyList4 = SensorPicker.Fans(valueOrDefault2.Item2);
				if (readOnlyList4.Count > 0)
				{
					readOnlyList = readOnlyList4;
				}
				if (text == null && sensorNode.Name != "Mainboard")
				{
					text = sensorNode.Name;
				}
			}
		}
		list = list.OrderBy((GpuInfo g) => g.Integrated ? 1 : 0).ToList();
		list2 = (from d in list2.Where((DiskInfo d) => SensorPicker.HasReadings(d) || paused).Select(delegate(DiskInfo d)
			{
				int? num2 = SensorPicker.DiskNumber(d.Id);
				if (num2.HasValue)
				{
					int valueOrDefault3 = num2.GetValueOrDefault();
					if (input.Letters.TryGetValue(valueOrDefault3, out string value))
					{
						return d with
						{
							Letters = value
						};
					}
				}
				return d;
			})
			select (!d.UsedPercent.HasValue) ? d with
			{
				UsedPercent = UsedOnVolumes(d.Letters, input.Volumes)
			} : d).ToList();
		IReadOnlyList<SensorPicker.FanReading> fans = readOnlyList ?? SensorPicker.Fans(builtin);
		IReadOnlyList<TempReading> boardTemps = readOnlyList2 ?? SensorPicker.BoardTemps(builtin.FirstOrDefault((SensorNode n) => n.IsBoard));
		if (!paused && input.Ohm.Count > 0)
		{
			if (fans.Count == 0)
			{
				fans = SensorPicker.Fans(input.Ohm);
			}
			if (boardTemps.Count == 0)
			{
				boardTemps = SensorPicker.BoardTemps(input.Ohm.FirstOrDefault((SensorNode n) => n.IsBoard));
			}
			if (text == null)
			{
				text = input.Ohm.FirstOrDefault((SensorNode n) => n.IsBoard)?.Name;
			}
		}
		return new HardwareView(cpuInfo, list, list2, fans, boardTemps, text, builtin);
	}

	private static List<DiskInfo> Number(List<DiskInfo> disks, IReadOnlyList<SensorMerge.PhysicalDisk> physical)
	{
		HashSet<int> hashSet = disks.Select((DiskInfo d) => SensorPicker.DiskNumber(d.Id)).OfType<int>().ToHashSet();
		List<DiskInfo> list = new List<DiskInfo>();
		foreach (DiskInfo disk in disks)
		{
			int? num = SensorPicker.DiskNumber(disk.Id);
			if (!num.HasValue)
			{
				int? num2 = SensorMerge.MatchPhysical(disk.Name, physical, hashSet);
				if (num2.HasValue)
				{
					int valueOrDefault = num2.GetValueOrDefault();
					num = valueOrDefault;
					hashSet.Add(valueOrDefault);
				}
			}
			list.Add(disk with
			{
				Id = (num.HasValue ? ("/disk/" + num.GetValueOrDefault()) : disk.Id),
				Name = SensorMerge.WithoutSerial(disk.Name)
			});
		}
		return list;
	}

	private static double? UsedOnVolumes(string letters, IReadOnlyList<VolumeInfo> volumes)
	{
		if (letters.Length == 0)
		{
			return null;
		}
		string[] parts = letters.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		List<VolumeInfo> source = volumes.Where((VolumeInfo v) => parts.Contains<string>(v.Name, StringComparer.OrdinalIgnoreCase)).ToList();
		double num = source.Sum((VolumeInfo v) => v.TotalGb);
		if (!(num > 0.0))
		{
			return null;
		}
		return (num - source.Sum((VolumeInfo v) => v.FreeGb)) / num * 100.0;
	}
}
