namespace Lumen.Core;

public sealed record MediaInfo(string Title, string Artist, string App, bool Playing, double? PositionSeconds, double? DurationSeconds, byte[]? Cover);
