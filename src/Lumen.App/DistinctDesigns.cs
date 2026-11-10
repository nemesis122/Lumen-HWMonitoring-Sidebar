using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed partial class LocalDesignTile
{
    private readonly List<(string Key, LevelBar Scale)> scales = new();
    private readonly List<(string Key, FillBack Back)> ribbons = new();

    private void UpdateDistinct(List<CompleteReading> rows, (LiveMetric Primary, LiveMetric Secondary) pair)
    {
        double? Level(string key) => Kind == WidgetKind.Drives ? DriveLevel(key, rows) : key == WidgetSensors.PrimaryKey(Kind) || key == "mem.used" ? pair.Primary.Level
            : key == WidgetSensors.SecondaryKey(Kind) || key == "mem.load" ? pair.Secondary.Level ?? rows.FirstOrDefault(r => r.Key == key)?.Fraction
            : rows.FirstOrDefault(r => r.Key == key)?.Fraction;
        foreach (var scale in scales)
        {
            var level = Level(scale.Key);
            scale.Scale.IsVisible = level.HasValue;
            scale.Scale.Set(level ?? 0, ColorFor(scale.Key), P.Track);
        }
        foreach (var ribbon in ribbons)
            ribbon.Back.Animate(Level(ribbon.Key) ?? 0, Palette.Mix(P.Card, ColorFor(ribbon.Key), P.Dark ? .25 : .13));
        UpdateSignature(rows, pair);
    }

    private Control DistinctPlot(string key, double height)
    {
        var host = Plot(key);
        plots[^1].Chart.Height = S.Horizontal ? Math.Min(height, 42) : height;
        plots[^1].Chart.FillOpacity = style == WidgetStyle.Panorama ? .22 : .10;
        return host;
    }

    private Control TinyRow(CompleteReading row, bool terminal = false)
    {
        var label = Text(row.Label, terminal ? 12 : 11.5, P.Soft);
        label.Margin = new Thickness(0, 0, 8, 0);
        bool main = row.Key == WidgetSensors.PrimaryKey(Kind) || row.Key == WidgetSensors.SecondaryKey(Kind);
        var value = Value(row, terminal && main ? 22 : 14);
        value.TextAlignment = TextAlignment.Right;
        if (terminal && Ui.FontKey == "inter") label.FontFamily = value.FontFamily = Ui.FamilyFor("mono");
        return new Border
        {
              Padding = new Thickness(terminal ? 5 : 0, terminal ? 1 : 2.5),
            BorderBrush = Palette.Brush(P.Stroke), BorderThickness = new Thickness(0, 0, 0, .5),
              Child = new StableReadingRow(label, value, S, M, 76)
        };
    }

    private void DistinctDetails(List<CompleteReading> details)
    {
        var rest = details.ToList();
        if (Kind == WidgetKind.Gpu)
        {
            var memory = rest.Where(r => r.Key is "gpu.vram.used" or "gpu.vram.total").ToArray();
            if (memory.Length > 0)
            {
                var readout = Ui.Row(3);
                foreach (var row in memory)
                {
                    if (readout.Children.Count > 0) readout.Children.Add(Text("/", 12, P.Soft));
                    readout.Children.Add(Value(row));
                    rest.Remove(row);
                }
                body.Children.Add(Ui.Columns("*,Auto", Text(memory.Length == 2 ? "VRAM" : memory[0].Label, 12, P.Soft), readout));
            }
        }
        if (style == WidgetStyle.MosaicPro)
        {
            var cells = rest.Where(r => !r.Address).Select(r => (Control)new Border
            {
                Background = Palette.Brush(Palette.Mix(P.Card, ColorFor(r.Key), P.Dark ? .07 : .035)),
                Padding = new Thickness(6), CornerRadius = new CornerRadius(7), Margin = new Thickness(0, 0, 4, 3),
                Child = Fact(r)
            }).ToArray();
            if (cells.Length > 0) body.Children.Add(new StatPanel(Array.Empty<double>(), cells)
                { MaxColumns = 3, MinimumColumnWidth = 94, Balanced = true, Tag = "elastic" });
        }
        else if (rest.Any(r => !r.Address)) body.Children.Add(Facts(rest.Where(r => !r.Address)));
        foreach (var address in rest.Where(r => r.Address)) body.Children.Add(TinyRow(address));
    }

    private Control Ribbon(CompleteReading row)
    {
        var back = new FillBack { Track = P.Track, Radius = 4 };
        ribbons.Add((row.Key, back));
        var label = Text(row.Key == "net.down" ? "↓ " + L.T("Download") : row.Key == "net.up" ? "↑ " + L.T("Upload") : row.Label, 12, P.Soft);
        var number = Value(row, Kind == WidgetKind.Network ? 25 : 28);
        number.TextAlignment = TextAlignment.Right;
        var line = new Border { Padding = new Thickness(9, 3), Child = Ui.Columns("*,Auto", label, number) };
        var panel = new Panel { MinHeight = 43, Children = { back, line } };
        return panel;
    }

    private Control Surface(Control child, double pad = 7) => new Border
    {
        Padding = new Thickness(pad), Background = Palette.Brush(Palette.Mix(P.Card, Accent, P.Dark ? .055 : .025)),
        CornerRadius = new CornerRadius(8), Margin = new Thickness(0, 0, 5, 0), Child = child
    };

    private void BuildDistinct(List<CompleteReading> rows)
    {
        scales.Clear(); ribbons.Clear();
        radial.Clear(); pixels.Clear(); horizon = null;
        string primary = Kind == WidgetKind.Memory && rows.Any(r => r.Key == "mem.used") ? "mem.used" : WidgetSensors.PrimaryKey(Kind);
        string secondary = Kind == WidgetKind.Memory ? "mem.load" : WidgetSensors.SecondaryKey(Kind);
        bool pair = Kind is WidgetKind.Cpu or WidgetKind.Gpu or WidgetKind.Memory or WidgetKind.Network;
        bool charts = Kind is WidgetKind.Cpu or WidgetKind.Gpu or WidgetKind.Network;
        var leads = rows.Where(r => r.Key == primary || pair && r.Key == secondary).OrderBy(r => r.Key == primary ? 0 : 1).ToArray();
        var rest = rows.Except(leads).ToList();
        if (LocalDesigns.Signature.Contains(style))
        {
            BuildSignature(leads, rest, primary, secondary, charts);
            if (Kind == WidgetKind.Memory && S.Shows("mem.bar") && rows.FirstOrDefault(r => r.Key == "mem.load") is { } mem) body.Children.Add(Level(mem, 4));
            if (Kind == WidgetKind.Cpu) body.Children.Add(cores);
            return;
        }

        if (style == WidgetStyle.Terminal)
        {
            var list = Ui.Stack(0);
            foreach (var row in rows) list.Children.Add(TinyRow(row, true));
            body.Children.Add(list);
            AddPlot();
        }
        else if (style == WidgetStyle.Panorama && leads.Length > 0)
        {
            string series = Kind is WidgetKind.Cpu or WidgetKind.Gpu ? WidgetSensors.PrimaryKey(Kind) : primary;
            var foreground = new StatPanel(Array.Empty<double>(), leads.Select(r => Hero(r, r.Key != primary)).ToArray())
                { MaxColumns = 2, MinimumColumnWidth = 108, Balanced = true };
            var landscape = Ui.Stack(0, foreground);
            if (charts) landscape.Children.Add(DistinctPlot(series, 32));
            body.Children.Add(landscape);
            DistinctDetails(rest);
            // Temperature and load histories are independently configurable.
            if (Kind is WidgetKind.Cpu or WidgetKind.Gpu) body.Children.Add(DistinctPlot(secondary, 16));
            if (Kind == WidgetKind.Network) body.Children.Add(DistinctPlot(secondary, 20));
        }
        else if (style == WidgetStyle.Thermal && leads.Length > 0)
        {
            var instruments = leads.Select(row =>
            {
                var scale = WidgetPresentation.Sensor(new LevelBar { Width = 10, Height = 71, Margin = new Thickness(0, 0, 8, 0), Tag = "graphic" }, row.Key);
                scales.Add((row.Key, scale));
                return (Control)Surface(Ui.Columns("Auto,*", scale, Hero(row, row.Key != primary)), 5);
            }).ToArray();
            body.Children.Add(new StatPanel(Array.Empty<double>(), instruments) { MaxColumns = 2, MinimumColumnWidth = 115, Balanced = true });
            DistinctDetails(rest); AddPlot();
        }
        else if (style == WidgetStyle.MosaicPro && leads.Length > 0)
        {
            var main = Surface(Hero(leads[0], false), 7);
            main.MinHeight = 89;
            var right = Ui.Stack(4);
            if (leads.Length > 1) right.Children.Add(Surface(Hero(leads[1], true), 4));
            if (charts) right.Children.Add(DistinctPlot(secondary, 25));
            body.Children.Add(new StatPanel(new[] { 1.35, 1d }, new Control[] { main, right })
                { MaxColumns = 2, MinimumColumnWidth = 128 });
            DistinctDetails(rest);
            if (Kind == WidgetKind.Network) body.Children.Add(DistinctPlot(primary, 20));
        }
        else if (style == WidgetStyle.RibbonFlow && leads.Length > 0)
        {
            body.Children.Add(Ui.Stack(4, leads.Select(Ribbon).ToArray()));
            DistinctDetails(rest); AddPlot();
        }
        else if (style == WidgetStyle.SplitLens && leads.Length > 0)
        {
            var columns = new List<Control>();
            for (int i = 0; i < leads.Length; i++)
            {
                var column = Ui.Stack(5, Hero(leads[i], i > 0));
                if (charts) column.Children.Add(DistinctPlot(leads[i].Key, 23));
                foreach (var detail in rest.Where((r, j) => j % leads.Length == i)) column.Children.Add(TinyRow(detail));
                columns.Add(Surface(column, 6));
            }
            body.Children.Add(new EqualPairPanel(columns.ToArray(), (Kind == WidgetKind.Network ? 126 : 112)
                * Math.Max(1, Math.Max(S.ActiveWidget?.Values ?? 1, S.ActiveWidget?.Labels ?? 1))) { Tag = "elastic" });
        }
        else DistinctDetails(rows);
        if (Kind == WidgetKind.Memory && style != WidgetStyle.RibbonFlow && S.Shows("mem.bar") && rows.FirstOrDefault(r => r.Key == "mem.load") is { } memory)
            body.Children.Add(Level(memory, 4));
        if (Kind == WidgetKind.Cpu) body.Children.Add(cores);
    }
}
