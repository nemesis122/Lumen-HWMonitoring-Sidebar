using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

/// <summary>
/// 5.8.0: the ten Living PC desktop designs (drafts LP-01 … LP-10). Each part (processor, graphics cards, memory, drives) is
/// shown once large with its glowing picture; everything else of the part follows in the panels beside it (details, cores,
/// memory, storage, network, fans, power supply), so every value appears exactly once.
/// </summary>
public sealed partial class AioTile
{
    private sealed record DeskItem(AioReading R, string Caption, string Art, Color Colour, string Model, string Kind, TextBlock? Under = null);

    /// <summary>The main value of every part: processor and graphics temperature (else load), memory in use, drive temperatures.</summary>
    private List<DeskItem> DeskItems(HashSet<string> top)
    {
        var items = new List<DeskItem>();
        if ((R("temp") ?? R("load")) is { } cpu) items.Add(new(cpu, "CPU", "cpu", NAcc(0), PartModel(WidgetKind.Cpu, 0, snapshot.Cpu?.Name ?? ""), "cpu"));
        int index = 0;
        foreach (var gpu in Rs("GPU").GroupBy(r => r.Device))
        {
            var rows = gpu.ToArray();
            var main = rows.FirstOrDefault(r => r.Key.EndsWith(".temp")) ?? rows.FirstOrDefault(r => r.Key.EndsWith(".load")) ?? rows[0];
            var info = snapshot.Gpus.FirstOrDefault(g => g.Id == gpu.Key);
            items.Add(new(main, GpuTitle(gpu.Key, index, true), "gpu", NAcc(1), PartModel(WidgetKind.Gpu, index, info?.Name ?? ""), "gpu"));
            index++;
        }
        if (R("ram", "System") is { } ram)
        {
            // The amount in use under the percentage, from the same reading.
            var amount = NText(Amount(ram), 12, nLabel, false, true); formats[amount] = Amount; AddField(ram.Key, amount);
            items.Add(new(ram, "RAM", "ram", NAcc(2), "", "ram", amount));
        }
        foreach (var d in PartDrives())
            if (d.Temp != null) items.Add(new(d.Temp, AioReadings.DriveName(snapshot, d.Id), "ssd", NAcc(3), DriveModel(d.Id), "drive"));
        foreach (var i in items) top.Add(i.R.Key);
        return items;
    }

    /// <summary>The line under a part's value: the memory amount, else the model.</summary>
    private TextBlock? DeskUnder(DeskItem it) => it.Under ?? (it.Model.Length > 0 ? NText(it.Model, 12, nLabel) : null);

    private Image DeskArt(string art) => new()
    {
        // Stretched alignment: the picture fills its space (drawn centred and uniform); centred alignment would keep its measured size.
        Source = isometricParts[RedParts ? art + "_red" : art].Value, Stretch = Stretch.Uniform, Tag = "graphic"
    };

    private enum DeskStage { Glow, Podium, Plain }

    /// <summary>The glowing picture of a part, as large as its space allows.</summary>
    private Control DeskStagePicture(DeskItem it, DeskStage stage, double min)
    {
        var glow = new ArtHeatGlow(stage == DeskStage.Glow); glow.Set(Celsius(it.R)); watchers.Add((it.R.Key, row => glow.Set(Celsius(row))));
        var picture = DeskArt(it.Art); picture.Margin = new Thickness(8);
        Control art = stage == DeskStage.Plain ? new Grid { Children = { picture } } : new Grid { Children = { glow, picture } };
        if (stage == DeskStage.Podium)
        {
            var beam = new ArtDeskBeam(); var podium = new ArtPodium { Margin = new Thickness(18, 0) };
            void Heat(AioReading? row) { beam.Set(Celsius(row)); podium.Set(Celsius(row)); }
            Heat(it.R); watchers.Add((it.R.Key, Heat));
            var grid = new Grid { RowDefinitions = new RowDefinitions("5*,1*") };
            Grid.SetRowSpan(beam, 2); grid.Children.Add(beam); grid.Children.Add(art);
            Grid.SetRow(podium, 1); grid.Children.Add(podium);
            art = grid;
        }
        return new DeskElastic { Min = min, Child = art };
    }

