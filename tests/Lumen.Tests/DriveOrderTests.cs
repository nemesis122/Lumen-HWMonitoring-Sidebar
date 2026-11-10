using System.Reflection;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Lumen.App;
using Lumen.Core;
using Xunit;

public class DriveOrderTests
{
    private static DiskInfo[] Disks() => new[] {
        new DiskInfo("g", "SSD G", 40, 14, 100, 200) { Letters = "G:" },
        new DiskInfo("c", "SSD C", 50, 20, 300, 400) { Letters = "C:" },
        new DiskInfo("a", "SSD A", 30, 30, 500, 600) { Letters = "A:" } };

    [Fact]
    public void Ordering_handles_multi_volume_lowercase_and_unmounted_drives_without_changing_identity()
    {
        var disks = Disks().Concat(new[] {
            new DiskInfo("multi", "Multi", null, null, null, null) { Letters = "e: c:" },
            new DiskInfo("none1", "No letter 1", null, null, null, null),
            new DiskInfo("none2", "No letter 2", null, null, null, null) }).ToArray();
        Assert.Equal(new[] { "c", "multi", "a", "g", "none1", "none2" }, DriveOrdering.Order(disks).Select(d => d.Id));
        Assert.Equal(new[] { "a", "c", "multi", "g", "none1", "none2" }, DriveOrdering.Order(disks, "letters").Select(d => d.Id));
        Assert.Equal(disks, DriveOrdering.Order(disks, "detected"));
        Assert.Equal(new[] { "g", "c", "a", "multi", "none1", "none2" }, disks.Select(d => d.Id));
        Assert.All(DriveOrdering.Order(disks), d => Assert.Contains(disks, original => ReferenceEquals(d, original)));
    }

    public static IEnumerable<object[]> Designs() => Settings.Selectable.Select(s => new object[] { s });
    [Theory, MemberData(nameof(Designs))]
    public void Every_design_changes_title_temperature_and_transfer_order_together(WidgetStyle style) => UiHost.Run(() =>
    {
        var (demo, history) = Render.Demo(); var snapshot = demo with { Disks = Disks() };
        var settings = new Settings(); var slot = settings.Widgets.Single(w => w.Kind == WidgetKind.Drives);
        var card = Cards.Create(WidgetKind.Drives, style, 0, Palette.For(true, false), Metrics.For(Density.Fit), settings);
        foreach (string mode in new[] { "system", "letters", "detected", "system" })
        {
            slot.DriveOrder = mode;
            ((IWidget)card).Update(snapshot, history);
            string[] expected = mode == "letters" ? new[] { "a", "c", "g" } : mode == "detected" ? new[] { "g", "c", "a" } : new[] { "c", "a", "g" };
            Assert.Equal(expected.Select(id => "drive:" + id + ":model"), SensorDetails.KeyOf(HardwareWidgetTitle.TitleOf(card)!).Split('|'));
            var temperatures = card.GetLogicalDescendants().OfType<TextBlock>().Where(t => SensorDetails.Visible(t, card) && SensorDetails.KeyOf(t).EndsWith(":temp")).ToArray();
            Assert.True(expected.Select(id => "drive:" + id + ":temp").SequenceEqual(temperatures.Select(SensorDetails.KeyOf)), style + "/" + mode + " " + string.Join("; ", temperatures.Select(t => SensorDetails.KeyOf(t) + "=" + t.Text + " @" + t.Parent?.GetType().Name + " " + string.Join(",", t.GetLogicalAncestors().Take(4).Select(a => a.GetType().Name)))));
            var transfers = Assert.Single(card.GetLogicalDescendants().OfType<DriveTransferPairs>());
            Assert.Equal(expected.Select(id => "drive:" + id + ":read"), transfers.GetLogicalDescendants().OfType<TextBlock>().Select(SensorDetails.KeyOf).Where(k => k.EndsWith(":read")));
        }
        slot.HiddenSensors.Add("drive:c"); ((IWidget)card).Update(snapshot, history);
        Assert.DoesNotContain(SensorDetails.VisibleKeys(card), key => key.StartsWith("drive:c:"));
        return true;
    });

    private sealed class TestPlatform : Lumen.App.Platform { public override bool IsAdministrator => false; public override bool AutostartEnabled => false; }
    [Fact]
    public void The_order_picker_updates_settings_and_survives_a_restart() => UiHost.Run(() =>
    {
        var settings = new Settings();
        var slot = settings.Widgets.Single(w => w.Kind == WidgetKind.Drives);
        Assert.Equal("system", slot.DriveOrder);
        var window = new SettingsWindow(settings, Palette.For(true, false), () => Settings.Normalize(settings), () => new[] { "Demo" }, new TestPlatform());
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            typeof(SettingsWindow).GetField("pageIndex", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, 0);
            ((HashSet<WidgetKind>)typeof(SettingsWindow).GetField("expanded", flags)!.GetValue(window)!).Add(WidgetKind.Drives);
            typeof(SettingsWindow).GetMethod("Build", flags)!.Invoke(window, null);
            var content = (StackPanel)typeof(SettingsWindow).GetField("content", flags)!.GetValue(window)!;
            var picker = content.GetLogicalDescendants().OfType<ComboBox>().Single(c => c.Name == "drive-order");
            foreach (int choice in new[] { 1, 2, 0 })
            {
                picker.SelectedIndex = choice;
                Assert.Equal(DriveOrdering.Modes[choice], slot.DriveOrder);
                var loaded = Settings.Normalize(JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(settings, Settings.Json), Settings.Json)!);
                Assert.Equal(slot.DriveOrder, loaded.Widgets.Single(w => w.Kind == WidgetKind.Drives).DriveOrder);
            }
        }
        finally { window.Close(); }
        return true;
    });
}
