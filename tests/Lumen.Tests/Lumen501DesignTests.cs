using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Lumen.App;
using Lumen.Core;
using Xunit;

/// <summary>Design polish of 5.0.1: short hardware names, idle fans, calm warning outlines, core heatmap.</summary>
public class Lumen501DesignTests
{
    [Theory]
    [InlineData("AMD Ryzen 7 9800X3D 8-Core Processor", "Ryzen 7 9800X3D")]
    [InlineData("AMD Radeon RX 9070 XT", "RX 9070 XT")]
    [InlineData("NVIDIA GeForce RTX 4090", "RTX 4090")]
    [InlineData("13th Gen Intel(R) Core(TM) i7-13700K", "Core i7-13700K")]
    [InlineData("Intel(R) Core(TM) i5-8400 CPU @ 2.80GHz", "Core i5-8400")]
    [InlineData("AMD Radeon(TM) Graphics", "Radeon Graphics")]
    [InlineData("Samsung SSD 990 PRO 2TB", "Samsung SSD 990 PRO 2TB")]
    [InlineData("Pump", "Pump")]
    public void Built_in_names_drop_maker_and_filler_words(string name, string expected) => Assert.Equal(expected, Naming.Short(name));

    private static (Control Card, Settings S) Tiles(WidgetStyle style, Func<Snapshot, Snapshot>? change = null)
    {
        var s = Settings.Starter(); s.HiddenValues.Clear(); DesignWidgetSelection.Apply(s, style); AioDesigns.ShowAllParts(s);
        foreach (var w in s.Widgets) w.HiddenSensors.Clear();
        var overview = s.Widgets.Single(w => w.Kind == WidgetKind.Overview);
        var card = Cards.Create(WidgetKind.Overview, style, 0, Palette.For(true, false), Metrics.For(Density.Comfortable), s.ForSlot(overview));
        ((Lumen.App.INamed)card).Own(overview, 0);
        var (snap, hist) = Render.Demo(); if (change != null) snap = change(snap);
        var window = new Window { Width = 360, Height = 2200, Content = card };
        window.Show();
        for (int i = 0; i < 3; i++) { ((IWidget)card).Update(snap, hist); Dispatcher.UIThread.RunJobs(); }
        return (card, s);
    }

    [Fact]
    public void Aio_designs_show_short_model_names_and_a_core_heatmap() => UiHost.Run(() =>
    {
        var (card, _) = Tiles(WidgetStyle.AioIsometricTiles);
        var texts = card.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Contains("RTX 3080", texts);
        Assert.DoesNotContain(texts, t => t != null && t.StartsWith("NVIDIA GeForce"));
        Assert.True(card.GetLogicalDescendants().OfType<AioCoreBars>().Single().Heatmap);
        return true;
    });

    [Fact]
    public void A_fan_at_zero_rpm_simply_reads_zero() => UiHost.Run(() =>
    {
        // User preference: a stopped fan reads 0 rpm, no "Idle" text.
        var (card, _) = Tiles(WidgetStyle.AioIsometricTiles, snap =>
            snap with { Fans = snap.Fans.Select((f, i) => i == 0 ? f with { Rpm = 0 } : f).ToArray() });
        var texts = card.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.DoesNotContain("Idle", texts);
        Assert.Contains("0", texts);
        return true;
    });

    [Fact]
    public void Red_tiles_use_red_illustrations() => UiHost.Run(() =>
    {
        var (card, _) = Tiles(WidgetStyle.AioIsometricTilesRed);
        var images = card.GetLogicalDescendants().OfType<Image>().Select(i => i.Source).Distinct().Count();
        Assert.True(images > 3);
        return true;
    });
}
