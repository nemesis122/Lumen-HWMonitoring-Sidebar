using System.Diagnostics;

namespace Lumen.Core;

// Top programs by processor use, measured as CPU-time deltas between samples and grouped by
// name, so twenty browser processes count as one "chrome".
public sealed class ProcessSampler
{
    private Dictionary<int, TimeSpan> last = new();
    private readonly Stopwatch clock = new();
    private IReadOnlyList<ProcessInfo> current = Array.Empty<ProcessInfo>();

    public IReadOnlyList<ProcessInfo> Sample(int top = 5)
    {
        // Enumerating every process costs a few milliseconds; twice a second is plenty.
        if (clock.IsRunning && clock.Elapsed.TotalSeconds < 1.8) return current;
        double elapsed = clock.IsRunning ? clock.Elapsed.TotalSeconds : 0;
        clock.Restart();
        var now = new Dictionary<int, TimeSpan>();
        var byName = new Dictionary<string, (double cpu, double mem)>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in Process.GetProcesses())
        {
            using (p)
            {
                try
                {
                    if (p.Id == 0) continue;
                    var cpu = p.TotalProcessorTime;
                    now[p.Id] = cpu;
                    double share = elapsed > 0 && last.TryGetValue(p.Id, out var before)
                        ? Math.Max(0, (cpu - before).TotalSeconds) / elapsed / Environment.ProcessorCount * 100 : 0;
                    byName.TryGetValue(p.ProcessName, out var sum);
                    byName[p.ProcessName] = (sum.cpu + share, sum.mem + p.WorkingSet64 / 1048576.0);
                }
                catch { /* protected system processes refuse access */ }
            }
        }
        last = now;
        if (elapsed > 0)
            current = byName.Where(kv => kv.Key is not ("Idle" or "System Idle Process"))
                .OrderByDescending(kv => kv.Value.cpu).ThenByDescending(kv => kv.Value.mem).Take(top)
                .Select(kv => new ProcessInfo(kv.Key, Math.Min(100, kv.Value.cpu), kv.Value.mem)).ToList();
        return current;
    }
}
