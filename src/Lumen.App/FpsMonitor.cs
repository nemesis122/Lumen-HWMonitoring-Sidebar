using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Lumen.Core;

namespace Lumen.App;

public sealed class FpsMonitor : IDisposable
{
	private struct JobLimits
	{
		public long PerProcessUserTimeLimit;

		public long PerJobUserTimeLimit;

		public uint LimitFlags;

		public nuint MinimumWorkingSetSize;

		public nuint MaximumWorkingSetSize;

		public uint ActiveProcessLimit;

		public nuint Affinity;

		public uint PriorityClass;

		public uint SchedulingClass;

		public ulong ReadOperationCount;

		public ulong WriteOperationCount;

		public ulong OtherOperationCount;

		public ulong ReadTransferCount;

		public ulong WriteTransferCount;

		public ulong OtherTransferCount;

		public nuint ProcessMemoryLimit;

		public nuint JobMemoryLimit;

		public nuint PeakProcessMemoryUsed;

		public nuint PeakJobMemoryUsed;
	}

	private readonly object gate = new object();

	private readonly Dictionary<int, Queue<(double time, double ms)>> frames = new Dictionary<int, Queue<(double, double)>>();

	private readonly Dictionary<int, string> names = new Dictionary<int, string>();

	private readonly Stopwatch clock = Stopwatch.StartNew();

	private Process? process;

	private nint job;

	public string? Error { get; private set; }

	public static bool Supported => OperatingSystem.IsWindows();

	public void Start()
	{
		if (process != null || !Supported)
		{
			return;
		}
		try
		{
			ProcessStartInfo startInfo = new ProcessStartInfo(Unpack(), "--output_stdout --v1_metrics --stop_existing_session --session_name LumenFps --no_track_gpu --no_track_input --no_console_stats --exclude dwm.exe")
			{
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true
			};
			process = Process.Start(startInfo);
			if (process == null)
			{
				Error = "PresentMon did not start.";
				return;
			}
			KillWithUs(process);
			process.ErrorDataReceived += delegate(object _, DataReceivedEventArgs e)
			{
				if (!string.IsNullOrWhiteSpace(e.Data))
				{
					Error = e.Data;
				}
			};
			process.BeginErrorReadLine();
			Task.Run(delegate
			{
				ReadLoop(process);
			});
		}
		catch (Exception ex)
		{
			Error = ex.Message;
			process = null;
		}
	}

	private static string Unpack()
	{
		string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lumen");
		Directory.CreateDirectory(text);
		string text2 = Path.Combine(text, "PresentMon-2.3.1.exe");
		if (File.Exists(text2) && new FileInfo(text2).Length > 100000)
		{
			return text2;
		}
		using Stream stream = typeof(FpsMonitor).Assembly.GetManifestResourceStream("PresentMon.exe") ?? throw new InvalidOperationException("PresentMon is not part of this build.");
		using FileStream destination = File.Create(text2);
		stream.CopyTo(destination);
		return text2;
	}

	private void ReadLoop(Process p)
	{
		int num = -1;
		int num2 = -1;
		int num3 = -1;
		try
		{
			string text;
			while ((text = p.StandardOutput.ReadLine()) != null)
			{
				string[] array = text.Split(',');
				if (num2 < 0)
				{
					num = Array.FindIndex(array, (string c) => c.Equals("Application", StringComparison.OrdinalIgnoreCase));
					num2 = Array.FindIndex(array, (string c) => c.Equals("ProcessID", StringComparison.OrdinalIgnoreCase));
					num3 = Array.FindIndex(array, (string c) => c.Equals("msBetweenPresents", StringComparison.OrdinalIgnoreCase) || c.Equals("MsBetweenPresents", StringComparison.OrdinalIgnoreCase) || c.Equals("FrameTime", StringComparison.OrdinalIgnoreCase));
					if (num2 < 0 || num3 < 0)
					{
						num2 = -1;
					}
				}
				else
				{
					if (array.Length <= Math.Max(num2, num3) || !int.TryParse(array[num2], out var result) || !double.TryParse(array[num3], NumberStyles.Float, CultureInfo.InvariantCulture, out var result2) || result2 <= 0.0)
					{
						continue;
					}
					double totalSeconds = clock.Elapsed.TotalSeconds;
					lock (gate)
					{
						if (!frames.TryGetValue(result, out Queue<(double time, double ms)> value))
						{
							value = (frames[result] = new Queue<(double, double)>());
						}
						value.Enqueue((totalSeconds, result2));
						while (value.Count > 0 && totalSeconds - value.Peek().Item1 > 10.0)
						{
							value.Dequeue();
						}
						if (num >= 0 && num < array.Length)
						{
							names[result] = Path.GetFileNameWithoutExtension(array[num]);
						}
					}
				}
			}
		}
		catch
		{
		}
	}

	[DllImport("user32.dll")]
	private static extern nint GetForegroundWindow();

	[DllImport("user32.dll")]
	private static extern uint GetWindowThreadProcessId(nint hWnd, out int processId);

	public FpsInfo? Read()
	{
		if (!Supported)
		{
			return null;
		}
		GetWindowThreadProcessId(GetForegroundWindow(), out var processId);
		double now = clock.Elapsed.TotalSeconds;
		lock (gate)
		{
			if (!frames.TryGetValue(processId, out Queue<(double time, double ms)> value))
			{
				return null;
			}
			List<double> list = (from f in value
				where now - f.time <= 10.0
				select f.ms).ToList();
			List<double> list2 = (from f in value
				where now - f.time <= 1.0
				select f.ms).ToList();
			if (list2.Count < 2)
			{
				return null;
			}
			double num = list2.Average();
			double? low = null;
			if (list.Count >= 30)
			{
				double num2 = list.OrderByDescending((double x) => x).Take(Math.Max(1, list.Count / 100)).Average();
				low = 1000.0 / num2;
			}
			return new FpsInfo(names.GetValueOrDefault(processId, "?"), 1000.0 / num, num, low);
		}
	}

	[DllImport("kernel32.dll")]
	private static extern nint CreateJobObject(nint a, string? name);

	[DllImport("kernel32.dll")]
	private static extern bool SetInformationJobObject(nint job, int infoClass, ref JobLimits info, int size);

	[DllImport("kernel32.dll")]
	private static extern bool AssignProcessToJobObject(nint job, nint process);

	private void KillWithUs(Process p)
	{
		try
		{
			job = CreateJobObject(IntPtr.Zero, null);
			JobLimits info = new JobLimits
			{
				LimitFlags = 8192u
			};
			SetInformationJobObject(job, 9, ref info, Marshal.SizeOf<JobLimits>());
			AssignProcessToJobObject(job, p.Handle);
		}
		catch
		{
		}
	}

	public void Dispose()
	{
		try
		{
			Process process = this.process;
			if (process != null && !process.HasExited)
			{
				this.process.Kill();
			}
		}
		catch
		{
		}
		this.process = null;
		lock (gate)
		{
			frames.Clear();
		}
	}
}

