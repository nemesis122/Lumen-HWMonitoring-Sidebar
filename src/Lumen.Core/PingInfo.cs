namespace Lumen.Core;

public sealed record PingInfo(string Target, double? Ms, double? GatewayMs, double? JitterMs, double LossPercent);
