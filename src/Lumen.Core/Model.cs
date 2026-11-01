namespace Lumen.Core;

// One sample of the whole machine. Every reading is optional: a missing sensor stays null
// and the interface shows "—" instead of a made-up zero.
public sealed record Snapshot(
    DateTime Time,
    CpuInfo? Cpu,
    IReadOnlyList<GpuInfo> Gpus,
    MemoryInfo? Memory,
    IReadOnlyList<DiskInfo> Disks,
    NetworkInfo? Network,
    SensorAccess Access)
{
    public IReadOnlyList<FanInfo> Fans { get; init; } = Array.Empty<FanInfo>();
    public IReadOnlyList<TempReading> BoardTemps { get; init; } = Array.Empty<TempReading>();
    public string? BoardName { get; init; }
    public BatteryInfo? Battery { get; init; }
    public IReadOnlyList<ProcessInfo> Processes { get; init; } = Array.Empty<ProcessInfo>();
    public WeatherInfo? Weather { get; init; }
    public PingInfo? Ping { get; init; }
    public MediaInfo? Media { get; init; }
    public UsageInfo? Usage { get; init; }
    public SystemInfo? System { get; init; }
    public IReadOnlyList<VolumeInfo> Volumes { get; init; } = Array.Empty<VolumeInfo>();
    public FpsInfo? Fps { get; init; }
}

// Frames per second of the app in the foreground, from PresentMon. Low1 is the 1 % low over ten seconds.
public sealed record FpsInfo(string App, double Fps, double FrameTimeMs, double? Low1);

// Round trip in ms to the internet target and to the router; loss over the last minute.
public sealed record PingInfo(string Target, double? Ms, double? GatewayMs, double? JitterMs, double LossPercent);

public sealed record MediaInfo(string Title, string Artist, string App, bool Playing, double? PositionSeconds, double? DurationSeconds, byte[]? Cover);

// Totals of energy and traffic for today, this week and this month; WattsNow is the estimate behind them.
public sealed record UsageInfo(double WattsNow, double KwhToday, double KwhWeek, double KwhMonth,
    double DownToday, double UpToday, double DownWeek, double UpWeek, double DownMonth, double UpMonth);

public sealed record SystemInfo(string Os, string Build, TimeSpan Uptime, DateTime BootTime, int Processes, int Threads);

public sealed record VolumeInfo(string Name, string Label, double TotalGb, double FreeGb)
{
    public double UsedPercent => TotalGb > 0 ? (TotalGb - FreeGb) / TotalGb * 100 : 0;
}

public sealed record FanInfo(string Name, double Rpm, double? Percent);

public sealed record TempReading(string Name, double Celsius);

// PowerW is positive while charging and negative while running on battery.
public sealed record BatteryInfo(double Percent, bool OnAc, bool Charging, double? PowerW, double? MinutesLeft, double? WearPercent);

public sealed record ProcessInfo(string Name, double CpuPercent, double MemoryMb);

public sealed record WeatherDay(DateTime Date, double MinC, double MaxC, int Code);

public sealed record WeatherInfo(string Place, double TemperatureC, double? FeelsLikeC, int Code, bool IsDay, double? WindKmh, double? Humidity, IReadOnlyList<WeatherDay> Days, DateTime Updated);

public sealed record CpuInfo(
    string Name,
    double? Temperature,
    double? Load,
    IReadOnlyList<double> CoreLoads,
    double? ClockMhz,
    double? PowerW,
    double? Voltage,
    double? FanRpm);

public sealed record GpuInfo(
    string Id,
    string Name,
    double? Temperature,
    double? HotSpot,
    double? Load,
    double? ClockMhz,
    double? PowerW,
    double? FanRpm,
    double? MemoryUsedMb,
    double? MemoryTotalMb)
{
    // Integrated graphics (sharing system memory) are shown after dedicated cards.
    public bool Integrated { get; init; }
}

public sealed record MemoryInfo(double UsedGb, double TotalGb)
{
    public double AvailableGb => Math.Max(0, TotalGb - UsedGb);
    public double Load => TotalGb > 0 ? UsedGb / TotalGb * 100 : 0;
}

public sealed record DiskInfo(
    string Id,
    string Name,
    double? Temperature,
    double? UsedPercent,
    double? ReadBytesPerSecond,
    double? WriteBytesPerSecond)
{
    // Drive letters of the partitions on this disk, e.g. "C:" or "D: E:".
    public string Letters { get; init; } = "";
}

public sealed record NetworkInfo(
    string Name,
    double? DownloadBytesPerSecond,
    double? UploadBytesPerSecond,
    double? LinkBitsPerSecond,
    string? Address);

// What the sensor library could reach: CPU temperatures need administrator rights
// and the PawnIO driver; the app says so instead of showing empty cards.
public enum SensorAccess { Full, Limited, Demo }

public interface ISnapshotSource : IDisposable
{
    Snapshot Read();
}
