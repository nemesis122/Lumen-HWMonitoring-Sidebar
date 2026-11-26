using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

/// <summary>
/// 5.8.0: the ten effect desktop designs (drafts FX-01 … FX-10): command deck, exploded view, thermal camera, halo grid,
/// spotlight, glass showroom, flow map, telemetry, tower cutaway and keynote. Built from the same parts and panels as the
/// Living PC designs; only the way the parts are shown differs.
/// </summary>
public sealed partial class AioTile
{
    private bool DeskFx => designStyle is WidgetStyle.AioDeskCommand or WidgetStyle.AioDeskExploded or WidgetStyle.AioDeskThermal or WidgetStyle.AioDeskHalo
        or WidgetStyle.AioDeskSpotlight or WidgetStyle.AioDeskShowroom or WidgetStyle.AioDeskFlow or WidgetStyle.AioDeskTelemetry
        or WidgetStyle.AioDeskTower or WidgetStyle.AioDeskKeynote;

    /// <summary>A part inside a halo ring that fills with its value, coloured by its temperature.</summary>
    private Control DeskHaloFigure(DeskItem it, double big, double min)
    {
        var halo = new ArtDeskHalo(nTrack);
        void Set(AioReading? row) => halo.Set(row?.Fraction, HeatColour.Of(Celsius(row)));
        Set(it.R); watchers.Add((it.R.Key, Set));
        var picture = DeskArt(it.Art); picture.Margin = new Thickness(0);
        var inner = new DeskSquare { Share = .5, Child = picture };
        var stage = new DeskElastic { Min = min, Child = new Grid { Children = { halo, inner } } };
        var grid = new Grid { RowDefinitions = new RowDefinitions("*,Auto,Auto,Auto"), Children = { stage } };
        var value = Fit(GValue(it.R, big)); value.HorizontalAlignment = HorizontalAlignment.Center; Grid.SetRow(value, 1); grid.Children.Add(value);
        var caption = DeskCaption(it.Caption, it.Colour); caption.HorizontalAlignment = HorizontalAlignment.Center; Grid.SetRow(caption, 2); grid.Children.Add(caption);
        if (DeskUnder(it) is { } u) { u.HorizontalAlignment = HorizontalAlignment.Center; u.TextTrimming = TextTrimming.CharacterEllipsis; Grid.SetRow(u, 3); grid.Children.Add(u); }
        return grid;
    }

