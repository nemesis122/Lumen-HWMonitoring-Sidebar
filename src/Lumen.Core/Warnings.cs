namespace Lumen.Core;

public sealed record Warning(string Key, string Name, double Celsius);

// Decides when a temperature is worth a pop-up: once when it crosses the threshold, again only
// after it has cooled 5 °C below it, and never more than once in five minutes per sensor.
public sealed class WarningTracker
{
    public const double Hysteresis = 5;
    public static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(5);
    private readonly Dictionary<string, (bool armed, DateTime last)> state = new();

    public IReadOnlyList<Warning> Check(Snapshot s, double threshold)
    {
        // Hot spots run about 10 °C above the core; drives are already hot at 70 °C.
        var readings = new List<(string key, string name, double? c, double limit)> { ("cpu", s.Cpu?.Name ?? "CPU", s.Cpu?.Temperature, threshold) };
        readings.AddRange(s.Gpus.Select(g => ($"gpu.{g.Id}", g.Name, g.Temperature, threshold)));
        readings.AddRange(s.Gpus.Select(g => ($"gpu.{g.Id}.hot", g.Name + " Hot Spot", g.HotSpot, threshold + 10)));
        readings.AddRange(s.Disks.Select(d => ($"disk.{d.Id}", d.Name, d.Temperature, Math.Min(threshold, 70))));
        var result = new List<Warning>();
        foreach (var (key, name, c, limit) in readings)
        {
            if (c is not double v || !double.IsFinite(v)) continue;
            var (armed, last) = state.TryGetValue(key, out var x) ? x : (true, DateTime.MinValue);
            if (armed && v >= limit && s.Time - last >= Cooldown)
            {
                result.Add(new Warning(key, name, v));
                state[key] = (false, s.Time);
            }
            else if (!armed && v <= limit - Hysteresis) state[key] = (true, last);
        }
        return result;
    }
}
