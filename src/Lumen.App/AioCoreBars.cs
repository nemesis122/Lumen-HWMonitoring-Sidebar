using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed partial class AioTile
{
    private AioCoreBars? coreBars;
    // The last per-core loads; a reading that drops out for a moment keeps the bars instead of rebuilding the card.
    private IReadOnlyList<double> lastCoreLoads = Array.Empty<double>();

    /// <summary>
    /// The load of every CPU core as a row of bars (K1, K2, …), placed right below the CPU part of every AIO design.
    /// Switched with the "Core load" choice in the settings; left out when the CPU or its per-core loads are missing.
    /// </summary>
    private void ReferenceCoreBars()
    {
        coreBars = null;
        var loads = lastCoreLoads;
        if (loads.Count < 2 || !AioReadings.Selected(settings, "aio.cpu.coreloads") || !referenceRows.Any(r => r.Group == "CPU")) return;
        // Every AIO design: a compact heatmap (one coloured cell per core) instead of mostly empty bars with tiny numbers.
        coreBars = new AioCoreBars(ink, muted, track) { Loads = loads, Heatmap = true };
        var section = new Border { Padding = new Thickness(9, 4, 9, 8), Child = coreBars, Tag = "aio-section" };
        // Right after the part of the design that shows the CPU values; otherwise after the hardware names.
        var cpuTexts = fields.Where(f => f.Key.StartsWith("aio.cpu.")).SelectMany(f => f.Value).ToHashSet();
        int at = -1;
        for (int i = 0; i < content.Children.Count && at < 0; i++)
            if (content.Children[i] is ILogical child && (child is TextBlock t && cpuTexts.Contains(t) || child.GetLogicalDescendants().OfType<TextBlock>().Any(cpuTexts.Contains))) at = i;
        if (at < 0) { content.Children.Add(section); return; }
        content.Children.Insert(at + 1, section);
    }

    private void UpdateCoreBars()
    {
        if (coreBars != null) coreBars.Loads = lastCoreLoads;
        if (mainboardCores != null) mainboardCores.Loads = lastCoreLoads;
    }
}

