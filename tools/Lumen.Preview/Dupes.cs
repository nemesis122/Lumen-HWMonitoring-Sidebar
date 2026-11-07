using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.Headless;
using Lumen.App;
using Lumen.Core;

static class Dupes
{
    // Lists, per design and widget, every value text that is shown more than once.
    public static void Run(string output, Snapshot snapshot, History history)
    {
        var lines = new List<string>();
        var kinds = new[] { WidgetKind.Cpu, WidgetKind.Gpu, WidgetKind.Memory, WidgetKind.Network, WidgetKind.Drives, WidgetKind.Cooling, WidgetKind.Power };
        foreach (var style in Settings.Selectable)
        foreach (var kind in kinds)
        {
            var s = new Settings { Language = "de" };
            var card = Cards.Create(kind, style, 0, Palette.For(true, false), Metrics.For(Density.Fit), s);
            ((IWidget)card).Update(snapshot, history);
            var window = new Window { Content = card, Width = 320, Height = 900 };
            window.Show();
            for (int i = 0; i < 3; i++) { Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame(); }
            var texts = card.GetLogicalDescendants().OfType<TextBlock>()
                .Where(t => t.IsEffectivelyVisible && t.Bounds.Width > 0 && !string.IsNullOrWhiteSpace(t.Text) && t.Text!.Any(char.IsDigit))
                .Select(t => (Text: t.Text!.Trim(), Size: t.FontSize)).ToList();
            // Combine a big number with its separate unit ("41" + "°C") so it compares with "41 °C".
            var all = card.GetLogicalDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && t.Bounds.Width > 0 && !string.IsNullOrWhiteSpace(t.Text)).ToList();
            var joined = new List<string>();
            for (int i = 0; i < all.Count; i++)
            {
                string t = all[i].Text!.Trim();
                if (i + 1 < all.Count && t.All(c => char.IsDigit(c) || c is '.' or ',' or '-') && all[i + 1].Text!.Trim() is "°C" or "°F" or "%" or "W" or "GHz" or "MHz")
                { joined.Add(t + " " + all[i + 1].Text!.Trim()); i++; continue; }
                if (t.Any(char.IsDigit)) joined.Add(t);
            }
            string Norm(string x) => x.Replace(" ", "").Replace(" ", "").Replace(" ", "");
            var dup = joined.GroupBy(Norm).Where(g => g.Count() > 1 && g.Key.Length > 1).Select(g => $"{g.First()} ×{g.Count()}").ToList();
            if (dup.Count > 0) lines.Add($"{style,-14} {kind,-8} {string.Join(" | ", dup)}");
            window.Close();
        }
        File.WriteAllLines(Path.Combine(output, "dupes.txt"), lines);
        Console.WriteLine($"dupes: {lines.Count} widget/design combinations with repeated values");
    }
}
