namespace Lumen.Core;

// A bounded series per key (e.g. "cpu.load"). Values are kept in arrival order; gaps
// (missing readings) are stored as NaN so charts can break the line instead of inventing data.
public sealed class History
{
    public const int Capacity = 120;
    private readonly Dictionary<string, double[]> series = new();
    private readonly Dictionary<string, int> counts = new();
    private readonly Dictionary<string, int> heads = new();

    public void Add(string key, double? value)
    {
        if (!series.TryGetValue(key, out var buffer))
        {
            buffer = new double[Capacity];
            series[key] = buffer; counts[key] = 0; heads[key] = 0;
        }
        int head = heads[key];
        buffer[head] = value is double v && double.IsFinite(v) ? v : double.NaN;
        heads[key] = (head + 1) % Capacity;
        counts[key] = Math.Min(Capacity, counts[key] + 1);
    }

    // Oldest first.
    public double[] Read(string key)
    {
        if (!series.TryGetValue(key, out var buffer)) return Array.Empty<double>();
        int count = counts[key], head = heads[key];
        var result = new double[count];
        for (int i = 0; i < count; i++) result[i] = buffer[(head - count + i + Capacity * 2) % Capacity];
        return result;
    }

    public void Record(Snapshot s)
    {
        Add("cpu.load", s.Cpu?.Load);
        Add("cpu.temp", s.Cpu?.Temperature);
        foreach (var g in s.Gpus) { Add($"gpu.{g.Id}.load", g.Load); Add($"gpu.{g.Id}.temp", g.Temperature); }
        if (s.Memory is { } m) Add("mem.load", m.Load);
        foreach (var d in s.Disks) { Add($"disk.{d.Id}.read", d.ReadBytesPerSecond); Add($"disk.{d.Id}.write", d.WriteBytesPerSecond); }
        Add("net.down", s.Network?.DownloadBytesPerSecond);
        Add("net.up", s.Network?.UploadBytesPerSecond);
        if (s.Ping != null) Add("ping", s.Ping.Ms);
        if (s.Usage != null) Add("power", s.Usage.WattsNow);
    }
}
