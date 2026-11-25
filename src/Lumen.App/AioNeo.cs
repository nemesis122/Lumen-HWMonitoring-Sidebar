using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

/// <summary>
/// The Cyberpunk and Neumorph designs (5.1.0) and the ten art designs (5.2.0). Cyberpunk: dark panels with a coloured
/// edge and a cut corner, big glowing numbers, uppercase captions in Oxanium. Neumorph: soft raised panels and pressed-in
/// wells drawn from the card colour, with rings (Dials, Hero) or bars (Bars). The art designs use the same two skeletons
/// (panels per part, or rings/tiles/rows on top) with their own panels, fonts and decorations: LED dot matrix, Tron grid,
/// code rain, Tokyo neon, thermal camera, phone widgets, oscilloscope, stealth, black and gold, sumi-e ink.
/// Every value is bound to its AIO sensor, so the sensor choices, the widget switches and "only what the hardware has" work
/// as in every AIO design; a value shown large on top is not repeated below, and anything else the user ticks ends in a
/// last panel instead of being lost.
/// </summary>
public sealed partial class AioTile
{
    private NeoDesigns.Look neo = NeoDesigns.All[0];
    private Color[] nAcc = { Colors.White };
    private Color nSurface, nDark, nLight, nInk, nLabel, nTrack, nPanel;
    private FontFamily nFont = Ui.Inter, nValueFont = Ui.Inter;
    private NeoDesigns.Family Fam => neo.Kind;
    private bool Cyber => Fam == NeoDesigns.Family.Cyber;
    private bool Soft => Fam == NeoDesigns.Family.Neumorph;
    /// <summary>Designs built from panels, one per part (processor and graphics first).</summary>
    private bool PanelPath => Fam is NeoDesigns.Family.Cyber or NeoDesigns.Family.Rain or NeoDesigns.Family.Neon or NeoDesigns.Family.Scope or NeoDesigns.Family.Stealth
        or NeoDesigns.Family.Vfd or NeoDesigns.Family.Mission or NeoDesigns.Family.Swiss or NeoDesigns.Family.Tilted or NeoDesigns.Family.Chrome
        || gal?.Arrangement is GalleryDesigns.Layout.Table or GalleryDesigns.Layout.BigNumbers or GalleryDesigns.Layout.Segments or GalleryDesigns.Layout.Split;
    /// <summary>Designs that write captions and titles in capitals.</summary>
    private bool Upper => Fam is NeoDesigns.Family.Cyber or NeoDesigns.Family.Matrix or NeoDesigns.Family.Tron or NeoDesigns.Family.Rain
        or NeoDesigns.Family.Neon or NeoDesigns.Family.Scope or NeoDesigns.Family.Stealth or NeoDesigns.Family.Thermal
        or NeoDesigns.Family.Vfd or NeoDesigns.Family.Synth or NeoDesigns.Family.Radar or NeoDesigns.Family.Hex or NeoDesigns.Family.Pixel or NeoDesigns.Family.Mission
        or NeoDesigns.Family.Equalizer or NeoDesigns.Family.Skyline or NeoDesigns.Family.Tilted or NeoDesigns.Family.Exploded or NeoDesigns.Family.Bars3D
        or NeoDesigns.Family.Hologram or NeoDesigns.Family.Tower || gal?.Capitals == true;
    /// <summary>Designs whose big numbers glow in the accent.</summary>
    private bool Glowing => Fam is NeoDesigns.Family.Matrix or NeoDesigns.Family.Tron or NeoDesigns.Family.Rain or NeoDesigns.Family.Neon or NeoDesigns.Family.Scope
        or NeoDesigns.Family.Vfd or NeoDesigns.Family.Synth or NeoDesigns.Family.Radar or NeoDesigns.Family.Equalizer
        or NeoDesigns.Family.Skyline or NeoDesigns.Family.Tilted or NeoDesigns.Family.Hologram
        || gal?.Look == GalleryDesigns.Panel.Glow;
    private Color NA => nAcc[0];
    private Color NB => nAcc[Math.Min(1, nAcc.Length - 1)];
    private Color NAcc(int i) => nAcc[i % nAcc.Length];

