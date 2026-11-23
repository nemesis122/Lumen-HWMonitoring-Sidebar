using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Lumen.App;
using Lumen.Core;
using Xunit;

/// <summary>
/// Release check 5.1.0: every "History chart" switch that Settings lists for a design must change what the design
/// draws. A switch without any effect (a design without a temperature curve, a Panel design without drive history)
/// is not listed at all.
/// </summary>
public class HistorySwitchEffectTests
{
    private static int tick;
    /// <summary>The short curves designs collect themselves start empty, so two pictures differ only by the switch.</summary>
    internal static void ClearDayStats() =>
        ((System.Collections.IDictionary)typeof(DayStats).GetField("all", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.GetValue(null)!).Clear();
    private static bool IsHistory(string key) => key.EndsWith(".history") || key.EndsWith(".chart");
    public static IEnumerable<object[]> Designs => Settings.Selectable.Distinct().Select(s => new object[] { s });

    private static byte[] Picture(Control card, Window window)
    {
        Dispatcher.UIThread.RunJobs();
        using var frame = window.CaptureRenderedFrame()!;
        using var buffer = frame.Lock();
        var bytes = new byte[buffer.RowBytes * buffer.Size.Height];
        System.Runtime.InteropServices.Marshal.Copy(buffer.Address, bytes, 0, bytes.Length);
        return bytes;
    }

    /// <summary>The charts a card shows: type, place, opacity and the numbers they draw.</summary>
    internal static string Charts(Control card)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var c in Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(card).OfType<Control>())
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(c.GetType().Name, "History|Area|Graph|Sparkline|Trace|Traffic|Chart|Curve|Plot|Wave")) continue;
            if (!c.IsEffectivelyVisible || c.Opacity < 0.01 || c.Bounds.Width < 1 || c.Bounds.Height < 1) continue;
            sb.Append(c.GetType().Name).Append('@').Append(c.Bounds.Width.ToString("0")).Append('x').Append(c.Bounds.Height.ToString("0")).Append(':');
            foreach (var f in c.GetType().GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public))
                if (f.GetValue(c) is IEnumerable<double> numbers) sb.Append(numbers.Count(double.IsFinite)).Append(',');
            sb.Append(';');
        }
        return sb.ToString();
    }

    /// <summary>A real change: more than a few hundred pixels differ clearly (a curve, a chart), not the anti-aliasing of an icon.</summary>
    private static bool Changed((byte[] Pixels, string Charts) a, (byte[] Pixels, string Charts) b) => a.Charts != b.Charts || Differ(a.Pixels, b.Pixels);
    internal static bool Differ(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return true;
        int count = 0;
        for (int i = 0; i + 3 < a.Length; i += 4)
            if (Math.Abs(a[i] - b[i]) + Math.Abs(a[i + 1] - b[i + 1]) + Math.Abs(a[i + 2] - b[i + 2]) > 60 && ++count > 150) return true;
        return false;
    }

    /// <summary>For one design: each listed history switch and whether switching it off (all others on) changes the picture.</summary>
    internal static List<(WidgetKind Kind, string Key, bool Changes)> Effects(WidgetStyle style)
    {
        var result = new List<(WidgetKind, string, bool)>();
        var (snap, history) = Render.Demo();
        bool aio = AioDesigns.Contains(style);
        foreach (var kind in aio ? new[] { WidgetKind.Overview } : new[] { WidgetKind.Cpu, WidgetKind.Gpu, WidgetKind.Network, WidgetKind.Memory, WidgetKind.Drives, WidgetKind.Cooling })
        {
            var options = (aio ? AioReadings.Options(snap, style).Select(o => o.Key) : WidgetSensors.Options(kind, snap, style).Select(o => o.Key)).Where(IsHistory).Distinct().ToList();
            if (options.Count == 0) continue;
            Edge edge = Edge.Right;
            (byte[] Pixels, string Charts) Draw(params string[] off)
            {
                var s = Settings.Starter(); s.HiddenValues.Clear(); s.Edge = edge;
                if (aio) { DesignWidgetSelection.Apply(s, style); AioDesigns.ShowAllParts(s); }
                var slot = s.Widgets.First(w => w.Kind == kind); slot.Style = style;
                foreach (var w in s.Widgets) w.HiddenSensors.Clear();
                foreach (var key in options) WidgetSensors.Select(s, slot, key, true, snap);
                foreach (var key in off) WidgetSensors.Select(s, slot, key, false, snap);
                Motion.Enabled = false; ClearDayStats();
                var card = Cards.Create(kind, style, 0, Palette.For(true, false), Metrics.For(Density.Fit), s.ForSlot(slot));
                if (card is Lumen.App.INamed named) named.Own(slot, 0);
                var window = edge == Edge.Top ? new Window { Width = 1600, Height = 700, Content = card } : new Window { Width = 340, Height = 2400, Content = card };
                window.Show();
                try
                {
                    // Readings at later and later times, as in the app: designs that collect their own short curves get some.
                    for (int i = 0; i < 4; i++) { ((IWidget)card).Update(snap with { Time = snap.Time.AddSeconds(++tick) }, history); Dispatcher.UIThread.RunJobs(); }
                    if (Environment.GetEnvironmentVariable("LUMEN_HISTORY_PNG") is { Length: > 0 } dir && dir.Split('|')[0] == style.ToString())
                    { using var f = window.CaptureRenderedFrame()!; f.Save(Path.Combine(dir.Split('|')[1], $"{style}-{kind}-{(off.Length == 0 ? "all" : string.Join("+", off))}.png")); }
                    return (Picture(card, window), Charts(card));
                }
                finally { window.Close(); }
            }
            foreach (var key in options)
            {
                bool changes = false;
                // Down the side of the screen and as a bar along the top: a design may draw its charts in only one of them.
                // As a bar, some designs page through their parts over time: a few tries before a switch counts as idle.
                foreach (var e in new[] { Edge.Right, Edge.Top, Edge.Top, Edge.Top })
                {
                    if (changes) break;
                    edge = e;
                    // Off with everything else on, or (for one curve of several) off with its sister curves already off.
                    changes = Changed(Draw(key), Draw());
                    var sisters = options.Where(o => o != key && o.EndsWith(".history") && key.EndsWith(".history")).ToArray();
                    if (!changes && sisters.Length > 0) changes = Changed(Draw(sisters.Append(key).ToArray()), Draw(sisters));
                }
                result.Add((kind, key, changes));
            }
        }
        return result;
    }

    [Theory, MemberData(nameof(Designs))]
    public void Every_listed_history_switch_changes_the_design(WidgetStyle style) => UiHost.Run(() =>
    {
        var effects = Effects(style);
        if (Environment.GetEnvironmentVariable("LUMEN_HISTORY_TABLE") is { Length: > 0 } file)
            File.AppendAllLines(file, effects.Select(e => $"{style} {e.Kind} {e.Key} {(e.Changes ? 1 : 0)}"));
        var idle = effects.Where(e => !e.Changes).Select(e => $"{e.Kind}: {e.Key}").ToList();
        Assert.True(idle.Count == 0, $"{style}: history switches without any effect: {string.Join(", ", idle)}");
        return true;
    }, 600);
}
