using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Lumen.Core;

public sealed class ProcessSampler
{
	private Dictionary<int, TimeSpan> last = new Dictionary<int, TimeSpan>();

	private readonly Stopwatch clock = new Stopwatch();

	private IReadOnlyList<ProcessInfo> current = Array.Empty<ProcessInfo>();

	public IReadOnlyList<ProcessInfo> Sample(int top = 5)
	{
		if (clock.IsRunning && clock.Elapsed.TotalSeconds < 1.8)
		{
			return current;
		}
		double num = (clock.IsRunning ? clock.Elapsed.TotalSeconds : 0.0);
		clock.Restart();
		Dictionary<int, TimeSpan> dictionary = new Dictionary<int, TimeSpan>();
		Dictionary<string, (double, double)> dictionary2 = new Dictionary<string, (double, double)>(StringComparer.OrdinalIgnoreCase);
		Process[] processes = Process.GetProcesses();
		foreach (Process process in processes)
		{
			using (process)
			{
				try
				{
					if (process.Id != 0)
					{
						TimeSpan totalProcessorTime = process.TotalProcessorTime;
						dictionary[process.Id] = totalProcessorTime;
						TimeSpan value;
						double num2 = ((num > 0.0 && last.TryGetValue(process.Id, out value)) ? (Math.Max(0.0, (totalProcessorTime - value).TotalSeconds) / num / (double)Environment.ProcessorCount * 100.0) : 0.0);
						dictionary2.TryGetValue(process.ProcessName, out var value2);
						dictionary2[process.ProcessName] = (value2.Item1 + num2, value2.Item2 + (double)process.WorkingSet64 / 1048576.0);
					}
				}
				catch
				{
				}
			}
		}
		last = dictionary;
		if (num > 0.0)
		{
			current = (from kv in (from kv in dictionary2.Where<KeyValuePair<string, (double, double)>>(delegate(KeyValuePair<string, (double cpu, double mem)> kv)
					{
						string key = kv.Key;
						bool flag = ((key == "Idle" || key == "System Idle Process") ? true : false);
						return !flag;
					})
					orderby kv.Value.Item1 descending, kv.Value.Item2 descending
					select kv).Take(top)
				select new ProcessInfo(kv.Key, Math.Min(100.0, kv.Value.Item1), kv.Value.Item2)).ToList();
		}
		return current;
	}
}

