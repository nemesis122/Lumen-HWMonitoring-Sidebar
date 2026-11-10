using System.Reflection;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Lumen.App;
using Lumen.Core;
using Xunit;

public class UserSensorPresetTests
{
    private static readonly Dictionary<WidgetKind, string[]> Enabled = new()
    {
        [WidgetKind.Cpu] = new[] { "cpu.temp", "cpu.load", "cpu.clock", "cpu.power", "cpu.voltage", "cpu.fan" },
        [WidgetKind.Gpu] = new[] { "gpu.temp", "gpu.load", "gpu.chart", "gpu.vram", "gpu.power", "gpu.hotspot", "gpu.fan", "gpu.temp.history", "gpu.load.history", "gpu.clock", "gpu.vram.used", "gpu.vram.total" },
        [WidgetKind.Memory] = new[] { "mem.load", "mem.free", "mem.bar", "mem.total", "mem.used" },
        [WidgetKind.Drives] = new[] { "drives.temp", "drives.model", "drives.bar", "drives.io", "drives.temps", "drives.read", "drives.write" },
        [WidgetKind.Network] = new[] { "net.down", "net.up", "net.link", "net.chart", "net.down.history", "net.up.history" }
    };
    public static IEnumerable<object[]> Kinds() => Enabled.Keys.Select(k => new object[] { k });
    private sealed class TestPlatform : Lumen.App.Platform { public override bool IsAdministrator => false; public override bool AutostartEnabled => false; }

    [Theory, MemberData(nameof(Kinds))]
    public void New_settings_and_recommended_checkbox_selection_match_the_screenshots(WidgetKind kind) => UiHost.Run(() =>
    {
        var (snapshot, _) = Render.Demo(); var settings = new Settings();
        var options = WidgetSensors.Options(kind, null);
        foreach (var option in options) Assert.Equal(Enabled[kind].Contains(option.Key), settings.ForWidget(kind).Shows(option.Key));
        Assert.False(settings.ShowIntegratedGpu);
        var untouched = new[] { WidgetKind.WorldClocks, WidgetKind.Weather, WidgetKind.Calendar }.ToDictionary(k => k, k => JsonSerializer.Serialize(settings.Widgets.Single(w => w.Kind == k), Settings.Json));
        var slot = settings.Widgets.Single(w => w.Kind == kind);
        slot.HiddenSensors = Enabled[kind].ToList(); settings.ShowIntegratedGpu = true;
        using var source = new DemoSource();
        var window = new SettingsWindow(settings, Palette.For(true, false), () => Settings.Normalize(settings), () => new[] { "Demo" }, new TestPlatform(), snapshot: () => snapshot);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            typeof(SettingsWindow).GetField("pageIndex", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, 0);
            ((HashSet<WidgetKind>)typeof(SettingsWindow).GetField("expanded", flags)!.GetValue(window)!).Add(kind);
            typeof(SettingsWindow).GetMethod("Build", flags)!.Invoke(window, null);
            StackPanel Content() => (StackPanel)typeof(SettingsWindow).GetField("content", flags)!.GetValue(window)!;
            Content().GetLogicalDescendants().OfType<Button>().Single(b => b.Name == "sensor-recommended-" + kind).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            foreach (var option in options)
            {
                Assert.Equal(Enabled[kind].Contains(option.Key), settings.ForWidget(kind).Shows(option.Key));
                Assert.Equal(Enabled[kind].Contains(option.Key), Content().GetLogicalDescendants().OfType<CheckBox>().Single(c => Equals(c.Tag, option.Key)).IsChecked);
            }
            if (kind == WidgetKind.Cpu) Assert.False(settings.ShowIntegratedGpu);
            foreach (var pair in untouched) Assert.Equal(pair.Value, JsonSerializer.Serialize(settings.Widgets.Single(w => w.Kind == pair.Key), Settings.Json));
            string optional = SensorDefaults.For(kind).First();
            WidgetSensors.Select(settings, slot, optional, true, snapshot);
            var loaded = Settings.Normalize(JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(settings, Settings.Json), Settings.Json)!);
            Assert.True(loaded.ForWidget(kind).Shows(optional));
        }
        finally { window.Close(); }
        return true;
    });
}
