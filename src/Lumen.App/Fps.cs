using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Lumen.Core;

namespace Lumen.App;

// Frames per second of whatever app is in the foreground, measured by PresentMon (Intel, MIT).
// PresentMon listens to the Windows graphics event trace, so it works for DirectX, Vulkan and
// OpenGL games without hooking into them. It needs administrator rights.
public sealed class FpsMonitor : IDisposable
{
    private readonly object gate = new();
    private readonly Dictionary<int, Queue<(double time, double ms)>> frames = new();
    private readonly Dictionary<int, string> names = new();
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private Process? process;
    private IntPtr job;
    public string? Error { get; private set; }

    public static bool Supported => OperatingSystem.IsWindows();

    public void Start()
    {
        if (process != null || !Supported) return;
        try
        {
            string exe = Unpack();
            var info = new ProcessStartInfo(exe, "--output_stdout --v1_metrics --stop_existing_session --session_name LumenFps --no_track_gpu --no_track_input --no_console_stats --exclude dwm.exe")
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
            };
            process = Process.Start(info);
            if (process == null) { Error = "PresentMon did not start."; return; }
            KillWithUs(process);
            process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) Error = e.Data; };
            process.BeginErrorReadLine();
            _ = Task.Run(() => ReadLoop(process));
        }
        catch (Exception e) { Error = e.Message; process = null; }
    }

    // Unpacks the bundled PresentMon once, next to the settings, keyed by its version.
    private static string Unpack()
    {
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lumen");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "PresentMon-2.3.1.exe");
        if (File.Exists(path) && new FileInfo(path).Length > 100_000) return path;
        using var source = typeof(FpsMonitor).Assembly.GetManifestResourceStream("PresentMon.exe")
            ?? throw new InvalidOperationException("PresentMon is not part of this build.");
        using var target = File.Create(path);
        source.CopyTo(target);
        return path;
    }

    private void ReadLoop(Process p)
    {
        int app = -1, pid = -1, ms = -1;
        string? line;
        try
        {
            while ((line = p.StandardOutput.ReadLine()) != null)
            {
                var cols = line.Split(',');
                if (pid < 0)
                {
                    // Header: find the columns by name, so newer PresentMon versions still work.
                    app = Array.FindIndex(cols, c => c.Equals("Application", StringComparison.OrdinalIgnoreCase));
                    pid = Array.FindIndex(cols, c => c.Equals("ProcessID", StringComparison.OrdinalIgnoreCase));
                    ms = Array.FindIndex(cols, c => c.Equals("msBetweenPresents", StringComparison.OrdinalIgnoreCase) || c.Equals("MsBetweenPresents", StringComparison.OrdinalIgnoreCase) || c.Equals("FrameTime", StringComparison.OrdinalIgnoreCase));
                    if (pid < 0 || ms < 0) pid = -1;
                    continue;
                }
                if (cols.Length <= Math.Max(pid, ms)) continue;
                if (!int.TryParse(cols[pid], out int id) || !double.TryParse(cols[ms], NumberStyles.Float, CultureInfo.InvariantCulture, out double frame) || frame <= 0) continue;
                double now = clock.Elapsed.TotalSeconds;
                lock (gate)
                {
                    if (!frames.TryGetValue(id, out var q)) frames[id] = q = new();
                    q.Enqueue((now, frame));
                    while (q.Count > 0 && now - q.Peek().time > 10) q.Dequeue();
                    if (app >= 0 && app < cols.Length) names[id] = Path.GetFileNameWithoutExtension(cols[app]);
                }
            }
        }
        catch { }
    }

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int processId);

    public FpsInfo? Read()
    {
        if (!Supported) return null;
        GetWindowThreadProcessId(GetForegroundWindow(), out int fg);
        double now = clock.Elapsed.TotalSeconds;
        lock (gate)
        {
            if (!frames.TryGetValue(fg, out var q)) return null;
            var all = q.Where(f => now - f.time <= 10).Select(f => f.ms).ToList();
            var second = q.Where(f => now - f.time <= 1).Select(f => f.ms).ToList();
            if (second.Count < 2) return null;
            double avgMs = second.Average();
            // 1 % low: the frame rate the slowest 1 % of the last ten seconds' frames ran at.
            double? low = null;
            if (all.Count >= 30)
            {
                var slow = all.OrderByDescending(x => x).Take(Math.Max(1, all.Count / 100)).Average();
                low = 1000 / slow;
            }
            return new FpsInfo(names.GetValueOrDefault(fg, "?"), 1000 / avgMs, avgMs, low);
        }
    }

    // A job object makes Windows end PresentMon together with Lumen, even after a crash.
    [DllImport("kernel32.dll")] private static extern IntPtr CreateJobObject(IntPtr a, string? name);
    [DllImport("kernel32.dll")] private static extern bool SetInformationJobObject(IntPtr job, int infoClass, ref JobLimits info, int size);
    [DllImport("kernel32.dll")] private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

    [StructLayout(LayoutKind.Sequential)]
    private struct JobLimits
    {
        public long PerProcessUserTimeLimit, PerJobUserTimeLimit; public uint LimitFlags; public UIntPtr MinimumWorkingSetSize, MaximumWorkingSetSize;
        public uint ActiveProcessLimit; public UIntPtr Affinity; public uint PriorityClass, SchedulingClass;
        public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount, ReadTransferCount, WriteTransferCount, OtherTransferCount;
        public UIntPtr ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed;
    }

    private void KillWithUs(Process p)
    {
        try
        {
            job = CreateJobObject(IntPtr.Zero, null);
            var limits = new JobLimits { LimitFlags = 0x2000 }; // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
            SetInformationJobObject(job, 9, ref limits, Marshal.SizeOf<JobLimits>());
            AssignProcessToJobObject(job, p.Handle);
        }
        catch { }
    }

    public void Dispose()
    {
        try { if (process is { HasExited: false }) process.Kill(); } catch { }
        process = null;
        lock (gate) { frames.Clear(); }
    }
}
