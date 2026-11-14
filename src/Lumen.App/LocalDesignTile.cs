using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

// Local additions; the discarded prototypes have no renderer or picker entry.
public static class LocalDesigns
{
    public static readonly WidgetStyle[] Styles =
    {
        WidgetStyle.Meridian, WidgetStyle.Strata, WidgetStyle.Vector,
        WidgetStyle.Orbit, WidgetStyle.Lucent, WidgetStyle.Circuit,
        WidgetStyle.Panorama, WidgetStyle.Terminal, WidgetStyle.Thermal,
        WidgetStyle.MosaicPro, WidgetStyle.RibbonFlow, WidgetStyle.SplitLens,
        WidgetStyle.PixelDeck, WidgetStyle.HorizonSync
    };
    public static readonly WidgetStyle[] Distinct =
    {
        WidgetStyle.Panorama, WidgetStyle.Terminal, WidgetStyle.Thermal,
        WidgetStyle.MosaicPro, WidgetStyle.RibbonFlow, WidgetStyle.SplitLens,
        WidgetStyle.PixelDeck, WidgetStyle.HorizonSync
    };
    public static readonly WidgetStyle[] Signature = { WidgetStyle.PixelDeck, WidgetStyle.HorizonSync };
    public static bool Contains(WidgetStyle style) => Styles.Contains(style);

    public static void Frame(Border frame, WidgetStyle style, Palette p, Metrics m)
    {
        frame.Padding = new Thickness(m.Pad, 12);
        frame.Background = Palette.Brush(p.Card);
        frame.BorderBrush = Palette.Brush(p.Stroke);
        frame.BorderThickness = new Thickness(style is WidgetStyle.Vector or WidgetStyle.Lucent or WidgetStyle.Circuit ? 1 : 0);
        frame.CornerRadius = new CornerRadius(style switch
        {
            WidgetStyle.Meridian => 14, WidgetStyle.Strata => 18, WidgetStyle.Vector => 4,
            WidgetStyle.Orbit => 22, WidgetStyle.Lucent => 18,
            WidgetStyle.Panorama => 16, WidgetStyle.Terminal => 0, WidgetStyle.Thermal => 8,
            WidgetStyle.MosaicPro => 14, WidgetStyle.RibbonFlow => 5, WidgetStyle.SplitLens => 12,
            WidgetStyle.Aperture => 24, WidgetStyle.PixelDeck => 4, WidgetStyle.HorizonSync => 16, _ => 10
        });
        if (style == WidgetStyle.Vector) frame.BorderThickness = new Thickness(2, 1, 1, 1);
        if (style == WidgetStyle.Strata) frame.Background = Palette.Brush(p.DesignBack("#151d25"));
        if (style == WidgetStyle.Circuit) frame.Background = Palette.Brush(p.DesignBack("#101c24"));
        if (style == WidgetStyle.Panorama) frame.Background = Palette.Brush(p.DesignBack("#131c2b"));
        if (style == WidgetStyle.Terminal)
        {
            frame.Background = Palette.Brush(p.DesignBack("#101b17"));
            frame.BorderThickness = new Thickness(1);
        }
        if (style == WidgetStyle.RibbonFlow) frame.BorderThickness = new Thickness(0, 1, 0, 1);
        if (style == WidgetStyle.SplitLens) frame.BorderThickness = new Thickness(1);
        if (style == WidgetStyle.Aperture) frame.Background = Palette.Brush(p.DesignBack("#121c24"));
        if (style == WidgetStyle.PixelDeck) frame.BorderThickness = new Thickness(1);
        if (style == WidgetStyle.HorizonSync) frame.Background = Palette.Brush(p.DesignBack("#141b28"));
    }
}

