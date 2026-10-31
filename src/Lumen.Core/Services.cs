using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Lumen.Core;

// Pings the internet target and the router every two seconds in the background.
// Keeps the last minute so jitter and loss mean something.
public sealed class PingMonitor : IDisposable
{
    private readonly CancellationTokenSource stop = new();
    private readonly object gate = new();
    private readonly Queue<double?> recent = new();
    private double? last, gatewayLast;
    private string target;

    public PingMonitor(string target)
    {
        this.target = target;
        _ = Task.Run(Loop);
    }

    public string Target { get => target; set { lock (gate) { target = value; recent.Clear(); last = null; } } }

    private async Task Loop()
    {
        using var ping = new Ping();
        using var gw = new Ping();
        while (!stop.IsCancellationRequested)
        {
            string t; lock (gate) t = target;
            double? ms = await Once(ping, t);
            var gateway = Gateway();
            double? gms = gateway is null ? null : await Once(gw, gateway.ToString());
            lock (gate)
            {
                last = ms; gatewayLast = gms;
                recent.Enqueue(ms);
                while (recent.Count > 30) recent.Dequeue();
            }
            try { await Task.Delay(2000, stop.Token); } catch { break; }
        }
    }

    private static async Task<double?> Once(Ping p, string host)
    {
        try
        {
            var r = await p.SendPingAsync(host, 1500);
            return r.Status == IPStatus.Success ? Math.Max(.1, r.RoundtripTime) : null;
        }
        catch { return null; }
    }

    private static IPAddress? Gateway()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up)
                .SelectMany(n => n.GetIPProperties().GatewayAddresses)
                .Select(g => g.Address)
                .FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !a.Equals(IPAddress.Any));
        }
        catch { return null; }
    }

    public PingInfo? Read()
    {
        lock (gate)
        {
            if (recent.Count == 0) return null;
            var ok = recent.Where(x => x.HasValue).Select(x => x!.Value).ToList();
            double? jitter = ok.Count > 1 ? ok.Zip(ok.Skip(1), (a, b) => Math.Abs(a - b)).Average() : null;
            return new PingInfo(target, last, gatewayLast, jitter, 100.0 * recent.Count(x => x is null) / recent.Count);
        }
    }

    public void Dispose() => stop.Cancel();
}

// Adds up energy and traffic per day and keeps them in a small file beside the settings,
// so today's and this month's totals survive a restart.
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
        try { days = path != null && File.Exists(path) ? JsonSerializer.Deserialize<List<Day>>(File.ReadAllText(path)) ?? new() : new(); }
        catch { days = new(); }
    }

    public static double EstimateWatts(Snapshot s, double baseWatts) =>
        (s.Cpu?.PowerW ?? 0) + s.Gpus.Where(g => !g.Integrated).Sum(g => g.PowerW ?? 0) + baseWatts;

    public UsageInfo Record(Snapshot s, double baseWatts)
    {
        double watts = EstimateWatts(s, baseWatts);
        if (lastTick is DateTime prev)
        {
            double seconds = (s.Time - prev).TotalSeconds;
            // Ignore gaps (sleep, clock changes): they would add energy that was never used.
            if (seconds > 0 && seconds < 30)
            {
                var day = Today(s.Time.Date);
                day.Kwh += watts * seconds / 3_600_000;
                day.Down += (s.Network?.DownloadBytesPerSecond ?? 0) * seconds;
                day.Up += (s.Network?.UploadBytesPerSecond ?? 0) * seconds;
            }
        }
        lastTick = s.Time;
        if ((s.Time - lastSave).TotalSeconds >= 60) Save(s.Time);
        return Summarize(s.Time, watts);
    }

    public UsageInfo Summarize(DateTime now, double watts)
    {
        var today = now.Date;
        int offset = ((int)today.DayOfWeek + 6) % 7;
        var week = today.AddDays(-offset);
        var month = new DateTime(today.Year, today.Month, 1);
        IEnumerable<Day> Since(DateTime from) => days.Where(d => d.Date >= from && d.Date <= today);
        var t = Since(today).ToList(); var w = Since(week).ToList(); var m = Since(month).ToList();
        return new UsageInfo(watts, t.Sum(d => d.Kwh), w.Sum(d => d.Kwh), m.Sum(d => d.Kwh),
            t.Sum(d => d.Down), t.Sum(d => d.Up), w.Sum(d => d.Down), w.Sum(d => d.Up), m.Sum(d => d.Down), m.Sum(d => d.Up));
    }

    private Day Today(DateTime date)
    {
        var d = days.FirstOrDefault(x => x.Date == date);
        if (d != null) return d;
        d = new Day { Date = date };
        days.Add(d);
        days.RemoveAll(x => x.Date < date.AddDays(-62));
        return d;
    }

    public void Save(DateTime now)
    {
        lastSave = now;
        if (path is null) return;
        try
        {
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(days));
            File.Move(path + ".tmp", path, true);
        }
        catch { }
    }
}

public static class SystemReader
{
    public static IReadOnlyList<VolumeInfo> Volumes()
    {
        var list = new List<VolumeInfo>();
        foreach (var d in DriveInfo.GetDrives())
        {
            try
            {
                if (!d.IsReady || d.DriveType is not (DriveType.Fixed or DriveType.Removable) || d.TotalSize <= 0) continue;
                const double GiB = 1024d * 1024 * 1024;
                list.Add(new VolumeInfo(d.Name.TrimEnd('\\', '/'), d.VolumeLabel, d.TotalSize / GiB, d.AvailableFreeSpace / GiB));
            }
            catch { }
        }
        return list.Take(8).ToList();
    }

    private static string? os;
    private static string? build;

    public static SystemInfo Read()
    {
        if (os is null) (os, build) = Describe();
        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        int processes = 0, threads = 0;
        foreach (var p in Process.GetProcesses())
        {
            using (p)
            {
                processes++;
                try { threads += p.Threads.Count; } catch { }
            }
        }
        return new SystemInfo(os, build!, uptime, DateTime.Now - uptime, processes, threads);
    }

    // "Windows 11 Pro" and "23H2 · 22631.4317"; the registry still says "Windows 10" on 11, so the build decides.
    private static (string, string) Describe()
    {
        if (!OperatingSystem.IsWindows()) return (RuntimeInformation.OSDescription, Environment.OSVersion.Version.ToString());
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            string name = key?.GetValue("ProductName") as string ?? "Windows";
            int buildNo = int.TryParse(key?.GetValue("CurrentBuildNumber") as string, out var b) ? b : Environment.OSVersion.Version.Build;
            if (buildNo >= 22000) name = name.Replace("Windows 10", "Windows 11");
            string display = key?.GetValue("DisplayVersion") as string ?? "";
            string ubr = key?.GetValue("UBR") is int u ? "." + u : "";
            return (name, (display.Length > 0 ? display + " · " : "") + buildNo + ubr);
        }
        catch { return ("Windows", Environment.OSVersion.Version.ToString()); }
    }
}
