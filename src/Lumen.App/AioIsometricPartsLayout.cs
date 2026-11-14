using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Lumen.Core;

namespace Lumen.App;

/// <summary>
/// "Isometric Atlas" (one row per part: illustration left, values right) and "Isometric Tiles"
/// (two columns of tiles with the illustration on top). Every part of the computer is shown as an
/// isometric illustration: CPU, GPU, RAM, one SSD per drive, every fan, the power supply and the network.
/// Isometric Core is not touched; these designs use their own copies of the illustrations.
/// </summary>
public sealed partial class AioTile
{
    private static readonly Dictionary<string, Lazy<Bitmap>> isometricParts =
        new[] { "cpu", "gpu", "ram", "ssd", "fan0", "fan1", "fan2", "fan3", "fan4", "psu", "router" }
            .ToDictionary(n => n, n => new Lazy<Bitmap>(() => new Bitmap(AssetLoader.Open(new Uri("avares://Lumen/Assets/Aio/Parts/" + n + ".png")))));

    private Control Part(string name, double width, double height) => new Image
    {
        Source = isometricParts[name].Value, MaxWidth = width, MaxHeight = height, Stretch = Stretch.Uniform,
        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Tag = "graphic"
    };

    /// <summary>The model name shown under a part's title (the widget's own name settings apply).</summary>
    private string PartModel(WidgetKind kind, int index, string model)
    {
        if (!settings.ShowNames || slot?.ShowHardware == false) return "";
        var owned = source.Widgets.FirstOrDefault(w => w.Kind == kind);
        return Naming.ShowHardware(owned, settings) ? Naming.Hardware(owned, index, model) : "";
    }

    private Control PartTitle(string title, Color color, string model)
    {
        var heading = Text(title, 15, color); heading.FontWeight = FontWeight.SemiBold;
        if (model.Length == 0) return FitLeft(heading);
        var name = Text(model, 10, muted); name.TextTrimming = TextTrimming.CharacterEllipsis; name.TextWrapping = TextWrapping.NoWrap;
        return Stack(1, FitLeft(heading), name);
    }

    private static string Amount(AioReading r) => L.Language == "de" ? r.Value.Replace('.', ',') : r.Value;

    private Control PartMeter(AioReading r, Color color)
    {
        var m = new AioMeter(color, track, 0) { Height = 6 }; meters.Add((r.Key, m)); return m;
    }

    /// <summary>"Load 8 %" with its bar.</summary>
    private Control LoadLine(AioReading r, Color color, double size = 14) =>
        Stack(4, Ui.Columns("*,Auto", Text(RefLabel(r), 12, muted), V(r, size)), PartMeter(r, color));

    private Control SmallValue(AioReading r, double size = 14) =>
        Stack(1, FitLeft(Text(RefLabel(r), 11, muted)), FitLeft(V(r, size)));

    private Control Pill(AioReading r, Color color, double size = 15)
    {
        var line = Ui.Columns("*,Auto", FitLeft(Text(RefLabel(r), 12, color)), V(r, size, ink)); line.ColumnSpacing = 6;
        return new Border
        {
            BorderBrush = Palette.Brush(color), BorderThickness = new Thickness(1.2), CornerRadius = new CornerRadius(9),
            Background = Palette.Brush(SeeThrough(Palette.Mix(back, color, .13))), Padding = new Thickness(8, 3), Child = line
        };
    }

    private Control FitRight(Control value)
    {
        var fit = Fit(value); if (fit is Decorator { Child: Viewbox box }) box.HorizontalAlignment = HorizontalAlignment.Right;
        else if (fit is Viewbox view) view.HorizontalAlignment = HorizontalAlignment.Right;
        return fit;
    }

    /// <summary>Label left, value right (lists inside tiles and the power supply). The value shrinks rather than leave the card.</summary>
    private Control LabelValue(string label, AioReading r, double size = 14, Color? color = null)
    {
        var row = Ui.Columns("Auto,*", Text(label, 12, muted), FitRight(V(r, size, color))); row.ColumnSpacing = 6; return row;
    }