    private TextBlock DeskCaption(string text, Color colour, double size = 12.5)
    {
        var t = NText(text.ToUpperInvariant(), size, colour, true); t.LetterSpacing = 1.6; t.TextTrimming = TextTrimming.CharacterEllipsis; return t;
    }

    /// <summary>A part standing on its own: picture on top, the value large below, caption and model under it.</summary>
    private Control DeskFigure(DeskItem it, double big, DeskStage stage = DeskStage.Glow, double min = 70)
    {
        var grid = new Grid { RowDefinitions = new RowDefinitions("*,Auto,Auto,Auto") };
        grid.Children.Add(DeskStagePicture(it, stage, min));
        var value = Fit(GValue(it.R, big)); value.HorizontalAlignment = HorizontalAlignment.Center; Grid.SetRow(value, 1); grid.Children.Add(value);
        var caption = DeskCaption(it.Caption, it.Colour); caption.HorizontalAlignment = HorizontalAlignment.Center; Grid.SetRow(caption, 2); grid.Children.Add(caption);
        if (DeskUnder(it) is { } u) { u.HorizontalAlignment = HorizontalAlignment.Center; u.TextTrimming = TextTrimming.CharacterEllipsis; u.Margin = new Thickness(0, 2, 0, 0); Grid.SetRow(u, 3); grid.Children.Add(u); }
        return grid;
    }

    /// <summary>A part in a card: picture left, caption, value and model right.</summary>
    private Control DeskTile(DeskItem it, double big, bool card = true)
    {
        var text = Stack(2, DeskCaption(it.Caption, it.Colour));
        text.Children.Add(FitLeft(GValue(it.R, big)));
        if (DeskUnder(it) is { } u) { u.TextTrimming = TextTrimming.CharacterEllipsis; text.Children.Add(u); }
        text.VerticalAlignment = VerticalAlignment.Center;
        var row = Ui.Columns("1*,1.25*", DeskStagePicture(it, DeskStage.Glow, 64), text); row.ColumnSpacing = 12;
        return card ? DeskCard(row, new Thickness(14, 10, 16, 10)) : row;
    }

    private DeskFlow DeskRow(IEnumerable<Control> items, double minItem, double gap = 14, bool vertical = false)
    {
        var flow = new DeskFlow { MinItem = minItem, Gap = gap, Vertical = vertical }; flow.Children.AddRange(items); return flow;
    }

    // ------------------------------------------------------------------ the panels beside the parts
    /// <summary>Processor and graphics cards: everything not shown large, with the history of the main value.</summary>
    private void DeskDetails(HashSet<string> top)
    {
        var columns = new List<Control>();
        Control Column(string title, Color colour, string model, IEnumerable<AioReading> rows, AioReading? history)
        {
            var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*"), RowSpacing = 8 };
            var head = Stack(1, DeskTitle(title, colour));
            if (DeskModel(model) is { } m) head.Children.Add(m);
            grid.Children.Add(head);
            var list = rows.ToList();
            if (list.Count > 0) { var values = DeskCols(1, 100, list.Select(r => DeskKV(r))); Grid.SetRow(values, 1); grid.Children.Add(values); }
            if (history != null && AioReadings.Graphs(settings, history.Key))
            {
                var plot = HistoryPlot(history, 30, "area", colour); plot.Height = double.NaN;
                var fill = new DeskElastic { Min = 30, Child = plot }; Grid.SetRow(fill, 2); grid.Children.Add(fill);
            }
            return grid;
        }
        var cpu = Rs("CPU");
        if (cpu.Length > 0)
        {
            var main = R("temp") ?? R("load") ?? cpu[0];
            columns.Add(Column("CPU", NAcc(0), PartModel(WidgetKind.Cpu, 0, snapshot.Cpu?.Name ?? ""), Ordered(cpu.Where(r => !top.Contains(r.Key))), main));
        }
        int index = 0;
        foreach (var gpu in Rs("GPU").GroupBy(r => r.Device))
        {
            var rows = gpu.ToArray();
            var main = rows.FirstOrDefault(r => r.Key.EndsWith(".temp")) ?? rows.FirstOrDefault(r => r.Key.EndsWith(".load")) ?? rows[0];
            var info = snapshot.Gpus.FirstOrDefault(g => g.Id == gpu.Key);
            columns.Add(Column(GpuTitle(gpu.Key, index, true), NAcc(1), PartModel(WidgetKind.Gpu, index, info?.Name ?? ""), Ordered(rows.Where(r => !top.Contains(r.Key))), main));
            index++;
        }
        if (columns.Count > 0) deskParts["details"] = DeskCard(DeskRow(columns, 230, 28));
    }

