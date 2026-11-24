using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Lumen.App;
using Lumen.Core;
using Xunit;

/// <summary>
/// The complete sensor selection of every AIO design (user request 5.0.2): each switch in Settings → Sensors, switched off
/// on its own, removes only what belongs to it (its value, its part, or only the history chart of its part) and nothing
/// else; switched on again, everything comes back. A switch that is off by default, switched on, only adds its own items.
/// Found by this test first: "History chart" under Processor also removed the network history.
/// </summary>
public class AioSensorSelectionTests
{
    public static IEnumerable<object[]> Designs => AioDesigns.Styles.Distinct().SelectMany(s => HardwareVariants.Names.Select(h => new object[] { s, h }));

    /// <summary>What a card shows: sensor key and kind of control (t = text, h = history chart, g = other graphic).</summary>
    internal static HashSet<(string Key, char Kind)> Shown(Control root)
    {
        var result = new HashSet<(string, char)>();
        foreach (var c in root.GetLogicalDescendants().OfType<Control>())
        {
            if (!SensorDetails.Visible(c, root)) continue;
            string bound = c.GetValue(TextColours.SensorProperty);
            string keys = SensorDetails.KeyOf(c);
            if (c is not TextBlock && bound.Length > 0) keys = bound + "|" + keys;
            if (keys.Length == 0) continue;
            char kind = c is TextBlock t ? (string.IsNullOrWhiteSpace(t.Text) || t.Text == "⁠" ? ' ' : 't')
                : IsHistory(c) ? 'h' : HasText(c) ? ' ' : 'g';
            if (kind == ' ') continue;
            foreach (var key in keys.Split('|', StringSplitOptions.RemoveEmptyEntries)) result.Add((key, kind));
        }
        return result;
    }

    private static bool HasText(Control c) => c.GetLogicalDescendants().OfType<TextBlock>().Any();
    private static bool IsHistory(Control c) =>
        System.Text.RegularExpressions.Regex.IsMatch(c.GetType().Name, "History|Area|PanelGraph|Sparkline|Trace|Traffic|Chart")
        || c is AioMeter { Historical: true };

    /// <summary>Whether an item shown for <paramref name="key"/> belongs to the switch <paramref name="option"/>.</summary>
    internal static bool Belongs(string option, (string Key, char Kind) item)
    {
        string key = item.Key;
        // A value of a further graphics card, switched on, brings that card (with its other chosen values) onto the card.
        if (System.Text.RegularExpressions.Regex.Match(option, @"^aio\.gpu\.([^.]+)\.") is { Success: true } gpu && (key.StartsWith("aio.gpu." + gpu.Groups[1].Value + ".") || key == "aio.cooling.gpu:" + gpu.Groups[1].Value)) return true;
        // The fan of a further graphics card brings its card along in the same way.
        if (option.StartsWith("aio.cooling.gpu:") && key.StartsWith("aio.gpu." + option["aio.cooling.gpu:".Length..] + ".")) return true;
        if (AioReadings.IsGraphKey(option)) return item.Kind == 'h' && AioReadings.GraphKey(key) == option;
        if (key == option || key.StartsWith(option + ".")) return true;
        if (option == "aio.cpu.cores") return key.StartsWith("aio.cpu.core.");
        if (option.EndsWith(".extra")) return key.StartsWith(option[..^".extra".Length] + ".");
        return false;
    }

    private static string Part(string key) => System.Text.RegularExpressions.Regex.Match(key, @"^aio\.(gpu\.[^.]+|drives\.[^.]+|[^.]+)").Value;
    internal static bool SamePart(string option, string key)
    {
        if (AioReadings.IsGraphKey(option)) return AioReadings.GraphKey(key) == option || key.StartsWith(option[..^".history".Length] + ".");
        // The same drive: its temperature (System) and its read/write/used (Drives).
        var disk = System.Text.RegularExpressions.Regex.Match(option, @"^aio\.system\.disk\.([^.]+)");
        if (disk.Success && key.StartsWith("aio.drives." + disk.Groups[1].Value + ".")) return true;
        if (option == "aio.system" && key.StartsWith("aio.drives.")) return true;
        return Part(option) is { Length: > 0 } p && (Part(key) == p || p is "aio.gpu" or "aio.drives" && key.StartsWith(p + "."));
    }

