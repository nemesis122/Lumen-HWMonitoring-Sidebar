using System.Collections.Generic;

namespace Lumen.Core;

public sealed record HardwareView(CpuInfo? Cpu, List<GpuInfo> Gpus, List<DiskInfo> Disks, IReadOnlyList<SensorPicker.FanReading> Fans, IReadOnlyList<TempReading> BoardTemps, string? BoardName, IReadOnlyList<SensorNode> Nodes);
