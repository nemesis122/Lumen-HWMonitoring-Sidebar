namespace Lumen.Core;

public sealed record GpuInfo(string Id, string Name, double? Temperature, double? HotSpot, double? Load, double? ClockMhz, double? PowerW, double? FanRpm, double? MemoryUsedMb, double? MemoryTotalMb)
{
	public bool Integrated { get; init; }
}
