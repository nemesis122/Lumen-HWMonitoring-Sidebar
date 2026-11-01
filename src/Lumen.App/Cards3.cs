using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Lumen.Core;
using static Lumen.App.L;

namespace Lumen.App;

// Frames per second of the game in the foreground: the big number, the 1 % low beside it.
public sealed class FpsCard : Card
{
    private readonly Hero hero; private readonly TextBlock low; private readonly AreaChart? chart;
    private readonly Dictionary<string, Stat> stats; private readonly TextBlock empty; private readonly Control[] live;

    public FpsCard(Palette p, Metrics m, Settings s) : base(p, m, s, Glyphs.Fps, "FPS")
    {
        (hero, low, chart) = HeroBlock(T("1 % low"), "fps.chart");
        if (chart != null) chart.Max = null;
        stats = Stats(("fps.frametime", T("Frame time")));
        empty = Ui.Text(T("No game in the foreground."), 12, p.Soft);
        empty.TextWrapping = TextWrapping.Wrap;
        empty.TextTrimming = TextTrimming.None;
        Body.Children.Add(empty);
        live = Body.Children.Skip(1).Where(c => c != empty).ToArray();
    }

    public static Color Color(double? fps, Palette p) => fps is not double f ? p.Faint : f >= 60 ? p.Calm : f >= 30 ? p.Ramp[2] : p.Ramp[^1];

    public override void Update(Snapshot s, History h)
    {
        var f = s.Fps;
        foreach (var c in live) c.IsVisible = f != null;
        empty.IsVisible = f == null;
        Aside = f?.App ?? "";
        hero.Set(Format.Number(f?.Fps), "fps", Color(f?.Fps, P));
        Motion.Text(low, f?.Low1 is double l ? Format.Number(l) : Format.Missing);
        if (chart != null)
        {
            var series = h.Read("fps");
            chart.Max = Math.Max(60, series.Where(double.IsFinite).DefaultIfEmpty(0).Max() * 1.15);
            chart.Set(series, Color(f?.Fps, P));
        }
        Put(stats, "fps.frametime", Format.Milliseconds(f?.FrameTimeMs));
    }
}