    /// <summary>FX-03: a tile seen through a thermal camera: the part glowing in the iron palette, spot label, crosshair and value.</summary>
    private Control DeskThermalTile(DeskItem it, int spot, double big)
    {
        var tile = new Border { CornerRadius = new CornerRadius(14), ClipToBounds = true, BorderThickness = new Thickness(1), BorderBrush = Palette.Brush(Color.Parse("#2A2140")) };
        double scale = it.R.Key.EndsWith(".temp") || it.R.Key.StartsWith("aio.system.disk.") ? 1 : 0;
        void Paint(AioReading? row) => tile.Background = HeatBrush(row?.Number is double n ? (scale == 1 ? (n - 20) / 65 : n / 100) : null);
        Paint(it.R); watchers.Add((it.R.Key, Paint));
        var label = NText($"SPOT {spot} · {it.Caption}".ToUpperInvariant(), 12, nInk, true); label.LetterSpacing = 1.4; label.TextTrimming = TextTrimming.CharacterEllipsis;
        label.Effect = new DropShadowEffect { BlurRadius = 4, OffsetX = 0, OffsetY = 0, Color = Color.FromArgb(200, 0, 0, 0) };
        var picture = DeskArt(it.Art); picture.Opacity = .82; picture.Margin = new Thickness(18, 6);
        var value = FitRight(GValue(it.R, big)); value.Effect = new DropShadowEffect { BlurRadius = 6, OffsetX = 0, OffsetY = 0, Color = Color.FromArgb(220, 0, 0, 0) };
        var cross = new ArtCrosshair(Colors.White) { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var face = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), Margin = new Thickness(14, 10, 14, 10) };
        face.Children.Add(label);
        var stage = new DeskElastic { Min = 60, Child = new Grid { Children = { picture, cross } } }; Grid.SetRow(stage, 1); face.Children.Add(stage);
        Grid.SetRow(value, 2); face.Children.Add(value);
        tile.Child = face;
        return tile;
    }

    /// <summary>FX-06: a lit glass case with the part inside and a plaque with its value below.</summary>
    private Control DeskVitrine(DeskItem it)
    {
        var box = new Grid { Children = { new ArtDeskVitrine(), DeskStagePicture(it, DeskStage.Glow, 90) } };
        var plaque = Stack(1, FitLeft(GValue(it.R, 36)), DeskCaption(it.Caption, it.Colour, 12));
        if (DeskUnder(it) is { } u) { u.TextTrimming = TextTrimming.CharacterEllipsis; plaque.Children.Add(u); }
        var grid = new Grid { RowDefinitions = new RowDefinitions("*,Auto"), RowSpacing = 8, Children = { box } };
        var card = DeskCard(plaque, new Thickness(14, 8, 14, 10)); Grid.SetRow(card, 1); grid.Children.Add(card);
        return grid;
    }

    /// <summary>FX-07: a round node with the part, its value and caption beside the bus lines.</summary>
    private Control DeskNode(DeskItem it, double big)
    {
        var ring = new ArtDeskNode(); ring.Set(Celsius(it.R)); watchers.Add((it.R.Key, row => ring.Set(Celsius(row))));
        var picture = DeskArt(it.Art);
        var disc = new DeskElastic { Min = 56, Child = new Grid { Children = { ring, new DeskSquare { Share = .62, Child = picture } } } };
        var grid = new Grid { RowDefinitions = new RowDefinitions("*,Auto,Auto"), Children = { disc } };
        var value = Fit(GValue(it.R, big)); value.HorizontalAlignment = HorizontalAlignment.Center; Grid.SetRow(value, 1); grid.Children.Add(value);
        var caption = DeskCaption(it.Caption, it.Colour, 12); caption.HorizontalAlignment = HorizontalAlignment.Center; Grid.SetRow(caption, 2); grid.Children.Add(caption);
        return grid;
    }

    /// <summary>FX-02: the parts drawn apart, each with a label card joined by a dashed line.</summary>
    private Control DeskExplodedScene(List<DeskItem> items)
    {
        var scene = new DeskExploded();
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            var label = Stack(0, DeskCaption(it.Caption, it.Colour, 12), FitLeft(GValue(it.R, 34)));
            if (DeskUnder(it) is { } u) { u.TextTrimming = TextTrimming.CharacterEllipsis; label.Children.Add(u); }
            var card = DeskCard(label, new Thickness(14, 8, 14, 10));
            scene.Add(DeskStagePicture(it, DeskStage.Glow, 40), card, it.Colour);
        }
        return scene;
    }

    /// <summary>FX-08: the history of every part on one 0–100 scale, with the parts and their values below.</summary>
    private Control DeskTelemetry(List<DeskItem> items)
    {
        var lines = new Grid { Children = { new ArtGraticule(nLabel) { Opacity = .35 } } };
        var colours = new[] { "#40DCC8", "#B57CFF", "#FFD25A", "#5AA9FF", "#FF7A6B", "#7CF3A0", "#FF9AD8", "#A0B4FF", "#F0F0F0" };
        int n = 0;
        var chips = new List<Control>();
        foreach (var it in items)
        {
            var colour = Color.Parse(colours[n++ % colours.Length]);
            if (AioReadings.Graphs(settings, it.R.Key))
            {
                referenceUsed.Add(it.R.Key);
                var graph = new AioReferenceHistory { Color = colour, Mode = "line", Tag = "graphic", Top = 100 };
                referencePlots.Add((it.R.Key, null, graph)); TextColours.Bind(graph, it.R.Key, null);
                lines.Children.Add(graph);
            }
            var dot = new Avalonia.Controls.Shapes.Ellipse { Width = 10, Height = 10, Fill = Palette.Brush(colour), VerticalAlignment = VerticalAlignment.Center };
            var text = Stack(0, DeskCaption(it.Caption, nLabel, 12), FitLeft(GValue(it.R, 30)));
            var chip = Ui.Columns("Auto,Auto,*", dot, DeskStagePicture(it, DeskStage.Plain, 40), text); chip.ColumnSpacing = 10;
            ((Control)chip.Children[1]).Width = 64;
            chips.Add(DeskCard(chip, new Thickness(12, 8, 12, 8)));
        }
        var body = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), RowSpacing = 12 };
        body.Children.Add(DeskTitle(L.T("History") + " · 0–100", nInk));
        var chart = new DeskElastic { Min = 120, Child = lines }; Grid.SetRow(chart, 1); body.Children.Add(chart);
        var row = DeskRow(chips, 200, 10); Grid.SetRow(row, 2); body.Children.Add(row);
        return DeskCard(body);
    }

    private (DeskGrid[] Wide, DeskGrid[] Tall) DeskEffects(HashSet<string> top, List<DeskItem> items)
    {
        var big = items.Where(i => i.Kind is "cpu" or "gpu").ToList(); var small = items.Where(i => i.Kind is not ("cpu" or "gpu")).ToList();
        DeskGrid G(string columns, string rows, params string[] areas) => DeskGrid.Of(columns, rows, areas);
        var tall = new[] { G("1 1", "1.6 .9 .7 .6 .5 0", "scene scene", "details details", "cores ram", "storage net", "fans psu", "more more") };
        switch (designStyle)
        {
            case WidgetStyle.AioDeskCommand:
            {
                // FX-01: processor and graphics card inside large rings in the middle, the panels at both sides.
                if (big.Count > 0) deskParts["scene"] = DeskRow(big.Select(i => DeskHaloFigure(i, 50, 150)), 260, 20);
                if (small.Count > 0) deskParts["small"] = DeskRow(small.Select(i => DeskFigure(i, 34, DeskStage.Glow, 60)), 150, 10);
                DeskPanels(top);
                return (new[]
                {
                    G("1 1.25 1.25 1", "1 1 .7 .5 0", "details scene scene net", "cores scene scene fans", "cores small small storage", "ram small small psu", "more more more more"),
                    G("1 1.2 1.2 1", "1 .75 .7 0", "details scene scene net", "cores small small fans", "ram storage storage psu", "more more more more"),
                }, new[] { G("1 1", "1.4 .8 .9 .7 .6 .5 0", "scene scene", "small small", "details details", "cores ram", "storage net", "fans psu", "more more") });
            }
            case WidgetStyle.AioDeskExploded:
            {
                // FX-02: the PC drawn apart with labels.
                if (items.Count > 0) deskParts["scene"] = DeskExplodedScene(items);
                DeskPanels(top);
                return (new[]
                {
                    G("1.7 1 1", "1 .9 .7 .6 0", "scene details details", "scene cores net", "scene storage fans", "scene ram psu", "more more more"),
                    G("1.4 1 1", "1 .8 .7 0", "scene details details", "scene cores net", "storage ram fans", "psu more more"),
                }, tall);
            }
            case WidgetStyle.AioDeskThermal:
            {
                // FX-03: every part through a thermal camera.
                int spot = 1;
                var tiles = items.Select(i => (i, DeskThermalTile(i, spot++, i.Kind is "cpu" or "gpu" ? 44 : 34))).ToList();
                var first = tiles.Where(t => t.i.Kind is "cpu" or "gpu").Select(t => t.Item2).ToList(); var second = tiles.Where(t => t.i.Kind is not ("cpu" or "gpu")).Select(t => t.Item2).ToList();
                if (first.Count > 0) deskParts["scene"] = DeskRow(first, 240, 12);
                if (second.Count > 0) deskParts["small"] = DeskRow(second, 180, 12);
                deskParts["legend"] = new ArtHeatLegend();
                DeskPanels(top);
                return (new[]
                {
                    G("1 1 1.05", "1.3 .9 .6 .5 0 0", "scene scene details", "small small cores", "storage net fans", "ram psu psu", "legend legend legend", "more more more"),
                    G("1 1 1", "1.2 .8 .7 0 0", "scene scene details", "small small cores", "storage net fans", "legend legend legend", "ram psu more"),
                }, new[] { G("1 1", "1.3 .8 .9 .7 .6 .5 0 0", "scene scene", "small small", "details details", "cores ram", "storage net", "fans psu", "legend legend", "more more") });
            }
            case WidgetStyle.AioDeskHalo:
            {
                // FX-04: a ring around every part.
                if (big.Count > 0) deskParts["scene"] = DeskRow(big.Select(i => DeskCard(DeskHaloFigure(i, 46, 150), new Thickness(14))), 260, 14);
                if (small.Count > 0) deskParts["small"] = DeskRow(small.Select(i => DeskCard(DeskHaloFigure(i, 30, 80), new Thickness(10))), 150, 12);
                DeskPanels(top);
                return (new[]
                {
                    G("1 1 1 1 1.1", "1.4 1 .7 0", "scene scene scene scene details", "small small small cores net", "ram storage storage fans psu", "more more more more more"),
                    G("1 1 1 1", "1.3 .9 .7 0", "scene scene scene details", "small small cores net", "ram storage fans psu", "more more more more"),
                }, new[] { G("1 1", "1.4 .9 .9 .7 .6 .5 0", "scene scene", "small small", "details details", "cores ram", "storage net", "fans psu", "more more") });
            }
            case WidgetStyle.AioDeskSpotlight:
            {
                // FX-05: the processor in the spotlight, the other parts below.
                var star = big.FirstOrDefault() ?? items.FirstOrDefault();
                if (star != null)
                {
                    var caption = DeskCaption(star.Caption + (star.Model.Length > 0 ? " · " + star.Model : ""), star.Colour, 13);
                    var text = Stack(2, caption);
                    text.Children.Add(FitLeft(GValue(star.R, 120)));
                    if (star.Under != null) text.Children.Add(star.Under);
                    text.VerticalAlignment = VerticalAlignment.Center;
                    var stage = new Grid { Children = { new ArtDeskSpot(), DeskStagePicture(star, DeskStage.Glow, 160) } };
                    var hero = Ui.Columns("1.1*,1*", stage, text); hero.ColumnSpacing = 24;
                    hero.ColumnDefinitions[1].MinWidth = DeskNatural(caption, star.Under);
                    var body = new Grid { RowDefinitions = new RowDefinitions("*,Auto"), RowSpacing = 10, Children = { hero } };
                    if (AioReadings.Graphs(settings, star.R.Key)) { var plot = HistoryPlot(star.R, 44, "area", star.Colour); Grid.SetRow(plot, 1); body.Children.Add(plot); }
                    deskParts["scene"] = new Border { Child = body, Padding = new Thickness(10) };
                }
                var rest = items.Where(i => i != star).ToList();
                if (rest.Count > 0) deskParts["small"] = DeskRow(rest.Select(i => DeskFigure(i, 34, DeskStage.Glow, 60)), 150, 10);
                DeskPanels(top);
                return (new[]
                {
                    G("1.2 1.2 1", "1 1 .7 .55 0", "scene scene details", "scene scene cores", "small small net", "storage fans ram", "psu more more"),
                    G("1.1 1.1 1", "1 .7 .7 0", "scene scene details", "small small cores", "storage net fans", "ram psu more"),
                }, new[] { G("1 1", "1.4 .8 .9 .7 .6 .5 0", "scene scene", "small small", "details details", "cores ram", "storage net", "fans psu", "more more") });
            }
            case WidgetStyle.AioDeskShowroom:
            {
                // FX-06: every part in a lit glass case.
                if (items.Count > 0) deskParts["scene"] = DeskRow(items.Select(DeskVitrine), 170, 12);
                DeskPanels(top);
                return (new[]
                {
                    G("1 1.2 1 1 1", "1.5 .8 0", "scene scene scene scene scene", "details details cores net fans", "storage storage ram psu more"),
                    G("1 1 1 1", "1.3 .8 .6 0", "scene scene scene scene", "details details cores net", "storage ram fans psu", "more more more more"),
                }, tall);
            }
            case WidgetStyle.AioDeskFlow:
            {
                // FX-07: the parts as nodes joined by the lines they talk over.
                if (items.Count > 0)
                {
                    var map = new DeskNodeMap();
                    foreach (var it in items) map.Add(DeskNode(it, it.Kind is "cpu" or "gpu" ? 36 : 30), it.Kind, HeatColour.Of(Celsius(it.R)));
                    deskParts["scene"] = new Border { Child = map, Padding = new Thickness(10), Background = DeskStageBrush(), CornerRadius = new CornerRadius(18) };
                }
                DeskPanels(top);
                return (new[]
                {
                    G("1.8 1 1", "1 .9 .7 .55 0", "scene details details", "scene cores net", "scene storage fans", "scene ram psu", "more more more"),
                    G("1.5 1 1", "1 .8 .7 0", "scene details details", "scene cores net", "storage ram fans", "psu more more"),
                }, tall);
            }
            case WidgetStyle.AioDeskTelemetry:
            {
                // FX-08: one large chart with the history of every part.
                if (items.Count > 0) deskParts["scene"] = DeskTelemetry(items);
                DeskPanels(top);
                return (new[]
                {
                    G("2.3 1", "1 1 .8 .6 .5 0", "scene details", "scene cores", "scene net", "storage fans", "ram psu", "more more"),
                    G("1.8 1 1", "1 .8 .7 0", "scene details details", "scene cores net", "storage ram fans", "psu more more"),
                }, tall);
            }
            case WidgetStyle.AioDeskTower:
            {
                // FX-09: the parts one above the other as in a tower.
                if (items.Count > 0)
                {
                    var rows = DeskRow(items.Select(DeskTowerRow), 0, 10, true);
                    deskParts["scene"] = new Grid { Children = { new ArtDeskCase(NA), new Border { Padding = new Thickness(16, 18), Child = rows } } };
                }
                DeskPanels(top);
                return (new[]
                {
                    G("1 1.3 1", "1 .8 .75 .6 0", "scene details details", "scene cores net", "scene storage fans", "scene ram psu", "more more more"),
                    G("1 1 1", "1 .8 .7 0", "scene details details", "scene cores net", "storage ram fans", "psu more more"),
                }, tall);
            }
            default:
            {
                // FX-10 Keynote: the processor with a huge number, the other parts as large figures, the panels quiet.
                deskPlain = true;
                var star = big.FirstOrDefault() ?? items.FirstOrDefault();
                if (star != null)
                {
                    var text = Stack(2, DeskCaption(star.Caption, star.Colour, 13));
                    text.Children.Add(FitLeft(GValue(star.R, 140)));
                    if (DeskUnder(star) is { } u) text.Children.Add(u);
                    text.VerticalAlignment = VerticalAlignment.Center;
                    var hero = Ui.Columns("1*,1.2*", DeskStagePicture(star, DeskStage.Glow, 140), text); hero.ColumnSpacing = 24;
                    deskParts["scene"] = hero;
                    if (AioReadings.Graphs(settings, star.R.Key))
                    {
                        var plot = HistoryPlot(star.R, 50, "area", star.Colour); plot.Height = double.NaN;
                        deskParts["history"] = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), RowSpacing = 6,
                            Children = { DeskCaption(L.T("History"), nLabel, 12), new DeskElastic { Min = 50, Child = plot, [Grid.RowProperty] = 1 } } };
                    }
                }
                var rest = items.Where(i => i != star).ToList();
                if (rest.Count > 0) deskParts["small"] = DeskRow(rest.Select(DeskKeynoteStat), 200, 24);
                DeskPanels(top);
                return (new[]
                {
                    G("1.3 1 1", "1.4 .7 .7 .55 0", "scene scene small", "history history details", "cores net details", "storage fans ram", "psu more more"),
                    G("1.2 1 1", "1.2 .7 .7 0", "scene scene small", "history details details", "cores net storage", "fans ram psu"),
                }, new[] { G("1 1", "1.1 .8 .5 .9 .7 .6 .5 0", "scene scene", "small small", "history history", "details details", "cores ram", "storage net", "fans psu", "more more") });
            }
        }
    }

    /// <summary>FX-09: a part in the tower: picture left, value right.</summary>
    private Control DeskTowerRow(DeskItem it)
    {
        var caption = DeskCaption(it.Caption, it.Colour);
        var text = Stack(2, caption);
        text.Children.Add(FitLeft(GValue(it.R, 40)));
        var under = DeskUnder(it);
        if (under != null) { under.TextTrimming = TextTrimming.CharacterEllipsis; text.Children.Add(under); }
        text.VerticalAlignment = VerticalAlignment.Center;
        var row = Ui.Columns("1*,1.1*", DeskStagePicture(it, DeskStage.Glow, 56), text); row.ColumnSpacing = 14;
        row.ColumnDefinitions[1].MinWidth = DeskNatural(caption, under);
        return row;
    }

    /// <summary>FX-10: a part as a keynote figure: caption, a large number and the model.</summary>
    private Control DeskKeynoteStat(DeskItem it)
    {
        var text = Stack(2, DeskCaption(it.Caption, it.Colour, 12));
        text.Children.Add(FitLeft(GValue(it.R, 64)));
        if (DeskUnder(it) is { } u) { u.TextTrimming = TextTrimming.CharacterEllipsis; text.Children.Add(u); }
        return new Border { BorderBrush = Palette.Brush(Palette.Mix(nSurface, Colors.White, .14)), BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 12, 0, 0), Child = text };
    }
}

