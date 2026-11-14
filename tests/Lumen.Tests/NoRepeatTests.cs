using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Lumen.App;
using Lumen.Core;
using Xunit;

// A widget shows each reading once: what the upper part shows (bar, big number) is not repeated below.
public class NoRepeatTests
{
    public static IEnumerable<object[]> Designs() => Settings.Selectable.Select(s => new object[] { s });

    private static readonly WidgetKind[] Kinds =
        { WidgetKind.Cpu, WidgetKind.Gpu, WidgetKind.Memory, WidgetKind.Network, WidgetKind.Drives, WidgetKind.Cooling, WidgetKind.Power };

    private static string Norm(string text) => new string(text.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToLowerInvariant();

    [Theory]
    [MemberData(nameof(Designs))]
    public void No_value_is_shown_twice(WidgetStyle style)
    {
        var repeats = UiHost.Run(() =>
        {
            L.Use("de");
            var (snapshot, history) = Render.Demo();
            var found = new List<string>();
            foreach (var kind in Kinds)
            {
                var card = Cards.Create(kind, style, 0, Palette.For(true, false), Metrics.For(Density.Fit), new Settings { Language = "de" });
                ((IWidget)card).Update(snapshot, history);
                var window = new Window { Content = card, Width = 320, Height = 900 };
                window.Show();
                for (int i = 0; i < 3; i++) { Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame(); }
                if (kind is WidgetKind.Drives or WidgetKind.Network)
                {
                    // Distinct devices and read/write counters may legitimately have equal numbers.
                    // Check source identity so that real duplicates still fail, including detail rows.
                    var keys = card.GetLogicalDescendants().OfType<TextBlock>()
                        .Where(t => SensorDetails.Visible(t, card) && t.FontFeatures != null && !string.IsNullOrWhiteSpace(t.Text)
                            && (t.Text.Any(char.IsDigit) || t.Text == "—"))
                        .Select(t => new[] { SensorDetails.KeyOf(t) }.Concat(t.GetLogicalAncestors().OfType<Control>().Select(SensorDetails.KeyOf))
                            .FirstOrDefault(k => k.StartsWith(kind == WidgetKind.Drives ? "drive:" : "net.")))
                        .Where(k => !string.IsNullOrEmpty(k) && !k.EndsWith(":model"));
                    found.AddRange(keys.GroupBy(k => k).Where(g => g.Count() > 1).Select(g => $"{kind}: {g.Key} ×{g.Count()}"));
                    window.Close(); continue;
                }
                var texts = card.GetLogicalDescendants().OfType<TextBlock>()
                    .Where(t => t.IsEffectivelyVisible && t.Bounds.Width > 0 && !string.IsNullOrWhiteSpace(t.Text)).Select(t => t.Text!.Trim()).ToList();
                var values = new List<string>();
                for (int i = 0; i < texts.Count; i++)
                {
                    string t = texts[i];
                    bool number = t.Length > 0 && t.All(c => char.IsDigit(c) || c is '.' or ',' or '-');
                    if (number && i + 1 < texts.Count && texts[i + 1] is "°C" or "°F" or "%" or "W") { values.Add(t + texts[i + 1]); i++; }
                    else if (t.Any(char.IsDigit)) values.Add(t);
                }
                // Read and write rates may both be 0 B/s; they are different readings that happen to match.
                found.AddRange(values.Where(v => !v.EndsWith("B/s")).GroupBy(Norm).Where(g => g.Count() > 1).Select(g => $"{kind}: {g.Key} ×{g.Count()}"));
                window.Close();
            }
            return found;
        });
        Assert.True(repeats.Count == 0, $"{style}: " + string.Join(", ", repeats));
    }

    [Fact]
    public void Rows_are_balanced_and_the_last_row_fills_the_width()
    {
        var (four, three) = UiHost.Run(() =>
        {
            Control[] Cells(int n) => Enumerable.Range(0, n).Select(_ => (Control)new Border { Height = 10 }).ToArray();
            var a = new StatPanel(Array.Empty<double>(), Cells(4)) { MaxColumns = 3, Balanced = true };
            a.Measure(new Size(300, double.PositiveInfinity)); a.Arrange(new Rect(0, 0, 300, a.DesiredSize.Height));
            var b = new StatPanel(Array.Empty<double>(), Cells(3)) { MaxColumns = 2, Balanced = true };
            b.Measure(new Size(300, double.PositiveInfinity)); b.Arrange(new Rect(0, 0, 300, b.DesiredSize.Height));
            return (a.Children.Select(c => c.Bounds).ToArray(), b.Children.Select(c => c.Bounds).ToArray());
        });
        // 4 values: two rows of two instead of 3 + 1.
        Assert.All(four, r => Assert.Equal(150, r.Width, 1));
        Assert.Equal(2, four.Select(r => r.Y).Distinct().Count());
        // 3 values in two columns: the single value of the last row takes the full width.
        Assert.Equal(300, three[2].Width, 1);
    }

    [Fact]
    public void First_drive_is_named_without_hottest_and_not_repeated()
    {
        var summary = UiHost.Run(() =>
        {
            L.Use("de");
            var (demo, history) = Render.Demo();
            var snapshot = demo with { Disks = demo.Disks.Take(2).Select((d, i) => d with { Letters = i == 0 ? "G:" : "C:", Temperature = i == 0 ? 80 : 30 }).ToArray() };
            var set = new Settings { Language = "de" };
            var sum = Summaries.For(WidgetKind.Drives, 0, snapshot, history, set, Palette.For(true, false));
            var extras = Summaries.Extras(WidgetKind.Drives, 0, snapshot, history, set);
            return (sum.HeroLabel, sum.Value, Labels: extras.Select(e => e.Label).ToList());
        });
        Assert.StartsWith("C: · ", summary.HeroLabel);
        Assert.DoesNotContain("Wärmstes", summary.HeroLabel);
        Assert.Equal("30", summary.Value);
        Assert.DoesNotContain("C:", summary.Labels);
        Assert.Contains("G:", summary.Labels);
    }

    [Fact]
    public void Cooling_shows_the_mainboard_big()
    {
        var (label, extras) = UiHost.Run(() =>
        {
            L.Use("de");
            var (snapshot, history) = Render.Demo();
            var set = new Settings { Language = "de" };
            var sum = Summaries.For(WidgetKind.Cooling, 0, snapshot, history, set, Palette.For(true, false));
            return (sum.HeroLabel, Summaries.Extras(WidgetKind.Cooling, 0, snapshot, history, set).Select(e => e.Label).ToList());
        });
        Assert.Equal("Board", label);
        Assert.DoesNotContain("Board", extras);
    }
}
