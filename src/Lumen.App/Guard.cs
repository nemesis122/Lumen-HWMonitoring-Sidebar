using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia.Threading;
using Lumen.Core;

namespace Lumen.App;

// Watches the interface thread from a background thread. When the sidebar stops answering for
// FreezeSeconds, it writes the event to lumen-crash.log and starts a fresh Lumen, so a hang
// never leaves a frozen strip on the desktop. At most three restarts in half an hour, so a
// fault that comes back at once cannot turn into an endless loop.
public sealed class FreezeWatch : IDisposable
{
    public const int FreezeSeconds = 20;
    private const int MaxRestarts = 3;
    private static readonly TimeSpan RestartWindow = TimeSpan.FromMinutes(30);

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
        watcher = new Thread(Watch) { IsBackground = true, Name = "Lumen freeze watch", Priority = ThreadPriority.AboveNormal };
    }

    public void Start()
    {
        // Background priority: the beat only arrives when the interface has worked through everything else.
        beat = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
        beat.Tick += (_, _) => Interlocked.Exchange(ref lastBeat, Environment.TickCount64);
        beat.Start();
        watcher.Start();
    }

    private void Watch()
    {
        long lastLoop = Environment.TickCount64;
        while (!stopped)
        {
            Thread.Sleep(2000);
            long now = Environment.TickCount64;
            // The PC was asleep (or this thread was held up too): that is no freeze, start counting afresh.
            if (now - lastLoop > 10_000) Interlocked.Exchange(ref lastBeat, now);
            lastLoop = now;
            long silent = now - Interlocked.Read(ref lastBeat);
            if (silent < FreezeSeconds * 1000L) continue;
            bool allowed = AllowRestart(DateTime.Now);
            CrashLog.Write(new TimeoutException($"The interface did not answer for {silent / 1000} s. {(allowed ? "Lumen restarts." : "Too many restarts in a short time – Lumen keeps waiting.")}\n{Snapshot()}"), "freeze");
            if (allowed) { stopped = true; restart(); return; }
            Interlocked.Exchange(ref lastBeat, now);
        }
    }

    // A short picture of the process at the moment of the freeze, for the log.
    private static string Snapshot()
    {
        try
        {
            using var me = Process.GetCurrentProcess();
            return $"Threads {me.Threads.Count} · working set {me.WorkingSet64 / 1048576} MB · private {me.PrivateMemorySize64 / 1048576} MB · CPU time {me.TotalProcessorTime.TotalSeconds:0} s · up {(DateTime.Now - me.StartTime).TotalMinutes:0} min";
        }
        catch { return ""; }
    }

    // Remembers restart times in a small file next to the settings.
    public bool AllowRestart(DateTime now)
    {
        var times = new List<DateTime>();
        try
        {
            if (File.Exists(historyPath))
                foreach (var line in File.ReadAllLines(historyPath))
                    if (DateTime.TryParse(line, null, System.Globalization.DateTimeStyles.RoundtripKind, out var t) && now - t < RestartWindow) times.Add(t);
        }
        catch { }
        if (times.Count >= MaxRestarts) return false;
        times.Add(now);
        try { File.WriteAllLines(historyPath, times.Select(t => t.ToString("o"))); } catch { }
        return true;
    }

    public void Dispose()
    {
        stopped = true;
        beat?.Stop();
    }

    // Starts a second Lumen that waits for this one to end, then ends this one at once.
    public static void RestartNow()
    {
        try
        {
            var args = Environment.GetCommandLineArgs().Skip(1).Where(a => a != "--restarted").Append("--restarted");
            var info = new ProcessStartInfo(Platform.ExePath) { UseShellExecute = false };
            foreach (var a in args) info.ArgumentList.Add(a);
            Process.Start(info);
        }
        catch (Exception e) { CrashLog.Write(e, "restart"); }
        // Not Environment.Exit: with the interface thread stuck, a regular shutdown can hang as well.
        Process.GetCurrentProcess().Kill();
    }
}

