using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

/// <summary>
/// The Black designs: black cards with drawn hardware (realistic, line art or 3D) and one layout each:
/// Dashboard (car-style gauges on top), Rings (a live ring around each picture), Graph (a curve for every part),
/// Classic, Tiles (big numbers on top) and X-Ray (the whole PC with values on its parts). Every value is bound to its
/// AIO sensor, so the sensor choices, the widget switches and "only what the hardware has" work as in every AIO design;
/// a value shown on top (gauge, tile, x-ray) is not repeated below.
/// </summary>
public sealed partial class AioTile
{
    private readonly List<(string Key, Action<AioReading?> Set)> blackLive = new();
    private readonly List<(string Key, BlackArea Area)> blackPlots = new();
    private BlackCores? blackCores;
    private BlackTheme black = BlackTheme.For(AioDesigns.BlackDefault(WidgetStyle.AioBlackDashboard));
    private Color bInk, bMuted;

    private WidgetStyle BlackLayout => AioDesigns.BlackLayout(style);

    private void BlackDesign()
    {
        drivesPlaced = true; coreBars = null; mainboardCores = null; blackCores = null;
        blackLive.Clear(); blackPlots.Clear();
        var look = AioDesigns.BlackLookOf(settings, designStyle);
        black = BlackTheme.For(look, Color.TryParse(slot?.Accent, out var own) ? own : palette.Chosen);
        bInk = lightSurface ? ink : Colors.White; bMuted = lightSurface ? muted : black.Muted;
        Padding = new Thickness(6); CornerRadius = new CornerRadius(16); content.Spacing = 10;
        if (!palette.OwnBackground)
            Background = look.Cards == "glass"
                ? new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                    GradientStops = { new(SeeThrough(BlackDraw.A(Palette.Mix(Colors.Black, black.A, .38), 1)), 0), new(SeeThrough(Colors.Black), .38),
                        new(SeeThrough(Palette.Mix(Colors.Black, black.B, .26)), .7), new(SeeThrough(Colors.Black), 1) }
                }
                : Palette.Brush(SeeThrough(Colors.Black));
        BorderBrush = Palette.Brush(Color.Parse("#1C1C1F"));

        var top = new HashSet<string>();
        switch (BlackLayout)
        {
            case WidgetStyle.AioBlackDashboard: BlackGauges(top); break;
            case WidgetStyle.AioBlackXRay: BlackXRay(top); break;
            case WidgetStyle.AioBlackTiles: BlackTiles(top); break;
        }

        // Processor
        var cpu = Rs("CPU");
        if (cpu.Length > 0)
        {
            var temp = R("temp"); var load = R("load");
            var values = Ordered(cpu.Where(r => !top.Contains(r.Key)));
            Control? cores = null;
            if (lastCoreLoads.Count >= 2 && AioReadings.Selected(settings, "aio.cpu.coreloads"))
            {
                blackCores = new BlackCores { Theme = black, Loads = lastCoreLoads };
                cores = Stack(6, BCaption("CORE USAGE"), blackCores);
            }
            BlackSection("PROCESSOR", PanelName(WidgetKind.Cpu, 0, snapshot.Cpu?.Name ?? "", ""), "cpu", values, load ?? temp, temp ?? load, false, cores);
        }

        // Graphics: every card that is shown (integrated graphics too)
        int index = 0;
        foreach (var gpu in Rs("GPU").GroupBy(r => r.Device))
        {
            var rows = gpu.ToArray();
            var info = snapshot.Gpus.FirstOrDefault(g => g.Id == gpu.Key);
            var temp = rows.FirstOrDefault(r => r.Key.EndsWith(".temp")); var load = rows.FirstOrDefault(r => r.Key.EndsWith(".load"));
            string title = info?.Integrated == true ? "INTEGRATED GRAPHICS" : index == 0 ? "GRAPHICS" : "GRAPHICS " + (index + 1);
            BlackSection(title, PanelName(WidgetKind.Gpu, index, info?.Name ?? "", ""), info?.Integrated == true ? "igpu" : "gpu",
                Ordered(rows.Where(r => !top.Contains(r.Key))), load ?? temp, temp ?? load, false);
            index++;
        }

