using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public static class Readings
{
    public static List<Reading> For(WidgetKind kind, int index, Snapshot s, History h, Settings set, Palette p) =>
        Steady.Keep("readings|" + kind + "|" + index, set, s,
            Raw(kind, index, s, h, set, p).Where(r => set.Shows(WidgetSensors.ReadingKey(kind, r.Label))).ToList(),
            r => r.Label, r => r.Value.Length == 0 || r.Value.StartsWith("—"));

	private static List<Reading> Raw(WidgetKind kind, int index, Snapshot s, History h, Settings set, Palette p)
	{
		List<Reading> list = new List<Reading>();
		NetworkInfo network;
		double num;
		double max;
		double? linkBitsPerSecond;
		switch (kind)
		{
		case WidgetKind.Cpu:
		{
			CpuInfo cpu = s.Cpu;
			if ((object)cpu != null)
			{
				list.Add(new Reading(L.T("Temp"), Tc(cpu.Temperature), Format.Heat(cpu.Temperature), Heat(cpu.Temperature)));
				list.Add(new Reading(L.T("Load"), Format.Percent(cpu.Load), Share(cpu.Load, 100.0), p.Cool));
				linkBitsPerSecond = cpu.PowerW;
				if (linkBitsPerSecond.HasValue)
				{
					double valueOrDefault4 = linkBitsPerSecond.GetValueOrDefault();
					list.Add(new Reading(L.T("Power"), Format.Watts(valueOrDefault4), Share(valueOrDefault4, 250.0), p.At(Share(valueOrDefault4, 250.0) * 0.8)));
				}
				return list;
			}
			break;
		}
		case WidgetKind.Gpu:
		{
			GpuInfo gpuInfo = GpuGroup.Visible(s, set).ElementAtOrDefault(index);
			if ((object)gpuInfo == null)
			{
				break;
			}
			list.Add(new Reading(L.T("Temp"), Tc(gpuInfo.Temperature), Format.Heat(gpuInfo.Temperature), Heat(gpuInfo.Temperature)));
			list.Add(new Reading(L.T("Load"), Format.Percent(gpuInfo.Load), Share(gpuInfo.Load, 100.0), p.Cool));
			linkBitsPerSecond = gpuInfo.HotSpot;
			if (linkBitsPerSecond.HasValue)
			{
				double valueOrDefault2 = linkBitsPerSecond.GetValueOrDefault();
				list.Add(new Reading(L.T("Hot spot"), Tc(valueOrDefault2), Format.Heat(valueOrDefault2), Heat(valueOrDefault2)));
			}
			else
			{
				linkBitsPerSecond = gpuInfo.PowerW;
				if (linkBitsPerSecond.HasValue)
				{
					double valueOrDefault3 = linkBitsPerSecond.GetValueOrDefault();
					list.Add(new Reading(L.T("Power"), Format.Watts(valueOrDefault3), Share(valueOrDefault3, 400.0), p.At(Share(valueOrDefault3, 400.0) * 0.8)));
				}
			}
			return list;
		}
		case WidgetKind.Memory:
		{
			MemoryInfo memory = s.Memory;
			if ((object)memory != null)
			{
				list.Add(new Reading(L.T("Used"), Format.Percent(memory.Load), Share(memory.Load, 100.0), p.At(Share(memory.Load, 100.0) * 0.9)));
				list.Add(new Reading(L.T("2-min peak"), Format.Percent(Peak("mem.load")), Share(Peak("mem.load"), 100.0), p.Cool));
				return list;
			}
			break;
		}
		case WidgetKind.Cooling:
			if (s.Fans.Count > 0)
			{
				foreach (FanInfo item in s.Fans.Take(8))
				{
					string label = Names.Fan(item.Name, set);
					string value = Format.Number(item.Rpm);
					linkBitsPerSecond = item.Percent;
					double fraction;
					if (linkBitsPerSecond.HasValue)
					{
						double valueOrDefault5 = linkBitsPerSecond.GetValueOrDefault();
						fraction = Share(valueOrDefault5, 100.0);
					}
					else
					{
						fraction = Share(item.Rpm, 3000.0);
					}
					list.Add(new Reading(label, value, fraction, (item.Rpm > 0.0) ? p.Cool : p.Faint));
				}
				return list;
			}
			if (s.BoardTemps.Count <= 0)
			{
				break;
			}
			{
				foreach (TempReading item2 in s.BoardTemps.Take(6))
				{
					list.Add(new Reading(Names.Temp(item2.Name, set), Tc(item2.Celsius), Format.Heat(item2.Celsius), Heat(item2.Celsius)));
				}
				return list;
			}
		case WidgetKind.Drives:
			foreach (DiskInfo item3 in s.Disks.Where((DiskInfo d) => set.Shows("drive:" + d.Id)))
			{
				list.Add(new Reading((item3.Letters.Length > 0) ? item3.Letters : Names.Short(item3.Name), Tc(item3.Temperature), Format.Heat(item3.Temperature), Heat(item3.Temperature)));
			}
			if (list.Count > 0)
			{
				return list;
			}
			break;
		case WidgetKind.Volumes:
			foreach (VolumeInfo item4 in s.Volumes.Where((VolumeInfo v) => set.Shows("volume:" + v.Name)).Take(8))
			{
				double num2 = item4.UsedPercent / 100.0;
				list.Add(new Reading(item4.Name, Format.Percent(item4.UsedPercent), num2, (num2 >= 0.9) ? p.Ramp[^1] : ((num2 >= 0.8) ? p.Ramp[2] : p.Cool)));
			}
			if (list.Count > 0)
			{
				return list;
			}
			break;
		case WidgetKind.Network:
			{
				network = s.Network;
				if ((object)network == null)
				{
					break;
				}
				linkBitsPerSecond = network.LinkBitsPerSecond;
				if (linkBitsPerSecond.HasValue)
				{
					double valueOrDefault = linkBitsPerSecond.GetValueOrDefault();
					if (valueOrDefault > 0.0)
					{
						num = valueOrDefault;
						goto IL_0732;
					}
				}
				num = 1000000000.0;
				goto IL_0732;
			}
			IL_0732:
			max = num;
			// Download and upload share one logarithmic scale, so upload does not look empty next to a fast download.
			list.Add(new Reading("↓ " + L.T("Down"), Format.Rate(network.DownloadBytesPerSecond), Summaries.NetFill(network.DownloadBytesPerSecond, max), p.Cool));
			list.Add(new Reading("↑ " + L.T("Up"), Format.Rate(network.UploadBytesPerSecond), Summaries.NetFill(network.UploadBytesPerSecond, max), p.Calm));
			return list;
		}
		Summary summary = Summaries.For(kind, index, s, h, set, p);
		list.Add(new Reading("", (summary.Value + " " + ((summary.Value == "—") ? "" : summary.Unit)).Trim(), summary.Fraction, (summary.Color == p.Ink) ? p.Cool : summary.Color));
		linkBitsPerSecond = summary.Second;
		if (linkBitsPerSecond.HasValue)
		{
			double valueOrDefault6 = linkBitsPerSecond.GetValueOrDefault();
			list.Add(new Reading(L.T("Load"), summary.SecondLabel, valueOrDefault6, p.Cool));
		}
		return list;
		Color Heat(double? c)
		{
			if (c.HasValue)
			{
				double valueOrDefault7 = c.GetValueOrDefault();
				if (double.IsFinite(valueOrDefault7))
				{
					if (!(valueOrDefault7 >= set.TemperatureWarning))
					{
						return p.At(Format.Heat(valueOrDefault7));
					}
					return p.Ramp[^1];
				}
			}
			return p.Faint;
		}
		double Peak(string key)
		{
			double[] array = h.Read(key);
			if (array.Length == 0)
			{
				return 0.0;
			}
			return array.Max();
		}
		static double Share(double? v, double num3)
		{
			if (v.HasValue)
			{
				double valueOrDefault7 = v.GetValueOrDefault();
				if (double.IsFinite(valueOrDefault7) && num3 > 0.0)
				{
					return Math.Clamp(valueOrDefault7 / num3, 0.0, 1.0);
				}
			}
			return 0.0;
		}
		string Tc(double? c)
		{
			return Format.Temperature(c, set.Fahrenheit) + " " + Format.TemperatureUnit(set.Fahrenheit);
		}
	}

	public static string SeriesText(WidgetKind kind, double v)
	{
		switch (kind)
		{
		case WidgetKind.Cpu:
		case WidgetKind.Gpu:
		case WidgetKind.Memory:
			return Format.Percent(v);
		case WidgetKind.Network:
			return Format.Rate(v);
		case WidgetKind.Fps:
			return Format.Number(v) + " fps";
		case WidgetKind.Ping:
			return Format.Milliseconds(v);
		case WidgetKind.Power:
			return Format.Watts(v);
		default:
			return Format.Number(v);
		}
	}

	public static Color TextOn(Color c, Palette p)
	{
		if (!(0.2126 * (double)(int)c.R + 0.7152 * (double)(int)c.G + 0.0722 * (double)(int)c.B > 140.0))
		{
			return p.Ink;
		}
		return Color.Parse("#0B0E14");
	}
}
