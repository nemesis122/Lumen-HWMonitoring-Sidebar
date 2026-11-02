namespace Lumen.Core;

public sealed record HwinfoReading(string Sensor, int SensorIndex, int Type, string Label, string Unit, double Value);