    private Control ArtRow(Control art, Control values)
    {
        var row = Ui.Columns(".82*,1.18*", art, values); row.ColumnSpacing = 10; return row;
    }

    private static readonly string[] FanArt = { "fan0", "fan4", "fan1", "fan2", "fan3" };

    private Control FanGrid(AioReading[] fans, double art, bool rpmBelow, bool uniform = false,bool twoTone=false)
    {
        return Cols(4, fans.Select((r, i) =>
        {
            var value = V(r, 16);
            if (rpmBelow) { formats[value] = row => row.Number.HasValue ? Format.Number(row.Number) : "—"; value.Text = formats[value](r); }
            value.HorizontalAlignment = HorizontalAlignment.Center;
            var name = Text(Translate(L.T(r.Label)), 11, muted); name.HorizontalAlignment = HorizontalAlignment.Center; name.TextAlignment = TextAlignment.Center;
            name.MaxLines = 2; name.TextTrimming = TextTrimming.CharacterEllipsis;
            var cell = Stack(1, Part(twoTone?(i%2==0?"fan0":"fan2"):uniform ? "fan0" : FanArt[i % FanArt.Length], art, art * .87), name, Fit(value));
            if (rpmBelow) { var unit = Text("rpm", 9, muted); unit.HorizontalAlignment = HorizontalAlignment.Center; cell.Children.Add(unit); }
            return (Control)cell;
        }).ToArray());
    }

    /// <summary>Drive ids in the user's order, each with its temperature (System group) and Drives rows.</summary>
    private (string Id, AioReading? Temp, AioReading[] Rows)[] PartDrives()
    {
        var order = referenceRows.Where(r => r.Group == "Drives" || r.Key.StartsWith("aio.system.disk.")).Select(r => r.Device).Distinct().ToList();
        return order.Select(id => (id, Rs("System").FirstOrDefault(r => r.Key == "aio.system.disk." + id), Rs("Drives").Where(r => r.Device == id).ToArray()))
            .Where(d => d.Item2 != null || d.Item3.Length > 0).ToArray();
    }

    private Control DriveArt(string id, double width)
    {
        var badge = new Border
        {
            Background = Palette.Brush(SeeThrough(Palette.Mix(back, Amber, .22))), BorderBrush = Palette.Brush(Amber), BorderThickness = new Thickness(1.2),
            CornerRadius = new CornerRadius(6), Padding = new Thickness(5, 1), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
            Child = Text(AioReadings.DriveName(snapshot, id), 12, Amber)
        };
        var grid = new Grid { Children = { new Border { Margin = new Thickness(8, 10, 0, 0), Child = Part("ssd", width, width * .62) }, badge } };
        return grid;
    }

    private string DriveModel(string id)
    {
        var d = snapshot.Disks.FirstOrDefault(x => x.Id == id);
        if (d == null) return "";
        return PartModel(WidgetKind.Drives, snapshot.Disks.ToList().IndexOf(d), d.Name);
    }

    private Control DriveValues((string Id, AioReading? Temp, AioReading[] Rows) drive, string[] parts)
    {
        AioReading? Find(string part) => part == "temp" ? drive.Temp : drive.Rows.FirstOrDefault(r => r.Key.EndsWith("." + part));
        var columns = Ui.Columns(string.Join(',', parts.Select(p => p is "read" or "write" ? "1.25*" : ".8*")), parts.Select(part =>
        {
            var r = Find(part); if (r == null) return (Control)new Border();
            string label = part == "temp" ? "Temp." : Translate(L.T(r.Label));
            return Stack(1, FitLeft(Text(label, 11, muted)), FitLeft(V(r, 14, part == "temp" ? Amber : Accent(r))));
        }).ToArray());
        columns.ColumnSpacing = 4;
        var body = Stack(4, columns);
        var used = Find("used"); if (used != null) body.Children.Add(PartMeter(used, Purple));
        return body;
    }

