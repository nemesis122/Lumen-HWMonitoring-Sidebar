using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace Lumen.App;

/// <summary>
/// The grid of a desktop design (5.8.0): column and row shares and the areas as rows of names, like a CSS grid template
/// ("cpu board gpu"). "." leaves a cell empty.
/// </summary>
public sealed record DeskGrid(double[] Columns, double[] Rows, string[] Areas)
{
    public static DeskGrid Of(string columns, string rows, params string[] areas)
    {
        static double[] Numbers(string text) => text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(n => double.Parse(n, CultureInfo.InvariantCulture)).ToArray();
        var grid = new DeskGrid(Numbers(columns), Numbers(rows), areas);
        if (areas.Length != grid.Rows.Length || areas.Any(a => a.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length != grid.Columns.Length))
            throw new ArgumentException("Desk grid: every row needs one name per column.");
        return grid;
    }
}

/// <summary>
/// A desktop design's scene (5.8.0): the parts of the design placed in a grid that fills the screen below the sidebar's header,
/// on every monitor shape: wide (16:9, 21:9, 5:4) and upright (portrait) each have their own grid. The rows share the height by
/// their shares, but never get less than their content needs: texts keep their size, only the pictures, charts and core cells
/// grow and shrink (<see cref="DeskElastic"/>). When the content needs more than the screen, the scene grows and the sidebar
/// scrolls instead of anything overlapping. An area without content (no graphics card, no power supply sensor) is taken by
/// the neighbour that shares a whole side with it; rows and columns left empty disappear. Narrower than <see cref="WideFrom"/>
/// (a design chosen in a normal sidebar) every part is stacked in one column.
/// </summary>
public sealed class DeskScene : Panel
{
    public static readonly AttachedProperty<string?> AreaProperty = AvaloniaProperty.RegisterAttached<DeskScene, Control, string?>("Area");
    public static void SetArea(Control c, string? area) => c.SetValue(AreaProperty, area);
    public static string? GetArea(Control c) => c.GetValue(AreaProperty);
    /// <summary>Height as a share of the width when the parts are stacked in one column (pictures and charts get room there too).</summary>
    public static readonly AttachedProperty<double> StackShareProperty = AvaloniaProperty.RegisterAttached<DeskScene, Control, double>("StackShare");
    public static void SetStackShare(Control c, double share) => c.SetValue(StackShareProperty, share);
    public static double GetStackShare(Control c) => c.GetValue(StackShareProperty);

    private readonly DeskGrid[] wide, tall;
    public double Gap { get; set; } = 16;
    public double WideFrom { get; set; } = 760;
    /// <summary>For tests and pictures: the height to fill instead of the screen's.</summary>
    public double? FillHeight { get; set; }
    /// <summary>The grid in use ("wide", "tall" or "stack"), for tests.</summary>
    public string Mode { get; private set; } = "";

    private sealed record Cell(Control Child, int Col, int Row, int ColSpan, int RowSpan);
    private List<Cell> cells = new();
    private double[] colX = Array.Empty<double>(), colW = Array.Empty<double>(), rowY = Array.Empty<double>(), rowH = Array.Empty<double>();
    private double target = double.NaN, laidWidth = double.NaN;

    /// <summary>The share the scene is drawn at: below 1 only when the parts need more than the screen in every grid (never below 0.75,
    /// so the smallest text, 12 px, stays at 9 px or more).</summary>
    public double Scale { get; private set; } = 1;
    public const double SmallestScale = .75;
    /// <summary>The share of their usual least height the pictures and charts get (0.5 when the screen is too low for the parts).</summary>
    public double Squeeze { get; private set; } = 1;

