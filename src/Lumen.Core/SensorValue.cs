namespace Lumen.Core;

public sealed record SensorValue(string Type, string Name, double? Value, int Index = 0, string Id = "");
