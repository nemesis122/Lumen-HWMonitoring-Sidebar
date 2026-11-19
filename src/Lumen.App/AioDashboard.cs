using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Lumen.Core;

namespace Lumen.App;

public sealed partial class AioTile
{
    private sealed record DashboardPart(string Id, string Title, string Model, List<AioReading> Rows);
    private readonly Dictionary<string, (double Min, double Max)> dashboardExtrema = new();
    private readonly List<(string Key, TextBlock Min, TextBlock Max)> dashboardLimits = new();
    private DashboardTrafficPlot? dashboardTraffic;
    private Control? dashboardLayout;
    private double dashboardNaturalHeight;
    public bool IsDashboard => AioDesigns.Dashboard(style);
    public Control DashboardSizingControl => dashboardLayout ?? this;
    public double DashboardHeightGrowth { get; private set; }
    public void ResetDashboardHeight()
    {
        if (dashboardLayout == null || DashboardHeightGrowth == 0) return;
        dashboardLayout.Height = double.NaN; DashboardHeightGrowth = 0;
    }
    public void GrowDashboard(double extra)
    {
        if (dashboardLayout == null) return;
        if (DashboardHeightGrowth == 0) dashboardNaturalHeight = dashboardLayout.DesiredSize.Height;
        DashboardHeightGrowth = Math.Max(0, extra);
        dashboardLayout.Height = dashboardNaturalHeight + DashboardHeightGrowth;
    }
    private static readonly Lazy<Bitmap> dashboardCase = new(() => new Bitmap(AssetLoader.Open(new Uri("avares://Lumen/Assets/Aio/Dashboard/case.png"))));
    private Color DashboardAccent => Color.TryParse(slot?.Accent, out var custom) ? custom : skin ?? C("#35D5E4");

    private void SampleDashboard(IEnumerable<AioReading> rows, History h)
    {
        foreach (var row in rows)
        {
            var data = dashboardExtrema.ContainsKey(row.Key) || row.History.Length == 0 ? Array.Empty<double>() : h.Read(row.History);
            foreach (var n in data.Append(row.Number ?? double.NaN).Where(double.IsFinite))
                dashboardExtrema[row.Key] = dashboardExtrema.TryGetValue(row.Key, out var old)
                    ? (Math.Min(old.Min, n), Math.Max(old.Max, n)) : (n, n);
        }
    }

    private string DashboardExtreme(AioReading r, double value)
    {
        if (r.Key.StartsWith("aio.system.ram.")) return Format.Gigabytes(value);
        if (r.Key == "aio.system.ram" || r.Key.EndsWith(".load") || r.Key.EndsWith(".used") || r.Key.EndsWith(".life") || r.Key.EndsWith(".loss") || r.Key.EndsWith(".activity")) return Format.Percent(value);
        if (r.Group is "Board" or "Cores" || r.Key.EndsWith(".temp") || r.Key.EndsWith(".hotspot") || r.Key.EndsWith(".junction") || r.Key.StartsWith("aio.system.disk."))
            return Format.Temperature(value, settings.Fahrenheit) + " " + Format.TemperatureUnit(settings.Fahrenheit);
        if (r.Group == "Cooling") return Format.Rpm(value);
        if (r.Group == "Rails" || r.Key.EndsWith(".voltage")) return Format.Volts(value);
        if (r.Key.EndsWith(".clock")) return Format.Clock(value);
        if (r.Key.EndsWith(".power")) return Format.Watts(value);
        if (r.Key.EndsWith(".vram")) return Format.Gigabytes(value / 1024);
        if (r.Key.EndsWith(".read") || r.Key.EndsWith(".write")) return Format.Rate(value);
        if (r.Key is "aio.network.down" or "aio.network.up") return Format.Bits(value * 8);
        if (r.Key.EndsWith(".link")) return Format.Link(value);
        if (r.Key.EndsWith(".ping") || r.Key.EndsWith(".jitter")) return Format.Milliseconds(value);
        if (r.Key.EndsWith(".hours")) return Format.Number(value) + " h";
        return Format.Number(value);
    }

