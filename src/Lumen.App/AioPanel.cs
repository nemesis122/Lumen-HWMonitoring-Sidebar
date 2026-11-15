using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

/// <summary>
/// The Panel designs: a dark sensor panel with drawn hardware pictures on the left and red labels, grid graphs and
/// values on the right. "Panel" puts the gauges (temperatures and loads) in a block at the top; "Panel Rings",
/// "Panel Badges" and "Panel Header" put them into the section of their hardware instead. Every value is bound to its
/// AIO sensor, so the sensor choices, the widget switches and "only what the hardware has" work as in every AIO design,
/// and no value is shown twice.
/// </summary>
public sealed partial class AioTile
{
    private readonly List<(string Key, Action<AioReading?> Set)> panelLive = new();
    private readonly List<(string Key, PanelGraph Graph)> panelPlots = new();
    private PanelCoreCells? panelCores;
    private PanelTheme panel = PanelTheme.For("red");

    private bool PanelPlain => style == WidgetStyle.AioPanelPlain;

    private void PanelDesign()
    {
        drivesPlaced = true; coreBars = null; mainboardCores = null; panelCores = null;
        panelLive.Clear(); panelPlots.Clear();
        panel = PanelTheme.For(settings.PanelTheme, Color.TryParse(slot?.Accent, out var own) ? own : null);
        Padding = new Thickness(6); CornerRadius = new CornerRadius(Math.Max(6, panel.Radius + 4)); content.Spacing = 7;
        // A background the user picked for the sidebar stays the card's background (as in every AIO design); the
        // sections then use it too, so the panel follows the picked colour.
        if (palette.OwnBackground) panel = panel with { Section = back, Side = back };
        else Background = Palette.Brush(SeeThrough(panel.Side));
        BorderBrush = Palette.Brush(panel.Border);

        var gaugeKeys = new HashSet<string>();
        if (style == WidgetStyle.AioPanel) PanelGaugeBlock(gaugeKeys);

        // CPU
        var cpu = Rs("CPU");
        if (cpu.Length > 0)
        {
            var owner = source.Widgets.FirstOrDefault(w => w.Kind == WidgetKind.Cpu);
            var temp = R("temp"); var load = R("load");
            var body = Stack(6);
            if (lastCoreLoads.Count >= 2 && AioReadings.Selected(settings, "aio.cpu.coreloads"))
            {
                panelCores = new PanelCoreCells { Theme = panel, Loads = lastCoreLoads };
                body.Children.Add(Stack(3, PanelLabel("USAGE"), panelCores));
            }
            var values = cpu.Where(r => (PanelPlain || (r != temp && r != load)) && !gaugeKeys.Contains(r.Key)).OrderBy(r => r == temp ? 0 : r == load ? 1 : 2).Select(r => PanelValue(r)).ToList();
            var graph = temp != null ? PanelPlot(temp, "TEMPERATURE") : load != null ? PanelPlot(load, "LOAD") : null;
            body.Children.Add(PanelGraphAndValues(graph, values));
            PanelSection("cpu", PanelName(WidgetKind.Cpu, 0, snapshot.Cpu?.Name ?? "", "CPU"), Naming.Title(owner, 0, "CPU"), body, temp, load);
        }

        // GPUs (the main card; further cards only when ticked, as in every AIO design)
        int index = 0;
        foreach (var gpu in Rs("GPU").GroupBy(r => r.Device))
        {
            var rows = gpu.ToArray();
            var owner = source.Widgets.FirstOrDefault(w => w.Kind == WidgetKind.Gpu);
            var temp = rows.FirstOrDefault(r => r.Key.EndsWith(".temp")); var load = rows.FirstOrDefault(r => r.Key.EndsWith(".load"));
            var power = rows.FirstOrDefault(r => r.Key.EndsWith(".power"));
            var graphs = new List<Control>();
            if (load != null && PanelPlot(load, "CORE") is { } g1) graphs.Add(g1);
            if (temp != null && PanelPlot(temp, "TEMP") is { } g2) graphs.Add(g2);
            if (power != null && PanelPlot(power, "POWER") is { } g3) graphs.Add(g3);
            var body = Stack(6);
            if (graphs.Count > 0) body.Children.Add(FixedCols(graphs.Count, graphs.ToArray()));
            var values = rows.Where(r => (PanelPlain || (r != temp && r != load)) && !gaugeKeys.Contains(r.Key)).OrderBy(r => r == temp ? 0 : r == load ? 1 : 2).Select(r => PanelValue(r)).ToArray();
            if (values.Length > 0) body.Children.Add(PanelValueGrid(values));
            string fallback = "GPU" + (index == 0 ? "" : " " + (index + 1));
            PanelSection("gpu", PanelName(WidgetKind.Gpu, index, snapshot.Gpus.FirstOrDefault(g => g.Id == gpu.Key)?.Name ?? "", fallback),
                Naming.Title(owner, index, fallback), body, temp, load);
            index++;
        }

        // Memory
        if (R("ram", "System") is { } ram)
        {
            var amount = PanelText(Amount(ram), 13, ink, true); formats[amount] = Amount;
            if (!fields.TryGetValue(ram.Key, out var list)) fields[ram.Key] = list = new(); list.Add(amount);
            var bar = PanelBarFor(ram);
            Control used = PanelText("USED MEMORY", 8.5, muted);
            if (PanelPlain)
            {
                // Panel Plain has no gauge, so the load is written next to the caption.
                var percent = PanelV(ram, 8.5, ink); formats[percent] = r => Format.Percent(r.Number); percent.Text = Format.Percent(ram.Number);
                used = Ui.Columns("*,Auto", used, percent);
            }
            var body = Stack(5, PanelLabel("MEMORY"), FitLeft(amount), Stack(3, used, bar));
            if (PanelPlot(ram, "USAGE") is { } plot) body.Children.Add(plot);
            // The load itself is the gauge (top block, ring, badge or header).
            PanelSection("ram", "MEMORY", "RAM", body, null, ram);
        }

        // Drives: name, temperature, used space and transfer rates
        var drives = PartDrives();
        if (drives.Length > 0)
        {
            var body = Stack(8);
            foreach (var drive in drives)
            {
                var name = PanelText(AioReadings.DriveName(snapshot, drive.Id), 10.5, ink);
                Control head = drive.Temp != null ? Ui.Columns("*,Auto", name, PanelV(drive.Temp, 11, panel.Accent)) : name;
                var block = Stack(3, head);
                if (drive.Rows.FirstOrDefault(r => r.Key.EndsWith(".used")) is { } used)
                    block.Children.Add(Ui.Columns("*,Auto", PanelBarFor(used), new Border { Margin = new Thickness(6, 0, 0, 0), Child = PanelV(used, 9.5, ink) }));
                var rates = drive.Rows.Where(r => !r.Key.EndsWith(".used")).Select(r => PanelValue(r, r.Key.EndsWith(".read") ? "READ" : "WRITE", 10)).ToArray();
                if (rates.Length > 0) block.Children.Add(FixedCols(rates.Length, rates));
                body.Children.Add(block);
            }
            PanelSection("ssd", "STORAGE", "DRIVES", body, null, null);
        }

        // Cooling: every fan that exists, with its speed in an RPM field
        var fans = Rs("Cooling");
        if (fans.Length > 0)
        {
            var body = Stack(5, fans.Select(f =>
            {
                var value = PanelV(f, 11, ink); formats[value] = row => row.Number.HasValue ? Format.Number(row.Number) : "—"; value.Text = formats[value](f);
                value.TextAlignment = TextAlignment.Right;
                var field = new Border
                {
                    Background = Palette.Brush(SeeThrough(Color.Parse("#2A2A2D"))), Padding = new Thickness(6, 2),
                    Child = Ui.Columns("Auto,*", PanelText("RPM", 8, Color.Parse("#A9A9AE")), FitRight(value))
                };
                var label = PanelText(Translate(L.T(f.Label)).ToUpperInvariant(), 9, panel.Accent); label.TextTrimming = TextTrimming.CharacterEllipsis; label.TextWrapping = TextWrapping.NoWrap;
                var line = Ui.Columns("*,1.05*", label, field); line.ColumnSpacing = 6; return (Control)line;
            }).ToArray());
            PanelSection("aio", "COOLING", "FANS", body, null, null);
        }

        // Power supply rails
        var rails = Rs("Rails");
        if (rails.Length > 0)
            PanelSection("psu", "POWER SUPPLY", "PSU", Stack(4, rails.Select(r => PanelLine(r.Label, r)).ToArray()), null, null);

        // Network: download and upload graphs, link speed
        var net = Rs("Network");
        if (net.Length > 0)
        {
            var body = Stack(5);
            var down = net.FirstOrDefault(r => r.Key.EndsWith(".down")); var up = net.FirstOrDefault(r => r.Key.EndsWith(".up"));
            var cells = new List<Control>();
            foreach (var (r, label) in new[] { (down, "DL"), (up, "UP") })
                if (r != null) cells.Add(Stack(3, PanelPlot(r, null) ?? new Border(), PanelValue(r, label, 11)));
            if (cells.Count > 0) body.Children.Add(FixedCols(cells.Count, cells.ToArray()));
            if (net.FirstOrDefault(r => r.Key.EndsWith(".link")) is { } link) body.Children.Add(PanelLine("LINK", link));
            PanelSection("net", "CONNECTION", "NETWORK", body, null, null);
        }

        // Core temperatures, when ticked
        var cores = Rs("Cores");
        if (cores.Length > 0)
            PanelSection("cpu", "CORE TEMP", "CORES", PanelValueGrid(cores.Select(r => PanelValue(r, r.Label.Replace("Core", "C").ToUpperInvariant(), 10)).ToArray(), 4), null, null);

        // Anything else the user ticked (a further sensor) keeps its value in the panel's own frame.
        var extras = referenceRows.Where(r => !referenceUsed.Contains(r.Key) && !gaugeKeys.Contains(r.Key)).ToArray();
        if (extras.Length > 0) PanelSection("cpu", "MORE", "MORE", PanelValueGrid(extras.Select(r => PanelValue(r)).ToArray()), null, null);
        foreach (var r in referenceRows) referenceUsed.Add(r.Key);
    }

