using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Lumen.Core;

namespace Lumen.App;

/// <summary>
/// 5.8.0: the full-screen desktop designs. Choosing one switches the sidebar to full screen (as Mainboard Map Desktop); the
/// design builds its parts (part cards with picture and history, cores, drives, fans, network, power supply, the mainboard)
/// and a <see cref="DeskScene"/> places them in a grid that fills the screen, wide or upright. Every value is bound to its AIO
/// sensor as in every AIO design: the sensor choices and widget switches work, each value is shown once (the mainboard and the
/// glowing pictures show the temperature as a colour, not as a second number) and anything else ticked ends in "More".
/// </summary>
public sealed partial class AioTile
{
    private static readonly Lazy<Bitmap> deskBoard = new(() => new Bitmap(AssetLoader.Open(new Uri("avares://Lumen/Assets/Aio/mainboard-map.png"))));
    private bool Desk => Fam == NeoDesigns.Family.Desk;
    private readonly Dictionary<string, Control> deskParts = new();
    /// <summary>Keynote: the panels without cards, only a fine line above each.</summary>
    private bool deskPlain;

    private void DeskLook()
    {
        FontFamily inter = Ui.DesignFont(Ui.Inter);
        (nFont, nValueFont, nBigFont) = (inter, inter, inter);
        nInk = lightSurface ? ink : Color.Parse("#F2F5F8");
        nLabel = lightSurface ? muted : Color.Parse("#8C96A3");
        nTrack = lightSurface ? track : Color.Parse("#1B232D");
        nPanel = Palette.Mix(nSurface, Colors.White, .035);
        CornerRadius = new CornerRadius(0); BorderThickness = new Thickness(0);
        Padding = new Thickness(18, 14, 18, 18);
        content.Spacing = 0;
    }

    private void DeskDesign()
    {
        DeskLook();
        deskParts.Clear(); deskPlain = false; deskSkin = DeskSkin.Default; deskBackdrop = null; deskMetal = false;
        if (DeskNext) DeskNextLook();
        var top = new HashSet<string>();
        (DeskGrid[] Wide, DeskGrid[] Tall) grids;
        if (designStyle is WidgetStyle.AioDeskBoard or WidgetStyle.AioDeskBoardLeft)
        {
            DeskBoardPicture();
            DeskCpu(top); DeskGpus(top); DeskCores(top); DeskMemory(top); DeskRails(top); DeskNetwork(top); DeskDrives(top); DeskFans(top);
            grids = DeskGrids(designStyle);
        }
        else grids = DeskLiving(top);
        var extras = referenceRows.Where(r => !referenceUsed.Contains(r.Key) && !top.Contains(r.Key)).ToArray();
        if (extras.Length > 0)
            deskParts["more"] = DeskCard(Stack(10, DeskTitle(L.T("More"), nInk), DeskCols(4, 220, extras.Select(r => DeskKV(r)))));
        foreach (var r in referenceRows) referenceUsed.Add(r.Key);

        var scene = new DeskScene(grids.Wide, grids.Tall) { Tag = "aio-section", Gap = 14 };
        // Stacked in a narrow sidebar the parts follow the order they were built in.
        foreach (var (name, part) in deskParts) { DeskScene.SetArea(part, name); scene.Children.Add(part); }
        // A design with a picture behind everything (the sea, space): the picture fills the screen, the scene lies on it.
        content.Children.Add(deskBackdrop == null ? scene : new Grid { Children = { deskBackdrop, scene } });
    }

