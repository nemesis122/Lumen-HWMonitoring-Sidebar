using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Lumen.App;
using Lumen.Core;
using Xunit;
using Xunit.Abstractions;

// Every design shows every drive of the drives widget (the Dial design one per page with a switcher).
public class AllDrivesTests
{
    private readonly ITestOutputHelper output;

    public AllDrivesTests(ITestOutputHelper output) => this.output = output;

    public static IEnumerable<object[]> Styles() => Settings.Selectable.Select(s => new object[] { s });

    [Theory]
    [MemberData(nameof(Styles))]
    public void Every_drive_is_shown(WidgetStyle style)
    {
        var (texts, missing) = UiHost.Run(() =>
        {
            L.Use("de");
            var (snap, history) = Render.Demo();
            var template = snap.Disks[0];
            var disks = new[]
            {
                template with { Id = "d1", Name = "Samsung SSD 990 PRO", Letters = "C:", Temperature = 44 },
                template with { Id = "d2", Name = "WD Black SN850X", Letters = "F:", Temperature = 39, UsedPercent = 61 },
                template with { Id = "d3", Name = "Crucial MX500", Letters = "G:", Temperature = 35, UsedPercent = 22 },
                template with { Id = "d4", Name = "Seagate IronWolf", Letters = "H:", Temperature = 31, UsedPercent = 80 },
                template with { Id = "d5", Name = "Kingston A2000", Letters = "I:", Temperature = 37, UsedPercent = 15 },
                template with { Id = "d6", Name = "Toshiba X300", Letters = "J:", Temperature = 33, UsedPercent = 70 }
            };
            snap = snap with { Disks = disks };
            var card = Cards.Create(WidgetKind.Drives, style, 0, Palette.For(true, false), Metrics.For(Density.Fit), new Settings { Language = "de" });
            ((IWidget)card).Update(snap, history);
            var window = new Window { Content = card, Width = 340, Height = 1200 };
            window.Show();
            for (int i = 0; i < 3; i++) { Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame(); }
            var shown = card.GetLogicalDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && t.Bounds.Width > 0).Select(t => t.Text ?? "").ToList();
            window.Close();
            string all = string.Join(" | ", shown);
            var lost = new[] { "C:", "F:", "G:", "H:", "I:", "J:" }.Where(l => !all.Contains(l)).ToList();
            if (style == WidgetStyle.Dial && all.Contains("1/6")) lost.Clear();
            return (all, lost);
        });
        output.WriteLine(texts);
        Assert.True(missing.Count == 0, style + ": missing " + string.Join(",", missing) + "\n" + texts);
    }

    // The designs that list drives line by line: every drive the same size, none named "the hottest".
    public static IEnumerable<object[]> Listed() => new[]
    {
        WidgetStyle.Segments, WidgetStyle.Pill, WidgetStyle.PillRow, WidgetStyle.Duplex, WidgetStyle.Dial, WidgetStyle.HistoryGraph,
        WidgetStyle.RingPair, WidgetStyle.NeonHud, WidgetStyle.Thermostat, WidgetStyle.PurpleDash, WidgetStyle.SciFi
    }.Select(s => new object[] { s });

    [Theory]
    [MemberData(nameof(Listed))]
    public void Every_drive_is_a_line_of_the_same_size(WidgetStyle style)
    {
        var (sizes, texts) = UiHost.Run(() =>
        {
            L.Use("de");
            var (snap, history) = Render.Demo();
            var template = snap.Disks[0];
            snap = snap with { Disks = new[]
            {
                template with { Id = "d1", Letters = "C:", Temperature = 56 },
                template with { Id = "d2", Letters = "G:", Temperature = 59 },
                template with { Id = "d3", Letters = "F:", Temperature = 34 },
                template with { Id = "d4", Letters = "H:", Temperature = 41 }
            } };
            var card = Cards.Create(WidgetKind.Drives, style, 0, Palette.For(true, false), Metrics.For(Density.Fit), new Settings { Language = "de" });
            ((IWidget)card).Update(snap, history);
            var window = new Window { Content = card, Width = 340, Height = 1200 };
            window.Show();
            for (int i = 0; i < 3; i++) { Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame(); }
            var blocks = card.GetLogicalDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && t.Bounds.Width > 0).ToList();
            var temps = new[] { "56", "59", "34", "41" }.Select(n => blocks.Where(t => (t.Text ?? "") == n || (t.Text ?? "").StartsWith(n + " ")).Select(t => t.FontSize).DefaultIfEmpty(0).Max()).ToList();
            string all = string.Join(" | ", blocks.Select(t => $"{t.Text}@{t.FontSize:0.#}"));
            window.Close();
            return (temps, all);
        });
        Assert.DoesNotContain("Wärmst", texts);
        Assert.True(sizes.All(s => s > 0), style + ": a drive is missing\n" + texts);
        Assert.True(sizes.Distinct().Count() == 1, style + ": drives in different sizes " + string.Join(", ", sizes) + "\n" + texts);
    }
}
