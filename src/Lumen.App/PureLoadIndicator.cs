using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

// The same secondary reading as Pure's ring, rendered in three quieter forms.
public sealed class PureLoadIndicator : Border
{
    public static readonly WidgetStyle[] Styles = { WidgetStyle.PureBar, WidgetStyle.PureNumber, WidgetStyle.PureSegments };
    private readonly TextBlock number;
    private readonly FillBack? bar;
    private readonly PureSegmentTrack? segments;
    public string ValueText => number.Text ?? "";
    public double? Fraction { get; private set; }

    public PureLoadIndicator(WidgetStyle style, Palette p)
    {
        Width = 120;
        MinHeight = 58;
        number = new ReservedNumberText(Ui.Text("—", 24, p.Ink, FontWeight.DemiBold, numbers: true), Format.Percent(100));
        var label = Ui.Text(L.T("Load"), 12.5, p.Soft);
        if (style == WidgetStyle.PureNumber)
        {
            CornerRadius = new CornerRadius(10);
            Background = Palette.Brush(p.DesignTile("#2a2a2d"));
            BorderBrush = Palette.Brush(p.Stroke);
            BorderThickness = new Thickness(1);
            Padding = new Thickness(8, 6);
            label.VerticalAlignment = VerticalAlignment.Center;
            Child = Ui.Row(4, number, label);
        }
        else
        {
            var stack = Ui.Stack(3, number, label);
            if (style == WidgetStyle.PureBar)
            {
                bar = new FillBack { Height = 5, Radius = 3, Track = p.Track };
                stack.Children.Add(bar);
            }
            else
            {
                segments = new PureSegmentTrack { Height = 7, Tag = "graphic" };
                stack.Children.Add(segments);
            }
            Child = stack;
        }
    }

    public void Set(double? fraction, Color accent, Color track)
    {
        Fraction = fraction is double value && double.IsFinite(value) ? Math.Clamp(value, 0, 1) : null;
        number.Text = Fraction is double current ? Format.Percent(current * 100) : "—";
        ToolTip.SetTip(this, L.T("Load") + ": " + ValueText);
        bar?.Animate(Fraction ?? 0, accent);
        if (bar != null) bar.Track = track;
        segments?.Set(Fraction ?? 0, accent, track);
    }
}

// Ten equal segments; partial fills preserve small loads such as 1% or 6%.
public sealed class PureSegmentTrack : Control
{
    public double Fraction { get; private set; }
    private Color accent, track;

    public void Set(double fraction, Color accent, Color track)
    {
        Fraction = double.IsFinite(fraction) ? Math.Clamp(fraction, 0, 1) : 0;
        this.accent = accent;
        this.track = track;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (Bounds.Width <= 18 || Bounds.Height <= 0) return;
        const int count = 10;
        const double gap = 2;
        double width = (Bounds.Width - gap * (count - 1)) / count;
        for (int i = 0; i < count; i++)
        {
            var rect = new RoundedRect(new Rect(i * (width + gap), 0, width, Bounds.Height), 2);
            context.DrawRectangle(Palette.Brush(track), null, rect);
            double fill = Math.Clamp(Fraction * count - i, 0, 1);
            if (fill <= 0) continue;
            using (context.PushClip(rect))
                context.DrawRectangle(Palette.Brush(accent), null, new Rect(rect.Rect.X, 0, width * fill, Bounds.Height));
        }
    }
}