    /// <summary>The grids of the desktop designs: wide screens (16:9, 21:9, 5:4) and upright ones, each in the order tried
    /// (the next one when the parts do not fit the screen, e.g. several graphics cards).</summary>
    private static (DeskGrid[] Wide, DeskGrid[] Tall) DeskGrids(WidgetStyle style)
    {
        var tall = new[]
        {
            DeskGrid.Of("1 1", "1.7 1.15 1 .8 .8 .7 0", "board board", "cpu gpu", "cores ram", "psu net", "drives drives", "fans fans", "more more"),
            DeskGrid.Of("1 1", "1.5 1 1 .8 .8 .8 .6 0", "board board", "cpu gpu", "cores gpu", "psu gpu", "ram net", "drives drives", "fans fans", "more more"),
        };
        return style switch
        {
            WidgetStyle.AioDeskBoardLeft => (new[]
            {
                DeskGrid.Of("1.5 1 1", "1.2 1 .85 .75 0", "board cpu gpu", "board cores ram", "board psu net", "drives drives fans", "more more more"),
                DeskGrid.Of("1.5 1 1", "1.2 1 .85 .9 .75 0", "board cpu gpu", "board cores gpu", "board psu gpu", "ram net fans", "drives drives drives", "more more more"),
            }, tall),
            _ => (new[]
            {
                DeskGrid.Of("1 1.5 1", "1.2 1 .85 .75 0", "cpu board gpu", "cores board ram", "psu board net", "drives drives fans", "more more more"),
                DeskGrid.Of("1 1.5 1", "1.2 1 .85 .9 .75 0", "cpu board gpu", "cores board gpu", "psu board gpu", "ram net fans", "drives drives drives", "more more more"),
            }, tall)
        };
    }

