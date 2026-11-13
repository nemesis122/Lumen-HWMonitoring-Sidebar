using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

/// <summary>
/// Four more designs with the isometric part illustrations (Isometric Core stays untouched):
/// "Isometric Rig" (the parts on a board with value tags), "Isometric Pedestals" (each part on a glowing pedestal),
/// "Isometric Compact" (one slim row per part) and "Isometric Rings" (each part inside its own ring gauge).
/// Every value follows the sensor choices; values without a place of their own fall back to the extras list.
/// </summary>
public sealed partial class AioTile
{
    private sealed record IsoPart(string Kind, string Art, string Title, Color Color, string Model, AioReading Main, AioReading[] Details, AioReading? Second = null);

    /// <summary>The computer's parts with a main value and their remaining values, in the user's sensor order.</summary>
    private List<IsoPart> IsoParts(Color fanColor)
    {
        var parts = new List<IsoPart>();
        var gpuIds = Rs("GPU").Select(r => r.Device).Distinct().ToList();
        foreach (var group in referenceRows.Where(r => r.Group is "CPU" or "GPU").GroupBy(r => r.Group + ":" + r.Device))
        {
            var rows = group.ToArray(); var first = rows[0]; bool cpu = first.Group == "CPU";
            int index = cpu ? 0 : gpuIds.IndexOf(first.Device);
            string model = cpu ? PartModel(WidgetKind.Cpu, 0, snapshot.Cpu?.Name ?? "") : PartModel(WidgetKind.Gpu, index, snapshot.Gpus.FirstOrDefault(g => g.Id == first.Device)?.Name ?? "");
            var main = rows.FirstOrDefault(r => r.Key.EndsWith(".temp")) ?? rows.FirstOrDefault(r => r.Key.EndsWith(".load")) ?? first;
            var load = rows.FirstOrDefault(r => r.Key.EndsWith(".load") && r != main);
            var details = (load == null ? Array.Empty<AioReading>() : new[] { load }).Concat(rows.Where(r => r != main && r != load)).ToArray();
            parts.Add(new IsoPart(cpu ? "cpu" : "gpu", cpu ? "cpu" : "gpu", cpu ? "CPU" : gpuIds.Count > 1 ? "GPU " + (index + 1) : "GPU", cpu ? Cyan : Purple, model, main, details));
        }
        var ram = R("ram", "System");
        if (ram != null) parts.Add(new IsoPart("ram", "ram", "RAM", Cyan, "", ram, Array.Empty<AioReading>()));
        foreach (var drive in PartDrives())
        {
            var main = drive.Temp ?? drive.Rows.FirstOrDefault(r => r.Key.EndsWith(".used")) ?? drive.Rows[0];
            parts.Add(new IsoPart("drive", "ssd", AioReadings.DriveName(snapshot, drive.Id), Amber, DriveModel(drive.Id), main, drive.Rows.Where(r => r != main).ToArray()));
        }
        var fans = Rs("Cooling");
        if (fans.Length > 0) parts.Add(new IsoPart("fan", "fan0", L.T("Cooling"), fanColor, "", fans[0], fans.Skip(1).ToArray()));
        var rails = Rs("Rails");
        if (rails.Length > 0) parts.Add(new IsoPart("psu", "psu", L.T("Power supply"), Amber, "", rails[0], rails.Skip(1).ToArray()));
        var net = Rs("Network");
        if (net.Length > 0)
        {
            var main = net.FirstOrDefault(r => r.Key.EndsWith(".down")) ?? net.FirstOrDefault(r => r.Key.EndsWith(".up")) ?? net[0];
            // Download and upload are shown at the same size (both are the network's main values).
            var second = main.Key.EndsWith(".down") ? net.FirstOrDefault(r => r.Key.EndsWith(".up")) : null;
            parts.Add(new IsoPart("net", "router", L.T("Network"), Cyan, "", main, net.Where(r => r != main && r != second).ToArray(), second));
        }
        return parts;
    }

    /// <summary>"14.4 / 93.7 GB": a value with a fixed room, like every other measured value.</summary>
    private TextBlock AmountText(AioReading ram, double size, Color color)
    {
        var t = Text(Amount(ram), size, color, true); t.FontWeight = FontWeight.Medium;
        if (t is AioValueText v) v.Hint = "88.8 / 88.8 GB";
        return t;
    }