    /// <summary>Memory: the percentage when it is not shown large, the amount, the other memory values and the history.</summary>
    private void DeskMemoryRest(HashSet<string> top)
    {
        var ram = R("ram", "System");
        var more = Rs("System").Where(r => r.Key.StartsWith("aio.system.ram.") && !AioReadings.IsGraphKey(r.Key)).ToArray();
        if (ram != null && !top.Contains(ram.Key)) { DeskMemory(top); return; }
        bool graph = ram != null && AioReadings.Graphs(settings, ram.Key);
        if (more.Length == 0 && !graph) return;
        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*"), RowSpacing = 10 };
        grid.Children.Add(DeskTitle(L.T("Memory"), NAcc(2)));
        if (more.Length > 0) { var list = DeskList(more); Grid.SetRow(list, 1); grid.Children.Add(list); }
        if (graph)
        {
            var plot = HistoryPlot(ram!, 30, "area", NAcc(2)); plot.Height = double.NaN;
            var fill = new DeskElastic { Min = 30, Child = plot }; Grid.SetRow(fill, 2); grid.Children.Add(fill);
        }
        deskParts["ram"] = DeskCard(grid);
    }

    /// <summary>The drives: name and model, the temperature when it is not shown large, read and write, space in use, history.</summary>
    private void DeskStorage(HashSet<string> top)
    {
        var drives = PartDrives();
        if (drives.Length == 0) return;
        var blocks = new List<Control>();
        foreach (var d in drives)
        {
            var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*"), RowSpacing = 6 };
            var name = NText(AioReadings.DriveName(snapshot, d.Id) + (DriveModel(d.Id) is { Length: > 0 } m ? "  " + m : ""), 13.5, nInk, true);
            name.TextTrimming = TextTrimming.CharacterEllipsis; TextFonts.Mark(name, TextRole.Title);
            grid.Children.Add(name);
            var used = d.Rows.FirstOrDefault(r => r.Key.EndsWith(".used"));
            var values = d.Rows.Where(r => r != used).ToList();
            if (d.Temp != null && !top.Contains(d.Temp.Key)) values.Insert(0, d.Temp);
            if (values.Count > 0) { var list = DeskCols(2, 140, values.Select(r => DeskKV(r))); Grid.SetRow(list, 1); grid.Children.Add(list); }
            if (used != null)
            {
                var meter = new AioMeter(NAcc(3), nTrack, 0) { Height = 6, VerticalAlignment = VerticalAlignment.Center }; meters.Add((used.Key, meter));
                var line = Ui.Columns("*,Auto", meter, NV(used, 14)); line.ColumnSpacing = 10; Grid.SetRow(line, 2); grid.Children.Add(line);
            }
            if (d.Temp != null && AioReadings.Graphs(settings, d.Temp.Key))
            {
                var plot = HistoryPlot(d.Temp, 24, "area", NAcc(3)); plot.Height = double.NaN;
                var fill = new DeskElastic { Min = 24, Child = plot }; Grid.SetRow(fill, 3); grid.Children.Add(fill);
            }
            blocks.Add(grid);
        }
        var body = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), RowSpacing = 10 };
        body.Children.Add(DeskTitle(L.T("Storage"), NAcc(3)));
        var flow = DeskRow(blocks, 240, 24); Grid.SetRow(flow, 1); body.Children.Add(flow);
        deskParts["storage"] = DeskCard(body);
    }

    /// <summary>Cores, memory, storage, network, fans and power supply beside the parts.</summary>
    private void DeskPanels(HashSet<string> top, bool details = true)
    {
        if (details) DeskDetails(top);
        DeskCores(top); DeskMemoryRest(top); DeskStorage(top); DeskNetwork(top); DeskFans(top); DeskRails(top);
    }

    // ------------------------------------------------------------------ the ten designs
    private (DeskGrid[] Wide, DeskGrid[] Tall) DeskLiving(HashSet<string> top)
    {
        var items = DeskItems(top);
        if (DeskFx) return DeskEffects(top, items);
        var big = items.Where(i => i.Kind is "cpu" or "gpu").ToList(); var small = items.Where(i => i.Kind is not ("cpu" or "gpu")).ToList();
        DeskGrid G(string columns, string rows, params string[] areas) => DeskGrid.Of(columns, rows, areas);
        switch (designStyle)
        {
            case WidgetStyle.AioDeskShowcase:
            {
                // LP-01: every part in a row, large and glowing; the details below.
                if (items.Count > 0) deskParts["parts"] = DeskRow(items.Select(i => DeskFigure(i, 46, DeskStage.Glow, 90)), 170, 10);
                DeskPanels(top);
                return (new[]
                {
                    G("1 1.35 1 1", "1.45 .55 .55 0", "parts parts parts parts", "cores details ram net", "cores details storage fans", "psu more more more"),
                    G("1 1 1 1", "1.2 .6 .5 0", "parts parts parts parts", "details details cores net", "ram storage fans psu", "more more more more"),
                }, new[]
                {
                    G("1 1", "1.2 .9 .7 .6 .5 0", "parts parts", "details details", "cores ram", "storage net", "fans psu", "more more"),
                });
            }
            case WidgetStyle.AioDeskPodiums:
            {
                // LP-02: the parts on glowing podiums with a beam of light in the colour of their temperature.
                if (items.Count > 0) deskParts["parts"] = DeskRow(items.Select(i => DeskFigure(i, 44, DeskStage.Podium, 110)), 170, 8);
                DeskPanels(top);
                return (new[]
                {
                    G("1 1 1 1.1", "1 1 .7 0", "parts parts parts cores", "parts parts parts details", "ram storage net fans", "psu more more more"),
                    G("1 1 1 1", "1.3 .7 .6 0", "parts parts parts parts", "cores details details net", "ram storage fans psu", "more more more more"),
                }, new[]
                {
                    G("1 1", "1.4 .9 .7 .6 .5 0", "parts parts", "details details", "cores ram", "storage net", "fans psu", "more more"),
                });
            }
            case WidgetStyle.AioDeskCards:
            {
                // LP-03: a card per part with its picture, the value large and the temperature scale.
                if (items.Count > 0) deskParts["cards"] = DeskRow(items.Select(DeskHeatCard), 280, 14);
                DeskPanels(top);
                return (new[]
                {
                    G("1 1 1 1.1", "1 1 .75 0", "cards cards cards details", "cards cards cards cores", "ram storage net fans", "psu more more more"),
                    G("1 1 1 1", "1.2 .7 .6 0", "cards cards cards cards", "details details cores net", "ram storage fans psu", "more more more more"),
                }, new[]
                {
                    G("1 1", "1.5 .9 .7 .6 .5 0", "cards cards", "details details", "cores ram", "storage net", "fans psu", "more more"),
                });
            }
            case WidgetStyle.AioDeskHero:
            {
                // LP-04: processor and graphics card large with all their values; memory and drives as tiles below.
                foreach (var i in big) top.Remove(i.R.Key);
                DeskCpu(top); DeskGpus(top);
                if (small.Count > 0) deskParts["parts"] = DeskRow(small.Select(i => DeskTile(i, 40)), 230, 14);
                DeskPanels(top, false);
                return (new[]
                {
                    G("1 1 1 1", "1.6 .8 .8 0", "cpu cpu gpu gpu", "parts parts parts cores", "ram storage net fans", "psu more more more"),
                    G("1 1 1 1", "1.4 .7 .7 0", "cpu cpu gpu gpu", "parts parts cores cores", "ram storage net fans", "psu more more more"),
                }, new[]
                {
                    G("1 1", "1.2 1.2 .7 .7 .6 .5 0", "cpu cpu", "gpu gpu", "parts parts", "cores ram", "storage net", "fans psu", "more more"),
                });
            }
            case WidgetStyle.AioDeskList:
            {
                // LP-05: one row per part: picture, value and its history across the row.
                if (items.Count > 0) deskParts["list"] = DeskRow(items.Select(i => DeskListRow(i, 110)), 0, 10, true);
                DeskPanels(top);
                return (new[]
                {
                    G("2.1 1 1", "1 1 .9 .8 0", "list cores cores", "list details details", "list ram net", "list storage fans", "psu more more"),
                    G("1.6 1 1", "1 1 .8 .8 0", "list details details", "list cores net", "list ram storage", "list fans psu", "more more more"),
                }, new[]
                {
                    G("1 1", "1.6 .9 .7 .6 .5 0", "list list", "details details", "cores ram", "storage net", "fans psu", "more more"),
                });
            }
            case WidgetStyle.AioDeskIsland:
            {
                // LP-06: the parts float freely in one large space, processor and graphics card in front.
                var island = new Grid { RowDefinitions = new RowDefinitions(small.Count > 0 && big.Count > 0 ? "1.25*,1*" : "*"), RowSpacing = 10 };
                if (big.Count > 0) island.Children.Add(DeskRow(big.Select(i => DeskFigure(i, 50, DeskStage.Glow, 110)), 220, 16));
                if (small.Count > 0) { var row = DeskRow(small.Select(i => DeskFigure(i, 38, DeskStage.Glow, 80)), 160, 12); Grid.SetRow(row, big.Count > 0 ? 1 : 0); island.Children.Add(row); }
                if (items.Count > 0) deskParts["island"] = new Border { Child = island, Padding = new Thickness(10), Background = DeskStageBrush(), CornerRadius = new CornerRadius(18) };
                DeskPanels(top);
                return (new[]
                {
                    G("1 1 1", "1 1 .8 .75 0", "island island details", "island island cores", "island island fans", "ram storage net", "psu more more"),
                    G("1 1 1", "1 .9 .7 0", "island island details", "island island cores", "ram storage net", "fans psu more"),
                }, new[]
                {
                    G("1 1", "1.6 .9 .7 .6 .5 0", "island island", "details details", "cores ram", "storage net", "fans psu", "more more"),
                });
            }
            case WidgetStyle.AioDeskBento:
            {
                // LP-07: bento tiles, processor and graphics card big, the others small.
                if (big.Count > 0) deskParts["big"] = DeskRow(big.Select(i => DeskBentoTile(i, true)), 300, 14);
                if (small.Count > 0) deskParts["small"] = DeskRow(small.Select(i => DeskBentoTile(i, false)), 180, 14);
                DeskPanels(top);
                return (new[]
                {
                    G("1 1 1 1 1.15", "1.5 1 .75 0", "big big big big details", "small small small cores net", "ram storage storage fans fans", "psu more more more more"),
                    G("1 1 1 1", "1.3 .9 .7 0", "big big big details", "small small cores net", "ram storage fans psu", "more more more more"),
                }, new[]
                {
                    G("1 1", "1.3 .9 .9 .7 .6 .5 0", "big big", "small small", "details details", "cores ram", "storage net", "fans psu", "more more"),
                });
            }
            case WidgetStyle.AioDeskCase:
            {
                // LP-08: the parts where they sit inside the case.
                if (items.Count > 0) deskParts["case"] = DeskCaseScene(items);
                DeskPanels(top);
                return (new[]
                {
                    G("1.15 1 1", "1 .8 .75 .6 0", "case details details", "case cores net", "case storage storage", "case ram fans", "psu more more"),
                    G("1 1 1", "1 .8 .7 0", "case details details", "case cores net", "case storage ram", "fans psu more"),
                }, new[]
                {
                    G("1 1", "1.7 .9 .7 .6 .5 0", "case case", "details details", "cores ram", "storage net", "fans psu", "more more"),
                });
            }
            case WidgetStyle.AioDeskHeatFrame:
            {
                // LP-09: the cores large in the middle, the parts around them.
                var left = items.Where(i => i.Kind is "cpu" or "ram").ToList(); var right = items.Where(i => i.Kind is "gpu" or "drive").ToList();
                if (left.Count > 0) deskParts["left"] = DeskRow(left.Select(i => DeskCard(DeskFigure(i, 40, DeskStage.Glow, 70), new Thickness(10))), 0, 14, true);
                if (right.Count > 0) deskParts["right"] = DeskRow(right.Select(i => DeskCard(DeskFigure(i, 40, DeskStage.Glow, 70), new Thickness(10))), 0, 14, true);
                DeskPanels(top);
                return (new[]
                {
                    G("1 2.1 1", "1 1 .75 .55 0", "left cores right", "left cores right", "details net storage", "ram fans psu", "more more more"),
                    G("1 1.6 1", "1 .9 .6 0", "left cores right", "left details right", "ram net storage", "fans psu more"),
                }, new[]
                {
                    G("1 1", "1.4 .9 .9 .7 .6 0", "cores cores", "left right", "details details", "ram net", "storage fans", "psu more"),
                });
            }
            default:
            {
                // LP-10 History Wall: processor and graphics cards with their history across the screen, the others as tiles.
                if (big.Count > 0) deskParts["list"] = DeskRow(big.Select(i => DeskListRow(i, 130)), 0, 14, true);
                if (small.Count > 0) deskParts["small"] = DeskRow(small.Select(DeskHistoryTile), 220, 14);
                DeskPanels(top);
                return (new[]
                {
                    G("2.4 1", "1.2 1.2 1 .8 .6 0", "list cores", "list net", "small details", "storage fans", "ram psu", "more more"),
                    G("2 1 1", "1.1 1.1 .9 .7 0", "list cores net", "list details details", "small small ram", "storage fans psu", "more more more"),
                }, new[]
                {
                    G("1 1", "1.4 .9 .9 .7 .6 .5 0", "list list", "small small", "details details", "cores ram", "storage net", "fans psu", "more more"),
                });
            }
        }
    }

    private IBrush DeskStageBrush() => new RadialGradientBrush
    {
        Center = new RelativePoint(.5, .45, RelativeUnit.Relative), GradientOrigin = new RelativePoint(.5, .45, RelativeUnit.Relative),
        RadiusX = new RelativeScalar(.7, RelativeUnit.Relative), RadiusY = new RelativeScalar(.7, RelativeUnit.Relative),
        GradientStops = { new(SeeThrough(Palette.Mix(nSurface, Color.Parse("#1A2733"), .7)), 0), new(SeeThrough(nSurface), 1) }
    };

    /// <summary>LP-03: a glass card in the colour of the part's temperature: caption and value left, picture right, scale below.</summary>
    private Control DeskHeatCard(DeskItem it)
    {
        var frame = new ArtHeatCard(nSurface); frame.Set(Celsius(it.R)); watchers.Add((it.R.Key, row => frame.Set(Celsius(row))));
        var scale = new ArtHeatScale { Margin = new Thickness(18, 0, 18, 14), VerticalAlignment = VerticalAlignment.Bottom };
        scale.Set(Celsius(it.R)); watchers.Add((it.R.Key, row => scale.Set(Celsius(row))));
        var text = Stack(2, DeskCaption(it.Caption, nLabel));
        text.Children.Add(FitLeft(GValue(it.R, 54)));
        if (DeskUnder(it) is { } u) { u.TextTrimming = TextTrimming.CharacterEllipsis; text.Children.Add(u); }
        text.VerticalAlignment = VerticalAlignment.Center; text.Margin = new Thickness(18, 12, 0, 12);
        var row = Ui.Columns("1*,1.1*", text, DeskStagePicture(it, DeskStage.Glow, 80));
        var face = new Grid { RowDefinitions = new RowDefinitions("*,Auto"), Children = { row, scale } };
        Grid.SetRow(scale, 1);
        return new Grid { Children = { frame, face } };
    }

    /// <summary>LP-05 and LP-10: picture, caption, value and model, and the part's history across the row.</summary>
    private Control DeskListRow(DeskItem it, double pictureWidth)
    {
        var text = Stack(2, DeskCaption(it.Caption, it.Colour));
        text.Children.Add(FitLeft(GValue(it.R, 44)));
        if (DeskUnder(it) is { } u) { u.TextTrimming = TextTrimming.CharacterEllipsis; text.Children.Add(u); }
        text.VerticalAlignment = VerticalAlignment.Center;
        // Shares, not fixed widths: the row also fits a narrow sidebar.
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(pictureWidth > 120 ? "1.2*,1.6*,4*" : "1*,1.6*,4*"), ColumnSpacing = 14, MaxHeight = double.PositiveInfinity };
        grid.Children.Add(DeskStagePicture(it, DeskStage.Glow, 60));
        Grid.SetColumn(text, 1); grid.Children.Add(text);
        if (AioReadings.Graphs(settings, it.R.Key))
        {
            var plot = HistoryPlot(it.R, 40, "area", it.Colour); plot.Height = double.NaN;
            var fill = new DeskElastic { Min = 40, Child = plot }; Grid.SetColumn(fill, 2); grid.Children.Add(fill);
        }
        return DeskCard(grid, new Thickness(14, 10, 18, 10));
    }

    /// <summary>LP-10: a smaller part with its value on top and its history below.</summary>
    private Control DeskHistoryTile(DeskItem it)
    {
        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), RowSpacing = 8 };
        grid.Children.Add(DeskTile(it, 34, false));
        if (AioReadings.Graphs(settings, it.R.Key))
        {
            var plot = HistoryPlot(it.R, 30, "area", it.Colour); plot.Height = double.NaN;
            var fill = new DeskElastic { Min = 30, Child = plot }; Grid.SetRow(fill, 1); grid.Children.Add(fill);
        }
        return DeskCard(grid, new Thickness(14, 10, 14, 12));
    }

    /// <summary>LP-07: a bento tile: caption, value and model at the top left, the picture large below.</summary>
    private Control DeskBentoTile(DeskItem it, bool large)
    {
        var text = Stack(1, DeskCaption(it.Caption + (large && it.Model.Length > 0 ? " · " + it.Model : ""), nLabel, 12));
        text.Children.Add(FitLeft(GValue(it.R, large ? 60 : 42)));
        if (!large && DeskUnder(it) is { } u) { u.TextTrimming = TextTrimming.CharacterEllipsis; text.Children.Add(u); }
        else if (large && it.Under != null) text.Children.Add(it.Under);
        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), RowSpacing = 4, Children = { text } };
        var art = DeskStagePicture(it, DeskStage.Glow, large ? 110 : 70); Grid.SetRow(art, 1); grid.Children.Add(art);
        return DeskCard(grid, new Thickness(18, 14, 18, 14));
    }

    /// <summary>LP-08: a glass case with the parts where they sit: processor and memory at the top, graphics in the middle, drives below.</summary>
    private Control DeskCaseScene(List<DeskItem> items)
    {
        var scene = new DeskPlaced { MinHeight = 420 };
        scene.Children.Add(new ArtDeskCase(NA));
        DeskPlaced.SetSpot(scene.Children[0], new Rect(0, 0, 1, 1));
        Control Small(DeskItem i)
        {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("1.2*,1*"), ColumnSpacing = 8 };
            grid.Children.Add(DeskStagePicture(i, DeskStage.Glow, 50));
            var text = Stack(0, DeskCaption(i.Caption, i.Colour, 12), FitLeft(GValue(i.R, 32)));
            if (i.Under != null) text.Children.Add(i.Under);
            text.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(text, 1); grid.Children.Add(text);
            return grid;
        }
        void Place(IEnumerable<DeskItem> group, Rect spot, double minItem)
        {
            var list = group.ToList(); if (list.Count == 0) return;
            var flow = DeskRow(list.Select(Small), minItem, 10, list.Count > 1 && spot.Width < .5);
            DeskPlaced.SetSpot(flow, spot); scene.Children.Add(flow);
        }
        Place(items.Where(i => i.Kind == "cpu"), new Rect(.07, .06, .5, .24), 200);
        Place(items.Where(i => i.Kind == "ram"), new Rect(.6, .06, .34, .24), 160);
        Place(items.Where(i => i.Kind == "gpu"), new Rect(.07, .34, .87, .3), 200);
        Place(items.Where(i => i.Kind == "drive"), new Rect(.07, .68, .87, .26), 170);
        return scene;
    }
}