    /// <param name="wide">Grids for wide screens, the preferred first: the first in which everything fits is used.</param>
    /// <param name="tall">The same for upright screens (empty: the wide grids).</param>
    public DeskScene(DeskGrid[] wide, DeskGrid[]? tall = null) { this.wide = wide; this.tall = tall is { Length: > 0 } ? tall : wide; RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative); }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LayoutUpdated += Recheck;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        LayoutUpdated -= Recheck;
        base.OnDetachedFromVisualTree(e);
    }

    // Before the first layout the scene does not know where it sits; once it does, it measures again with the exact height.
    // The height never depends on the scene itself (only on the screen and what is above it), so this settles at once.
    private void Recheck(object? sender, EventArgs e)
    {
        if (Mode == "stack" || double.IsNaN(target)) return;
        // A safety catch: should the height keep changing (a parent that scales with its content), it is left as it is
        // until the window changes size.
        var screen = TopLevel.GetTopLevel(this)?.ClientSize ?? default;
        if (screen != checkedScreen) { checkedScreen = screen; rechecks = 0; }
        if (rechecks > 4) return;
        double now = Target(laidWidth);
        if (Math.Abs(now - target) > .5) { rechecks++; InvalidateMeasure(); }
    }
    private int rechecks;
    private Size checkedScreen;

    /// <summary>The height from the scene's top to the bottom of the screen (less the card's own padding and border below it).</summary>
    private double Target(double width)
    {
        if (FillHeight is double fixedHeight) return fixedHeight;
        Visual? host = null; double height = 0;
        var chain = this.GetVisualAncestors().ToList();
        if (chain.OfType<ScrollViewer>().FirstOrDefault() is { } scroll)
        {
            height = scroll.Viewport.Height > 0 ? scroll.Viewport.Height : scroll.Bounds.Height;
            host = scroll.Content as Visual ?? scroll;
            if (height <= 0 && TopLevel.GetTopLevel(this) is { } top) height = top.ClientSize.Height - 80;
        }
        else if (chain.OfType<FitPanel>().FirstOrDefault() is { Bounds.Height: > 0 } fit) { height = fit.Bounds.Height; host = fit; }
        else if (TopLevel.GetTopLevel(this) is { } top) { height = top.ClientSize.Height; host = top; }
        if (height <= 0 || host == null) return Math.Round(width * 9 / 16);
        // The distance from the top in layout positions (not drawn ones: a parent that scales its content must not change it).
        double offset = Bounds.Y, reserve = 0;
        foreach (var a in chain)
        {
            if (a == host) break;
            offset += a.Bounds.Y;
            if (a is Border b) reserve += b.Padding.Bottom + b.BorderThickness.Bottom;
            else if (a is Decorator d) reserve += d.Padding.Bottom;
            if (a is Layoutable l) reserve += l.Margin.Bottom;
        }
        return Math.Max(360, Math.Floor(height - offset - reserve - 1));
    }

    private IEnumerable<Control> Shown => Children.Where(c => c.IsVisible);

    protected override Size MeasureOverride(Size available)
    {
        double width = double.IsFinite(available.Width) ? available.Width : 1200;
        laidWidth = width;
        if (width < WideFrom)
        {
            Mode = "stack"; target = double.NaN; if (Scale != 1) { Scale = 1; RenderTransform = null; }
            SetSqueeze(1);
            double y = 0; bool first = true;
            foreach (var child in Shown)
            {
                child.Measure(new Size(width, double.PositiveInfinity));
                if (!first) y += Gap;
                y += Math.Max(child.DesiredSize.Height, width * GetStackShare(child)); first = false;
            }
            return new Size(width, y);
        }
        target = Target(width);
        var grids = target > width * 1.05 ? tall : wide;
        Mode = grids == tall && tall != wide ? "tall" : "wide";
        SetSqueeze(1);
        double total = Choose(grids, width, target);
        // Too low a screen: first the pictures and charts make do with half their usual least height.
        if (total > target + .5) { SetSqueeze(.5); total = Choose(grids, width, target); }
        if (total > target + .5) { SetSqueeze(.3); total = Choose(grids, width, target); }
        double scale = 1;
        // Nothing fits: the scene is drawn a little smaller (at most to 80 %), then the sidebar scrolls.
        for (int round = 0; round < 2 && total * scale > target + .5 && scale > SmallestScale; round++)
        {
            scale = Math.Max(SmallestScale, Math.Min(scale, target / total) * (round == 0 ? 1 : .985));
            total = Choose(grids, width / scale, target / scale);
        }
        if (Math.Abs(scale - Scale) > .0001) { Scale = scale; RenderTransform = scale == 1 ? null : new Avalonia.Media.ScaleTransform(scale, scale); }
        return new Size(width, Math.Ceiling(total * scale));
    }

    private void SetSqueeze(double value)
    {
        if (Math.Abs(value - Squeeze) < .001) return;
        Squeeze = value;
        // Every picture and chart measures again, and so does everything between it and the scene.
        foreach (var elastic in this.GetVisualDescendants().OfType<DeskElastic>())
            for (Visual? v = elastic; v != null && v != this; v = v.GetVisualParent()) (v as Layoutable)?.InvalidateMeasure();
    }

    /// <summary>Lays the parts out in the first grid where everything fits the height (else in the one that needs the least) and returns its height.</summary>
    private double Choose(DeskGrid[] grids, double width, double height)
    {
        double Total() => rowH.Length == 0 ? 0 : rowY[^1] + rowH[^1];
        int best = 0; double least = double.MaxValue;
        for (int i = 0; i < grids.Length; i++)
        {
            Solve(grids[i], width, height);
            double total = Total();
            if (total <= height + .5) return total;
            if (total < least - .5) { least = total; best = i; }
        }
        if (grids.Length > 1) Solve(grids[best], width, height);
        return Total();
    }

    protected override Size ArrangeOverride(Size final)
    {
        Grid = Mode == "stack" ? null : gridInUse;
        if (Mode == "stack")
        {
            double y = 0;
            foreach (var child in Shown)
            {
                double h = Math.Max(child.DesiredSize.Height, final.Width * GetStackShare(child));
                child.Arrange(new Rect(0, y, final.Width, h)); y += h + Gap;
            }
            return final;
        }
        foreach (var child in Children.Where(c => !cells.Any(x => x.Child == c))) child.Arrange(new Rect());
        foreach (var c in cells)
        {
            double x = colX[c.Col], y = rowY[c.Row];
            double w = colX[c.Col + c.ColSpan - 1] + colW[c.Col + c.ColSpan - 1] - x, h = rowY[c.Row + c.RowSpan - 1] + rowH[c.Row + c.RowSpan - 1] - y;
            c.Child.Arrange(new Rect(x, y, w, h));
        }
        return final;
    }

    /// <summary>Places the shown parts in the grid, gives the empty areas to their neighbours and works out the column widths and row heights.</summary>
    private DeskGrid? gridInUse;
    /// <summary>The grid the parts are laid out in (null when stacked), for tests.</summary>
    public DeskGrid? Grid { get; private set; }

    private void Solve(DeskGrid grid, double width, double height)
    {
        gridInUse = grid;
        int rows = grid.Rows.Length, cols = grid.Columns.Length;
        var names = grid.Areas.Select(a => a.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToArray();
        // Every area as a rectangle of cells: first and last row and column.
        var areas = new Dictionary<string, (int R0, int C0, int R1, int C1)>();
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                var n = names[r][c]; if (n == ".") continue;
                areas[n] = areas.TryGetValue(n, out var a) ? (Math.Min(a.R0, r), Math.Min(a.C0, c), Math.Max(a.R1, r), Math.Max(a.C1, c)) : (r, c, r, c);
            }
        var byArea = new Dictionary<string, Control>();
        foreach (var child in Shown) if (GetArea(child) is { } area && areas.ContainsKey(area) && !byArea.ContainsKey(area)) byArea[area] = child;
        var taken = areas.Where(a => byArea.ContainsKey(a.Key)).ToDictionary(a => a.Key, a => a.Value);
        var empty = areas.Keys.Where(k => !byArea.ContainsKey(k)).ToList();
        for (bool changed = true; changed && empty.Count > 0;)
        {
            changed = false;
            foreach (var e in empty.ToList())
            {
                var E = areas[e];
                string? Neighbour()
                {
                    foreach (var (k, N) in taken)
                        if (N.R0 == E.R0 && N.R1 == E.R1 && (N.C1 + 1 == E.C0 || E.C1 + 1 == N.C0)) return k;
                    foreach (var (k, N) in taken)
                        if (N.C0 == E.C0 && N.C1 == E.C1 && (N.R1 + 1 == E.R0 || E.R1 + 1 == N.R0)) return k;
                    return null;
                }
                if (Neighbour() is not { } owner) continue;
                var N = taken[owner];
                taken[owner] = (Math.Min(N.R0, E.R0), Math.Min(N.C0, E.C0), Math.Max(N.R1, E.R1), Math.Max(N.C1, E.C1));
                empty.Remove(e); changed = true;
            }
        }
        // Rows and columns no part reaches are left out.
        var usedRows = Enumerable.Range(0, rows).Where(r => taken.Values.Any(t => t.R0 <= r && r <= t.R1)).ToList();
        var usedCols = Enumerable.Range(0, cols).Where(c => taken.Values.Any(t => t.C0 <= c && c <= t.C1)).ToList();
        cells = taken.Select(t =>
        {
            int r0 = usedRows.IndexOf(t.Value.R0), r1 = usedRows.IndexOf(t.Value.R1), c0 = usedCols.IndexOf(t.Value.C0), c1 = usedCols.IndexOf(t.Value.C1);
            return new Cell(byArea[t.Key], c0, r0, c1 - c0 + 1, r1 - r0 + 1);
        }).ToList();
        int R = usedRows.Count, C = usedCols.Count;
        var colShare = usedCols.Select(c => grid.Columns[c]).ToArray(); var rowShare = usedRows.Select(r => grid.Rows[r]).ToArray();
        colW = new double[C]; colX = new double[C];
        double free = Math.Max(0, width - Gap * Math.Max(0, C - 1)), sum = colShare.Sum();
        for (int c = 0; c < C; c++) { colW[c] = free * colShare[c] / sum; colX[c] = c == 0 ? 0 : colX[c - 1] + colW[c - 1] + Gap; }

        // What every row needs at least: its parts measured at their width; a part over several rows shares its need by their shares.
        var need = new double[R];
        foreach (var c in cells.OrderBy(c => c.RowSpan))
        {
            double w = colX[c.Col + c.ColSpan - 1] + colW[c.Col + c.ColSpan - 1] - colX[c.Col];
            c.Child.Measure(new Size(w, double.PositiveInfinity));
            double h = c.Child.DesiredSize.Height;
            if (c.RowSpan == 1) { need[c.Row] = Math.Max(need[c.Row], h); continue; }
            double have = Enumerable.Range(c.Row, c.RowSpan).Sum(r => need[r]) + Gap * (c.RowSpan - 1);
            if (h <= have) continue;
            double shares = Enumerable.Range(c.Row, c.RowSpan).Sum(r => rowShare[r]);
            for (int r = c.Row; r < c.Row + c.RowSpan; r++) need[r] += (h - have) * rowShare[r] / shares;
        }
        // The height shared by the rows' shares, each row at least its need (rows that need more are fixed, the others share the rest).
        rowH = new double[R]; rowY = new double[R];
        double space = Math.Max(0, height - Gap * Math.Max(0, R - 1));
        var fixedRows = new bool[R];
        for (int round = 0; round <= R; round++)
        {
            double left = space - Enumerable.Range(0, R).Where(r => fixedRows[r]).Sum(r => need[r]);
            double shares = Enumerable.Range(0, R).Where(r => !fixedRows[r]).Sum(r => rowShare[r]);
            bool more = false;
            for (int r = 0; r < R; r++)
            {
                if (fixedRows[r]) { rowH[r] = need[r]; continue; }
                rowH[r] = shares > 0 ? Math.Max(0, left) * rowShare[r] / shares : 0;
                if (rowH[r] < need[r]) { fixedRows[r] = true; more = true; }
            }
            if (!more) break;
        }
        for (int r = 0; r < R; r++) { rowH[r] = Math.Max(rowH[r], need[r]); rowY[r] = r == 0 ? 0 : rowY[r - 1] + rowH[r - 1] + Gap; }
    }
}