    private void UpdateDashboard()
    {

        if(CaseEditionDesigns.Contains(style))UpdateCaseEditionValues();
        foreach (var (key, min, max) in dashboardLimits)
        {
            if (readings.TryGetValue(key, out var row) && dashboardExtrema.TryGetValue(key, out var limits))
            {
                min.Text = DashboardExtreme(row, limits.Min); max.Text = DashboardExtreme(row, limits.Max);
            }
            else min.Text = max.Text = "—";
        }
        double[] Series(string key, string fallback) => history.Read(key) is { Length: > 0 } saved ? saved : samples.Read(fallback);
        dashboardTraffic?.Set(Series("net.down", "aio.network.down"), Series("net.up", "aio.network.up"));
    }

    private List<DashboardPart> DashboardParts(List<AioReading> rows)
    {
        var result = new List<DashboardPart>();
        void Add(string id, string name, WidgetKind kind, string model, Func<AioReading, bool> filter)
        {
            var selected = rows.Where(filter).ToList(); if (selected.Count == 0) return;
            var owner = source.Widgets.FirstOrDefault(w => w.Kind == kind);
            string title = owner?.ShowTitle == false ? "" : Naming.Title(owner, 0, name);
            result.Add(new(id, title, PartModel(kind, 0, model), selected));
        }
        Add("cpu", "CPU", WidgetKind.Cpu, snapshot.Cpu?.Name ?? "", r => r.Group is "CPU" or "Cores");
        Add("gpu", "GPU", WidgetKind.Gpu, string.Join(" · ", snapshot.Gpus.Where(g => rows.Any(r => r.Group == "GPU" && r.Device == g.Id)).Select(g => g.Name)), r => r.Group == "GPU");
        Add("ram", "RAM", WidgetKind.Memory, L.T("Memory"), r => r.Key == "aio.system.ram" || r.Key.StartsWith("aio.system.ram."));
        Add("ssd", L.T("Drives"), WidgetKind.Drives, string.Join(" · ", snapshot.Disks.Where(d => rows.Any(r => r.Device == d.Id)).Select(d => d.Name)), r => r.Group == "Drives" || r.Key.StartsWith("aio.system.disk."));
        Add("fan", L.T("Cooling"), WidgetKind.Cooling, snapshot.BoardName ?? "", r => r.Group is "Cooling" or "Board");
        Add("power", L.T("Voltages"), WidgetKind.Power, "", r => r.Group == "Rails");
        Add("net", L.T("Network"), WidgetKind.Network, snapshot.Network?.Name ?? "", r => r.Group == "Network");
        var remaining = rows.Except(result.SelectMany(p => p.Rows)).ToList();
        if (remaining.Count > 0) result.Add(new("other", L.T("Sensors"), "", remaining));
        return result;
    }

    private string DashboardLabel(AioReading row)
    {
        if (row.Key == "aio.system.ram") return L.T("Load");
        string label = row.Key.StartsWith("aio.system.disk.") ? L.T("Temperature") : L.T(row.Label);
        if (row.Group == "Drives" || row.Key.StartsWith("aio.system.disk.")) return AioReadings.DriveName(snapshot, row.Device) + " · " + label;
        if (row.Group == "GPU")
        {
            var main = GpuGroup.Main(GpuGroup.All(snapshot));
            if (row.Device != main?.Id) label = (snapshot.Gpus.FirstOrDefault(g => g.Id == row.Device)?.Integrated == true ? "iGPU" : "GPU 2") + " · " + label;
        }
        return label;
    }

    private Control DashboardImage(string id, double width = 54, double height = 42)
    {
        if (id == "net") return Part("router", width, height);
        if (id == "other") return Ui.Glyph(Glyphs.Cpu, DashboardAccent, 32);
        PixelRect rect = id switch
        {
            "cpu" => new(473, 256, 223, 238), "gpu" => new(29, 654, 853, 331),
            "ram" => new(810, 90, 300, 505), "ssd" => new(370, 1038, 395, 95),
            "fan" => new(136, 692, 290, 260), _ => new(1044, 47, 154, 130)
        };
        return new Image { Source = new CroppedBitmap(mapArtwork.Value, rect), Width = width, Height = height, Stretch = Stretch.Uniform, Tag = "graphic" };
    }