/// <summary>A child kept square (its share of the smaller side), centred: the picture inside a ring.</summary>
public sealed class DeskSquare : Decorator
{
    public double Share { get; set; } = .5;
    protected override Size MeasureOverride(Size available) { Child?.Measure(new Size(0, 0)); return new Size(0, 0); }
    protected override Size ArrangeOverride(Size final)
    {
        double s = Math.Min(final.Width, final.Height) * Share;
        Child?.Arrange(new Rect((final.Width - s) / 2, (final.Height - s) / 2, s, s));
        return final;
    }
}

/// <summary>Halo: a thick ring with ticks that fills with the value, in the colour of the temperature.</summary>
public sealed class ArtDeskHalo : ArtDecoration
{
    private double? level; private Color heat = HeatColour.Of(null); private readonly Color track;
    public ArtDeskHalo(Color track) : base(Colors.White) { this.track = track; }
    public void Set(double? fraction, Color colour) { level = fraction; heat = colour; InvalidateVisual(); }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height, r = Math.Min(w, h) / 2 - 6; if (r < 20) return;
        var centre = new Point(w / 2, h / 2); double thick = Math.Clamp(r * .07, 5, 14);
        var glow = new RadialGradientBrush { GradientStops = { new(BlackDraw.A(heat, .22), 0), new(BlackDraw.A(heat, 0), 1) } };
        c.DrawEllipse(glow, null, centre, r, r);
        for (int i = 0; i <= 60; i++)
        {
            double a = (135 + i * 270.0 / 60) * Math.PI / 180, inner = r - thick * 2.2, outer = r - thick * (i % 5 == 0 ? 1.4 : 1.8);
            c.DrawLine(new Pen(new SolidColorBrush(BlackDraw.A(track, i % 5 == 0 ? .9 : .5)), 1.2), new Point(centre.X + Math.Cos(a) * inner, centre.Y + Math.Sin(a) * inner), new Point(centre.X + Math.Cos(a) * outer, centre.Y + Math.Sin(a) * outer));
        }
        c.DrawGeometry(null, new Pen(new SolidColorBrush(track), thick, lineCap: PenLineCap.Round), Rings.Arc(centre, r, 135, 270));
        if (level is double f && f > 0)
        {
            var arc = Rings.Arc(centre, r, 135, 270 * Math.Clamp(f, 0, 1));
            c.DrawGeometry(null, new Pen(new SolidColorBrush(BlackDraw.A(heat, .3)), thick * 2.4, lineCap: PenLineCap.Round), arc);
            c.DrawGeometry(null, new Pen(new SolidColorBrush(heat), thick, lineCap: PenLineCap.Round), arc);
        }
    }
}