    private Control NetworkValues(AioReading[] rates, double size)
    {
        var lines = Stack(4);
        foreach (var r in rates.Where(r => !r.Key.EndsWith(".link")))
        {
            bool down = r.Key.EndsWith("down");
            var line = Ui.Columns("Auto,*", Text(down ? "↓" : "↑", size + 2, down ? Cyan : Amber), FitLeft(V(r, size, ink))); line.ColumnSpacing = 4;
            lines.Children.Add(line);
        }
        var link = rates.FirstOrDefault(r => r.Key.EndsWith(".link"));
        if (link != null) lines.Children.Add(Stack(0, Text(L.T("Link speed"), 11, muted), V(link, size - 1, ink)));
        return lines;
    }

    // ---------------------------------------------------------------- Isometric Atlas (draft A)
    private void IsometricAtlas()
    {
        drivesPlaced = true;
        var gpuIds = Rs("GPU").Select(r => r.Device).Distinct().ToList();
        foreach (var group in referenceRows.Where(r => r.Group is "CPU" or "GPU").GroupBy(r => r.Group + ":" + r.Device))
        {
            var rows = group.ToArray(); var first = rows[0]; bool cpu = first.Group == "CPU";
            var color = cpu ? Cyan : Purple;
            int index = cpu ? 0 : gpuIds.IndexOf(first.Device);
            string model = cpu ? PartModel(WidgetKind.Cpu, 0, snapshot.Cpu?.Name ?? "") : PartModel(WidgetKind.Gpu, index, snapshot.Gpus.FirstOrDefault(g => g.Id == first.Device)?.Name ?? "");
            string title = cpu ? "CPU" : gpuIds.Count > 1 ? "GPU " + (index + 1) : "GPU";
            var temp = rows.FirstOrDefault(r => r.Key.EndsWith(".temp")); var load = rows.FirstOrDefault(r => r.Key.EndsWith(".load"));
            var side = Stack(5, PartTitle(title, color, model));
            if (temp != null) side.Children.Add(MainTemp(temp, 36, color));
            if (load != null) side.Children.Add(LoadLine(load, color));
            foreach (var pill in rows.Where(r => r.Key.EndsWith(".hotspot") || r.Key.EndsWith(".junction")))
                side.Children.Add(Pill(pill, pill.Key.EndsWith(".hotspot") ? Coral : Purple));
            var rest = rows.Where(r => r != temp && r != load && !r.Key.EndsWith(".hotspot") && !r.Key.EndsWith(".junction")).ToArray();
            var body = Stack(8, ArtRow(Part(cpu ? "cpu" : "gpu", cpu ? 118 : 124, cpu ? 92 : 122), side));
            if (rest.Length > 0) body.Children.Add(FixedCols(Math.Min(4, rest.Length), rest.Select(r => SmallValue(r)).ToArray()));
            RefSection(body, color);
        }
        var ram = R("ram", "System");
        if (ram != null)
        {
            var percent = V(ram, 32, Cyan); formats[percent] = row => Format.Percent(row.Number); percent.Text = formats[percent](ram);
            var amount = AmountText(ram, 14, ink); fields[ram.Key].Add(amount); formats[amount] = Amount;
            var side = Stack(5, PartTitle(L.T("Memory"), Cyan, ""), FitLeft(percent), FitLeft(amount), PartMeter(ram, Cyan));
            RefSection(ArtRow(Part("ram", 112, 88), side), Cyan);
        }
        var drives = PartDrives();
        if (drives.Length > 0)
        {
            var parts = new[] { "temp", "read", "write", "used" }.Where(p => drives.Any(d => p == "temp" ? d.Temp != null : d.Rows.Any(r => r.Key.EndsWith("." + p)))).ToArray();
            var body = Stack(10, PartTitle(L.T("Drives"), Amber, ""));
            foreach (var drive in drives)
            {
                var model = DriveModel(drive.Id);
                var side = Stack(3); if (model.Length > 0) { var m = Text(model, 10, muted); m.TextTrimming = TextTrimming.CharacterEllipsis; m.TextWrapping = TextWrapping.NoWrap; side.Children.Add(m); }
                side.Children.Add(DriveValues(drive, parts));
                var row = Ui.Columns(".52*,1.48*", DriveArt(drive.Id, 70), side); row.ColumnSpacing = 8;
                body.Children.Add(row);
            }
            RefSection(body, Amber);
        }
        var fans = Rs("Cooling");
        if (fans.Length > 0) RefSection(Stack(8, PartTitle(L.T("Cooling"), Cyan, ""), FanGrid(fans, 52, true)), Cyan);
        var rails = Rs("Rails");
        if (rails.Length > 0)
        {
            var colors = new[] { Cyan, Purple, Amber };
            var side = Stack(6, new[] { PartTitle(L.T("Power supply"), Amber, "") }.Concat(rails.Select((r, i) => LabelValue(r.Label, r, 16, colors[i % 3]))).ToArray());
            RefSection(ArtRow(Part("psu", 104, 82), side), Amber);
        }
        var rates = Rs("Network");
        if (rates.Length > 0)
        {
            var body = Stack(6, ArtRow(Part("router", 108, 84), Stack(5, PartTitle(L.T("Network"), Cyan, ""), NetworkValues(rates, 16))));
            var down = R("down", "Network"); var up = R("up", "Network");
            if (down != null || up != null) body.Children.Add(HistoryPlot(down ?? up!, 34, up != null && down != null ? "mirror" : "area", Cyan, down != null ? up : null));
            RefSection(body, Cyan, true);
        }
    }

