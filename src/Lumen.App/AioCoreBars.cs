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
        coreBars = new AioCoreBars(ink, muted, track) { Loads = loads };
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
    }
}

/// <summary>One bar per CPU core, filled and coloured by its load; core names and loads below when there is room.</summary>
public sealed class AioCoreBars : Control
{
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
    private int MostPerRow(double width) => Math.Max(1, Math.Min(loads.Count, (int)Math.Floor((width + Gap) / (MinColumn + Gap))));
    private int Rows(double width) => loads.Count == 0 ? 0 : (int)Math.Ceiling(loads.Count / (double)MostPerRow(width));
    // Rows are filled evenly (16 cores: 8 + 8, not 11 + 5).
    private int PerRow(double width) => Rows(width) == 0 ? 1 : (int)Math.Ceiling(loads.Count / (double)Rows(width));
    private double BarHeight(double width) => Rows(width) > 1 ? 34 : 46;
    // The height depends on the width and the number of cores only, never on the values, so nothing below moves.
    protected override Size MeasureOverride(Size available)
    {
        double width = double.IsFinite(available.Width) ? available.Width : 260;
        int rows = Rows(width);
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
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width; if (loads.Count == 0 || w < 10) return;
        int perRow = PerRow(w); double barHeight = BarHeight(w);
        double col = (w - Gap * (perRow - 1)) / perRow;
        double radius = Math.Min(5, col / 2);
        var typeface = new Typeface(Ui.Font, FontStyle.Normal, FontWeight.Medium);
        var number = new Typeface(Ui.Font, FontStyle.Normal, FontWeight.SemiBold);
        double size = Math.Clamp(col * .5, 8, 11);
        var back = Palette.Brush(Color.FromArgb(Math.Max(track.A, (byte)120), track.R, track.G, track.B));
        for (int i = 0; i < loads.Count; i++)
        {
            double x = (i % perRow) * (col + Gap), top = (i / perRow) * (barHeight + Labels + 6);
            double f = double.IsFinite(loads[i]) ? Math.Clamp(loads[i] / 100, 0, 1) : 0;
            c.DrawRectangle(back, null, new Rect(x, top, col, barHeight), radius, radius);
            double h = Math.Max(Math.Min(radius * 2, barHeight), barHeight * f);
            c.DrawRectangle(Palette.Brush(Heat(f)), null, new Rect(x, top + barHeight - h, col, h), radius, radius);
            void Centered(string text, Typeface face, Color color, double y)
            {
                var ft = new FormattedText(text, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, face, size, Palette.Brush(color));
                double s = ft.Width > col + Gap - 1 ? (col + Gap - 1) / ft.Width : 1;
                using (c.PushTransform(Matrix.CreateTranslation(-(x + col / 2), -y) * Matrix.CreateScale(s, s) * Matrix.CreateTranslation(x + col / 2, y)))
                    c.DrawText(ft, new Point(x + col / 2 - ft.Width / 2, y));
            }
            Centered("C" + (i + 1), typeface, muted, top + barHeight + 2);
            Centered(double.IsFinite(loads[i]) ? Math.Round(loads[i]).ToString("0") : "—", number, ink, top + barHeight + 14);
        }
    }
}