/// <summary>Glass showroom: a glass case lit from the top.</summary>
public sealed class ArtDeskVitrine : ArtDecoration
{
    public ArtDeskVitrine() : base(Colors.White) { }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 30 || h < 30) return;
        var r = new Rect(.5, .5, w - 1, h - 1);
        c.DrawRectangle(new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = { new(Color.FromArgb(255, 22, 30, 40), 0), new(Color.FromArgb(255, 8, 11, 15), .75), new(Color.FromArgb(255, 18, 24, 32), 1) }
        }, new Pen(new SolidColorBrush(Color.FromArgb(255, 54, 70, 88)), 1), r, 10, 10);
        c.DrawRectangle(new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = { new(Color.FromArgb(70, 255, 255, 255), 0), new(Color.FromArgb(0, 255, 255, 255), 1) }
        }, null, new Rect(w * .15, 4, w * .7, h * .5), 6, 6);
        c.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(200, 230, 240, 255)), 2), new Point(w * .18, 5), new Point(w * .82, 5));
        // A glass reflection across the front.
        c.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)), w * .05), new Point(w * .1, h * .95), new Point(w * .35, h * .05));
        c.DrawRectangle(new SolidColorBrush(Color.FromArgb(255, 30, 40, 52)), null, new Rect(1, h - 10, w - 2, 9), 0, 0);
    }
}

