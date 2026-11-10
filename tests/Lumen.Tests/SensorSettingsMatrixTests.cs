using System.Text.Json;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.LogicalTree;
using Lumen.App;
using Lumen.Core;
using Xunit;

public class SensorSettingsMatrixTests
{
    private sealed class TestPlatform : Lumen.App.Platform
    {
        public override bool IsAdministrator => false;
        public override bool AutostartEnabled => false;
    }
    public static IEnumerable<object[]> Designs() => Settings.Selectable.Select(style => new object[] { style });
    private static string[] Texts(Control card) => card.GetLogicalDescendants().OfType<TextBlock>()
        .Where(t => SensorDetails.Visible(t, card)).Select(t => (t.Text ?? "").ToUpperInvariant()).ToArray();

    [Theory, MemberData(nameof(Designs))]
    public void Last_selected_sensor_is_reachable_in_a_flat_bar_without_shrinking_text(WidgetStyle style) => UiHost.Run(() =>
    {
        var (snap, history) = Render.Demo();
        var settings = new Settings { Edge = Edge.Top, BarHeight = 240 };
        settings.Widgets.Single(w => w.Kind == WidgetKind.Drives).HiddenSensors.Clear();
        var card = Cards.Create(WidgetKind.Drives, style, 0, Palette.For(true, false), Metrics.For(Density.Fit), settings);
        ((IWidget)card).Update(snap, history);
        var window = new Window { Content = card, Width = 300, Height = 240 };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame();
            string key = "drive:" + snap.Disks.Last().Id + ":life";
            var target = card.GetLogicalDescendants().OfType<Control>().First(c => SensorDetails.KeyOf(c).Split('|').Contains(key) && SensorDetails.Visible(c, card));
            for (int pass = 0; pass < 3; pass++) { target.BringIntoView(); Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame(); }
            var bottom = target.TranslatePoint(new Point(0, target.Bounds.Height), window)!.Value;
            var top = target.TranslatePoint(default, window)!.Value;
            Assert.True(top.Y >= -1 && bottom.Y <= window.Bounds.Height + 1, $"{style}: selected value cannot be scrolled into view ({top.Y:0}–{bottom.Y:0})");
        }
        finally { window.Close(); }
        return true;
    });

    [Fact]
    public void Recommended_button_and_group_checkboxes_apply_real_settings() => UiHost.Run(() =>
    {
        var settings = new Settings(); var diskSlot = settings.Widgets.Single(w => w.Kind == WidgetKind.Drives);
        diskSlot.HiddenSensors.Clear(); L.Use("de");
        var window = new SettingsWindow(settings, Palette.For(true, false), () => Settings.Normalize(settings), () => new[] { "Demo" }, new TestPlatform(), snapshot: () => new DemoSource().Read());
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            typeof(SettingsWindow).GetField("pageIndex", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, 0);
            ((HashSet<WidgetKind>)typeof(SettingsWindow).GetField("expanded", flags)!.GetValue(window)!).Add(WidgetKind.Drives);
            void Build() => typeof(SettingsWindow).GetMethod("Build", flags)!.Invoke(window, null);
            StackPanel Content() => (StackPanel)typeof(SettingsWindow).GetField("content", flags)!.GetValue(window)!;
            CheckBox Check(string key) => Content().GetLogicalDescendants().OfType<CheckBox>().Single(c => Equals(c.Tag, key));
            Build();
            var recommended = Content().GetLogicalDescendants().OfType<Button>().Single(b => b.Name == "sensor-recommended-Drives");
            recommended.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.False(settings.ForWidget(WidgetKind.Drives).Shows("drives.hours"));
            Check("drives.io").IsChecked = false;
            Assert.False(settings.ForWidget(WidgetKind.Drives).Shows("drives.read"));
            Check("drives.read").IsChecked = true;
            Assert.True(Check("drives.io").IsChecked);
            Assert.False(Check("drives.write").IsChecked);
            Check("drives.hours").IsChecked = true;
            Assert.True(settings.ForWidget(WidgetKind.Drives).Shows("drives.hours"));
            if (Environment.GetEnvironmentVariable("LUMEN_SENSOR_SCREENSHOT") is string path)
            {
                // Deliver the recommended starting state, rather than the interaction test's opt-in.
                Content().GetLogicalDescendants().OfType<Button>().Single(b => b.Name == "sensor-recommended-Drives")
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                window.Width = 1120; window.Height = 1000; window.Show(); Dispatcher.UIThread.RunJobs();
                using var frame = window.CaptureRenderedFrame()!; frame.Save(path);
            }
        }
        finally { window.Close(); L.Use("en"); }
        return true;
    });

    [Theory, MemberData(nameof(Designs))]
    public void Custom_titles_and_hardware_names_can_be_shown_and_hidden(WidgetStyle style) => UiHost.Run(() =>
    {
        L.Use("en"); var (snap, history) = Render.Demo();
        foreach (var kind in new[] { WidgetKind.Cpu, WidgetKind.Gpu, WidgetKind.Memory, WidgetKind.Network })
        foreach (int mode in new[] { 0, 1, 2, 3 })
        {
            var settings = new Settings { ShowNames = mode != 3 };
            var slot = settings.Widgets.Single(w => w.Kind == kind);
            slot.Name = "MyTitle_ZX"; slot.HardwareName = "MyHardware_QV";
            slot.ShowTitle = mode != 1; slot.ShowHardware = mode != 2;
            var card = Cards.Create(kind, style, 0, Palette.For(true, false), Metrics.For(Density.Fit), settings);
            ((Lumen.App.INamed)card).Own(slot, 0); ((IWidget)card).Update(snap, history);
            var texts = Texts(card);
            Assert.True(texts.Any(t => t.Contains("MYTITLE_ZX")) == slot.ShowTitle, $"{style}/{kind}/{mode}: custom title");
            Assert.True(texts.Any(t => t.Contains("MYHARDWARE_QV")) == (slot.ShowHardware && settings.ShowNames), $"{style}/{kind}/{mode}: custom hardware name");
        }
        return true;
    });

    [Theory, MemberData(nameof(Designs))]
    public void Selected_details_survive_units_scaling_orientation_and_saved_settings(WidgetStyle style) => UiHost.Run(() =>
    {
        var (snap, history) = Render.Demo();
        foreach (var edge in Enum.GetValues<Edge>())
        foreach (bool dark in new[] { true, false })
        {
            var settings = new Settings { Edge = edge, Fahrenheit = true, Font = "mono", Background = "ocean", SensorSelectionVersion = SensorDefaults.SelectionVersion };
            var slot = settings.Widgets.Single(w => w.Kind == WidgetKind.Drives);
            slot.Style = style; slot.HiddenSensors.Clear(); slot.HiddenSensors.Add("drives.write");
            slot.Name = "My drives"; slot.HardwareName = "Storage"; slot.Accent = "#28B89B";
            slot.GraphicScale = .7; slot.LabelScale = 1.2; slot.ValueScale = 1.3; slot.HeroScale = 1.1;
            settings = Settings.Normalize(JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(settings, Settings.Json), Settings.Json)!);
            Ui.UseFont(settings.Font);
            var card = Cards.Create(WidgetKind.Drives, style, 0, Palette.For(settings, dark, null), Metrics.For(Density.Fit), settings);
            ((IWidget)card).Update(snap, history);
            card.Measure(new Size(375, double.PositiveInfinity)); card.Arrange(new Rect(0, 0, 375, card.DesiredSize.Height));
            var keys = SensorDetails.VisibleKeys(card);
            foreach (var disk in snap.Disks)
            {
                Assert.Contains("drive:" + disk.Id + ":hours", keys);
                Assert.DoesNotContain("drive:" + disk.Id + ":write", keys);
            }
            var valueControls = card.GetLogicalDescendants().OfType<TextBlock>().Where(t => SensorDetails.Visible(t, card) && SensorDetails.KeyOf(t).EndsWith(":temp")).ToArray();
            Assert.NotEmpty(valueControls);
            // The main number can split its unit into another text control; both are still present.
            Assert.Contains(card.GetLogicalDescendants().OfType<TextBlock>(), t => SensorDetails.Visible(t, card) && (t.Text ?? "").Contains("°F"));
            Assert.True(double.IsFinite(card.DesiredSize.Height));
        }
        Ui.UseFont("inter");
        return true;
    });

    [Theory, MemberData(nameof(Designs))]
    public void Core_bars_remain_available_even_in_designs_without_a_native_core_panel(WidgetStyle style) => UiHost.Run(() =>
    {
        var (snap, history) = Render.Demo();
        var settings = new Settings(); var slot = settings.Widgets.Single(w => w.Kind == WidgetKind.Cpu);
        slot.HiddenSensors = WidgetSensors.Options(WidgetKind.Cpu, snap).Select(o => o.Key).Where(k => k != "cpu.cores").ToList();
        var card = Cards.Create(WidgetKind.Cpu, style, 0, Palette.For(true, false), Metrics.For(Density.Fit), settings);
        ((IWidget)card).Update(snap, history);
        Assert.Contains(card.GetLogicalDescendants().OfType<Control>(), c => SensorDetails.Visible(c, card) && (c is Skyline or CoreBars || c.GetType().Name == "ThreadBars"));
        return true;
    });
}
