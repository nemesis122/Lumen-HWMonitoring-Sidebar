using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

/// <summary>
/// The Cyberpunk and Neumorph designs (5.1.0). Cyberpunk: dark panels with a coloured edge and a cut corner, big glowing
/// numbers, uppercase captions in Oxanium. Neumorph: soft raised panels and pressed-in wells drawn from the card colour,
/// with rings (Dials, Hero) or bars (Bars). Every value is bound to its AIO sensor, so the sensor choices, the widget
/// switches and "only what the hardware has" work as in every AIO design; a value shown large on top is not repeated
/// below, and anything else the user ticks ends in a last panel instead of being lost.
/// </summary>
public sealed partial class AioTile
{
    private NeoDesigns.Look neo = NeoDesigns.All[0];
    private Color[] nAcc = { Colors.White };
    private Color nSurface, nDark, nLight, nInk, nLabel, nTrack;
    private FontFamily nFont = Ui.Inter;
    private bool Cyber => neo.Kind == NeoDesigns.Family.Cyber;
    private Color NA => nAcc[0];
    private Color NB => nAcc[Math.Min(1, nAcc.Length - 1)];
    private Color NAcc(int i) => nAcc[i % nAcc.Length];

    private void NeoDesign()
    {
        drivesPlaced = true; coreBars = null; mainboardCores = null;
        neo = NeoDesigns.For(designStyle);
        // A colour chosen for the card or in Appearance (or monochrome) replaces the design's accents.
        Color? own = Color.TryParse(slot?.Accent, out var picked) ? picked : skin;
        nAcc = own is Color o ? (Cyber ? new[] { o, Palette.Mix(o, Colors.White, .45) } : new[] { o }) : neo.Accents.Select(Color.Parse).ToArray();
        // An own background (Appearance) replaces the design's card colour; panels draw it opaque, the card keeps its alpha.
        nSurface = palette.OwnBackground ? Color.FromArgb(255, back.R, back.G, back.B) : Color.Parse(neo.Surface);
        nDark = Palette.Mix(nSurface, Colors.Black, .55); nLight = Palette.Mix(nSurface, Colors.White, .075);
        nInk = lightSurface ? ink : Color.Parse("#F1F4F8");
        nLabel = lightSurface ? muted : Color.Parse("#9AA1AC");
        nTrack = lightSurface ? track : Palette.Mix(nSurface, Colors.White, .1);
        nFont = Cyber ? Ui.DesignFont(Ui.FamilyFor("oxanium")) : Ui.DesignFont(Ui.Inter);
        Background = Palette.Brush(palette.OwnBackground ? palette.Card : SeeThrough(nSurface));
        BorderBrush = Palette.Brush(Cyber ? BlackDraw.A(NA, .45) : Palette.Mix(nSurface, Colors.White, .05));
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(Cyber ? 4 : 26);
        Padding = new Thickness(Cyber ? 8 : 14);
        content.Spacing = Cyber ? 10 : 16;

        var top = new HashSet<string>();
        if (Cyber) CyberParts(top); else NeumorphTop(top);

        // Everything not shown yet: the rest of every part in the design's own panels.
        if (!Cyber) NeumorphDetails(top);
        NeoMemory(top);
        NeoDrives(top);
        NeoNetwork(top);
        NeoFans(top);
        NeoRails(top);
        var extras = referenceRows.Where(r => !referenceUsed.Contains(r.Key) && !top.Contains(r.Key)).ToArray();
        if (extras.Length > 0) content.Children.Add(NPanel(Cyber ? "MISC_SENSORS" : "More", NGrid(extras.Select(r => NKV(r)), 2)));
        foreach (var r in referenceRows) referenceUsed.Add(r.Key);
    }