/// <summary>
/// A picture, chart or drawing of a desktop design that takes whatever room its area has: it asks for only <see cref="Min"/>
/// pixels of height (so the texts around it decide the layout) and is then drawn as large as the space it gets.
/// </summary>
public sealed class DeskElastic : Decorator
{
    public double Min { get; set; } = 40;
    public double Max { get; set; } = double.PositiveInfinity;
    protected override Size MeasureOverride(Size available)
    {
        double w = double.IsFinite(available.Width) ? available.Width : 300;
        double min = Min * (this.FindAncestorOfType<DeskScene>()?.Squeeze ?? 1);
        Child?.Measure(new Size(w, min));
        return new Size(Math.Min(w, Child?.DesiredSize.Width ?? 0), min);
    }
    protected override Size ArrangeOverride(Size final)
    {
        double h = Math.Min(final.Height, Max);
        Child?.Arrange(new Rect(0, (final.Height - h) / 2, final.Width, h));
        return final;
    }
}

/// <summary>
/// Several parts of one area side by side (drives, fans) or one above the other (two graphics cards), all as high (or wide)
/// as the area allows. Side by side, parts that would get narrower than <see cref="MinItem"/> go onto further rows.
/// </summary>
public sealed class DeskFlow : Panel
{
    public bool Vertical { get; set; }
    public double Gap { get; set; } = 14;
    public double MinItem { get; set; } = 220;
    private int perRow = 1;
    private double measuredWidth = double.NaN;
    private double[] rowNeed = Array.Empty<double>();

