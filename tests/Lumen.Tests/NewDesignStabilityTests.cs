using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Lumen.App;
using Lumen.Core;
using Xunit;

public class NewDesignStabilityTests
{
    public static IEnumerable<object[]> Designs() => FavoriteTile.Styles.Concat(PureLoadIndicator.Styles).Append(WidgetStyle.PureWidgetsRight).Select(s => new object[] { s });

    [Theory]
    [MemberData(nameof(Designs))]
    public void Changing_digits_units_and_peak_does_not_move_widgets(WidgetStyle style) => UiHost.Run(() =>
    {
        L.Use("de");
        var problems = new List<string>();
        foreach (bool dark in new[] { true, false })
        foreach (double width in new[] { 280d, 375d })
        foreach (var kind in new[] { WidgetKind.Cpu, WidgetKind.Gpu, WidgetKind.Memory, WidgetKind.Network, WidgetKind.Drives, WidgetKind.Cooling, WidgetKind.Power })
        {
            var (demo, _) = Render.Demo();
            var history = new History();
            var settings = new Settings { Language = "de" };
            var card = Cards.Create(kind, style, 0, Palette.For(dark, false), Metrics.For(Density.Fit), settings);
            var marker = new Border { Height = 2, Background = Brushes.Red };
            var stack = new StackPanel { Children = { card, marker } };
            var window = new Window { Width = width, Height = 1500, Content = stack };
            window.Show();
            double? top = null, height = null;
            double? unitX = null, loadLabelX = null;
            int tick = 0;
            foreach (var point in new[] { (9d, 0d, 900d), (99d, 1130d, 999_000d), (100d, 9999d, 13_000_000d), (9d, 0d, 900d) })
            {
                var snapshot = demo with
                {
                    Time = demo.Time.AddSeconds(tick++),
                    Cpu = demo.Cpu! with { Temperature = point.Item1, Load = point.Item1, FanRpm = point.Item2 },
                    Gpus = demo.Gpus.Select(g => g with { Temperature = point.Item1, Load = point.Item1, FanRpm = point.Item2 }).ToArray(),
                    Memory = new MemoryInfo(point.Item1, 128),
                    Network = demo.Network! with { DownloadBytesPerSecond = point.Item3, UploadBytesPerSecond = point.Item3 / 2 },
                    Disks = demo.Disks.Select(d => d with { Temperature = point.Item1, ReadBytesPerSecond = point.Item3, WriteBytesPerSecond = point.Item3 / 2 }).ToArray()
                };
                history.Record(snapshot);
                ((IWidget)card).Update(snapshot, history);
                for (int pass = 0; pass < 3; pass++) { Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame(); }
                string context = $"{kind} {width}px {(dark ? "dark" : "light")} tick {tick}";
                if (top.HasValue && Math.Abs(marker.Bounds.Y - top.Value) > .1) problems.Add(context + $": next widget moved {top:0.##} → {marker.Bounds.Y:0.##}");
                if (height.HasValue && Math.Abs(card.Bounds.Height - height.Value) > .1) problems.Add(context + $": height changed {height:0.##} → {card.Bounds.Height:0.##}");
                top ??= marker.Bounds.Y; height ??= card.Bounds.Height;
                var texts = card.GetLogicalDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).ToArray();
                if (kind is WidgetKind.Cpu or WidgetKind.Gpu)
                {
                    var unit = texts.FirstOrDefault(t => t.Text == "°C");
                    if (unit?.TranslatePoint(default, card) is Point location)
                    {
                        if (unitX.HasValue && Math.Abs(location.X - unitX.Value) > .1) problems.Add(context + ": temperature unit moved horizontally");
                        unitX ??= location.X;
                    }
                    var indicator = card.GetLogicalDescendants().OfType<PureLoadIndicator>().SingleOrDefault();
                    var label = indicator?.GetLogicalDescendants().OfType<TextBlock>().Single(t => t.Text == L.T("Load"));
                    if (label?.TranslatePoint(default, card) is Point labelLocation)
                    {
                        if (loadLabelX.HasValue && Math.Abs(labelLocation.X - loadLabelX.Value) > .1) problems.Add(context + ": load label moved horizontally");
                        loadLabelX ??= labelLocation.X;
                    }
                }
            }
            window.Close();
        }
        Assert.True(problems.Count == 0, style + "\n" + string.Join("\n", problems.Take(30)));
        return true;
    });

    [Theory]
    [MemberData(nameof(Designs))]
    public void Network_peak_stays_visible_even_when_equal_to_download(WidgetStyle style) => UiHost.Run(() =>
    {
        L.Use("en");
        var (demo, _) = Render.Demo();
        var history = new History();
        var card = Cards.Create(WidgetKind.Network, style, 0, Palette.For(true, false), Metrics.For(Density.Fit), new Settings { Language = "en" });
        var window = new Window { Content = card, Width = 320, Height = 1000 };
        window.Show();
        int tick = 0;
        foreach (double down in new[] { 1024d, 1024d * 1024, 900d, 13d * 1024 * 1024 })
        {
            var snap = demo with { Time = demo.Time.AddSeconds(tick++), Network = demo.Network! with { DownloadBytesPerSecond = down } };
            history.Record(snap); ((IWidget)card).Update(snap, history);
            for (int pass = 0; pass < 3; pass++) { Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame(); }
            var peak = card.GetLogicalDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Text is string text && (text == "2-min peak" || text.StartsWith("DL peak")));
            Assert.NotNull(peak);
            Assert.True(peak!.IsEffectivelyVisible, style + ": peak label missing");
            Assert.DoesNotContain(peak.GetLogicalAncestors().OfType<Control>(), c => c.Opacity == 0 || !c.IsVisible);
        }
        window.Close(); return true;
    });

    [Theory]
    [MemberData(nameof(Designs))]
    public void Network_peak_expires_without_hiding_or_moving_its_row(WidgetStyle style) => UiHost.Run(() =>
    {
        L.Use("en");
        var (demo, _) = Render.Demo();
        var history = new History();
        var card = Cards.Create(WidgetKind.Network, style, 0, Palette.For(true, false), Metrics.For(Density.Fit), new Settings { Language = "en" });
        var window = new Window { Content = card, Width = 320, Height = 1000 };
        window.Show();
        double? rowTop = null, cardHeight = null;
        for (int tick = 0; tick <= History.Capacity + 1; tick++)
        {
            double down = tick == 0 ? 13d * 1024 * 1024 : 900;
            var snap = demo with { Time = demo.Time.AddSeconds(tick), Network = demo.Network! with { DownloadBytesPerSecond = down } };
            history.Record(snap); ((IWidget)card).Update(snap, history);
            if (tick is not (0 or 1) && tick < History.Capacity - 1) continue;
            for (int pass = 0; pass < 3; pass++) { Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame(); }
            var peak = card.GetLogicalDescendants().OfType<TextBlock>().First(t => t.Text is string text && (text == "2-min peak" || text.StartsWith("DL peak")));
            double y = peak.TranslatePoint(default, card)!.Value.Y;
            Assert.DoesNotContain(peak.GetLogicalAncestors().OfType<Control>(), c => c.Opacity == 0 || !c.IsVisible);
            Assert.True(rowTop == null || Math.Abs(y - rowTop.Value) < .1, style + ": peak row moved when old peak expired");
            Assert.True(cardHeight == null || Math.Abs(card.Bounds.Height - cardHeight.Value) < .1, style + ": card height changed when old peak expired");
            string expected = Format.Rate(tick < History.Capacity ? 13d * 1024 * 1024 : 900);
            Assert.Contains(((Control)peak.Parent!).GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == expected);
            rowTop ??= y; cardHeight ??= card.Bounds.Height;
        }
        window.Close(); return true;
    });
}