    private void NeoDesign()
    {
        drivesPlaced = true; coreBars = null; mainboardCores = null;
        neo = NeoDesigns.For(designStyle); gal = neo.Gallery;
        // A colour chosen for the card or in Appearance (or monochrome) replaces the design's accents.
        Color? own = Color.TryParse(slot?.Accent, out var picked) ? picked : skin;
        nAcc = own is Color o ? (Cyber ? new[] { o, Palette.Mix(o, Colors.White, .45) } : new[] { o }) : neo.Accents.Select(Color.Parse).ToArray();
        // An own background (Appearance) replaces the design's card colour; panels draw it opaque, the card keeps its alpha.
        nSurface = palette.OwnBackground ? Color.FromArgb(255, back.R, back.G, back.B) : Color.Parse(neo.Surface);
        nDark = Palette.Mix(nSurface, Colors.Black, .55); nLight = Palette.Mix(nSurface, Colors.White, .075);
        nInk = lightSurface ? ink : Color.Parse(Fam switch
        {
            NeoDesigns.Family.Matrix => "#FFE0BA", NeoDesigns.Family.Rain => "#C8FFD4", NeoDesigns.Family.Scope => "#D2FFE4", NeoDesigns.Family.Tron => "#E2FBFF",
            NeoDesigns.Family.Stealth => "#E4E4E8", NeoDesigns.Family.Gold => "#F1E7D0", NeoDesigns.Family.Ink => "#F2EFE8", _ => "#F1F4F8"
        });
        nLabel = lightSurface ? muted : Fam switch
        {
            // Greyish tints, so a chosen label colour still replaces them.
            NeoDesigns.Family.Matrix => Color.Parse("#B8A58A"), NeoDesigns.Family.Rain or NeoDesigns.Family.Scope => Color.Parse("#8FAA97"),
            NeoDesigns.Family.Gold => Color.Parse("#B9AD92"), NeoDesigns.Family.Ink => Color.Parse("#9A968E"), NeoDesigns.Family.Phone => Color.Parse("#8E8E93"),
            NeoDesigns.Family.Stealth => Color.Parse("#7A7A82"), _ => Color.Parse("#9AA1AC")
        };
        nTrack = lightSurface ? track : Fam switch
        {
            NeoDesigns.Family.Matrix => Palette.Mix(NA, Colors.Black, .85), NeoDesigns.Family.Tron or NeoDesigns.Family.Neon or NeoDesigns.Family.Rain or NeoDesigns.Family.Scope => Palette.Mix(NA, nSurface, .82),
            NeoDesigns.Family.Gold => Palette.Mix(NA, nSurface, .8), NeoDesigns.Family.Phone => Color.Parse("#2C2C2E"), _ => Palette.Mix(nSurface, Colors.White, .1)
        };
        // The colour of a panel or tile, drawn from the card colour so an own background reaches it.
        nPanel = Fam switch
        {
            NeoDesigns.Family.Phone => Palette.Mix(nSurface, Colors.White, .11), NeoDesigns.Family.Thermal => Palette.Mix(nSurface, Colors.White, .05),
            NeoDesigns.Family.Stealth => Palette.Mix(nSurface, Colors.White, .03), NeoDesigns.Family.Matrix => Palette.Mix(nSurface, NA, .03),
            NeoDesigns.Family.Scope => Palette.Mix(nSurface, NA, .04), _ => nSurface
        };
        nFont = Ui.DesignFont(Fam switch
        {
            NeoDesigns.Family.Cyber or NeoDesigns.Family.Tron => Ui.FamilyFor("oxanium"),
            NeoDesigns.Family.Matrix or NeoDesigns.Family.Scope => Ui.FamilyFor("ibm-plex-mono"),
            NeoDesigns.Family.Rain or NeoDesigns.Family.Thermal => Ui.FamilyFor("jetbrains-mono"),
            NeoDesigns.Family.Neon or NeoDesigns.Family.Stealth or NeoDesigns.Family.Gold or NeoDesigns.Family.Ink => Ui.FamilyFor("lato"),
            _ => Ui.Inter
        });
        nValueFont = Ui.DesignFont(Fam switch
        {
            NeoDesigns.Family.Matrix => Ui.Doto, NeoDesigns.Family.Stealth => Ui.FamilyFor("anton"),
            NeoDesigns.Family.Gold or NeoDesigns.Family.Ink => Ui.Playfair, _ => nFont
        });
        Background = Palette.Brush(palette.OwnBackground ? palette.Card : SeeThrough(nSurface));
        BorderBrush = Palette.Brush(Fam switch
        {
            NeoDesigns.Family.Cyber or NeoDesigns.Family.Tron => BlackDraw.A(NA, .45), NeoDesigns.Family.Neon or NeoDesigns.Family.Gold => BlackDraw.A(NA, .3),
            NeoDesigns.Family.Matrix or NeoDesigns.Family.Rain or NeoDesigns.Family.Scope => BlackDraw.A(NA, .18), _ => Palette.Mix(nSurface, Colors.White, .05)
        });
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(Fam switch
        {
            NeoDesigns.Family.Cyber or NeoDesigns.Family.Tron or NeoDesigns.Family.Gold or NeoDesigns.Family.Ink => 4, NeoDesigns.Family.Rain => 2,
            NeoDesigns.Family.Neumorph => 26, NeoDesigns.Family.Phone => 30, NeoDesigns.Family.Matrix => 8, _ => 16
        });
        Padding = new Thickness(Fam switch { NeoDesigns.Family.Cyber => 8, NeoDesigns.Family.Neumorph or NeoDesigns.Family.Gold or NeoDesigns.Family.Ink => 14, _ => 10 });
        content.Spacing = Fam switch { NeoDesigns.Family.Neumorph => 16, NeoDesigns.Family.Gold or NeoDesigns.Family.Ink => 14, _ => 10 };

        nBigFont = nValueFont; nScale = 1;
        if (gal != null) GalleryLook();
        if (Studio) StudioLook();

        var top = new HashSet<string>();
        if (PanelPath) CyberParts(top); else NeumorphTop(top);

        // Everything not shown yet: the rest of every part in the design's own panels.
        if (!PanelPath) NeumorphDetails(top);
        NeoMemory(top);
        NeoDrives(top);
        NeoNetwork(top);
        NeoFans(top);
        NeoRails(top);
        var extras = referenceRows.Where(r => !referenceUsed.Contains(r.Key) && !top.Contains(r.Key)).ToArray();
        if (extras.Length > 0) content.Children.Add(NPanel(T("MISC_SENSORS", "More"), NGrid(extras.Select(r => NKV(r)), 2)));
        foreach (var r in referenceRows) referenceUsed.Add(r.Key);
    }

    /// <summary>A panel title: the Cyberpunk code name, or the plain name (in capitals where the design writes so).</summary>
    private string T(string cyber, string plain) => Cyber ? cyber : Fam == NeoDesigns.Family.Mission ? MissionName(cyber) : Upper ? L.T(plain).ToUpperInvariant() : L.T(plain);

    // ------------------------------------------------------------------ panel designs: processor and graphics
    private void CyberParts(HashSet<string> top)
    {
        var cpu = Rs("CPU");
        var gpus = Rs("GPU").GroupBy(r => r.Device).ToList();
        if (neo.Layout == NeoDesigns.Arrangement.Split && cpu.Length > 0 && gpus.Count > 0)
        {
            var first = gpus[0].ToArray();
            var left = CyberColumn("CPU", "cpu", cpu, top, NModelLine(WidgetKind.Cpu, 0, snapshot.Cpu?.Name), 0);
            var right = CyberColumn(GpuTitle(first[0].Device, 0, true), GpuArt(first[0].Device), first, top,
                NModelLine(WidgetKind.Gpu, 0, snapshot.Gpus.FirstOrDefault(g => g.Id == first[0].Device)?.Name), 1);
            var split = Ui.Columns("*,*", left, right); split.ColumnSpacing = 14;
            var body = Stack(10, split);
            if (CyberCores() is { } cores) body.Children.Add(cores);
            content.Children.Add(NPanel(null, body));
            for (int i = 1; i < gpus.Count; i++) CyberSection(gpus[i].ToArray(), i, top);
            return;
        }
        if (cpu.Length > 0)
        {
            var body = CyberHero("cpu", cpu, top, 0);
            if (CyberCores() is { } cores) body.Children.Add(cores);
            content.Children.Add(NPanel(T("CPU_CORE", "Processor"), body, PartModel(WidgetKind.Cpu, 0, snapshot.Cpu?.Name ?? ""), 0));
        }
        for (int i = 0; i < gpus.Count; i++) CyberSection(gpus[i].ToArray(), i, top);
    }