    // ------------------------------------------------------------------ pieces
    private Border DeskCard(Control body, Thickness? padding = null) => deskSkin != DeskSkin.Default && !deskPlain ? DeskSkinned(body, padding) : deskPlain ? new Border
    {
        BorderBrush = Palette.Brush(Palette.Mix(nSurface, Colors.White, .14)), BorderThickness = new Thickness(0, 1, 0, 0),
        Padding = new Thickness(0, 14, 0, 4), Child = body, ClipToBounds = true
    } : new()
    {
        Background = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = { new(SeeThrough(Palette.Mix(nSurface, Color.Parse("#16212C"), .62)), 0), new(SeeThrough(Palette.Mix(nSurface, Color.Parse("#0E151C"), .7)), 1) }
        },
        BorderBrush = Palette.Brush(Palette.Mix(nSurface, Color.Parse("#2A3A4A"), .7)), BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(14), Padding = padding ?? new Thickness(20, 14, 20, 14), Child = body, ClipToBounds = true
    };

    private TextBlock DeskTitle(string text, Color colour)
    {
        var t = NText(text, 15, colour, true); TextFonts.Mark(t, TextRole.Title); t.TextTrimming = TextTrimming.CharacterEllipsis; return t;
    }

    private TextBlock? DeskModel(string model)
    {
        if (model.Length == 0) return null;
        var t = NText(model, 12.5, nLabel); t.TextWrapping = TextWrapping.Wrap; t.MaxLines = 2; t.TextTrimming = TextTrimming.CharacterEllipsis; return t;
    }

    /// <summary>Caption left, value right, both readable from across the desk.</summary>
    private Control DeskKV(AioReading r, string? caption = null)
    {
        var label = NText(caption ?? RefLabel(r), 13.5, nLabel); label.TextTrimming = TextTrimming.CharacterEllipsis;
        var row = Ui.Columns("Auto,*", label, FitRight(NV(r, 17))); row.ColumnSpacing = 10; return row;
    }

    private Control DeskList(IEnumerable<AioReading> rows) => DeskCols(2, 180, rows.Select(r => DeskKV(r)));

    /// <summary>Values in up to <paramref name="most"/> columns with a clear gap between them; fewer columns where they would get narrower than <paramref name="least"/>.</summary>
    private Control DeskCols(int most, double least, IEnumerable<Control> items)
    {
        var cells = items.Select(c => (Control)new Border { Margin = new Thickness(14, 0), Child = c }).ToArray();
        return new StatPanel(Array.Empty<double>(), cells) { MaxColumns = most, MinimumColumnWidth = least, RespectIntrinsicWidth = false, Balanced = false, RowSpacing = 6, Margin = new Thickness(-14, 0) };
    }

    /// <summary>The picture of a part glowing in the colour of its temperature.</summary>
    private Control DeskPicture(string art, AioReading? heatFrom, double width, double height)
    {
        var glow = new ArtHeatGlow(false);
        if (heatFrom != null) { glow.Set(Celsius(heatFrom)); watchers.Add((heatFrom.Key, row => glow.Set(Celsius(row)))); }
        var picture = Part(art, width * .86, height * .86);
        var scene = new Grid { Width = width, Height = height, Children = { glow, picture } };
        return new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, MaxWidth = width, Child = scene, VerticalAlignment = VerticalAlignment.Center };
    }

    /// <summary>The big value of a part: temperature with its degree sign, memory as a percentage, else as the reading reads.</summary>
    private Control DeskBig(AioReading r, double size = 54)
    {
        var value = FitLeft(GValue(r, size));
        return value;
    }

    /// <summary>A part card: picture, title, model and the big value on top, the other values below, the history filling the rest.</summary>
    private Control DeskPartCard(string title, Color colour, string model, string? art, AioReading main, IEnumerable<AioReading> rest, AioReading? history, Control? under = null, bool compact = false)
    {
        var head = Stack(2, DeskTitle(title, colour));
        if (DeskModel(model) is { } m) head.Children.Add(m);
        if (under != null) head.Children.Add(under);
        head.Children.Add(DeskBig(main, compact ? 40 : 50));
        Control top = head;
        if (art != null)
        {
            var row = Ui.Columns(compact ? "*,3*" : "*,2*", compact ? DeskPicture(art, main, 104, 78) : DeskPicture(art, main, 150, 112), head); row.ColumnSpacing = 14; top = row;
        }
        var body = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*"), RowSpacing = compact ? 8 : 12 };
        body.Children.Add(top);
        var list = rest.ToList();
        if (list.Count > 0) { var values = compact ? DeskCols(3, 150, list.Select(r => DeskKV(r))) : DeskList(list); Grid.SetRow(values, 1); body.Children.Add(values); }
        if (history != null && AioReadings.Graphs(settings, history.Key))
        {
            var plot = HistoryPlot(history, compact ? 24 : 32, "area", colour); plot.Height = double.NaN;
            var fill = new DeskElastic { Min = compact ? 24 : 32, Child = plot }; Grid.SetRow(fill, 2); body.Children.Add(fill);
        }
        return DeskCard(body, compact ? new Thickness(18, 12, 18, 12) : null);
    }

    private void DeskCpu(HashSet<string> top)
    {
        var cpu = Rs("CPU");
        if (cpu.Length == 0) return;
        var main = R("temp") ?? R("load") ?? cpu[0];
        top.Add(main.Key);
        deskParts["cpu"] = DeskPartCard("CPU", NAcc(0), PartModel(WidgetKind.Cpu, 0, snapshot.Cpu?.Name ?? ""), "cpu", main, Ordered(cpu.Where(r => r != main)), main);
    }

    private void DeskGpus(HashSet<string> top)
    {
        var cards = new List<Control>(); int index = 0;
        var groups = Rs("GPU").GroupBy(r => r.Device).ToList();
        foreach (var gpu in groups)
        {
            var rows = gpu.ToArray();
            var main = rows.FirstOrDefault(r => r.Key.EndsWith(".temp")) ?? rows.FirstOrDefault(r => r.Key.EndsWith(".load")) ?? rows[0];
            top.Add(main.Key);
            var info = snapshot.Gpus.FirstOrDefault(g => g.Id == gpu.Key);
            cards.Add(DeskPartCard(GpuTitle(gpu.Key, index, true), NAcc(1), PartModel(WidgetKind.Gpu, index, info?.Name ?? ""), "gpu", main, Ordered(rows.Where(r => r != main)), main, compact: groups.Count > 1));
            index++;
        }
        if (cards.Count == 1) deskParts["gpu"] = cards[0];
        else if (cards.Count > 1) { var flow = new DeskFlow { Vertical = true, Gap = 12 }; flow.Children.AddRange(cards); deskParts["gpu"] = flow; }
    }

    private void DeskMemory(HashSet<string> top)
    {
        var ram = R("ram", "System");
        var more = Rs("System").Where(r => r.Key.StartsWith("aio.system.ram.") && !AioReadings.IsGraphKey(r.Key)).ToArray();
        if (ram == null)
        {
            if (more.Length > 0) deskParts["ram"] = DeskCard(Stack(10, DeskTitle("RAM", NAcc(2)), DeskList(more)));
            return;
        }
        top.Add(ram.Key);
        // The amount in use under the title, from the same reading as the percentage.
        var amount = NText(Amount(ram), 12.5, nLabel, false, true); formats[amount] = Amount; AddField(ram.Key, amount);
        deskParts["ram"] = DeskPartCard("RAM", NAcc(2), "", "ram", ram, more, ram, amount);
    }

    private void DeskCores(HashSet<string> top)
    {
        var body = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), RowSpacing = 12 };
        body.Children.Add(DeskTitle(L.T("Cores"), nInk));
        bool any = false;
        if (lastCoreLoads.Count >= 2 && AioReadings.Selected(settings, "aio.cpu.coreloads") && Rs("CPU").Length > 0)
        {
            coreBars = new AioCoreBars(nInk, nLabel, nTrack) { Loads = lastCoreLoads, Heatmap = true, Fill = true, Tint = Color.TryParse(slot?.Accent, out var own) ? own : skin, Tag = "graphic" };
            TextColours.Bind(coreBars, "aio.cpu.coreloads");
            Grid.SetRow(coreBars, 1); body.Children.Add(coreBars); any = true;
        }
        var temps = Rs("Cores");
        if (temps.Length > 0)
        {
            var list = DeskCols(4, 110, temps.Select(r => DeskKV(r, r.Label.Replace("Core", "C"))));
            Grid.SetRow(list, 2); body.Children.Add(list); any = true;
        }
        if (any) deskParts["cores"] = DeskCard(body);
    }

    private void DeskRails(HashSet<string> top)
    {
        var rails = Rs("Rails");
        if (rails.Length == 0) return;
        var main = rails.FirstOrDefault(r => r.Label.Contains("12")) ?? rails[0];
        top.Add(main.Key);
        deskParts["psu"] = DeskPartCard(L.T("PSU"), NAcc(1), "", null, main, rails.Where(r => r != main), null, DeskModel(main.Label));
    }

    private void DeskNetwork(HashSet<string> top)
    {
        var net = Rs("Network");
        if (net.Length == 0) return;
        var down = net.FirstOrDefault(r => r.Key.EndsWith(".down")); var up = net.FirstOrDefault(r => r.Key.EndsWith(".up")); var link = net.FirstOrDefault(r => r.Key.EndsWith(".link"));
        Control Column(AioReading r, string caption, Color colour)
        {
            var label = NText(caption, 12, nLabel, true); label.LetterSpacing = 1.4;
            var value = NV(r, 24, colour); value.TextWrapping = TextWrapping.NoWrap;
            return Stack(3, label, FitLeft(value));
        }
        var columns = new List<Control>();
        if (down != null) columns.Add(Column(down, "↓ DL", NAcc(0)));
        if (up != null) columns.Add(Column(up, "↑ UP", NAcc(1)));
        if (link != null) columns.Add(Column(link, "LINK", nInk));
        var body = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*"), RowSpacing = 12 };
        body.Children.Add(DeskTitle(L.T("Network"), NAcc(0)));
        if (columns.Count > 0) { var row = DeskCols(3, 140, columns); Grid.SetRow(row, 1); body.Children.Add(row); }
        var others = net.Where(r => r != down && r != up && r != link).ToArray();
        if (others.Length > 0) { var list = DeskList(others); Grid.SetRow(list, 2); body.Children.Add(list); }
        var first = down ?? up;
        if (first != null && AioReadings.Graphs(settings, first.Key))
        {
            var plot = HistoryPlot(first, 32, "area", NAcc(0), first == down ? up : null); plot.Height = double.NaN;
            var fill = new DeskElastic { Min = 32, Child = plot }; Grid.SetRow(fill, 3); body.Children.Add(fill);
        }
        deskParts["net"] = DeskCard(body);
    }

    private void DeskDrives(HashSet<string> top)
    {
        var drives = PartDrives();
        if (drives.Length == 0) return;
        var flow = new DeskFlow { MinItem = 300, Gap = 16 };
        foreach (var d in drives)
        {
            var used = d.Rows.FirstOrDefault(r => r.Key.EndsWith(".used"));
            var main = d.Temp ?? used ?? d.Rows[0];
            top.Add(main.Key);
            var head = Stack(2, DeskTitle(AioReadings.DriveName(snapshot, d.Id), nInk));
            if (DeskModel(DriveModel(d.Id)) is { } m) head.Children.Add(m);
            head.Children.Add(DeskBig(main, 44));
            var rates = d.Rows.Where(r => r != main && r != used).ToArray();
            if (rates.Length > 0) head.Children.Add(DeskCols(2, 150, rates.Select(r => DeskKV(r))));
            if (used != null)
            {
                // The bar of the space in use stays when the temperature is switched off (then the percentage is the big value).
                var meter = new AioMeter(NAcc(0), nTrack, 0) { Height = 6, VerticalAlignment = VerticalAlignment.Center }; meters.Add((used.Key, meter));
                if (used != main) { var line = Ui.Columns("*,Auto", meter, NV(used, 15)); line.ColumnSpacing = 10; head.Children.Add(line); }
                else { meter.Margin = new Thickness(0, 4, 0, 0); head.Children.Add(meter); }
            }
            var row = Ui.Columns("*,2.2*", DeskPicture("ssd", d.Temp ?? main, 140, 104), head); row.ColumnSpacing = 14;
            var body = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), RowSpacing = 10, Children = { row } };
            if (d.Temp != null && AioReadings.Graphs(settings, d.Temp.Key))
            {
                var plot = HistoryPlot(d.Temp, 32, "area", NAcc(3)); plot.Height = double.NaN;
                var fill = new DeskElastic { Min = 32, Child = plot }; Grid.SetRow(fill, 1); body.Children.Add(fill);
            }
            flow.Children.Add(DeskCard(body));
        }
        deskParts["drives"] = flow;
    }

    private void DeskFans(HashSet<string> top)
    {
        var fans = Rs("Cooling");
        if (fans.Length == 0) return;
        var cells = new DeskFlow { MinItem = 72, Gap = 6 };
        for (int i = 0; i < fans.Length; i++)
        {
            var f = fans[i];
            var value = NV(f, 24); formats[value] = row => row.Number.HasValue ? Format.Number(row.Number) : "—"; value.Text = formats[value](f);
            value.HorizontalAlignment = HorizontalAlignment.Center; value.TextWrapping = TextWrapping.NoWrap;
            var name = NText(Naming.Short(Translate(L.T(f.Label))), 12, nLabel); name.HorizontalAlignment = HorizontalAlignment.Center; name.TextTrimming = TextTrimming.CharacterEllipsis;
            var picture = Part(FanArt[i % FanArt.Length], 400, 400);
            var art = new DeskElastic { Min = 34, Max = 86, Child = picture };
            var cell = new Grid { RowDefinitions = new RowDefinitions("*,Auto,Auto"), RowSpacing = 2, Children = { art } };
            var number = Fit(value); Grid.SetRow(number, 1); Grid.SetRow(name, 2); cell.Children.Add(number); cell.Children.Add(name);
            cells.Children.Add(cell);
        }
        var body = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), RowSpacing = 10 };
        body.Children.Add(DeskTitle(L.T("Fans") + " · rpm", nInk));
        Grid.SetRow(cells, 1); body.Children.Add(cells);
        if (AioReadings.Graphs(settings, fans[0].Key) && fans.FirstOrDefault(f => f.Number > 0) is { } spinning)
        {
            var plot = HistoryPlot(spinning, 30, "area", NAcc(0)); Grid.SetRow(plot, 2); body.Children.Add(plot);
        }
        deskParts["fans"] = DeskCard(body);
    }

    /// <summary>The mainboard with the processor, graphics card, memory and the first two drives outlined in the colour of their temperature.</summary>
    private void DeskBoardPicture()
    {
        var art = new DeskBoardArt(deskBoard.Value, nSurface);
        void Spot(string name, AioReading? r, Rect box)
        {
            if (r == null) return;
            int at = art.Add(name, box); art.Set(at, Celsius(r)); watchers.Add((r.Key, row => art.Set(at, Celsius(row))));
        }
        Spot("CPU", R("temp") ?? R("load"), new Rect(.37, .20, .22, .21));
        Spot("RAM", R("ram", "System"), new Rect(.63, .11, .15, .46));
        var gpu = Rs("GPU").FirstOrDefault(g => snapshot.Gpus.FirstOrDefault(x => x.Id == g.Device)?.Integrated != true)?.Device;
        if (gpu != null) Spot(GpuTitle(gpu, 0, true), R("temp", "GPU", gpu) ?? R("load", "GPU", gpu), new Rect(.22, .54, .46, .20));
        var drives = PartDrives();
        var slots = new[] { new Rect(.33, .76, .26, .07), new Rect(.61, .76, .26, .07) };
        for (int i = 0; i < Math.Min(2, drives.Length); i++)
            Spot(AioReadings.DriveName(snapshot, drives[i].Id), drives[i].Temp ?? drives[i].Rows.FirstOrDefault(r => r.Key.EndsWith(".used")), slots[i]);
        // The colours of the temperatures under the board.
        var legend = new ArtHeatLegend { Margin = new Thickness(0, 6, 0, 2) };
        var body = new Grid { RowDefinitions = new RowDefinitions("*,Auto"), Children = { new DeskElastic { Min = 240, Child = art }, legend } };
        Grid.SetRow(legend, 1);
        var card = DeskCard(body, new Thickness(10));
        DeskScene.SetStackShare(card, 1);
        deskParts["board"] = card;
    }
}

