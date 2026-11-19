using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed partial class AioTile
{
    private bool AmberInstruments => style == WidgetStyle.AioAmberInstruments;
    private readonly List<(string Key, InstrumentGauge Gauge)> instrumentGauges = new();
    private readonly List<(string Key, string? Second, InstrumentHistory Chart)> instrumentHistory = new();

    private Color InstrumentAccent(WidgetKind kind, bool second = false)
    {
        var owner = source.Widgets.FirstOrDefault(w => w.Kind == kind);
        var custom = Color.TryParse(owner?.Accent, out var local) ? local
            : Color.TryParse(slot?.Accent, out var overall) ? overall : skin;
        var color = custom ?? C(AmberInstruments ? second ? "#EBEDEB" : "#FFB82E" : second ? "#D987C6" : "#73DCE0");
        return Accents.Readable(color, palette);
    }

    private Color InstrumentSurface(string dark, string light, double shade) => SeeThrough(palette.OwnBackground
        ? Palette.Mix(Solid(back), ink, shade) : C("#" + (lightSurface ? light : dark)));

    private TextBlock InstrumentLabel(string caption, double size, Color? color = null, bool title = false)
    {
        var text = DashboardText(caption, size, title);
        text.Foreground = Palette.Brush(color ?? (title ? ink : muted));
        return text;
    }

    private Control InstrumentModule(string id, string title, string glyph, Control body, WidgetKind kind)
    {
        var panel = Stack(5);
        panel.Margin = new Thickness(13, 7, 13, 9);
        if (!string.IsNullOrWhiteSpace(title))
        {
            var heading = InstrumentLabel(title, 13, title: true);
            heading.TextAlignment = TextAlignment.Center;
            var icons = Ui.Columns("Auto,*,Auto", InstrumentIcon(glyph), heading, InstrumentIcon(glyph));
            icons.ColumnSpacing = 6;
            panel.Children.Add(new Border { Child = icons, Padding = new Thickness(7, 3), CornerRadius = new CornerRadius(5),
                Background = Palette.Brush(InstrumentSurface("141818", "EDF1F2", .02)) });
        }
        panel.Children.Add(body);
        var shell = new InstrumentHousing(AmberInstruments,
            InstrumentSurface("383F3F", "CBD4D7", .21), InstrumentSurface("272C2C", "F5F7F8", .07),
            InstrumentSurface("171B1C", "E2E8EA", .02), InstrumentAccent(kind), InstrumentAccent(kind, true));
        var layer = new Grid { Children = { shell, panel } };
        return new Border { Child = new Border { Child = layer, ClipToBounds = true },
            Tag = settings.Horizontal ? "aio-section" : "dashboard-part:" + id };

        Control InstrumentIcon(string path)
        {
            var box = new Viewbox { Child = Ui.Glyph(path, muted, 14), Width = 14, Height = 14,
                VerticalAlignment = VerticalAlignment.Center, Tag = "graphic" };
            return box;
        }
    }

    private Control InstrumentDial(AioReading reading, int size, string unit, WidgetKind kind, bool secondary = false)
    {
        var color = InstrumentAccent(kind, secondary);
        var dial = InstrumentGraphic(reading, color, false, size, size);
        var value = V(reading, size >= 72 ? 28 : 22, ink);
        formats[value] = row => row.Number == null ? "—" : Display(row).Split(' ')[0].TrimEnd('%');
        value.Text = formats[value](reading);
        value.TextAlignment = TextAlignment.Center;
        value.TextWrapping = TextWrapping.NoWrap;
        var number = new Viewbox { Child = value, StretchDirection = StretchDirection.DownOnly, MaxWidth = size * .71,
            HorizontalAlignment = HorizontalAlignment.Center };
        var suffix = TextColours.Bind(InstrumentLabel(unit, 9), reading.Key);
        suffix.HorizontalAlignment = HorizontalAlignment.Center;
        var face = Stack(0, number, suffix);
        face.VerticalAlignment = VerticalAlignment.Center;
        return new InstrumentDialFace(dial, face, number);
    }

    private InstrumentGauge InstrumentGraphic(AioReading reading, Color color, bool column, double width, double height)
    {
        var gauge = new InstrumentGauge(AmberInstruments, column, color,
            InstrumentSurface("0F1314", "C9D1D4", .12), InstrumentSurface("323A3A", "F7FAFB", .25))
        {
            MaxWidth = width, Height = height, HorizontalAlignment = HorizontalAlignment.Stretch, Tag = "graphic"
        };
        instrumentGauges.Add((reading.Key, gauge));
        return TextColours.Bind(gauge, reading.Key);
    }

    private Control InstrumentLadder(AioReading reading, WidgetKind kind, bool secondary, string label = "", double height = 53)
    {
        var color = InstrumentAccent(kind, secondary);
        var column = Stack(3);
        if (label.Length > 0)
        {
            var text = TextColours.Bind(InstrumentLabel(label, 9, color), reading.Key);
            text.TextAlignment = TextAlignment.Center;
            column.Children.Add(text);
        }
        column.Children.Add(InstrumentGraphic(reading, color, true, 19, height));
        column.VerticalAlignment = VerticalAlignment.Center;
        return column;
    }

    private Control InstrumentRow(AioReading reading, WidgetKind kind, bool secondary = false, bool bar = true)
    {
        var color = InstrumentAccent(kind, secondary);
        string label = reading.Key.EndsWith(".temp") && DiskReading(reading) ? L.T("Temperature") : L.T(reading.Label);
        if (reading.Key is "aio.network.down" or "aio.network.up") label = reading.Key.EndsWith("down") ? "↓ " + label : "↑ " + label;
        var title = TextColours.Bind(InstrumentLabel(label, 10, bar ? color : muted), reading.Key);
        ToolTip.SetTip(title, label);
        var row = Ui.Columns("*,1.1*", title, FitRight(V(reading, 12)));
        row.ColumnSpacing = 5;
        if (!bar) return row;
        var meter = new InstrumentGauge(AmberInstruments, false, color,
            InstrumentSurface("101314", "D5DCDF", .10), InstrumentSurface("354343", "E7EDEF", .2), bar: true)
            { Height = 4, Tag = "graphic" };
        instrumentGauges.Add((reading.Key, meter));
        return Stack(2, row, TextColours.Bind(meter, reading.Key));
    }

    private Control InstrumentFaces(AioReading? first, AioReading? second, string name, string otherName,
        string unit, string otherUnit, WidgetKind kind)
    {
        var cells = new List<Control>();
        if (AmberInstruments && first != null) cells.Add(InstrumentLadder(first, kind, false, name));
        if (first != null) cells.Add(InstrumentDial(first, AmberInstruments ? 67 : 92, unit, kind));
        if (second != null) cells.Add(InstrumentDial(second, 67, otherUnit, kind, true));
        if (AmberInstruments && second != null) cells.Add(InstrumentLadder(second, kind, true, otherName));
        return FixedCols(Math.Max(1, cells.Count), cells.ToArray());
    }

    private Control InstrumentHardware(DashboardPart part)
    {
        var kind = part.Id == "cpu" ? WidgetKind.Cpu : WidgetKind.Gpu;
        var temp = part.Rows.FirstOrDefault(r => r.Key.EndsWith(".temp") && r.Group != "Cores");
        var load = part.Rows.FirstOrDefault(r => r.Key.EndsWith(".load"));
        var body = Stack(5);
        if (part.Model.Length > 0)
        {
            var model = InstrumentLabel(part.Model, 10.5);
            model.TextAlignment = TextAlignment.Center;
            ToolTip.SetTip(model, part.Model);
            body.Children.Add(model);
        }
        if (temp != null || load != null) body.Children.Add(InstrumentFaces(temp, load, L.T("Temp."), L.T("Load"),
            Format.TemperatureUnit(settings.Fahrenheit), AmberInstruments ? "%" : "% " + L.T("Load"), kind));
        var details = part.Rows.Where(r => r != temp && r != load && !r.Key.EndsWith(".vram"));
        var readings = details.Select((r, i) => InstrumentRow(r, kind, i % 2 != 0)).ToArray();
        if (readings.Length > 0) body.Children.Add(AmberInstruments ? Stack(3, readings) : InstrumentColumns(2, readings));
        if (kind == WidgetKind.Cpu && lastCoreLoads.Count >= 2 && AioReadings.Selected(settings,"aio.cpu.coreloads"))
        {
            coreBars = new AioCoreBars(ink, muted, track) { Loads = lastCoreLoads, Tint = InstrumentAccent(kind), NumbersOnly = true, Compact = true, Tag = "graphic" };
            body.Children.Add(TextColours.Bind(coreBars,"aio.cpu.coreloads"));
        }
        return InstrumentModule(part.Id, part.Title, Glyphs.Cpu, BodyInset(body), kind);
    }

    private Control? InstrumentPerformance(List<AioReading> rows, DashboardPart? network)
    {
        var fps = rows.Find(r => r.Key == AioReadings.InstrumentFps);
        var hz = rows.Find(r => r.Key == AioReadings.InstrumentRefresh);
        var cpu = rows.Find(r => r.Group == "CPU" && r.Key.EndsWith(".load"));
        var gpu = rows.Find(r => r.Group == "GPU" && r.Key.EndsWith(".load"));
        var graph = settings.Shows("aio.graphs") ? cpu ?? gpu : null;
        if (fps == null && hz == null && graph == null && network == null) return null;
        var body = Stack(5);
        if (fps != null || hz != null) body.Children.Add(InstrumentFaces(fps, hz, "FPS", "Hz", "FPS", "Hz", WidgetKind.Fps));
        if (network != null)
        {
            if (network.Model.Length > 0)
            {
                var name = InstrumentLabel(network.Model, 9);
                name.TextAlignment = TextAlignment.Center;
                body.Children.Add(name);
            }
            body.Children.Add(InstrumentColumns(2, network.Rows.Select(r => InstrumentRow(r, WidgetKind.Network,
                r.Key.EndsWith(".up"), bar: false)).ToArray()));
        }
        if (graph != null)
        {
            var other = cpu == null ? null : gpu;
            var color = InstrumentAccent(cpu == null ? WidgetKind.Gpu : WidgetKind.Cpu, cpu == null);
            var second = InstrumentAccent(WidgetKind.Gpu, true);
            TextBlock Caption(AioReading r, Color tint) => TextColours.Bind(
                TextFonts.Mark(InstrumentLabel(r.Group + " %", 9, tint), TextRole.Graph), r.Key);
            var first = Caption(graph, color);
            if (other == null) body.Children.Add(first);
            else
            {
                var right = Caption(other, second); right.TextAlignment = TextAlignment.Right;
                body.Children.Add(Ui.Columns("*,*", first, right));
            }
            var chart = new InstrumentHistory(color, second, InstrumentSurface("101415", "EEF3F4", .10),
                InstrumentSurface("353E3E", "D1DBDE", .25)) { Height = 47, Tag = "graphic" };
            instrumentHistory.Add((graph.Key, other?.Key, chart));
            body.Children.Add(TextColours.Bind(chart, graph.Key, other?.Key));
        }
        return InstrumentModule("net", network is { Title.Length: 0 } && fps == null && hz == null ? ""
            : network == null || network.Title == L.T("Network") ? L.T("Performance") : network.Title,
            Glyphs.Fps, BodyInset(body), WidgetKind.Network);
    }

    private Control? InstrumentCooling(DashboardPart? part)
    {
        if (part == null && !AioReadings.Selected(settings, "aio.cooling")) return null;
        var owner = source.Widgets.FirstOrDefault(w => w.Kind == WidgetKind.Cooling);
        var body = Stack(4);
        if (!string.IsNullOrWhiteSpace(part?.Model)) body.Children.Add(InstrumentLabel(part.Model, 10));
        if (part == null)
        {
            var empty = InstrumentLabel(L.T("No data"), 11);
            empty.TextAlignment = TextAlignment.Center;
            empty.Margin = new Thickness(0, 8);
            body.Children.Add(empty);
        }
        else
        {
            var fans = part.Rows.Where(r => r.Group == "Cooling").ToArray();
            var board = part.Rows.Where(r => r.Group != "Cooling").ToArray();
            if (fans.Length > 0) body.Children.Add(InstrumentColumns(2, fans.Select((r, i) => InstrumentRow(r, WidgetKind.Cooling, i % 2 != 0)).ToArray()));
            if (board.Length > 0) body.Children.Add(InstrumentColumns(2, board.Select(r => InstrumentRow(r, WidgetKind.Cooling, true, false)).ToArray()));
        }
        return InstrumentModule("fan", part?.Title ?? (owner?.ShowTitle == false ? "" : Naming.Title(owner, 0, L.T("Cooling"))),
            Glyphs.Fan, BodyInset(body), WidgetKind.Cooling);
    }

    private Control? InstrumentResources(List<AioReading> rows, List<DashboardPart> parts)
    {
        var resources = new List<Control>();
        var memory = parts.Find(p => p.Id == "ram");
        var vram = rows.Where(r => r.Group == "GPU" && r.Key.EndsWith(".vram")).ToArray();
        var order = DriveOrdering.Order(snapshot.Disks, DriveOrdering.Mode(settings.ForWidget(WidgetKind.Drives)))
            .Select((disk, index) => (disk.Id, Index: index)).ToDictionary(d => d.Id, d => d.Index);
        var drives = rows.Where(DiskReading).GroupBy(r => r.Device).OrderBy(d => order.GetValueOrDefault(d.Key, int.MaxValue)).ToArray();
        var gpu = source.Widgets.FirstOrDefault(w => w.Kind == WidgetKind.Gpu);
        var owner = DriveOwner;
        string memoryModel = PartModel(WidgetKind.Memory, 0, "");
        bool showModels = memoryModel.Length > 0 || settings.ShowNames && (drives.Length > 0 && owner?.ShowHardware != false
            || vram.Length > 1 && gpu?.ShowHardware != false);
        bool hasTitles = false;
        if (memory != null || vram.Length > 0)
        {
            var memoryCells = new List<Control>();
            if (memory != null)
            {
                hasTitles |= memory.Title.Length > 0;
                memoryCells.Add(InstrumentResourceCell(memory.Title, memoryModel, showModels, memory.Rows, "aio.system.ram", WidgetKind.Memory));
            }
            hasTitles |= vram.Length > 0 && gpu?.ShowTitle != false;
            memoryCells.AddRange(vram.Select((r, index) => InstrumentResourceCell(gpu?.ShowTitle == false ? "" : r.Label,
                vram.Length > 1 ? PartModel(WidgetKind.Gpu, index, snapshot.Gpus.FirstOrDefault(g => g.Id == r.Device)?.Name ?? "") : "",
                showModels, new[] { r }, r.Key, WidgetKind.Gpu, true)));
            bool showGroup = memory?.Title.Length > 0 || vram.Length > 0 && gpu?.ShowTitle != false;
            resources.Add(InstrumentResourceGroup(showGroup ? L.T("System") : "", memoryCells.ToArray()));
        }
        if (drives.Length > 0)
        {
            hasTitles |= owner?.ShowTitle != false;
            resources.Add(InstrumentResourceGroup(owner?.ShowTitle == false ? "" : L.T("Drives"), drives.Select((drive, ordinal) =>
            {
                var best = drive.FirstOrDefault(r => r.Key.EndsWith(".used"))?.Key;
                var device = snapshot.Disks.FirstOrDefault(d => d.Id == drive.Key);
                int index = device == null ? ordinal : snapshot.Disks.ToList().IndexOf(device);
                var name = owner?.ShowTitle == false ? "" : Naming.Title(owner, index, AioReadings.DriveName(snapshot, drive.Key));
                var model = PartModel(WidgetKind.Drives, index, device?.Name ?? "");
                return InstrumentResourceCell(name, model, showModels, drive, best, WidgetKind.Drives, ordinal % 2 != 0);
            }).ToArray()));
        }
        if (resources.Count == 0) return null;
        var separator = new Border { Background = Palette.Brush(InstrumentSurface("414849", "DAE1E4", .2)), Margin = new Thickness(0, 7) };
        var pair = resources.Count == 2 ? Ui.Columns("*,1,*", resources[0], separator, resources[1]) : resources[0];
        return InstrumentModule("ram", hasTitles ? L.T("Resources") : "", Glyphs.Drive, BodyInset(pair), WidgetKind.Memory);
    }

    private Control InstrumentResourceGroup(string title, Control[] children)
    {
        var cells = InstrumentColumns(2, children);
        if (title.Length == 0) return cells;
        var label = InstrumentLabel(title.ToUpperInvariant(), 9);
        label.TextAlignment = TextAlignment.Center;
        return Stack(4, label, cells);
    }

    private Grid InstrumentColumns(int columns, params Control[] children)
    {
        var grid = FixedCols(Math.Max(1, Math.Min(columns, children.Length)), children);
        grid.ColumnSpacing = 9;
        return grid;
    }

    private Control InstrumentResourceCell(string name, string model, bool showModels, IEnumerable<AioReading> cells, string? primary,
        WidgetKind kind, bool secondary = false)
    {
        var body = Stack(2);
        if (name.Length > 0) body.Children.Add(ResourceCaption(name, true));
        if (showModels || model.Length > 0) body.Children.Add(ResourceCaption(model.Length > 0 ? model : "\u00a0", false));
        TextBlock ResourceCaption(string text, bool heading)
        {
            var caption = InstrumentLabel(text, 9, title: heading);
            caption.TextAlignment = TextAlignment.Center;
            if (text.Trim().Length > 0) ToolTip.SetTip(caption, text);
            return caption;
        }
        foreach (var row in cells)
        {
            if (row.Key != primary) { body.Children.Add(InstrumentRow(row, kind, secondary, false)); continue; }
            var text = V(row, 14, ink);
            text.TextAlignment = TextAlignment.Center;
            if (row.Key.EndsWith(".vram"))
            {
                formats[text] = r => r.Number.HasValue ? Format.Gigabytes(r.Number.Value / 1024) : "—";
                text.Text = formats[text](row);
            }
            body.Children.Add(InstrumentLadder(row, kind, secondary, height: 35));
            body.Children.Add(new Viewbox { Child = text, StretchDirection = StretchDirection.DownOnly });
        }
        return body;
    }

    private Border BodyInset(Control body) => new() { Child = body, Padding = new Thickness(8, 1, 8, 0) };

    private void BuildInstrumentDashboard(List<AioReading> rows)
    {
        dashboardLimits.Clear(); dashboardTraffic = null; coreBars = null; mainboardCores = null;
        instrumentGauges.Clear(); instrumentHistory.Clear();
        DashboardHeightGrowth = 0; dashboardLayout = null;
        Padding = new Thickness(2); BorderThickness = new Thickness(0);
        Background = palette.OwnBackground ? Palette.Brush(palette.Card) : Brushes.Transparent;
        content.Spacing = 6;
        ReferenceHeader();
        var parts = DarkParts(rows.Where(r => r.Group != "Performance").ToList());
        var modules = parts.Where(p => p.Id is "cpu" or "gpu").Select(InstrumentHardware).ToList();
        if (InstrumentPerformance(rows, parts.Find(p => p.Id == "net")) is { } performance) modules.Add(performance);
        if (InstrumentCooling(parts.Find(p => p.Id == "fan")) is { } cooling) modules.Add(cooling);
        if (InstrumentResources(rows, parts) is { } resources) modules.Add(resources);
        foreach (var part in parts.Where(p => p.Id is "power" or "other"))
        {
            if (part.Id == "power" && !part.Rows.Any(r => r.Number.HasValue || settings.ActiveWidget?.AioEnabledSensors.Contains(r.Key) == true)) continue;
            var kind = part.Id == "power" ? WidgetKind.Power : WidgetKind.Overview;
            var body = Cols(2, part.Rows.Select(r => InstrumentRow(r, kind, bar: false)).ToArray());
            modules.Add(InstrumentModule(part.Id, part.Title, Glyphs.Power, BodyInset(body), kind));
        }
        if (modules.Count == 0) content.Children.Add(InstrumentLabel(L.T("No data"), 12));
        else if (!settings.Horizontal && source.Columns > 1)
        {
            dashboardLayout = new DashboardFlow(source.Columns, modules.Select(c => (c, 1d)).ToArray());
            content.Children.Add(dashboardLayout);
        }
        else foreach (var module in modules) content.Children.Add(module);
    }

    private void UpdateInstrumentDashboard()
    {
        foreach (var (key, gauge) in instrumentGauges) gauge.Set(readings.GetValueOrDefault(key)?.Fraction);
        foreach (var (key, other, chart) in instrumentHistory)
            chart.Set(Series(key), other == null ? Array.Empty<double>() : Series(other));
        double[] Series(string key)
        {
            var row = readings.GetValueOrDefault(key);
            var series = row != null && row.History.Length > 0 ? history.Read(row.History) : Array.Empty<double>();
            return series.Length > 0 ? series : samples.Read(key);
        }
    }
}
