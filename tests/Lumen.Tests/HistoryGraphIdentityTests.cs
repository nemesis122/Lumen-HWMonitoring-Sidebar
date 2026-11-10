using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Lumen.App;
using Lumen.Core;
using Xunit;

public class HistoryGraphIdentityTests
{
    [Theory]
    [InlineData(WidgetKind.Network, WidgetStyle.HistoryGraph)]
    [InlineData(WidgetKind.Cpu, WidgetStyle.HistoryGraph)]
    [InlineData(WidgetKind.Gpu, WidgetStyle.HistoryGraph)]
    [InlineData(WidgetKind.Network, WidgetStyle.Evolution)]
    [InlineData(WidgetKind.Network, WidgetStyle.Dial)]
    [InlineData(WidgetKind.Network, WidgetStyle.Pill)]
    public void Main_and_secondary_readings_appear_once_including_equal_and_zero_values(WidgetKind kind, WidgetStyle style) => UiHost.Run(() =>
    {
        var (snapshot, history) = Render.Demo(); var settings = new Settings();
        var slot = settings.Widgets.Single(w => w.Kind == kind); slot.HiddenSensors.Clear();
        var card = Cards.Create(kind, style, 0, Palette.For(true, false), Metrics.For(Density.Fit), settings);
        var window = new Window { Content = card, Width = 320, Height = 1200 };
        try
        {
            window.Show();
            foreach (double rate in new[] { 0d, 1024d, 8192d })
            {
                var sample = snapshot with { Network = snapshot.Network! with { DownloadBytesPerSecond = rate, UploadBytesPerSecond = rate } };
                history.Record(sample); ((IWidget)card).Update(sample, history); Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame();
                foreach (string key in new[] { WidgetSensors.PrimaryKey(kind), WidgetSensors.SecondaryKey(kind) })
                    Assert.Single(card.GetLogicalDescendants().OfType<TextBlock>(), t => SensorDetails.KeyOf(t) == key && SensorDetails.Visible(t, card) && (t.Text ?? "").Any(char.IsDigit));
            }
            WidgetSensors.Select(settings, slot, WidgetSensors.SecondaryKey(kind), false, snapshot);
            ((IWidget)card).Update(snapshot, history);
            Assert.DoesNotContain(SensorDetails.VisibleKeys(card), key => key == WidgetSensors.SecondaryKey(kind));
        }
        finally { window.Close(); }
        return true;
    });
}
