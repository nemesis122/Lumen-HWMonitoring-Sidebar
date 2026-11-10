using Avalonia;
using Avalonia.Controls;

namespace Lumen.App;

// Split Lens aligns the complete surfaces, even when one side has more sensor rows.
public sealed class EqualPairPanel : Panel
{
    private readonly double minimumWidth;
    public EqualPairPanel(Control[] children, double minimumWidth)
    {
        this.minimumWidth = minimumWidth;
        Children.AddRange(children);
    }
    private int Columns(double width) => Children.Count > 1 && width >= minimumWidth * 2 ? 2 : 1;
    protected override Size MeasureOverride(Size available)
    {
        double width = double.IsFinite(available.Width) ? available.Width : minimumWidth * Math.Min(2, Children.Count);
        int columns = Columns(width);
        double height = 0;
        foreach (var child in Children) { child.Measure(new Size(width / columns, double.PositiveInfinity)); height = Math.Max(height, child.DesiredSize.Height); }
        return new Size(width, height * Math.Ceiling(Children.Count / (double)columns));
    }
    protected override Size ArrangeOverride(Size final)
    {
        int columns = Columns(final.Width);
        double rows = Math.Max(1, Math.Ceiling(Children.Count / (double)columns));
        for (int i = 0; i < Children.Count; i++)
            Children[i].Arrange(new Rect(i % columns * final.Width / columns, i / columns * final.Height / rows, final.Width / columns, final.Height / rows));
        return final;
    }
}
