using System;
using System.Collections.Generic;

namespace Lumen.Core;

public sealed record HardwareInput(IReadOnlyList<SensorNode>? Builtin, IReadOnlyList<SensorNode> Ohm, (string Name, IReadOnlyList<SensorNode> Nodes)? External, IReadOnlyList<SensorMerge.PhysicalDisk> Physical, IReadOnlyDictionary<int, string> Letters, IReadOnlyList<VolumeInfo> Volumes, double? SystemLoad = null, Func<double?>? FallbackCpuTemperature = null);