        if (R("ram", "System") is { } ram) BlackMemory(ram, top);
        BlackDrives(top);
        BlackFans(top);

        var rails = Rs("Rails").Where(r => !top.Contains(r.Key)).ToArray();
        if (rails.Length > 0)
        {
            var list = Stack(8, rails.Select(r => BKV(r, r.Label.ToUpperInvariant(), 15)).ToArray());
            content.Children.Add(BCard("POWER SUPPLY", null, ArtBeside("psu", 96, list)));
        }

        var net = Rs("Network");
        if (net.Length > 0)
        {
            var down = net.FirstOrDefault(r => r.Key.EndsWith(".down")); var up = net.FirstOrDefault(r => r.Key.EndsWith(".up"));
            var link = net.FirstOrDefault(r => r.Key.EndsWith(".link"));
            var list = Stack(8);
            foreach (var r in net.Where(r => r != link && !top.Contains(r.Key)))
                list.Children.Add(BKV(r, r == down ? "DOWN" : r == up ? "UP" : PanelCaption(r)));
            Control? right = link != null && !top.Contains(link.Key) ? FitRight(Ui.Row(5, BText("LINK", 9, bMuted), BV(link, 11))) : null;
            var body = Stack(8, ArtBeside("net", 104, list));
            if (down != null && BArea(down, black.B, 30) is { } area) body.Children.Add(area);
            content.Children.Add(BCard("NETWORK", right, body));
        }

        var coreTemps = Rs("Cores");
        if (coreTemps.Length > 0)
            content.Children.Add(BCard("CORE TEMP", null, BGrid(coreTemps.Select(r => BKV(r, r.Label.Replace("Core", "C").ToUpperInvariant(), 13)), 4)));