    private void CyberSection(AioReading[] rows, int index, HashSet<string> top)
    {
        var info = snapshot.Gpus.FirstOrDefault(g => g.Id == rows[0].Device);
        content.Children.Add(NPanel(GpuTitle(rows[0].Device, index, false), CyberHero(GpuArt(rows[0].Device), rows, top, 1), PartModel(WidgetKind.Gpu, index, info?.Name ?? ""), 1));
    }

    private string GpuTitle(string? device, int index, bool shortForm)
    {
        var info = snapshot.Gpus.FirstOrDefault(g => g.Id == device);
        if (info?.Integrated == true) return Cyber ? (shortForm ? "iGPU" : "IGPU_UNIT") : Soft ? "Integrated graphics" : shortForm ? "iGPU" : L.T("Integrated graphics");
        if (Cyber) return shortForm ? (index == 0 ? "GPU" : "GPU " + (index + 1)) : index == 0 ? "GPU_UNIT" : "GPU_UNIT_" + (index + 1);
        if (Fam == NeoDesigns.Family.Mission && !shortForm) return "GUIDANCE · GPU" + (index == 0 ? "" : " " + (index + 1));
        return index == 0 ? "GPU" : "GPU " + (index + 1);
    }
    private string GpuArt(string? device) => "gpu";

    /// <summary>Picture (optional) and the big temperature left, the other values right, the history below.</summary>
    private StackPanel CyberHero(string art, AioReading[] rows, HashSet<string> top, int colour)
    {
        var main = rows.FirstOrDefault(r => r.Key.EndsWith(".temp")) ?? rows.FirstOrDefault(r => r.Key.EndsWith(".load")) ?? rows[0];
        top.Add(main.Key);
        if (GalleryLayout(GalleryDesigns.Layout.Table) || Fam == NeoDesigns.Family.Mission)
        {
            // Table: every value a row, the temperature first.
            var table = Stack(8, Stack(3, Ordered(rows.Where(r => r != main)).Prepend(main).Select(r => NKV(r)).ToArray()));
            if (NHistory(main) is { } trace) table.Children.Add(trace);
            if (main.Group == "CPU") AddCoreTemps(table);
            return table;
        }
        var left = Stack(2);
        if (neo.Parts) left.Children.Add(NPart(art, art == "gpu" ? 128 : 104));
        left.Children.Add(NBig(main, BigSize(46), true, colour));
        var list = Stack(3, Ordered(rows.Where(r => r != main)).Select(r => NKV(r)).ToArray());
        // Big numbers: the values keep their width and the number shrinks into what is left, so a wide reading ("34 %" without a temperature) never pushes the values out.
        var row = Ui.Columns(GalleryLayout(GalleryDesigns.Layout.BigNumbers) || GalleryLayout(GalleryDesigns.Layout.Segments) || Fam is NeoDesigns.Family.Swiss or NeoDesigns.Family.Vfd or NeoDesigns.Family.Chrome or NeoDesigns.Family.Tilted ? "*,Auto" : "Auto,*", left, new Border { VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(14, 0, 0, 4), Child = list });
        var body = Stack(8, row);
        if (NHistory(main) is { } plot) body.Children.Add(plot);
        if (main.Group == "CPU") AddCoreTemps(body);
        return body;
    }
    private double BigSize(double cyber) => gal?.Arrangement == GalleryDesigns.Layout.BigNumbers || Fam is NeoDesigns.Family.Swiss or NeoDesigns.Family.Chrome ? cyber + 30
        : gal?.Arrangement == GalleryDesigns.Layout.Segments || Fam == NeoDesigns.Family.Vfd ? cyber - 4 : Fam switch
    {
        NeoDesigns.Family.Stealth => cyber + 8, NeoDesigns.Family.Rain or NeoDesigns.Family.Scope => cyber - 6, _ => cyber
    };

    /// <summary>Split layout: one column of a part (tag, picture, big number, values, history).</summary>
    private Control CyberColumn(string tag, string art, AioReading[] rows, HashSet<string> top, Control? model, int colour)
    {
        var main = rows.FirstOrDefault(r => r.Key.EndsWith(".temp")) ?? rows.FirstOrDefault(r => r.Key.EndsWith(".load")) ?? rows[0];
        top.Add(main.Key);
        Control head;
        if (Cyber)
        {
            var label = NText(tag, 10.5, Colors.Black, true); label.LetterSpacing = 1.5;
            head = new Border { Background = Palette.Brush(NA), Padding = new Thickness(6, 1), HorizontalAlignment = HorizontalAlignment.Left, Child = label };
        }
        else head = NHeading(tag, NAcc(colour));
        var column = Stack(4, head);
        if (model != null) column.Children.Add(model);
        if (neo.Parts) column.Children.Add(NPart(art, art == "gpu" ? 140 : 118));
        column.Children.Add(NBig(main, BigSize(52), true, colour));
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
        var caption = NText(Cyber ? "THREAD_MATRIX" : Upper ? L.T("Cores").ToUpperInvariant() : L.T("Cores"), 10.5, Cyber || Glowing ? NA : nLabel, true); TextFonts.Mark(caption, TextRole.Title);
        return Stack(6, caption, coreBars);
    }

    private void AddCoreTemps(StackPanel body)
    {
        var temps = Rs("Cores");
        if (temps.Length == 0) return;
        body.Children.Add(NGrid(temps.Select(r => NKV(r, r.Label.Replace("Core", "C"))), 4));
    }

    // ------------------------------------------------------------------ ring designs: rings, tiles or rows on top
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
        if (GalleryTop(items, top) || StudioTop(items, top)) return;