    // ------------------------------------------------------------------ Cyberpunk: processor and graphics
    private void CyberParts(HashSet<string> top)
    {
        var cpu = Rs("CPU");
        var gpus = Rs("GPU").GroupBy(r => r.Device).ToList();
        if (neo.Layout == NeoDesigns.Arrangement.Split && cpu.Length > 0 && gpus.Count > 0)
        {
            var first = gpus[0].ToArray();
            var left = CyberColumn("CPU", "cpu", cpu, top, NModelLine(WidgetKind.Cpu, 0, snapshot.Cpu?.Name));
            var right = CyberColumn(GpuTitle(first[0].Device, 0, true), GpuArt(first[0].Device), first, top,
                NModelLine(WidgetKind.Gpu, 0, snapshot.Gpus.FirstOrDefault(g => g.Id == first[0].Device)?.Name));
            var split = Ui.Columns("*,*", left, right); split.ColumnSpacing = 14;
            var body = Stack(10, split);
            if (CyberCores() is { } cores) body.Children.Add(cores);
            content.Children.Add(NPanel(null, body));
            for (int i = 1; i < gpus.Count; i++) CyberSection(gpus[i].ToArray(), i, top);
            return;
        }
        if (cpu.Length > 0)
        {
            var body = CyberHero("cpu", cpu, top);
            if (CyberCores() is { } cores) body.Children.Add(cores);
            content.Children.Add(NPanel("CPU_CORE", body, PartModel(WidgetKind.Cpu, 0, snapshot.Cpu?.Name ?? "")));
        }
        for (int i = 0; i < gpus.Count; i++) CyberSection(gpus[i].ToArray(), i, top);
    }

    private void CyberSection(AioReading[] rows, int index, HashSet<string> top)
    {
        var info = snapshot.Gpus.FirstOrDefault(g => g.Id == rows[0].Device);
        content.Children.Add(NPanel(GpuTitle(rows[0].Device, index, false), CyberHero(GpuArt(rows[0].Device), rows, top), PartModel(WidgetKind.Gpu, index, info?.Name ?? "")));
    }

    private string GpuTitle(string? device, int index, bool shortForm)
    {
        var info = snapshot.Gpus.FirstOrDefault(g => g.Id == device);
        if (info?.Integrated == true) return Cyber ? (shortForm ? "iGPU" : "IGPU_UNIT") : "Integrated graphics";
        if (Cyber) return shortForm ? (index == 0 ? "GPU" : "GPU " + (index + 1)) : index == 0 ? "GPU_UNIT" : "GPU_UNIT_" + (index + 1);
        return index == 0 ? "GPU" : "GPU " + (index + 1);
    }
    private string GpuArt(string? device) => "gpu";

    /// <summary>Picture (optional) and the big temperature left, the other values right, the history below.</summary>
    private StackPanel CyberHero(string art, AioReading[] rows, HashSet<string> top)
    {
        var main = rows.FirstOrDefault(r => r.Key.EndsWith(".temp")) ?? rows.FirstOrDefault(r => r.Key.EndsWith(".load")) ?? rows[0];
        top.Add(main.Key);
        var left = Stack(2);
        if (neo.Parts) left.Children.Add(NPart(art, art == "gpu" ? 128 : 104));
        left.Children.Add(NBig(main, 46));
        var list = Stack(3, Ordered(rows.Where(r => r != main)).Select(r => NKV(r)).ToArray());
        var row = Ui.Columns("Auto,*", left, new Border { VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(14, 0, 0, 4), Child = list });
        var body = Stack(8, row);
        if (NHistory(main) is { } plot) body.Children.Add(plot);
        if (main.Group == "CPU") AddCoreTemps(body);
        return body;
    }

    /// <summary>Split layout: one column of a part (tag, picture, big number, values, history).</summary>
    private Control CyberColumn(string tag, string art, AioReading[] rows, HashSet<string> top, Control? model)
    {
        var main = rows.FirstOrDefault(r => r.Key.EndsWith(".temp")) ?? rows.FirstOrDefault(r => r.Key.EndsWith(".load")) ?? rows[0];
        top.Add(main.Key);
        var label = NText(tag, 10.5, Colors.Black, true); label.LetterSpacing = 1.5;
        var badge = new Border { Background = Palette.Brush(NA), Padding = new Thickness(6, 1), HorizontalAlignment = HorizontalAlignment.Left, Child = label };
        var column = Stack(4, badge);
        if (model != null) column.Children.Add(model);
        if (neo.Parts) column.Children.Add(NPart(art, art == "gpu" ? 140 : 118));
        column.Children.Add(NBig(main, 52));
        foreach (var r in Ordered(rows.Where(r => r != main))) column.Children.Add(NKV(r));
        if (NHistory(main) is { } plot) column.Children.Add(plot);
        if (main.Group == "CPU") AddCoreTemps(column);
        return column;
    }