    // ---------------------------------------------------------------- the gauge block of "Panel"
    private void PanelGaugeBlock(HashSet<string> keys)
    {
        string mode = AioDesigns.PanelGauges.Any(g => g.Key == settings.PanelGauges) ? settings.PanelGauges : "dots";
        var gpuId = Rs("GPU").Select(r => r.Device).FirstOrDefault();
        var items = new List<(AioReading R, string Label)>();
        void Add(AioReading? r, string label) { if (r != null && !keys.Contains(r.Key)) { items.Add((r, label)); keys.Add(r.Key); } }
        if (mode == "twin")
        {
            // Double rings: temperature outside, load inside, one ring per part.
            var cells = new List<Control>();
            void Twin(AioReading? temp, AioReading? load, string label)
            {
                if (temp == null && load == null) return;
                if (temp != null) keys.Add(temp.Key); if (load != null) keys.Add(load.Key);
                cells.Add(PanelTwin(temp, load, label, 96));
            }
            Twin(R("temp"), R("load"), "CPU");
            Twin(gpuId == null ? null : R("temp", "GPU", gpuId), gpuId == null ? null : R("load", "GPU", gpuId), "GPU");
            Twin(null, R("ram", "System"), "RAM");
            if (cells.Count > 0) PanelBlock(new UniformGrid { Columns = Math.Min(3, cells.Count), Children = { } }.With(cells));
            return;
        }
        Add(R("temp"), "CPU");
        if (gpuId != null) { Add(R("temp", "GPU", gpuId), "GPU"); Add(R("hotspot", "GPU", gpuId), "HOT"); }
        Add(R("load"), "CPU"); Add(R("ram", "System"), "RAM");
        if (gpuId != null) Add(R("load", "GPU", gpuId), "GPU");
        if (items.Count == 0) return;
        Control block = mode switch
        {
            "leds" => Stack(6, items.Select(i => PanelLedRow(i.R, i.Label)).ToArray()),
            "columns" or "tubes" => new UniformGrid { Columns = items.Count }.With(items.Select(i => PanelColumn(i.R, i.Label, mode))),
            "strip" => new UniformGrid { Columns = items.Count }.With(items.Select(i => PanelRingCell(i.R, i.Label, "strip", 52))),
            "grid" => new UniformGrid { Columns = 3 }.With(items.Select(i => PanelGridCell(i.R, i.Label))),
            "tiles" => new UniformGrid { Columns = 3 }.With(items.Select(i => PanelTile(i.R, i.Label))),
            _ => new UniformGrid { Columns = 3 }.With(items.Select(i => PanelRingCell(i.R, i.Label, mode, 92)))
        };
        PanelBlock(block);
    }