    private static IEnumerable<IsoPart> Ordered(List<IsoPart> parts, params string[] kinds) =>
        kinds.SelectMany(k => parts.Where(p => p.Kind == k));

    private static T Align<T>(T control, HorizontalAlignment alignment) where T : Control
    {
        if (control is Decorator { Child: Viewbox box }) box.HorizontalAlignment = alignment;
        else if (control is Viewbox view) view.HorizontalAlignment = alignment;
        return control;
    }

    /// <summary>The big value of a part: a temperature with its unit, RAM in percent, everything else as read.</summary>
    private Control IsoMain(AioReading r, double size, Color color, HorizontalAlignment alignment, bool arrow = true)
    {
        if (r.Key.EndsWith(".temp") || r.Key.StartsWith("aio.system.disk.")) return Align(MainTemp(r, size, color), alignment);
        var value = V(r, size, color);
        if (r.Key == "aio.system.ram") { formats[value] = row => Format.Percent(row.Number); value.Text = formats[value](r); }
        if (r.Group == "Network" && arrow) { formats[value] = row => (r.Key.EndsWith(".up") ? "↑ " : r.Key.EndsWith(".down") ? "↓ " : "") + (L.Language == "de" ? row.Value.Replace('.', ',') : row.Value); value.Text = formats[value](r); }
        return Align(Fit(value), alignment);
    }

    /// <summary>
    /// Download and upload as in Isometric Tiles and Rings: the arrow in its own column, the value left-aligned beside it.
    /// The arrows never move, whatever the rates show, and both rates have the same size.
    /// </summary>
    private Control RateLines(IsoPart p, double size)
    {
        var lines = Stack(2);
        foreach (var r in new[] { p.Main, p.Second! })
        {
            bool down = r.Key.EndsWith(".down");
            var arrow = Text(down ? "↓" : "↑", size, down ? Cyan : Amber); arrow.TextWrapping = TextWrapping.NoWrap;
            var line = Ui.Columns("Auto,*", arrow, FitLeft(V(r, size, down ? Cyan : Amber))); line.ColumnSpacing = 6;
            lines.Children.Add(line);
        }
        return lines;
    }