    private Control? CyberCores()
    {
        if (lastCoreLoads.Count < 2 || !AioReadings.Selected(settings, "aio.cpu.coreloads") || Rs("CPU").Length == 0) return null;
        coreBars = new AioCoreBars(nInk, nLabel, nTrack) { Loads = lastCoreLoads, Tint = NA, Heatmap = true, Tag = "graphic" };
        TextColours.Bind(coreBars, "aio.cpu.coreloads");
        var caption = NText(Cyber ? "THREAD_MATRIX" : "Cores", 10.5, Cyber ? NA : nLabel, true); TextFonts.Mark(caption, TextRole.Title);
        return Stack(6, caption, coreBars);
    }

    private void AddCoreTemps(StackPanel body)
    {
        var temps = Rs("Cores");
        if (temps.Length == 0) return;
        body.Children.Add(NGrid(temps.Select(r => NKV(r, r.Label.Replace("Core", "C"))), 4));
    }

    // ------------------------------------------------------------------ Neumorph: rings or bars on top
    private void NeumorphTop(HashSet<string> top)
    {
        var cpuTemp = R("temp") ?? R("load");
        var gpus = Rs("GPU").GroupBy(r => r.Device).Select(g => g.ToArray()).ToList();
        AioReading? Main(AioReading[] rows) => rows.FirstOrDefault(r => r.Key.EndsWith(".temp")) ?? rows.FirstOrDefault(r => r.Key.EndsWith(".load"));
        var ram = R("ram", "System");
        var drives = PartDrives();
        var items = new List<(AioReading R, string Caption, string Art, int Colour)>();
        if (cpuTemp != null) items.Add((cpuTemp, "CPU", "cpu", 0));
        for (int i = 0; i < gpus.Count; i++) if (Main(gpus[i]) is { } g) items.Add((g, GpuTitle(g.Device, i, true), "gpu", 1));
        if (ram != null) items.Add((ram, "RAM", "ram", 2));
        foreach (var d in drives) if (d.Temp != null) items.Add((d.Temp, AioReadings.DriveName(snapshot, d.Id), "ssd", 3));
        foreach (var i in items) top.Add(i.R.Key);
        if (items.Count == 0) return;

        if (neo.Layout == NeoDesigns.Arrangement.Bars)
        {
            foreach (var (r, caption, art, colour) in items) content.Children.Add(NBarRow(r, caption, art, NAcc(colour)));
        }
        else if (neo.Layout == NeoDesigns.Arrangement.Hero)
        {
            var (r, caption, _, colour) = items[0];
            var hero = NDial(r, caption, NAcc(colour), 220);
            hero.HorizontalAlignment = HorizontalAlignment.Center;
            content.Children.Add(new Border { Tag = "aio-section", Child = hero });
            var rest = items.Skip(1).Select(i => (Control)NDial(i.R, i.Caption, NAcc(i.Colour), 96)).ToArray();
            if (rest.Length > 0) content.Children.Add(new Border { Tag = "aio-section", Child = NGrid(rest, Math.Min(3, rest.Length), 12) });
        }
        else
        {
            var dials = items.Select(i => (Control)NDial(i.R, i.Caption, NAcc(i.Colour), 138)).ToArray();
            content.Children.Add(new Border { Tag = "aio-section", Child = NGrid(dials, 2, 14) });
        }
        if (CyberCores() is { } cores) content.Children.Add(NRaised(cores, new Thickness(14)));
    }

