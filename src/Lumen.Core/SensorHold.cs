using System;
using System.Collections.Generic;
using System.Linq;

namespace Lumen.Core;

/// <summary>
/// Real sensors sometimes report nothing for a single reading (a power or clock value, a drive
/// temperature, a fan or board sensor that is briefly missing). Every design then dropped the line or tile
/// for that second and everything below it jumped. The hold fills such a gap with the last value for a
/// short while; a sensor that stays away is let go after <see cref="Patience"/> readings.
/// </summary>
public sealed class SensorHold
{
	public const int Patience = 15;

	private Snapshot? last;

	private readonly Dictionary<string, int> misses = new();

	public Snapshot Apply(Snapshot now)
	{
		Snapshot? before = last;
		if (before == null)
		{
			last = now;
			return now;
		}
		double? Keep(string key, double? value, double? previous)
		{
			if (value.HasValue && double.IsFinite(value.Value))
			{
				misses.Remove(key);
				return value;
			}
			if (!previous.HasValue)
			{
				return value;
			}
			int n = misses.GetValueOrDefault(key) + 1;
			misses[key] = n;
			return n <= Patience ? previous : value;
		}
		CpuInfo? cpu = now.Cpu;
		if (cpu != null && before.Cpu is CpuInfo c)
		{
			cpu = cpu with
			{
				Temperature = Keep("cpu.temp", cpu.Temperature, c.Temperature),
				Load = Keep("cpu.load", cpu.Load, c.Load),
				ClockMhz = Keep("cpu.clock", cpu.ClockMhz, c.ClockMhz),
				PowerW = Keep("cpu.power", cpu.PowerW, c.PowerW),
				Voltage = Keep("cpu.voltage", cpu.Voltage, c.Voltage),
				FanRpm = Keep("cpu.fan", cpu.FanRpm, c.FanRpm)
			};
		}
		var gpus = now.Gpus.Select(g =>
		{
			GpuInfo? p = before.Gpus.FirstOrDefault(x => x.Id == g.Id);
			if (p == null)
			{
				return g;
			}
			string k = "gpu:" + g.Id + ".";
			return g with
			{
				Temperature = Keep(k + "temp", g.Temperature, p.Temperature),
				HotSpot = Keep(k + "hot", g.HotSpot, p.HotSpot),
				Load = Keep(k + "load", g.Load, p.Load),
				ClockMhz = Keep(k + "clock", g.ClockMhz, p.ClockMhz),
				PowerW = Keep(k + "power", g.PowerW, p.PowerW),
				FanRpm = Keep(k + "fan", g.FanRpm, p.FanRpm),
				MemoryUsedMb = Keep(k + "vram", g.MemoryUsedMb, p.MemoryUsedMb),
				MemoryTotalMb = Keep(k + "vramtotal", g.MemoryTotalMb, p.MemoryTotalMb),
				MemoryJunction = Keep(k + "junction", g.MemoryJunction, p.MemoryJunction)
			};
		}).ToList();
		var disks = now.Disks.Select(d =>
		{
			DiskInfo? p = before.Disks.FirstOrDefault(x => x.Id == d.Id);
			if (p == null)
			{
				return d;
			}
			string k = "disk:" + d.Id + ".";
			return d with
			{
				Temperature = Keep(k + "temp", d.Temperature, p.Temperature),
				UsedPercent = Keep(k + "used", d.UsedPercent, p.UsedPercent),
				ReadBytesPerSecond = Keep(k + "read", d.ReadBytesPerSecond, p.ReadBytesPerSecond),
				WriteBytesPerSecond = Keep(k + "write", d.WriteBytesPerSecond, p.WriteBytesPerSecond)
			};
		}).ToList();
		// Disks, fans and board sensors that vanish for a moment keep their place.
		disks.AddRange(Missing("disk", before.Disks, now.Disks, d => d.Id));
		var fans = now.Fans.ToList();
		foreach (var f in Missing("fan", before.Fans, now.Fans, f => f.Name))
		{
			int i = before.Fans.ToList().FindIndex(x => x.Name == f.Name);
			fans.Insert(Math.Min(Math.Max(0, i), fans.Count), f);
		}
		var temps = now.BoardTemps.ToList();
		foreach (var t in Missing("temp", before.BoardTemps, now.BoardTemps, t => t.Name))
		{
			int i = before.BoardTemps.ToList().FindIndex(x => x.Name == t.Name);
			temps.Insert(Math.Min(Math.Max(0, i), temps.Count), t);
		}
		NetworkInfo? net = now.Network;
		if (net != null && before.Network is NetworkInfo n0 && n0.Name == net.Name)
		{
			net = net with
			{
				DownloadBytesPerSecond = Keep("net.down", net.DownloadBytesPerSecond, n0.DownloadBytesPerSecond),
				UploadBytesPerSecond = Keep("net.up", net.UploadBytesPerSecond, n0.UploadBytesPerSecond),
				LinkBitsPerSecond = Keep("net.link", net.LinkBitsPerSecond, n0.LinkBitsPerSecond),
				Address = net.Address ?? n0.Address
			};
		}
		if (now.Memory != null)
		{
			misses.Remove("memory");
		}
		var result = now with { Cpu = cpu, Gpus = gpus, Disks = disks, Network = net, Memory = now.Memory ?? HoldMemory(before.Memory) };
		result = result with { Fans = fans, BoardTemps = temps };
		last = result;
		return result;
	}

	private MemoryInfo? HoldMemory(MemoryInfo? previous)
	{
		int n = misses.GetValueOrDefault("memory") + 1;
		misses["memory"] = n;
		return n <= Patience ? previous : null;
	}

	private IEnumerable<T> Missing<T>(string kind, IReadOnlyList<T> before, IReadOnlyList<T> now, Func<T, string> name)
	{
		var present = now.Select(name).ToHashSet();
		foreach (T item in before)
		{
			string key = kind + ":" + name(item) + ".gone";
			if (present.Contains(name(item)))
			{
				misses.Remove(key);
				continue;
			}
			int n = misses.GetValueOrDefault(key) + 1;
			misses[key] = n;
			if (n <= Patience)
			{
				yield return item;
			}
		}
	}
}