    private void PanelBlock(Control block)
    {
        var frame = PanelFrame(new Border { Padding = new Thickness(8, 10), Child = block });
        content.Children.Add(frame);
    }

    // ---------------------------------------------------------------- sections
    private void PanelSection(string art, string name, string label, Control body, AioReading? temp, AioReading? load)
    {
        Control left;
        Control? header = null;
        var nameText = PanelText(name, 8.4, ink); nameText.TextAlignment = TextAlignment.Center; nameText.HorizontalAlignment = HorizontalAlignment.Center;
        bool gauge = style != WidgetStyle.AioPanel && !PanelPlain && (temp != null || load != null);
        if (gauge && style == WidgetStyle.AioPanelRings)
            left = Stack(4, PanelTwin(temp, load, null, 92), nameText);
        else if (gauge && style == WidgetStyle.AioPanelBadges)
        {
            var art2 = new PanelArt { Kind = art, Theme = panel, Margin = new Thickness(0, 12, 0, 0) };
            var badge = PanelBadge(load ?? temp!, load != null ? temp : null);
            left = Stack(4, new Grid { Children = { art2, badge } }, nameText);
        }
        else
        {
            left = Stack(4, new PanelArt { Kind = art, Theme = panel }, nameText);
            if (gauge && style == WidgetStyle.AioPanelHeader) header = PanelHeader(label, temp, load);
        }
        var columns = new Grid { ColumnDefinitions = new ColumnDefinitions(gauge && style is WidgetStyle.AioPanelRings or WidgetStyle.AioPanelBadges ? "0.31*,0.69*" : "0.25*,0.75*") };
        var leftBox = new Border
        {
            Child = new Border { VerticalAlignment = VerticalAlignment.Center, Child = left },
            BorderBrush = Palette.Brush(panel.Border), BorderThickness = new Thickness(0, 0, 1.5, 0), Padding = new Thickness(5, 7)
        };
        var right = new Border { Padding = new Thickness(8, 6), Child = body };
        Grid.SetColumn(right, 1);
        columns.Children.Add(leftBox); columns.Children.Add(right);
        Control inner = columns;
        if (header != null)
        {
            var stack = new StackPanel();
            stack.Children.Add(new Border { BorderBrush = Palette.Brush(panel.Border), BorderThickness = new Thickness(0, 0, 0, 1.5), Padding = new Thickness(8, 5), Child = header });
            stack.Children.Add(columns); inner = stack;
        }
        content.Children.Add(PanelFrame(inner));
    }

