using System.IO.MemoryMappedFiles;
using Lumen.Core;

namespace Lumen.App;

// Reads AIDA64's or HWiNFO's shared memory, whichever the settings ask for. Nothing is installed or
// started: if the program is not running (or its shared memory is switched off) the read returns
// nothing and Lumen keeps showing its built-in readings. Opening a mapping that does not exist is
// cheap, but after a miss the next attempt waits a few seconds.
public sealed class ExternalSensors
{
    public const string AidaMap = "AIDA64_SensorValues";
    public const string HwinfoMap = @"Global\HWiNFO_SENS_SM2";

    private readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
    private readonly Dictionary<SensorSource, double> missedAt = new();

    // What the last read used, for the settings page and the sensor report.
    public static volatile string Status = "";
    public SensorSource? Active { get; private set; }
    public string? LastText { get; private set; }

    public (string Name, List<SensorNode> Nodes)? Read(SensorSource wanted)
    {
        Active = null;
        if (wanted == SensorSource.Builtin || !OperatingSystem.IsWindows()) { Status = ""; return null; }
        var order = wanted == SensorSource.Auto ? new[] { SensorSource.Aida64, SensorSource.HwInfo } : new[] { wanted };
        foreach (var s in order)
        {
            if (missedAt.TryGetValue(s, out var at) && clock.Elapsed.TotalSeconds - at < 5) continue;
            try
            {
                var nodes = s == SensorSource.Aida64 ? ReadAida() : ReadHwinfo();
                if (nodes.Count > 0)
                {
                    Active = s;
                    Status = "connected:" + s;
                    return (s == SensorSource.Aida64 ? "AIDA64" : "HWiNFO", nodes);
                }
            }
            catch { }
            missedAt[s] = clock.Elapsed.TotalSeconds;
        }
        Status = "missing:" + wanted;
        return null;
    }

    private List<SensorNode> ReadAida()
    {
        using var map = MemoryMappedFile.OpenExisting(AidaMap, MemoryMappedFileRights.Read);
        using var view = map.CreateViewStream(0, 0, MemoryMappedFileAccess.Read);
        // The block is plain text ending at the first zero byte.
        var bytes = new List<byte>(64 * 1024);
        int b;
        while ((b = view.ReadByte()) > 0) bytes.Add((byte)b);
        string text = System.Text.Encoding.Latin1.GetString(bytes.ToArray());
        LastText = text;
        return External.AidaNodes(External.ParseAida(text));
    }

    private List<SensorNode> ReadHwinfo()
    {
        using var map = MemoryMappedFile.OpenExisting(HwinfoMap, MemoryMappedFileRights.Read);
        using var view = map.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
        var header = new byte[44];
        view.ReadArray(0, header, 0, header.Length);
        if (External.HwinfoSize(header) is not int size || size > view.Capacity) return new();
        var data = new byte[size];
        view.ReadArray(0, data, 0, size);
        var readings = External.ParseHwinfo(data);
        LastText = string.Join("\n", readings.Select(r => $"{r.Sensor} | {r.Label} = {r.Value:0.###} {r.Unit}"));
        return External.HwinfoNodes(readings);
    }
}
