using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Lumen.App;
using Lumen.Core;
using Xunit;

public class WidgetOptionsTests
{
    static bool Visible(Control control) => control.IsVisible && control.GetLogicalAncestors().OfType<Control>().All(c => c.IsVisible);
    static readonly WidgetKind[] Kinds = Enum.GetValues<WidgetKind>().Where(Settings.HasStyles).ToArray();

    [Fact]
    public void EveryWidgetCanHideAllSensorReadoutsInEveryRetainedDesign() => UiHost.Run(() =>
    {
        L.Use("en");
        var (snapshot, history) = Render.Demo();
        var palette = Palette.For(true, false);
        var failures = new List<string>();
        foreach (var style in Settings.Selectable)
        foreach (var kind in Kinds)
        {
            var settings = new Settings();
            var slot = settings.Widgets.Single(w => w.Kind == kind);
            slot.Style = style;
            slot.HiddenSensors = WidgetSensors.Options(kind, snapshot).Select(o => o.Key).ToList();
            var card = Cards.Create(kind, style, 0, palette, Metrics.For(Density.Fit), settings);
            ((IWidget)card).Update(snapshot, history);
            var visibleNumbers = card.GetLogicalDescendants().OfType<TextBlock>()
                .Where(t => t.FontFeatures != null && Visible(t) && !string.IsNullOrWhiteSpace(t.Text)).Select(t => t.Text).ToArray();
            if (visibleNumbers.Length > 0) failures.Add($"{style}/{kind}: {string.Join(", ", visibleNumbers)}");
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
        return true;
    });

    [Fact]
    public void IndependentScalesApplyToDynamicReadoutsWithoutAccumulating() => UiHost.Run(() =>
    {
        var (snapshot, history) = Render.Demo();
        var settings = new Settings();
        var slot = settings.Widgets.Single(w => w.Kind == WidgetKind.Cpu);
        slot.Style = WidgetStyle.Table;
        slot.ValueScale = 2; slot.GraphicScale = 0.5;
        var card = Cards.Create(WidgetKind.Cpu, slot.Style, 0, Palette.For(true, false), Metrics.For(Density.Fit), settings);
        ((IWidget)card).Update(snapshot, history);
        var value = card.GetLogicalDescendants().OfType<TextBlock>().First(t => t.FontFeatures != null && t.FontSize == 25);
        var bar = card.GetLogicalDescendants().OfType<Meter>().First();
        Assert.Equal(2.5, bar.Height);
        for (int i = 0; i < 4; i++) ((IWidget)card).Update(snapshot, history);
        Assert.Equal(25, value.FontSize); Assert.Equal(2.5, bar.Height);
        Assert.Equal(1, settings.Widgets.Single(w => w.Kind == WidgetKind.Gpu).ValueScale);
        slot.ValueScale = 0.5; slot.GraphicScale = 2;
        ((IWidget)card).Update(snapshot, history);
        Assert.Equal(6.25, value.FontSize); Assert.Equal(10, bar.Height);
        return true;
    });

    [Fact]
    public void HistoryCanBeHiddenWithoutHidingLiveValues() => UiHost.Run(() =>
    {
        var (snapshot, history) = Render.Demo();
        var settings = new Settings();
        settings.Widgets.Single(w => w.Kind == WidgetKind.Cpu).HiddenSensors.Add("cpu.chart");
        var card = Cards.Create(WidgetKind.Cpu, WidgetStyle.TwinFocus, 0, Palette.For(true, false), Metrics.For(Density.Fit), settings);
        ((IWidget)card).Update(snapshot, history);
        Assert.All(card.GetLogicalDescendants().OfType<WaveChart>(), graph => Assert.False(graph.IsVisible));
        Assert.Contains(card.GetLogicalDescendants().OfType<TextBlock>(), text => text.FontFeatures != null && Visible(text) && text.Text!.Any(char.IsDigit));
        Assert.True(settings.ForWidget(WidgetKind.Gpu).Shows("cpu.chart"));
        return true;
    });

    [Fact]
    public void ScalesAndSensorSelectionsSurviveRestartAndNormalizeInvalidInput()
    {
        var settings = new Settings();
        var cpu = settings.Widgets.Single(w => w.Kind == WidgetKind.Cpu);
        cpu.GraphicScale = 3; cpu.ValueScale = 0; cpu.HiddenSensors = new() { "cpu.temp", "cpu.chart", "cpu.temp" };
        Settings.Normalize(settings);
        Assert.Equal(2, cpu.GraphicScale); Assert.Equal(.5, cpu.ValueScale); Assert.Equal(2, cpu.HiddenSensors.Count);
        var file = Path.Combine(Path.GetTempPath(), "lumen-options-" + Guid.NewGuid() + ".json");
        try
        {
            settings.Save(file);
            var back = Settings.Load(file).Widgets.Single(w => w.Kind == WidgetKind.Cpu);
            Assert.Equal(cpu.HiddenSensors, back.HiddenSensors); Assert.Equal(2, back.GraphicScale); Assert.Equal(.5, back.ValueScale);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void ExtremePerWidgetScalesSettleInHorizontalAndVerticalLayouts() => UiHost.Run(() =>
    {
        var (snapshot, history) = Render.Demo();
        foreach (var style in Settings.Selectable)
        foreach (var edge in new[] { Edge.Right, Edge.Top })
        {
            var settings = new Settings { Edge = edge, Glass = false, BarHeight = 300,
                Widgets = new() {
                    new() { Kind = WidgetKind.Cpu, Style = style, GraphicScale = .5, ValueScale = 2 },
                    new() { Kind = WidgetKind.Gpu, Style = style, GraphicScale = 2, ValueScale = .5 },
                    new() { Kind = WidgetKind.Memory, Style = style, GraphicScale = 2, ValueScale = 2 }
                } };
            var (window, view) = Render.Show(settings, edge == Edge.Top ? 1600 : 440, edge == Edge.Top ? 360 : 1200);
            try
            {
                while (UiHost.LayoutWarnings.TryDequeue(out _)) { }
                for (int i = 0; i < 4; i++) { view.Update(snapshot, history); Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame(); }
                int layouts = 0; window.LayoutUpdated += (_, _) => layouts++;
                for (int i = 0; i < 3; i++) { Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame(); }
                Assert.Equal(0, layouts);
                Assert.Empty(UiHost.LayoutWarnings);
            }
            finally { window.Close(); }
        }
        return true;
    });

    [Fact]
    public void IndividualDetectedSensorsAreListedAndFilterOnlyTheirWidget()
    {
        using var demo = new DemoSource();
        var snapshot = demo.Read();
        var settings = new Settings();
        var cpu = settings.Widgets.Single(w => w.Kind == WidgetKind.Cpu);
        cpu.HiddenSensors.Add("cpu.core:0");
        var scoped = settings.ForWidget(WidgetKind.Cpu);
        Assert.DoesNotContain(WidgetSensors.Options(WidgetKind.Cpu, snapshot), x => x.Key.StartsWith("cpu.core:"));
        Assert.Equal(snapshot.Cpu!.CoreLoads.Count, WidgetSensors.FilterSnapshot(snapshot, scoped).Cpu!.CoreLoads.Count);
        Assert.Equal(snapshot.Cpu.CoreLoads.Count, WidgetSensors.FilterSnapshot(snapshot, settings.ForWidget(WidgetKind.Gpu)).Cpu!.CoreLoads.Count);
        foreach (var kind in Kinds)
        {
            var keys = WidgetSensors.Options(kind, snapshot).Select(x => x.Key).ToArray();
            Assert.Equal(keys.Length, keys.Distinct().Count());
        }
    }

    // Appearance: the five sizes for all widgets at once.
    [Fact]
    public void AppearanceSizesChangeEveryWidget() => UiHost.Run(() =>
    {
        L.Use("de");
        var settings = new Settings();
        settings.Widgets[0].ValueScale = 1.3;
        var window = new SettingsWindow(settings, Palette.For(true, false), () => Settings.Normalize(settings), () => new[] { "Demo" }, new TestPlatform(), snapshot: () => new DemoSource().Read());
        try
        {
            var section = (Control)typeof(SettingsWindow).GetMethod("AllSizesSection", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null)!;
            var sliders = section.GetLogicalDescendants().OfType<Slider>().ToArray();
            Assert.Equal(5, sliders.Length);
            // Starts at what most widgets have (100 %), not at the one changed widget.
            Assert.Equal(1.0, sliders[3].Value, 3);
            sliders[0].Value = 1.2; sliders[1].Value = 0.8; sliders[2].Value = 1.6; sliders[3].Value = 0.9; sliders[4].Value = 1.1;
            Assert.All(settings.Widgets, w =>
            {
                Assert.Equal(1.2, w.WidgetScale, 3); Assert.Equal(0.8, w.GraphicScale, 3); Assert.Equal(1.6, w.HeroScale, 3);
                Assert.Equal(0.9, w.ValueScale, 3); Assert.Equal(1.1, w.LabelScale, 3);
            });
        }
        finally { window.Close(); L.Use("en"); }
        return true;
    });

    [Fact]
    public void SettingsControlsUpdateOnlyTheChosenWidget() => UiHost.Run(() =>
    {
        L.Use("de");
        var settings = new Settings();
        var window = new SettingsWindow(settings, Palette.For(true, false), () => Settings.Normalize(settings), () => new[] { "Demo" }, new TestPlatform(), snapshot: () => new DemoSource().Read());
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            typeof(SettingsWindow).GetField("pageIndex", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, 0);
            ((HashSet<WidgetKind>)typeof(SettingsWindow).GetField("expanded", flags)!.GetValue(window)!).Add(WidgetKind.Cpu);
            typeof(SettingsWindow).GetMethod("Build", flags)!.Invoke(window, null);
            var content = (StackPanel)typeof(SettingsWindow).GetField("content", flags)!.GetValue(window)!;
            var history = content.GetLogicalDescendants().OfType<CheckBox>().Single(c => Equals(c.Tag, "cpu.chart"));
            history.IsChecked = false;
            var sliders = content.GetLogicalDescendants().OfType<Slider>().ToArray();
            // Widget, graphic, main value, value and label size.
            Assert.Equal(5, sliders.Length);
            Assert.All(sliders, slider => { Assert.Equal(.5, slider.Minimum); Assert.Equal(2, slider.Maximum); });
            sliders[0].Value = 1.25; sliders[1].Value = 1.5; sliders[2].Value = 1.75; sliders[3].Value = .75;
            var cpu = settings.Widgets.Single(w => w.Kind == WidgetKind.Cpu);
            Assert.Contains("cpu.chart", cpu.HiddenSensors); Assert.Equal(1.25, cpu.WidgetScale); Assert.Equal(1.5, cpu.GraphicScale); Assert.Equal(1.75, cpu.HeroScale); Assert.Equal(.75, cpu.ValueScale);
            Assert.Equal(SensorDefaults.For(WidgetKind.Gpu), settings.Widgets.Single(w => w.Kind == WidgetKind.Gpu).HiddenSensors);
            if (Environment.GetEnvironmentVariable("LUMEN_OPTIONS_SCREENSHOT") is string file)
            {
                window.Width = 1120; window.Height = 1200; window.Show(); Dispatcher.UIThread.RunJobs();
                using var frame = window.CaptureRenderedFrame()!; frame.Save(file);
            }
        }
        finally { window.Close(); L.Use("en"); }
        return true;
    });

    sealed class TestPlatform : Lumen.App.Platform
    {
        public override bool IsAdministrator => false;
        public override bool AutostartEnabled => false;
    }
}
