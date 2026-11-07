using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Lumen.App;
using Lumen.Core;

static class Cardshots
{
    public static void Run(string output, Snapshot snapshot, History history, IEnumerable<(WidgetStyle Style, WidgetKind Kind)> pairs)
    {
        foreach (var (style, kind) in pairs)
        {
            var s = new Settings { Language = "de" };
            double Env(string n) => double.TryParse(Environment.GetEnvironmentVariable(n), System.Globalization.CultureInfo.InvariantCulture, out double v) ? v : 1.0;
            foreach (var w in s.Widgets) { w.GraphicScale = Env("GS"); w.ValueScale = Env("VS"); w.LabelScale = Env("LS"); w.HeroScale = Env("HS"); w.WidgetScale = Env("WS"); }
            var p = Palette.For(true, false);
            var card = Cards.Create(kind, style, 0, p, Metrics.For(Density.Fit), s);
            ((IWidget)card).Update(snapshot, history);
            var window = new Window { Content = new Border { Padding = new Thickness(10), Child = card }, Width = 340, SizeToContent = SizeToContent.Height, Background = Palette.Brush(p.Surface) };
            window.Show();
            for (int i = 0; i < 4; i++) { Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame(); }
            using var image = window.CaptureRenderedFrame()!;
            image.Save(Path.Combine(output, $"card-{style}-{kind}.png"));
            window.Close();
        }
    }
}