    private TextBlock DashboardText(string text, double size = 11.5, bool title = false)
    {
        var result = Text(text, size, title ? DashboardAccent : muted);
        result.TextWrapping = TextWrapping.NoWrap; result.TextTrimming = TextTrimming.CharacterEllipsis;
        TextFonts.Mark(result, title ? TextRole.Title : TextRole.Label);
        return result;
    }
    private Control DashboardHeader(DashboardPart part, bool picture = true)
    {
        var names = Stack(2);
        if (part.Title.Length > 0) names.Children.Add(DashboardText(part.Title, 14, true));
        if (part.Model.Length > 0) names.Children.Add(DashboardText(part.Model, 10.5));
        return picture ? Ui.Columns("54,*", DashboardImage(part.Id), names) : names;
    }
    private Border DashboardFrame(Control body) => new()
    {
        Child = body, Background = Palette.Brush(SeeThrough(Palette.Mix(back, ink, .025))), Padding = new Thickness(10),
        CornerRadius = new CornerRadius(7), BorderBrush = Palette.Brush(track), BorderThickness = new Thickness(1), Tag = "aio-section"
    };

    private Control DashboardRow(AioReading row, bool limits)
    {
        var label = DashboardText(DashboardLabel(row)); ToolTip.SetTip(label, DashboardLabel(row));
        var value = V(row, 12.5, ink); value.TextWrapping = TextWrapping.NoWrap;
        if (!limits) return new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 }.WithDashboard(label, FitRight(value));
        var min = Text("—", 11, muted, true); var max = Text("—", 11, muted, true);
        min.Tag = "dashboard-min:" + row.Key; max.Tag = "dashboard-max:" + row.Key;
        ToolTip.SetTip(min, L.T("Minimum observed value")); ToolTip.SetTip(max, L.T("Maximum observed value"));
        dashboardLimits.Add((row.Key, min, max));
        return new DashboardTableRow(label, FitRight(value), FitRight(min), FitRight(max));
    }

    private Control DashboardSection(DashboardPart part, bool limits = false, bool rail = false)
    {
        var body = new DashboardVertical(5); body.Add(DashboardHeader(part, !rail), false);
        if (limits) body.Add(Ui.Columns("1.25*,1*,.8*,.8*", DashboardText(L.T("Sensor"), 10.5), DashboardText(L.T("Current"), 10.5), DashboardText("Min", 10.5), DashboardText("Max", 10.5)), false);
        foreach (var row in part.Rows) body.Add(DashboardRow(row, limits));
        if (part.Id == "cpu" && lastCoreLoads.Count >= 2 && AioReadings.Selected(settings, "aio.cpu.coreloads"))
        {
            coreBars = new AioCoreBars(ink, muted, track) { Loads = lastCoreLoads, Tint = DashboardAccent };
            body.Add(coreBars, false);
        }
        var frame = DashboardFrame(rail ? Ui.Columns("48,*", DashboardImage(part.Id, 48, 58), body) : body);
        frame.Tag = "dashboard-part:" + part.Id; return frame;
    }
    private Control DashboardStretch(params Control[] items) => new DashboardVertical(7, items);
    private Control DashboardStack(IEnumerable<DashboardPart> parts, bool limits = false) => DashboardStretch(parts.Select(p => DashboardSection(p, limits)).ToArray());
    private Control DashboardPair(Control first, Control second, string tag) => new DashboardColumns(first, second, source.Columns > 1 ? 2 : 1) { Tag = tag };
    private Control DashboardFlow(List<DashboardPart> parts, int columns, bool limits = false, bool rail = false) =>
        new DashboardFlow(columns, parts.Select(p => (DashboardSection(p, limits, rail), (double)p.Rows.Count + 4)).ToArray());
    private Control DashboardTop(Control top, Control fill)
    { var panel = new DashboardVertical(7); panel.Add(top, false); panel.Add(fill); return panel; }
    private Control DashboardArtwork(bool casePlan, List<DashboardPart> parts, bool summary)
    {
        var body = new DashboardVertical(8);
        body.Add(DashboardText(casePlan ? "Case overview" : "Mainboard map", 14, true), false);
        body.Add(new DashboardBoardPicture(casePlan ? dashboardCase.Value : mapArtwork.Value, DashboardAccent, Ui.GraphFont, casePlan) { MinHeight = 260, MaxWidth = Settings.MaxColumnWidth, Tag = "graphic" });
        if (summary) body.Add(DashboardSummary(parts), false);
        var frame = DashboardFrame(body); frame.Tag = "dashboard-picture"; return frame;
    }

    private Control DashboardSummary(List<DashboardPart> parts)
    {
        var cells = new List<Control>();
        foreach (var id in new[] { "cpu", "gpu", "ram" })
        {
            var part = parts.FirstOrDefault(p => p.Id == id); var row = part?.Rows.FirstOrDefault(); if (part == null || row == null) continue;
            var mirror = Text(Display(row), 20, DashboardAccent, true); // summary mirrors are not duplicate sensor identities
            if (!fields.TryGetValue(row.Key, out var values)) fields[row.Key] = values = new(); values.Add(mirror);
            cells.Add(DashboardFrame(Stack(4, DashboardText(part.Title, 10.5, true), FitRight(mirror))));
        }
        return FixedCols(Math.Max(1, cells.Count), cells.ToArray());
    }

    private void BuildDashboard(List<AioReading> rows)
    {
        if (AioDesigns.Instruments(style)) { BuildInstrumentDashboard(rows); return; }
        if (DarkAioDesigns.Contains(style)) { BuildDarkDesign(rows); return; }
        if (CaseEditionDesigns.Contains(style)) { BuildCaseEdition(rows); return; }
        if (CaseDesigns.Contains(style)) { BuildCaseOverlay(rows); return; }
        dashboardLimits.Clear(); dashboardTraffic = null; coreBars = null; mainboardCores = null;
        DashboardHeightGrowth = 0; dashboardLayout = null;
        Padding = new Thickness(8); CornerRadius = new CornerRadius(9); content.Spacing = 7;
        if (slot?.ShowTitle != false && !string.IsNullOrWhiteSpace(slot?.Name)) content.Children.Add(DashboardText(slot!.Name, 18, true));
        if (settings.ShowNames && slot is { ShowHardware: true, HardwareName.Length: > 0 }) content.Children.Add(DashboardText(slot.HardwareName));
        var parts = DashboardParts(rows);
        if (parts.Count == 0) { content.Children.Add(DashboardText(L.T("No data"), 13)); return; }
        var network = AioDesigns.NetworkFooterLayout(designStyle) ? parts.Find(p => p.Id == "net") : null;
        if (network != null) parts.Remove(network);
        Control Get(string id) => parts.FirstOrDefault(p => p.Id == id) is { } part ? DashboardSection(part) : new Border { IsVisible = false };
        IEnumerable<DashboardPart> Pick(params string[] ids) => ids.SelectMany(id => parts.Where(p => p.Id == id));
        Control layout;
        int columns = Math.Clamp(source.Columns, 1, 6);
        if (columns >= 3)
        {
            if (style is WidgetStyle.AioCasePlan or WidgetStyle.AioBoardCard)
            {
                bool showCase = style == WidgetStyle.AioCasePlan;
                var picture = DashboardArtwork(showCase, parts, false);
                layout = new DashboardFlow(columns, new[] { ((Control)DashboardStretch(picture, Get("ssd"), Get("net")), 22d) }
                    .Concat(parts.Where(p => p.Id is not ("ssd" or "net")).Select(p => (DashboardSection(p), (double)p.Rows.Count + 4))).ToArray(), firstColumnReserved: true);
            }
            else if (style == WidgetStyle.AioSummaryDetails) layout = DashboardTop(DashboardSummary(parts), DashboardFlow(parts, columns, rail: true));
            else layout = DashboardFlow(parts, columns, style is WidgetStyle.AioSensorTable or WidgetStyle.AioMinMax, style == WidgetStyle.AioMinMax);
        }
        else
        switch (style)
        {
            case WidgetStyle.AioSensorTable: layout = DashboardFlow(parts, columns, true); break;
            case WidgetStyle.AioMinMax: layout = DashboardFlow(parts, columns, true, true); break;
            case WidgetStyle.AioSummaryDetails: layout = DashboardTop(DashboardSummary(parts), DashboardFlow(parts, columns, rail: true)); break;
            case WidgetStyle.AioTwoHalves:
                layout = DashboardPair(DashboardStack(Pick("cpu", "ram", "fan", "power", "other")), DashboardStack(Pick("gpu", "ssd", "net")), "dashboard-two-halves"); break;
            case WidgetStyle.AioDoubleTable:
                layout = DashboardStretch(DashboardPair(Get("cpu"), Get("gpu"), "dashboard-pair-cpu-gpu"), DashboardPair(Get("ram"), Get("ssd"), "dashboard-pair-memory-drives"), Get("fan"), Get("power"), Get("other")); break;
            case WidgetStyle.AioQuadrants:
                layout = DashboardStretch(DashboardPair(Get("cpu"), Get("gpu"), "dashboard-upper-quadrants"), DashboardPair(DashboardFrame(DashboardStack(Pick("ram", "ssd"))), DashboardFrame(DashboardStack(Pick("fan", "power", "net", "other"))), "dashboard-lower-quadrants")); break;
            case WidgetStyle.AioCasePlan:
            case WidgetStyle.AioBoardCard:
                bool casePlan = style == WidgetStyle.AioCasePlan;
                var picture = DashboardArtwork(casePlan, parts, casePlan);
                layout = DashboardPair(DashboardStretch(picture, Get("ssd"), Get("net")), DashboardStack(Pick("cpu", "gpu", "ram", "fan", "power", "other")), "dashboard-illustrated-columns"); break;
            case WidgetStyle.AioNetworkCenter:
                layout = DashboardFlow(parts, columns); break;
            default: throw new InvalidOperationException("Unknown dashboard");
        }
        if (network != null)
        {
            var footer = style == WidgetStyle.AioNetworkCenter ? DashboardNetwork(network) : DashboardSection(network);
            if (parts.Count == 0) layout = footer;
            else
            {
                var stack = new DashboardVertical(7);
                stack.Add(layout);
                stack.Add(footer, false);
                layout = stack;
            }
        }
        dashboardLayout = layout; content.Children.Add(layout);
        if (style is WidgetStyle.AioSensorTable or WidgetStyle.AioMinMax)
            content.Children.Add(DashboardText(L.T("Min/max · observed values and available history"), 10.5));
    }
    private Control DashboardNetwork(DashboardPart network)
    {
                var top = Stack(7);
                if (network != null)
                {
                    top.Children.Add(DashboardHeader(network));
                    if (settings.Shows("aio.graphs") && network.Rows.Any(r => r.Key is "aio.network.down" or "aio.network.up"))
                    {
                        var down = network.Rows.Any(r => r.Key == "aio.network.down"); var up = network.Rows.Any(r => r.Key == "aio.network.up");
                        top.Children.Add(TextFonts.Mark(DashboardText(L.T("Download / Upload")), TextRole.Graph));
                        dashboardTraffic = new DashboardTrafficPlot(DashboardAccent, C("#F3B85A"), track, muted, down, up) { Height = 120, Tag = "graphic" };
                        top.Children.Add(dashboardTraffic);
                        top.Children.Add(TextFonts.Mark(DashboardText(L.T(down && up ? "Download + Upload · recent samples" : down ? "Download · recent samples" : "Upload · recent samples"), 10.5), TextRole.Graph));
                    }
                    foreach (var row in network.Rows) top.Children.Add(DashboardRow(row, false));
                }
                return DashboardFrame(top);
    }
}