    private static (AioTile Card, Settings S, WidgetSlot Slot, Snapshot Snap, History H) Build(WidgetStyle style, string hardware)
    {
        var s = Settings.Starter(); s.HiddenValues.Clear(); DesignWidgetSelection.Apply(s, style); AioDesigns.ShowAllParts(s);
        foreach (var w in s.Widgets) w.HiddenSensors.Clear();
        var slot = s.Widgets.Single(w => w.Kind == WidgetKind.Overview);
        var (snap, h) = HardwareVariants.For(hardware);
        var card = new AioTile(style, Palette.For(true, false), Metrics.For(Density.Fit), s.ForSlot(slot)); card.Own(slot, 0);
        card.Update(snap, h);
        return (card, s, slot, snap, h);
    }

    internal static List<string> Problems(WidgetStyle style, string hardware)
    {
        var problems = new List<string>();
        var (card, s, slot, snap, h) = Build(style, hardware);
        var view = s.ForSlot(slot);
        var options = AioReadings.Options(snap, style).Select(o => o.Key).Distinct().ToArray();
        var baseline = Shown(card);
        var hidden = slot.HiddenSensors.ToList(); var enabled = slot.AioEnabledSensors?.ToList(); var hiddenValues = s.HiddenValues.ToList(); bool integrated = s.ShowIntegratedGpu;
        void Restore()
        {
            slot.HiddenSensors = hidden.ToList(); slot.AioEnabledSensors = enabled?.ToList() ?? new(); s.HiddenValues = hiddenValues.ToList(); s.ShowIntegratedGpu = integrated;
        }
        foreach (var option in options)
        {
            // In effect: switched on, and (for a value of a further graphics card) that card is on the card too.
            bool wasOn = view.Shows(option) && AioDesigns.SwitchedOn(view, option) && !AioReadings.CardSwitchedOff(snap, view, option);
            WidgetSensors.Select(s, slot, option, !wasOn, snap); card.Update(snap, h);
            var changed = Shown(card);
            Restore(); card.Update(snap, h);
            var back = Shown(card);
            if (!back.SetEquals(baseline))
                problems.Add($"{style} '{option}': restored settings, the card differs ({string.Join(", ", baseline.Except(back).Concat(back.Except(baseline)).Take(4))})");
            if (wasOn)
            {
                var gone = baseline.Except(changed).ToList();
                var foreign = gone.Where(i => !Belongs(option, i)).ToList();
                var added = changed.Except(baseline).ToList();
                if (foreign.Count > 0) problems.Add($"{style} '{option}' off also removes: {string.Join(", ", foreign.Take(5))}");
                // A design may draw another graphic of the same part instead (meters instead of a chart, one line instead of two).
                added = added.Where(i => !(i.Kind is 'g' or 'h' && SamePart(option, i.Key))).ToList();
                if (added.Count > 0) problems.Add($"{style} '{option}' off adds: {string.Join(", ", added.Take(5))}");
                if (baseline.Any(i => Belongs(option, i)) && gone.Count == 0) problems.Add($"{style} '{option}' off changes nothing although the card shows it");
            }
            else
            {
                var added = changed.Except(baseline).ToList();
                var foreign = added.Where(i => !Belongs(option, i)).ToList();
                var gone = baseline.Except(changed).ToList();
                if (foreign.Count > 0) problems.Add($"{style} '{option}' on also adds: {string.Join(", ", foreign.Take(5))}");
                if (gone.Count > 0) problems.Add($"{style} '{option}' on removes: {string.Join(", ", gone.Take(5))}");
            }
        }
        return problems;
    }

    [Theory, MemberData(nameof(Designs))]
    public void Every_sensor_switch_changes_only_its_own_items(WidgetStyle style, string hardware) => UiHost.Run(() =>
    {
        var problems = Problems(style, hardware).Select(p => hardware + ": " + p).ToList();
        if (Environment.GetEnvironmentVariable("LUMEN_SENSOR_REPORT") is { Length: > 0 } dir)
        { Directory.CreateDirectory(dir); File.WriteAllLines(Path.Combine(dir, style + "-" + hardware + ".txt"), problems); }
        Assert.True(problems.Count == 0, string.Join("\n", problems.Take(30)));
        return true;
    });
}
