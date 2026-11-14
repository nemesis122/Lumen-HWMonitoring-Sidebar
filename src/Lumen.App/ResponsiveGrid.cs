using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;

namespace Lumen.App;

// Reflow enlarged measurements from the available measure width, never from arranged bounds.
public class ResponsiveGrid : Grid
{
    private bool adapt, folded;
    public bool Adapt { get => adapt; set { if (adapt != value) { adapt = value; InvalidateMeasure(); } } }
    private GridLength[]? columns, rows;
    private readonly Dictionary<Control, (int Column, int Row, int Span)> positions = new();
    public void RestoreForUpdate()
    {
        if (!folded) return;
        ColumnDefinitions.Clear(); RowDefinitions.Clear();
        foreach (var width in columns!) ColumnDefinitions.Add(new ColumnDefinition(width));
        foreach (var height in rows!) RowDefinitions.Add(new RowDefinition(height));
        foreach (var c in Children) if (positions.TryGetValue(c,out var p)) { SetColumn(c,p.Column); SetRow(c,p.Row); SetColumnSpan(c,p.Span); }
        folded = false;
    }

    protected override Size MeasureOverride(Size available)
    {
        foreach (var old in positions.Keys.Where(c => !Children.Contains(c)).ToArray()) positions.Remove(old);
        if (!folded && ColumnDefinitions.Count is 2 or 3)
        {
            columns = ColumnDefinitions.Select(c => c.Width).ToArray();
            rows = RowDefinitions.Select(r => r.Height).ToArray();
        }
        bool eligible = columns != null && double.IsFinite(available.Width) && available.Width > 0;
        if (eligible)
        {
            foreach (var c in Children)
                if (!folded || !positions.ContainsKey(c)) positions[c] = (GetColumn(c), GetRow(c), GetColumnSpan(c));
            var visible = Children.Where(c => c.IsVisible).ToArray();
            bool numbers = visible.SelectMany(c => c.GetLogicalDescendants().OfType<TextBlock>().Prepend(c as TextBlock).OfType<TextBlock>())
                .Any(t => t.FontFeatures != null && !string.IsNullOrWhiteSpace(t.Text));
            var widths = new double[columns!.Length];
            if (Adapt && numbers)
            {
                foreach (var c in visible)
                {
                    c.Measure(Size.Infinity); var pos = positions[c];
                    int col = Math.Clamp(pos.Column, 0, widths.Length - 1);
                    // A value that fits itself into its column (AIO) may shrink a little before the row is split into lines,
                    // so the room it keeps for its widest form does not fold rows that still fit.
                    bool fits = c is AioReferenceValue or AioFit || c is Panel panel && panel.Children.Any(x => x is AioReferenceValue or AioFit);
                    double desired = fits ? c.DesiredSize.Width * .75 : c.DesiredSize.Width;
                    widths[col] = Math.Max(widths[col], desired / Math.Max(1, pos.Span));
                }
            }
            double needed = widths.Sum() + ColumnSpacing * Math.Max(0, widths.Length - 1);
            for (int i = 0; i < columns.Length; i++)
                if (columns[i].IsAbsolute) widths[i] = Math.Max(widths[i], columns[i].Value);
            needed = widths.Sum() + ColumnSpacing * Math.Max(0, widths.Length - 1);
            if (columns.All(c => c.IsStar))
            {
                double sum = columns.Sum(c => c.Value);
                for (int i = 0; i < columns.Length; i++) if (columns[i].Value > 0) needed = Math.Max(needed, widths[i] * sum / columns[i].Value);
            }
            bool fold = Adapt && numbers && needed > available.Width + 1;
            if (fold)
            {
                if (!folded) { ColumnDefinitions.Clear(); ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star)); }
                var ordered = Children.OrderBy(c => positions[c].Row).ThenBy(c => positions[c].Column).ToArray();
                if (RowDefinitions.Count != ordered.Length) { RowDefinitions.Clear(); foreach (var c in ordered) RowDefinitions.Add(new RowDefinition(GridLength.Auto)); }
                for (int i = 0; i < ordered.Length; i++) { SetColumn(ordered[i], 0); SetRow(ordered[i], i); SetColumnSpan(ordered[i], 1); }
            }
            else if (folded)
            {
                ColumnDefinitions.Clear(); RowDefinitions.Clear();
                foreach (var width in columns) ColumnDefinitions.Add(new ColumnDefinition(width));
                foreach (var height in rows!) RowDefinitions.Add(new RowDefinition(height));
                foreach (var c in Children) { var p = positions[c]; SetColumn(c, p.Column); SetRow(c, p.Row); SetColumnSpan(c, p.Span); }
            }
            folded = fold;
        }
        return base.MeasureOverride(available);
    }
}
