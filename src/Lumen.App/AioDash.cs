using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

/// <summary>
/// 5.6.0: four dashboards (race telemetry, control room, gauge wall, studio mixer, drafts D-06 … D-09) and Capsule Glass
/// (draft N-01). They build on the 3D designs: the same skeleton, a look and a layout of their own on top; the drawings
/// carry no numbers, every number is a value bound to its sensor (the position boxes of Live Timing, 5.7.0, are ranks, not readings).
/// 5.7.0: Activity Rings, Live Timing and Smart Home (drafts W-01, W-07, W-24); Glass Tubes, Glass Dial, Chrome Bezels and Glossy Pills (G-05, G-08, G-13, G-15).
/// </summary>
public sealed partial class AioTile
{
    private bool Dash => Fam >= NeoDesigns.Family.Race && Fam < NeoDesigns.Family.Desk;

    private void DashLook()
    {
        FontFamily F(string key) => Ui.DesignFont(Ui.FamilyFor(key));
        (nFont, nValueFont, nBigFont) = Fam switch
        {
            NeoDesigns.Family.Race => (F("chakra-petch"), F("exo-2"), Ui.DesignFont(Ui.Orbitron)),
            NeoDesigns.Family.Scada => (F("share-tech-mono"), F("share-tech-mono"), F("share-tech-mono")),
            NeoDesigns.Family.Timing => (F("barlow-condensed"), F("barlow-condensed"), F("barlow-condensed")),
            NeoDesigns.Family.Rings or NeoDesigns.Family.Home or NeoDesigns.Family.GlassTubes or NeoDesigns.Family.GlassDial or NeoDesigns.Family.ChromeBezel or NeoDesigns.Family.GlossPills
                => (F("open-sans"), F("open-sans"), F("open-sans")),
            _ when Fresh => FreshFonts(),
            _ => (Ui.DesignFont(Ui.Inter), Ui.DesignFont(Ui.Inter), Ui.DesignFont(Ui.Inter))
        };
        nLabel = lightSurface ? muted : Color.Parse(Fam == NeoDesigns.Family.Scada ? "#9AA6B2" : "#8E949C");
        nInk = lightSurface ? ink : Color.Parse("#F2F4F6");
        CornerRadius = new CornerRadius(Fam switch { NeoDesigns.Family.Race => 18, NeoDesigns.Family.Timing => 4, NeoDesigns.Family.Rings or NeoDesigns.Family.Home => 20, NeoDesigns.Family.Scada => 6, NeoDesigns.Family.Capsule => 22, NeoDesigns.Family.Cover or NeoDesigns.Family.Loadout => 4, _ => 14 });
        BorderBrush = Palette.Brush(Palette.Mix(nSurface, Colors.White, .09));
    }