        // Anything else the user ticked keeps its value in a card of its own.
        var extras = referenceRows.Where(r => !referenceUsed.Contains(r.Key) && !top.Contains(r.Key)).ToArray();
        if (extras.Length > 0) content.Children.Add(BCard("MORE", null, BGrid(extras.Select(r => BKV(r)), 2)));
        foreach (var r in referenceRows) referenceUsed.Add(r.Key);
    }

    /// <summary>Temperature, load, clock and power first; then the rest in the reading order.</summary>
    private static List<AioReading> Ordered(IEnumerable<AioReading> rows) => rows.OrderBy(r =>
        r.Key.EndsWith(".temp") ? 0 : r.Key.EndsWith(".load") ? 1 : r.Key.EndsWith(".clock") ? 2 : r.Key.EndsWith(".power") ? 3 : 4).ToList();

    // ---------------------------------------------------------------- the parts
    /// <summary>
    /// A processor or graphics section in the chosen layout. <paramref name="ring"/> fills the ring (Rings);
    /// <paramref name="main"/> is the big value with its curve (Graph).
    /// </summary>
    private void BlackSection(string title, string model, string art, List<AioReading> values, AioReading? ring, AioReading? main, bool wideArt, Control? after = null)
    {
        var body = Stack(10);
        if (BlackLayout == WidgetStyle.AioBlackGraph && main != null && values.Contains(main))
        {
            values.Remove(main);
            var big = BV(main, 26); big.HorizontalAlignment = HorizontalAlignment.Right;
            var row = Ui.Columns("Auto,*", BArt(art, 64), FitRight(big)); row.ColumnSpacing = 10;
            body.Children.Add(row);
            if (BArea(main, black.A, 40) is { } curve) body.Children.Add(curve);
            if (values.Count > 0) body.Children.Add(BGrid(values.Select(r => BKV(r, null, 15)), 3));
        }
        else if (BlackLayout == WidgetStyle.AioBlackRings)
        {
            var first = values.Take(5).ToList();
            body.Children.Add(ArtBeside(art, 112, BGrid(first.Select(r => BKV(r)), 2), ring));
            if (values.Count > first.Count) body.Children.Add(BGrid(values.Skip(first.Count).Select(r => BKV(r)), 2));
        }
        else if (wideArt)
        {
            body.Children.Add(new Border { HorizontalAlignment = HorizontalAlignment.Center, Child = BArt(art, 250) });
            var load = values.FirstOrDefault(r => r.Key.EndsWith(".load")) ?? ring;
            if (load != null && BArea(load, black.B, 32) is { } curve) body.Children.Add(curve);
            if (values.Count > 0) body.Children.Add(BGrid(values.Select(r => BKV(r)), 3));
        }
        else
        {
            var first = values.Take(4).ToList();
            if (first.Count > 0) body.Children.Add(ArtBeside(art, art == "gpu" ? 118 : 88, BGrid(first.Select(r => BKV(r)), 2)));
            else body.Children.Add(new Border { HorizontalAlignment = HorizontalAlignment.Center, Child = BArt(art, 88) });
            if (values.Count > first.Count) body.Children.Add(BGrid(values.Skip(first.Count).Select(r => BKV(r)), 2));
        }
        if (after != null) body.Children.Add(after);
        content.Children.Add(BCard(title, BModel(model), body));
    }

    private void BlackMemory(AioReading ram, HashSet<string> top)
    {
        // The amount ("15.9 / 93.7 GB") follows the reading; the load is the bound value of the card.
        var amount = BText(Amount(ram), 17, bInk, true, true); formats[amount] = Amount;
        if (!fields.TryGetValue(ram.Key, out var list)) fields[ram.Key] = list = new(); list.Add(amount);
        TextBlock? percent = null;
        if (!top.Contains(ram.Key))
        {
            var p = BV(ram, BlackLayout == WidgetStyle.AioBlackRings ? 30 : 12); formats[p] = r => Format.Percent(r.Number); p.Text = Format.Percent(ram.Number);
            percent = p;
        }
        else referenceUsed.Add(ram.Key);
        Control body;
        if (BlackLayout == WidgetStyle.AioBlackRings && percent != null)
            body = ArtBeside("ram", 112, Stack(2, FitLeft(percent), BCaption("USED MEMORY"), FitLeft(amount)), ram);
        else
        {
            var info = Stack(4, FitLeft(amount), BCaption("USED"), BBar(ram, 8));
            body = Stack(8, ArtBeside("ram", 120, info));
            if (BlackLayout == WidgetStyle.AioBlackGraph && BArea(ram, black.A, 34) is { } curve) ((StackPanel)body).Children.Add(curve);
        }
        content.Children.Add(BCard("MEMORY", BlackLayout == WidgetStyle.AioBlackRings ? null : percent, body));
    }

    private void BlackDrives(HashSet<string> top)
    {
        var drives = PartDrives();
        if (drives.Length == 0) return;
        var body = Stack(12, new Border { HorizontalAlignment = HorizontalAlignment.Center, Child = BArt("ssd", 150) });
        if (BlackLayout == WidgetStyle.AioBlackRings)
        {
            var cells = drives.Select(d =>
            {
                var used = d.Rows.FirstOrDefault(r => r.Key.EndsWith(".used"));
                var face = Stack(0);
                if (d.Temp != null && !top.Contains(d.Temp.Key)) { var t = BV(d.Temp, 15); t.HorizontalAlignment = HorizontalAlignment.Center; face.Children.Add(t); }
                if (used != null) { var u = BV(used, 9.5, bMuted); u.HorizontalAlignment = HorizontalAlignment.Center; face.Children.Add(u); }
                face.HorizontalAlignment = HorizontalAlignment.Center; face.VerticalAlignment = VerticalAlignment.Center;
                var ring = new BlackRing { Theme = black, Color = black.B, Thickness = 6, Width = 92, Height = 92, Tag = "graphic" };
                if (used != null) blackLive.Add((used.Key, r => ring.Set(r?.Fraction)));
                var name = BText(AioReadings.DriveName(snapshot, d.Id), 13, bInk, true); name.HorizontalAlignment = HorizontalAlignment.Center;
                var cell = Stack(6, new Grid { Width = 92, Height = 92, HorizontalAlignment = HorizontalAlignment.Center, Children = { ring, Squeeze(face, 14) } }, name);
                var rates = d.Rows.Where(r => !r.Key.EndsWith(".used")).Select(r => BKV(r, r.Key.EndsWith(".read") ? "READ" : "WRITE", 11)).ToArray();
                if (rates.Length > 0) cell.Children.Add(BGrid(rates, 2));
                return (Control)cell;
            }).ToArray();
            body.Children.Add(BGrid(cells, 2));
        }
        else
            foreach (var d in drives)
            {
                var name = BText(AioReadings.DriveName(snapshot, d.Id), 15, bInk, true);
                Control head = d.Temp != null && !top.Contains(d.Temp.Key) ? Ui.Columns("*,Auto", name, BV(d.Temp, 15, black.B)) : name;
                var block = Stack(6, head);
                if (d.Rows.FirstOrDefault(r => r.Key.EndsWith(".used")) is { } used)
                {
                    var line = Ui.Columns("*,Auto", BBar(used, 6), BV(used, 11)); line.ColumnSpacing = 8;
                    block.Children.Add(line);
                }
                var rates = d.Rows.Where(r => !r.Key.EndsWith(".used")).Select(r => BKV(r, r.Key.EndsWith(".read") ? "READ" : "WRITE", 12)).ToArray();
                if (rates.Length > 0) block.Children.Add(BGrid(rates, 2));
                if (BlackLayout == WidgetStyle.AioBlackGraph && d.Temp != null && BArea(d.Temp, black.B, 28) is { } curve) block.Children.Add(curve);
                body.Children.Add(block);
            }
        content.Children.Add(BCard("STORAGE", null, body));
    }

    private void BlackFans(HashSet<string> top)
    {
        var fans = Rs("Cooling").Where(r => !top.Contains(r.Key)).ToArray();
        if (fans.Length == 0) return;
        var tiles = fans.Select(f =>
        {
            var art = BArt("fan", 44); art.Active = (f.Number ?? 0) > 0;
            blackLive.Add((f.Key, r => art.Active = (r?.Number ?? 0) > 0));
            var value = BV(f, 16); formats[value] = row => row.Number.HasValue ? Format.Number(row.Number) + " rpm" : "—"; value.Text = formats[value](f);
            var name = BText(Translate(L.T(f.Label)).ToUpperInvariant(), 9.5, bMuted, true); name.TextTrimming = TextTrimming.CharacterEllipsis; name.TextWrapping = TextWrapping.NoWrap;
            var info = Stack(3, FitLeft(value), name, BBar(f, 4));
            var row = Ui.Columns("Auto,*", art, info); row.ColumnSpacing = 8;
            return (Control)new Border
            {
                Background = Palette.Brush(SeeThrough(Color.FromArgb(12, 255, 255, 255))), CornerRadius = new CornerRadius(12),
                Padding = new Thickness(8), Child = row
            };
        }).ToArray();
        content.Children.Add(BCard("COOLING", null, BGrid(tiles, 2, 8)));
    }

    // ---------------------------------------------------------------- tops of the layouts
    /// <summary>Dashboard: car-style gauges for processor and graphics temperature and load.</summary>
    private void BlackGauges(HashSet<string> top)
    {
        var gpuId = Rs("GPU").Select(r => r.Device).FirstOrDefault();
        var items = new List<(AioReading R, string Caption, Color C)>();
        void Add(AioReading? r, string caption, Color c) { if (r != null) { items.Add((r, caption, c)); top.Add(r.Key); } }
        Add(R("temp"), "CPU TEMP", black.A);
        if (gpuId != null) Add(R("temp", "GPU", gpuId), "GPU TEMP", black.B);
        Add(R("load"), "CPU LOAD", black.A);
        if (gpuId != null) Add(R("load", "GPU", gpuId), "GPU LOAD", black.B);
        if (items.Count == 0) return;
        var cells = items.Select(i =>
        {
            var arc = new BlackArc { Theme = black, Color = i.C, Tag = "graphic" };
            blackLive.Add((i.R.Key, r => arc.Set(r?.Fraction)));
            var value = BV(i.R, 20); value.HorizontalAlignment = HorizontalAlignment.Center;
            var caption = BText(i.Caption, 9.5, bMuted, true); caption.HorizontalAlignment = HorizontalAlignment.Center;
            return (Control)Stack(2, arc, Fit(value), caption);
        });
        content.Children.Add(BCard("LIVE", null, BGrid(cells, 2, 8)));
    }

    /// <summary>Tiles: big numbers for processor, graphics, memory and the first drive.</summary>
    private void BlackTiles(HashSet<string> top)
    {
        var gpuId = Rs("GPU").Select(r => r.Device).FirstOrDefault();
        var drive = PartDrives().FirstOrDefault();
        var tiles = new List<Control>();
        void Tile(string art, string caption, AioReading? value, AioReading? bar)
        {
            if (value == null) return;
            top.Add(value.Key);
            var big = BV(value, 24);
            if (value.Key == "aio.system.ram") { formats[big] = r => Format.Percent(r.Number); big.Text = Format.Percent(value.Number); }
            var head = Ui.Columns("Auto,*", BArt(art, 40), new Border { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Child = BText(caption, 9, bMuted, true) });
            var tile = Stack(8, head, FitLeft(big));
            if (bar != null) tile.Children.Add(BBar(bar, 5));
            tiles.Add(BFrame(tile, new Thickness(12)));
        }
        Tile("cpu", "CPU", R("temp") ?? R("load"), R("load"));
        if (gpuId != null) Tile("gpu", "GPU", R("temp", "GPU", gpuId) ?? R("load", "GPU", gpuId), R("load", "GPU", gpuId));
        var ram = R("ram", "System"); Tile("ram", "RAM", ram, ram);
        if (drive.Id != null) Tile("ssd", AioReadings.DriveName(snapshot, drive.Id), drive.Temp, drive.Rows.FirstOrDefault(r => r.Key.EndsWith(".used")));
        if (tiles.Count > 0) content.Children.Add(BGrid(tiles, 2, 10));
    }

    /// <summary>X-Ray: the PC from the side with the main value at each part.</summary>
    private void BlackXRay(HashSet<string> top)
    {
        var gpuId = Rs("GPU").Select(r => r.Device).FirstOrDefault();
        var drive = PartDrives().FirstOrDefault();
        var canvas = new Canvas { Width = BlackTower.Box.Width, Height = BlackTower.Box.Height };
        canvas.Children.Add(new BlackTower { Theme = black, Width = BlackTower.Box.Width, Height = BlackTower.Box.Height, Tag = "graphic" });
        void Callout(string spot, AioReading? r, string caption, bool left, double y, Func<AioReading, string>? format = null)
        {
            if (r == null) return;
            top.Add(r.Key);
            var at = BlackTower.Spots[spot]; double x = left ? 78 : 242;
            var color = left ? black.A : black.B;
            canvas.Children.Add(new Line { StartPoint = at, EndPoint = new Point(x, y), Stroke = Palette.Brush(color), StrokeThickness = 1.2 });
            canvas.Children.Add(new Ellipse { Width = 6, Height = 6, Fill = Palette.Brush(color), Margin = new Thickness(at.X - 3, at.Y - 3, 0, 0) });
            var value = BV(r, 13);
            if (format != null) { formats[value] = format; value.Text = format(r); }
            var label = BText(caption, 8, bMuted, true);
            // Each label keeps to its side of the tower: at large sizes it shrinks instead of leaving the card.
            var stack = left ? Stack(0, FitRight(label), FitRight(value)) : Stack(0, FitLeft(label), FitLeft(value));
            var box = new Border { Width = left ? x - 3 : BlackTower.Box.Width - x - 3, Child = stack };
            Canvas.SetLeft(box, left ? 0 : x + 3);
            Canvas.SetTop(box, y - 14);
            canvas.Children.Add(box);
        }
        Callout("cpu", R("temp") ?? R("load"), "CPU", true, 40);
        Callout("ram", R("ram", "System"), "RAM", false, 40, r => Format.Percent(r.Number));
        if (gpuId != null) Callout("gpu", R("temp", "GPU", gpuId) ?? R("load", "GPU", gpuId), "GPU", true, 116);
        if (drive.Id != null) Callout("ssd", drive.Temp, AioReadings.DriveName(snapshot, drive.Id), true, 186);
        var fan = Rs("Cooling").FirstOrDefault();
        if (fan != null) Callout("fan", fan, Translate(L.T(fan.Label)).ToUpperInvariant(), false, 168, r => r.Number.HasValue ? Format.Number(r.Number) + " rpm" : "—");
        Callout("psu", Rs("Rails").FirstOrDefault(), Rs("Rails").FirstOrDefault()?.Label ?? "", true, 254);
        var view = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.Both, Child = canvas, MaxHeight = 460 };
        content.Children.Add(BCard("SYSTEM", BText("X-RAY", 9, bMuted, true), view));
    }

    // ---------------------------------------------------------------- pieces
    /// <summary>Black letters are drawn a quarter larger than the other designs, so values and captions stay readable in a wide sidebar.</summary>
    private const double BlackBoost = 1.25;
    private TextBlock BText(string text, double size, Color color, bool bold = false, bool number = false)
    {
        var t = Text(text, size * BlackBoost, color, number); t.FontFamily = black.Font; t.FontWeight = bold ? FontWeight.Bold : FontWeight.SemiBold; t.TextWrapping = TextWrapping.NoWrap; return t;
    }
    private Control BCaption(string text) => FitLeft(BText(text, 9.5, bMuted, true));
    private TextBlock BV(AioReading r, double size, Color? color = null)
    {
        var t = V(r, size * BlackBoost, color ?? bInk); t.FontFamily = black.Font; t.FontWeight = FontWeight.Bold; return t;
    }
    /// <summary>A value with its small caption below ("3.88 GHz" / CLOCK).</summary>
    private Control BKV(AioReading r, string? caption = null, double size = 16) => Stack(1, FitLeft(BV(r, size)), BCaption(caption ?? PanelCaption(r)));
    private Control BGrid(IEnumerable<Control> items, int columns, double gap = 10)
    {
        var array = items.ToArray();
        var grid = FixedCols(Math.Min(columns, Math.Max(1, array.Length)), array);
        grid.ColumnSpacing = gap; grid.RowSpacing = gap; return grid;
    }
    private Control? BModel(string model)
    {
        if (model.Length == 0) return null;
        var t = BText(model, 10, bInk, true); t.TextTrimming = TextTrimming.CharacterEllipsis; return t;
    }
    private BlackArt BArt(string kind, double width) => new()
    {
        // Line art and 3D pictures are square-ish, so wide spots (graphics card, drive) get a smaller box than the realistic pictures.
        Kind = kind, Theme = black, MaxWidth = (black.Art == "real" ? width : Math.Min(width, 120)) * (settings.ActiveWidget?.Graphics ?? 1), Tag = "graphic",
        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
    };
    /// <summary>Picture left (inside a live ring on the Rings layout), values right.</summary>
    private Control ArtBeside(string kind, double width, Control values, AioReading? ring = null)
    {
        // Rings: the picture fills the inside of the ring; wide pictures (graphics card, memory, network) get more width.
        bool square = kind is "cpu" or "igpu" or "psu" or "fan";
        Control left = BArt(kind, BlackLayout == WidgetStyle.AioBlackRings ? width * 1.12 * (square ? .66 : .7) : width);
        if (BlackLayout == WidgetStyle.AioBlackRings)
        {
            double size = width * 1.12 * (settings.ActiveWidget?.Graphics ?? 1);
            var r = new BlackRing { Theme = black, Width = size, Height = size, Tag = "graphic" };
            if (ring != null) blackLive.Add((ring.Key, row => r.Set(row?.Fraction)));
            left = new Grid { Width = size, Height = size, Children = { r, left } };
        }
        var row = Ui.Columns("Auto,*", left, new Border { VerticalAlignment = VerticalAlignment.Center, Child = values }); row.ColumnSpacing = 12;
        return row;
    }
    private BlackBar BBar(AioReading r, double height)
    {
        var bar = new BlackBar { Theme = black, Height = height, MinWidth = 30, VerticalAlignment = VerticalAlignment.Center };
        blackLive.Add((r.Key, row => bar.Set(row?.Fraction)));
        return bar;
    }
    /// <summary>History curve of a reading; nothing when graphs are switched off.</summary>
    private Control? BArea(AioReading r, Color color, double height)
    {
        if (!settings.Shows("aio.graphs")) return null;
        var area = new BlackArea { Color = color, Height = height, Tag = "graphic" };
        blackPlots.Add((r.Key, area));
        return area;
    }
    private Control BCard(string title, Control? right, Control body)
    {
        var name = BText(title, 10.5, bMuted, true); name.LetterSpacing = 1.2;
        Control head = right == null ? name : Ui.Columns("Auto,*", name, new Border { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(10, 0, 0, 0), Child = right });
        return BFrame(Stack(10, head, body), new Thickness(14));
    }
    /// <summary>A card in the chosen style: black, carbon, neon outline, top line, gradient edge or glass.</summary>
    private Control BFrame(Control child, Thickness padding)
    {
        var a = black.A;
        Color fill = palette.OwnBackground ? back : Color.Parse("#0B0B0D");
        var border = new Border { Padding = padding, Child = child, Tag = "aio-section", ClipToBounds = true };
        switch (black.Cards)
        {
            case "carbon":
                border.Background = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Absolute), EndPoint = new RelativePoint(6, 6, RelativeUnit.Absolute), SpreadMethod = GradientSpreadMethod.Repeat,
                    GradientStops = { new(SeeThrough(Color.Parse("#0D0D0D")), 0), new(SeeThrough(Color.Parse("#0D0D0D")), .5), new(SeeThrough(Color.Parse("#141414")), .5), new(SeeThrough(Color.Parse("#141414")), 1) }
                };
                border.BorderBrush = Palette.Brush(Color.Parse("#262626")); border.BorderThickness = new Thickness(1); border.CornerRadius = new CornerRadius(10);
                break;
            case "neon":
                border.Background = Palette.Brush(SeeThrough(Palette.Mix(fill, Colors.Black, .5)));
                border.BorderBrush = Palette.Brush(BlackDraw.A(a, .7)); border.BorderThickness = new Thickness(1); border.CornerRadius = new CornerRadius(4);
                border.BoxShadow = new BoxShadows(new BoxShadow { Blur = 14, Color = BlackDraw.A(a, .16) });
                break;
            case "line":
                border.Background = Palette.Brush(SeeThrough(fill));
                border.BorderBrush = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                    GradientStops = { new(a, 0), new(a, .02), new(Color.Parse("#1D1D20"), .021), new(Color.Parse("#1D1D20"), 1) }
                };
                border.BorderThickness = new Thickness(1, 2, 1, 1); border.CornerRadius = new CornerRadius(12);
                break;
            case "edge":
                border.Background = Palette.Brush(SeeThrough(fill));
                border.BorderBrush = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                    GradientStops = { new(a, 0), new(Colors.Transparent, .6), new(BlackDraw.A(a, .4), 1) }
                };
                border.BorderThickness = new Thickness(1.5); border.CornerRadius = new CornerRadius(18);
                break;
            case "glass":
                border.Background = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                    GradientStops = { new(SeeThrough(Color.FromArgb(30, 255, 255, 255)), 0), new(SeeThrough(Color.FromArgb(10, 255, 255, 255)), 1) }
                };
                border.BorderBrush = Palette.Brush(Color.FromArgb(36, 255, 255, 255)); border.BorderThickness = new Thickness(1); border.CornerRadius = new CornerRadius(20);
                break;
            default:
                border.Background = Palette.Brush(SeeThrough(fill));
                border.BorderBrush = Palette.Brush(BlackDraw.A(a, .2)); border.BorderThickness = new Thickness(1); border.CornerRadius = new CornerRadius(16);
                break;
        }
        return border;
    }

    /// <summary>Red designs: every text of the card in the Panel designs' Orbitron, set when the card is built.</summary>
    private void RedFont()
    {
        // Type Block is built from huge figures sized for its own font; it keeps it (Orbitron would be too wide).
        if (style == WidgetStyle.AioTypeBlock) return;
        foreach (var text in content.GetLogicalDescendants().OfType<TextBlock>()) text.FontFamily = PanelTheme.Font;
    }

    private void UpdateBlack(History h)
    {
        foreach (var (key, set) in blackLive) set(readings.GetValueOrDefault(key));
        foreach (var (key, area) in blackPlots)
        {
            var r = readings.GetValueOrDefault(key);
            var series = r != null && r.History.Length > 0 ? h.Read(r.History) : samples.Read(key);
            if (series.Length == 0) series = samples.Read(key);
            area.Set(series, r?.Maximum ?? 100);
        }
        if (blackCores != null) blackCores.Loads = lastCoreLoads;
    }
}