/// <summary>The mainboard of the desktop designs: the picture with glowing outlines and name tags on its parts (no numbers).</summary>
public sealed class DeskBoardArt : ArtDecoration
{
    private static readonly Typeface Face = new(Ui.Inter, FontStyle.Normal, FontWeight.ExtraBold);
    private readonly Bitmap board; private readonly Color surface;
    private readonly List<(string Name, Rect Box, Color Heat)> spots = new();
    public DeskBoardArt(Bitmap board, Color surface) : base(Colors.White) { this.board = board; this.surface = surface; ClipToBounds = true; }
    public int Add(string name, Rect box) { spots.Add((name, box, HeatColour.Of(null))); return spots.Count - 1; }
    public void Set(int index, double? celsius) { spots[index] = spots[index] with { Heat = HeatColour.Of(celsius) }; InvalidateVisual(); }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height, s = Math.Min(w, h); if (s < 40) return;
        var area = new Rect((w - s) / 2, (h - s) / 2, s, s);
        c.DrawImage(board, new Rect(board.Size), area);
        c.FillRectangle(new SolidColorBrush(Color.FromArgb(46, 0, 0, 0)), area);
        double size = Math.Clamp(s * .021, 11, 17);
        foreach (var (name, box, heat) in spots)
        {
            var r = new Rect(area.X + box.X * s, area.Y + box.Y * s, box.Width * s, box.Height * s);
            c.DrawRectangle(new SolidColorBrush(Color.FromArgb(30, heat.R, heat.G, heat.B)), null, r, 6, 6);
            foreach (var (width, alpha) in new[] { (14.0, 18), (9.0, 34), (5.0, 60) })
                c.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb((byte)alpha, heat.R, heat.G, heat.B)), width), r, 6, 6);
            c.DrawRectangle(null, new Pen(new SolidColorBrush(heat), 2), r, 6, 6);
            var text = new FormattedText(name, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Face, size, new SolidColorBrush(heat));
            var tag = new Rect(r.X - 1, r.Y - text.Height - 10, text.Width + 16, text.Height + 6);
            c.DrawRectangle(new SolidColorBrush(Palette.Mix(surface, Colors.Black, .2)), new Pen(new SolidColorBrush(heat), 1), tag, 4, 4);
            c.DrawText(text, new Point(tag.X + 8, tag.Y + 3));
        }
    }
}
