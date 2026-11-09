using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Lumen.App;

// Reserve a representative numeric field before the first update. Units and neighbouring labels
// therefore keep their position even on the first 9 → 100 transition, not just after seeing 100 once.
public sealed class ReservedNumberText : TextBlock
{
    private readonly TextBlock probe = new();
    private readonly string sample;
    protected override Type StyleKeyOverride => typeof(TextBlock);

    public ReservedNumberText(TextBlock model, string sample)
    {
        this.sample = sample;
        Text = model.Text;
        FontFamily = model.FontFamily;
        FontStretch = model.FontStretch;
        FontSize = model.FontSize;
        FontWeight = model.FontWeight;
        FontFeatures = model.FontFeatures;
        Foreground = model.Foreground;
        VerticalAlignment = model.VerticalAlignment;
        TextWrapping = TextWrapping.NoWrap;
        TextTrimming = TextTrimming.None;
        TextAlignment = TextAlignment.Right;
    }

    protected override Size MeasureOverride(Size available)
    {
        probe.Text = sample;
        probe.FontFamily = FontFamily;
        probe.FontSize = FontSize;
        probe.FontWeight = FontWeight;
        probe.FontStretch = FontStretch;
        probe.FontFeatures = FontFeatures;
        probe.LetterSpacing = LetterSpacing;
        probe.Measure(Size.Infinity);
        var size = base.MeasureOverride(available);
        return new Size(Math.Max(size.Width, probe.DesiredSize.Width), Math.Max(size.Height, probe.DesiredSize.Height));
    }
}
