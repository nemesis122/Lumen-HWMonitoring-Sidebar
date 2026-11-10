using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Lumen.App;
using Lumen.Core;
using Xunit;

public class DriveTransferPairTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Neon_read_write_use_the_same_boxes_as_cpu_temperature(bool dark) => UiHost.Run(() =>
    {
        var (snap, history) = Render.Demo(); var palette = Palette.For(dark, false); var settings = new Settings();
        var cpu = Cards.Create(WidgetKind.Cpu, WidgetStyle.NeonTiles, 0, palette, Metrics.For(Density.Fit), settings);
        var drives = Cards.Create(WidgetKind.Drives, WidgetStyle.NeonTiles, 0, palette, Metrics.For(Density.Fit), settings);
        ((IWidget)cpu).Update(snap, history); ((IWidget)drives).Update(snap, history);
        var temperature = cpu.GetLogicalDescendants().OfType<TextBlock>().Single(t => SensorDetails.KeyOf(t) == "cpu.temp");
        var prototype = temperature.GetLogicalAncestors().OfType<Border>().First(b => b.MinHeight == 62);
        var pair = Assert.Single(drives.GetLogicalDescendants().OfType<DriveTransferPairs>());
        foreach (var value in pair.GetLogicalDescendants().OfType<TextBlock>().Where(t => SensorDetails.KeyOf(t).StartsWith("drive:")))
        {
            var tile = value.GetLogicalAncestors().OfType<Border>().First(b => b.MinHeight == 62);
            Assert.Equal(prototype.CornerRadius, tile.CornerRadius); Assert.Equal(prototype.Padding, tile.Padding);
            Assert.Equal(prototype.BorderThickness, tile.BorderThickness);
            Assert.Equal(((Avalonia.Media.ISolidColorBrush)prototype.Background!).Color, ((Avalonia.Media.ISolidColorBrush)tile.Background!).Color);
            Assert.Equal(((Avalonia.Media.ISolidColorBrush)prototype.BorderBrush!).Color, ((Avalonia.Media.ISolidColorBrush)tile.BorderBrush!).Color);
            Assert.Equal(temperature.FontSize, value.FontSize); Assert.Equal(temperature.FontWeight, value.FontWeight);
        }
        return true;
    });

    public static IEnumerable<object[]> Designs() => DriveTransferPairs.Styles.Select(s => new object[] { s });
    [Theory, MemberData(nameof(Designs))]
    public void Transfers_are_a_single_pair_without_bars_and_follow_the_requested_orientation(WidgetStyle style) => UiHost.Run(() =>
    {
        var (demo, history) = Render.Demo();
        foreach (bool dark in new[] { true, false })
        foreach (double width in new[] { 280d, 375d })
        {
            var settings = new Settings();
            var card = Cards.Create(WidgetKind.Drives, style, 0, Palette.For(dark, false), Metrics.For(Density.Fit), settings);
            var window = new Window { Content = card, Width = width, Height = 2000 };
            try
            {
                window.Show(); ((IWidget)card).Update(demo, history); Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame();
                var pairs = Assert.Single(card.GetLogicalDescendants().OfType<DriveTransferPairs>());
                var graphics = pairs.GetLogicalDescendants().Where(c => c is Meter or FillBack or GradientBar or SegmentBar or AreaChart or RingBar).ToArray();
                if (style is WidgetStyle.SegmentGraph or WidgetStyle.TableGraph)
                {
                    Assert.Equal(demo.Disks.Count * 2, graphics.Length);
                    Assert.All(graphics, graphic => Assert.Equal(style == WidgetStyle.SegmentGraph ? 12 : 5, Assert.IsType<Meter>(graphic).Height));
                }
                else if (style == WidgetStyle.FillBarsGraph)
                {
                    Assert.Equal(demo.Disks.Count * 2, graphics.Length);
                    Assert.All(graphics, graphic => Assert.Equal(10, Assert.IsType<FillBack>(graphic).Radius));
                }
                else if (style is WidgetStyle.PillRowGraph or WidgetStyle.BadgeListGraph)
                {
                    Assert.Equal(demo.Disks.Count * 2, graphics.Length);
                    Assert.All(graphics, graphic => Assert.Equal(style == WidgetStyle.PillRowGraph ? ((PillRowTile)card).CapsuleHeight / 2 : 2.5, Assert.IsType<FillBack>(graphic).Radius));
                }
                else if (style == WidgetStyle.NeonHudGraph)
                {
                    Assert.Equal(demo.Disks.Count * 2, graphics.Length);
                    Assert.All(graphics, graphic => Assert.Equal(4, Assert.IsType<GradientBar>(graphic).Height));
                }
                else Assert.Empty(graphics);
                foreach (var disk in demo.Disks)
                {
                    var read = pairs.GetLogicalDescendants().OfType<TextBlock>().Single(t => SensorDetails.KeyOf(t) == "drive:" + disk.Id + ":read");
                    var write = pairs.GetLogicalDescendants().OfType<TextBlock>().Single(t => SensorDetails.KeyOf(t) == "drive:" + disk.Id + ":write");
                    var a = read.TranslatePoint(default, pairs)!.Value; var b = write.TranslatePoint(default, pairs)!.Value;
                    Assert.InRange(Math.Abs(a.Y - b.Y), 0, 1); Assert.True(b.X > a.X);
                    if (style == WidgetStyle.RingPair) Assert.Empty(pairs.GetLogicalDescendants().OfType<DialArc>());
                }
                // Optional SMART and capacity fields must also stay above the transfer footer.
                settings.Widgets.Single(w => w.Kind == WidgetKind.Drives).HiddenSensors.Clear();
                ((IWidget)card).Update(demo, history); Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame();
                double pairTop = pairs.TranslatePoint(default, card)!.Value.Y;
                foreach (var metric in card.GetLogicalDescendants().OfType<TextBlock>().Where(t => SensorDetails.Visible(t, card)
                    && SensorDetails.KeyOf(t).Length > 0 && !t.GetLogicalAncestors().Contains(pairs)))
                    Assert.True(metric.TranslatePoint(new Point(0, metric.Bounds.Height), card)!.Value.Y <= pairTop + 1,
                        $"{style}: {SensorDetails.KeyOf(metric)} is below the read/write footer");
                settings.Widgets.Single(w => w.Kind == WidgetKind.Drives).HiddenSensors.Add("drives.write");
                ((IWidget)card).Update(demo, history);
                Assert.DoesNotContain(SensorDetails.VisibleKeys(card), key => key.EndsWith(":write"));
                Assert.Contains(SensorDetails.VisibleKeys(card), key => key.EndsWith(":read"));
            }
            finally { window.Close(); }
        }
        return true;
    });
}
