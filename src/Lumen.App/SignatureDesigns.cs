using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed partial class LocalDesignTile
{
    private readonly List<(string Key, RadialHistory Plot)> radial = new();
    private readonly List<(string Key, PixelPercent Gauge)> pixels = new();
    private HorizonHistory? horizon;
    private string horizonPrimary = "", horizonSecondary = "";

    private bool HistoryVisible(string key) => RenderedKeys.Contains(key) && S.Shows(key + ".history")
        && (Kind == WidgetKind.Memory ? S.Shows("mem.bar") : Summaries.ChartKey(Kind) is not string group || S.Shows(group));

    private LiveMetric SignatureMetric(string key, (LiveMetric Primary, LiveMetric Secondary) pair)
    {
        if (key == "mem.used" && Last.Snap.Memory is { } memory)
            return pair.Primary with { Series = Last.History.Read("mem.load").Select(v => v * memory.TotalGb / 100).ToArray(), Maximum = memory.TotalGb };
        return key == WidgetSensors.PrimaryKey(Kind) ? pair.Primary : pair.Secondary;
    }

    private void UpdateSignature(List<CompleteReading> rows, (LiveMetric Primary, LiveMetric Secondary) pair)
    {
        foreach (var item in radial)
        {
            var metric = SignatureMetric(item.Key, pair);
            item.Plot.Set(HistoryVisible(item.Key) ? metric.Series : Array.Empty<double>(), metric.Maximum, ColorFor(item.Key), P.Track);
        }
        foreach (var item in pixels)
            item.Gauge.Set(rows.FirstOrDefault(r => r.Key == item.Key)?.Fraction, ColorFor(item.Key), P.Track);
        if (horizon != null)
        {
            var first = SignatureMetric(horizonPrimary, pair);
            var second = SignatureMetric(horizonSecondary, pair);
            horizon.IsVisible = HistoryVisible(horizonPrimary) || HistoryVisible(horizonSecondary);
            horizon.Set(HistoryVisible(horizonPrimary) ? first.Series : Array.Empty<double>(), first.Maximum,
                HistoryVisible(horizonSecondary) ? second.Series : Array.Empty<double>(), second.Maximum,
                Accent, Kind == WidgetKind.Network ? ColorFor("net.up") : P.Calm, P.Stroke);
        }
    }

    private Control SignatureNumber(CompleteReading row, double size, bool verticalUnit = false)
    {
        var number = Value(row, size);
        var unit = Text("", 12, P.Soft);
        units.Add(row.Key, unit);
        number.HorizontalAlignment = unit.HorizontalAlignment = HorizontalAlignment.Center;
        number.TextAlignment = TextAlignment.Center;
        unit.VerticalAlignment = VerticalAlignment.Bottom;
        unit.Margin = new Thickness(0, 0, 0, verticalUnit ? 0 : 4);
        var readout = verticalUnit ? Ui.Stack(0, number, unit) : Ui.Row(3, number, unit);
        readout.HorizontalAlignment = HorizontalAlignment.Center;
        return readout;
    }

    private Control ApertureFace(CompleteReading row)
    {
        var trace = new RadialHistory { Width = 98, Height = 98, Tag = "graphic", HorizontalAlignment = HorizontalAlignment.Center };
        radial.Add((row.Key, trace));
        ToolTip.SetTip(trace, row.Label + " · " + L.T("History chart") + " · " + History.Capacity * S.IntervalMs / 1000 + " s");
        var center = SignatureNumber(row, Kind == WidgetKind.Network ? 25 : 28, true);
        center.VerticalAlignment = VerticalAlignment.Center;
        var face = new Grid { Children = { trace, center } };
        var label = Text(row.Label, 11.5, P.Soft);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        return Ui.Stack(1, label, face);
    }

    private void BuildSignature(CompleteReading[] leads, List<CompleteReading> rest, string primary, string secondary, bool charts)
    {
        if (style == WidgetStyle.Aperture && leads.Length > 0)
        {
            body.Children.Add(new StatPanel(Array.Empty<double>(), leads.Select(ApertureFace).ToArray())
                { MaxColumns = 2, MinimumColumnWidth = 112 * Math.Max(1, S.ActiveWidget?.Graphics ?? 1), Balanced = true });
            DistinctDetails(rest);
        }
        else if (style == WidgetStyle.PixelDeck && leads.Length > 0)
        {
            var cells = new List<Control>();
            foreach (var row in leads)
            {
                var cell = Ui.Stack(2, Hero(row, row.Key != primary));
                if (row.Fraction.HasValue && row.Value.Contains('%') || row.Key is "cpu.load" or "gpu.load" or "mem.load")
                {
                    var gauge = new PixelPercent { Width = 96, Height = 42, Tag = "graphic" };
                    pixels.Add((row.Key, gauge));
                    ToolTip.SetTip(gauge, row.Label + " · 100 cells = 100 %");
                    cell.Children.Add(gauge);
                }
                cells.Add(cell);
            }
            body.Children.Add(new StatPanel(Array.Empty<double>(), cells.ToArray())
                { MaxColumns = 2, MinimumColumnWidth = 112 * Math.Max(1, S.ActiveWidget?.Graphics ?? 1), Balanced = true });
            DistinctDetails(rest); AddPlot();
        }
        else if (style == WidgetStyle.HorizonSync && leads.Length > 0)
        {
            var values = leads.Select(row =>
            {
                var label = Text(row.Label, 11.5, P.Soft); label.HorizontalAlignment = HorizontalAlignment.Center;
                return (Control)Ui.Stack(1, label, SignatureNumber(row, Kind == WidgetKind.Network ? 27 : 32));
            }).ToArray();
            body.Children.Add(new StatPanel(Array.Empty<double>(), values) { MaxColumns = 2, MinimumColumnWidth = 108, Balanced = true });
            if (charts)
            {
                horizonPrimary = primary; horizonSecondary = secondary;
                horizon = new HorizonHistory { Height = S.Horizontal ? 36 : 47, Tag = "graphic" };
                ToolTip.SetTip(horizon, WidgetSensors.PrimaryLabel(Kind) + " ↑ · " + WidgetSensors.SecondaryLabel(Kind) + " ↓ · " + History.Capacity * S.IntervalMs / 1000 + " s");
                body.Children.Add(horizon);
            }
            DistinctDetails(rest);
        }
        else DistinctDetails(rest.Concat(leads).ToList());
    }
}