// One ZIP with everything needed to trace a problem: the crash log, the sensor lists, the
// settings without personal entries and a few facts about the PC. Nothing is sent anywhere.
public static class ErrorReport
{
    public static string Create(Settings settings, string sensorReport, IReadOnlyList<string> screens, string? folder = null)
    {
        folder ??= DefaultFolder();
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, $"Lumen-Report-{DateTime.Now:yyyy-MM-dd-HHmm}.zip");
        if (File.Exists(path)) File.Delete(path);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        void Add(string name, string text)
        {
            var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
            using var w = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            w.Write(text);
        }
        Add("system.txt", SystemText(settings, screens));
        Add("sensors.txt", sensorReport);
        Add("settings.json", System.Text.Json.JsonSerializer.Serialize(Anonymous(settings), Settings.Json));
        var logs = CrashLogs().ToList();
        Add("lumen-crash.log", logs.Count == 0 ? "No errors were recorded.\n" : string.Join("\n", logs.Select(l => $"--- {l.Path}\n{l.Text}")));
        return path;
    }

    // The desktop, so the file is found at once; the settings folder where there is no desktop.
    public static string DefaultFolder()
    {
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        return desktop.Length > 0 && Directory.Exists(desktop) ? desktop : Path.GetTempPath();
    }

    // Notes, the weather place, its coordinates, own names and the ping target can be personal; they stay out.
    public static Settings Anonymous(Settings s)
    {
        var copy = System.Text.Json.JsonSerializer.Deserialize<Settings>(System.Text.Json.JsonSerializer.Serialize(s, Settings.Json), Settings.Json)!;
        copy.Notes = copy.Notes.Length > 0 ? "(removed)" : "";
        copy.WeatherPlace = copy.WeatherPlace.Length > 0 ? "(removed)" : "";
        copy.WeatherLatitude = copy.WeatherLatitude is null ? null : 0;
        copy.WeatherLongitude = copy.WeatherLongitude is null ? null : 0;
        copy.PingTarget = "(removed)";
        foreach (var w in copy.Widgets) { if (w.Name.Length > 0) w.Name = "(own name)"; if (w.HardwareName.Length > 0) w.HardwareName = "(own name)"; }
        return copy;
    }

    private static IEnumerable<(string Path, string Text)> CrashLogs()
    {
        string exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
        foreach (var dir in new[] { exeDir, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lumen") }.Distinct())
        {
            string p = Path.Combine(dir, "lumen-crash.log");
            string? text = null;
            try
            {
                if (File.Exists(p))
                {
                    text = File.ReadAllText(p);
                    // The last 200 kB are enough; old entries only repeat.
                    if (text.Length > 200_000) text = "…\n" + text[^200_000..];
                }
            }
            catch { }
            if (text != null) yield return (p, text);
        }
    }

    private static string SystemText(Settings s, IReadOnlyList<string> screens)
    {
        var b = new StringBuilder();
        b.AppendLine($"Lumen {typeof(ErrorReport).Assembly.GetName().Version?.ToString(3)} · report of {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        b.AppendLine($"Windows: {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
        b.AppendLine($".NET: {RuntimeInformation.FrameworkDescription}");
        b.AppendLine($"Processors: {Environment.ProcessorCount} logical");
        b.AppendLine($"Administrator: {(OperatingSystem.IsWindows() && new Platform().IsAdministrator ? "yes" : "no")}");
        b.AppendLine($"Sensor source: {s.SensorSource} · {ExternalSensors.Status}");
        b.AppendLine($"Layout: edge {s.Edge}, {(s.Horizontal ? $"{s.Rows} rows of {s.BarHeight:0} px" : $"{s.Columns} column(s), width {s.Width:0}")}, {s.Pages} register(s), density {s.Density}, text {s.TextScale * 100:0} %");
        b.AppendLine($"Look: theme {s.Theme}, colours {s.Colors}, background {s.Background}, glass {s.Glass}, font {s.Font}");
        b.AppendLine("Screens: " + (screens.Count == 0 ? "—" : string.Join(" | ", screens)));
        b.AppendLine("Widgets: " + string.Join(", ", s.Widgets.Where(w => w.Visible).Select(w => $"{w.Kind}/{w.Style}")));
        try
        {
            using var me = Process.GetCurrentProcess();
            b.AppendLine($"Lumen process: up {(DateTime.Now - me.StartTime).TotalMinutes:0} min, CPU time {me.TotalProcessorTime.TotalSeconds:0} s, working set {me.WorkingSet64 / 1048576} MB, private {me.PrivateMemorySize64 / 1048576} MB, {me.Threads.Count} threads, {me.HandleCount} handles");
        }
        catch { }
        b.AppendLine($"Managed heap: {GC.GetTotalMemory(false) / 1048576.0:0.0} MB, collections {GC.CollectionCount(0)}/{GC.CollectionCount(1)}/{GC.CollectionCount(2)}");
        return b.ToString();
    }

    // Opens Explorer with the new file selected.
    public static void Reveal(string path)
    {
        if (!OperatingSystem.IsWindows()) return;
        try { Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true }); } catch { }
    }
}
