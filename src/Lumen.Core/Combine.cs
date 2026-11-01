namespace Lumen.Core;

// Everything one update read from the hardware libraries, before Lumen decides what to show.
// Builtin is LibreHardwareMonitor (null while it is paused for AIDA64/HWiNFO), Ohm the driverless
// Open Hardware Monitor, External the program chosen under "Sensor source" if it answered.
public sealed record HardwareInput(
    IReadOnlyList<SensorNode>? Builtin,
    IReadOnlyList<SensorNode> Ohm,
    (string Name, IReadOnlyList<SensorNode> Nodes)? External,
    IReadOnlyList<SensorMerge.PhysicalDisk> Physical,
    IReadOnlyDictionary<int, string> Letters,
    IReadOnlyList<VolumeInfo> Volumes,
    double? SystemLoad = null,
    Func<double?>? FallbackCpuTemperature = null);

public sealed record HardwareView(
    CpuInfo? Cpu,
    List<GpuInfo> Gpus,
    List<DiskInfo> Disks,
    IReadOnlyList<SensorPicker.FanReading> Fans,
    IReadOnlyList<TempReading> BoardTemps,
    string? BoardName,
    IReadOnlyList<SensorNode> Nodes);

// Puts the sources together the same way every time, so a drive, a card or a fan keeps its id
// whichever source is chosen: drives are "/disk/<Windows disk number>", found by serial number
// where a source numbers them differently. While AIDA64 or HWiNFO is connected the built-in
// libraries rest; the hardware they last saw (names, ids, space in use) keeps the widgets in place.
public sealed class HardwareCombiner
{
    private List<SensorNode> idNodes = new();
    private CpuInfo? idCpu;
    private List<GpuInfo>? idGpus;
    private List<DiskInfo> idDisks = new();

    // True once the built-in libraries have been read at least once; only then can they rest.
    public bool HasIdentity => idGpus != null;
    public List<string> Log { get; } = new();

    public HardwareView Combine(HardwareInput input)
    {
        Log.Clear();
        bool paused = input.Builtin is null;
        IReadOnlyList<SensorNode> nodes;
        CpuInfo? cpu;
        List<GpuInfo> gpus;
        List<DiskInfo> disks;
        if (paused)
        {
            nodes = idNodes;
            cpu = idCpu is null ? null : idCpu with { Load = input.SystemLoad };
            gpus = (idGpus ?? new()).ToList();
            disks = idDisks.ToList();
        }
        else
        {
            nodes = input.Builtin!;
            cpu = SensorPicker.Cpu(nodes, input.SystemLoad);
            if (cpu is { Temperature: null } && input.FallbackCpuTemperature?.Invoke() is double zone) cpu = cpu with { Temperature = zone };
            gpus = nodes.Where(n => n.IsGpu).Select(SensorPicker.Gpu).ToList();
            disks = nodes.Where(n => n.Type == "Storage").Select(SensorPicker.Disk).ToList();
            if (input.Ohm.Count > 0)
            {
                gpus = SensorMerge.Gpus(gpus, input.Ohm.Where(n => n.IsGpu).Select(SensorPicker.Gpu).ToList(), Log);
                disks = SensorMerge.Disks(disks, input.Ohm.Where(n => n.Type == "Storage").Select(SensorPicker.Disk).ToList(), Log);
            }
            disks = Number(disks, input.Physical);
            idNodes = nodes.Select(n => n with { Sensors = Array.Empty<SensorValue>(), Sub = Array.Empty<SensorNode>() }).ToList();
            idCpu = cpu is null ? null : new CpuInfo(cpu.Name, null, null, Array.Empty<double>(), null, null, null, null);
            idGpus = gpus.Select(g => new GpuInfo(g.Id, g.Name, null, null, null, null, null, null, null, g.MemoryTotalMb) { Integrated = g.Integrated }).ToList();
            idDisks = disks.Where(SensorPicker.HasReadings).Select(d => new DiskInfo(d.Id, d.Name, null, d.UsedPercent, null, null)).ToList();
        }

        IReadOnlyList<SensorPicker.FanReading>? fans = null;
        IReadOnlyList<TempReading>? boardTemps = null;
        string? boardName = nodes.FirstOrDefault(n => n.IsBoard)?.Name;
        if (input.External is { } ext)
        {
            cpu = SensorMerge.PreferCpu(SensorPicker.Cpu(ext.Nodes), cpu);
            gpus = SensorMerge.PreferGpus(ext.Nodes.Where(n => n.IsGpu).Select(SensorPicker.Gpu).ToList(), gpus, ext.Name, Log);
            var extDisks = Number(ext.Nodes.Where(n => n.Type == "Storage").Select(SensorPicker.Disk).ToList(), input.Physical);
            disks = SensorMerge.PreferDisks(extDisks, disks, ext.Name, Log);
            if (ext.Nodes.FirstOrDefault(n => n.IsBoard) is { } extBoard)
            {
                var temps = SensorPicker.BoardTemps(extBoard);
                if (temps.Count > 0) boardTemps = temps;
                var extFans = SensorPicker.Fans(ext.Nodes);
                if (extFans.Count > 0) fans = extFans;
                if (boardName is null && extBoard.Name != "Mainboard") boardName = extBoard.Name;
            }
        }
        gpus = gpus.OrderBy(g => g.Integrated ? 1 : 0).ToList();

        disks = disks.Where(d => SensorPicker.HasReadings(d) || paused)
            .Select(d => SensorPicker.DiskNumber(d.Id) is int n && input.Letters.TryGetValue(n, out var l) ? d with { Letters = l } : d)
            .Select(d => d.UsedPercent != null ? d : d with { UsedPercent = UsedOnVolumes(d.Letters, input.Volumes) })
            .ToList();
        return new HardwareView(cpu, gpus, disks, fans ?? SensorPicker.Fans(nodes), boardTemps ?? SensorPicker.BoardTemps(nodes.FirstOrDefault(n => n.IsBoard)), boardName, nodes);
    }

    // Every drive gets the same id from every source: "/disk/<Windows disk number>". LibreHardwareMonitor and
    // Open Hardware Monitor already number drives like Windows; AIDA64 and HWiNFO are matched by serial number.
    private static List<DiskInfo> Number(List<DiskInfo> disks, IReadOnlyList<SensorMerge.PhysicalDisk> physical)
    {
        var taken = disks.Select(d => SensorPicker.DiskNumber(d.Id)).OfType<int>().ToHashSet();
        var result = new List<DiskInfo>();
        foreach (var d in disks)
        {
            int? n = SensorPicker.DiskNumber(d.Id);
            if (n is null && SensorMerge.MatchPhysical(d.Name, physical, taken) is int found) { n = found; taken.Add(found); }
            result.Add(d with { Id = n is int k ? "/disk/" + k : d.Id, Name = SensorMerge.WithoutSerial(d.Name) });
        }
        return result;
    }

    private static double? UsedOnVolumes(string letters, IReadOnlyList<VolumeInfo> volumes)
    {
        if (letters.Length == 0) return null;
        var parts = letters.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var vols = volumes.Where(v => parts.Contains(v.Name, StringComparer.OrdinalIgnoreCase)).ToList();
        double total = vols.Sum(v => v.TotalGb);
        return total > 0 ? (total - vols.Sum(v => v.FreeGb)) / total * 100 : null;
    }
}
