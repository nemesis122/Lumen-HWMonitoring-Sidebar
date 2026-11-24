using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Lumen.App;
using Lumen.Core;
using Xunit;

/// <summary>
/// Settings → Widgets → each widget's sensor list, with an AIO design (user report 5.0.2: "System", "System · History
/// chart" and "Integrated graphics" were listed under CPU although they switch RAM, drives and graphics).
/// Every switch is listed once, in the section of the part it switches, and nothing is missing.
/// </summary>
public class SensorSectionPlacementTests
{
    private static readonly System.Reflection.BindingFlags Private = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;

    // Every AIO design on the demo PC, and every PC on one design of each family (the section of a switch depends on
    // the kind of sensor, not on the design; 65 × 5 settings windows would take very long).
    private static readonly WidgetStyle[] Families =
    {
        WidgetStyle.CaseIntelEdition, WidgetStyle.AioIsometricTiles, WidgetStyle.AioSignalRack, WidgetStyle.AioTealInstruments,
        WidgetStyle.AioPanelRings, WidgetStyle.AioMainboardMap, WidgetStyle.CaseCyanGlass, WidgetStyle.AioBlackDashboard, WidgetStyle.AioTitaniumRack
    };
    public static IEnumerable<object[]> Designs =>
        AioDesigns.Styles.Distinct().Select(s => new object[] { s, "demo" })
            .Concat(Families.Where(f => AioDesigns.Styles.Contains(f)).SelectMany(s => HardwareVariants.Names.Skip(1).Select(h => new object[] { s, h })));

    /// <summary>Which section a switch belongs in, by what it switches (independent of the code under test).</summary>
    private static WidgetKind Expected(string key) => key switch
    {
        _ when key.StartsWith("aio.cpu") => WidgetKind.Cpu,
        _ when key.StartsWith("aio.gpu") || key.StartsWith("aio.performance") => WidgetKind.Gpu,
        _ when key.StartsWith("aio.system.ram") => WidgetKind.Memory,
        _ when key.StartsWith("aio.system.disk") || key.StartsWith("aio.drives") => WidgetKind.Drives,
        _ when key.StartsWith("aio.network") => WidgetKind.Network,
        _ when key.StartsWith("aio.rails") => WidgetKind.Power,
        _ when key.StartsWith("aio.cooling") || key.StartsWith("aio.board") => WidgetKind.Cooling,
        _ => throw new InvalidOperationException("No section known for " + key)
    };

    [Theory, MemberData(nameof(Designs))]
    public void Every_switch_is_listed_once_in_its_own_section(WidgetStyle style, string hardware) => UiHost.Run(() =>
    {
        L.Use("en");
        var (snap, _) = HardwareVariants.For(hardware);
        var s = Settings.Starter();
        foreach (var w in s.Widgets) w.Style = style;
        AioDesigns.ShowAllParts(s);
        typeof(SettingsWindow).GetField("pageIndex", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.SetValue(null, 0);
        var window = new SettingsWindow(s, Palette.For(true, false), () => { }, () => new[] { "Demo" }, new IntegratedGraphicsOptionTests.TestPlatform(), snapshot: () => snap);
        try
        {
            var expanded = (HashSet<WidgetKind>)typeof(SettingsWindow).GetField("expanded", Private)!.GetValue(window)!;
            var found = new List<(string Key, WidgetKind Section)>();
            var problems = new List<string>();
            foreach (var section in AioDesigns.Switches)
            {
                expanded.Clear(); expanded.Add(section);
                typeof(SettingsWindow).GetMethod("Build", Private)!.Invoke(window, null);
                var content = (Control)typeof(SettingsWindow).GetField("content", Private)!.GetValue(window)!;
                var boxes = content.GetLogicalDescendants().OfType<CheckBox>().ToList();
                foreach (var box in boxes.Where(b => b.Tag is string k && k.StartsWith("aio.")))
                    found.Add(((string)box.Tag!, section));
                // The plain "Integrated graphics" switch is not listed for an AIO card (its card is "GPU 2 · …" under Graphics).
                if (boxes.Any(b => b.Name == "sensor-integrated-graphics")) problems.Add($"'Integrated graphics' listed under {section}");
            }
            foreach (var (key, section) in found)
            {
                var expected = Expected(key);
                if (section != expected) problems.Add($"{key} listed under {section}, belongs under {expected}");
            }
            foreach (var twice in found.GroupBy(f => f.Key).Where(g => g.Count() > 1)) problems.Add($"{twice.Key} listed {twice.Count()} times");
            var options = AioReadings.Options(snap, style).Select(o => o.Key).Where(k => k != "aio.performance.fps" && k != "aio.performance.refresh" || AioDesigns.Instruments(style));
            foreach (var missing in options.Except(found.Select(f => f.Key))) problems.Add($"{missing} is not listed anywhere");
            Assert.DoesNotContain(found, f => f.Key == "aio.system");
            // The test really sees the lists: CPU, graphics, memory, drives and network each list switches, RAM is under Memory.
            foreach (var section in new[] { WidgetKind.Cpu, WidgetKind.Gpu, WidgetKind.Memory, WidgetKind.Drives })
                Assert.Contains(found, f => f.Section == section);
            Assert.Contains(found, f => f.Key == "aio.system.ram" && f.Section == WidgetKind.Memory);
            Assert.True(problems.Count == 0, string.Join("\n", problems));
        }
        // Run what the closed window posted: the test thread pumps no dispatcher of its own, and every window it left
        // waiting kept about 40 MB (179 designs ran out of memory).
        finally { window.Close(); Avalonia.Threading.Dispatcher.UIThread.RunJobs(); }
        return true;
    });
}
