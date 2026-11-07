using System;
using System.Collections.Generic;
using System.Linq;

namespace Lumen.App;

// The last 60 readings of a value (and its lowest and highest since midnight), for the curves of the
// "Dial" design. Kept outside the cards so it survives rebuilding the sidebar.
public static class DayStats
{
	public const int Recent = 60;

	private sealed class Entry
	{
		public DateTime Day;
		public double Min = double.PositiveInfinity;
		public double Max = double.NegativeInfinity;
		public readonly List<double> Last = new();
		public DateTime Time;
	}

	private static readonly Dictionary<string, Entry> all = new();

	public static void Add(string key, DateTime time, double? value)
	{
		if (!all.TryGetValue(key, out var e))
		{
			e = new Entry { Day = time.Date };
			all[key] = e;
		}
		if (time.Date != e.Day)
		{
			e.Day = time.Date;
			e.Min = double.PositiveInfinity;
			e.Max = double.NegativeInfinity;
		}
		if (time == e.Time && e.Last.Count > 0)
		{
			// The same snapshot shown twice (two cards, a rebuild): count it once.
			return;
		}
		e.Time = time;
		double v = value is double x && double.IsFinite(x) ? x : double.NaN;
		e.Last.Add(v);
		if (e.Last.Count > Recent)
		{
			e.Last.RemoveAt(0);
		}
		if (!double.IsNaN(v))
		{
			e.Min = Math.Min(e.Min, v);
			e.Max = Math.Max(e.Max, v);
		}
	}

	public static (double Min, double Max)? Range(string key) =>
		all.TryGetValue(key, out var e) && e.Min <= e.Max ? (e.Min, e.Max) : null;

	public static double[] Series(string key) => all.TryGetValue(key, out var e) ? e.Last.ToArray() : Array.Empty<double>();

	public static void Clear() => all.Clear();
}