    // ---------------------------------------------------------------- Isometric Tiles (draft B)
    private Control Tile(Control body, Color color) => new Border
    {
        CornerRadius = new CornerRadius(14), Background = Palette.Brush(SeeThrough(Palette.Mix(back, ink, .035))),
        BorderBrush = Palette.Brush(Palette.Mix(back, color, .38)), BorderThickness = new Thickness(1), Padding = new Thickness(9, 8),
        VerticalAlignment = VerticalAlignment.Stretch, Child = body, Tag = "aio-section"
    };

    private void TileRow(params Control[] tiles)
    {
        if (tiles.Length == 0) return;
        Control row = tiles.Length == 1 ? tiles[0] : FixedCols(2, tiles);
        if (row is Grid g) { g.ColumnSpacing = 8; g.RowSpacing = 8; }
        content.Children.Add(new Border { Margin = new Thickness(0, 0, 0, 8), Child = row });
    }

    private void IsometricTiles()
    {
        drivesPlaced = true;
        content.Spacing = 0;
        var gpuIds = Rs("GPU").Select(r => r.Device).Distinct().ToList();
        var chips = new List<Control>(); var details = new List<Control>();
        foreach (var group in referenceRows.Where(r => r.Group is "CPU" or "GPU").GroupBy(r => r.Group + ":" + r.Device))
        {
            var rows = group.ToArray(); var first = rows[0]; bool cpu = first.Group == "CPU";
            var color = cpu ? Cyan : Purple; int index = cpu ? 0 : gpuIds.IndexOf(first.Device);
            string title = cpu ? "CPU" : gpuIds.Count > 1 ? "GPU " + (index + 1) : "GPU";
            var temp = rows.FirstOrDefault(r => r.Key.EndsWith(".temp")); var load = rows.FirstOrDefault(r => r.Key.EndsWith(".load"));
            string model = cpu ? PartModel(WidgetKind.Cpu, 0, snapshot.Cpu?.Name ?? "") : PartModel(WidgetKind.Gpu, index, snapshot.Gpus.FirstOrDefault(g => g.Id == first.Device)?.Name ?? "");
            var head = Ui.Columns("Auto,*", Text(title, 15, color), temp == null ? new Border() : FitRight(MainTemp(temp, 26, color))); head.ColumnSpacing = 6;
            var body = Stack(5, Part(cpu ? "cpu" : "gpu", 112, cpu ? 74 : 86), head);
            if (model.Length > 0) { var m = Text(model, 10, muted); m.TextTrimming = TextTrimming.CharacterEllipsis; m.TextWrapping = TextWrapping.NoWrap; body.Children.Add(m); }
            if (load != null) body.Children.Add(LoadLine(load, color, 13));
            var pills = rows.Where(r => r.Key.EndsWith(".hotspot") || r.Key.EndsWith(".junction")).ToArray();
            foreach (var pill in pills) body.Children.Add(Pill(pill, pill.Key.EndsWith(".hotspot") ? Coral : Purple, 13));
            var rest = rows.Where(r => r != temp && r != load && !pills.Contains(r)).ToArray();
            if (cpu) foreach (var r in rest) body.Children.Add(LabelValue(RefLabel(r), r, 13));
            else if (rest.Length > 0)
            {
                var strip = FixedCols(Math.Min(4, rest.Length), rest.Select(r => SmallValue(r, 13)).ToArray());
                details.Add(Tile(gpuIds.Count > 1 ? Stack(4, Text(title, 11, Purple), strip) : strip, Purple));
            }
            chips.Add(Tile(body, color));
        }
        for (int i = 0; i < chips.Count; i += 2) TileRow(chips.Skip(i).Take(2).ToArray());
        foreach (var d in details) TileRow(d);

        var pair = new List<Control>();
        var ram = R("ram", "System");
        if (ram != null)
        {
            var percent = V(ram, 26, Cyan); formats[percent] = row => Format.Percent(row.Number); percent.Text = formats[percent](ram);
            var amount = AmountText(ram, 13, ink); fields[ram.Key].Add(amount); formats[amount] = Amount;
            var ramHead = Ui.Columns("Auto,*", Text("RAM", 15, Cyan), FitRight(percent)); ramHead.ColumnSpacing = 6;
            pair.Add(Tile(Stack(5, Part("ram", 112, 76), ramHead, FitLeft(amount), PartMeter(ram, Cyan)), Cyan));
        }
        var rails = Rs("Rails");
        if (rails.Length > 0)
            pair.Add(Tile(Stack(5, new[] { Part("psu", 100, 76), PartTitle(L.T("Power supply"), Amber, "") }.Concat(rails.Select(r => LabelValue(r.Label, r, 13))).ToArray()), Amber));
        TileRow(pair.ToArray());

        var driveTiles = PartDrives().Select(drive =>
        {
            var body = Stack(5, DriveArt(drive.Id, 92));
            if (drive.Temp != null) { var t = Ui.Columns("Auto,*", Text("Temp.", 12, muted), FitRight(V(drive.Temp, 22, Amber))); t.ColumnSpacing = 6; body.Children.Add(t); }
            var rates = drive.Rows.Where(r => !r.Key.EndsWith(".used")).ToArray();
            if (rates.Length > 0) body.Children.Add(FixedCols(rates.Length, rates.Select(r => SmallValue(r, 13)).ToArray()));
            var used = drive.Rows.FirstOrDefault(r => r.Key.EndsWith(".used"));
            if (used != null) body.Children.Add(Stack(4, LabelValue(Translate(L.T(used.Label)), used, 13), PartMeter(used, Purple)));
            return Tile(body, Amber);
        }).ToArray();
        for (int i = 0; i < driveTiles.Length; i += 2) TileRow(driveTiles.Skip(i).Take(2).ToArray());

        var rates = Rs("Network");
        if (rates.Length > 0)
        {
            var top = Ui.Columns(".8*,1.2*", Part("router", 100, 76), Stack(5, PartTitle(L.T("Network"), Cyan, ""), NetworkValues(rates, 15))); top.ColumnSpacing = 8;
            var body = Stack(6, top);
            var down = R("down", "Network");
            if (down != null) body.Children.Add(HistoryPlot(down, 30, "bars", Cyan));
            TileRow(Tile(body, Cyan));
        }
        var fans = Rs("Cooling");
        if (fans.Length > 0) TileRow(Tile(Stack(8, PartTitle(L.T("Cooling"), Cyan, ""), FanGrid(fans, 50, true)), Cyan));
    }
}