    private Control IsoMains(IsoPart p, double size, HorizontalAlignment alignment, bool arrow = true)
    {
        if (p.Kind == "net" && p.Second != null) return RateLines(p, size);
        if (p.Second == null) return IsoMain(p.Main, size, p.Color, alignment, arrow);
        // One shared scale, so download and upload always appear at exactly the same size.
        TextBlock Rate(AioReading r, Color color)
        {
            var t = V(r, size, color); t.TextWrapping = TextWrapping.NoWrap; t.HorizontalAlignment = HorizontalAlignment.Stretch;
            formats[t] = row => (r.Key.EndsWith(".up") ? "↑ " : "↓ ") + (L.Language == "de" ? row.Value.Replace('.', ',') : row.Value); t.Text = formats[t](r); return t;
        }
        return new AioFit{Child=new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = alignment,
            Child = new AioWidest { Child = new StackPanel { Spacing = 0, Children = { Rate(p.Main, p.Color), Rate(p.Second, Amber) } } } }};
    }

    private string IsoLabel(AioReading r) => r.Group switch
    {
        "Network" when r.Key.EndsWith(".up") => "↑", "Network" when r.Key.EndsWith(".down") => "↓",
        "Cooling" or "Rails" => Translate(L.T(r.Label)), _ => RefLabel(r)
    };

    /// <summary>"Label value" pairs that wrap onto the next line; a pair wider than the whole line shrinks instead of being cut.</summary>
    /// <summary>
    /// "Label value" pairs in fixed places: one per line in the narrow centred cells, two per line otherwise.
    /// The number of lines never depends on the values, so the card keeps its height; a pair wider than its place shrinks.
    /// </summary>
    private Control IsoDetails(IEnumerable<AioReading> rows, bool center, double size = 12, AioReading? ram = null)
    {
        var list = rows.ToArray();
        TextBlock Label(AioReading r)
        {
            var label = Text(IsoLabel(r), size - 2, muted); label.TextWrapping = TextWrapping.NoWrap; label.VerticalAlignment = VerticalAlignment.Center;
            label.MaxWidth = center ? 92 : 62; label.TextTrimming = TextTrimming.CharacterEllipsis; return label;
        }
        TextBlock Value(AioReading r)
        {
            var value = V(r, size, ink); value.TextWrapping = TextWrapping.NoWrap;
            if (r.Group == "Cooling") { formats[value] = row => row.Number.HasValue ? Format.Number(row.Number) : "—"; value.Text = formats[value](r); }
            return value;
        }
        if (center)
        {
            // A small table in the middle of the cell: names left-aligned under each other, values left-aligned beside them.
            var table = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto"), ColumnSpacing = 6, RowSpacing = 1 };
            int row = 0;
            void Row(Control? label, Control value)
            {
                table.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                if (label != null) { Grid.SetRow(label, row); table.Children.Add(label); }
                Grid.SetRow(value, row); Grid.SetColumn(value, label == null ? 0 : 1); if (label == null) Grid.SetColumnSpan(value, 2);
                table.Children.Add(value); row++;
            }
            if (ram != null) { var amount = AmountText(ram, size - 1, muted); amount.TextWrapping = TextWrapping.NoWrap; amount.HorizontalAlignment = HorizontalAlignment.Center; fields[ram.Key].Add(amount); formats[amount] = Amount; Row(null, amount); }
            foreach (var r in list) Row(Label(r), Value(r));
            return new AioFit { Child = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = HorizontalAlignment.Center, Child = new AioWidest { Child = table } } };
        }
        var items = new List<Control>();
        void Add(Control item) => items.Add(new AioFit { Child = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = HorizontalAlignment.Left, Child = new AioWidest { Child = item } } });
        if (ram != null) { var amount = AmountText(ram, size - 1, muted); amount.TextWrapping = TextWrapping.NoWrap; fields[ram.Key].Add(amount); formats[amount] = Amount; Add(amount); }
        foreach (var r in list) Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { Label(r), Value(r) } });
        // Transfer rates need their room: one pair per line, so "Read 368 KB/s" keeps its full size.
        if (list.Any(r => r.Key.EndsWith(".read") || r.Key.EndsWith(".write"))) return Stack(1, items.ToArray());
        var grid = FixedCols(2, items.ToArray()); grid.ColumnSpacing = 10; grid.RowSpacing = 1; return grid;
    }

    private TextBlock IsoModel(string model, bool center)
    {
        var m = Text(model, 10, muted); m.TextTrimming = TextTrimming.CharacterEllipsis; m.TextWrapping = TextWrapping.NoWrap;
        if (center) { m.HorizontalAlignment = HorizontalAlignment.Center; m.TextAlignment = TextAlignment.Center; }
        return m;
    }

    private Control IsoTitle(string title, Color color, double size, HorizontalAlignment alignment)
    {
        var t = Text(title, size, color); t.FontWeight = FontWeight.SemiBold;
        return Align(Fit(t), alignment);
    }

    private void IsoGrid(IEnumerable<Control> cells, double gap)
    {
        var list = cells.ToArray();
        for (int i = 0; i < list.Length; i += 2)
        {
            var row = FixedCols(2, list.Skip(i).Take(2).ToArray()); row.ColumnSpacing = 10;
            content.Children.Add(new Border { Padding = new Thickness(6, i == 0 ? 8 : 0, 6, gap), Child = row, Tag = "aio-section" });
        }
    }

    // ---------------------------------------------------------------- Isometric Compact (draft E)
    private void IsometricCompact()
    {
        drivesPlaced = true;
        var parts = Ordered(IsoParts(Cyan), "cpu", "gpu", "ram", "drive", "fan", "psu", "net").ToArray();
        for (int i = 0; i < parts.Length; i++)
        {
            var p = parts[i];
            // The value column is as wide as the widest value it can show ("↓ 88.8 Mbit/s"), from the start;
            // the name beside it takes what is left. So the value never needs to shrink or move.
            // Wide values (fan speed, network rates) get their own full-width line below the name.
            Control head;
            if (p.Kind is "net" or "fan") head = Stack(0, IsoTitle(p.Title, p.Color, 15, HorizontalAlignment.Left), IsoMains(p, 22, HorizontalAlignment.Right, false));
            else { var g = Ui.Columns("*,Auto", IsoTitle(p.Title, p.Color, 15, HorizontalAlignment.Left), IsoMains(p, 22, HorizontalAlignment.Right, false)); g.ColumnSpacing = 6; head = g; }
            var side = Stack(2, head);
            if (p.Model.Length > 0) side.Children.Add(IsoModel(p.Model, false));
            if (p.Kind == "ram" || p.Details.Length > 0) side.Children.Add(IsoDetails(p.Details, false, 12, p.Kind == "ram" ? p.Main : null));
            var art = new Border { Width = 56, Height = 44, VerticalAlignment = VerticalAlignment.Top, Child = Part(p.Art, 56, 44) };
            var row = Ui.Columns("Auto,*", art, side); row.ColumnSpacing = 10;
            RefSection(row, p.Color, i == parts.Length - 1);
        }
    }

    // ---------------------------------------------------------------- Isometric Pedestals (draft D)
    private void IsometricPedestals()
    {
        drivesPlaced = true;
        var parts = Ordered(IsoParts(Coral), "cpu", "gpu", "ram", "psu", "drive", "net", "fan");
        IsoGrid(parts.Select(p =>
        {
            var stage = new Grid { Height = 96, Children = { new AioPedestal(p.Color, back, ink, cardAlpha / 255d) { VerticalAlignment = VerticalAlignment.Bottom, Height = 40, Margin = new Thickness(4, 0) },
                new Border { Margin = new Thickness(0, 0, 0, 14), Child = Part(p.Art, 104, 72) } } };
            var cell = Stack(1, stage, IsoTitle(p.Title, p.Color, 14, HorizontalAlignment.Center), IsoMains(p, 24, HorizontalAlignment.Center));
            if (p.Kind == "ram" || p.Details.Length > 0) cell.Children.Add(IsoDetails(p.Details, true, 11, p.Kind == "ram" ? p.Main : null));
            return (Control)cell;
        }), 14);
    }

    // ---------------------------------------------------------------- Isometric Rings (draft F)
    private void IsometricRings()
    {
        drivesPlaced = true;
        var all = IsoParts(Cyan);
        IsoGrid(Ordered(all, "cpu", "gpu", "ram", "drive", "psu").Select(p =>
        {
            const double size = 138;
            var ring = new AioRing(p.Color, track, false, true) { Width = size, Height = size }; rings.Add((p.Main.Key, ring));
            var value = new Border { Width = size * .66, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, size * .6, 0, 0), Child = IsoMain(p.Main, 22, p.Color, HorizontalAlignment.Center) };
            var art = new Border { Width = size * .56, Height = size * .4, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, size * .18, 0, 0), Child = Part(p.Art, size * .56, size * .4) };
            var face = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, MaxWidth = size, Child = new Grid { Width = size, Height = size, Children = { ring, art, value } } };
            var cell = Stack(2, face, IsoTitle(p.Title, p.Color, 13, HorizontalAlignment.Center));
            if (p.Kind == "ram" || p.Details.Length > 0) cell.Children.Add(IsoDetails(p.Details, true, 11, p.Kind == "ram" ? p.Main : null));
            return (Control)cell;
        }), 12);
        var fans = Rs("Cooling");
        if (fans.Length > 0) { content.Children.Add(Divider()); RefSection(FanGrid(fans, 46, true), Cyan); }
        var rates = Rs("Network");
        if (rates.Length > 0) { if (fans.Length == 0) content.Children.Add(Divider()); RefSection(ArtRow(Part("router", 96, 70), Stack(5, PartTitle(L.T("Network"), Cyan, ""), NetworkValues(rates, 16))), Cyan, true); }
    }

    // ---------------------------------------------------------------- Isometric Rig (draft C)
    private Control RigTag(Control header, Control value, Color color, double width) => new Border
    {
        Width = width, CornerRadius = new CornerRadius(9), BorderBrush = Palette.Brush(color), BorderThickness = new Thickness(1.3),
        Background = Palette.Brush(Solid(Palette.Mix(back, color, .1))), Opacity = 1, Padding = new Thickness(7, 3, 7, 2), Child = Stack(0, header, value)
    };

    private Control RigHeader(string title, AioReading? extra)
    {
        var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 0 };
        var t = Text(title, 10, muted); t.TextWrapping = TextWrapping.NoWrap; line.Children.Add(t);
        if (extra != null)
        {
            line.Children.Add(Text(" · ", 10, muted));
            var v = V(extra, 10, muted); v.TextWrapping = TextWrapping.NoWrap; line.Children.Add(v);
        }
        return new AioFit{Child=new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = HorizontalAlignment.Left, Child = new AioWidest { Child = line } }};
    }

    private void IsometricRig()
    {
        drivesPlaced = true;
        var gpuIds = Rs("GPU").Select(r => r.Device).Distinct().ToList();
        var shown = new HashSet<string>();
        AioReading? Take(AioReading? r) { if (r != null) shown.Add(r.Key); return r; }
        AioReading? Main(string group, string? device) =>
            Take(referenceRows.FirstOrDefault(r => r.Group == group && r.Device == device && r.Key.EndsWith(".temp")) ?? referenceRows.FirstOrDefault(r => r.Group == group && r.Device == device && r.Key.EndsWith(".load")));
        AioReading? Load(string group, string? device, AioReading? main) =>
            main != null && main.Key.EndsWith(".temp") ? Take(referenceRows.FirstOrDefault(r => r.Group == group && r.Device == device && r.Key.EndsWith(".load"))) : null;

        var canvas = new Canvas { Width = 300, Height = 312 };
        void At(Control c, double x, double y) { Canvas.SetLeft(c, x); Canvas.SetTop(c, y); canvas.Children.Add(c); }
        void Leader(Point a, Point b, Color color)
        {
            canvas.Children.Add(new Line { StartPoint = a, EndPoint = b, Stroke = Palette.Brush(color), StrokeThickness = 1.3 });
            var dot = new Ellipse { Width = 7, Height = 7, Fill = Palette.Brush(color) }; At(dot, b.X - 3.5, b.Y - 3.5);
        }
        var cpuDevice = Rs("CPU").FirstOrDefault()?.Device;
        bool hasCpu = Rs("CPU").Length > 0, hasGpu = gpuIds.Count > 0;
        var ram = Take(R("ram", "System"));
        var drives = PartDrives();
        var fans = Rs("Cooling");
        var traces = new List<(Color, (double U, double V)[])>();
        if (hasCpu && hasGpu) traces.Add((Purple, new[] { (.62, .3), (.62, .62), (.38, .62) }));
        if (hasCpu && ram != null) traces.Add((Cyan, new[] { (.5, .3), (.3, .3), (.3, .55) }));
        if (drives.Length > 0) traces.Add((Amber, new[] { (.86, .68), (.66, .68), (.66, .5) }));
        At(new AioIsoPlate(back, ink, track, cardAlpha / 255d, traces) { Width = 300, Height = 190 }, 0, 80);

        // Parts on the board (painted back to front).
        if (fans.Length > 0) for (int i = 0; i < Math.Min(3, fans.Length); i++) At(Part(FanArt[i], 36, 34), 26 + i * 34, 104 - i * 4);
        if (hasCpu) At(Part("cpu", 92, 72), 104, 64);
        if (ram != null) At(Part("ram", 84, 60), 204, 112);
        if (hasGpu) At(Part("gpu", 118, 92), 6, 150);
        for (int i = 0; i < Math.Min(2, drives.Length); i++) At(Part("ssd", 58, 38), 124 + i * 50, 208 + i * 14);

        // Value tags with leaders.
        if (fans.Length > 0)
        {
            var fan = Take(fans[0])!;
            Leader(new Point(52, 46), new Point(70, 112), Cyan);
            At(RigTag(RigHeader(Translate(L.T(fan.Label)), null), Align(Fit(V(fan, 17, Cyan)), HorizontalAlignment.Right), Cyan, 104), 0, 0);
        }
        if (hasCpu)
        {
            var main = Main("CPU", cpuDevice);
            if (main != null)
            {
                var load = Load("CPU", cpuDevice, main);
                Leader(new Point(232, 46), new Point(150, 92), Cyan);
                At(RigTag(RigHeader("CPU", load), IsoMain(main, 19, Cyan, HorizontalAlignment.Right), Cyan, 104), 196, 0);
            }
        }
        if (hasGpu)
        {
            var main = Main("GPU", gpuIds[0]);
            if (main != null)
            {
                var load = Load("GPU", gpuIds[0], main);
                Leader(new Point(52, 142), new Point(66, 190), Purple);
                At(RigTag(RigHeader(gpuIds.Count > 1 ? "GPU 1" : "GPU", load), IsoMain(main, 19, Purple, HorizontalAlignment.Right), Purple, 104), 0, 96);
            }
        }
        if (ram != null)
        {
            var value = V(ram, 19, Cyan); formats[value] = row => Format.Percent(row.Number); value.Text = formats[value](ram);
            var amount = AmountText(ram, 10, muted); amount.TextWrapping = TextWrapping.NoWrap; fields[ram.Key].Add(amount); formats[amount] = Amount;
            var header = new AioFit{Child=new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = HorizontalAlignment.Left,
                Child = new AioWidest { Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { Text("RAM · ", 10, muted), amount } } } }};
            Leader(new Point(248, 178), new Point(244, 142), Cyan);
            At(RigTag(header, Align(Fit(value), HorizontalAlignment.Right), Cyan, 104), 196, 178);
        }
        if (drives.Length > 0)
        {
            var mains = drives.Select(d => Take(d.Temp ?? d.Rows.FirstOrDefault(r => r.Key.EndsWith(".used")))).Where(r => r != null).Select(r => r!).ToArray();
            if (mains.Length > 0)
            {
                var names = Text(string.Join(" · ", drives.Where(d => mains.Any(m => m.Device == d.Id)).Select(d => AioReadings.DriveName(snapshot, d.Id))), 10, muted); names.TextWrapping = TextWrapping.NoWrap;
                var values = new StackPanel { Orientation = Orientation.Horizontal };
                for (int i = 0; i < mains.Length; i++)
                {
                    if (i > 0) values.Children.Add(Text(" · ", 17, Amber));
                    var v = V(mains[i], 17, Amber); v.TextWrapping = TextWrapping.NoWrap; values.Children.Add(v);
                }
                Leader(new Point(150, 262), new Point(152, 228), Amber);
                At(RigTag(new AioFit{Child=new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = HorizontalAlignment.Left, Child = new AioWidest { Child = names } }},
                    new AioFit{Child=new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = HorizontalAlignment.Right, Child = new AioWidest { Child = values } }}, Amber, 132), 84, 262);
            }
        }
        RefSection(new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, MaxWidth = 340, Child = canvas }, Cyan);

        // Everything else as a compact value grid, named so it is clear which part a value belongs to.
        string Context(AioReading r)
        {
            var label = IsoLabel(r);
            if (r.Group == "GPU" && gpuIds.Count > 1) return "GPU " + (gpuIds.IndexOf(r.Device) + 1) + " " + label;
            if (r.Group == "Drives" || r.Key.StartsWith("aio.system.disk.")) return label + " " + AioReadings.DriveName(snapshot, r.Device ?? "");
            if (r.Group == "CPU" && r.Key.EndsWith(".power")) return "CPU " + label;
            if (r.Group == "GPU" && r.Key.EndsWith(".power")) return "GPU " + label;
            return label;
        }
        var rest = referenceRows.Where(r => r.Group is "CPU" or "GPU" or "Drives" or "Cooling" || r.Key.StartsWith("aio.system.disk.")).Where(r => !shown.Contains(r.Key)).ToArray();
        if (rest.Length > 0)
        {
            int columns = settings.Width < 270 ? 2 : settings.Width < 330 ? 3 : 4;
            RefSection(FixedCols(columns, rest.Select(r =>
            {
                var value = V(r, 14, r.Key.EndsWith(".hotspot") ? Coral : r.Key.EndsWith(".junction") ? Purple : r.Group == "Drives" ? Accent(r) : ink);
                if (r.Group == "Cooling") { formats[value] = row => row.Number.HasValue ? Format.Number(row.Number) + " rpm" : "—"; value.Text = formats[value](r); }
                if (r.Key.StartsWith("aio.system.disk.")) value.Foreground = Palette.Brush(Amber);
                var label = Text(Context(r), 11, muted); label.TextWrapping = TextWrapping.NoWrap; label.TextTrimming = TextTrimming.CharacterEllipsis;
                return (Control)Stack(1, label, FitLeft(value));
            }).ToArray()), Cyan);
        }
        // Network and power supply in the same grid as the values above: name on top, value below, fixed columns.
        var rates = Rs("Network"); var rails = Rs("Rails");
        if (rates.Length > 0 || rails.Length > 0)
        {
            Control Cell(string caption, Color tint, AioReading r)
            {
                var label = Text(caption, 11, tint); label.TextWrapping = TextWrapping.NoWrap; label.TextTrimming = TextTrimming.CharacterEllipsis;
                return Stack(1, label, FitLeft(V(r, 14, ink)));
            }
            var cells = rates.Select(r => r.Key.EndsWith(".link") ? Cell("⇄ " + L.T("Link speed"), Purple, r) : r.Key.EndsWith(".down") ? Cell("↓ " + L.T("Download"), Cyan, r) : Cell("↑ " + L.T("Upload"), Amber, r))
                .Concat(rails.Select(r => Cell(r.Label, Amber, r))).ToArray();
            RefSection(FixedCols(3, cells), Cyan, true);
        }
    }
}

