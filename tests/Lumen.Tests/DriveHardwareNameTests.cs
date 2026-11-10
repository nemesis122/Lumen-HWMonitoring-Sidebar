using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Lumen.App;
using Lumen.Core;
using Xunit;

public class DriveHardwareNameTests
{
    public static IEnumerable<object[]> Designs() => Settings.Selectable.Select(style => new object[] { style });

    [Theory, MemberData(nameof(Designs))]
    public void Hardware_show_switch_hides_drive_names_immediately(WidgetStyle style) => UiHost.Run(() =>
    {
        var (snap, history) = Render.Demo(); var settings = new Settings();
        var slot = settings.Widgets.Single(w => w.Kind == WidgetKind.Drives);
        slot.HardwareName = "OwnStorage_XQ";
        var card = Cards.Create(WidgetKind.Drives, style, 0, Palette.For(true, false), Metrics.For(Density.Fit), settings);
        foreach (bool shown in new[] { true, false, true, false })
        {
            slot.ShowHardware = shown;
            ((IWidget)card).Update(snap, history);
            var title = HardwareWidgetTitle.TitleOf(card)!;
            Assert.Equal(shown, (title.Text ?? "").Contains("OwnStorage_XQ", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(shown, SensorDetails.VisibleKeys(card).Any(k => k.EndsWith(":model")));
            foreach (var disk in snap.Disks)
                Assert.Equal(shown, (ToolTip.GetTip(title)?.ToString() ?? "").Contains(disk.Name));
            foreach (var disk in snap.Disks)
            { Assert.Contains("drive:" + disk.Id + ":read", SensorDetails.VisibleKeys(card)); Assert.Contains("drive:" + disk.Id + ":write", SensorDetails.VisibleKeys(card)); }
        }
        settings.ShowNames = false; slot.ShowHardware = true;
        var hidden = Cards.Create(WidgetKind.Drives, style, 0, Palette.For(true, false), Metrics.For(Density.Fit), settings);
        ((IWidget)hidden).Update(snap, history);
        Assert.DoesNotContain(SensorDetails.VisibleKeys(hidden), k => k.EndsWith(":model"));
        return true;
    });

    [Theory]
    [InlineData("Samsung SSD 990 PRO 4TB", "990 PRO 4 TB")]
    [InlineData("Samsung SSD 990 PRO 2TB", "990 PRO 2 TB")]
    [InlineData("WDC WD40EZAZ 4TB", "WD40EZAZ 4 TB")]
    [InlineData("Unknown Manufacturer Super Long Drive Model 999 8TB", null)]
    public void Drive_title_shortening_keeps_model_and_capacity(string input, string? expected)
    {
        string result = Names.CompactDrive(input);
        if (expected != null) Assert.Equal(expected, result);
        Assert.True(result.Length <= 24);
        Assert.False(string.IsNullOrWhiteSpace(result));
    }

    [Theory, MemberData(nameof(Designs))]
    public void Drive_models_follow_the_designs_hardware_name_side(WidgetStyle style) => UiHost.Run(() =>
    {
        var (snap, history) = Render.Demo();
        foreach (bool dark in new[] { true, false })
        foreach (double width in new[] { 280d, 375d })
        {
            var settings = new Settings();
            var card = Cards.Create(WidgetKind.Drives, style, 0, Palette.For(dark, false), Metrics.For(Density.Fit), settings);
            ((IWidget)card).Update(snap, history);
            var window = new Window { Content = card, Width = width, Height = 1800 };
            try
            {
                window.Show(); Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame();
                foreach (var disk in snap.Disks)
                {
                    string key = "drive:" + disk.Id + ":model";
                    var name = Assert.IsType<TextBlock>(HardwareWidgetTitle.TitleOf(card));
                    Assert.Contains(key, SensorDetails.KeyOf(name).Split('|'));
                    Assert.Contains(Names.CompactDrive(disk.Name).ToUpperInvariant(), (name.Text ?? "").ToUpperInvariant());
                    Assert.Contains(disk.Name, ToolTip.GetTip(name)?.ToString());
                    Assert.Equal(snap.Disks.Count + 1, name.TextLayout.TextLines.Count);
                    var lines = name.Text!.Split('\n');
                    Assert.Equal(snap.Disks.Count + 1, lines.Length);
                    Assert.Equal(TextWrapping.NoWrap, name.TextWrapping);
                    for (int i = 0; i < snap.Disks.Count; i++)
                        Assert.StartsWith(snap.Disks[i].Letters.ToUpperInvariant(), lines[i + 1].ToUpperInvariant());
                    Assert.True(name.Bounds.Width > 0 && name.Bounds.Height > 0);
                    var firstRate = card.GetLogicalDescendants().OfType<TextBlock>().First(t =>
                        SensorDetails.Visible(t, card) && SensorDetails.KeyOf(t).EndsWith(":read"));
                    var nameEnd = name.TranslatePoint(new Point(0, name.Bounds.Height), card)!.Value.Y;
                    Assert.True(nameEnd <= firstRate.TranslatePoint(default, card)!.Value.Y + 1, $"{style}: hardware name is below the readings");
                }
            }
            finally { window.Close(); }
        }
        return true;
    });

    [Theory, MemberData(nameof(Designs))]
    public void Model_sensor_can_still_be_hidden_without_hiding_transfer_rates(WidgetStyle style) => UiHost.Run(() =>
    {
        var (snap, history) = Render.Demo(); var settings = new Settings();
        settings.Widgets.Single(w => w.Kind == WidgetKind.Drives).HiddenSensors.Add("drives.model");
        var card = Cards.Create(WidgetKind.Drives, style, 0, Palette.For(true, false), Metrics.For(Density.Fit), settings);
        ((IWidget)card).Update(snap, history);
        var keys = SensorDetails.VisibleKeys(card);
        Assert.DoesNotContain(keys, key => key.EndsWith(":model"));
        foreach (var disk in snap.Disks)
        { Assert.Contains("drive:" + disk.Id + ":read", keys); Assert.Contains("drive:" + disk.Id + ":write", keys); }
        return true;
    });
}