        if (neo.Layout == NeoDesigns.Arrangement.Bars)
        {
            foreach (var (r, caption, art, colour) in items) content.Children.Add(NBarRow(r, caption, art, NAcc(colour)));
        }
        else if (Fam == NeoDesigns.Family.Thermal)
        {
            var tiles = items.Select((i, n) => NHeat(i.R, i.Caption, n < 2 ? 112 : 84)).ToArray();
            content.Children.Add(new Border { Tag = "aio-section", Child = NGrid(tiles.Take(2), 2, 10) });
            if (tiles.Length > 2) content.Children.Add(new Border { Tag = "aio-section", Child = NGrid(tiles.Skip(2), 3, 8) });
        }
        else if (neo.Layout == NeoDesigns.Arrangement.Hero)
        {
            var (r, caption, _, colour) = items[0];
            var hero = NDial(r, caption, NAcc(colour), 220);
            hero.HorizontalAlignment = HorizontalAlignment.Center;
            content.Children.Add(Decorated(new Border { Tag = "aio-section", Child = hero }, true));
            var rest = items.Skip(1).Select(i => (Control)NDial(i.R, i.Caption, NAcc(i.Colour), 96)).ToArray();
            if (rest.Length > 0) content.Children.Add(new Border { Tag = "aio-section", Child = NGrid(rest, Math.Min(3, rest.Length), 12) });
        }
        else
        {
            var dials = items.Select(i => (Control)NDial(i.R, i.Caption, NAcc(i.Colour), 138)).ToArray();
            content.Children.Add(Decorated(new Border { Tag = "aio-section", Child = NGrid(dials, 2, 14) }, false));
        }
        if (CyberCores() is { } cores) content.Children.Add(Shell(cores, new Thickness(14)));
    }

    /// <summary>The section of rings on top with its design's backdrop: the Tron floor, the seal of the ink painting.</summary>
    private Control Decorated(Border section, bool hero)
    {
        if (Fam is not (NeoDesigns.Family.Tron or NeoDesigns.Family.Ink) || Fam == NeoDesigns.Family.Ink && !hero) return section;
        section.Tag = null;
        if (Fam == NeoDesigns.Family.Tron)
            return new Grid { Tag = "aio-section", Children = { new ArtTronFloor(NA) { Margin = new Thickness(-4, 0) }, section } };
        return new Grid { Tag = "aio-section", Children = { section, new ArtStamp(NAcc(3)) { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top } } };
    }

    /// <summary>A ring: coloured arc with the value and its caption in the middle, dressed by the design.</summary>
    private Control NDial(AioReading r, string caption, Color colour, double size)
    {
        // Font sizes follow the design size, not the graphic size: the size settings scale them on their own.
        double design = size;
        size *= settings.ActiveWidget?.Graphics ?? 1;
        var ring = new AioRing(colour, nTrack, false, Soft || Fam == NeoDesigns.Family.Gold) { Width = size, Height = size };
        rings.Add((r.Key, ring));
        double big = design > 150 ? 40 : design > 110 ? 28 : 20;
        // One text with its unit in a box of fixed width, centred: a value that gains or loses a digit moves nothing.
        var number = NV(r, big); number.TextAlignment = TextAlignment.Center; number.HorizontalAlignment = HorizontalAlignment.Stretch; number.TextWrapping = TextWrapping.NoWrap;
        if (r.Key == "aio.system.ram") { formats[number] = row => Format.Percent(row.Number); number.Text = formats[number](r); }
        else if (r.Key.EndsWith(".temp") || r.Key.StartsWith("aio.system.disk.")) { formats[number] = row => Format.Temperature(row.Number, settings.Fahrenheit) + " " + Format.TemperatureUnit(settings.Fahrenheit); number.Text = formats[number](r); }
        if (Glowing) number.Effect = Glow(colour, 10);
        // A larger main value size shrinks back into the ring instead of being cut off at its edge.
        var value = new Border { Width = big * 4, HorizontalAlignment = HorizontalAlignment.Center,
            Child = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Child = number } };
        var label = NText(Upper ? caption.ToUpperInvariant() : caption, design > 150 ? 12 : 10, Fam is NeoDesigns.Family.Tron or NeoDesigns.Family.Gold or NeoDesigns.Family.Chalk ? colour : nLabel, true);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        var face = Stack(0, value, label); face.HorizontalAlignment = HorizontalAlignment.Center; face.VerticalAlignment = VerticalAlignment.Center;
        var dial = new Grid { Width = size, Height = size };
        switch (Fam)
        {
            case NeoDesigns.Family.Neumorph:
            {
                double inner = size * .66;
                dial.Children.Add(new Border { Width = size, Height = size, CornerRadius = new CornerRadius(size / 2), Background = Palette.Brush(SeeThrough(nSurface)), BoxShadow = Raised(7) });
                dial.Children.Add(ring);
                dial.Children.Add(new Border
                {
                    Width = inner, Height = inner, CornerRadius = new CornerRadius(inner / 2), Background = Palette.Brush(SeeThrough(nSurface)),
                    BoxShadow = Inset(4), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
                });
                break;
            }
            case NeoDesigns.Family.Tron:
                dial.Children.Add(new Border
                {
                    Width = size * .9, Height = size * .9, CornerRadius = new CornerRadius(size), BorderThickness = new Thickness(2), BorderBrush = Palette.Brush(colour),
                    Background = Palette.Brush(SeeThrough(Palette.Mix(nSurface, colour, .06))), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                    BoxShadow = new BoxShadows(new BoxShadow { Blur = 14, Color = BlackDraw.A(colour, .55) }, new[] { new BoxShadow { Blur = 12, Color = BlackDraw.A(colour, .3), IsInset = true } })
                });
                dial.Children.Add(ring);
                break;
            case NeoDesigns.Family.Phone:
                dial.Children.Add(new Border { Width = size, Height = size, CornerRadius = new CornerRadius(size * .24), Background = Palette.Brush(SeeThrough(nPanel)) });
                ring.Width = ring.Height = size * .86; dial.Children.Add(ring);
                break;
            case NeoDesigns.Family.Gold:
                dial.Children.Add(new Ellipse { Width = size, Height = size, Fill = Palette.Brush(SeeThrough(Palette.Mix(nSurface, colour, .04))), Stroke = Palette.Brush(BlackDraw.A(colour, .75)), StrokeThickness = 1.2 });
                dial.Children.Add(new ArtTicks(colour) { Width = size * .92, Height = size * .92, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
                ring.Width = ring.Height = size * .74; dial.Children.Add(ring);
                break;
            case NeoDesigns.Family.Chalk:
                dial.Children.Add(new Ellipse { Width = size * .94, Height = size * .94, Stroke = Palette.Brush(BlackDraw.A(nInk, .35)), StrokeThickness = 1.5,
                    StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 2, 3 }, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
                ring.Width = ring.Height = size * .86; dial.Children.Add(ring);
                break;
            default:
                dial.Children.Add(ring);
                break;
        }
        dial.Children.Add(Squeeze(face, size * .2));
        // A narrow sidebar or a large graphic size: the whole ring shrinks to its column instead of leaving the card.
        // Not clipped: the soft shadow of the raised disc reaches past the ring and would otherwise end in a square.
        return new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, MaxWidth = size, MaxHeight = size, ClipToBounds = false,
            HorizontalAlignment = HorizontalAlignment.Center, Child = dial };
    }

    /// <summary>Thermal camera: a tile glowing in the colour of its reading (iron palette), with a spot-meter crosshair.</summary>
    private Control NHeat(AioReading r, string caption, double height)
    {
        // The font size follows the design size, not the graphic size: the size settings scale it on their own.
        double size = height > 100 ? 30 : 20;
        height *= settings.ActiveWidget?.Graphics ?? 1;
        var number = NV(r, size); number.TextWrapping = TextWrapping.NoWrap; number.HorizontalAlignment = HorizontalAlignment.Center;
        if (r.Key == "aio.system.ram") { formats[number] = row => Format.Percent(row.Number); number.Text = formats[number](r); }
        else if (r.Key.EndsWith(".temp") || r.Key.StartsWith("aio.system.disk.")) { formats[number] = row => Format.Temperature(row.Number, settings.Fahrenheit) + "°"; number.Text = formats[number](r); }
        number.Effect = new DropShadowEffect { BlurRadius = 6, OffsetX = 0, OffsetY = 0, Color = Color.FromArgb(200, 0, 0, 0) };
        var label = NText(caption.ToUpperInvariant(), 10, nInk, true); label.Margin = new Thickness(10, 8, 10, 0); label.TextTrimming = TextTrimming.CharacterEllipsis;
        label.Effect = new DropShadowEffect { BlurRadius = 4, OffsetX = 0, OffsetY = 0, Color = Color.FromArgb(200, 0, 0, 0) };
        var value = new Border { Margin = new Thickness(6, 0, 6, 8), VerticalAlignment = VerticalAlignment.Bottom, Child = Fit(number) };
        // Caption on top, value at the bottom, each in its own row: they never meet, however small the tile.
        var cross = new ArtCrosshair(Colors.White) { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var face = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), Children = { cross, label, value } };
        Grid.SetRowSpan(cross, 2); Grid.SetRow(value, 1);
        var tile = new Border { Height = height, CornerRadius = new CornerRadius(14), ClipToBounds = true, Child = face };
        double scale = r.Key.EndsWith(".temp") || r.Key.StartsWith("aio.system.disk.") ? 1 : 0;
        void Paint(AioReading? row) => tile.Background = HeatBrush(row?.Number is double n ? (scale == 1 ? (n - 20) / 65 : n / 100) : null);
        Paint(r); watchers.Add((r.Key, Paint));
        return tile;
    }

    /// <summary>Thermal camera: a fan cell glowing with its speed.</summary>
    private Control HeatCell(AioReading fan, Control cell)
    {
        var border = new Border { CornerRadius = new CornerRadius(10), Padding = new Thickness(4, 6), Child = cell };
        void Paint(AioReading? row) => border.Background = HeatBrush(row?.Number is double n ? .15 + n / 3200 : null);
        Paint(fan); watchers.Add((fan.Key, Paint));
        return border;
    }

    /// <summary>The iron palette of a thermal camera (black, purple, red, orange, yellow, white), or the chosen accent.</summary>
    private IBrush HeatBrush(double? level)
    {
        double t = Math.Clamp(level ?? 0, 0, 1);
        Color Iron(double x)
        {
            x = Math.Clamp(x, 0, 1);
            if (nAcc.Length == 1 || Color.TryParse(slot?.Accent, out _) || skin != null) return Palette.Mix(nPanel, NA, .15 + .8 * x);
            var stops = new[] { (0.0, "#08061A"), (.25, "#500A78"), (.5, "#D22828"), (.7, "#FA8214"), (.88, "#FFDC3C"), (1.0, "#FFFFE6") };
            for (int i = 1; i < stops.Length; i++)
                if (x <= stops[i].Item1)
                    return Palette.Mix(Color.Parse(stops[i - 1].Item2), Color.Parse(stops[i].Item2), (x - stops[i - 1].Item1) / (stops[i].Item1 - stops[i - 1].Item1));
            return Color.Parse("#FFFFE6");
        }
        if (level == null) return Palette.Brush(SeeThrough(nPanel));
        return new RadialGradientBrush
        {
            Center = new RelativePoint(.5, .55, RelativeUnit.Relative), GradientOrigin = new RelativePoint(.5, .55, RelativeUnit.Relative),
            RadiusX = new RelativeScalar(.7, RelativeUnit.Relative), RadiusY = new RelativeScalar(.7, RelativeUnit.Relative),
            GradientStops = { new(SeeThrough(Iron(t)), 0), new(SeeThrough(Iron(t - .22)), .55), new(SeeThrough(Iron(.1)), 1) }
        };
    }

    /// <summary>A row: picture in a well (Parts designs), caption and value, a bar in a groove (Neumorph) or on an LED board.</summary>
    private Control NBarRow(AioReading r, string caption, string art, Color colour)
    {
        var value = r.Key == "aio.system.ram" ? NPercent(r, 24) : NBig(r, Fam == NeoDesigns.Family.Matrix ? 30 : 24, false);
        if (Fam == NeoDesigns.Family.Matrix) value.HorizontalAlignment = HorizontalAlignment.Right;
        var head = Ui.Columns("*,Auto", FitLeft(NText(Upper ? caption.ToUpperInvariant() : caption, 11.5, Fam == NeoDesigns.Family.Matrix ? colour : nLabel, true)), value);
        var meter = new AioMeter(colour, Color.FromArgb(0, 0, 0, 0), 0) { Height = Fam == NeoDesigns.Family.Matrix ? 5 : 8, Margin = new Thickness(3) };
        meters.Add((r.Key, meter));
        Control bar = Soft
            ? new Border { CornerRadius = new CornerRadius(7), Background = Palette.Brush(SeeThrough(nSurface)), BoxShadow = Inset(3), Child = meter }
            : new Border { CornerRadius = new CornerRadius(3), Background = Palette.Brush(SeeThrough(nTrack)), Child = meter };
        var info = Stack(8, head, bar);
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
        return Shell(body, new Thickness(14, 12));
    }

    /// <summary>Ring designs: the remaining processor and graphics values in one panel, two columns.</summary>
    private void NeumorphDetails(HashSet<string> top)
    {
        var parts = new List<Control>();
        var cpu = Ordered(Rs("CPU").Where(r => !top.Contains(r.Key)));
        // The history chart of the processor keeps its column when every other value is shown large (Orbit puts temperature and load on top).
        if (cpu.Count > 0 || Rs("Cores").Length > 0 || R("temp") is { } shownTemp && AioReadings.Graphs(settings, shownTemp.Key))
        {
            var column = Stack(3, NHeading("CPU", NAcc(0)));
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
            var column = Stack(3, NHeading(GpuTitle(gpu.Key, index, false), NAcc(1)));
            if (NModelLine(WidgetKind.Gpu, index, info?.Name) is { } gpuModel) column.Children.Add(gpuModel);
            foreach (var r in rows) column.Children.Add(NKV(r));
            var main = gpu.FirstOrDefault(r => r.Key.EndsWith(".temp"));
            if (main != null && NHistory(main) is { } plot) column.Children.Add(plot);
            parts.Add(column); index++;
        }
        if (parts.Count == 0) return;
        // The 8-bit face is wide: one part per panel.
        int per = Fam == NeoDesigns.Family.Pixel ? 1 : 2;
        for (int i = 0; i < parts.Count; i += per)
            content.Children.Add(Shell(NGrid(parts.Skip(i).Take(per), per, 16)));
    }

    // ------------------------------------------------------------------ the other parts (all families)
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
            var meter = new AioMeter(Cyber || Soft ? NA : NAcc(2), nTrack, 0) { Height = 6 }; meters.Add((ram.Key, meter)); body.Children.Add(meter);
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
        if (body.Children.Count > 0) content.Children.Add(NPanel(T("MEM_ALLOC", "Memory"), body, null, 2));
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
        content.Children.Add(NPanel(T("STORAGE_ARRAY", "Storage"), Stack(12, blocks.ToArray()), null, 3));
    }

    private void NeoNetwork(HashSet<string> top)
    {
        var net = Rs("Network").Where(r => !top.Contains(r.Key)).ToArray();
        if (net.Length == 0) return;
        var down = net.FirstOrDefault(r => r.Key.EndsWith(".down")); var up = net.FirstOrDefault(r => r.Key.EndsWith(".up"));
        var link = net.FirstOrDefault(r => r.Key.EndsWith(".link"));
        var body = Stack(5);
        foreach (var r in net.Where(r => r != link)) body.Children.Add(NKV(r, r == down ? (Upper ? "RX ↓" : L.T("Download")) : r == up ? (Upper ? "TX ↑" : L.T("Upload")) : null));
        if (link != null) body.Children.Add(NKV(link, Upper ? "LINK" : L.T("Link")));
        var first = down ?? up;
        if (first != null && AioReadings.Graphs(settings, first.Key))
            body.Children.Add(HistoryPlot(first, Fam == NeoDesigns.Family.Scope ? 46 : 36, "area", Fam == NeoDesigns.Family.Scope ? NAcc(2) : NB, first == down ? up : null));
        content.Children.Add(NPanel(T("NET_UPLINK", "Network"), body, null, 2));
    }

    private void NeoFans(HashSet<string> top)
    {
        var fans = Rs("Cooling").Where(r => !top.Contains(r.Key)).ToArray();
        if (fans.Length == 0) return;
        bool wide = Fam is NeoDesigns.Family.Neumorph or NeoDesigns.Family.Phone or NeoDesigns.Family.Gold or NeoDesigns.Family.Ink;
        var cells = fans.Select((f, i) =>
        {
            var value = NV(f, Cyber ? 20 : Fam == NeoDesigns.Family.Matrix ? 19 : 17); formats[value] = row => row.Number.HasValue ? Format.Number(row.Number) : "—"; value.Text = formats[value](f);
            value.HorizontalAlignment = HorizontalAlignment.Center;
            var name = NText(Upper ? Naming.Short(Translate(L.T(f.Label))).ToUpperInvariant() : Naming.Short(Translate(L.T(f.Label))), 9.5, nLabel, true); name.HorizontalAlignment = HorizontalAlignment.Center;
            name.TextTrimming = TextTrimming.CharacterEllipsis; name.TextWrapping = TextWrapping.NoWrap;
            var cell = Stack(2);
            if (neo.Parts && Cyber) cell.Children.Add(NPart(FanArt[i % FanArt.Length], 36));
            cell.Children.Add(Fit(value)); cell.Children.Add(name);
            return Fam switch
            {
                NeoDesigns.Family.Neumorph => new Border { CornerRadius = new CornerRadius(16), Background = Palette.Brush(SeeThrough(nSurface)), BoxShadow = Raised(4), Padding = new Thickness(6, 10), Margin = new Thickness(3), Child = cell },
                NeoDesigns.Family.Phone => new Border { CornerRadius = new CornerRadius(16), Background = Palette.Brush(SeeThrough(Palette.Mix(nPanel, Colors.White, .06))), Padding = new Thickness(6, 8), Child = cell },
                NeoDesigns.Family.Tron or NeoDesigns.Family.Neon => new Border { BorderThickness = new Thickness(1), BorderBrush = Palette.Brush(BlackDraw.A(NAcc(i), .5)), CornerRadius = new CornerRadius(Fam == NeoDesigns.Family.Neon ? 8 : 0), Padding = new Thickness(4, 6), Child = cell },
                NeoDesigns.Family.Thermal => HeatCell(f, cell),
                _ => (Control)cell
            };
        }).ToArray();
        var grid = NGrid(cells, wide ? 3 : 4, wide ? 6 : 8);
        if (AioReadings.Graphs(settings, fans[0].Key) && fans.FirstOrDefault(f => f.Number > 0) is { } spinning)
            content.Children.Add(NPanel(T("COOLING_ARRAY", "Fans"), Stack(8, grid, HistoryPlot(spinning, 28, "area", NA)), null, 0));
        else content.Children.Add(NPanel(T("COOLING_ARRAY", "Fans"), grid, null, 0));
    }

    private void NeoRails(HashSet<string> top)
    {
        var rails = Rs("Rails").Where(r => !top.Contains(r.Key)).ToArray();
        if (rails.Length == 0) return;
        content.Children.Add(NPanel(T("POWER_RAILS", "Power supply"), NGrid(rails.Select(r => NKV(r, r.Label)), 3, 10), null, 1));
    }

    // ------------------------------------------------------------------ pieces
    private TextBlock NText(string text, double size, Color color, bool bold = false, bool number = false)
    {
        var t = Text(text, size * nScale, color, number); TextFonts.Mark(t, TextRole.Label); t.FontFamily = nFont;
        // The 8-bit face fills its whole em: a little more line height keeps two lines apart.
        if (Fam == NeoDesigns.Family.Pixel) t.LineHeight = size * nScale * 1.5;
        t.FontWeight = bold ? FontWeight.Bold : FontWeight.SemiBold; t.TextWrapping = TextWrapping.NoWrap; return t;
    }
    private TextBlock NV(AioReading r, double size, Color? color = null)
    {
        var t = V(r, size * nScale, color ?? nInk); t.FontFamily = nValueFont;
        t.FontWeight = Fam is NeoDesigns.Family.Stealth or NeoDesigns.Family.Gold or NeoDesigns.Family.Ink ? FontWeight.Normal : FontWeight.Bold; return t;
    }
    private void AddField(string key, TextBlock t) { TextFonts.Mark(t, TextRole.Value); if (t is AioValueText v) v.Hint = "888.8 / 888.8 GB"; if (!fields.TryGetValue(key, out var list)) fields[key] = list = new(); list.Add(t); }
    private static DropShadowEffect Glow(Color c, double blur) => new() { OffsetX = 0, OffsetY = 0, BlurRadius = blur, Color = BlackDraw.A(c, .85) };

    /// <summary>The big number of a part: temperature with its unit small after it; Cyberpunk adds a coloured offset glow, the
    /// light designs (LED, Tron, code rain, neon, oscilloscope) a soft glow in the part's colour.</summary>
    private Control NBig(AioReading r, double size, bool left = true, int colour = 0)
    {
        var t = NV(r, size, Fam == NeoDesigns.Family.Stealth ? Color.Parse("#A2A2AA") : null); t.FontFamily = nBigFont;
        bool temperature = r.Key.EndsWith(".temp") || r.Key.StartsWith("aio.system.disk.");
        Control face = t;
        if (temperature)
        {
            formats[t] = row => Format.Temperature(row.Number, settings.Fahrenheit); t.Text = formats[t](r);
            var unit = NText(Format.TemperatureUnit(settings.Fahrenheit), size * .36, nLabel); unit.VerticalAlignment = VerticalAlignment.Bottom; unit.Margin = new Thickness(2, 0, 0, size * .14);
            face = new StackPanel { Orientation = Orientation.Horizontal, Children = { t, unit } };
        }
        if (Cyber) t.Effect = new DropShadowEffect { OffsetX = 2.5, OffsetY = 0, BlurRadius = 0, Color = BlackDraw.A(NB, .5) };
        else if (Fam == NeoDesigns.Family.Chrome) t.Effect = Extrusion(colour);
        else if (Glowing) t.Effect = Glow(NAcc(colour), 12);
        else if (Fam == NeoDesigns.Family.Stealth) t.Effect = new DropShadowEffect { OffsetX = 1, OffsetY = 1, BlurRadius = 0, Color = Color.Parse("#3A3A40") };
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
        var row = Ui.Columns("Auto,*", NText(Upper ? caption.ToUpperInvariant() : caption, 11.5, nLabel), FitRight(NV(r, 13.5))); row.ColumnSpacing = 8;
        return row;
    }
    private Control NGrid(IEnumerable<Control> items, int columns, double gap = 10)
    {
        var array = items.ToArray();
        var grid = FixedCols(Math.Min(columns, Math.Max(1, array.Length)), array);
        grid.ColumnSpacing = gap; grid.RowSpacing = Math.Min(gap, 8); return grid;
    }
    private Control? NHistory(AioReading r) => AioReadings.Graphs(settings, r.Key)
        ? HistoryPlot(r, Fam == NeoDesigns.Family.Scope ? 44 : Cyber ? 32 : 30, "area", Cyber ? NA : NAcc(r.Group == "GPU" ? 1 : r.Group == "System" ? 2 : 0)) : null;
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
        var t = NText(name, 10, nLabel); t.TextTrimming = TextTrimming.CharacterEllipsis; t.Margin = new Thickness(0, Fam == NeoDesigns.Family.Pixel ? 2 : -2, 0, 4); return t;
    }
    /// <summary>The colour of a heading: the accent of its part where the design colours its headings, else the caption grey.</summary>
    private Color HeadingColour(Color part) => Fam switch
    {
        NeoDesigns.Family.Cyber => NA,
        NeoDesigns.Family.Matrix or NeoDesigns.Family.Tron or NeoDesigns.Family.Neon or NeoDesigns.Family.Rain or NeoDesigns.Family.Scope or NeoDesigns.Family.Gold or NeoDesigns.Family.Gallery => part,
        NeoDesigns.Family.Swiss or NeoDesigns.Family.Tubes => nLabel,
        _ when Studio => part,
        _ => nLabel
    };
    private TextBlock NHeading(string title, Color? part = null)
    {
        var t = NText(Upper ? title.ToUpperInvariant() : title, Cyber ? 11.5 : 11, HeadingColour(part ?? NA), true); TextFonts.Mark(t, TextRole.Title);
        t.LetterSpacing = Upper || Fam == NeoDesigns.Family.Gold ? 1.6 : .8; t.TextTrimming = TextTrimming.CharacterEllipsis;
        if (Fam == NeoDesigns.Family.Neon) t.Effect = Glow(part ?? NA, 8);
        return t;
    }

    /// <summary>A body in the design's plain panel (no title): Neumorph raised, otherwise the design's panel.</summary>
    private Control Shell(Control body, Thickness? soft = null) => Soft ? NRaised(body, soft ?? new Thickness(16, 14)) : NPanel(null, body);

    /// <summary>A panel of the design with an optional heading (and the part's model after it).</summary>
    private Control NPanel(string? title, Control body, string? model = null, int colour = 0)
    {
        var part = NAcc(colour);
        if (Studio) return StudioPanel(title, body, model, part);
        Control? heading = title == null ? null : NHeading(title, part);
        if (heading != null && !string.IsNullOrEmpty(model))
        {
            // The part's name as the user or the hardware gives it (not upper-cased), after the panel title.
            var name = NText(model, Cyber ? 11.5 : 11, Cyber ? NA : nLabel, true); name.TextTrimming = TextTrimming.CharacterEllipsis;
            ((TextBlock)heading).Text += Cyber ? " //" : " ·";
            var row = Ui.Columns("Auto,*", heading, name); row.ColumnSpacing = 6; heading = row;
        }
        if (heading != null && Fam == NeoDesigns.Family.Stealth)
        {
            var dot = new Ellipse { Width = 6, Height = 6, Fill = Palette.Brush(NA), VerticalAlignment = VerticalAlignment.Center };
            var row = Ui.Columns("Auto,*", dot, heading); row.ColumnSpacing = 8; heading = row;
        }
        if (heading != null && Fam == NeoDesigns.Family.Ink) heading = Stack(4, heading, new ArtBrushStroke(nInk));
        var inner = heading == null ? body : Stack(Cyber ? 8 : 10, heading, body);
        if (gal != null) return GalleryPanel(inner, part);
        switch (Fam)
        {
            case NeoDesigns.Family.Neumorph: return NRaised(inner, new Thickness(16, 14));
            case NeoDesigns.Family.Cyber:
            {
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
            case NeoDesigns.Family.Rain:
            {
                var panel = new Border
                {
                    Padding = new Thickness(12, 10), Child = inner, BorderThickness = new Thickness(1), BorderBrush = Palette.Brush(BlackDraw.A(part, .35)),
                    Background = Palette.Brush(SeeThrough(BlackDraw.A(nSurface, .55)))
                };
                return new Grid { Tag = "aio-section", Children = { new ArtCodeRain(part), panel } };
            }
            case NeoDesigns.Family.Scope:
            {
                var panel = new Border { Padding = new Thickness(12, 10), Child = inner };
                var screen = new Border
                {
                    CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(2), BorderBrush = Palette.Brush(Palette.Mix(nSurface, NA, .2)), ClipToBounds = true,
                    Background = Palette.Brush(SeeThrough(nPanel)), Child = new Grid { Children = { new ArtGraticule(NA), panel } }
                };
                screen.Tag = "aio-section"; return screen;
            }
            default:
            {
                var panel = new Border { Padding = new Thickness(14, 12), Child = inner, Tag = "aio-section" };
                switch (Fam)
                {
                    case NeoDesigns.Family.Matrix:
                        panel.CornerRadius = new CornerRadius(6); panel.BorderThickness = new Thickness(1.5); panel.BorderBrush = Palette.Brush(Palette.Mix(nSurface, NA, .16));
                        panel.Background = Palette.Brush(SeeThrough(nPanel)); break;
                    case NeoDesigns.Family.Tron:
                        panel.BorderThickness = new Thickness(1); panel.BorderBrush = Palette.Brush(BlackDraw.A(part, .6)); panel.Background = Palette.Brush(SeeThrough(Palette.Mix(nSurface, part, .05)));
                        panel.BoxShadow = new BoxShadows(new BoxShadow { Blur = 10, Color = BlackDraw.A(part, .3) }); break;
                    case NeoDesigns.Family.Neon:
                        panel.CornerRadius = new CornerRadius(12); panel.BorderThickness = new Thickness(2); panel.BorderBrush = Palette.Brush(part);
                        panel.Background = Palette.Brush(SeeThrough(Palette.Mix(nSurface, part, .04)));
                        panel.BoxShadow = new BoxShadows(new BoxShadow { Blur = 14, Color = BlackDraw.A(part, .55) }, new[] { new BoxShadow { Blur = 10, Color = BlackDraw.A(part, .25), IsInset = true } }); break;
                    case NeoDesigns.Family.Thermal:
                        panel.CornerRadius = new CornerRadius(14); panel.Background = Palette.Brush(SeeThrough(nPanel)); break;
                    case NeoDesigns.Family.Phone:
                        panel.CornerRadius = new CornerRadius(24); panel.Background = Palette.Brush(SeeThrough(nPanel)); panel.Padding = new Thickness(16, 14); break;
                    case NeoDesigns.Family.Stealth:
                        panel.CornerRadius = new CornerRadius(14); panel.BorderThickness = new Thickness(1); panel.BorderBrush = Palette.Brush(Color.FromArgb(255, 0, 0, 0));
                        panel.Background = new LinearGradientBrush
                        {
                            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                            GradientStops = { new(SeeThrough(Palette.Mix(nPanel, Colors.White, .02)), 0), new(SeeThrough(Palette.Mix(nPanel, Colors.Black, .3)), 1) }
                        };
                        panel.BoxShadow = new BoxShadows(new BoxShadow { OffsetX = 1, OffsetY = 1, Color = Palette.Mix(nPanel, Colors.White, .08), IsInset = true },
                            new[] { new BoxShadow { OffsetY = 6, Blur = 14, Color = Color.FromArgb(160, 0, 0, 0) } }); break;
                    case NeoDesigns.Family.Gold:
                        panel.BorderThickness = new Thickness(0, 1, 0, 0); panel.BorderBrush = Palette.Brush(BlackDraw.A(part, .5)); panel.Padding = new Thickness(4, 12, 4, 4); break;
                    case NeoDesigns.Family.Ink:
                        panel.Padding = new Thickness(4, 4); break;
                }
                return panel;
            }
        }
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
