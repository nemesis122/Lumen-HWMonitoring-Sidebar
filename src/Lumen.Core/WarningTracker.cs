using System;
using System.Collections.Generic;
using System.Linq;

namespace Lumen.Core;

public sealed class WarningTracker
{
	public const double Hysteresis = 5.0;

	public static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(5.0);

	private readonly Dictionary<string, (bool armed, DateTime last)> state = new Dictionary<string, (bool, DateTime)>();

	public IReadOnlyList<Warning> Check(Snapshot s, double threshold)
	{
		List<(string, string, double?, double)> list = new List<(string, string, double?, double)>();
		list.Add(("cpu", s.Cpu?.Name ?? "CPU", s.Cpu?.Temperature, threshold));
		list.AddRange(s.Gpus.Select((GpuInfo g) => ("gpu." + g.Id, Name: g.Name, Temperature: g.Temperature, threshold: threshold)));
		list.AddRange(s.Gpus.Select((GpuInfo g) => ("gpu." + g.Id + ".hot", g.Name + " Hot Spot", HotSpot: g.HotSpot, threshold + 10.0)));
		list.AddRange(s.Disks.Select((DiskInfo d) => ("disk." + d.Id, Name: d.Name, Temperature: d.Temperature, Math.Min(threshold, 70.0))));
		List<Warning> list2 = new List<Warning>();
		foreach (var (key, name, num, num2) in list)
		{
			if (!num.HasValue)
			{
				continue;
			}
			double valueOrDefault = num.GetValueOrDefault();
			if (double.IsFinite(valueOrDefault))
			{
				bool flag;
				DateTime dateTime;
				if (state.TryGetValue(key, out (bool, DateTime) value))
				{
					(flag, dateTime) = value;
				}
				else
				{
					DateTime minValue = DateTime.MinValue;
					dateTime = minValue;
					flag = true;
				}
				if (flag && valueOrDefault >= num2 && s.Time - dateTime >= Cooldown)
				{
					list2.Add(new Warning(key, name, valueOrDefault));
					state[key] = (false, s.Time);
				}
				else if (!flag && valueOrDefault <= num2 - 5.0)
				{
					state[key] = (true, dateTime);
				}
			}
		}
		return list2;
	}
}
