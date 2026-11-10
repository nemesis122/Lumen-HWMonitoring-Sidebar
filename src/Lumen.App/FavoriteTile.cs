using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

// Three layouts from the user's Lumen sidebar references. All consume the same filtered,
// complete readings, so switching a design never changes sensor selection or stored data.
public sealed class FavoriteTile : SummaryTile
{
    public static readonly WidgetStyle[] Styles = { WidgetStyle.CleanDark, WidgetStyle.CompactPro, WidgetStyle.PerformanceView };
    private readonly WidgetStyle style;
    private readonly StackPanel body;
    private readonly TextBlock title, device;
    private readonly Border dot;
    private readonly Dictionary<string, TextBlock> values = new();
    private readonly Dictionary<string, TextBlock> units = new();
    private readonly Dictionary<string, FillBack> bars = new();
    private readonly List<(string Key, AreaChart Chart, Control Host)> charts = new();
    private string shape = "";
    private Color Accent => Accents.For(Kind, Slot ?? S.ActiveWidget, P);
    private bool Compact => style == WidgetStyle.CompactPro;
    private bool Performance => style == WidgetStyle.PerformanceView;
    public IReadOnlyList<string> RenderedKeys { get; private set; } = Array.Empty<string>();

    public FavoriteTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s, WidgetStyle style)
        : base(kind, index, p, m, s, false)
    {
        this.style = style;
        Padding = new Thickness(m.Pad, Compact ? 10 : 14);
        CornerRadius = new CornerRadius(Compact ? 12 : Performance ? 14 : 24);
        BorderThickness = new Thickness(Performance ? 1 : 0);
        if (Performance) Background = Palette.Brush(p.DesignBack("#101925"));
        title = Ui.Text("", 13, p.Ink, FontWeight.SemiBold);
        device = Ui.Text("", 10.5, p.Soft);
        HardwareWidgetTitle.Register(this, title, device);
        device.TextWrapping = TextWrapping.Wrap;
        dot = new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(3), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        var header = Ui.Columns("Auto,*", dot, Ui.Stack(3, title, device));
        body = Ui.Stack(s.Horizontal ? 6 : Compact ? 7 : 10);
        body.Tag = "elastic";
        Child = Ui.Stack(Compact ? 9 : 12, header, body);
        Child.Tag = "elastic";
    }

    protected override void Show(Summary summary)
    {
        title.Text = Naming.Title(Slot, Index, summary.Title);
        device.Text = Naming.Hardware(Slot, Index, summary.Name);
        device.IsVisible = Naming.ShowHardware(Slot, S) && device.Text.Length > 0;
        dot.Background = Palette.Brush(Accent);
        var rows = CompleteReadings.Body(Kind, Index, Last.Snap, Last.History, S, P, style);
        RenderedKeys = rows.Select(r => r.Key).ToArray();
        string next = string.Join("\n", rows.Select(r => r.Key + "|" + r.Label));
        if (shape != next) { shape = next; Build(rows); }
        foreach (var row in rows)
        {
            if (values.TryGetValue(row.Key, out var text))
            {
                if (units.TryGetValue(row.Key, out var unit))
                {
                    var split = TwinFocusMetrics.Split(row.Value);
                    text.Text = split.Value; unit.Text = split.Unit;
                }
                else text.Text = row.Value;
                ToolTip.SetTip(text, row.Label + ": " + row.Value);
            }
            if (bars.TryGetValue(row.Key, out var bar))
            {
                bar.IsVisible = row.Fraction.HasValue;
                bar.Animate(row.Fraction ?? 0, ColorFor(row.Key));
            }
        }
        var pair = TwinFocusMetrics.Pair(Kind, Index, summary, Last.Snap, Last.History, S);
        foreach (var plot in charts)
        {
            var metric = plot.Key == WidgetSensors.PrimaryKey(Kind) ? pair.Primary : pair.Secondary;
            bool visible = RenderedKeys.Contains(plot.Key) && S.Shows(plot.Key + ".history")
                && metric.Series.Any(double.IsFinite)
                && (Summaries.ChartKey(Kind) is not string group || S.Shows(group));
            plot.Host.IsVisible = visible;
            plot.Chart.Max = metric.Maximum;
            plot.Chart.Set(visible ? metric.Series : Array.Empty<double>(), ColorFor(plot.Key));
        }
    }

    private Color ColorFor(string key) => key == "net.up" ? Accents.Readable(Color.Parse(Accents.Upload), P) : Accent;
    private Border Rule() => new() { Height = 1, Background = Palette.Brush(P.Stroke), Margin = new Thickness(0, 2) };

    private TextBlock Value(CompleteReading row, double size, Color color, string? reserve = null)
    {
        var model = Ui.Text("", size, color, FontWeight.SemiBold, numbers: true);
        var text = WidgetPresentation.Sensor(reserve == null ? model : new ReservedNumberText(model, reserve), row.Key);
        text.TextTrimming = TextTrimming.None;
        text.TextWrapping = reserve == null ? TextWrapping.Wrap : TextWrapping.NoWrap;
        text.Tag = "favorite-value:" + row.Key;
        values[row.Key] = text;
        return text;
    }

    private Control Hero(CompleteReading row, bool secondary)
    {
        double size = Kind == WidgetKind.Network ? M.Hero * .68 : Compact ? M.Hero * .72 : secondary ? M.Hero * .65 : M.Hero * 1.12;
        Color color = !Compact && !Performance || Kind == WidgetKind.Network ? ColorFor(row.Key) : P.Ink;
        var number = Value(row, size, color, Kind == WidgetKind.Network ? "888.8" : "888");
        number.LetterSpacing = -1;
        var unit = Ui.Text("", Math.Max(11, size * .38), P.Soft, FontWeight.Medium);
        unit.VerticalAlignment = VerticalAlignment.Bottom;
        unit.Margin = new Thickness(3, 0, 0, size * .10);
        units[row.Key] = unit;
        var readout = Ui.Row(0, number, unit);
        var label = Ui.Text(row.Key == "net.down" ? "↓ " + L.T("Download") : row.Key == "net.up" ? "↑ " + L.T("Upload") : row.Label, 10, P.Soft);
        Control face = Kind == WidgetKind.Drives ? SteadyHeight.Of(new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = HorizontalAlignment.Left, Child = readout }) : readout;
        var cell = Ui.Stack(4, label, face);
        cell.Margin = new Thickness(0, 0, 8, 0);
        return cell;
    }

    private Control Plot(string key, string label)
    {
        var chart = WidgetPresentation.Sensor(new AreaChart
        {
            Height = S.Horizontal ? M.Chart * (Performance ? .6 : .3) : Performance ? M.Chart * 1.3 : Compact ? M.Chart * .4 : M.Chart * .3,
            FillOpacity = Performance ? .24 : .02,
            LineWidth = Performance ? 1.7 : 1.3,
            Tag = "fixed"
        }, key);
        Control surface = chart;
        if (Performance)
        {
            var grid = new Grid();
            grid.Children.Add(new HistoryGrid(P.Stroke));
            grid.Children.Add(chart);
            surface = grid;
        }
        string duration = (History.Capacity * S.IntervalMs / 1000d).ToString("0", L.Culture) + " s";
        var host = Ui.Stack(3, surface, Ui.Columns("*,Auto", Ui.Text(label, 9, P.Soft), Ui.Text(key == "net.up" ? "" : duration, 9, P.Faint)));
        charts.Add((key, chart, host));
        return host;
    }

    private Control Detail(CompleteReading row)
    {
        var text = Value(row, Compact ? 12.5 : 13, row.Address ? P.Soft : P.Ink);
        var label = Ui.Text(row.Label, 10, P.Soft);
        label.TextWrapping = TextWrapping.Wrap;
        var cell = Ui.Stack(3, label, text);
        cell.Margin = new Thickness(0, 0, 9, Compact ? 4 : 7);
        if (row.Fraction.HasValue)
        {
            var bar = WidgetPresentation.Sensor(new FillBack { Height = Compact ? 3 : 5, Radius = 3, Track = P.Track }, row.Key);
            bars[row.Key] = bar;
            cell.Children.Add(bar);
        }
        return cell;
    }

    private void Build(List<CompleteReading> rows)
    {
        body.Children.Clear(); values.Clear(); units.Clear(); bars.Clear(); charts.Clear();
        string primary = WidgetSensors.PrimaryKey(Kind), secondary = WidgetSensors.SecondaryKey(Kind);
        var leads = rows.Where(r => r.Key == primary || r.Key == secondary && Kind is WidgetKind.Cpu or WidgetKind.Gpu or WidgetKind.Network).ToList();
        if (Kind == WidgetKind.Drives) leads = rows.Where(r => r.Key.StartsWith("drive:") && r.Key.EndsWith(":temp")).ToList();
        if (Kind is WidgetKind.Volumes or WidgetKind.Cooling) leads.Clear();
        if (Compact && leads.Count > 0) body.Children.Add(Rule());
        if (leads.Count > 0)
            body.Children.Add(new StatPanel(Array.Empty<double>(), leads.Select(r => Hero(r, Kind != WidgetKind.Drives && r.Key != primary)).ToArray()) { MaxColumns = 2, MinimumColumnWidth = 112 });
        if (Kind == WidgetKind.Memory && rows.FirstOrDefault(r => r.Key == primary) is { } memory && S.Shows("mem.bar"))
        {
            var bar = WidgetPresentation.Sensor(new FillBack { Height = Compact ? 5 : 8, Radius = 4, Track = P.Track }, primary);
            bars[primary] = bar; body.Children.Add(bar);
        }
        if (Kind is WidgetKind.Cpu or WidgetKind.Gpu)
            body.Children.Add(Plot(secondary, L.T("Load")));
        else if (Kind == WidgetKind.Network)
        {
            var down = Plot(primary, "↓ " + L.T("Download"));
            var up = Plot(secondary, "↑ " + L.T("Upload"));
            body.Children.Add(new StatPanel(Array.Empty<double>(), new[] { down, up }) { MaxColumns = 2, MinimumColumnWidth = 100 });
        }
        var details = rows.Except(leads).Where(r => S.Horizontal || !r.Address).ToArray();
        if (details.Length > 0)
        {
            if (Compact || Performance) body.Children.Add(Rule());
            if (Kind == WidgetKind.Drives)
            {
                foreach (var row in details.Where(r => !r.Key.StartsWith("drive:"))) body.Children.Add(Detail(row));
                var horizontalDrives = new List<Control>();
                foreach (var group in details.Where(r => r.Key.StartsWith("drive:")).GroupBy(r => r.Key[..r.Key.LastIndexOf(':')]))
                {
                    var model = group.FirstOrDefault(r => r.Key.EndsWith(":model"));
                    var metrics = new StatPanel(Array.Empty<double>(), group.Where(r => r != model).Select(Detail).ToArray())
                        { MaxColumns = 2, MinimumColumnWidth = 94, Balanced = true };
                    if (S.Horizontal)
                    {
                        var block = Ui.Stack(4);
                        if (model != null) block.Children.Add(Detail(model));
                        block.Children.Add(metrics);
                        horizontalDrives.Add(block);
                    }
                    else
                    {
                        if (model != null) body.Children.Add(Detail(model));
                        body.Children.Add(metrics);
                    }
                }
                if (S.Horizontal) body.Children.Add(new StatPanel(Array.Empty<double>(), horizontalDrives.ToArray()) { MaxColumns = 2, MinimumColumnWidth = 150, Balanced = true });
            }
            else body.Children.Add(new StatPanel(Array.Empty<double>(), details.Select(Detail).ToArray())
            {
                MaxColumns = Kind is WidgetKind.Cpu or WidgetKind.Gpu || S.Horizontal && Kind == WidgetKind.Network ? 3 : 2,
                MinimumColumnWidth = 94,
                Balanced = true,
                Tag = "elastic"
            });
        }
        if (!S.Horizontal) foreach (var address in rows.Where(r => r.Address)) body.Children.Add(Detail(address));
    }
}

internal sealed class HistoryGrid(Color stroke) : Control
{
    public override void Render(DrawingContext context)
    {
        var pen = new Pen(Palette.Brush(Palette.Alpha(stroke, .6)), 1);
        for (int i = 0; i < 4; i++)
        {
            double y = i * Math.Max(0, Bounds.Height - 1) / 3;
            context.DrawLine(pen, new Point(0, y), new Point(Bounds.Width, y));
        }
    }
}
