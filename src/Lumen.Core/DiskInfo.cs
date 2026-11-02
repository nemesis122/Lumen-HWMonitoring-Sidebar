namespace Lumen.Core;

public sealed record DiskInfo(string Id, string Name, double? Temperature, double? UsedPercent, double? ReadBytesPerSecond, double? WriteBytesPerSecond)
{
	public string Letters { get; init; } = "";
}
