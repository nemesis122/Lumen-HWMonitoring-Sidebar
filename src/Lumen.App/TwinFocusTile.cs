using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed class TwinFocusTile : SummaryTile
{
    private readonly TextBlock label, hardware, firstLabel, secondLabel;
    private readonly ContentControl glyph = new() { Width = 18, Height = 18 };
    private readonly BoldValue value, secondValue;
    private readonly WaveChart firstPlot, secondPlot;
    private readonly StackPanel secondColumn;
    private string glyphKey = "";

    public TwinFocusTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s)
        : base(kind, index, p, m, s, false)
    {
        Padding = new Thickness(16, 12);
        CornerRadius = new CornerRadius(18);
        ClipToBounds = true;
        label = Ui.Text("", 13, p.Ink, FontWeight.Bold);
        hardware = Ui.Text("", 11.5, p.Soft);
        HardwareWidgetTitle.Register(this, label, hardware);
        label.MaxLines = hardware.MaxLines = 1;
        double size = m.Subtitles ? 44 : 40;
        value = new BoldValue(size, p);
        secondValue = new BoldValue(size, p);
        var number = SteadyHeight.Of(new Viewbox { Child = value, StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = HorizontalAlignment.Stretch });
        var secondNumber = SteadyHeight.Of(new Viewbox { Child = secondValue, StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = HorizontalAlignment.Stretch });
        firstLabel = Ui.Text("", 11.5, p.Soft);
        secondLabel = Ui.Text("", 11.5, p.Soft);
        firstPlot = new WaveChart { Theme = p, Height = 28, Tag = "fixed", IsHitTestVisible = false };
        secondPlot = new WaveChart { Theme = p, Height = 28, Tag = "fixed", IsHitTestVisible = false };
        var first = Ui.Stack(2, firstLabel, number, firstPlot);
        WidgetPresentation.Sensor(first, WidgetSensors.PrimaryKey(kind));
        first.Margin = new Thickness(0, 0, 10, 0);
        var second = Ui.Stack(2, secondLabel, secondNumber, secondPlot);
        secondColumn = second;
        WidgetPresentation.Sensor(second, WidgetSensors.SecondaryKey(kind));
        second.Margin = new Thickness(10, 0, 0, 0);
        var divider = new Border { Width = 1, Background = Palette.Brush(p.Track), Margin = new Thickness(0, 6) };
        var root = Ui.Stack(8, Ui.Columns("*,Auto", label, glyph), Ui.Columns("*,1,*", first, divider, second), hardware, Extras);
        // Height left over when the sidebar fills the screen goes between these parts, not below them.
        root.Tag = "elastic";
        Child = root;
    }

    protected override void Show(Summary sum)
    {
        var accent = Shade(sum);
        label.Text = Naming.Title(Slot, Index, sum.Title);
        hardware.Text = Naming.Hardware(Slot, Index, sum.Name);
        hardware.IsVisible = Naming.ShowHardware(Slot, S) && hardware.Text.Length > 0;
        ToolTip.SetTip(hardware, hardware.Text);
        value.Set(sum.Value, sum.Unit, accent, sum.Color == P.Ramp[^1]);
        BindPrimary(value);
        var pair = TwinFocusMetrics.Pair(Kind, Index, sum, Last.Snap, Last.History, S);
        secondColumn.IsVisible = pair.Secondary.Value.Length > 0 && pair.Secondary.Value != "—" && S.Shows(WidgetSensors.SecondaryKey(Kind));
        firstLabel.Text = pair.Primary.Label;
        secondLabel.Text = pair.Secondary.Label;
        secondValue.Set(pair.Secondary.Value, pair.Secondary.Unit, P.Cool, false);
        firstPlot.Set(pair.Primary.Series, pair.Primary.Maximum, pair.Primary.Level ?? 0, accent);
        secondPlot.Set(pair.Secondary.Series, pair.Secondary.Maximum, pair.Secondary.Level ?? 0, P.Cool);
        bool showHistory = Summaries.ChartKey(Kind) is not string historyKey || S.Shows(historyKey);
        firstPlot.IsVisible = showHistory && S.Shows(WidgetSensors.PrimaryKey(Kind) + ".history") && (pair.Primary.Series.Any(double.IsFinite) || pair.Primary.Level.HasValue);
        secondPlot.IsVisible = showHistory && S.Shows(WidgetSensors.SecondaryKey(Kind) + ".history") && (pair.Secondary.Series.Any(double.IsFinite) || pair.Secondary.Level.HasValue);
        ToolTip.SetTip(secondValue, pair.Secondary.Label + ": " + pair.Secondary.Value + " " + pair.Secondary.Unit);
        var next = sum.Glyph + accent;
        if (next != glyphKey)
        {
            glyphKey = next;
            glyph.Content = Ui.Glyph(sum.Glyph, accent, 18);
        }
    }
}