internal sealed class PixelPercent : Control
{
    private double? fraction;
    private Color color, track;
    public int Rows { get; init; } = 10;
    public void Set(double? value, Color accent, Color background) { fraction = value; color = accent; track = background; InvalidateVisual(); }
    public override void Render(DrawingContext context)
    {
        int columns = 100 / Rows;
        int filled = fraction.HasValue && double.IsFinite(fraction.Value) ? (int)Math.Round(Math.Clamp(fraction.Value, 0, 1) * 100) : 0;
        double w = Bounds.Width / columns, h = Bounds.Height / Rows;
        for (int i = 0; i < 100; i++)
            context.DrawRectangle(Palette.Brush(i < filled ? color : track), null,
                new Rect(i % columns * w, i / columns * h, Math.Max(.5, w - 1.2), Math.Max(.5, h - .8)), .7, .7);
    }
}

internal sealed class RadialHistory : Control
{
    private double[] values = Array.Empty<double>();
    private double maximum = 1;
    private Color color, track;
    public void Set(double[] data, double? max, Color accent, Color background)
    {
        values = data; maximum = max is > 0 ? max.Value : Math.Max(1e-9, data.Where(double.IsFinite).DefaultIfEmpty(1).Max() * 1.12);
        color = accent; track = background; InvalidateVisual();
    }
    public override void Render(DrawingContext context)
    {
        double r = Math.Max(0, Math.Min(Bounds.Width, Bounds.Height) / 2 - 5);
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        context.DrawEllipse(null, new Pen(Palette.Brush(track), 1), center, r, r);
        for (int i = 0; i < 12; i++)
        {
            double a = i * Math.PI / 6;
            context.DrawLine(new Pen(Palette.Brush(track), 1), At(a, r + 2), At(a, r + 4));
        }
        if (values.Length < 2) return;
        var geometry = new StreamGeometry();
        bool open = false, any = false;
        Point last = center;
        using (var path = geometry.Open())
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (!double.IsFinite(values[i])) { if (open) path.EndFigure(false); open = false; continue; }
                any = true;
                double angle = -Math.PI / 2 + i / (double)Math.Max(1, values.Length - 1) * Math.PI * 1.94;
                last = At(angle, r * (.66 + .33 * Math.Clamp(values[i] / maximum, 0, 1)));
                if (!open) { path.BeginFigure(last, false); open = true; } else path.LineTo(last);
            }
            if (open) path.EndFigure(false);
        }
        if (!any) return;
        context.DrawGeometry(null, new Pen(Palette.Brush(color), 1.8, lineCap: PenLineCap.Round), geometry);
        context.DrawEllipse(Palette.Brush(color), null, last, 2, 2);
        Point At(double angle, double radius) => new(center.X + Math.Cos(angle) * radius, center.Y + Math.Sin(angle) * radius);
    }
}

