using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Lumen.Core;

public sealed class UsageMeter
{
	public sealed class Day
	{
		public DateTime Date { get; set; }

		public double Kwh { get; set; }

		public double Down { get; set; }

		public double Up { get; set; }
	}

	private readonly string? path;

	private readonly List<Day> days;

	private DateTime lastSave = DateTime.MinValue;

	private DateTime? lastTick;

	public UsageMeter(string? path)
	{
		this.path = path;
		try
		{
			days = ((path != null && File.Exists(path)) ? (JsonSerializer.Deserialize<List<Day>>(File.ReadAllText(path)) ?? new List<Day>()) : new List<Day>());
		}
		catch
		{
			days = new List<Day>();
		}
	}

	public static double EstimateWatts(Snapshot s, double baseWatts)
	{
		return (s.Cpu?.PowerW).GetValueOrDefault() + s.Gpus.Where((GpuInfo g) => !g.Integrated).Sum((GpuInfo g) => g.PowerW.GetValueOrDefault()) + baseWatts;
	}

	public UsageInfo Record(Snapshot s, double baseWatts)
	{
		double num = EstimateWatts(s, baseWatts);
		DateTime? dateTime = lastTick;
		if (dateTime.HasValue)
		{
			DateTime valueOrDefault = dateTime.GetValueOrDefault();
			double totalSeconds = (s.Time - valueOrDefault).TotalSeconds;
			if (totalSeconds > 0.0 && totalSeconds < 30.0)
			{
				Day day = Today(s.Time.Date);
				day.Kwh += num * totalSeconds / 3600000.0;
				day.Down += (s.Network?.DownloadBytesPerSecond).GetValueOrDefault() * totalSeconds;
				day.Up += (s.Network?.UploadBytesPerSecond).GetValueOrDefault() * totalSeconds;
			}
		}
		lastTick = s.Time;
		if ((s.Time - lastSave).TotalSeconds >= 60.0)
		{
			Save(s.Time);
		}
		return Summarize(s.Time, num);
	}

	public UsageInfo Summarize(DateTime now, double watts)
	{
		DateTime today = now.Date;
		int num = (int)(today.DayOfWeek + 6) % 7;
		DateTime dateTime = today.AddDays(-num);
		DateTime dateTime2 = new DateTime(today.Year, today.Month, 1);
		List<Day> source = Since(today).ToList();
		List<Day> source2 = Since(dateTime).ToList();
		List<Day> source3 = Since(dateTime2).ToList();
		return new UsageInfo(watts, source.Sum((Day d) => d.Kwh), source2.Sum((Day d) => d.Kwh), source3.Sum((Day d) => d.Kwh), source.Sum((Day d) => d.Down), source.Sum((Day d) => d.Up), source2.Sum((Day d) => d.Down), source2.Sum((Day d) => d.Up), source3.Sum((Day d) => d.Down), source3.Sum((Day d) => d.Up));
		IEnumerable<Day> Since(DateTime from)
		{
			return days.Where((Day d) => d.Date >= from && d.Date <= today);
		}
	}

	private Day Today(DateTime date)
	{
		Day day = days.FirstOrDefault((Day x) => x.Date == date);
		if (day != null)
		{
			return day;
		}
		day = new Day
		{
			Date = date
		};
		days.Add(day);
		days.RemoveAll((Day x) => x.Date < date.AddDays(-62.0));
		return day;
	}

	public void Save(DateTime now)
	{
		lastSave = now;
		if (path == null)
		{
			return;
		}
		try
		{
			File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(days));
			File.Move(path + ".tmp", path, overwrite: true);
		}
		catch
		{
		}
	}
}