/// <summary>Spotlight: a cone of light from above onto the part.</summary>
public sealed class ArtDeskSpot : ArtDecoration
{
    public ArtDeskSpot() : base(Colors.White) { }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height; if (w < 30 || h < 30) return;
        var g = new StreamGeometry();
        using (var x = g.Open()) { x.BeginFigure(new Point(w * .42, 0), true); x.LineTo(new Point(w * .58, 0)); x.LineTo(new Point(w * .95, h)); x.LineTo(new Point(w * .05, h)); x.EndFigure(true); }
        c.DrawGeometry(new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = { new(Color.FromArgb(46, 255, 255, 255), 0), new(Color.FromArgb(10, 255, 255, 255), .8), new(Color.FromArgb(0, 255, 255, 255), 1) }
        }, null, g);
        c.DrawEllipse(new RadialGradientBrush { GradientStops = { new(Color.FromArgb(60, 255, 255, 255), 0), new(Color.FromArgb(0, 255, 255, 255), 1) } }, null, new Point(w / 2, h * .9), w * .42, h * .08);
    }
}

/// <summary>Flow map: a dark disc with a ring in the colour of the part's temperature.</summary>
public sealed class ArtDeskNode : ArtDecoration
{
    private Color heat = HeatColour.Of(null);
    public ArtDeskNode() : base(Colors.White) { }
    public void Set(double? celsius) { heat = HeatColour.Of(celsius); InvalidateVisual(); }
    public override void Render(DrawingContext c)
    {
        double w = Bounds.Width, h = Bounds.Height, r = Math.Min(w, h) / 2 - 4; if (r < 10) return;
        var centre = new Point(w / 2, h / 2);
        c.DrawEllipse(new RadialGradientBrush { GradientStops = { new(BlackDraw.A(heat, .35), 0), new(BlackDraw.A(heat, 0), 1) } }, null, centre, r + 4, r + 4);
        c.DrawEllipse(new SolidColorBrush(Color.FromArgb(255, 12, 17, 23)), new Pen(new SolidColorBrush(heat), 2), centre, r * .82, r * .82);
    }
}