internal sealed class HorizonHistory : Control
{
    private double[] upper = Array.Empty<double>(), lower = Array.Empty<double>();
    private double maxUpper = 1, maxLower = 1;
    private Color first, second, grid;
    public void Set(double[] top, double? topMax, double[] bottom, double? bottomMax, Color a, Color b, Color rule)
    {
        upper = top; lower = bottom;
        maxUpper = topMax is > 0 ? topMax.Value : Math.Max(1e-9, top.Where(double.IsFinite).DefaultIfEmpty(1).Max() * 1.12);
        maxLower = bottomMax is > 0 ? bottomMax.Value : Math.Max(1e-9, bottom.Where(double.IsFinite).DefaultIfEmpty(1).Max() * 1.12);
        first = a; second = b; grid = rule; InvalidateVisual();
    }
    public override void Render(DrawingContext context)
    {
        double middle = Bounds.Height / 2;
        context.DrawLine(new Pen(Palette.Brush(grid), 1), new Point(0, middle), new Point(Bounds.Width, middle));
        Draw(upper, maxUpper, -1, first); Draw(lower, maxLower, 1, second);
        void Draw(double[] data, double max, int direction, Color color)
        {
            if (data.Length > 1 && data.All(double.IsFinite))
            {
                var area = new StreamGeometry();
                using (var fill = area.Open())
                {
                    fill.BeginFigure(new Point(0, middle), true);
                    for (int i = 0; i < data.Length; i++)
                        fill.LineTo(new Point(Bounds.Width * i / (data.Length - 1d), middle + direction * (middle - 2) * Math.Clamp(data[i] / max, 0, 1)));
                    fill.LineTo(new Point(Bounds.Width, middle)); fill.EndFigure(true);
                }
                context.DrawGeometry(Palette.Brush(Palette.Alpha(color, .13)), null, area);
            }
            var geometry = new StreamGeometry(); bool open = false;
            using (var path = geometry.Open())
            {
                for (int i = 0; i < data.Length; i++)
                {
                    if (!double.IsFinite(data[i])) { if (open) path.EndFigure(false); open = false; continue; }
                    var point = new Point(Bounds.Width * i / Math.Max(1d, data.Length - 1), middle + direction * (middle - 2) * Math.Clamp(data[i] / max, 0, 1));
                    if (!open) { path.BeginFigure(point, false); open = true; } else path.LineTo(point);
                }
                if (open) path.EndFigure(false);
            }
            context.DrawGeometry(null, new Pen(Palette.Brush(color), 1.7, lineCap: PenLineCap.Round), geometry);
        }
    }
}