    private int PerRow(double width, int count)
    {
        if (Vertical) return 1;
        int most = Math.Max(1, Math.Min(count, (int)Math.Floor((width + Gap) / (MinItem + Gap))));
        int rows = (int)Math.Ceiling(count / (double)most);
        return (int)Math.Ceiling(count / (double)rows);
    }

    protected override Size MeasureOverride(Size available)
    {
        var shown = Children.Where(c => c.IsVisible).ToList();
        double width = double.IsFinite(available.Width) ? available.Width : 600;
        measuredWidth = width;
        perRow = PerRow(width, Math.Max(1, shown.Count));
        int rows = (int)Math.Ceiling(shown.Count / (double)perRow);
        double w = (width - Gap * (perRow - 1)) / perRow;
        rowNeed = new double[rows];
        for (int i = 0; i < shown.Count; i++)
        {
            shown[i].Measure(new Size(w, double.PositiveInfinity));
            rowNeed[i / perRow] = Math.Max(rowNeed[i / perRow], shown[i].DesiredSize.Height);
        }
        return new Size(width, rowNeed.Sum() + Gap * Math.Max(0, rows - 1));
    }

    protected override Size ArrangeOverride(Size final)
    {
        var shown = Children.Where(c => c.IsVisible).ToList();
        // Arranged at another width than measured: the parts per row follow this width (without measuring again here,
        // which would start the layout over and over).
        if (Math.Abs(final.Width - measuredWidth) > .5)
        {
            perRow = PerRow(final.Width, Math.Max(1, shown.Count));
            int count = (int)Math.Ceiling(shown.Count / (double)perRow);
            rowNeed = new double[count];
            for (int i = 0; i < shown.Count; i++) rowNeed[i / perRow] = Math.Max(rowNeed[i / perRow], shown[i].DesiredSize.Height);
        }
        int rows = rowNeed.Length; if (rows == 0) return final;
        double extra = Math.Max(0, final.Height - rowNeed.Sum() - Gap * (rows - 1)) / rows;
        double w = (final.Width - Gap * (perRow - 1)) / perRow, y = 0;
        for (int r = 0; r < rows; r++)
        {
            double h = rowNeed[r] + extra;
            var inRow = shown.Skip(r * perRow).Take(perRow).ToList();
            // A last row with fewer parts is centred.
            double x = (final.Width - (inRow.Count * w + (inRow.Count - 1) * Gap)) / 2;
            foreach (var child in inRow) { child.Arrange(new Rect(x, y, w, h)); x += w + Gap; }
            y += h + Gap;
        }
        return final;
    }
}