/// <summary>Exploded view: parts down the middle in a zigzag, each with its label card at the side and a dashed line to it.</summary>
public sealed class DeskExploded : Panel
{
    private readonly List<(Control Part, Control Label, Color Colour)> items = new();
    private readonly DeskLines lines = new(true);
    public DeskExploded() { Children.Add(lines); }
    public void Add(Control part, Control label, Color colour) { items.Add((part, label, colour)); Children.Add(part); Children.Add(label); }
    private const double LabelWidth = .3;
    private const double ListPart = .28, ListGap = 12;
    /// <summary>Narrow (a sidebar): a label would not fit in its third, so the parts stand in a list, label beside each.</summary>
    private bool list;
    protected override Size MeasureOverride(Size available)
    {
        double w = double.IsFinite(available.Width) ? available.Width : 800, need = 0;
        lines.Measure(default);
        list = false;
        foreach (var (_, label, _) in items) { label.Measure(Size.Infinity); if (label.DesiredSize.Width > w * LabelWidth + .5) list = true; }
        double labelWidth = list ? w * (1 - ListPart) - ListGap : w * LabelWidth;
        foreach (var (part, label, _) in items)
        {
            part.Measure(new Size(w * (list ? ListPart : .4), double.PositiveInfinity)); label.Measure(new Size(labelWidth, double.PositiveInfinity));
            need += Math.Max(label.DesiredSize.Height, part.DesiredSize.Height) + 8;
        }
        return new Size(w, need);
    }
    protected override Size ArrangeOverride(Size final)
    {
        lines.Lines.Clear(); lines.Arrange(new Rect(final));
        int n = Math.Max(1, items.Count); double slot = final.Height / n;
        for (int i = 0; i < items.Count; i++)
        {
            var (part, label, colour) = items[i];
            bool left = i % 2 == 0; double y = i * slot;
            double lh = Math.Min(slot - 4, label.DesiredSize.Height), lw = list ? final.Width * (1 - ListPart) - ListGap : final.Width * LabelWidth;
            var partRect = list ? new Rect(0, y + 2, final.Width * ListPart, slot - 4) : new Rect(final.Width * (left ? .42 : .28), y + 2, final.Width * .3, slot - 4);
            var labelRect = list ? new Rect(final.Width - lw, y + (slot - lh) / 2, lw, lh) : new Rect(left ? final.Width - lw : 0, y + (slot - lh) / 2, lw, lh);
            if (list) { part.Arrange(partRect); label.Arrange(labelRect); continue; }
            part.Arrange(partRect); label.Arrange(labelRect);
            var from = new Point(left ? labelRect.X : labelRect.Right, labelRect.Y + lh / 2);
            var to = new Point(left ? partRect.Right - partRect.Width * .2 : partRect.X + partRect.Width * .2, partRect.Y + partRect.Height / 2);
            lines.Lines.Add((from, to, colour));
        }
        lines.InvalidateVisual();
        return final;
    }
}