/// <summary>A glowing oval pedestal under a part (Isometric Pedestals).</summary>
public sealed class AioPedestal : Control
{
    private readonly Color color, back, ink; private readonly double alpha;
    public AioPedestal(Color color, Color back, Color ink, double alpha) { this.color = color; this.back = back; this.ink = ink; this.alpha = alpha; }
    private Color A(Color c, double a) => Color.FromArgb((byte)Math.Clamp(c.A * a, 0, 255), c.R, c.G, c.B);
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 10 || h < 10) return;
        double rx = w / 2 - 5, ry = Math.Min(h / 2 - 5, rx * .26);
        var center = new Point(w / 2, h / 2 - 2);
        c.DrawEllipse(Palette.Brush(A(Palette.Mix(back, Colors.Black, .35), Math.Max(.5, alpha))), null, center + new Point(0, 4), rx, ry);
        c.DrawEllipse(Palette.Brush(A(Palette.Mix(back, ink, .06), Math.Max(.6, alpha))), null, center, rx, ry);
        c.DrawEllipse(null, new Pen(Palette.Brush(A(color, .12)), 9), center, rx, ry);
        c.DrawEllipse(null, new Pen(Palette.Brush(A(color, .28)), 5), center, rx, ry);
        c.DrawEllipse(null, new Pen(Palette.Brush(color), 2), center, rx, ry);
    }
}

