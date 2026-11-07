using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public static class Summaries
{
    /// <summary>The weather condition in words ("Partly cloudy", "Teilweise bewölkt"), never the raw code name.</summary>
    public static string Sky(int code)
    {
        string group = WeatherClient.Group(code);
        string text = L.T(group);
        if (text != group) return text;
        text = group.Replace('-', ' ');
        return text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
    }

    public static Summary For(WidgetKind kind, int index, Snapshot s, History h, Settings set, Palette p)
    {
        var summary = Raw(kind, index, s, h, set, p);
        return WidgetSensors.FilterSummary(kind, summary, set);
    }

	private static Summary Raw(WidgetKind kind, int index, Snapshot s, History h, Settings set, Palette p)
	{
		string unit = Format.TemperatureUnit(set.Fahrenheit);
		NetworkInfo network;
		string item;
		string item2;
		double num2;
		MediaInfo media;
		double num;
		double fraction2;
		double? positionSeconds;
		object obj2;
		string value3;
		double value5;
		switch (kind)
		{
		case WidgetKind.Cpu:
		{
			CpuInfo cpu = s.Cpu;
			return new Summary("M5,3 H11 A2,2 0 0 1 13,5 V11 A2,2 0 0 1 11,13 H5 A2,2 0 0 1 3,11 V5 A2,2 0 0 1 5,3 Z M6.5,6.5 H9.5 V9.5 H6.5 Z M6,1 V3 M10,1 V3 M6,13 V15 M10,13 V15 M1,6 H3 M1,10 H3 M13,6 H15 M13,10 H15", L.T("Processor"), T(cpu?.Temperature), unit, Heat(cpu?.Temperature), Format.Heat(cpu?.Temperature), L.T("Load") + " " + Format.Percent(cpu?.Load), h.Read("cpu.load"), 100.0)
			{
				Name = Names.Short(cpu?.Name),
				Second = (((object)cpu == null) ? ((double?)null) : (cpu.Load / 100.0)),
				SecondLabel = Format.Percent(cpu?.Load)
			};
		}
		case WidgetKind.Gpu:
		{
			GpuInfo gpuInfo = GpuGroup.Visible(s, set).ElementAtOrDefault(index);
			string title = (((object)gpuInfo != null && gpuInfo.Integrated) ? L.T("Integrated") : L.T("Graphics"));
			return new Summary("M1.5,4 H14.5 V12 H1.5 Z M6,8 m-2.2,0 a2.2,2.2 0 1 0 4.4,0 a2.2,2.2 0 1 0 -4.4,0 M10,6.5 H12.5 M10,9.5 H12.5 M3,12 V14", title, T(gpuInfo?.Temperature), unit, Heat(gpuInfo?.Temperature), Format.Heat(gpuInfo?.Temperature), L.T("Load") + " " + Format.Percent(gpuInfo?.Load), ((object)gpuInfo == null) ? Array.Empty<double>() : h.Read("gpu." + gpuInfo.Id + ".load"), 100.0)
			{
				Name = Names.Short(gpuInfo?.Name),
				Second = (((object)gpuInfo == null) ? ((double?)null) : (gpuInfo.Load / 100.0)),
				SecondLabel = Format.Percent(gpuInfo?.Load)
			};
		}
		case WidgetKind.Fps:
		{
			FpsInfo fps = s.Fps;
			return new Summary("M2,12 A6,6 0 0 1 14,12 M8,12 L11,7.5 M4.2,8.6 L5,9.3 M11.8,8.6 L11,9.3 M8,6 V7", "FPS", Format.Number(fps?.Fps), "fps", FpsCard.Color(fps?.Fps, p), Math.Clamp((fps?.Fps ?? 0.0) / 144.0, 0.0, 1.0), ((object)fps == null) ? L.T("No game in the foreground.") : fps.App, h.Read("fps"), null);
		}
		case WidgetKind.Memory:
		{
			MemoryInfo memory = s.Memory;
			return new Summary("M1.5,5 H14.5 V11 H1.5 Z M4,7 V9 M6.5,7 V9 M9,7 V9 M11.5,7 V9 M3,11 V13 M6,11 V13 M10,11 V13 M13,11 V13", L.T("Memory"), Format.Number(memory?.Load), "%", p.At(Format.Share(memory?.Load) * 0.9), Format.Share(memory?.Load), ((object)memory == null) ? "" : WidgetSensors.MemoryCaption(memory, set), h.Read("mem.load"), 100.0);
		}
		case WidgetKind.Battery:
		{
			BatteryInfo battery = s.Battery;
			Color color = (((object)battery == null || battery.Charging || battery.OnAc) ? p.Calm : p.At(Math.Clamp((60.0 - battery.Percent) / 50.0, 0.0, 1.0) * 0.5 + ((battery.Percent < 15.0) ? 0.5 : 0.0)));
			return new Summary("M2,4.5 H12.5 A1,1 0 0 1 13.5,5.5 V10.5 A1,1 0 0 1 12.5,11.5 H2 A1,1 0 0 1 1,10.5 V5.5 A1,1 0 0 1 2,4.5 Z M15,6.8 V9.2 M3.5,7 V9 M6,7 V9", L.T("Battery"), Format.Number(battery?.Percent), "%", color, (battery?.Percent ?? 0.0) / 100.0, ((object)battery == null) ? "" : (battery.Charging ? L.T("Charging") : (battery.OnAc ? L.T("Plugged in") : L.T("On battery"))), Array.Empty<double>(), null);
		}
		case WidgetKind.Cooling:
		{
			TempReading tempReading = CoolingHero(s);
			return new Summary("M8,8 m-1.3,0 a1.3,1.3 0 1 0 2.6,0 a1.3,1.3 0 1 0 -2.6,0 M8,6.7 C7.2,4.5 7.6,2 9.6,1.8 C11.4,1.7 11,4.6 9.2,7.2 M9.3,8 C11.5,7.2 14,7.6 14.2,9.6 C14.3,11.4 11.4,11 8.8,9.2 M8,9.3 C8.8,11.5 8.4,14 6.4,14.2 C4.6,14.3 5,11.4 6.8,8.8 M6.7,8 C4.5,8.8 2,8.4 1.8,6.4 C1.7,4.6 4.6,5 7.2,6.8", L.T("Cooling"), T(tempReading?.Celsius), unit, Heat(((object)tempReading != null) ? new double?(tempReading.Celsius + 5.0) : ((double?)null)), Format.Heat(((object)tempReading != null) ? new double?(tempReading.Celsius + 5.0) : ((double?)null)), (s.Fans.Count == 0) ? "" : $"{L.T("Fans")} {s.Fans.Count((FanInfo f) => f.Rpm > 0.0)}/{s.Fans.Count}", Array.Empty<double>(), null)
			{
				Name = Names.Short(s.BoardName),
				HeroLabel = ((object)tempReading == null) ? "" : Names.Temp(tempReading.Name, set)
			};
		}
		case WidgetKind.Network:
		{
			network = s.Network;
			(string Number, string Unit) tuple2 = Format.Split(Format.Rate(network?.DownloadBytesPerSecond));
			item = tuple2.Number;
			item2 = tuple2.Unit;
			num2 = NetFill(network?.DownloadBytesPerSecond, network?.LinkBitsPerSecond);
			goto IL_080e;
		}
		case WidgetKind.Ping:
		{
			// No caption: the jitter is one of the extras and would otherwise show twice.
			PingInfo ping = s.Ping;
			var (value4, unit4) = Format.Split(Format.Milliseconds(ping?.Ms));
			return new Summary("M2,13 L5,13 L7,4 L9.5,12 L11,8 L14,8", "Ping", value4, unit4, PingColor(ping?.Ms, p), Math.Clamp((ping?.Ms).GetValueOrDefault() / 150.0, 0.0, 1.0), "", h.Read("ping"), null);
		}
		case WidgetKind.Power:
		{
			UsageInfo usage2 = s.Usage;
			return new Summary("M9,1.5 L3.5,9 H8 L7,14.5 L12.5,7 H8 Z", L.T("Power"), Format.Number(usage2?.WattsNow), "W", p.At(Math.Clamp((usage2?.WattsNow ?? 0.0) / 600.0, 0.0, 1.0)), Math.Clamp((usage2?.WattsNow ?? 0.0) / 600.0, 0.0, 1.0), ((object)usage2 == null) ? "" : (L.T("Today") + " " + Format.Number(usage2.KwhToday, "0.00") + " kWh"), h.Read("power"), null);
		}
		case WidgetKind.Weather:
		{
			WeatherInfo weather = s.Weather;
			return new Summary("M4.5,12.5 H11.5 A3,3 0 0 0 11.7,6.5 A4,4 0 0 0 4.1,7.6 A2.5,2.5 0 0 0 4.5,12.5 Z", weather?.Place ?? L.T("Weather"), T(weather?.TemperatureC), unit, p.At(Math.Clamp(((weather?.TemperatureC ?? 0.0) + 10.0) / 45.0, 0.0, 1.0)), Math.Clamp(((weather?.TemperatureC ?? 0.0) + 10.0) / 45.0, 0.0, 1.0), ((object)weather == null) ? L.T("Set a place in the settings.") : Sky(weather.Code), Array.Empty<double>(), null);
		}
		case WidgetKind.System:
		{
			SystemInfo system = s.System;
			string value2;
			string unit3;
			if ((object)system != null)
			{
				(value2, unit3) = SplitDuration(system.Uptime);
			}
			else
			{
				value2 = "—";
				unit3 = "";
			}
			return new Summary("M1.5,3 H14.5 V11 H1.5 Z M6,14 H10 M8,11 V14", L.T("Uptime"), value2, unit3, p.Ink, Math.Clamp((system?.Uptime.TotalDays ?? 0.0) / 7.0, 0.0, 1.0), system?.Os ?? "", Array.Empty<double>(), null);
		}
		case WidgetKind.Drives:
		{
			List<DiskInfo> list = s.Disks.Where((DiskInfo d) => set.Shows("drive:" + d.Id)).ToList();
			DiskInfo diskInfo = (from d in list
				where d.Temperature.HasValue
				orderby d.Temperature descending
				select d).FirstOrDefault();
			string caption = $"{list.Count} {((list.Count == 1) ? L.T("drive") : L.T("drives"))}";
			return new Summary("M2,4 A1.5,1.5 0 0 1 3.5,2.5 H12.5 A1.5,1.5 0 0 1 14,4 V12 A1.5,1.5 0 0 1 12.5,13.5 H3.5 A1.5,1.5 0 0 1 2,12 Z M2,9.5 H14 M11,11.5 H12", L.T("Drives"), T(diskInfo?.Temperature), unit, Heat(diskInfo?.Temperature), Format.Heat(diskInfo?.Temperature), caption, Array.Empty<double>(), null)
			{
				HeroLabel = ((object)diskInfo == null || list.Count < 2) ? "" : (L.T("Hottest") + " · " + DriveName(diskInfo))
			};
		}
		case WidgetKind.Volumes:
		{
			VolumeInfo volumeInfo = (from v in s.Volumes.Where((VolumeInfo v) => set.Shows("volume:" + v.Name)).ToList()
				orderby v.UsedPercent descending
				select v).FirstOrDefault();
			double num4 = (volumeInfo?.UsedPercent ?? 0.0) / 100.0;
			Color color2 = ((num4 >= 0.9) ? p.Ramp[^1] : ((num4 >= 0.8) ? p.Ramp[2] : p.Cool));
			return new Summary("M2,4 H14 V12 H2 Z M4.5,8 H8 M11.5,8 h0.01", L.T("Storage space"), Format.Number(volumeInfo?.UsedPercent), "%", color2, num4, ((object)volumeInfo == null) ? "" : (volumeInfo.Name + " " + L.F("{0} free", Format.Capacity(volumeInfo.FreeGb))), Array.Empty<double>(), null);
		}
		case WidgetKind.Clock:
		{
			DateTime time = s.Time;
			double fraction = time.TimeOfDay.TotalHours / 24.0;
			return new Summary("M8,1.5 A6.5,6.5 0 1 1 7.99,1.5 Z M8,4.5 V8 L10.5,9.5", L.T("Today"), time.ToString("HH:mm", L.Culture), time.ToString("ss", L.Culture), p.Ink, fraction, time.ToString("ddd d. MMM", L.Culture), Array.Empty<double>(), null);
		}
		case WidgetKind.WorldClocks:
		{
			string text = set.WorldClocks.FirstOrDefault();
			TimeZoneInfo timeZoneInfo = null;
			try
			{
				if (text != null)
				{
					timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(text);
				}
			}
			catch
			{
			}
			if (timeZoneInfo == null)
			{
				return new Summary("M8,1.5 A6.5,6.5 0 1 1 7.99,1.5 Z M1.5,8 H14.5 M8,1.5 C5.5,4 5.5,12 8,14.5 M8,1.5 C10.5,4 10.5,12 8,14.5", L.T("World clocks"), "—", "", p.Faint, 0.0, L.T("Up to four cities."), Array.Empty<double>(), null);
			}
			DateTime dateTime = TimeZoneInfo.ConvertTime(s.Time, timeZoneInfo);
			double totalHours = (dateTime - s.Time).TotalHours;
			return new Summary("M8,1.5 A6.5,6.5 0 1 1 7.99,1.5 Z M1.5,8 H14.5 M8,1.5 C5.5,4 5.5,12 8,14.5 M8,1.5 C10.5,4 10.5,12 8,14.5", WorldClocksCard.City(text), dateTime.ToString("HH:mm", L.Culture), "", p.Ink, dateTime.TimeOfDay.TotalHours / 24.0, $"{((totalHours >= 0.0) ? "+" : "−")}{Math.Abs(totalHours):0.#} h" + ((set.WorldClocks.Count > 1) ? $" · +{set.WorldClocks.Count - 1}" : ""), Array.Empty<double>(), null);
		}
		case WidgetKind.Calendar:
		{
			DateTime date = s.Time.Date;
			int num3 = DateTime.DaysInMonth(date.Year, date.Month);
			return new Summary("M2,3.5 H14 V14 H2 Z M2,6.5 H14 M5,2 V5 M11,2 V5", date.ToString("MMMM yyyy", L.Culture), date.Day.ToString(L.Culture), date.ToString("MMM", L.Culture), p.Ink, (double)date.Day / (double)num3, $"{date.ToString("dddd", L.Culture)} · {L.T("Week")} {ISOWeek.GetWeekOfYear(date)}", Array.Empty<double>(), null);
		}
		case WidgetKind.Media:
			media = s.Media;
			if ((object)media == null)
			{
				return new Summary("M6,12 V3 L13,1.8 V10.5 M6,12 A2,2 0 1 1 5.99,12 Z M13,10.5 A2,2 0 1 1 12.99,10.5 Z", L.T("Now playing"), "—", "", p.Faint, 0.0, L.T("Nothing is playing."), Array.Empty<double>(), null);
			}
			positionSeconds = media.PositionSeconds;
			if (positionSeconds.HasValue)
			{
				double valueOrDefault = positionSeconds.GetValueOrDefault();
				positionSeconds = media.DurationSeconds;
				if (positionSeconds.HasValue)
				{
					double valueOrDefault2 = positionSeconds.GetValueOrDefault();
					if (valueOrDefault2 > 0.0)
					{
						num = valueOrDefault / valueOrDefault2;
						goto IL_12aa;
					}
				}
			}
			num = 0.0;
			goto IL_12aa;
		case WidgetKind.Processes:
		{
			ProcessInfo processInfo = s.Processes.OrderByDescending((ProcessInfo x) => x.CpuPercent).FirstOrDefault();
			return new Summary("M2,2.5 H6.5 V7 H2 Z M9.5,2.5 H14 V7 H9.5 Z M2,9 H6.5 V13.5 H2 Z M9.5,9 H14 V13.5 H9.5 Z", L.T("Top apps"), Format.Number(processInfo?.CpuPercent, "0.#"), "%", p.At(Math.Clamp((processInfo?.CpuPercent ?? 0.0) / 100.0, 0.0, 1.0)), Math.Clamp((processInfo?.CpuPercent ?? 0.0) / 100.0, 0.0, 1.0), ((object)processInfo == null) ? "" : (processInfo.Name + " · " + Format.Number(processInfo.MemoryMb / 1024.0, "0.0") + " GB"), Array.Empty<double>(), null);
		}
		case WidgetKind.DataUsage:
		{
			UsageInfo usage = s.Usage;
			var (value, unit2) = Format.Split(Format.Size(usage?.DownToday));
			return new Summary("M8,2 V10 M5,7 L8,10 L11,7 M3,13.5 H13", L.T("Data today"), value, unit2, p.Cool, 0.0, ((object)usage == null) ? "" : ("↑ " + Format.Size(usage.UpToday)), Array.Empty<double>(), null);
		}
		default:
			{
				return Summary.Empty;
			}
			IL_12aa:
			fraction2 = num;
			positionSeconds = media.PositionSeconds;
			if (positionSeconds.HasValue)
			{
				double valueOrDefault4 = positionSeconds.GetValueOrDefault();
				obj2 = TimeSpan.FromSeconds(valueOrDefault4).ToString((valueOrDefault4 >= 3600.0) ? "h\\:mm\\:ss" : "m\\:ss");
			}
			else
			{
				obj2 = "—";
			}
			value3 = (string)obj2;
			return new Summary("M6,12 V3 L13,1.8 V10.5 M6,12 A2,2 0 1 1 5.99,12 Z M13,10.5 A2,2 0 1 1 12.99,10.5 Z", (media.Artist.Length > 0) ? media.Artist : L.T("Now playing"), value3, "", media.Playing ? p.Cool : p.Soft, fraction2, media.Title, Array.Empty<double>(), null)
			{
				Name = media.App
			};
			IL_080e:
			value5 = num2;
			return new Summary("M5,13 V3 M2.5,5.5 L5,3 L7.5,5.5 M11,3 V13 M8.5,10.5 L11,13 L13.5,10.5", L.T("Network"), item, item2, p.Cool, Math.Clamp(value5, 0.0, 1.0), "↑ " + Format.Rate(network?.UploadBytesPerSecond), h.Read("net.down"), null)
			{
				Name = Names.Short(network?.Name)
			};
		}
		Color Heat(double? c)
		{
			if (c.HasValue)
			{
				double valueOrDefault5 = c.GetValueOrDefault();
				if (double.IsFinite(valueOrDefault5))
				{
					if (!(valueOrDefault5 >= set.TemperatureWarning))
					{
						return p.At(Format.Heat(valueOrDefault5));
					}
					return p.Ramp[^1];
				}
			}
			return p.Faint;
		}
		string T(double? c)
		{
			return Format.Temperature(c, set.Fahrenheit);
		}
	}

	public static string? ChartKey(WidgetKind kind)
	{
		return kind switch
		{
			WidgetKind.Cpu => "cpu.chart", 
			WidgetKind.Gpu => "gpu.chart", 
			WidgetKind.Network => "net.chart", 
			WidgetKind.Fps => "fps.chart", 
			WidgetKind.Ping => "ping.chart", 
			WidgetKind.Power => "power.chart", 
			_ => null, 
		};
	}

	// Fill of a download or upload bar, on a logarithmic scale from 1 KB/s up to the link speed
	// (1 Gbit/s when unknown). Download and upload use the same scale, so more traffic always means a
	// fuller bar, and a few MB/s of upload stay visible next to a fast download on a 10 Gbit link.
	public static double NetFill(double? bytesPerSecond, double? linkBitsPerSecond)
	{
		if (!bytesPerSecond.HasValue || !double.IsFinite(bytesPerSecond.Value) || bytesPerSecond.Value < 1024.0)
		{
			return 0.0;
		}
		double link = (linkBitsPerSecond.HasValue && linkBitsPerSecond.Value > 0.0) ? (linkBitsPerSecond.Value / 8.0) : 125000000.0;
		return Math.Clamp(Math.Log10(bytesPerSecond.Value / 1024.0) / Math.Log10(Math.Max(link, 2048.0) / 1024.0), 0.0, 1.0);
	}

	// The board sensor shown big on the cooling widget: the mainboard itself, otherwise the hottest one.
	public static TempReading? CoolingHero(Snapshot s)
	{
		return s.BoardTemps.FirstOrDefault((TempReading t) => t.Name == "Board") ?? s.BoardTemps.OrderByDescending((TempReading t) => t.Celsius).FirstOrDefault();
	}

	private static string DriveName(DiskInfo d)
	{
		if (d.Letters.Length <= 0)
		{
			return Names.Short(d.Name);
		}
		return d.Letters;
	}

	public static List<(string Label, string Value)> Extras(WidgetKind kind, int index, Snapshot s, History h, Settings set)
	{
		List<(string, string)> list = new List<(string, string)>();
		GpuInfo gpuInfo;
		object value;
		switch (kind)
		{
		case WidgetKind.Cpu:
            Add("cpu.clock", L.T("Clock speed"), Format.Clock(s.Cpu?.ClockMhz));
			Add("cpu.power", L.T("Power"), Format.Watts(s.Cpu?.PowerW));
			Add("cpu.voltage", L.T("Voltage"), Format.Volts(s.Cpu?.Voltage));
			Add("cpu.fan", L.T("Fan"), Format.Rpm(s.Cpu?.FanRpm));
			break;
		case WidgetKind.Gpu:
		{
			gpuInfo = GpuGroup.Visible(s, set).ElementAtOrDefault(index);
            Add("gpu.clock", L.T("Clock speed"), Format.Clock(gpuInfo?.ClockMhz));
			double? num = gpuInfo?.MemoryUsedMb;
			if (num.HasValue)
			{
				double valueOrDefault = num.GetValueOrDefault();
				num = gpuInfo.MemoryTotalMb;
				if (num.HasValue)
				{
					double valueOrDefault2 = num.GetValueOrDefault();
					if (valueOrDefault2 > 0.0)
					{
						value = Format.Number(valueOrDefault / 1024.0, "0.0") + "/" + Format.Number(valueOrDefault2 / 1024.0, "0.#") + " GB";
						goto IL_01cf;
					}
				}
			}
			value = "—";
			goto IL_01cf;
		}
		case WidgetKind.Memory:
			Add("mem.free", L.T("Available"), Format.Gigabytes(s.Memory?.AvailableGb));
			Add("mem.total", L.T("Installed"), Format.Gigabytes(s.Memory?.TotalGb));
			Add("mem.peak", L.T("2-min peak"), Format.Percent(Peak("mem.load")));
			break;
		case WidgetKind.Battery:
		{
			string label10 = L.T("Time left");
			double? num = s.Battery?.MinutesLeft;
			object value3;
			if (num.HasValue)
			{
				double valueOrDefault4 = num.GetValueOrDefault();
				value3 = Format.Duration(TimeSpan.FromMinutes(valueOrDefault4));
			}
			else
			{
				value3 = "—";
			}
			Add("battery.time", label10, (string)value3);
			Add("battery.rate", L.T("Rate"), Format.Watts(s.Battery?.PowerW));
			string label11 = L.T("Health");
			num = s.Battery?.WearPercent;
			object value4;
			if (num.HasValue)
			{
				double valueOrDefault5 = num.GetValueOrDefault();
				value4 = Format.Percent(100.0 - valueOrDefault5);
			}
			else
			{
				value4 = "—";
			}
			Add("battery.health", label11, (string)value4);
			break;
		}
		case WidgetKind.Cooling:
			Add("cooling.fans", L.T("Fans"), (s.Fans.Count == 0) ? "—" : $"{s.Fans.Count((FanInfo f) => f.Rpm > 0.0)}/{s.Fans.Count}");
			if (!set.Shows("cooling.board"))
			{
				break;
			}
			foreach (TempReading boardTemp in s.BoardTemps.Where((TempReading t) => t != CoolingHero(s)))
			{
				list.Add((Names.Temp(boardTemp.Name, set), Tc(boardTemp.Celsius)));
			}
			break;
		case WidgetKind.Network:
		{
			NetworkInfo network = s.Network;
			Add("net.link", L.T("Link speed"), Format.Link(network?.LinkBitsPerSecond));
			Add("net.use", L.T("Link use"), Format.Bits(((object)network == null) ? ((double?)null) : (network.DownloadBytesPerSecond * 8.0)));
			Add("net.peak", L.T("2-min peak"), Format.Rate(Peak("net.down")));
			Add("net.address", L.T("Address"), network?.Address ?? "—");
			string address = ExternalIp.Address;
			if (address != null)
			{
				Add("net.external", L.T("External IP"), address);
			}
			break;
		}
		case WidgetKind.Drives:
			if (set.Shows("drives.temps"))
			{
				List<DiskInfo> drives = s.Disks.Where((DiskInfo d) => set.Shows("drive:" + d.Id)).ToList();
				DiskInfo? hottest = set.Shows("drives.temp") ? drives.Where((DiskInfo d) => d.Temperature.HasValue).OrderByDescending((DiskInfo d) => d.Temperature).FirstOrDefault() : null;
				foreach (DiskInfo item in drives.Where((DiskInfo d) => d != hottest))
				{
					list.Add((DriveName(item), Tc(item.Temperature)));
				}
			}
			Add("drives.io", L.T("Read"), Format.Rate(s.Disks.Where((DiskInfo d) => set.Shows("drive:" + d.Id)).Sum((DiskInfo d) => d.ReadBytesPerSecond.GetValueOrDefault())));
			Add("drives.io", L.T("Write"), Format.Rate(s.Disks.Where((DiskInfo d) => set.Shows("drive:" + d.Id)).Sum((DiskInfo d) => d.WriteBytesPerSecond.GetValueOrDefault())));
			break;
		case WidgetKind.Volumes:
			if (!set.Shows("volumes.each"))
			{
				break;
			}
			foreach (VolumeInfo item2 in s.Volumes.Where((VolumeInfo v) => set.Shows("volume:" + v.Name)))
			{
				list.Add((item2.Name, L.F("{0} free", Format.Capacity(item2.FreeGb))));
			}
			break;
		case WidgetKind.Fps:
		{
			string label8 = L.T("Frame time");
			FpsInfo fps = s.Fps;
			Add("fps.frametime", label8, ((object)fps != null) ? Format.Milliseconds(fps.FrameTimeMs) : "—");
			break;
		}
		case WidgetKind.Ping:
		{
			Add("ping.jitter", L.T("Jitter"), Format.Milliseconds(s.Ping?.JitterMs));
			string label9 = L.T("Loss");
			PingInfo ping = s.Ping;
			Add("ping.loss", label9, ((object)ping != null) ? Format.Percent(ping.LossPercent) : "—");
			Add("ping.target", L.T("Target"), s.Ping?.Target ?? set.PingTarget);
			break;
		}
		case WidgetKind.Power:
		{
			string label4 = L.T("Today");
			UsageInfo usage3 = s.Usage;
            if (set.Shows("power.cost"))
            {
                Add("power.today", label4, usage3 != null ? Format.Money(usage3.KwhToday * set.PricePerKwh, set.Currency) : "—");
                Add("power.week", L.T("This week"), usage3 != null ? Format.Money(usage3.KwhWeek * set.PricePerKwh, set.Currency) : "—");
                Add("power.month", L.T("This month"), usage3 != null ? Format.Money(usage3.KwhMonth * set.PricePerKwh, set.Currency) : "—");
            }
			break;
		}
		case WidgetKind.Weather:
		{
			Add("weather.feels", L.T("Feels like"), Tc(s.Weather?.FeelsLikeC));
			string label3 = L.T("Wind");
			double? num = s.Weather?.WindKmh;
			object value2;
			if (num.HasValue)
			{
				double valueOrDefault3 = num.GetValueOrDefault();
				value2 = Format.Number(valueOrDefault3) + " km/h";
			}
			else
			{
				value2 = "—";
			}
			Add("weather.wind", label3, (string)value2);
			Add("weather.humidity", L.T("Humidity"), Format.Percent(s.Weather?.Humidity));
			break;
		}
		case WidgetKind.System:
		{
			string label5 = L.T("Started");
			SystemInfo system = s.System;
			Add("system.boot", label5, ((object)system != null) ? system.BootTime.ToString("ddd HH:mm", L.Culture) : "—");
			string label6 = L.T("Processes");
			SystemInfo system2 = s.System;
			Add("system.processes", label6, ((object)system2 != null) ? system2.Processes.ToString(L.Culture) : "—");
			string label7 = L.T("Threads");
			SystemInfo system3 = s.System;
			Add("system.threads", label7, ((object)system3 != null) ? system3.Threads.ToString(L.Culture) : "—");
			break;
		}
		case WidgetKind.DataUsage:
			{
				string label = L.T("This week");
				UsageInfo usage = s.Usage;
                    if (set.Shows("data.week"))
                    {
                        Add("data.week.down", label + " ↓", usage != null ? Format.Size(usage.DownWeek) : "—");
                        Add("data.week.up", label + " ↑", usage != null ? Format.Size(usage.UpWeek) : "—");
                    }
				string label2 = L.T("This month");
				UsageInfo usage2 = s.Usage;
                    if (set.Shows("data.month"))
                    {
                        Add("data.month.down", label2 + " ↓", usage2 != null ? Format.Size(usage2.DownMonth) : "—");
                        Add("data.month.up", label2 + " ↑", usage2 != null ? Format.Size(usage2.UpMonth) : "—");
                    }
				break;
			}
			IL_01cf:
			if (set.Shows("gpu.vram.used") || set.Shows("gpu.vram.total")) Add("gpu.vram", "VRAM", WidgetSensors.Vram(gpuInfo, set));
			Add("gpu.power", L.T("Power"), Format.Watts(gpuInfo?.PowerW));
			Add("gpu.hotspot", L.T("Hot spot"), Tc(gpuInfo?.HotSpot));
			Add("gpu.fan", L.T("Fan"), Format.Rpm(gpuInfo?.FanRpm));
			break;
		}
		return list;
		void Add(string key, string item, string item2)
		{
            if (key == "drives.io" && !set.Shows(item == L.T("Read") ? "drives.read" : "drives.write")) return;
            if (key == "power.cost" && !set.Shows("power.today")) return;
			if (set.Shows(key))
			{
				list.Add((item, item2));
			}
		}
		double? Peak(string key)
		{
			double[] array = h.Read(key);
			if (array.Length == 0)
			{
				return null;
			}
			return array.Max();
		}
		string Tc(double? c)
		{
			if (c.HasValue)
			{
				return Format.Temperature(c, set.Fahrenheit) + " " + Format.TemperatureUnit(set.Fahrenheit);
			}
			return "—";
		}
	}

	private static (string, string) SplitDuration(TimeSpan t)
	{
		string text = Format.Duration(t);
		int num = text.IndexOf(' ');
		if (num >= 0)
		{
			string item = text.Substring(0, num);
			string text2 = text;
			int num2 = num + 1;
			return (item, text2.Substring(num2, text2.Length - num2));
		}
		return (text, "");
	}

	public static Color PingColor(double? ms, Palette p)
	{
		if (ms.HasValue)
		{
			double valueOrDefault = ms.GetValueOrDefault();
			return p.At(Math.Clamp(valueOrDefault / 120.0, 0.0, 1.0) * 0.95);
		}
		return p.Faint;
	}
}