    /// <summary>The frame of a section in the chosen colour set (plain, carbon, gold, glow, glass, side stripe).</summary>
    private Control PanelFrame(Control child)
    {
        IBrush fill = panel.Surface switch
        {
            "carbon" => new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Absolute), EndPoint = new RelativePoint(6, 6, RelativeUnit.Absolute), SpreadMethod = GradientSpreadMethod.Repeat,
                GradientStops = { new(SeeThrough(Color.Parse("#141416")), 0), new(SeeThrough(Color.Parse("#141416")), .5), new(SeeThrough(Color.Parse("#1B1B1E")), .5), new(SeeThrough(Color.Parse("#1B1B1E")), 1) }
            },
            "gold" => new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = { new(SeeThrough(Color.Parse("#16130D")), 0), new(SeeThrough(Color.Parse("#0C0B08")), 1) }
            },
            _ => Palette.Brush(SeeThrough(panel.Section))
        };
        var border = new Border
        {
            Background = fill, BorderBrush = Palette.Brush(panel.Surface == "carbon" ? PanelDraw.A(panel.Accent, .45) : panel.Border),
            BorderThickness = panel.Stripe ? new Thickness(3, 1.5, 1.5, 1.5) : new Thickness(1.5), CornerRadius = new CornerRadius(panel.Radius),
            ClipToBounds = true, Child = child, Tag = "aio-section"
        };
        if (panel.Stripe) border.BorderBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Absolute), EndPoint = new RelativePoint(3.01, 0, RelativeUnit.Absolute),
            GradientStops = { new(panel.Accent, 0), new(panel.Accent, .99), new(panel.Border, 1) }
        };
        if (panel.Surface == "glow") border.BoxShadow = new BoxShadows(new BoxShadow { Blur = 14, Color = PanelDraw.A(panel.Accent, .2) });
        if (panel.Surface == "glass") border.BoxShadow = new BoxShadows(new BoxShadow { OffsetY = 1, Blur = 0, Spread = 0, IsInset = true, Color = Color.FromArgb(30, 255, 255, 255) });
        return border;
    }

    // ---------------------------------------------------------------- pieces
    /// <summary>Panel letters are drawn a fifth larger than the other designs so values stay readable in a narrow sidebar.</summary>
    private const double PanelBoost = 1.22;
    private TextBlock PanelText(string text, double size, Color color, bool number = false)
    {
        var t = Text(text, size * PanelBoost, color, number); t.FontFamily = PanelTheme.Font; t.FontWeight = FontWeight.Bold; return t;
    }
    private Control PanelLabel(string text) => FitLeft(PanelText(text, 9, panel.Accent));
    private TextBlock PanelV(AioReading r, double size, Color? color = null)
    {
        var t = V(r, size * PanelBoost, color ?? ink); t.FontFamily = PanelTheme.Font; t.FontWeight = FontWeight.Bold; return t;
    }
    private string PanelName(WidgetKind kind, int index, string model, string fallback)
    {
        string name = PartModel(kind, index, model);
        return name.Length > 0 ? name : fallback;
    }
    private static string PanelCaption(AioReading r) => r.Key switch
    {
        _ when r.Key.EndsWith(".junction") => "VRAM °C",
        _ when r.Key.EndsWith(".hotspot") => "HOTSPOT",
        _ when r.Key.EndsWith(".voltage") => r.Group == "CPU" ? "VCORE" : "VOLT",
        _ when r.Key.EndsWith(".power") => "POWER",
        _ when r.Key.EndsWith(".clock") => "CLOCK",
        _ when r.Key.EndsWith(".vram") => "VRAM",
        _ when r.Key.EndsWith(".temp") => "TEMP",
        _ when r.Key.EndsWith(".load") => "LOAD",
        _ => Translate(L.T(r.Label)).ToUpperInvariant()
    };
    /// <summary>A value with its caption below in the panel's small grey letters ("4.15 GHz" / CLOCK).</summary>
    private Control PanelValue(AioReading r, string? caption = null, double size = 12)
    {
        var value = PanelV(r, size, ink);
        var label = PanelText(caption ?? PanelCaption(r), 7.5, muted);
        return Stack(1, FitLeft(value), FitLeft(label));
    }
    private Control PanelValueGrid(Control[] values, int columns = 2) => FixedCols(Math.Min(columns, Math.Max(1, values.Length)), values);
    private Control PanelLine(string label, AioReading r)
    {
        var line = Ui.Columns("*,Auto", FitLeft(PanelText(label.ToUpperInvariant(), 9, muted)), PanelV(r, 11, ink)); line.ColumnSpacing = 6; return line;
    }
    private Control PanelGraphAndValues(Control? graph, List<Control> values)
    {
        if (graph == null) return PanelValueGrid(values.ToArray());
        if (values.Count == 0) return graph;
        var side = Stack(5, values.Take(3).ToArray());
        var row = Ui.Columns("1.35*,*", graph, side); row.ColumnSpacing = 8;
        if (values.Count <= 3) return row;
        return Stack(6, row, PanelValueGrid(values.Skip(3).ToArray()));
    }
    /// <summary>Label and grid graph of a reading's history; nothing when graphs are switched off.</summary>
    private Control? PanelPlot(AioReading r, string? label)
    {
        if (!settings.Shows("aio.graphs")) return null;
        var graph = new PanelGraph { Theme = panel, Height = 46, Tag = "graphic" };
        panelPlots.Add((r.Key, graph));
        return label == null ? graph : Stack(3, PanelLabel(label), graph);
    }
    private PanelBar PanelBarFor(AioReading r, bool leds = false)
    {
        var bar = new PanelBar { Theme = panel, Leds = leds, Height = leds ? 9 : 7, MinWidth = 30 };
        panelLive.Add((r.Key, row => bar.Set(row?.Fraction)));
        return bar;
    }
    private (TextBlock, Color)[] PanelFace(StackPanel face, params (TextBlock Text, Color Normal)[] texts)
    {
        foreach (var (t, _) in texts) { t.TextAlignment = TextAlignment.Center; t.HorizontalAlignment = HorizontalAlignment.Center; face.Children.Add(t); }
        return texts;
    }
    private void PanelGaugeLive(PanelGauge gauge, AioReading? first, AioReading? second, (TextBlock Text, Color Normal)[] faces)
    {
        void Refresh(AioReading? _)
        {
            gauge.Set(first == null ? null : readings.GetValueOrDefault(first.Key)?.Fraction, second == null ? null : readings.GetValueOrDefault(second.Key)?.Fraction);
            // On a gauge that fills completely when hot, its numbers turn white (dark on the light colour sets).
            bool onFill = gauge.Hot && gauge.Mode is "dots" or "strip" or "tiles";
            foreach (var (t, normal) in faces) t.Foreground = Palette.Brush(onFill ? (panel.DarkOnHot ? Color.Parse("#101010") : Colors.White) : normal);
        }
        panelLive.Add((first?.Key ?? second?.Key ?? "", Refresh));
    }
    private Control Squeeze(Control face, double margin) => new AioFit
    { Child = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Margin = new Thickness(margin), Child = new AioWidest { Child = face } } };

    /// <summary>A ring-like gauge (dots, segments, hexagon, rays, diamond, strip) with value and label inside.</summary>
    private Control PanelRingCell(AioReading r, string label, string mode, double size)
    {
        // Font sizes follow the design size, not the graphic size (the size settings scale them separately).
        bool large = size > 60;
        size *= settings.ActiveWidget?.Graphics ?? 1;
        var gauge = new PanelGauge { Mode = mode, Theme = panel, Width = size, Height = size, Tag = "graphic" };
        var value = PanelV(r, large ? 14 : 11, ink); var caption = PanelText(label, large ? 10 : 8, panel.Accent);
        var face = new StackPanel { Spacing = 0, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        PanelGaugeLive(gauge, r, null, PanelFace(face, (value, ink), (caption, panel.Accent)));
        return new Viewbox
        {
            MaxWidth = size + 8, MaxHeight = size + 8, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Margin = new Thickness(2),
            Child = new Grid { Width = size, Height = size, Children = { gauge, Squeeze(face, size * .24) } }
        };
    }
    /// <summary>Double ring: temperature outside, load inside (Panel Rings, and the "Double rings" gauge block).</summary>
    private Control PanelTwin(AioReading? temp, AioReading? load, string? label, double size)
    {
        size *= settings.ActiveWidget?.Graphics ?? 1;
        var gauge = new PanelGauge { Mode = "twin", Theme = panel, Width = size, Height = size, Tag = "graphic" };
        var face = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var texts = new List<(TextBlock, Color)>();
        if (temp != null) texts.Add((PanelV(temp, 13, panel.Accent), panel.Accent));
        if (load != null) texts.Add((PanelV(load, 12, ink), ink));
        PanelGaugeLive(gauge, temp, load, PanelFace(face, texts.ToArray()));
        var cell = new Grid { Width = size, Height = size, Children = { gauge, Squeeze(face, size * .21) } };
        Control result = new Viewbox { MaxWidth = size, MaxHeight = size, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Child = cell };
        if (label == null) return result;
        var caption = PanelText(label, 10, ink); caption.HorizontalAlignment = HorizontalAlignment.Center;
        return Stack(4, result, caption);
    }
    /// <summary>Panel Badges: a small dot ring on the corner of the hardware picture.</summary>
    private Control PanelBadge(AioReading main, AioReading? second)
    {
        double size = 50 * (settings.ActiveWidget?.Graphics ?? 1);
        var gauge = new PanelGauge { Mode = "dots", Theme = panel, Width = size, Height = size, Tag = "graphic" };
        var face = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var texts = new List<(TextBlock, Color)> { (PanelV(main, 10, ink), ink) };
        if (second != null) texts.Add((PanelV(second, 7, panel.Accent), panel.Accent));
        PanelGaugeLive(gauge, main, null, PanelFace(face, texts.ToArray()));
        // The badge never gets wider than the picture column (large sizes in a narrow sidebar).
        return new Viewbox
        {
            MaxWidth = size, MaxHeight = size, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
            Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly,
            Child = new Grid { Width = size, Height = size, Children = { gauge, Squeeze(face, size * .2) } }
        };
    }
    /// <summary>Panel Header: the part's name with temperature and load as LED strips across the top of its section.</summary>
    private Control PanelHeader(string label, AioReading? temp, AioReading? load)
    {
        var cells = new List<Control> { PanelText(label.ToUpperInvariant(), 11, panel.Accent) };
        string columns = "Auto";
        foreach (var (r, caption) in new[] { (temp, "TEMP"), (load, "LOAD") })
        {
            if (r == null) continue;
            var line = Ui.Columns("Auto,*,Auto", PanelText(caption, 7, muted), PanelBarFor(r, true), PanelV(r, 9.5, ink)); line.ColumnSpacing = 5;
            cells.Add(line); columns += ",*";
        }
        var row = Ui.Columns(columns, cells.ToArray()); row.ColumnSpacing = 10; return row;
    }
    private Control PanelLedRow(AioReading r, string label)
    {
        var gauge = new PanelGauge { Mode = "leds", Theme = panel, Height = 16, Tag = "graphic" };
        var value = PanelV(r, 11, ink); value.TextAlignment = TextAlignment.Right;
        PanelGaugeLive(gauge, r, null, Array.Empty<(TextBlock, Color)>());
        var row = Ui.Columns("40,*,64", PanelText(label, 10, panel.Accent), gauge, FitRight(value)); row.ColumnSpacing = 6; return row;
    }
    private Control PanelColumn(AioReading r, string label, string mode)
    {
        double height = 130 * (settings.ActiveWidget?.Graphics ?? 1);
        var gauge = new PanelGauge { Mode = mode, Theme = panel, Height = height, Width = 34, Tag = "graphic", HorizontalAlignment = HorizontalAlignment.Center };
        PanelGaugeLive(gauge, r, null, Array.Empty<(TextBlock, Color)>());
        var value = PanelV(r, 10, ink); value.HorizontalAlignment = HorizontalAlignment.Center;
        var caption = PanelText(label, 8.5, panel.Accent); caption.HorizontalAlignment = HorizontalAlignment.Center;
        return Stack(4, gauge, Fit(value), caption);
    }
    private Control PanelGridCell(AioReading r, string label)
    {
        double size = 58 * (settings.ActiveWidget?.Graphics ?? 1);
        var gauge = new PanelGauge { Mode = "grid", Theme = panel, Width = size, Height = size, Tag = "graphic" };
        PanelGaugeLive(gauge, r, null, Array.Empty<(TextBlock, Color)>());
        var row = Ui.Columns("Auto,*", gauge, Stack(2, FitLeft(PanelV(r, 12, ink)), PanelText(label, 9, panel.Accent))); row.ColumnSpacing = 6;
        return new Border { Margin = new Thickness(2, 4), Child = row };
    }
    private Control PanelTile(AioReading r, string label)
    {
        var gauge = new PanelGauge { Mode = "tiles", Theme = panel, Tag = "graphic" };
        var caption = PanelText(label, 9, panel.Accent);
        var value = PanelV(r, 20, ink);
        PanelGaugeLive(gauge, r, null, new[] { (caption, panel.Accent), (value, ink) });
        var face = Stack(6, caption, FitLeft(value)); face.Margin = new Thickness(9, 8, 9, 16);
        return new Grid { Margin = new Thickness(3), MinHeight = 74, Children = { gauge, face } };
    }

    private void UpdatePanel(History h)
    {
        foreach (var (key, set) in panelLive) set(readings.GetValueOrDefault(key));
        foreach (var (key, graph) in panelPlots)
        {
            var r = readings.GetValueOrDefault(key);
            var series = r != null && r.History.Length > 0 ? h.Read(r.History) : samples.Read(key);
            if (series.Length == 0) series = samples.Read(key);
            graph.Set(series, r?.Maximum ?? 100);
        }
        if (panelCores != null) panelCores.Loads = lastCoreLoads;
    }
}

internal static class PanelLayoutExtensions
{
    public static T With<T>(this T panel, IEnumerable<Control> children) where T : Panel
    {
        foreach (var child in children) panel.Children.Add(child);
        return panel;
    }
}
