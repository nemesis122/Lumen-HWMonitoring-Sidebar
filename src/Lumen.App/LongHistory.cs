using System;
using System.Collections.Generic;
using System.Linq;

namespace Lumen.App;

// Ten minutes of a value in five-second steps, for the chart that opens when a card is clicked.
// Kept outside the cards so it survives rebuilding the sidebar.
public static class LongHistory
{
	public const int Steps = 120;

	public static readonly TimeSpan Step = TimeSpan.FromSeconds(5);

	private sealed class Series
	{
		public readonly List<double> Values = new();
		public DateTime Start;
		public double Sum;
		public int Count;
	}

	private static readonly Dictionary<string, Series> all = new();

	public static void Add(string key, DateTime time, double? value)
	{
		if (!all.TryGetValue(key, out var s))
		{
			s = new Series { Start = time };
			all[key] = s;
		}
		if (time - s.Start >= Step || time < s.Start)
		{
			if (s.Count > 0)
			{
				s.Values.Add(s.Sum / s.Count);
				if (s.Values.Count > Steps)
				{
					s.Values.RemoveAt(0);
				}
			}
			s.Start = time;
			s.Sum = 0;
			s.Count = 0;
		}
		if (value is double v && double.IsFinite(v))
		{
			s.Sum += v;
			s.Count++;
		}
	}

	public static double[] Read(string key)
	{
		if (!all.TryGetValue(key, out var s))
		{
			return Array.Empty<double>();
		}
		var values = s.Values.ToList();
		if (s.Count > 0)
		{
			values.Add(s.Sum / s.Count);
		}
		return values.ToArray();
	}

	public static void Clear() => all.Clear();
}