internal static class DashboardGridExtensions
{
    public static Grid WithDashboard(this Grid grid, params Control[] children)
    {
        for (int i = 0; i < children.Length; i++) { Grid.SetColumn(children[i], i); grid.Children.Add(children[i]); }
        return grid;
    }
}

public sealed class DashboardVertical : Grid
{
    private readonly Dictionary<Control, double> naturalHeights = new();
    public DashboardVertical(double gap, params Control[] controls)
    {
        RowSpacing = gap; ColumnDefinitions = new ColumnDefinitions("*");
        foreach (var control in controls) Add(control);
    }
    public void Add(Control control, bool grow = true)
    {
        double weight = Equals(control.Tag, "dashboard-picture") ? 12 : Math.Max(1, control.GetLogicalDescendants().OfType<TextBlock>().Count() / 2d);
        RowDefinitions.Add(new RowDefinition(grow ? new GridLength(weight, GridUnitType.Star) : GridLength.Auto));
        SetRow(control, Children.Count); Children.Add(control);
    }
    protected override Size MeasureOverride(Size available)
    {
        double width = 0, height = 0; int count = 0;
        foreach (var child in Children.Where(c => c.IsVisible))
        {
            child.Measure(new Size(available.Width, double.PositiveInfinity));
            naturalHeights[child] = child.DesiredSize.Height;
            width = Math.Max(width, child.DesiredSize.Width); height += child.DesiredSize.Height; count++;
        }
        return new Size(width, height + Math.Max(0, count - 1) * RowSpacing);
    }
    protected override Size ArrangeOverride(Size final)
    {
        var visible = Children.Where(c => c.IsVisible).ToArray();
        double natural = visible.Sum(c => naturalHeights.GetValueOrDefault(c)) + Math.Max(0, visible.Length - 1) * RowSpacing;
        double Weight(Control c) => RowDefinitions[GetRow(c)].Height.IsStar ? RowDefinitions[GetRow(c)].Height.Value : 0;
        double weight = visible.Sum(Weight), extra = Math.Max(0, final.Height - natural), y = 0;
        foreach (var child in visible)
        {
            double height = naturalHeights.GetValueOrDefault(child) + (weight > 0 ? extra * Weight(child) / weight : 0);
            child.Arrange(new Rect(0, y, final.Width, height)); y += height + RowSpacing;
        }
        foreach (var child in Children.Where(c => !c.IsVisible)) child.Arrange(new Rect());
        return final;
    }
}

