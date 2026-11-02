namespace Lumen.Core;

public sealed record BatteryInfo(double Percent, bool OnAc, bool Charging, double? PowerW, double? MinutesLeft, double? WearPercent);
