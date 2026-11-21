using Avalonia.Controls;
using Lumen.App;
using Lumen.Core;
using Xunit;

/// <summary>
/// The sensor selection of every single-widget design (CPU, GPU, Network … in every non-AIO design): each switch,
/// changed on its own, only removes or adds its own items; with the settings restored, the widget is the same again.
/// </summary>
public class WidgetSensorSelectionTests
{
    private static readonly WidgetKind[] Kinds = Settings.Starter().Widgets.Select(w => w.Kind).Where(k => k != WidgetKind.Overview).Distinct().ToArray();
    public static IEnumerable<object[]> Designs => Settings.Selectable.Where(s => !AioDesigns.Contains(s)).SelectMany(s => HardwareVariants.Names.Select(h => new object[] { s, h }));

    private static bool Belongs(string option, (string Key, char Kind) item)
    {
        string key = item.Key;
        if (option.EndsWith(".history") || option.EndsWith(".chart")) return item.Kind == 'h' || key == option;
        if (key == option || key.StartsWith(option + ".") || key.StartsWith(option + ":")) return true;
        // A field switch for every item of a list: "drives.temp" → "drive:nvme0:temp", "world.time" → "world:Asia/Tokyo:time".
        if (option.Contains('.') && key.Contains(':') && key.EndsWith(":" + option[(option.LastIndexOf('.') + 1)..])) return true;
        if (SensorDefaults.Groups.TryGetValue(option, out var children) && children.Any(c => key == c || key.StartsWith(c + "."))) return true;
        // Switches whose items carry another field name.
        string[] fields = option switch
        {
            "proc.memory" => new[] { "ram" }, "volumes.detail" => new[] { "total" }, "drives.io" => new[] { "read", "write" },
            "health.count" => new[] { "powercount" }, _ => Array.Empty<string>()
        };
        if (key.Contains(':') && fields.Any(f => key.EndsWith(":" + f))) return true;
        if (option == "weather.forecast") return key.StartsWith("weather:");
        // The running-fans count counts the chosen fans: without fans there is nothing to count.
        if (option == "cooling.fans") return key.StartsWith("fan:") || key == "cooling.fancount";
        // One fan: its count line counts it.
        if (option.StartsWith("fan:") && key == "cooling.fancount") return true;
        if (option == "cooling.board") return key.StartsWith("temp:");
        return false;
    }
    private static string Head(string key) => key.Split('.', ':')[0] switch { "drive" => "drives", "volume" => "volumes", "process" => "proc", var h => h };

    private static HashSet<(string, char)> Draw(WidgetKind kind, WidgetStyle style, Settings s, WidgetSlot slot, Snapshot snap, History h)
    {
        var card = Cards.Create(kind, style, 0, Palette.For(true, false), Metrics.For(Density.Fit), s.ForSlot(slot));
        if (card is Lumen.App.INamed named) named.Own(slot, 0);
        if (card is IWidget widget) { widget.Update(snap, h); widget.Update(snap, h); }
        return AioSensorSelectionTests.Shown(card);
    }

    internal static List<string> Problems(WidgetStyle style, string hardware)
    {
        var problems = new List<string>();
        var (snap, h) = HardwareVariants.For(hardware);
        foreach (var kind in Kinds)
        {
            var s = Settings.Starter(); s.HiddenValues.Clear();
            var slot = s.Widgets.First(w => w.Kind == kind); slot.Style = style; slot.HiddenSensors.Clear();
            HashSet<(string, char)> baseline;
            try { baseline = Draw(kind, style, s, slot, snap, h); } catch (Exception e) { problems.Add($"{style} {kind}: cannot be shown ({e.GetType().Name})"); continue; }
            var hidden = slot.HiddenSensors.ToList(); var hiddenValues = s.HiddenValues.ToList();
            foreach (var option in WidgetSensors.Options(kind, snap, style).Select(o => o.Key).Distinct())
            {
                bool wasOn = s.ForSlot(slot).Shows(option);
                WidgetSensors.Select(s, slot, option, !wasOn, snap);
                var changed = Draw(kind, style, s, slot, snap, h);
                slot.HiddenSensors = hidden.ToList(); s.HiddenValues = hiddenValues.ToList();
                var gone = baseline.Except(changed).ToList(); var added = changed.Except(baseline).ToList();
                if (wasOn)
                {
                    // A foreign value must not vanish; its graphic may change while the value itself stays visible
                    // (the hero value moves up into the badge, a combined bar becomes a single one).
                    var foreign = gone.Where(i => !Belongs(option, i) && !(i.Item2 is 'g' or 'h' && changed.Any(c => c.Item1 == i.Item1))).ToList();
                    // The widget may draw another graphic of the same kind of item instead (e.g. fan bars once board temperatures are off).
                    // A design with a fixed number of places fills a freed place with another value of the same widget.
                    added = added.Where(i => !(Head(i.Item1) == Head(option) || kind == WidgetKind.Cooling && i.Item2 is 'g' or 'h')).ToList();
                    // With its last value switched off, a widget shows its empty placeholder ("—") under its main key.
                    if (changed.All(c => c.Item1 == WidgetSensors.PrimaryKey(kind) || !baseline.Contains(c) && c.Item2 == 't' && c.Item1 == WidgetSensors.PrimaryKey(kind) || baseline.Contains(c)))
                        added = added.Where(i => i.Item1 != WidgetSensors.PrimaryKey(kind)).ToList();
                    if (foreign.Count > 0) problems.Add($"{style} {kind} '{option}' off also removes: {string.Join(", ", foreign.Take(5))}");
                    if (added.Count > 0) problems.Add($"{style} {kind} '{option}' off adds: {string.Join(", ", added.Take(5))}");
                }
                else
                {
                    var foreign = added.Where(i => !Belongs(option, i)).ToList();
                    if (foreign.Count > 0) problems.Add($"{style} {kind} '{option}' on also adds: {string.Join(", ", foreign.Take(5))}");
                    if (gone.Count > 0) problems.Add($"{style} {kind} '{option}' on removes: {string.Join(", ", gone.Take(5))}");
                }
            }
        }
        return problems;
    }

    [Theory, MemberData(nameof(Designs))]
    public void Every_sensor_switch_changes_only_its_own_items(WidgetStyle style, string hardware) => UiHost.Run(() =>
    {
        var problems = Problems(style, hardware).Select(p => hardware + ": " + p).ToList();
        if (Environment.GetEnvironmentVariable("LUMEN_SENSOR_REPORT") is { Length: > 0 } dir)
        { Directory.CreateDirectory(dir); File.WriteAllLines(Path.Combine(dir, "widget-" + style + "-" + hardware + ".txt"), problems); }
        Assert.True(problems.Count == 0, string.Join("\n", problems.Take(30)));
        return true;
    });
}