public sealed class DashboardFlow : Grid
{
    private readonly (Control Card, double Weight)[] items;
    private readonly int maximum;
    private readonly bool reserved;
    private int columns;
    public int VisibleColumns => columns;
    public DashboardFlow(int maximum, (Control Card, double Weight)[] items, bool firstColumnReserved = false)
    {
        this.maximum = Math.Clamp(maximum, 1, 6); this.items = items; reserved = firstColumnReserved;
        ColumnSpacing = 7; RowDefinitions = new RowDefinitions("*"); Reflow(this.maximum);
    }
    private void Reflow(int count)
    {
        columns = Math.Max(1, Math.Min(count, Math.Max(1, items.Length)));
        foreach (var old in Children.OfType<DashboardVertical>()) old.Children.Clear();
        Children.Clear(); ColumnDefinitions = new ColumnDefinitions(string.Join(',', Enumerable.Repeat("*", columns)));
        var weights = new double[columns]; var stacks = Enumerable.Range(0, columns).Select(_ => new DashboardVertical(7)).ToArray();
        for (int i = 0; i < stacks.Length; i++) { SetColumn(stacks[i], i); Children.Add(stacks[i]); }
        for (int i = 0; i < items.Length; i++)
        {
            int first = reserved && columns > 1 && i > 0 ? 1 : 0;
            int target = i == 0 && reserved ? 0 : Enumerable.Range(first, columns - first).MinBy(c => weights[c]);
            stacks[target].Add(items[i].Card); weights[target] += items[i].Weight;
        }
    }
    protected override Size MeasureOverride(Size available)
    {
        int count = double.IsFinite(available.Width) ? Math.Clamp((int)((available.Width + 7) / 180), 1, maximum) : maximum;
        count = Math.Min(count, Math.Max(1, items.Length));
        if (count != columns) Reflow(count);
        return base.MeasureOverride(available);
    }
}

