using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Lumen.App;
using Lumen.Core;
using Xunit;

/// <summary>
/// User report 5.0.2: switching off "History chart" under Processor also removed the network history.
/// Every part now has its own history switch, in every AIO design.
/// </summary>
public class AioHistorySwitchTests
{
    public static IEnumerable<object[]> Designs => AioDesigns.Styles.Distinct().Where(s => !AioDesigns.Panel(s)).Select(s => new object[] { s });

    private static (Control Card, HashSet<string> Plotted) Build(WidgetStyle style, params string[] off)
    {
        var s = Settings.Starter(); s.HiddenValues.Clear(); DesignWidgetSelection.Apply(s, style); AioDesigns.ShowAllParts(s);
        var overview = s.Widgets.Single(w => w.Kind == WidgetKind.Overview);
        foreach (var w in s.Widgets) w.HiddenSensors.Clear();
        foreach (var key in off) overview.HiddenSensors.Add(key);
        var card = Cards.Create(WidgetKind.Overview, style, 0, Palette.For(true, false), Metrics.For(Density.Comfortable), s.ForSlot(overview));
        ((Lumen.App.INamed)card).Own(overview, 0);
        var (snap, hist) = Render.Demo();
        var window = new Window { Width = 360, Height = 2200, Content = card }; window.Show();
        for (int i = 0; i < 2; i++) { ((IWidget)card).Update(snap, hist); Dispatcher.UIThread.RunJobs(); }
        var plotted = card.GetLogicalDescendants().OfType<Control>().Where(c => System.Text.RegularExpressions.Regex.IsMatch(c.GetType().Name, "History|Area|Graph|Sparkline|Trace|Traffic|Chart"))
            .Select(c => c.GetValue(TextColours.SensorProperty)).Where(k => k.Length > 0).ToHashSet();
        window.Close();
        return (card, plotted);
    }

    [Theory, MemberData(nameof(Designs))]
    public void Processor_history_off_keeps_the_network_history(WidgetStyle style) => UiHost.Run(() =>
    {
        var (_, all) = Build(style);
        bool network = all.Any(k => k.StartsWith("aio.network.")), cpu = all.Any(k => k.StartsWith("aio.cpu."));
        var (_, without) = Build(style, "aio.cpu.history");
        Assert.DoesNotContain(without, k => k.StartsWith("aio.cpu.") && !k.StartsWith("aio.cpu.core"));
        if (network) Assert.Contains(without, k => k.StartsWith("aio.network."));
        var (_, noNet) = Build(style, "aio.network.history");
        Assert.DoesNotContain(noNet, k => k.StartsWith("aio.network."));
        if (cpu) Assert.Contains(noNet, k => k.StartsWith("aio.cpu."));
        return true;
    });

    [Fact]
    public void The_old_single_switch_becomes_the_processor_switch() => UiHost.Run(() =>
    {
        var s = Settings.Starter(); var slot = s.Widgets[0]; slot.HiddenSensors.Add("aio.graphs");
        Settings.Normalize(s);
        Assert.Contains("aio.cpu.history", slot.HiddenSensors);
        Assert.DoesNotContain("aio.graphs", slot.HiddenSensors);
        return true;
    });

    [Fact]
    public void Each_part_lists_its_own_history_switch() =>
        Assert.Equal(AioReadings.GraphSwitches.Select(g => g.Key), AioReadings.Options(null).Select(o => o.Key).Where(AioReadings.IsGraphKey));
}

public class AioHistorySwitchPlacementTests
{
    [Theory]
    [InlineData("aio.cpu.history", WidgetKind.Cpu)] [InlineData("aio.gpu.history", WidgetKind.Gpu)]
    [InlineData("aio.cooling.history", WidgetKind.Cooling)] [InlineData("aio.system.ram.history", WidgetKind.Memory)]
    [InlineData("aio.drives.history", WidgetKind.Drives)] [InlineData("aio.network.history", WidgetKind.Network)]
    public void Each_history_switch_is_listed_under_its_own_part(string key, WidgetKind section) => Assert.Equal(section, AioDesigns.SwitchFor(key));

    [Theory]
    [InlineData("aio.system.ram", "aio.system.ram.history")] [InlineData("aio.system.disk.nvme0", "aio.drives.history")]
    [InlineData("aio.drives.nvme0.read", "aio.drives.history")] [InlineData("aio.network.down", "aio.network.history")]
    public void Each_reading_uses_the_history_switch_of_its_part(string reading, string history) => Assert.Equal(history, AioReadings.GraphKey(reading));
}
