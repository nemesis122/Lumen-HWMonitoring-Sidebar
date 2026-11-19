using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Lumen.App;
using Lumen.Core;
using Xunit;

/// <summary>
/// The colours and fonts chosen in Appearance (global, not per widget) reach the drive, network and voltage parts of
/// every AIO design, the same as the CPU, GPU and memory parts. User report 4.5.5: compact drive values, network
/// values and the voltage boxes kept the design's own red although the value colour was set to blue.
/// </summary>
public class AioGlobalAppearanceTests
{
    private const string ValueHex = "#3B82F6", LabelHex = "#F5F5F5", TitleHex = "#FDE047", GraphHex = "#22C55E";
    public static IEnumerable<object[]> Designs => AioDesigns.Styles.Distinct().SelectMany(s => new[] { new object[] { s, true }, new object[] { s, false } });

    private static Settings Configure(WidgetStyle style, bool all)
    {
        var s = Settings.Starter(); s.HiddenValues.Clear();
        DesignWidgetSelection.Apply(s, style); AioDesigns.ShowAllParts(s);
        foreach (var slot in s.Widgets) slot.HiddenSensors.Clear();
        s.ValueColor = ValueHex; s.LabelColor = LabelHex;
        // Only value and label colour: the plain path of TextColours (no colour rules active).
        if (all) { s.TitleColor = TitleHex; s.GraphColor = GraphHex; s.ValueFont = "jetbrains-mono"; s.LabelFont = "rajdhani"; s.TitleFont = "orbitron"; }
        return s;
    }

    private static bool Inside(Control c, Control root, Func<Control, bool> test)
    {
        for (Control? node = c; node != null && node != root; node = node.Parent as Control) if (test(node)) return true;
        return false;
    }

    private static string Part(TextBlock t, Control root)
    {
        string key = t.GetValue(TextColours.SensorProperty);
        if (key.Length == 0) key = SensorDetails.KeyOf(t);
        if (key.StartsWith("aio.drives.") || key.StartsWith("aio.system.disk.") || Inside(t, root, n => n.Tag is string tag && tag.StartsWith("atlas-drive"))) return "drives";
        if (key.StartsWith("aio.network.") || Inside(t, root, n => Equals(n.Tag, "compact-network-values"))) return "network";
        if (key.StartsWith("aio.rails.")) return "voltages";
        return "";
    }

    internal static List<string> Problems(WidgetStyle style, bool all, bool render = false)
    {
        var s = Configure(style, all);
        var overview = s.Widgets.Single(w => w.Kind == WidgetKind.Overview);
        var card = Cards.Create(WidgetKind.Overview, style, 0, Palette.For(true, false), Metrics.For(Density.Comfortable), s.ForSlot(overview));
        ((Lumen.App.INamed)card).Own(overview, 0);
        var (snapshot, history) = Render.Demo();
        for (int tick = 0; tick < 3; tick++) { ((IWidget)card).Update(snapshot, history); TextColours.Apply(card, s); }
        var problems = new List<string>();
        var window = new Window { Width = 440, Height = 2400, Content = card, Background = Brushes.Black };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            if (render && Environment.GetEnvironmentVariable("LUMEN_APPEARANCE_OUTPUT") is { Length: > 0 } dir)
            {
                Directory.CreateDirectory(dir);
                using var frame = window.CaptureRenderedFrame(); frame!.Save(Path.Combine(dir, style + (all ? "" : "-values") + ".png"));
            }
            foreach (var text in card.GetLogicalDescendants().OfType<TextBlock>().Where(t => !string.IsNullOrWhiteSpace(t.Text) && t.IsEffectivelyVisible))
            {
                string part = Part(text, card); if (part.Length == 0) continue;
                var role = TextFonts.RoleOf(text);
                if (!all && role is TextRole.Title or TextRole.Graph) continue;
                string wantedHex = role switch { TextRole.Title => TitleHex, TextRole.Value => ValueHex, TextRole.Graph => GraphHex, _ => LabelHex };
                var wanted = Color.Parse(wantedHex);
                if (text.Foreground is ISolidColorBrush b && (b.Color.R != wanted.R || b.Color.G != wanted.G || b.Color.B != wanted.B))
                    problems.Add($"{style} {part} colour {role} '{text.Text}' = {b.Color}");
                var family = Ui.FamilyFor(TextFonts.Key(s, role));
                if (all && !Equals(text.FontFamily, family)) problems.Add($"{style} {part} font {role} '{text.Text}' = {text.FontFamily}");
            }
            foreach (var frame in card.GetLogicalDescendants().OfType<Border>().Where(b => all && b.GetValue(TextColours.FrameProperty)))
                if (frame.BorderBrush is ISolidColorBrush fb && (fb.Color.R, fb.Color.G, fb.Color.B) != (Color.Parse(GraphHex).R, Color.Parse(GraphHex).G, Color.Parse(GraphHex).B))
                    problems.Add($"{style} frame '{frame.Tag}' = {fb.Color}");
        }
        finally { window.Close(); }
        return problems;
    }

    [Theory, MemberData(nameof(Designs))]
    public void Global_colours_and_fonts_reach_drives_network_and_voltages(WidgetStyle style, bool all) => UiHost.Run(() =>
    {
        var problems = Problems(style, all, render: true);
        Assert.True(problems.Count == 0, string.Join("\n", problems.Take(40)));
        return true;
    });
}