/// <summary>Two readable columns when room permits, otherwise a vertical flow. Never scales down the whole UI.</summary>
public sealed class DashboardColumns : Grid
{
    private bool folded;
    private readonly int maximumColumns;
    public DashboardColumns(Control left, Control right, int maximumColumns = 2)
    {
        this.maximumColumns = maximumColumns;
        ColumnDefinitions = new ColumnDefinitions("*,*"); RowDefinitions = new RowDefinitions("*"); ColumnSpacing = RowSpacing = 7;
        Children.Add(left); Children.Add(right); SetColumn(right, 1);
    }
    protected override Size MeasureOverride(Size available)
    {
        bool fold = maximumColumns == 1 || double.IsFinite(available.Width) && available.Width < 367;
        if (fold != folded)
        {
            folded = fold; ColumnDefinitions = new ColumnDefinitions(fold ? "*" : "*,*"); RowDefinitions = new RowDefinitions(fold ? "*,*" : "*");
            SetColumn(Children[1], fold ? 0 : 1); SetRow(Children[1], fold ? 1 : 0);
        }
        return base.MeasureOverride(available);
    }
}

public sealed class DashboardTableRow : Grid
{
    private bool folded;
    public DashboardTableRow(Control label, Control value, Control min, Control max)
    {
        ColumnDefinitions = new ColumnDefinitions("1.25*,1*,.8*,.8*"); ColumnSpacing = 5; RowSpacing = 3;
        this.WithDashboard(label, value, min, max);
    }
    protected override Size MeasureOverride(Size available)
    {
        bool fold = double.IsFinite(available.Width) && available.Width < 330;
        if (fold != folded)
        {
            folded = fold; ColumnDefinitions = new ColumnDefinitions(fold ? "*,*" : "1.25*,1*,.8*,.8*"); RowDefinitions = new RowDefinitions(fold ? "Auto,Auto" : "Auto");
            for (int i = 0; i < 4; i++) { SetColumn(Children[i], fold ? i % 2 : i); SetRow(Children[i], fold ? i / 2 : 0); }
        }
        return base.MeasureOverride(available);
    }
}

