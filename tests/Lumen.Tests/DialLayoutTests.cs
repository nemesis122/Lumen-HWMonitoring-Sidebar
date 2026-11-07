using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Lumen.App;
using Lumen.Core;
using Xunit;

// "Messuhr" in two columns: cards stay in place from update to update and both columns end on one line.
public class DialLayoutTests
{
    [Theory]
    [InlineData(Density.Fit)]
    [InlineData(Density.Comfortable)]
    public void Two_columns_stay_still_and_end_together(Density density)
    {
        var dbg = new List<string>();
        var (moves, bottoms) = UiHost.Run(() =>
        {
            L.Use("de");
            var source = new DemoSource();
            var history = new History();
            var settings = new Settings { Language = "de", Columns = 2, Density = density, Width = 620 };
            var kinds = new[] { WidgetKind.Cpu, WidgetKind.Gpu, WidgetKind.Memory, WidgetKind.Drives, WidgetKind.Network };
            foreach (var w in settings.Widgets) { w.Visible = kinds.Contains(w.Kind); if (Settings.HasStyles(w.Kind)) w.Style = WidgetStyle.Dial; }
            var view = new SidebarView(settings, Palette.For(true, false));
            var window = new Window { Content = view, Width = 620, Height = 1025 };
            window.Show();
            var positions = new List<string>();
            for (int i = 0; i < 25; i++)
            {
                var snap = source.Read();
                history.Record(snap);
                view.Update(snap with { Gpus = snap.Gpus.Take(1).ToArray() }, history);
                for (int k = 0; k < 4; k++) { Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame(); }
                var cards = view.GetLogicalDescendants().OfType<DialTile>().ToList();
                positions.Add(string.Join(";", cards.Select(c => $"{c.TranslatePoint(default, view)!.Value.X:0}/{c.TranslatePoint(default, view)!.Value.Y:0}/{c.Bounds.Height:0}")));
            }
            var columns = view.GetLogicalDescendants().OfType<DialTile>()
                .GroupBy(c => Math.Round(c.TranslatePoint(default, view)!.Value.X))
                .Select(g => g.Max(c => c.TranslatePoint(new Point(0, c.Bounds.Height), view)!.Value.Y)).ToList();
            dbg.AddRange(positions);
            window.Close();
            int changes = positions.Skip(5).Zip(positions.Skip(6), (a, b) => a == b ? 0 : 1).Sum();
            return (changes, columns);
        });
        Assert.True(moves == 0, string.Join("\n", dbg));
        Assert.Equal(2, bottoms.Count);
        Assert.True(Math.Abs(bottoms[0] - bottoms[1]) <= 2.0, $"bottoms {bottoms[0]:0.0} / {bottoms[1]:0.0}\n" + dbg.Last());
    }
}
