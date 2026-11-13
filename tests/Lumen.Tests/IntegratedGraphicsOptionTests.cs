using System.Reflection;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Lumen.App;
using Lumen.Core;
using Xunit;

public class IntegratedGraphicsOptionTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Fact]
    public void Integrated_graphics_is_opt_in_and_saved_choices_are_preserved()
    {
        Assert.False(new Settings().ShowIntegratedGpu);
        string file = Path.Combine(Path.GetTempPath(), "lumen-integrated-" + Guid.NewGuid() + ".json");
        try
        {
            File.WriteAllText(file, "{}");
            Assert.False(Settings.Load(file).ShowIntegratedGpu);
            foreach (bool selected in new[] { true, false })
            {
                new Settings { ShowIntegratedGpu = selected }.Save(file);
                Assert.Equal(selected, Settings.Load(file).ShowIntegratedGpu);
            }
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void Checkbox_belongs_to_processor_sensor_values_and_not_sensor_settings() => UiHost.Run(() =>
    {
        L.Use("en");
        var (snap, _) = Render.Demo();
        var settings = Settings.Starter();
        var page = typeof(SettingsWindow).GetField("pageIndex", BindingFlags.Static | BindingFlags.NonPublic)!;
        page.SetValue(null, 0);
        int changes = 0;
        var window = new SettingsWindow(settings, Palette.For(true, false), () => changes++, () => new[] { "Demo" }, new TestPlatform(), snapshot: () => snap);
        var build = typeof(SettingsWindow).GetMethod("Build", Private)!;
        Control Content() => (Control)typeof(SettingsWindow).GetField("content", Private)!.GetValue(window)!;
        CheckBox Choice() => Content().GetLogicalDescendants().OfType<CheckBox>().Single(c => c.Name == "sensor-integrated-graphics");
        try
        {
            var expanded = (HashSet<WidgetKind>)typeof(SettingsWindow).GetField("expanded", Private)!.GetValue(window)!;
            expanded.Add(WidgetKind.Gpu);
            build.Invoke(window, null);
            Assert.DoesNotContain(Content().GetLogicalDescendants().OfType<CheckBox>(), c => c.Name == "sensor-integrated-graphics");
            expanded.Add(WidgetKind.Cpu);
            build.Invoke(window, null);
            Assert.False(Choice().IsChecked);
            Assert.Single(GpuGroup.Visible(snap, settings));
            var cpu = settings.Widgets.Single(w => w.Kind == WidgetKind.Cpu);
            var gpu = settings.Widgets.Single(w => w.Kind == WidgetKind.Gpu);
            cpu.Name = "My CPU"; gpu.Style = WidgetStyle.PureBar;
            Choice().IsChecked = true;
            Assert.True(settings.ShowIntegratedGpu);
            Assert.Contains(GpuGroup.Visible(snap, settings), g => g.Integrated);
            Assert.True(changes > 0);
            Assert.True(Choice().IsChecked);
            Choice().IsChecked = false;
            Assert.DoesNotContain(GpuGroup.Visible(snap, settings), g => g.Integrated);
            Assert.Equal("My CPU", cpu.Name);
            Assert.Equal(WidgetStyle.PureBar, gpu.Style);
            var properties = (Dictionary<int, List<string>>)typeof(SettingsWindow).GetField("pageProps", Private)!.GetValue(window)!;
            Assert.Contains("ShowIntegratedGpu", properties[0]);
            Assert.DoesNotContain("ShowIntegratedGpu", properties.GetValueOrDefault(3) ?? new());
            page.SetValue(null, 3);
            build.Invoke(window, null);
            Assert.DoesNotContain(Content().GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Integrated graphics");
            Assert.DoesNotContain(Content().GetLogicalDescendants().OfType<CheckBox>(), c => c.Name == "sensor-integrated-graphics");
        }
        finally { page.SetValue(null, 0); window.Close(); }
        return true;
    });

    internal sealed class TestPlatform : Lumen.App.Platform
    {
        public override bool IsAdministrator => false;
        public override bool AutostartEnabled => false;
    }
}
