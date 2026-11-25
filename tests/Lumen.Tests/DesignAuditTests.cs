using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Headless;
using Lumen.App;
using Lumen.Core;
using Xunit;

/// <summary>
/// An audit on request (LUMEN_AUDIT=&lt;folder&gt;): per AIO design and hardware variant, texts cut off with "…", texts drawn
/// smaller than 9 px, and (LUMEN_AUDIT_SWITCHES=1) sensor switches in Settings that change nothing in that design.
/// It only lists; the release tests decide.
/// </summary>
public class DesignAuditTests
{
    private static IEnumerable<WidgetStyle> Chosen() => Environment.GetEnvironmentVariable("LUMEN_STYLES") is { Length: > 0 } names
        ? names.Split(',').Select(Enum.Parse<WidgetStyle>) : AioDesigns.Styles.Distinct();
    private static string[] Hardware() => (Environment.GetEnvironmentVariable("LUMEN_RELEASE_HARDWARE") ?? string.Join(",", HardwareVariants.Names)).Split(',');

    /// <summary>
    /// Every AIO design on every test PC (5.7.0, user request "everything must be readable"): no hardware name or label cut
    /// off, no word broken over two lines, no label or value drawn below 9 px. Units beside a number ("°C", "%") and the
    /// 8-bit face of Pixel 8 (its letters fill the whole em, 6.5 px there reads like 9 px elsewhere) are left out.
    /// </summary>
    [Fact]
    public void Every_text_is_whole_and_readable() => UiHost.Run(() =>
    {
        var problems = Texts(AioDesigns.Styles.Distinct(), HardwareVariants.Names, null)
            .Where(l => l.Split('\t') is var f && !(f[2] == "tiny" && (Units.Contains(f[4]) || f[0] == nameof(WidgetStyle.AioPixel8)))).ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems.Take(30)));
        return true;
    }, 3600);
    private static readonly HashSet<string> Units = new() { "°C", "°F", "%", "—", "—°", "W", "R", "V", "rpm" };

    [Fact]
    public void Lists_cut_and_tiny_texts_on_request() => UiHost.Run(() =>
    {
        if (Environment.GetEnvironmentVariable("LUMEN_AUDIT") is not { Length: > 0 } folder) return true;
        Directory.CreateDirectory(folder);
        var lines = Texts(Chosen(), Hardware(), Environment.GetEnvironmentVariable("LUMEN_AUDIT_PICTURES") == "1" ? folder : null);
        File.WriteAllLines(Path.Combine(folder, "texts-" + (Environment.GetEnvironmentVariable("LUMEN_SHARD") ?? "all").Replace('/', '-') + ".tsv"), lines);
        return true;
    }, 7200);

    private static List<string> Texts(IEnumerable<WidgetStyle> styles, IEnumerable<string> hardware, string? pictures)
    {
        var lines = new List<string>();
        foreach (var style in styles)
            foreach (var hw in hardware)
            {
                var (card, window) = NeoDesignTests.Show(style, hw);
                try
                {
                    if (pictures != null)
                    {
                        using var frame = window.CaptureRenderedFrame()!;
                        frame.Save(Path.Combine(pictures, $"{style}-{hw}.png"));
                    }
                    foreach (var t in card.GetLogicalDescendants().OfType<TextBlock>().Where(t => !string.IsNullOrWhiteSpace(t.Text) && SensorDetails.Visible(t, card) && t.Bounds.Width > 0))
                    {
                        var m = t.TransformToVisual(card);
                        double drawn = t.FontSize * (m is { } x ? Math.Sqrt(Math.Abs(x.M11 * x.M22)) : 1);
                        if (drawn < 9) lines.Add($"{style}\t{hw}\ttiny\t{drawn:0.0}px\t{t.Text}");
                        // A word broken over two lines ("Availa-ble"): wrapping text whose longest word is wider than its box.
                        if (t.TextWrapping != TextWrapping.NoWrap && t.Text!.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w => new TextBlock { Text = w, FontFamily = t.FontFamily, FontSize = t.FontSize, FontWeight = t.FontWeight, LetterSpacing = t.LetterSpacing })
                                .Any(w => { w.Measure(Size.Infinity); return w.DesiredSize.Width > t.Bounds.Width + 1; }))
                            lines.Add($"{style}\t{hw}\tbroken\t{t.Bounds.Width:0}\t{t.Text}");
                        if (t.TextTrimming != TextTrimming.None)
                        {
                            // Cut when the whole text needs more room than the box has: wider on one line, or (wrapping text with
                            // a line limit) more lines than it may show.
                            var full = new TextBlock { Text = t.Text, FontFamily = t.FontFamily, FontSize = t.FontSize, FontWeight = t.FontWeight, LetterSpacing = t.LetterSpacing, FontStretch = t.FontStretch,
                                TextWrapping = t.TextWrapping, LineHeight = t.LineHeight };
                            bool wraps = t.TextWrapping != TextWrapping.NoWrap;
                            full.Measure(wraps ? new Size(t.Bounds.Width, double.PositiveInfinity) : Size.Infinity);
                            if (wraps ? full.DesiredSize.Height > t.Bounds.Height + 1 : full.DesiredSize.Width > t.Bounds.Width + 1) lines.Add($"{style}\t{hw}\tcut\t{t.Bounds.Width:0}<{full.DesiredSize.Width:0}\t{t.Text}");
                        }
                    }
                }
                finally { window.Close(); Dispatcher.UIThread.RunJobs(); }
            }
        return lines;
    }

    [Fact]
    public void Lists_switches_that_change_nothing_on_request() => UiHost.Run(() =>
    {
        if (Environment.GetEnvironmentVariable("LUMEN_AUDIT") is not { Length: > 0 } folder || Environment.GetEnvironmentVariable("LUMEN_AUDIT_SWITCHES") != "1") return true;
        Directory.CreateDirectory(folder);
        string file = Path.Combine(folder, "switches-" + (Environment.GetEnvironmentVariable("LUMEN_SHARD") ?? "all").Replace('/', '-') + ".tsv");
        var done = File.Exists(file) ? File.ReadAllLines(file).Where(l => l.StartsWith("#done\t")).Select(l => l[6..]).ToHashSet() : new HashSet<string>();
        var styles = Chosen().ToArray();
        if (Environment.GetEnvironmentVariable("LUMEN_SHARD") is { } shard && shard.Split('/') is [var a, var b] && int.TryParse(a, out int i) && int.TryParse(b, out int n))
            styles = styles.Where((_, k) => k % n == i).ToArray();
        foreach (var style in styles)
            foreach (var hw in Hardware())
            {
                if (done.Contains($"{style}\t{hw}")) continue;
                var lines = ReleaseSettingsUiTests.SwitchesWithoutEffect(style, hw).Select(l => $"{style}\t{hw}\t{l}").ToList();
                lines.Add($"#done\t{style}\t{hw}");
                File.AppendAllLines(file, lines);
            }
        return true;
    }, 7200 * 4);
}
