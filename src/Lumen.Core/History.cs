using System;
using System.Collections.Generic;

namespace Lumen.Core;

public sealed class History
{
	public const int Capacity = 120;

	private readonly Dictionary<string, double[]> series = new Dictionary<string, double[]>();

	private readonly Dictionary<string, int> counts = new Dictionary<string, int>();

	private readonly Dictionary<string, int> heads = new Dictionary<string, int>();

	public void Add(string key, double? value)
	{
		if (!series.TryGetValue(key, out double[] value2))
		{
			value2 = new double[120];
			series[key] = value2;
			counts[key] = 0;
			heads[key] = 0;
		}
		int num = heads[key];
		double[] array = value2;
		double num2;
		if (value.HasValue)
		{
			double valueOrDefault = value.GetValueOrDefault();
			if (double.IsFinite(valueOrDefault))
			{
				num2 = valueOrDefault;
				goto IL_0073;
			}
		}
		num2 = double.NaN;
		goto IL_0073;
		IL_0073:
		array[num] = num2;
		heads[key] = (num + 1) % 120;
		counts[key] = Math.Min(120, counts[key] + 1);
	}

	public double[] Read(string key)
	{
		if (!series.TryGetValue(key, out double[] value))
		{
			return Array.Empty<double>();
		}
		int num = counts[key];
		int num2 = heads[key];
		double[] array = new double[num];
		for (int i = 0; i < num; i++)
		{
			array[i] = value[(num2 - num + i + 240) % 120];
		}
		return array;
	}

	public void Record(Snapshot s)
	{
		Add("cpu.load", s.Cpu?.Load);
		Add("cpu.temp", s.Cpu?.Temperature);
		foreach (GpuInfo gpu in s.Gpus)
		{
			Add("gpu." + gpu.Id + ".load", gpu.Load);
			Add("gpu." + gpu.Id + ".temp", gpu.Temperature);
		}
		MemoryInfo memory = s.Memory;
		if ((object)memory != null)
		{
			Add("mem.load", memory.Load);
		}
		foreach (DiskInfo disk in s.Disks)
		{
			Add("disk." + disk.Id + ".read", disk.ReadBytesPerSecond);
			Add("disk." + disk.Id + ".write", disk.WriteBytesPerSecond);
		}
		Add("net.down", s.Network?.DownloadBytesPerSecond);
		Add("net.up", s.Network?.UploadBytesPerSecond);
		if (s.Ping != null)
		{
			Add("ping", s.Ping.Ms);
		}
		Add("fps", s.Fps?.Fps);
		if (s.Usage != null)
		{
			Add("power", s.Usage.WattsNow);
		}
	}
}