/// <summary>One bar per CPU core, filled and coloured by its load; core names and loads below when there is room.</summary>
public sealed class AioCoreBars : Control, IColourGraph, RedSkin.IOneColour
{
    /// <summary>Red designs: one red, no lighter second tone (set by RedSkin).</summary>
    public bool OneColour { get; set; }
    public bool Compact { get; set; }
    public bool NumbersOnly { get; init; }
    /// <summary>One cell per core, filled with the load colour; core name small at the top, load large in the middle.</summary>
    public bool Heatmap { get; init; }
    /// <summary>Desktop designs (5.8.0): the heatmap cells grow to fill the space they are given (at least 36 px high).</summary>
    public bool Fill { get; init; }
    /// <summary>3D bar chart (5.5.0): one isometric column per core on a floor grid, as tall as its load.</summary>
    public bool Iso { get; init; }
    private const double HeatCell = 32, HeatGap = 4;
    private readonly Color ink, muted, track;
    private IReadOnlyList<double> loads = Array.Empty<double>();
    public AioCoreBars(Color ink, Color muted, Color track) { this.ink = ink; this.muted = muted; this.track = track; }
    public IReadOnlyList<double> Loads
    {
        get => loads;
        set { bool countChanged = value.Count != loads.Count; loads = value.ToArray(); if (countChanged) InvalidateMeasure(); InvalidateVisual(); }
    }
    private const double Gap = 4, MinColumn = 15, Labels = 28;
    /// <summary>Cores per row: as many as fit with a readable name and load below; more cores wrap onto further rows.</summary>
    private int MostPerRow(double width) => Math.Max(1, Math.Min(loads.Count, (int)Math.Floor((width + Gap) / ((Heatmap ? 30 : NumbersOnly ? 27 : MinColumn) + Gap))));
    private int Rows(double width) => loads.Count == 0 ? 0 : (int)Math.Ceiling(loads.Count / (double)MostPerRow(width));
    // Rows are filled evenly (16 cores: 8 + 8, not 11 + 5).
    private int PerRow(double width) => Rows(width) == 0 ? 1 : (int)Math.Ceiling(loads.Count / (double)Rows(width));
    private double BarHeight(double width) => NumbersOnly ? 0 : Compact ? 14 : Rows(width) > 1 ? 34 : 46;
    // The height depends on the width and the number of cores only, never on the values, so nothing below moves.
    protected override Size MeasureOverride(Size available)
    {
        double width = double.IsFinite(available.Width) ? available.Width : 260;
        if (Iso) return new Size(width, Math.Min(190, width * .62));
        if (Heatmap && Fill)
        {
            if (loads.Count == 0) return new Size(width, 0);
            int across = Math.Max(1, Math.Min(loads.Count, (int)Math.Floor((width + HeatGap) / (44 + HeatGap))));
            int down = (int)Math.Ceiling(loads.Count / (double)across);
            return new Size(width, down * 38 + (down - 1) * HeatGap);
        }
        int rows = Rows(width);
        if (Heatmap) return new Size(width, rows * HeatCell + Math.Max(0, rows - 1) * HeatGap);
        return new Size(width, rows * (BarHeight(width) + Labels) + Math.Max(0, rows - 1) * 6);
    }
    internal static Color Heat(double f)
    {
        var stops = new (double At, Color C)[] { (0, Color.FromRgb(46, 110, 220)), (.35, Color.FromRgb(41, 211, 221)), (.7, Color.FromRgb(243, 188, 74)), (1, Color.FromRgb(255, 122, 107)) };
        f = Math.Clamp(f, 0, 1);
        for (int i = 1; i < stops.Length; i++)
            if (f <= stops[i].At)
            {
                double t = (f - stops[i - 1].At) / (stops[i].At - stops[i - 1].At); var a = stops[i - 1].C; var b = stops[i].C;
                return Color.FromRgb((byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));
            }
        return stops[^1].C;
    }
    /// <summary>Chosen colour or Red design: the load colours in that hue.</summary>
    public Color? Tint { get; set; }
    /// <summary>Value and label colours chosen in Appearance (null: the design's own).</summary>
    public Color? ValueInk { get; set; }
    public Color? LabelInk { get; set; }
    private void RenderIso(DrawingContext c, double w, double h)
    {
        int n = loads.Count, columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(n))), rows = (int)Math.Ceiling(n / (double)columns);
        const double cell = 38, box = 24;
        double floorW = columns * cell, floorD = rows * cell;
        // Fit the floor (and room for the tallest column) into the box.
        double s = Math.Min((w - 8) / ((floorW + floorD) * Lumen.App.Iso.Cos), (h - 6) / ((floorW + floorD) * Lumen.App.Iso.Sin + 100));
        var o = new Point(w / 2 - (floorW - floorD) / 2 * Lumen.App.Iso.Cos * s, h - 4 - (floorW + floorD) * Lumen.App.Iso.Sin * s);
        var line = new Pen(Palette.Brush(Color.FromArgb(40, (Tint ?? ink).R, (Tint ?? ink).G, (Tint ?? ink).B)), 1);
        for (int i = 0; i <= columns; i++) c.DrawLine(line, Lumen.App.Iso.P(i * cell, 0, 0, o, s), Lumen.App.Iso.P(i * cell, floorD, 0, o, s));
        for (int i = 0; i <= rows; i++) c.DrawLine(line, Lumen.App.Iso.P(0, i * cell, 0, o, s), Lumen.App.Iso.P(floorW, i * cell, 0, o, s));
        var order = Enumerable.Range(0, n).OrderBy(i => i % columns + i / columns).ToArray();
        foreach (int i in order)
        {
            double f = double.IsFinite(loads[i]) ? Math.Clamp(loads[i] / 100, 0, 1) : 0;
            var colour = Tint is Color tint ? RedSkin.Tint(Heat(f), tint, OneColour) : Heat(f);
            Lumen.App.Iso.Box(c, (i % columns) * cell + (cell - box) / 2, (i / columns) * cell + (cell - box) / 2, box, box, 6 + f * 94, colour, o, s);
        }
    }

    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width; if (loads.Count == 0 || w < 10) return;
        if (Heatmap) { RenderHeatmap(c, w); return; }
        if (Iso) { RenderIso(c, w, Bounds.Height); return; }
        int perRow = PerRow(w); double barHeight = BarHeight(w);
        double col = (w - Gap * (perRow - 1)) / perRow;
        double radius = Math.Min(5, col / 2);
        var typeface = new Typeface(Ui.GraphFont, FontStyle.Normal, FontWeight.Medium);
        var number = new Typeface(Ui.GraphFont, FontStyle.Normal, FontWeight.SemiBold);
        double size = Math.Clamp(col * .5, 8, 11);
        var back = Palette.Brush(Color.FromArgb(Math.Max(track.A, (byte)120), track.R, track.G, track.B));
        double peak = NumbersOnly ? loads.Where(double.IsFinite).DefaultIfEmpty(0).Max() : 0;
        for (int i = 0; i < loads.Count; i++)
        {
            double x = (i % perRow) * (col + Gap), top = (i / perRow) * (barHeight + Labels + 6);
            double f = double.IsFinite(loads[i]) ? Math.Clamp(loads[i] / 100, 0, 1) : 0;
            if (NumbersOnly)
                c.DrawRectangle(back, null, new Rect(x, top, col, Labels), radius, radius);
            else
            {
                c.DrawRectangle(back, null, new Rect(x, top, col, barHeight), radius, radius);
                double h = Math.Max(Math.Min(radius * 2, barHeight), barHeight * f);
                c.DrawRectangle(Palette.Brush(Tint is Color tint ? RedSkin.Tint(Heat(f), tint, OneColour) : Heat(f)),
                    null, new Rect(x, top + barHeight - h, col, h), radius, radius);
            }
            void Centered(string text, Typeface face, Color color, double y)
            {
                var ft = new FormattedText(text, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, face, size, Palette.Brush(color));
                double s = ft.Width > col + Gap - 1 ? (col + Gap - 1) / ft.Width : 1;
                using (c.PushTransform(Matrix.CreateTranslation(-(x + col / 2), -y) * Matrix.CreateScale(s, s) * Matrix.CreateTranslation(x + col / 2, y)))
                    c.DrawText(ft, new Point(x + col / 2 - ft.Width / 2, y));
            }
            Centered("C" + (i + 1), typeface, LabelInk is Color li ? li : muted, top + barHeight + 2);
            Centered(double.IsFinite(loads[i]) ? Math.Round(loads[i]).ToString("0") + (NumbersOnly ? " %" : "") : "—", number, NumbersOnly && peak > 0 && loads[i] == peak && Tint is Color highlight ? highlight : ValueInk ?? ink, top + barHeight + 14);
        }
    }

    private void RenderHeatmap(DrawingContext c, double w)
    {
        int perRow = PerRow(w);
        double col = (w - HeatGap * (perRow - 1)) / perRow, cell = HeatCell, nameSize = 8.5, numberSize = 12.5, top0 = 0;
        if (Fill)
        {
            // The arrangement that gives the largest cells (cells at most twice as wide as high), rows filled evenly.
            double h = Bounds.Height, best = -1;
            for (int tried = 1; tried <= loads.Count; tried++)
            {
                int down = (int)Math.Ceiling(loads.Count / (double)tried), across = (int)Math.Ceiling(loads.Count / (double)down);
                double cw = (w - HeatGap * (across - 1)) / across, ch = (h - HeatGap * (down - 1)) / down;
                if (ch < 34 || cw < 34) continue;
                // Empty places in the last row count against an arrangement (16 cores: 4 × 4 or 8 × 2 rather than 6 + 6 + 4).
                double score = Math.Min(cw, ch * 2) * Math.Min(ch, cw * 1.2) * loads.Count / (across * down);
                if (score > best) { best = score; perRow = across; col = cw; cell = Math.Min(ch, cw * 1.2); }
            }
            nameSize = Math.Clamp(cell * .2, 11.5, 15); numberSize = Math.Clamp(cell * .4, 12.5, 34);
            int rows = (int)Math.Ceiling(loads.Count / (double)perRow);
            top0 = Math.Max(0, (h - rows * cell - (rows - 1) * HeatGap) / 2);
        }
        var name = new Typeface(Ui.GraphFont, FontStyle.Normal, FontWeight.Medium);
        var number = new Typeface(Ui.GraphFont, FontStyle.Normal, FontWeight.SemiBold);
        var empty = Color.FromArgb(Math.Max(track.A, (byte)120), track.R, track.G, track.B);
        for (int i = 0; i < loads.Count; i++)
        {
            double x = (i % perRow) * (col + HeatGap), y = top0 + (i / perRow) * (cell + HeatGap);
            // A shorter last row is centred.
            int last = loads.Count - (loads.Count - 1) / perRow * perRow;
            if (Fill && i / perRow == (loads.Count - 1) / perRow && last < perRow) x += (perRow - last) * (col + HeatGap) / 2;
            double f = double.IsFinite(loads[i]) ? Math.Clamp(loads[i] / 100, 0, 1) : 0;
            var heat = Tint is Color tint ? RedSkin.Tint(Heat(f), tint, OneColour) : Heat(f);
            // Idle cores stay a calm dark cell with a hint of colour; the cell fills with colour as the load rises.
            var fill = Palette.Mix(empty, heat, .18 + .72 * Math.Sqrt(f));
            c.DrawRectangle(Palette.Brush(fill), new Pen(Palette.Brush(Color.FromArgb(70, heat.R, heat.G, heat.B)), 1), new Rect(x + .5, y + .5, col - 1, cell - 1), Fill ? 8 : 5, Fill ? 8 : 5);
            void Draw(string text, Typeface face, double size, Color color, double top)
            {
                var ft = new FormattedText(text, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, face, size, Palette.Brush(color));
                double s = ft.Width > col - 4 ? (col - 4) / ft.Width : 1;
                using (c.PushTransform(Matrix.CreateTranslation(-(x + col / 2), -top) * Matrix.CreateScale(s, s) * Matrix.CreateTranslation(x + col / 2, top)))
                    c.DrawText(ft, new Point(x + col / 2 - ft.Width / 2, top));
            }
            double top = Fill ? y + (cell - nameSize * 1.3 - numberSize * 1.25) / 2 : y + 2;
            Draw("C" + (i + 1), name, nameSize, LabelInk ?? Color.FromArgb(190, ink.R, ink.G, ink.B), top);
            Draw(double.IsFinite(loads[i]) ? Math.Round(loads[i]).ToString("0") : "—", number, numberSize, ValueInk ?? ink, Fill ? top + nameSize * 1.3 : y + 13);
        }
    }
}
