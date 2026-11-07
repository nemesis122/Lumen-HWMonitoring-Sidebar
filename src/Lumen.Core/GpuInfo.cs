namespace Lumen.Core;

public sealed record GpuInfo(string Id, string Name, double? Temperature, double? HotSpot, double? Load, double? ClockMhz, double? PowerW, double? FanRpm, double? MemoryUsedMb, double? MemoryTotalMb)
{
	public bool Integrated { get; init; }

	// Temperature of the video memory (GDDR6X and others report it as "Memory Junction").
	public double? MemoryJunction { get; init; }
}