/// <summary>The dashed lines of the exploded view and the flow map, drawn under the parts.</summary>
public sealed class DeskLines : ArtDecoration
{
    private readonly bool dots;
    public readonly List<(Point From, Point To, Color Colour)> Lines = new();
    public DeskLines(bool dots) : base(Colors.White) { this.dots = dots; }
    public override void Render(DrawingContext c)
    {
        foreach (var (from, to, colour) in Lines)
        {
            var pen = new Pen(new SolidColorBrush(BlackDraw.A(colour, dots ? .7 : .55)), dots ? 1.5 : 2.5) { DashStyle = new DashStyle(dots ? new double[] { 4, 3 } : new double[] { 3, 3 }, 0) };
            c.DrawLine(pen, from, to);
            if (dots) c.DrawEllipse(new SolidColorBrush(colour), null, to, 3.5, 3.5);
        }
    }
}

/// <summary>Flow map: the processor in the middle, graphics cards above, memory left, drives below, joined by dashed lines.</summary>
public sealed class DeskNodeMap : Panel
{
    private readonly List<(Control Node, string Kind, Color Colour)> nodes = new();
    private readonly List<Rect> placed = new();
    private readonly DeskLines lines = new(false);
    public DeskNodeMap() { Children.Add(lines); }
    public void Add(Control node, string kind, Color colour) { nodes.Add((node, kind, colour)); Children.Add(node); }
    protected override Size MeasureOverride(Size available)
    {
        double w = double.IsFinite(available.Width) ? available.Width : 800, need = 0;
        lines.Measure(default);
        foreach (var (node, _, _) in nodes) { node.Measure(new Size(w / 4, double.PositiveInfinity)); need = Math.Max(need, node.DesiredSize.Height); }
        return new Size(w, need * 3 + 40);
    }
    protected override Size ArrangeOverride(Size final)
    {
        placed.Clear(); lines.Arrange(new Rect(final));
        double rowH = final.Height / 3;
        foreach (var (node, kind, _) in nodes)
        {
            var same = nodes.Where(n => n.Kind == kind).ToList(); int at = same.FindIndex(n => n.Node == node);
            // Parts of a kind share their row evenly: never wider than their share, so they never overlap.
            Rect Spread(double from, double to, double y)
            {
                double share = (to - from) * final.Width / same.Count, size = Math.Min(Math.Min(rowH - 6, final.Width / 4.2), share - 8);
                double x = final.Width * from + share * (at + .5);
                return new Rect(x - size / 2, y, size, rowH - 6);
            }
            Rect r = kind switch
            {
                "cpu" => Spread(.27, .57, rowH),
                "gpu" => same.Count == 1 ? Spread(.27, .57, 0) : Spread(.15, .95, 0),
                "ram" => Spread(0, .25, rowH),
                _ => same.Count == 1 ? Spread(.6, .95, rowH * 2) : Spread(.05, .95, rowH * 2)
            };
            node.Arrange(r); placed.Add(r);
        }
        lines.Lines.Clear();
        int cpu = nodes.FindIndex(n => n.Kind == "cpu");
        if (cpu >= 0)
        {
            var hub = new Point(placed[cpu].Center.X, placed[cpu].Y + placed[cpu].Height * .35);
            for (int i = 0; i < nodes.Count; i++)
                if (i != cpu) lines.Lines.Add((hub, new Point(placed[i].Center.X, placed[i].Y + placed[i].Height * .35), nodes[i].Colour));
        }
        lines.InvalidateVisual();
        return final;
    }
}