    /// <summary>A raised ring: coloured arc around a pressed-in centre with the value and its caption.</summary>
    private Control NDial(AioReading r, string caption, Color colour, double size)
    {
        // Font sizes follow the design size, not the graphic size: the size settings scale them on their own.
        double design = size;
        size *= settings.ActiveWidget?.Graphics ?? 1;
        var ring = new AioRing(colour, nTrack, false, true) { Width = size, Height = size };
        rings.Add((r.Key, ring));
        double inner = size * .66;
        var well = new Border
        {
            Width = inner, Height = inner, CornerRadius = new CornerRadius(inner / 2), Background = Palette.Brush(SeeThrough(nSurface)),
            BoxShadow = Inset(4), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
        };
        double big = design > 150 ? 40 : design > 110 ? 28 : 20;
        // One text with its unit in a box of fixed width, centred: a value that gains or loses a digit moves nothing.
        var number = NV(r, big); number.TextAlignment = TextAlignment.Center; number.HorizontalAlignment = HorizontalAlignment.Stretch; number.TextWrapping = TextWrapping.NoWrap;
        if (r.Key == "aio.system.ram") { formats[number] = row => Format.Percent(row.Number); number.Text = formats[number](r); }
        else if (r.Key.EndsWith(".temp") || r.Key.StartsWith("aio.system.disk.")) { formats[number] = row => Format.Temperature(row.Number, settings.Fahrenheit) + " " + Format.TemperatureUnit(settings.Fahrenheit); number.Text = formats[number](r); }
        var value = new Border { Width = big * 4, Child = number, HorizontalAlignment = HorizontalAlignment.Center };
        var label = NText(caption, design > 150 ? 12 : 10, nLabel, true); label.HorizontalAlignment = HorizontalAlignment.Center;
        var face = Stack(0, value, label); face.HorizontalAlignment = HorizontalAlignment.Center; face.VerticalAlignment = VerticalAlignment.Center;
        var disc = new Border
        {
            Width = size, Height = size, CornerRadius = new CornerRadius(size / 2), Background = Palette.Brush(SeeThrough(nSurface)), BoxShadow = Raised(7)
        };
        var dial = new Grid { Width = size, Height = size, Children = { disc, ring, well, Squeeze(face, size * .2) } };
        // A narrow sidebar or a large graphic size: the whole ring shrinks to its column instead of leaving the card.
        return new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, MaxWidth = size, MaxHeight = size,
            HorizontalAlignment = HorizontalAlignment.Center, Child = dial };
    }

    /// <summary>A raised row: picture in a well (Parts designs), caption and value, a bar in a groove.</summary>
    private Control NBarRow(AioReading r, string caption, string art, Color colour)
    {
        var value = r.Key == "aio.system.ram" ? NPercent(r, 24) : NBig(r, 24, false);
        var head = Ui.Columns("*,Auto", FitLeft(NText(caption, 11.5, nLabel, true)), value);
        var meter = new AioMeter(colour, Color.FromArgb(0, 0, 0, 0), 0) { Height = 8, Margin = new Thickness(3) };
        meters.Add((r.Key, meter));
        var groove = new Border { CornerRadius = new CornerRadius(7), Background = Palette.Brush(SeeThrough(nSurface)), BoxShadow = Inset(3), Child = meter };
        var info = Stack(8, head, groove);
        if (r.Key == "aio.system.ram") { var amount = NText(Amount(r), 11, nLabel, false, true); formats[amount] = Amount; AddField(r.Key, amount); info.Children.Add(FitLeft(amount)); }
        Control body = info;
        if (neo.Parts)
        {
            var well = new Border
            {
                Width = 76, Height = 62, CornerRadius = new CornerRadius(16), Background = Palette.Brush(SeeThrough(nSurface)), BoxShadow = Inset(4),
                Child = NPart(art, 62), Tag = "graphic"
            };
            var row = Ui.Columns("Auto,*", well, new Border { VerticalAlignment = VerticalAlignment.Center, Child = info }); row.ColumnSpacing = 14; body = row;
        }
        return NRaised(body, new Thickness(14, 12));
    }

    /// <summary>Neumorph: the remaining processor and graphics values in one panel, two columns.</summary>
    private void NeumorphDetails(HashSet<string> top)
    {
        var parts = new List<Control>();
        var cpu = Ordered(Rs("CPU").Where(r => !top.Contains(r.Key)));
        if (cpu.Count > 0 || Rs("Cores").Length > 0)
        {
            var column = Stack(3, NHeading("CPU"));
            if (NModelLine(WidgetKind.Cpu, 0, snapshot.Cpu?.Name) is { } cpuModel) column.Children.Add(cpuModel);
            foreach (var r in cpu) column.Children.Add(NKV(r));
            if (R("temp") is { } t && NHistory(t) is { } plot) column.Children.Add(plot);
            AddCoreTemps(column);
            parts.Add(column);
        }
        int index = 0;
        foreach (var gpu in Rs("GPU").GroupBy(r => r.Device))
        {
            var rows = Ordered(gpu.Where(r => !top.Contains(r.Key)));
            var info = snapshot.Gpus.FirstOrDefault(g => g.Id == gpu.Key);
            var column = Stack(3, NHeading(GpuTitle(gpu.Key, index, false)));
            if (NModelLine(WidgetKind.Gpu, index, info?.Name) is { } gpuModel) column.Children.Add(gpuModel);
            foreach (var r in rows) column.Children.Add(NKV(r));
            var main = gpu.FirstOrDefault(r => r.Key.EndsWith(".temp"));
            if (main != null && NHistory(main) is { } plot) column.Children.Add(plot);
            parts.Add(column); index++;
        }
        if (parts.Count == 0) return;
        for (int i = 0; i < parts.Count; i += 2)
            content.Children.Add(NRaised(NGrid(parts.Skip(i).Take(2), 2, 16), new Thickness(16, 14)));
    }

    // ------------------------------------------------------------------ the other parts (both families)
    private void NeoMemory(HashSet<string> top)
    {
        var ram = R("ram", "System");
        var more = Rs("System").Where(r => r.Key.StartsWith("aio.system.ram.") && !AioReadings.IsGraphKey(r.Key)).ToArray();
        if (ram == null && more.Length == 0) return;
        var body = Stack(6);
        if (ram != null && !top.Contains(ram.Key))
        {
            var amount = NText(Amount(ram), 13, nInk, false, true); formats[amount] = Amount; AddField(ram.Key, amount);
            var percent = NPercent(ram, 15);
            body.Children.Add(Ui.Columns("*,Auto", FitLeft(amount), percent));
            var meter = new AioMeter(NA, nTrack, 0) { Height = 6 }; meters.Add((ram.Key, meter)); body.Children.Add(meter);
            if (NHistory(ram) is { } plot) body.Children.Add(plot);
        }
        else if (ram != null)
        {
            // Bars show the amount in their memory row already.
            if (neo.Layout != NeoDesigns.Arrangement.Bars)
            {
                var amount = NText(Amount(ram), 13, nInk, false, true); formats[amount] = Amount; AddField(ram.Key, amount);
                body.Children.Add(FitLeft(amount));
            }
            if (NHistory(ram) is { } plot) body.Children.Add(plot);
        }
        foreach (var r in more) body.Children.Add(NKV(r));
        if (body.Children.Count > 0) content.Children.Add(NPanel(Cyber ? "MEM_ALLOC" : "Memory", body));
    }

    private void NeoDrives(HashSet<string> top)
    {
        var drives = PartDrives();
        if (drives.Length == 0) return;
        var blocks = new List<Control>();
        foreach (var d in drives)
        {
            var name = NText(AioReadings.DriveName(snapshot, d.Id) + (DriveModel(d.Id) is { Length: > 0 } m ? "  " + m : ""), 12, Cyber ? NB : nInk, true);
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            Control head = d.Temp != null && !top.Contains(d.Temp.Key) ? Ui.Columns("*,Auto", name, NV(d.Temp, 14)) : name;
            var block = Stack(4, head);
            var rates = d.Rows.Where(r => !r.Key.EndsWith(".used")).Select(r => NKV(r)).ToArray();
            if (rates.Length > 0) block.Children.Add(NGrid(rates, 2, 12));
            if (d.Rows.FirstOrDefault(r => r.Key.EndsWith(".used")) is { } used)
            {
                var meter = new AioMeter(NAcc(3), nTrack, 0) { Height = 5, VerticalAlignment = VerticalAlignment.Center }; meters.Add((used.Key, meter));
                var line = Ui.Columns("*,Auto", meter, NV(used, 11)); line.ColumnSpacing = 8; block.Children.Add(line);
            }
            if (d.Temp != null && NHistory(d.Temp) is { } plot) block.Children.Add(plot);
            blocks.Add(block);
        }
        content.Children.Add(NPanel(Cyber ? "STORAGE_ARRAY" : "Storage", Stack(12, blocks.ToArray())));
    }

    private void NeoNetwork(HashSet<string> top)
    {
        var net = Rs("Network").Where(r => !top.Contains(r.Key)).ToArray();
        if (net.Length == 0) return;
        var down = net.FirstOrDefault(r => r.Key.EndsWith(".down")); var up = net.FirstOrDefault(r => r.Key.EndsWith(".up"));
        var link = net.FirstOrDefault(r => r.Key.EndsWith(".link"));
        var body = Stack(5);
        foreach (var r in net.Where(r => r != link)) body.Children.Add(NKV(r, r == down ? (Cyber ? "RX ↓" : "Download") : r == up ? (Cyber ? "TX ↑" : "Upload") : null));
        if (link != null) body.Children.Add(NKV(link, Cyber ? "LINK" : "Link"));
        var first = down ?? up;
        if (first != null && AioReadings.Graphs(settings, first.Key))
            body.Children.Add(HistoryPlot(first, 36, "area", NB, first == down ? up : null));
        content.Children.Add(NPanel(Cyber ? "NET_UPLINK" : "Network", body));
    }

    private void NeoFans(HashSet<string> top)
    {
        var fans = Rs("Cooling").Where(r => !top.Contains(r.Key)).ToArray();
        if (fans.Length == 0) return;
        var cells = fans.Select((f, i) =>
        {
            var value = NV(f, Cyber ? 20 : 17); formats[value] = row => row.Number.HasValue ? Format.Number(row.Number) : "—"; value.Text = formats[value](f);
            value.HorizontalAlignment = HorizontalAlignment.Center;
            var name = NText(Naming.Short(Translate(L.T(f.Label))), 9.5, nLabel, true); name.HorizontalAlignment = HorizontalAlignment.Center;
            name.TextTrimming = TextTrimming.CharacterEllipsis; name.TextWrapping = TextWrapping.NoWrap;
            var cell = Stack(2);
            if (neo.Parts && Cyber) cell.Children.Add(NPart(FanArt[i % FanArt.Length], 36));
            cell.Children.Add(Fit(value)); cell.Children.Add(name);
            if (Cyber) return (Control)cell;
            return new Border { CornerRadius = new CornerRadius(16), Background = Palette.Brush(SeeThrough(nSurface)), BoxShadow = Raised(4), Padding = new Thickness(6, 10), Margin = new Thickness(3), Child = cell };
        }).ToArray();
        var grid = NGrid(cells, Cyber ? 4 : 3, Cyber ? 8 : 6);
        if (AioReadings.Graphs(settings, fans[0].Key) && fans.FirstOrDefault(f => f.Number > 0) is { } spinning)
            content.Children.Add(NPanel(Cyber ? "COOLING_ARRAY" : "Fans", Stack(8, grid, HistoryPlot(spinning, 28, "area", NA))));
        else content.Children.Add(NPanel(Cyber ? "COOLING_ARRAY" : "Fans", grid));
    }

    private void NeoRails(HashSet<string> top)
    {
        var rails = Rs("Rails").Where(r => !top.Contains(r.Key)).ToArray();
        if (rails.Length == 0) return;
        content.Children.Add(NPanel(Cyber ? "POWER_RAILS" : "Power supply", NGrid(rails.Select(r => NKV(r, r.Label)), 3, 10)));
    }

    // ------------------------------------------------------------------ pieces
    private TextBlock NText(string text, double size, Color color, bool bold = false, bool number = false)
    {
        var t = Text(text, size, color, number); TextFonts.Mark(t, TextRole.Label); t.FontFamily = nFont;
        t.FontWeight = bold ? FontWeight.Bold : FontWeight.SemiBold; t.TextWrapping = TextWrapping.NoWrap; return t;
    }
    private TextBlock NV(AioReading r, double size, Color? color = null)
    {
        var t = V(r, size, color ?? nInk); t.FontFamily = nFont; t.FontWeight = FontWeight.Bold; return t;
    }
    private void AddField(string key, TextBlock t) { TextFonts.Mark(t, TextRole.Value); if (t is AioValueText v) v.Hint = "888.8 / 888.8 GB"; if (!fields.TryGetValue(key, out var list)) fields[key] = list = new(); list.Add(t); }

    /// <summary>The big number of a part: temperature with its unit small after it; Cyberpunk adds a coloured offset glow.</summary>
    private Control NBig(AioReading r, double size, bool left = true)
    {
        var t = NV(r, size);
        bool temperature = r.Key.EndsWith(".temp") || r.Key.StartsWith("aio.system.disk.");
        Control face = t;
        if (temperature)
        {
            formats[t] = row => Format.Temperature(row.Number, settings.Fahrenheit); t.Text = formats[t](r);
            var unit = NText(Format.TemperatureUnit(settings.Fahrenheit), size * .36, nLabel); unit.VerticalAlignment = VerticalAlignment.Bottom; unit.Margin = new Thickness(2, 0, 0, size * .14);
            face = new StackPanel { Orientation = Orientation.Horizontal, Children = { t, unit } };
        }
        if (Cyber) t.Effect = new DropShadowEffect { OffsetX = 2.5, OffsetY = 0, BlurRadius = 0, Color = BlackDraw.A(NB, .5) };
        var value = new AioReferenceValue(t, face);
        value.HorizontalAlignment = left ? HorizontalAlignment.Left : HorizontalAlignment.Center;
        return value;
    }
    private Control NPercent(AioReading r, double size)
    {
        var t = NV(r, size); formats[t] = row => row.Number is double n ? Math.Round(n).ToString("0") : "—"; t.Text = formats[t](r);
        var unit = NText("%", size * .4, nLabel); unit.VerticalAlignment = VerticalAlignment.Bottom; unit.Margin = new Thickness(1, 0, 0, size * .14);
        return new AioReferenceValue(t, new StackPanel { Orientation = Orientation.Horizontal, Children = { t, unit } }) { HorizontalAlignment = HorizontalAlignment.Center };
    }
    /// <summary>Caption left, value right; the value shrinks rather than leave the panel.</summary>
    private Control NKV(AioReading r, string? caption = null)
    {
        caption ??= Cyber ? PanelCaption(r) : RefLabel(r);
        var row = Ui.Columns("Auto,*", NText(Cyber ? caption.ToUpperInvariant() : caption, 11.5, nLabel), FitRight(NV(r, 13.5))); row.ColumnSpacing = 8;
        return row;
    }
    private Control NGrid(IEnumerable<Control> items, int columns, double gap = 10)
    {
        var array = items.ToArray();
        var grid = FixedCols(Math.Min(columns, Math.Max(1, array.Length)), array);
        grid.ColumnSpacing = gap; grid.RowSpacing = Math.Min(gap, 8); return grid;
    }
    private Control? NHistory(AioReading r) => AioReadings.Graphs(settings, r.Key) ? HistoryPlot(r, Cyber ? 32 : 30, "area", Cyber ? NA : NAcc(r.Group == "GPU" ? 1 : r.Group == "System" ? 2 : 0)) : null;
    private Control NPart(string name, double width)
    {
        width *= settings.ActiveWidget?.Graphics ?? 1;
        bool own = Color.TryParse(slot?.Accent, out _) || skin != null;
        string file = neo.RedParts && !own ? name + "_red" : name;
        return new Image
        {
            Source = isometricParts[file].Value, MaxWidth = width, MaxHeight = width * .75, Stretch = Stretch.Uniform, Tag = "graphic",
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center
        };
    }
    private TextBlock? NModelLine(WidgetKind kind, int index, string? model)
    {
        var name = PartModel(kind, index, model ?? "");
        if (name.Length == 0) return null;
        var t = NText(name, 10, nLabel); t.TextTrimming = TextTrimming.CharacterEllipsis; t.Margin = new Thickness(0, -2, 0, 4); return t;
    }
    private TextBlock NHeading(string title)
    {
        var t = NText(Cyber ? title.ToUpperInvariant() : title, Cyber ? 11.5 : 11, Cyber ? NA : nLabel, true); TextFonts.Mark(t, TextRole.Title);
        t.LetterSpacing = Cyber ? 1.6 : .8; t.TextTrimming = TextTrimming.CharacterEllipsis; return t;
    }

    /// <summary>A panel of the design: Cyberpunk edge panel or Neumorph raised panel, with an optional heading.</summary>
    private Control NPanel(string? title, Control body, string? model = null)
    {
        Control? heading = title == null ? null : NHeading(title);
        if (heading != null && !string.IsNullOrEmpty(model))
        {
            // The part's name as the user or the hardware gives it (not upper-cased), after the panel title.
            var name = NText(model, Cyber ? 11.5 : 11, Cyber ? NA : nLabel, true); name.TextTrimming = TextTrimming.CharacterEllipsis;
            ((TextBlock)heading).Text += " //";
            var row = Ui.Columns("Auto,*", heading, name); row.ColumnSpacing = 6; heading = row;
        }
        var inner = heading == null ? body : Stack(Cyber ? 8 : 10, heading, body);
        if (!Cyber) return NRaised(inner, new Thickness(16, 14));
        var panel = new Border
        {
            Padding = new Thickness(12, 10, 14, 12), Child = inner, Tag = "aio-section", BorderBrush = Palette.Brush(NA), BorderThickness = new Thickness(3, 0, 0, 0),
            CornerRadius = new CornerRadius(0, 14, 0, 0),
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                GradientStops = { new(SeeThrough(BlackDraw.A(NA, .13)), 0), new(SeeThrough(BlackDraw.A(NA, .02)), .75), new(SeeThrough(BlackDraw.A(NA, .02)), 1) }
            }
        };
        // The cut corner of the panel, drawn as a small accent triangle.
        var notch = new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse("M0,0 L12,0 L12,12 Z"), Fill = Palette.Brush(NA), Width = 12, Height = 12, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
        return new Grid { Tag = "aio-section", Children = { panel, notch } };
    }
    private Border NRaised(Control body, Thickness padding) => new()
    {
        Padding = padding, Child = body, Tag = "aio-section", CornerRadius = new CornerRadius(22), Margin = new Thickness(4),
        Background = Palette.Brush(SeeThrough(nSurface)), BoxShadow = Raised(8)
    };
    private BoxShadows Raised(double d) => new(new BoxShadow { OffsetX = d, OffsetY = d, Blur = d * 2, Color = SeeThrough(nDark) },
        new[] { new BoxShadow { OffsetX = -d, OffsetY = -d, Blur = d * 2, Color = SeeThrough(nLight) } });
    private BoxShadows Inset(double d) => new(new BoxShadow { OffsetX = d, OffsetY = d, Blur = d * 2, Color = SeeThrough(nDark), IsInset = true },
        new[] { new BoxShadow { OffsetX = -d, OffsetY = -d, Blur = d * 2, Color = SeeThrough(nLight), IsInset = true } });
}