public sealed class DashboardBoardPicture(Bitmap board, Color accent, FontFamily font, bool casePlan = false) : Control
{
    public IReadOnlyList<string> ComponentLabels => new[] { "CPU", "GPU", "RAM" };
    protected override Size MeasureOverride(Size available)
    {
        double width = double.IsFinite(available.Width) ? available.Width : 300;
        return new Size(width, width * (casePlan ? 1.5 : 1.35));
    }
    public override void Render(DrawingContext context)
    {
        context.DrawImage(board, new Rect(0, 0, board.PixelSize.Width, board.PixelSize.Height), new Rect(Bounds.Size));
        var labels = casePlan ? new[] { ("CPU", .52, .39), ("GPU", .50, .59), ("RAM", .76, .40) }
            : new[] { ("CPU", .43, .30), ("GPU", .29, .66), ("RAM", .80, .24) };
        foreach (var (label, px, py) in labels)
        {
            var point = new Point(px * Bounds.Width, py * Bounds.Height);
            var text = new FormattedText(label, L.Culture, FlowDirection.LeftToRight, new Typeface(font), 13, Palette.Brush(accent));
            context.DrawRectangle(new SolidColorBrush(Colors.Black, .85), new Pen(Palette.Brush(accent), 1),
                new Rect(point.X - text.Width / 2 - 6, point.Y - text.Height / 2 - 3, text.Width + 12, text.Height + 6), 4, 4);
            context.DrawText(text, point - new Vector(text.Width / 2, text.Height / 2));
        }
    }
}

public sealed class DashboardTrafficPlot(Color downColor, Color upColor, Color gridColor, Color labelColor, bool showDown, bool showUp) : Control, IColourGraph
{
    private double[] down = Array.Empty<double>(), up = Array.Empty<double>();
    public void Set(double[] first, double[] second) { down = first; up = second; InvalidateVisual(); }
    public override void Render(DrawingContext context)
    {
        double width = Bounds.Width, height = Math.Max(1, Bounds.Height - 20);
        var visible = (showDown ? down : Array.Empty<double>()).Concat(showUp ? up : Array.Empty<double>()).Where(double.IsFinite).ToArray();
        double ceiling = Math.Max(1, visible.DefaultIfEmpty(1).Max());
        for (int i = 0; i <= 4; i++) context.DrawLine(new Pen(Palette.Brush(gridColor), 1), new Point(0, i * height / 4), new Point(width, i * height / 4));
        if (showDown) Draw(down, downColor); if (showUp) Draw(up, upColor);
        var label = new FormattedText(Format.Bits(ceiling * 8), L.Culture, FlowDirection.LeftToRight, new Typeface(Ui.GraphFont), 10.5, Palette.Brush(labelColor));
        context.DrawText(label, new Point(0, height + 3));
        void Draw(double[] points, Color color)
        {
            var pen = new Pen(Palette.Brush(color), 1.5);
            for (int i = 1; i < points.Length; i++) if (double.IsFinite(points[i]) && double.IsFinite(points[i - 1]))
                context.DrawLine(pen, new Point((i - 1) * width / (points.Length - 1), height - points[i - 1] / ceiling * height), new Point(i * width / (points.Length - 1), height - points[i] / ceiling * height));
        }
    }
}
