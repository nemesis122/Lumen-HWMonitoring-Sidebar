namespace Lumen.Core;

public sealed record DiskInfo(string Id, string Name, double? Temperature, double? UsedPercent, double? ReadBytesPerSecond, double? WriteBytesPerSecond)
{
	public string Letters { get; init; } = "";

	// Further readings LibreHardwareMonitor reports for many drives; null when a drive does not have them.
	public double? ReadActivity { get; init; }

	public double? WriteActivity { get; init; }

	public double? TotalActivity { get; init; }

	public double? PowerOnHours { get; init; }

	public double? PowerOnCount { get; init; }

	// Remaining life in percent (100 = new).
	public double? Life { get; init; }
}