    private bool DashTop(List<(AioReading R, string Caption, string Art, int Colour)> items, HashSet<string> top)
    {
        if (Fresh) return FreshTop(items, top);
        string Cap(string c) => Upper ? c.ToUpperInvariant() : c;
        double g = settings.ActiveWidget?.Graphics ?? 1;
        TextBlock Caption(string text, Color colour, double size = 10)
        {
            var t = NText(Cap(text), size, colour, true); t.HorizontalAlignment = HorizontalAlignment.Center; t.TextTrimming = TextTrimming.CharacterEllipsis; t.LetterSpacing = 1.2; return t;
        }
        Control Centred(Control c) { c.HorizontalAlignment = HorizontalAlignment.Center; return c; }
        Viewbox Shrink(Control c, double max) => new() { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, MaxWidth = max, HorizontalAlignment = HorizontalAlignment.Center, Child = c };
        var large = items.Where(i => i.Art is "cpu" or "gpu").Take(2).ToList();
        switch (Fam)
        {
            case NeoDesigns.Family.Race:
            {
                var lights = new ArtShiftLights();
                var load = R("load") ?? items[0].R;
                lights.Set(load.Fraction); watchers.Add((load.Key, row => lights.Set(row?.Fraction)));
                Control Box((AioReading R, string Caption, string Art, int Colour) i, bool big)
                {
                    var label = NText(Cap(i.Caption), 9, nLabel, true); label.TextTrimming = TextTrimming.CharacterEllipsis; label.LetterSpacing = 1.5;
                    var value = Fit(GValue(i.R, big ? 34 : 20)); value.HorizontalAlignment = HorizontalAlignment.Left;
                    return new Border
                    {
                        Background = Palette.Brush(SeeThrough(Color.Parse("#000000"))), BorderBrush = Palette.Brush(Color.Parse("#2A2A2A")), BorderThickness = new Thickness(1),
                        Padding = new Thickness(8, 6), Child = Stack(0, label, value)
                    };
                }
                var screen = new Border
                {
                    Background = Palette.Brush(SeeThrough(Color.Parse("#050505"))), CornerRadius = new CornerRadius(8), Padding = new Thickness(6),
                    Child = Stack(6, large.Count > 0 ? NGrid(large.Select(i => Box(i, true)), large.Count, 6) : new Control(),
                        NGrid(items.Where(i => !large.Contains(i)).Select(i => Box(i, false)), 3, 6))
                };
                var wheel = new Border
                {
                    Tag = "aio-section", CornerRadius = new CornerRadius(26, 26, 54, 54), Padding = new Thickness(14, 12, 14, 12), BorderThickness = new Thickness(1),
                    BorderBrush = Palette.Brush(Color.Parse("#2A2A2A")),
                    Background = new LinearGradientBrush
                    {
                        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                        GradientStops = { new(SeeThrough(Color.Parse("#1B1B1B")), 0), new(SeeThrough(Color.Parse("#101010")), 1) }
                    },
                    Child = Stack(10, lights, screen, new ArtWheelButtons())
                };
                content.Children.Add(wheel);
                break;
            }
            case NeoDesigns.Family.Scada:
            {
                var title = NText("PLANT OVERVIEW · ALL SYSTEMS NOMINAL", 10, nLabel, true); title.LetterSpacing = 1.6; title.TextTrimming = TextTrimming.CharacterEllipsis;
                TextFonts.Mark(title, TextRole.Title);
                var tanks = items.Select(i =>
                {
                    var tank = new ArtTank(NAcc(i.Colour), Color.Parse("#5A6674")) { Height = 120 * g };
                    tank.Set(i.R.Fraction); watchers.Add((i.R.Key, row => tank.Set(row?.Fraction)));
                    var lamp = new Avalonia.Controls.Shapes.Ellipse { Width = 9, Height = 9, Fill = Palette.Brush(Color.Parse("#3DFF8B")), HorizontalAlignment = HorizontalAlignment.Center, Tag = "graphic",
                        Effect = new DropShadowEffect { BlurRadius = 6, OffsetX = 0, OffsetY = 0, Color = Color.Parse("#3DFF8B") } };
                    return (Control)Stack(5, Centred(Fit(GValue(i.R, 17))), tank, Caption(i.Caption, nLabel), lamp);
                }).ToArray();
                content.Children.Add(NPanel(null, Stack(8, title, NGrid(tanks, Math.Min(5, tanks.Length), 0))));
                break;
            }
            case NeoDesigns.Family.GaugeWall:
            {
                var cells = items.Select(i =>
                {
                    var colour = NAcc(i.Colour);
                    var gauge = new ArtHalfGauge(colour, Palette.Mix(nSurface, Colors.White, .1)) { Width = 96 * g, Height = 52 * g };
                    gauge.Set(i.R.Fraction); watchers.Add((i.R.Key, row => gauge.Set(row?.Fraction)));
                    var value = Fit(GValue(i.R, 19)); value.HorizontalAlignment = HorizontalAlignment.Center; value.VerticalAlignment = VerticalAlignment.Bottom; value.Margin = new Thickness(22 * g, 0, 22 * g, 0);
                    var face = Shrink(new Grid { Width = 96 * g, Height = 52 * g, Children = { gauge, value } }, 96 * g);
                    return (Control)new Border
                    {
                        Background = Palette.Brush(SeeThrough(Palette.Mix(nSurface, Colors.White, .045))), BorderBrush = Palette.Brush(Palette.Mix(nSurface, Colors.White, .1)), BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(14), Padding = new Thickness(6, 8, 6, 6), Child = Stack(4, face, Caption(i.Caption, colour))
                    };
                }).ToArray();
                content.Children.Add(new Border { Tag = "aio-section", Child = NGrid(cells, 3, 8) });
                break;
            }
            case NeoDesigns.Family.Mixer:
            {
                var body = Stack(12);
                if (large.Count > 0)
                {
                    var meters = large.Select(i =>
                    {
                        var vu = new ArtVu { Width = 130 * g, Height = 78 * g };
                        vu.Set(i.R.Fraction); watchers.Add((i.R.Key, row => vu.Set(row?.Fraction)));
                        return (Control)Stack(4, Shrink(vu, 130 * g), Centred(Fit(GValue(i.R, 22))), Caption(i.Caption, nLabel));
                    });
                    body.Children.Add(NGrid(meters, 2, 10));
                }
                // Each value once: processor and graphics on the VU meters, every other part on a fader.
                var faders = items.Where(i => !large.Contains(i)).Select(i =>
                {
                    var fader = new ArtFader { Width = 40, Height = 140 * g, HorizontalAlignment = HorizontalAlignment.Center };
                    fader.Set(i.R.Fraction); watchers.Add((i.R.Key, row => fader.Set(row?.Fraction)));
                    return (Control)Stack(6, fader, Centred(Fit(GValue(i.R, 15))), Caption(i.Caption, nLabel, 9));
                }).ToArray();
                if (faders.Length > 0) body.Children.Add(NPanel(null, NGrid(faders, Math.Min(5, faders.Length), 4)));
                content.Children.Add(new Border { Tag = "aio-section", Child = body });
                break;
            }
            case NeoDesigns.Family.Capsule:
            {
                var capsules = items.Select(i =>
                {
                    bool big = large.Contains(i);
                    var capsule = new ArtCapsule(NAcc(i.Colour)) { Width = (big ? 54 : 44) * g, Height = (big ? 200 : 160) * g, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom };
                    capsule.Set(i.R.Fraction); watchers.Add((i.R.Key, row => capsule.Set(row?.Fraction)));
                    var holder = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Height = 200 * g, VerticalAlignment = VerticalAlignment.Bottom, Child = capsule };
                    return (Control)Stack(8, holder, Centred(Fit(GValue(i.R, big ? 20 : 16))), Caption(i.Caption, nLabel));
                }).ToArray();
                content.Children.Add(new Border { Tag = "aio-section", Padding = new Thickness(0, 6), Child = NGrid(capsules, Math.Min(5, capsules.Length), 8) });
                break;
            }
            case NeoDesigns.Family.Floating:
            {
                Control Float((AioReading R, string Caption, string Art, int Colour) i, double width)
                {
                    var glow = new ArtHeatGlow(); glow.Set(Celsius(i.R)); watchers.Add((i.R.Key, row => glow.Set(Celsius(row))));
                    var picture = Part(i.Art, width, width * .72);
                    var scene = new Grid { Width = width * 1.2, Height = width * .86, Children = { glow, picture } };
                    return Stack(4, Shrink(scene, width * 1.2), Centred(Fit(GValue(i.R, large.Contains(i) ? 22 : 18))), Caption(i.Caption, nLabel));
                }
                var body = Stack(12);
                if (large.Count > 0) body.Children.Add(NGrid(large.Select(i => Float(i, (i.Art == "gpu" ? 140 : 116) * g)), 2, 12));
                var others = items.Where(i => !large.Contains(i)).ToList();
                if (others.Count > 0) body.Children.Add(NGrid(others.Select(i => Float(i, 76 * g)), 3, 8));
                body.Children.Add(new ArtHeatLegend());
                content.Children.Add(new Border { Tag = "aio-section", Padding = new Thickness(0, 6), Child = body });
                break;
            }
            case NeoDesigns.Family.PartCards:
            {
                var cards = items.Select(i =>
                {
                    var frame = new ArtHeatCard(nSurface); frame.Set(Celsius(i.R)); watchers.Add((i.R.Key, row => frame.Set(Celsius(row))));
                    var glow = new ArtHeatGlow(false) { Width = 130 * g, Height = 96 * g, HorizontalAlignment = HorizontalAlignment.Right };
                    glow.Set(Celsius(i.R)); watchers.Add((i.R.Key, row => glow.Set(Celsius(row))));
                    var picture = Part(i.Art, 104 * g, 70 * g); picture.HorizontalAlignment = HorizontalAlignment.Right; picture.Margin = new Thickness(0, 0, 12 * g, 0);
                    var label = NText(Cap(i.Caption), 10, nLabel, true); label.TextTrimming = TextTrimming.CharacterEllipsis; label.LetterSpacing = 1.6;
                    var value = Fit(GValue(i.R, 34)); value.HorizontalAlignment = HorizontalAlignment.Left;
                    var text = Stack(2, label, value); text.VerticalAlignment = VerticalAlignment.Center; text.Margin = new Thickness(14, 10, 120 * g, 10);
                    return (Control)new Grid { MinHeight = 92 * g, Children = { frame, glow, picture, text } };
                }).ToList();
                cards.Add(new ArtHeatLegend());
                content.Children.Add(new Border { Tag = "aio-section", Child = Stack(10, cards.ToArray()) });
                break;
            }
            case NeoDesigns.Family.Podiums:
            {
                Control Stand((AioReading R, string Caption, string Art, int Colour) i, double width)
                {
                    var glow = new ArtHeatGlow(false); var podium = new ArtPodium { Width = width * .95, Height = width * .24, HorizontalAlignment = HorizontalAlignment.Center };
                    void Heat(AioReading? row) { glow.Set(Celsius(row)); podium.Set(Celsius(row)); }
                    Heat(i.R); watchers.Add((i.R.Key, Heat));
                    var picture = Part(i.Art, width, width * .7); picture.VerticalAlignment = VerticalAlignment.Bottom;
                    var scene = new Grid { Width = width * 1.15, Height = width * .78, Children = { glow, picture } };
                    var stand = Stack(0, scene, podium); podium.Margin = new Thickness(0, -width * .06, 0, 0);
                    return Stack(4, Shrink(stand, width * 1.15), Centred(Fit(GValue(i.R, large.Contains(i) ? 22 : 18))), Caption(i.Caption, nLabel));
                }
                var body = Stack(12);
                if (large.Count > 0) body.Children.Add(NGrid(large.Select(i => Stand(i, (i.Art == "gpu" ? 140 : 112) * g)), 2, 12));
                var others = items.Where(i => !large.Contains(i)).ToList();
                if (others.Count > 0) body.Children.Add(NGrid(others.Select(i => Stand(i, 76 * g)), 3, 8));
                body.Children.Add(new ArtHeatLegend());
                content.Children.Add(new Border { Tag = "aio-section", Padding = new Thickness(0, 6), Child = body });
                break;
            }
            case NeoDesigns.Family.Strip:
            {
                var cells = items.Select(i =>
                {
                    var glow = new ArtHeatGlow(false); glow.Set(Celsius(i.R)); watchers.Add((i.R.Key, row => glow.Set(Celsius(row))));
                    var picture = Part(i.Art, (i.Art == "gpu" ? 66 : 56) * g, 46 * g);
                    var scene = new Grid { Width = 72 * g, Height = 56 * g, Children = { glow, picture } };
                    return (Control)Stack(3, Shrink(scene, 72 * g), Centred(Fit(GValue(i.R, 17))), Caption(i.Caption, nLabel, 9));
                }).ToArray();
                content.Children.Add(NPanel(null, Stack(8, NGrid(cells, Math.Min(5, cells.Length), 4), new ArtHeatLegend())));
                break;
            }
            case NeoDesigns.Family.HeatBar:
            {
                var cards = items.Select(i =>
                {
                    var frame = new ArtHeatCard(nSurface); var scale = new ArtHeatScale();
                    var glow = new ArtHeatGlow(false) { Width = 130 * g, Height = 96 * g, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
                    void Heat(AioReading? row) { frame.Set(Celsius(row)); scale.Set(Celsius(row)); glow.Set(Celsius(row)); }
                    Heat(i.R); watchers.Add((i.R.Key, Heat));
                    var picture = Part(i.Art, 100 * g, 62 * g); picture.HorizontalAlignment = HorizontalAlignment.Right; picture.VerticalAlignment = VerticalAlignment.Top; picture.Margin = new Thickness(0, 10, 12 * g, 0);
                    var label = NText(Cap(i.Caption), 10, nLabel, true); label.TextTrimming = TextTrimming.CharacterEllipsis; label.LetterSpacing = 1.6;
                    var value = Fit(GValue(i.R, 32)); value.HorizontalAlignment = HorizontalAlignment.Left;
                    var text = Stack(2, label, value); text.Margin = new Thickness(0, 0, 112 * g, 0);
                    var inner = Stack(8, text, scale); inner.Margin = new Thickness(14, 12, 14, 12);
                    return (Control)new Grid { Children = { frame, glow, picture, inner } };
                }).ToList();
                cards.Add(new ArtHeatLegend());
                content.Children.Add(new Border { Tag = "aio-section", Child = Stack(10, cards.ToArray()) });
                break;
            }
            case NeoDesigns.Family.HeatList:
            {
                var rows = items.Select(i =>
                {
                    var glow = new ArtHeatGlow(false); glow.Set(Celsius(i.R)); watchers.Add((i.R.Key, row => glow.Set(Celsius(row))));
                    var picture = Part(i.Art, (i.Art == "gpu" ? 60 : 52) * g, 40 * g);
                    var scene = new Grid { Width = 66 * g, Height = 48 * g, Children = { glow, picture } };
                    var label = NText(Cap(i.Caption), 10.5, nLabel, true); label.TextTrimming = TextTrimming.CharacterEllipsis; label.VerticalAlignment = VerticalAlignment.Center; label.LetterSpacing = 1.4;
                    var value = new AioReferenceValue(GValue(i.R, 24)) { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
                    var line = Ui.Columns("Auto,*,Auto", Shrink(scene, 66 * g), label, value); line.ColumnSpacing = 10;
                    return (Control)new Border { BorderBrush = Palette.Brush(Palette.Mix(nSurface, Colors.White, .07)), BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(0, 4), Child = line };
                }).ToArray();
                content.Children.Add(NPanel(null, Stack(4, Stack(0, rows), new ArtHeatLegend())));
                break;
            }
            case NeoDesigns.Family.Rings:
            {
                // Activity rings: the first three parts as rings inside one another, every other part as a small tile.
                var ringed = items.Take(3).ToList();
                var rings = new ArtRings(ringed.Select((_, k) => NAcc(k)).ToArray()) { Width = 168 * g, Height = 168 * g };
                for (int k = 0; k < ringed.Count; k++)
                {
                    int at = k; var reading = ringed[k].R;
                    rings.Set(at, reading.Fraction); watchers.Add((reading.Key, row => rings.Set(at, row?.Fraction)));
                }
                Control Entry((AioReading R, string Caption, string Art, int Colour) i, Color colour, double size)
                {
                    var label = NText(Cap(i.Caption), 10.5, colour, true); label.TextTrimming = TextTrimming.CharacterEllipsis; label.LetterSpacing = .6;
                    return Stack(0, label, FitLeft(GValue(i.R, size)));
                }
                var legend = Stack(8, ringed.Select((i, k) => Entry(i, NAcc(k), 26)).ToArray()); legend.VerticalAlignment = VerticalAlignment.Center;
                var head = Ui.Columns("Auto,*", Shrink(rings, 168 * g), legend); head.ColumnSpacing = 14;
                var body = Stack(12, head);
                var others = items.Skip(3).Select(i => (Control)new Border
                {
                    Background = Palette.Brush(SeeThrough(Palette.Mix(nSurface, Colors.White, .05))), CornerRadius = new CornerRadius(12), Padding = new Thickness(10, 8),
                    Child = Entry(i, nLabel, 18)
                }).ToArray();
                if (others.Length > 0) body.Children.Add(NGrid(others, Math.Min(3, others.Length), 8));
                content.Children.Add(new Border { Tag = "aio-section", Child = body });
                break;
            }
            case NeoDesigns.Family.Timing:
            {
                // Live timing: one row per part with position, team colour, name and reading, like a race broadcast.
                var title = NText("LIVE TIMING", 11, NAcc(0), true); title.LetterSpacing = 2.4; TextFonts.Mark(title, TextRole.Title);
                var rows = items.Select((i, k) =>
                {
                    var tag = new ArtTimingTag(k + 1, NAcc(k % 4)) { VerticalAlignment = VerticalAlignment.Stretch };
                    var name = NText(Cap(i.Caption), 14, nInk, true); name.TextTrimming = TextTrimming.CharacterEllipsis; name.VerticalAlignment = VerticalAlignment.Center; name.LetterSpacing = .8;
                    var value = new AioReferenceValue(GValue(i.R, 21)) { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
                    var line = Ui.Columns("Auto,*,Auto", tag, name, value); line.ColumnSpacing = 8;
                    return (Control)new Border
                    {
                        MinHeight = 38, Padding = new Thickness(0, 0, 10, 0),
                        Background = new LinearGradientBrush
                        {
                            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                            GradientStops = { new(SeeThrough(Color.Parse("#15151E")), 0), new(SeeThrough(Color.Parse("#0D0D14")), 1) }
                        },
                        Child = line
                    };
                }).ToArray();
                content.Children.Add(new Border { Tag = "aio-section", Child = Stack(3, new Control[] { title }.Concat(rows).ToArray()) });
                break;
            }
            case NeoDesigns.Family.Home:
            {
                // Smart home: every part a tile with its picture; processor and graphics are the tiles that are "on".
                var title = NText(Cap("My PC"), 18, nInk, true); TextFonts.Mark(title, TextRole.Title);
                var tiles = items.Select(i =>
                {
                    bool on = large.Contains(i); var colour = NAcc(i.Colour);
                    var icon = Part(i.Art, 42 * g, 30 * g); icon.HorizontalAlignment = HorizontalAlignment.Left;
                    var name = NText(i.Caption, 12, nInk, true); name.TextTrimming = TextTrimming.CharacterEllipsis;
                    return (Control)new Border
                    {
                        CornerRadius = new CornerRadius(16), Padding = new Thickness(10, 10, 10, 8), BorderThickness = new Thickness(1),
                        Background = Palette.Brush(SeeThrough(on ? Palette.Mix(nSurface, colour, .24) : Palette.Mix(nSurface, Colors.White, .07))),
                        BorderBrush = Palette.Brush(on ? Palette.Mix(nSurface, colour, .55) : Palette.Mix(nSurface, Colors.White, .1)),
                        Child = Stack(6, icon, Stack(0, name, FitLeft(GValue(i.R, 17))))
                    };
                }).ToArray();
                content.Children.Add(new Border { Tag = "aio-section", Child = Stack(10, title, NGrid(tiles, Math.Min(3, tiles.Length), 8)) });
                break;
            }
            case NeoDesigns.Family.GlassTubes:
            {
                // Glass tubes: every part a clear tube filled with glowing liquid; the number below it.
                var tubes = items.Select(i =>
                {
                    var tube = new ArtGlassTube(NAcc(i.Colour)) { Width = 42 * g, Height = 190 * g, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom };
                    tube.Set(i.R.Fraction); watchers.Add((i.R.Key, row => tube.Set(row?.Fraction)));
                    var holder = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Height = 190 * g, VerticalAlignment = VerticalAlignment.Bottom, Child = tube };
                    return (Control)Stack(6, holder, Centred(Fit(GValue(i.R, large.Contains(i) ? 20 : 17))), Caption(i.Caption, nLabel));
                }).ToArray();
                content.Children.Add(new Border { Tag = "aio-section", Padding = new Thickness(0, 6), Child = NGrid(tubes, Balanced(tubes.Length), 6) });
                break;
            }
            case NeoDesigns.Family.GlassDial:
            {
                // Glass dial: processor (outer ring) and graphics (inner ring) on one glass disc, every other part on a glass tile.
                var outer = large.Count > 0 ? large[0] : items[0];
                var inner = large.Count > 1 ? large[1] : items.Where(i => !ReferenceEquals(i.R, outer.R)).FirstOrDefault();
                var dial = new ArtGlassDial(NAcc(0), NAcc(1)) { Width = 230 * g, Height = 230 * g };
                dial.Set(0, outer.R.Fraction); watchers.Add((outer.R.Key, row => dial.Set(0, row?.Fraction)));
                var face = Stack(0, Centred(Fit(GValue(outer.R, 46))), Caption(outer.Caption, nLabel, 11));
                face.VerticalAlignment = VerticalAlignment.Center; face.Margin = new Thickness(56 * g, 0);
                var shown = new List<(AioReading R, string Caption, string Art, int Colour)> { outer };
                var body = Stack(12, Shrink(new Grid { Width = 230 * g, Height = 230 * g, Children = { dial, face } }, 230 * g));
                if (inner.R != null && !ReferenceEquals(inner.R, outer.R))
                {
                    dial.Set(1, inner.R.Fraction); watchers.Add((inner.R.Key, row => dial.Set(1, row?.Fraction)));
                    var line = Stack(0, Caption(inner.Caption, NAcc(1), 10), Centred(Fit(GValue(inner.R, 22))));
                    body.Children.Add(line); shown.Add(inner);
                }
                var tiles = items.Where(i => !shown.Contains(i)).Select(i => (Control)new Border
                {
                    Background = new LinearGradientBrush
                    {
                        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                        GradientStops = { new(Color.FromArgb(40, 255, 255, 255), 0), new(Color.FromArgb(10, 255, 255, 255), 1) }
                    },
                    BorderBrush = Palette.Brush(Color.FromArgb(56, 255, 255, 255)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(8, 8),
                    Child = Stack(2, Caption(i.Caption, nLabel, 9.5), Centred(Fit(GValue(i.R, 18))))
                }).ToArray();
                if (tiles.Length > 0) body.Children.Add(NGrid(tiles, Math.Min(3, tiles.Length), 8));
                content.Children.Add(new Border { Tag = "aio-section", Child = body });
                break;
            }
            case NeoDesigns.Family.ChromeBezel:
            {
                // Chrome bezels: processor and graphics on polished round gauges, every other part as a number underneath.
                Control Gauge((AioReading R, string Caption, string Art, int Colour) i, double size)
                {
                    var bezel = new ArtChromeBezel(NAcc(i.Colour)) { Width = size, Height = size };
                    bezel.Set(i.R.Fraction); watchers.Add((i.R.Key, row => bezel.Set(row?.Fraction)));
                    var face = Stack(0, Centred(Fit(GValue(i.R, 28))), Caption(i.Caption, nLabel, 10)); face.VerticalAlignment = VerticalAlignment.Center; face.Margin = new Thickness(size * .22, 0);
                    return Shrink(new Grid { Width = size, Height = size, Children = { bezel, face } }, size);
                }
                var heads = large.Count > 0 ? large : items.Take(2).ToList();
                var body = Stack(12, NGrid(heads.Select(i => Gauge(i, 150 * g)), heads.Count, 10));
                var others = items.Where(i => !heads.Contains(i)).Select(i => (Control)Stack(0, Caption(i.Caption, nLabel, 9.5), Centred(Fit(GValue(i.R, 20))))).ToArray();
                if (others.Length > 0) body.Children.Add(NGrid(others, Math.Min(3, others.Length), 8));
                content.Children.Add(new Border { Tag = "aio-section", Padding = new Thickness(0, 4), Child = body });
                break;
            }
            case NeoDesigns.Family.GlossPills:
            {
                // Glossy pills: a glossy capsule per part, like a row of sweets; the number sits on the capsule.
                var pills = items.Select((i, k) =>
                {
                    var pill = new ArtGlossPill(NAcc(k % 4)) { Width = 56 * g, Height = 118 * g };
                    var value = Fit(GValue(i.R, 15)); value.VerticalAlignment = VerticalAlignment.Bottom; value.HorizontalAlignment = HorizontalAlignment.Center; value.Margin = new Thickness(3, 0, 3, 12 * g);
                    var face = Shrink(new Grid { Width = 56 * g, Height = 118 * g, Children = { pill, value } }, 56 * g);
                    return (Control)Stack(6, face, Caption(i.Caption, nLabel));
                }).ToArray();
                content.Children.Add(new Border { Tag = "aio-section", Padding = new Thickness(0, 6), Child = NGrid(pills, Balanced(pills.Length), 6) });
                break;
            }
            default: return false;
        }
        if (CyberCores() is { } cores) content.Children.Add(Shell(cores));
        return true;
    }

    /// <summary>Columns for a row of up to five; more parts split into two even rows (seven as four and three).</summary>
    private static int Balanced(int n) => n <= 5 ? Math.Max(1, n) : (n + 1) / 2;

    /// <summary>The temperature that colours a part: its own for temperatures, otherwise its share of 30–85 °C.</summary>
    private static double? Celsius(AioReading? r) => r == null ? null
        : r.Key.EndsWith(".temp") || r.Key.StartsWith("aio.system.disk.") ? r.Number : r.Fraction is double f ? 30 + 55 * f : null;

    private Control DashPanel(Border panel, Color part)
    {
        if (Fresh) { FreshPanel(panel, part); return panel; }
        switch (Fam)
        {
            case NeoDesigns.Family.Race:
                panel.CornerRadius = new CornerRadius(4); panel.Background = Palette.Brush(SeeThrough(Color.Parse("#000000"))); panel.BorderBrush = Palette.Brush(Color.Parse("#2A2A2A"));
                break;
            case NeoDesigns.Family.Scada:
                panel.CornerRadius = new CornerRadius(6); panel.Background = Palette.Brush(SeeThrough(Color.Parse("#0B0E12"))); panel.BorderBrush = Palette.Brush(Color.Parse("#263039"));
                break;
            case NeoDesigns.Family.Mixer:
                panel.CornerRadius = new CornerRadius(10); panel.Background = Palette.Brush(SeeThrough(Color.Parse("#141414"))); panel.BorderBrush = Palette.Brush(Color.Parse("#262626"));
                break;
            case NeoDesigns.Family.Capsule:
                panel.CornerRadius = new CornerRadius(22);
                break;
            case NeoDesigns.Family.Timing:
                panel.CornerRadius = new CornerRadius(2); panel.Background = Palette.Brush(SeeThrough(Color.Parse("#0D0D14"))); panel.BorderBrush = Palette.Brush(Color.Parse("#22222E"));
                break;
            case NeoDesigns.Family.Rings or NeoDesigns.Family.Home or NeoDesigns.Family.GlassTubes or NeoDesigns.Family.GlassDial or NeoDesigns.Family.ChromeBezel or NeoDesigns.Family.GlossPills:
                panel.CornerRadius = new CornerRadius(16);
                break;
        }
        return panel;
    }
}
