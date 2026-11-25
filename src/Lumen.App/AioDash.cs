using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

/// <summary>
/// 5.6.0: four dashboards (race telemetry, control room, gauge wall, studio mixer, drafts D-06 … D-09) and Capsule Glass
/// (draft N-01). They build on the 3D designs: the same skeleton, a look and a layout of their own on top; the drawings
/// carry no numbers, every number is a value bound to its sensor.
/// </summary>
public sealed partial class AioTile
{
    private bool Dash => Fam >= NeoDesigns.Family.Race;

    private void DashLook()
    {
        FontFamily F(string key) => Ui.DesignFont(Ui.FamilyFor(key));
        (nFont, nValueFont, nBigFont) = Fam switch
        {
            NeoDesigns.Family.Race => (F("chakra-petch"), F("exo-2"), Ui.DesignFont(Ui.Orbitron)),
            NeoDesigns.Family.Scada => (F("share-tech-mono"), F("share-tech-mono"), F("share-tech-mono")),
            _ => (Ui.DesignFont(Ui.Inter), Ui.DesignFont(Ui.Inter), Ui.DesignFont(Ui.Inter))
        };
        nLabel = lightSurface ? muted : Color.Parse(Fam == NeoDesigns.Family.Scada ? "#9AA6B2" : "#8E949C");
        nInk = lightSurface ? ink : Color.Parse("#F2F4F6");
        CornerRadius = new CornerRadius(Fam switch { NeoDesigns.Family.Race => 18, NeoDesigns.Family.Scada => 6, NeoDesigns.Family.Capsule => 22, _ => 14 });
        BorderBrush = Palette.Brush(Palette.Mix(nSurface, Colors.White, .09));
    }

    private bool DashTop(List<(AioReading R, string Caption, string Art, int Colour)> items)
    {
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
            default: return false;
        }
        if (CyberCores() is { } cores) content.Children.Add(Shell(cores));
        return true;
    }

    /// <summary>The temperature that colours a part: its own for temperatures, otherwise its share of 30–85 °C.</summary>
    private static double? Celsius(AioReading? r) => r == null ? null
        : r.Key.EndsWith(".temp") || r.Key.StartsWith("aio.system.disk.") ? r.Number : r.Fraction is double f ? 30 + 55 * f : null;

    private Control DashPanel(Border panel, Color part)
    {
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
        }
        return panel;
    }
}
