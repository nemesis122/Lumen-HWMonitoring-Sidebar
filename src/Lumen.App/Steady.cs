using System;
using System.Collections.Generic;
using System.Linq;
using Lumen.Core;

namespace Lumen.App;

/// <summary>
/// Keeps a widget's lines in place when a sensor drops out for a moment. Real hardware sometimes reports
/// nothing for a reading (a fan that stops, a power value that is briefly missing); dropping the line for
/// that second made the card shorter and everything below it jump. A line that was shown keeps its place
/// and its last value for a short while; only when the sensor stays away does the line go.
/// </summary>
public static class Steady
{
	// About 30 updates (half a minute at the default rate) before a missing line is given up.
	private const int Patience = 30;

	private sealed class Memory
	{
		public object? Source;
		public DateTime Time;
		public object? Result;
		public List<(string Label, object Item)> Last = new();
		public Dictionary<string, int> Misses = new();
	}

	// One memory per widget: every widget has its own settings object (Settings.ForWidget), so a list is
	// never kept alive across widgets, windows or rebuilt sidebars.
	private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Settings, Dictionary<string, Memory>> memories = new();

	public static List<T> Keep<T>(string key, Settings set, Snapshot source, List<T> now, Func<T, string> label, Func<T, bool> empty)
	{
		// The same list is asked for several times per update (each design part asks again); one answer per
		// update, so a missing line is counted once.
		key += "|" + string.Join(",", set.HiddenValues) + "|" + L.Language;
		var memory = memories.GetOrCreateValue(set);
		lock (memory)
		{
			if (!memory.TryGetValue(key, out Memory? m))
			{
				memory[key] = m = new Memory();
			}
			if (ReferenceEquals(m.Source, source) && m.Result is List<T> same)
			{
				return new List<T>(same);
			}
			// A miss is counted once per reading time, however often the list is asked for.
			bool next = source.Time != m.Time;
			var result = new List<T>();
			var present = new HashSet<string>();
			foreach (T item in now)
			{
				string name = label(item);
				var previous = m.Last.FirstOrDefault(x => x.Label == name);
				if (empty(item) && previous.Item is T old && m.Misses.GetValueOrDefault(name) < Patience)
				{
					// Briefly empty: the last value stays.
					if (next) m.Misses[name] = m.Misses.GetValueOrDefault(name) + 1;
					result.Add(old);
				}
				else
				{
					if (!empty(item))
					{
						m.Misses.Remove(name);
					}
					result.Add(item);
				}
				present.Add(name);
			}
			// Lines that vanished completely keep their place for a while.
			for (int i = 0; i < m.Last.Count; i++)
			{
				var (name, item) = m.Last[i];
				if (present.Contains(name) || item is not T old)
				{
					continue;
				}
				int misses = m.Misses.GetValueOrDefault(name) + (next ? 1 : 0);
				if (misses > Patience)
				{
					m.Misses.Remove(name);
					continue;
				}
				m.Misses[name] = misses;
				result.Insert(Math.Min(i, result.Count), old);
				present.Add(name);
			}
			m.Last = result.Select(x => (label(x), (object)x!)).ToList();
			m.Source = source;
			m.Time = source.Time;
			m.Result = result;
			return new List<T>(result);
		}
	}
}
