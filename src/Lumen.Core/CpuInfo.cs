using System.Collections.Generic;

namespace Lumen.Core;

public sealed record CpuInfo(string Name, double? Temperature, double? Load, IReadOnlyList<double> CoreLoads, double? ClockMhz, double? PowerW, double? Voltage, double? FanRpm);
