using System;
using System.Collections.Generic;

namespace Lumen.Core;

public sealed record Snapshot(DateTime Time, CpuInfo? Cpu, IReadOnlyList<GpuInfo> Gpus, MemoryInfo? Memory, IReadOnlyList<DiskInfo> Disks, NetworkInfo? Network, SensorAccess Access)
{
    // Original merged hardware nodes retain per-core temperatures, GPU voltage, rails and extra fans.
    // No estimates are substituted when the driver/provider does not expose a reading.
    public IReadOnlyList<SensorNode> HardwareSensors { get; init; } = Array.Empty<SensorNode>();
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