public sealed partial class LocalDesignTile : SummaryTile
{
    private readonly WidgetStyle style;
    private readonly TextBlock title, device, headerValue;
    private readonly ContentControl symbol;
    private readonly StackPanel body;
    private readonly Dictionary<string, TextBlock> values = new();
    private readonly Dictionary<string, TextBlock> units = new();
    private readonly List<(string Key, AreaChart Chart, Control Host)> plots = new();
    private readonly List<(string Key, FillBack Bar)> levels = new();
    private readonly List<(string Key, CompactOrbit Dial)> dials = new();
    private readonly Skyline cores;
    private FillBack? vram;
    private string shape = "", glyph = "";
    private Color Accent => Accents.For(Kind, Slot ?? S.ActiveWidget, P);
    public IReadOnlyList<string> RenderedKeys { get; private set; } = Array.Empty<string>();

    public LocalDesignTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s, WidgetStyle style)
        : base(kind, index, p, m, s, false)
    {
        this.style = style;
        LocalDesigns.Frame(this, style, p, m);
        if (kind == WidgetKind.Drives) Padding = new Thickness(m.Pad, 8);
        if (style == WidgetStyle.Lucent)
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(p.Card, 0), new GradientStop(Palette.Mix(p.Card, Accent, .1), 1) }
            };
        title = Text("", 14, p.Ink, FontWeight.SemiBold);
        device = Text("", 12, p.Soft);
        HardwareWidgetTitle.Register(this, title, device);
        headerValue = Ui.Text("", 13, p.Ink, FontWeight.SemiBold, numbers: true);
        headerValue.TextTrimming = TextTrimming.None;
        headerValue.Margin = new Thickness(8, 0, 0, 0);
        symbol = new ContentControl { Width = 17, Height = 17, VerticalAlignment = VerticalAlignment.Center };
        var badge = new Border
        {
            Width = 27, Height = 27, CornerRadius = new CornerRadius(style == WidgetStyle.Vector ? 5 : style == WidgetStyle.Orbit ? 14 : 8),
            Background = Palette.Brush(Palette.Mix(p.Card, Accent, .13)), Child = symbol,
            Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center
        };
        var heading = Ui.Columns("Auto,*,Auto", badge, title, headerValue);
        Control head = Ui.Stack(3, heading, device);
        if (style == WidgetStyle.Circuit)
        {
            title.LetterSpacing = .5;
            head = new Border { BorderBrush = Palette.Brush(Accent), BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(0, 0, 0, 7), Child = head };
        }
        if (style == WidgetStyle.Terminal && Ui.FontKey == "inter")
            title.FontFamily = device.FontFamily = headerValue.FontFamily = Ui.FamilyFor("mono");
        body = Ui.Stack(kind == WidgetKind.Drives ? 2 : 7);
        body.Tag = "elastic";
        cores = WidgetPresentation.Sensor(new Skyline { Height = s.Horizontal ? 12 : 14, Palette = p }, "cpu.cores");
        ToolTip.SetTip(cores, L.T("Core bars"));
        Child = Ui.Stack(kind == WidgetKind.Drives ? 4 : 8, head, body);
        Child.Tag = "elastic";
    }

    private static TextBlock Text(string text, double size, Color color, FontWeight? weight = null)
    {
        var result = Ui.Text(text, size, color, weight ?? FontWeight.Normal);
        result.TextWrapping = TextWrapping.Wrap;
        result.TextTrimming = TextTrimming.None;
        return result;
    }

    protected override void Show(Summary summary)
    {
        title.Text = Naming.Title(Slot, Index, summary.Title);
        device.Text = Naming.Hardware(Slot, Index, summary.Name);
        device.IsVisible = Naming.ShowHardware(Slot, S) && device.Text.Length > 0;
        if (glyph != summary.Glyph) { glyph = summary.Glyph; symbol.Content = Ui.Glyph(glyph, Accent, 16); }
        var rows = CompleteReadings.Body(Kind, Index, Last.Snap, Last.History, S, P, style);
        RenderedKeys = rows.Select(r => r.Key).ToArray();
        string next = string.Join("\n", rows.Select(r => r.Key + "|" + r.Label));
        if (next != shape) { shape = next; Build(rows); }
        foreach (var row in rows)
        {
            if (!values.TryGetValue(row.Key, out var number)) continue;
            if (units.TryGetValue(row.Key, out var unit))
            {
                var parts = TwinFocusMetrics.Split(row.Value);
                number.Text = parts.Value; unit.Text = parts.Unit;
            }
            else number.Text = row.Value;
            ToolTip.SetTip(number, row.Label + ": " + row.Value);
        }
        var pair = TwinFocusMetrics.Pair(Kind, Index, summary, Last.Snap, Last.History, S);
        foreach (var plot in plots)
        {
            var metric = plot.Key == WidgetSensors.PrimaryKey(Kind) ? pair.Primary : pair.Secondary;
            bool visible = RenderedKeys.Contains(plot.Key) && S.Shows(plot.Key + ".history") && metric.Series.Any(double.IsFinite)
                && (Summaries.ChartKey(Kind) is not string group || S.Shows(group));
            plot.Host.IsVisible = plot.Chart.IsVisible = visible;
            plot.Chart.Max = metric.Maximum;
            plot.Chart.Set(visible ? metric.Series : Array.Empty<double>(), ColorFor(plot.Key));
        }
        foreach (var level in levels)
        {
            var row = rows.First(r => r.Key == level.Key);
            double? fraction = row.Fraction;
            if (level.Key == "net.down") fraction = pair.Primary.Level;
            if (level.Key == "net.up") fraction = pair.Secondary.Level;
            level.Bar.IsVisible = fraction.HasValue;
            level.Bar.Animate(fraction ?? 0, ColorFor(level.Key));
        }
        foreach (var dial in dials)
            dial.Dial.Set(rows.First(r => r.Key == dial.Key).Fraction ?? 0, ColorFor(dial.Key), P.Track);
        if (vram != null)
        {
            var gpu = GpuGroup.Visible(Last.Snap, S).ElementAtOrDefault(Index);
            double fraction = gpu?.MemoryTotalMb is > 0 ? (gpu.MemoryUsedMb ?? 0) / gpu.MemoryTotalMb.Value : 0;
            vram.Animate(fraction, Accent);
        }
        cores.Loads = Last.Snap.Cpu?.CoreLoads ?? Array.Empty<double>();
        cores.IsVisible = Kind == WidgetKind.Cpu && S.Shows("cpu.cores") && cores.Loads.Count > 0;
        cores.InvalidateVisual();
        if (LocalDesigns.Distinct.Contains(style)) UpdateDistinct(rows, pair);
        if (Kind == WidgetKind.Drives) UpdateDrivePlots(rows);
    }

    private Color ColorFor(string key) => key == "net.up" ? Accents.Readable(Color.Parse(Accents.Upload), P) : Accent;

    private TextBlock Value(CompleteReading row, double size = 14, bool reserve = false)
    {
        var model = Ui.Text("", size, P.Ink, FontWeight.SemiBold, numbers: true);
        var text = WidgetPresentation.Sensor(reserve ? new ReservedNumberText(model, row.Key is "net.down" or "net.up" or "mem.used" ? "888.8" : "888") : model, row.Key);
        text.TextWrapping = reserve ? TextWrapping.NoWrap : TextWrapping.Wrap;
        text.TextTrimming = TextTrimming.None;
        text.TextAlignment = TextAlignment.Left;
        text.Tag = "local-value:" + row.Key;
        values.Add(row.Key, text);
        return text;
    }

    private FillBack Level(CompleteReading row, double height = 4)
    {
        var bar = WidgetPresentation.Sensor(new FillBack { Height = height, Radius = height / 2, Track = P.Track }, row.Key);
        levels.Add((row.Key, bar));
        return bar;
    }

    private Control Hero(CompleteReading row, bool secondary)
    {
        bool rate = Kind == WidgetKind.Network;
        bool orbit = style == WidgetStyle.Orbit && secondary && Kind is WidgetKind.Cpu or WidgetKind.Gpu or WidgetKind.Memory;
        double size = rate ? 27 : style == WidgetStyle.SplitLens ? Kind == WidgetKind.Memory ? 32 : 36
            : row.Key == "mem.used" ? 32 : style == WidgetStyle.Vector ? 36 : secondary ? 28 : 40;
        var number = Value(row, size, !orbit);
        var unit = Text("", rate ? 12 : 14, P.Soft, FontWeight.Medium);
        unit.VerticalAlignment = VerticalAlignment.Bottom;
        unit.Margin = new Thickness(0, 0, 0, 4);
        units.Add(row.Key, unit);
        if (style is WidgetStyle.Vector or WidgetStyle.Lucent or WidgetStyle.Circuit) number.Foreground = Palette.Brush(ColorFor(row.Key));
        var readout = Ui.Row(2, number, unit);
        var caption = Text(row.Key == "net.down" ? "↓ " + L.T("Download") : row.Key == "net.up" ? "↑ " + L.T("Upload") : row.Label, 11.5, P.Soft);
        var content = Ui.Stack(1, caption, readout);
        if (orbit)
        {
            var dial = new CompactOrbit { Width = 73, Height = 73, Tag = "graphic", HorizontalAlignment = HorizontalAlignment.Center };
            dials.Add((row.Key, dial));
            number.FontSize = 24;
            unit.FontSize = 12;
            caption.FontSize = 11;
            caption.TextAlignment = TextAlignment.Center;
            caption.HorizontalAlignment = HorizontalAlignment.Center;
            readout.HorizontalAlignment = HorizontalAlignment.Center;
            readout.Orientation = Orientation.Vertical;
            readout.Spacing = 0;
            number.HorizontalAlignment = HorizontalAlignment.Center;
            unit.HorizontalAlignment = HorizontalAlignment.Center;
            unit.Margin = new Thickness(0);
            unit.FontSize = 10;
            number.TextAlignment = TextAlignment.Center;
            content.HorizontalAlignment = HorizontalAlignment.Center;
            content.VerticalAlignment = VerticalAlignment.Center;
            var face = new Grid { Children = { dial, content } };
            return face;
        }
        return new Border
        {
            Padding = new Thickness(style == WidgetStyle.Lucent ? 8 : 0, 3, 5, 3),
            Background = style == WidgetStyle.Lucent ? Palette.Brush(Palette.Mix(P.Card, Accent, .055)) : Brushes.Transparent,
            CornerRadius = new CornerRadius(10), Child = content
        };
    }

    private Control Fact(CompleteReading row)
    {
        var label = Text(row.Label, 11.5, P.Soft);
        var value = Value(row);
        value.TextWrapping = TextWrapping.NoWrap;
        var fitted = SteadyHeight.Of(new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = HorizontalAlignment.Left, Child = value });
        var content = Ui.Stack(2, label, fitted);
        return new Border
        {
            Padding = new Thickness(style == WidgetStyle.Lucent ? 6 : 0, 3, 5, 3),
            Margin = new Thickness(0, 0, 3, 0),
            Background = style == WidgetStyle.Lucent ? Palette.Brush(Palette.Mix(P.Card, P.Ink, .035)) : Brushes.Transparent,
            BorderBrush = Palette.Brush(P.Stroke),
            BorderThickness = style is WidgetStyle.Strata or WidgetStyle.Vector ? new Thickness(0, 1, 0, 0) : new Thickness(0),
            CornerRadius = new CornerRadius(7), Child = content
        };
    }

    private Control Facts(IEnumerable<CompleteReading> rows, int columns = 3) => new StatPanel(Array.Empty<double>(), rows.Select(Fact).ToArray())
    {
        MaxColumns = columns, MinimumColumnWidth = 88, Balanced = true, Tag = "elastic"
    };

    private Control Plot(string key)
    {
        var chart = WidgetPresentation.Sensor(new AreaChart
        {
            Height = S.Horizontal ? 18 : Kind == WidgetKind.Network ? 32 : style == WidgetStyle.Circuit ? 25 : 20,
            FillOpacity = style is WidgetStyle.Strata or WidgetStyle.Lucent ? .13 : .025,
            LineWidth = 1.5, Tag = "fixed"
        }, key);
        Control host = chart;
        if (style == WidgetStyle.Circuit)
            host = new Grid { Children = { new HistoryGrid(P.Stroke), chart } };
        ToolTip.SetTip(host, (key == "net.up" ? L.T("Upload") : key == "net.down" ? L.T("Download")
            : key == WidgetSensors.PrimaryKey(Kind) && Kind is WidgetKind.Cpu or WidgetKind.Gpu ? L.T("Temp") : L.T("Load")) + " · " + (History.Capacity * S.IntervalMs / 1000) + " s");
        plots.Add((key, chart, host));
        return host;
    }

    private void AddPlot()
    {
        if (Kind is WidgetKind.Cpu or WidgetKind.Gpu) body.Children.Add(Plot(WidgetSensors.SecondaryKey(Kind)));
        if (Kind == WidgetKind.Network)
            body.Children.Add(new StatPanel(Array.Empty<double>(), new[] { Plot("net.down"), Plot("net.up") })
                { MaxColumns = 2, MinimumColumnWidth = 110, Balanced = true });
    }

    private void BuildDrives(List<CompleteReading> rows)
    {
        BuildStyledDrives(rows);
    }

    private void Build(List<CompleteReading> rows)
    {
        body.Children.Clear(); values.Clear(); units.Clear(); plots.Clear(); levels.Clear(); dials.Clear(); vram = null;
        string headerKey = Kind == WidgetKind.Cpu ? "cpu.clock" : Kind == WidgetKind.Gpu ? "gpu.clock" : Kind == WidgetKind.Drives ? "drives.count" : Kind == WidgetKind.Network ? "net.link" : "";
        var header = rows.FirstOrDefault(r => r.Key == headerKey);
        headerValue.IsVisible = header != null;
        if (header != null)
        {
            values.Add(header.Key, headerValue);
            headerValue.Tag = "local-value:" + header.Key;
        }
        else headerValue.Tag = null;
        var remaining = rows.Where(r => r != header).ToList();
        if (Kind == WidgetKind.Drives) { BuildDrives(remaining); return; }
        if (LocalDesigns.Distinct.Contains(style)) { BuildDistinct(remaining); return; }
        string primary = Kind == WidgetKind.Memory && remaining.Any(r => r.Key == "mem.used") ? "mem.used" : WidgetSensors.PrimaryKey(Kind);
        string secondary = Kind == WidgetKind.Memory ? "mem.load" : WidgetSensors.SecondaryKey(Kind);
        bool twin = Kind is WidgetKind.Cpu or WidgetKind.Gpu or WidgetKind.Network or WidgetKind.Memory;
        var leads = remaining.Where(r => r.Key == primary || twin && r.Key == secondary).OrderBy(r => r.Key == primary ? 0 : 1).ToArray();
        foreach (var lead in leads) remaining.Remove(lead);
        if (style == WidgetStyle.Meridian && Kind is WidgetKind.Cpu or WidgetKind.Gpu)
        {
            var left = Ui.Stack(3, leads.Select(r => Hero(r, r.Key != primary)).ToArray());
            var right = Ui.Stack(3);
            foreach (var row in remaining)
            {
                var label = Text(row.Label, 11.5, P.Soft);
                label.Margin = new Thickness(0, 0, 6, 0);
                var number = Value(row);
                number.TextAlignment = TextAlignment.Right;
                var line = Ui.Columns("*,Auto", label, number);
                right.Children.Add(new Border { Padding = new Thickness(0, 3), BorderThickness = new Thickness(0, 0, 0, .5), BorderBrush = Palette.Brush(P.Stroke), Child = line });
            }
            body.Children.Add(new StatPanel(new[] { 1d, 1.35 }, new Control[] { left, right })
                { MaxColumns = 2, MinimumColumnWidth = 96 });
            AddPlot();
            if (Kind == WidgetKind.Cpu) body.Children.Add(cores);
            return;
        }
        if (leads.Length > 0)
        {
            Control lead = new StatPanel(Array.Empty<double>(), leads.Select(r => Hero(r, r.Key != primary)).ToArray())
                { MaxColumns = 2, MinimumColumnWidth = 108, Balanced = true };
            if (style == WidgetStyle.Strata)
                lead = new Border { Background = Palette.Brush(Palette.Mix(P.Card, Accent, .065)), Padding = new Thickness(8, 2), CornerRadius = new CornerRadius(8), Child = lead };
            body.Children.Add(lead);
        }
        if (Kind == WidgetKind.Memory && S.Shows("mem.bar") && rows.FirstOrDefault(r => r.Key == "mem.load") is { } memory)
            body.Children.Add(Level(memory, 5));
        if (style is WidgetStyle.Strata or WidgetStyle.Circuit) AddPlot();
        if (Kind == WidgetKind.Gpu)
        {
            var gpuMemory = remaining.Where(r => r.Key is "gpu.vram.used" or "gpu.vram.total").ToArray();
            if (gpuMemory.Length > 0)
            {
                var parts = Ui.Row(3);
                foreach (var row in gpuMemory)
                {
                    if (parts.Children.Count > 0) parts.Children.Add(Text("/", 12, P.Soft));
                    parts.Children.Add(Value(row));
                    remaining.Remove(row);
                }
                var block = Ui.Stack(3, Ui.Columns("*,Auto", Text(gpuMemory.Length == 1 ? gpuMemory[0].Label : "VRAM", 12, P.Soft), parts));
                if (gpuMemory.Length == 2)
                {
                    vram = new FillBack { Height = 3, Radius = 2, Track = P.Track };
                    block.Children.Add(vram);
                }
                body.Children.Add(block);
            }
        }
        var ordinary = remaining.Where(r => !r.Address).ToArray();
        if (ordinary.Length > 0) body.Children.Add(Facts(ordinary));
        foreach (var row in remaining.Where(r => r.Address))
        {
            var value = Value(row, 12);
            value.TextAlignment = TextAlignment.Right;
            body.Children.Add(Ui.Columns("*,*", Text(row.Label, 11.5, P.Soft), value));
        }
        if (style is not (WidgetStyle.Strata or WidgetStyle.Circuit)) AddPlot();
        if (Kind == WidgetKind.Cpu) body.Children.Add(cores);
    }
}

internal sealed class CompactOrbit : Control
{
    private double fraction;
    private Color color, track;
    public void Set(double value, Color accent, Color background)
    {
        fraction = Math.Clamp(double.IsFinite(value) ? value : 0, 0, 1); color = accent; track = background; InvalidateVisual();
    }
    public override void Render(DrawingContext context)
    {
        double radius = Math.Min(Bounds.Width, Bounds.Height) / 2 - 3;
        if (radius < 1) return;
        Draw(1, track);
        if (fraction > 0) Draw(fraction, color);
        void Draw(double part, Color stroke)
        {
            var path = new StreamGeometry();
            using (var geometry = path.Open())
            {
                for (int i = 0; i <= 60; i++)
                {
                    double angle = (135 + 270 * part * i / 60) * Math.PI / 180;
                    var point = new Point(Bounds.Width / 2 + radius * Math.Cos(angle), Bounds.Height / 2 + radius * Math.Sin(angle));
                    if (i == 0) geometry.BeginFigure(point, false); else geometry.LineTo(point);
                }
                geometry.EndFigure(false);
            }
            context.DrawGeometry(null, new Pen(Palette.Brush(stroke), 3, lineCap: PenLineCap.Round), path);
        }
    }
}
