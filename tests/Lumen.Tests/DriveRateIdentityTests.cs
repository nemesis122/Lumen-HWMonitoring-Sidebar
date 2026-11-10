using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Lumen.App;
using Lumen.Core;
using Xunit;

public class DriveRateIdentityTests
{
    public static IEnumerable<object[]> Designs() => Settings.Selectable.Select(s => new object[] { s });

    [Theory, MemberData(nameof(Designs))]
    public void Read_write_are_shown_once_per_device_without_an_aggregate_pair(WidgetStyle style) => UiHost.Run(() =>
    {
        L.Use("en"); var (demo, history) = Render.Demo();
        var settings = new Settings(); var slot = settings.Widgets.Single(w => w.Kind == WidgetKind.Drives);
        foreach (bool all in new[] { false, true })
        {
            if (all) slot.HiddenSensors.Clear();
            var card = Cards.Create(WidgetKind.Drives, style, 0, Palette.For(true, false), Metrics.For(Density.Fit), settings);
            var window = new Window { Content = card, Width = 375, Height = 2000 };
            try
            {
                window.Show();
                foreach (double rate in new[] { 0d, 1024d, 13_000_000_000d })
                {
                    var snap = demo with { Disks = demo.Disks.Select(d => d with { ReadBytesPerSecond = rate, WriteBytesPerSecond = rate }).ToArray() };
                    ((IWidget)card).Update(snap, history);
                    Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame();
                    var values = card.GetLogicalDescendants().OfType<TextBlock>().Where(t => SensorDetails.Visible(t, card)
                        && t.FontFeatures != null && (t.Text ?? "").Any(char.IsDigit)).ToArray();
                    string Key(TextBlock t) => new[] { SensorDetails.KeyOf(t) }.Concat(t.GetLogicalAncestors().OfType<Control>().Select(SensorDetails.KeyOf))
                        .FirstOrDefault(k => k.StartsWith("drive:")) ?? "";
                    foreach (var disk in snap.Disks)
                    foreach (var suffix in new[] { ":read", ":write" })
                        Assert.Single(values.Where(t => Key(t) == "drive:" + disk.Id + suffix));
                    // A displayed transfer rate must belong to a particular drive, including zero rates.
                    Assert.All(values.Where(t => (t.Text ?? "").EndsWith("B/s")), t => Assert.True(Key(t).EndsWith(":read") || Key(t).EndsWith(":write"), $"{style}: unassigned rate {t.Text}"));
                    Assert.DoesNotContain(Summaries.Extras(WidgetKind.Drives, 0, snap, history, settings), r => r.Label == L.T("Read") || r.Label == L.T("Write"));
                }
            }
            finally { window.Close(); }
        }
        return true;
    }, seconds: 120);
}
