using System.Reflection;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Lumen.App;
using Lumen.Core;
using Xunit;

public class StyleSelectionTests
{
    static readonly WidgetStyle[] Expected =
    {
        WidgetStyle.Standard, WidgetStyle.Segments, WidgetStyle.Table,
        WidgetStyle.Pill, WidgetStyle.FillBars, WidgetStyle.Split, WidgetStyle.PillRow,
        WidgetStyle.KeyFigures, WidgetStyle.BadgeList, WidgetStyle.TwinFocus,
        WidgetStyle.Duplex, WidgetStyle.SignalRows, WidgetStyle.OpenPanel, WidgetStyle.Clarity, WidgetStyle.ControlDeck, WidgetStyle.ClarityColor, WidgetStyle.ClarityRows, WidgetStyle.ClarityTiles, WidgetStyle.Evolution, WidgetStyle.Dial, WidgetStyle.HistoryGraph, WidgetStyle.RingPair, WidgetStyle.LevelColumns, WidgetStyle.NeonHud, WidgetStyle.NeonTiles, WidgetStyle.Thermostat, WidgetStyle.SmartPanel, WidgetStyle.PurpleDash, WidgetStyle.SciFi, WidgetStyle.PurpleList, WidgetStyle.SegmentGraph, WidgetStyle.TableGraph, WidgetStyle.FillBarsGraph, WidgetStyle.PillRowGraph, WidgetStyle.BadgeListGraph, WidgetStyle.NeonHudGraph, WidgetStyle.Pure, WidgetStyle.PureWidgets,
        WidgetStyle.CleanDark,
        WidgetStyle.CompactPro,
        WidgetStyle.PerformanceView, WidgetStyle.PureBar, WidgetStyle.PureNumber, WidgetStyle.PureSegments, WidgetStyle.PureWidgetsRight,
        WidgetStyle.Meridian, WidgetStyle.Strata, WidgetStyle.Vector, WidgetStyle.Orbit, WidgetStyle.Lucent, WidgetStyle.Circuit,
        WidgetStyle.Panorama, WidgetStyle.Terminal, WidgetStyle.Thermal, WidgetStyle.MosaicPro, WidgetStyle.RibbonFlow, WidgetStyle.SplitLens,
        WidgetStyle.Aperture, WidgetStyle.PixelDeck, WidgetStyle.HorizonSync
    };
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Fact]
    public void RemovedStylesMigrateWithoutChangingWidgetPreferences()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lumen-designs-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            foreach (var style in Enum.GetValues<WidgetStyle>().Append((WidgetStyle)999))
            {
                var settings = new Settings { Pages = 3, Notes = "keep notes", Width = 350 };
                var cpu = settings.Widgets.Single(w => w.Kind == WidgetKind.Cpu);
                cpu.Style = style; cpu.Page = 3; cpu.Column = 2; cpu.Name = "My CPU"; cpu.Visible = false;
                var file = Path.Combine(directory, "settings.json");
                settings.Save(file);
                var loaded = Settings.Load(file);
                var restored = loaded.Widgets.Single(w => w.Kind == WidgetKind.Cpu);
                Assert.Equal(Expected.Contains(style) ? style : WidgetStyle.Standard, restored.Style);
                Assert.Equal(3, restored.Page); Assert.Equal(2, restored.Column);
                Assert.Equal("My CPU", restored.Name); Assert.False(restored.Visible);
                Assert.Equal("keep notes", loaded.Notes); Assert.Equal(350, loaded.Width);
                loaded.Save(file);
                Assert.Equal(restored.Style, Settings.Load(file).Widgets.Single(w => w.Kind == WidgetKind.Cpu).Style);
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    // "Style for all widgets" with a register chosen: only the widgets of that register get the design.
    [Fact]
    public void StyleGridCanTargetOneRegister() => UiHost.Run(() =>
    {
        L.Use("en");
        typeof(SettingsWindow).GetField("pageIndex", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, 0);
        var register = typeof(SettingsWindow).GetField("styleRegister", BindingFlags.Static | BindingFlags.NonPublic)!;
        var settings = Settings.Starter();
        var window = new SettingsWindow(settings, Palette.For(true, false), () => Settings.Normalize(settings), () => new[] { "Demo" }, new TestPlatform());
        try
        {
            register.SetValue(null, 2);
            var parts = (Control[])typeof(SettingsWindow).GetMethod("WidgetsPage", Private)!.Invoke(window, null)!;
            var buttons = parts.SelectMany(c => c.GetLogicalDescendants().Prepend(c)).OfType<UniformGrid>().Single().Children.OfType<Button>().ToList();
            buttons[Array.IndexOf(Expected, WidgetStyle.SciFi)].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var info = settings.Widgets.Where(w => Settings.HasStyles(w.Kind) && settings.EffectivePage(w) == 2).ToList();
            var hw = settings.Widgets.Where(w => Settings.HasStyles(w.Kind) && settings.EffectivePage(w) == 1).ToList();
            Assert.NotEmpty(info);
            Assert.All(info, w => Assert.Equal(WidgetStyle.SciFi, w.Style));
            Assert.All(hw, w => Assert.NotEqual(WidgetStyle.SciFi, w.Style));
        }
        finally { register.SetValue(null, 0); window.Close(); }
        return true;
    });

    [Fact]
    public void GlobalAndPerWidgetPickersOfferExactlyTheRetainedStyles() => UiHost.Run(() =>
    {
        L.Use("en");
        typeof(SettingsWindow).GetField("pageIndex", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, 0);
        var settings = new Settings();
        var window = new SettingsWindow(settings, Palette.For(true, false), () => Settings.Normalize(settings), () => new[] { "Demo" }, new TestPlatform());
        try
        {
            Assert.Equal(Expected, Settings.Selectable);
            var parts = (Control[])typeof(SettingsWindow).GetMethod("WidgetsPage", Private)!.Invoke(window, null)!;
            // One grid lists every design once, in order, without groups or preview.
            var buttons = parts.SelectMany(c => c.GetLogicalDescendants().Prepend(c)).OfType<UniformGrid>().Single().Children.OfType<Button>().ToList();
            string Name(Button button) => button.GetLogicalDescendants().OfType<TextBlock>().Single().Text!;
            Assert.Equal(Expected.Select(SettingsWindow.StyleName), buttons.Select(Name));
            buttons.Last().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.All(settings.Widgets.Where(w => Settings.HasStyles(w.Kind)), w => Assert.Equal(WidgetStyle.HorizonSync, w.Style));

            ((HashSet<WidgetKind>)typeof(SettingsWindow).GetField("expanded", Private)!.GetValue(window)!).Add(WidgetKind.Cpu);
            typeof(SettingsWindow).GetMethod("Build", Private)!.Invoke(window, null);
            var content = (StackPanel)typeof(SettingsWindow).GetField("content", Private)!.GetValue(window)!;
            var choices = content.GetLogicalDescendants().OfType<WrapPanel>()
                .Single(p => p.Children.Count == Expected.Length && p.Children.All(c => c is Button));
            Assert.Equal(Expected.Select(SettingsWindow.StyleName), choices.Children.OfType<Button>().Select(Name));
            choices.Children.OfType<Button>().Single(b => Name(b) == "Capsule").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(WidgetStyle.Pill, settings.Widgets.Single(w => w.Kind == WidgetKind.Cpu).Style);
            Assert.Equal(WidgetStyle.HorizonSync, settings.Widgets.Single(w => w.Kind == WidgetKind.Gpu).Style);
        }
        finally { window.Close(); }
        return true;
    });

    [Fact]
    public void RemovedRenderersFallBackToStandard() => UiHost.Run(() =>
    {
        var settings = new Settings();
        var palette = Palette.For(true, false);
        foreach (var style in Settings.Retired)
            Assert.IsType<CpuCard>(Cards.Create(WidgetKind.Cpu, style, 0, palette, Metrics.For(Density.Fit), settings));
        return true;
    });

    sealed class TestPlatform : Lumen.App.Platform
    {
        public override bool IsAdministrator => false;
        public override bool AutostartEnabled => false;
    }
}
