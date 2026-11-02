namespace Lumen.Core;

public sealed record NetworkInfo(string Name, double? DownloadBytesPerSecond, double? UploadBytesPerSecond, double? LinkBitsPerSecond, string? Address);