/// <summary>Parts placed at fixed shares of the space (the inside of a case). The space is never smaller than its parts need.</summary>
public sealed class DeskPlaced : Panel
{
    public static readonly AttachedProperty<Rect> SpotProperty = AvaloniaProperty.RegisterAttached<DeskPlaced, Control, Rect>("Spot");
    public static void SetSpot(Control c, Rect r) => c.SetValue(SpotProperty, r);
    public static Rect GetSpot(Control c) => c.GetValue(SpotProperty);
    protected override Size MeasureOverride(Size available)
    {
        double w = double.IsFinite(available.Width) ? available.Width : 600, h = MinHeight;
        foreach (var c in Children)
        {
            var s = GetSpot(c);
            c.Measure(new Size(w * s.Width, double.PositiveInfinity));
            if (s.Height > 0 && s.Height < 1) h = Math.Max(h, c.DesiredSize.Height / s.Height);
        }
        return new Size(w, h);
    }
    protected override Size ArrangeOverride(Size final)
    {
        foreach (var c in Children) { var s = GetSpot(c); c.Arrange(new Rect(final.Width * s.X, final.Height * s.Y, final.Width * s.Width, final.Height * s.Height)); }
        return final;
    }
}

/// <summary>Podium Hall: a beam of light rising from the podium in the colour of the part's temperature.</summary>
public sealed class ArtDeskBeam : ArtDecoration
{
    private Color heat = HeatColour.Of(null);
    public ArtDeskBeam() : base(Colors.White) { }
    public void Set(double? celsius) { heat = HeatColour.Of(celsius); InvalidateVisual(); }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 20 || h < 20) return;
        double bottom = h * .9, half = w * .36, topHalf = w * .22;
        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 1, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            GradientStops = { new(BlackDraw.A(heat, .34), 0), new(BlackDraw.A(heat, .1), .6), new(BlackDraw.A(heat, 0), 1) }
        };
        var g = new StreamGeometry();
        using (var x = g.Open())
        {
            x.BeginFigure(new Point(w / 2 - half, bottom), true); x.LineTo(new Point(w / 2 + half, bottom));
            x.LineTo(new Point(w / 2 + topHalf, 0)); x.LineTo(new Point(w / 2 - topHalf, 0)); x.EndFigure(true);
        }
        c.DrawGeometry(brush, null, g);
    }
}

/// <summary>Inside the Case: the outline of a tower with a glass side, its fans and a soft light inside.</summary>
public sealed class ArtDeskCase : ArtDecoration
{
    public ArtDeskCase(Color accent) : base(accent) { }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 60 || h < 60) return;
        var r = new Rect(1, 1, w - 2, h - 2);
        var fill = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = { new(Color.FromArgb(255, 16, 23, 31), 0), new(Color.FromArgb(255, 8, 11, 15), 1) }
        };
        c.DrawRectangle(fill, new Pen(new SolidColorBrush(Color.FromArgb(255, 52, 68, 84)), 2), r, 18, 18);
        var glass = new Rect(w * .035, h * .03, w * .93, h * .94);
        c.DrawRectangle(new SolidColorBrush(BlackDraw.A(Color, .04)), new Pen(new SolidColorBrush(BlackDraw.A(Color, .35)), 1), glass, 12, 12);
        var light = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = { new(BlackDraw.A(Color, .5), 0), new(BlackDraw.A(Color, 0), 1) }
        };
        c.DrawRectangle(light, null, new Rect(glass.X + 14, glass.Y + 4, glass.Width - 28, 3), 2, 2);
    }
}
