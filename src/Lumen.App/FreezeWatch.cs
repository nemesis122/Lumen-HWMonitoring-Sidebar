using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Avalonia.Threading;

namespace Lumen.App;

public sealed class FreezeWatch : IDisposable
{
	public const int FreezeSeconds = 20;

	private const int MaxRestarts = 3;

	private static readonly TimeSpan RestartWindow = TimeSpan.FromMinutes(30.0);

	private DispatcherTimer? beat;

	private readonly Thread watcher;

	private readonly Action restart;

	private readonly string historyPath;

	private long lastBeat = Environment.TickCount64;

	private volatile bool stopped;

	public FreezeWatch(Action restart, string historyPath)
	{
		this.restart = restart;
		this.historyPath = historyPath;
		watcher = new Thread(Watch)
		{
			IsBackground = true,
			Name = "Lumen freeze watch",
			Priority = ThreadPriority.AboveNormal
		};
	}

	public void Start()
	{
		beat = new DispatcherTimer(DispatcherPriority.Background)
		{
			Interval = TimeSpan.FromSeconds(1.0)
		};
		beat.Tick += delegate
		{
			Interlocked.Exchange(ref lastBeat, Environment.TickCount64);
		};
		beat.Start();
		watcher.Start();
	}

	private void Watch()
	{
		long num = Environment.TickCount64;
		while (!stopped)
		{
			Thread.Sleep(2000);
			long tickCount = Environment.TickCount64;
			if (tickCount - num > 10000)
			{
				Interlocked.Exchange(ref lastBeat, tickCount);
			}
			num = tickCount;
			long num2 = tickCount - Interlocked.Read(in lastBeat);
			if (num2 >= 20000)
			{
				bool flag = AllowRestart(DateTime.Now);
				CrashLog.Write(new TimeoutException($"The interface did not answer for {num2 / 1000} s. {(flag ? "Lumen restarts." : "Too many restarts in a short time – Lumen keeps waiting.")}\n{Snapshot()}"), "freeze");
				if (flag)
				{
					stopped = true;
					restart();
					break;
				}
				Interlocked.Exchange(ref lastBeat, tickCount);
			}
		}
	}

	private static string Snapshot()
	{
		try
		{
			using Process process = Process.GetCurrentProcess();
			return $"Threads {process.Threads.Count} · working set {process.WorkingSet64 / 1048576} MB · private {process.PrivateMemorySize64 / 1048576} MB · CPU time {process.TotalProcessorTime.TotalSeconds:0} s · up {(DateTime.Now - process.StartTime).TotalMinutes:0} min";
		}
		catch
		{
			return "";
		}
	}

	public bool AllowRestart(DateTime now)
	{
		List<DateTime> list = new List<DateTime>();
		try
		{
			if (File.Exists(historyPath))
			{
				string[] array = File.ReadAllLines(historyPath);
				for (int i = 0; i < array.Length; i++)
				{
					if (DateTime.TryParse(array[i], null, DateTimeStyles.RoundtripKind, out var result) && now - result < RestartWindow)
					{
						list.Add(result);
					}
				}
			}
		}
		catch
		{
		}
		if (list.Count >= 3)
		{
			return false;
		}
		list.Add(now);
		try
		{
			File.WriteAllLines(historyPath, list.Select((DateTime t) => t.ToString("o")));
		}
		catch
		{
		}
		return true;
	}

	public void Dispose()
	{
		stopped = true;
		beat?.Stop();
	}

	public static void RestartNow()
	{
		try
		{
			IEnumerable<string> enumerable = (from a in Environment.GetCommandLineArgs().Skip(1)
				where a != "--restarted"
				select a).Append("--restarted");
			ProcessStartInfo processStartInfo = new ProcessStartInfo(Platform.ExePath)
			{
				UseShellExecute = false
			};
			foreach (string item in enumerable)
			{
				processStartInfo.ArgumentList.Add(item);
			}
			Process.Start(processStartInfo);
		}
		catch (Exception e)
		{
			CrashLog.Write(e, "restart");
		}
		Process.GetCurrentProcess().Kill();
	}
}
