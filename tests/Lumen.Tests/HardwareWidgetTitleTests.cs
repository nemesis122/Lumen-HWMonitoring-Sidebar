using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Headless;
using Avalonia.Threading;
using Lumen.App;
using Lumen.Core;
using Xunit;

public class HardwareWidgetTitleTests
{
    public static IEnumerable<object[]> Designs() => Settings.Selectable.Select(s => new object[] { s });
    [Theory, MemberData(nameof(Designs))]
    public void All_hardware_widgets_integrate_names_in_the_actual_title(WidgetStyle style) => UiHost.Run(() =>
    {
        var (snap, history) = Render.Demo();
        foreach (var kind in new[] { WidgetKind.Cpu, WidgetKind.Gpu, WidgetKind.Memory, WidgetKind.Network, WidgetKind.Cooling, WidgetKind.Drives })
        {
            var settings = new Settings(); var slot = settings.Widgets.Single(w => w.Kind == kind);
            slot.Name = "Title_ZQ"; slot.HardwareName = "Hardware_XY";
            var card = Cards.Create(kind, style, 0, Palette.For(true, false), Metrics.For(Density.Fit), settings);
            ((INamed)card).Own(slot, 0); ((IWidget)card).Update(snap, history);
            var title = Assert.IsType<TextBlock>(HardwareWidgetTitle.TitleOf(card));
            Assert.Contains("TITLE_ZQ", title.Text!.ToUpperInvariant());
            Assert.Contains("HARDWARE_XY", title.Text.ToUpperInvariant());
            Assert.Single(card.GetLogicalDescendants().OfType<TextBlock>().Where(t => SensorDetails.Visible(t, card) && (t.Text ?? "").ToUpperInvariant().Contains("HARDWARE_XY")));
        }
        return true;
    });

    [Theory, MemberData(nameof(Designs))]
    public void Read_write_use_the_normal_value_colour(WidgetStyle style) => UiHost.Run(() =>
    {
        var (snap, history) = Render.Demo();
        foreach (bool dark in new[] { true, false })
        {
            var palette = Palette.For(dark, false); var settings = new Settings();
            var card = Cards.Create(WidgetKind.Drives, style, 0, palette, Metrics.For(Density.Fit), settings);
            ((IWidget)card).Update(snap, history);
            var window = new Window { Content = card, Width = 375, Height = 1800 };
            window.Show(); Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame();
            var values = card.GetLogicalDescendants().OfType<TextBlock>().Where(t => SensorDetails.Visible(t, card)
                && (SensorDetails.KeyOf(t).EndsWith(":read") || SensorDetails.KeyOf(t).EndsWith(":write")));
            Assert.NotEmpty(values);
            foreach (var value in values) Assert.Equal(palette.Ink, Assert.IsAssignableFrom<ISolidColorBrush>(value.Foreground).Color);
            window.Close();
        }
        return true;
    });
}