/// <summary>The isometric board of Isometric Rig: a grid plate with coloured traces between the parts.</summary>
public sealed class AioIsoPlate : Control
{
    private readonly Color back, ink, track; private readonly double alpha; private readonly List<(Color Color, (double U, double V)[] Path)> traces;
    public AioIsoPlate(Color back, Color ink, Color track, double alpha, List<(Color, (double U, double V)[])> traces)
    { this.back = back; this.ink = ink; this.track = track; this.alpha = alpha; this.traces = traces; }
    private static Color A(Color c, double a) => Color.FromArgb((byte)Math.Clamp(c.A * a, 0, 255), c.R, c.G, c.B);
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height - 8; if (w < 20 || h < 20) return;
        var left = new Point(2, h / 2); var top = new Point(w / 2, 2); var bottom = new Point(w / 2, h - 2); var right = new Point(w - 2, h / 2);
        Point P(double u, double v) => new(left.X + u * (top.X - left.X) + v * (bottom.X - left.X), left.Y + u * (top.Y - left.Y) + v * (bottom.Y - left.Y));
        StreamGeometry Poly(params Point[] points)
        {
            var g = new StreamGeometry(); using (var x = g.Open()) { x.BeginFigure(points[0], true); foreach (var p in points.Skip(1)) x.LineTo(p); x.EndFigure(true); } return g;
        }
        double fill = Math.Max(.55, alpha);
        var side = new Point(0, 7);
        c.DrawGeometry(Palette.Brush(A(Palette.Mix(back, Colors.Black, .3), fill)), null, Poly(left, bottom, bottom + side, left + side));
        c.DrawGeometry(Palette.Brush(A(Palette.Mix(back, Colors.Black, .45), fill)), null, Poly(bottom, right, right + side, bottom + side));
        c.DrawGeometry(Palette.Brush(A(Palette.Mix(back, ink, .05), fill)), new Pen(Palette.Brush(track), 1), Poly(left, top, right, bottom));
        var grid = new Pen(Palette.Brush(A(track, .55)), .8);
        for (int i = 1; i < 10; i++) { double t = i / 10d; c.DrawLine(grid, P(t, 0), P(t, 1)); c.DrawLine(grid, P(0, t), P(1, t)); }
        foreach (var (color, path) in traces)
        {
            var g = new StreamGeometry(); using (var x = g.Open()) { x.BeginFigure(P(path[0].U, path[0].V), false); foreach (var p in path.Skip(1)) x.LineTo(P(p.U, p.V)); x.EndFigure(false); }
            c.DrawGeometry(null, new Pen(Palette.Brush(A(color, .25)), 5), g);
            c.DrawGeometry(null, new Pen(Palette.Brush(A(color, .9)), 1.6), g);
        }
    }
}
