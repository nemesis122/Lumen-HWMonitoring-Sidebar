using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

/// <summary>
/// The ten 3D designs (5.5.0): iso skyline, glass cubes, perspective HUD, torus rings, exploded stack, chrome numbers,
/// 3D bar chart, hologram pads, glass cylinders and iso tower. They build on the studio designs: the same skeleton, a look
/// and a 3D layout of their own on top. Every number is a value bound to its sensor; the 3D drawings carry no numbers.
/// </summary>
public sealed partial class AioTile
{
    private bool Depth => Fam >= NeoDesigns.Family.Skyline && Fam < NeoDesigns.Family.Desk;

    private void DepthLook()
    {
        nScale = 1;
        nInk = lightSurface ? ink : Color.Parse(Fam switch
        {
            NeoDesigns.Family.Skyline or NeoDesigns.Family.Tilted or NeoDesigns.Family.Exploded => "#E8F6FB", NeoDesigns.Family.Bars3D => "#EAFFEF",
            NeoDesigns.Family.Hologram => "#EEF4FF", _ => "#F2F4F7"
        });
        nLabel = lightSurface ? muted : Fam is NeoDesigns.Family.Skyline or NeoDesigns.Family.Tilted or NeoDesigns.Family.Exploded or NeoDesigns.Family.Bars3D or NeoDesigns.Family.Hologram
            ? Neutralize(Palette.Mix(NA, Color.Parse("#8A8F96"), .6)) : Color.Parse("#8B939C");
        nTrack = lightSurface ? track : Palette.Mix(nSurface, Colors.White, .1);
        nPanel = Palette.Mix(nSurface, Colors.White, .04);
        FontFamily F(string key) => Ui.DesignFont(Ui.FamilyFor(key));
        (nFont, nValueFont, nBigFont) = Fam switch
        {
            NeoDesigns.Family.Skyline or NeoDesigns.Family.Cubes or NeoDesigns.Family.Exploded or NeoDesigns.Family.Bars3D or NeoDesigns.Family.Tower => (F("chakra-petch"), F("exo-2"), F("exo-2")),
            NeoDesigns.Family.Tilted => (F("share-tech-mono"), F("share-tech-mono"), Ui.DesignFont(Ui.Orbitron)),
            NeoDesigns.Family.Hologram => (F("chakra-petch"), F("exo-2"), Ui.DesignFont(Ui.Orbitron)),
            NeoDesigns.Family.Chrome => (Ui.DesignFont(Ui.Inter), Ui.DesignFont(Ui.Inter), F("anton")),
            _ => (F("chakra-petch"), F("exo-2"), F("exo-2"))
        };
        BorderBrush = Palette.Brush(Fam is NeoDesigns.Family.Skyline or NeoDesigns.Family.Tilted or NeoDesigns.Family.Exploded or NeoDesigns.Family.Bars3D or NeoDesigns.Family.Hologram or NeoDesigns.Family.Tower
            ? BlackDraw.A(NA, .2) : Palette.Mix(nSurface, Colors.White, .09));
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(Fam == NeoDesigns.Family.Tilted ? 6 : 14);
        Padding = new Thickness(10);
        content.Spacing = 10;
        if (Dash) DashLook();
    }

    /// <summary>The 3D layout on top; false for the designs that keep the panel skeleton (perspective HUD, chrome numbers).</summary>
    private bool DepthTop(List<(AioReading R, string Caption, string Art, int Colour)> items, HashSet<string> top)
    {
        if (items.Count == 0 || Fam is NeoDesigns.Family.Tilted or NeoDesigns.Family.Chrome) return false;
        if (Dash) return DashTop(items, top);
        string Cap(string c) => Upper ? c.ToUpperInvariant() : c;
        double g = settings.ActiveWidget?.Graphics ?? 1;
        Viewbox Fit3D(Control c, double max) => new() { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, MaxWidth = max, HorizontalAlignment = HorizontalAlignment.Center, Child = c };
        TextBlock Caption(string text, Color colour)
        {
            var t = NText(Cap(text), 10, colour, true); t.HorizontalAlignment = HorizontalAlignment.Center; t.TextTrimming = TextTrimming.CharacterEllipsis; t.LetterSpacing = 1.2; return t;
        }
        Control Centred(Control c) { c.HorizontalAlignment = HorizontalAlignment.Center; return c; }
        var large = items.Where(i => i.Art is "cpu" or "gpu").Take(2).ToList();
        var rest = items.Where(i => !large.Contains(i)).ToList();
        switch (Fam)
        {
            case NeoDesigns.Family.Skyline:
            {
                var city = new ArtSkyline(NA) { Height = 290 * g };
                foreach (var i in items) { int index = city.Add(NAcc(i.Colour), i.R.Fraction); watchers.Add((i.R.Key, row => city.Set(index, row?.Fraction))); }
                var tiles = items.Select(i => (Control)Stack(0, Caption(i.Caption, NAcc(i.Colour)), Centred(Fit(GValue(i.R, 20))))).ToArray();
                content.Children.Add(new Border { Tag = "aio-section", Child = Stack(6, city, NPanel(null, NGrid(tiles, Math.Min(5, tiles.Length), 6))) });
                break;
            }
            case NeoDesigns.Family.Cubes:
            {
                Control Cube((AioReading R, string Caption, string Art, int Colour) i, double size, double text)
                {
                    var colour = NAcc(i.Colour); double depth = size * ArtCube.Depth;
                    var face = Stack(0, Centred(Fit(GValue(i.R, text))), Caption(i.Caption, colour));
                    face.VerticalAlignment = VerticalAlignment.Center; face.Margin = new Thickness(4, depth, depth + 4, 0);
                    return Fit3D(new Grid { Width = size, Height = size, Children = { new ArtCube(colour), face } }, size);
                }
                var body = Stack(8);
                if (large.Count > 0) body.Children.Add(NGrid(large.Select(i => Cube(i, 120 * g, 30)), 2, 12));
                if (rest.Count > 0) body.Children.Add(NGrid(rest.Select(i => Cube(i, 78 * g, 18)), 3, 8));
                content.Children.Add(new Border { Tag = "aio-section", Padding = new Thickness(0, 4), Child = body });
                break;
            }
            case NeoDesigns.Family.Torus:
            {
                Control Ring((AioReading R, string Caption, string Art, int Colour) i, double width, double text)
                {
                    var colour = NAcc(i.Colour); var torus = new ArtTorus(colour, nTrack) { Width = width, Height = width * .56 };
                    torus.Set(i.R.Fraction); watchers.Add((i.R.Key, row => torus.Set(row?.Fraction)));
                    var value = Centred(Fit(GValue(i.R, text))); value.VerticalAlignment = VerticalAlignment.Center; value.Margin = new Thickness(width * .18, 0, width * .18, width * .06);
                    return Stack(2, Fit3D(new Grid { Width = width, Height = width * .56, Children = { torus, value } }, width), Caption(i.Caption, colour));
                }
                var body = Stack(10);
                if (large.Count > 0) body.Children.Add(NGrid(large.Select(i => Ring(i, 150 * g, 26)), 2, 12));
                if (rest.Count > 0) body.Children.Add(NGrid(rest.Select(i => Ring(i, 96 * g, 17)), 3, 8));
                content.Children.Add(new Border { Tag = "aio-section", Child = body });
                break;
            }
            case NeoDesigns.Family.Exploded:
            {
                var body = Stack(0);
                foreach (var i in items)
                {
                    var colour = NAcc(i.Colour);
                    var art = new Grid { Width = 190 * g, Height = 74 * g, Children = { new ArtSlab(colour, 124 * g) } };
                    var picture = Part(i.Art, 104 * g, 62 * g); picture.HorizontalAlignment = HorizontalAlignment.Left; picture.Margin = new Thickness(10 * g, 0, 0, 6 * g); art.Children.Add(picture);
                    var label = NText(Cap(i.Caption), 10, colour, true); label.HorizontalAlignment = HorizontalAlignment.Right; label.TextTrimming = TextTrimming.CharacterEllipsis; label.LetterSpacing = 1.2;
                    var value = Fit(GValue(i.R, 20)); value.HorizontalAlignment = HorizontalAlignment.Right;
                    var side = Stack(0, label, value); side.VerticalAlignment = VerticalAlignment.Center;
                    var row = Ui.Columns("Auto,*", new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, MaxWidth = 190 * g, Child = art }, side);
                    row.ColumnSpacing = 6; body.Children.Add(row);
                }
                content.Children.Add(new Border { Tag = "aio-section", Child = body });
                break;
            }
            case NeoDesigns.Family.Bars3D:
            {
                if (lastCoreLoads.Count >= 2 && AioReadings.Selected(settings, "aio.cpu.coreloads") && Rs("CPU").Length > 0)
                {
                    coreBars = new AioCoreBars(nInk, nLabel, nTrack) { Loads = lastCoreLoads, Tint = NA, Iso = true, Tag = "graphic" };
                    TextColours.Bind(coreBars, "aio.cpu.coreloads");
                    content.Children.Add(new Border { Tag = "aio-section", Child = coreBars });
                }
                var rows = items.Select(i =>
                {
                    var colour = NAcc(i.Colour);
                    var label = NText(Cap(i.Caption), 10.5, nLabel, true); label.VerticalAlignment = VerticalAlignment.Center; label.TextTrimming = TextTrimming.CharacterEllipsis;
                    var meter = new AioMeter(colour, nTrack, 0) { Height = 14, RenderTransform = new SkewTransform(-30, 0), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0) };
                    meters.Add((i.R.Key, meter));
                    var value = new AioReferenceValue(GValue(i.R, 17)) { HorizontalAlignment = HorizontalAlignment.Right };
                    var line = Ui.Columns("52,*,Auto", label, meter, value); line.ColumnSpacing = 8; return (Control)line;
                }).ToArray();
                content.Children.Add(NPanel(null, Stack(10, rows)));
                return true;
            }
            case NeoDesigns.Family.Hologram:
            {
                Control Pad((AioReading R, string Caption, string Art, int Colour) i, double width, double text)
                {
                    var colour = NAcc(i.Colour); double height = width * 1.42;
                    var picture = Part(i.Art, width * .56, width * .42); picture.VerticalAlignment = VerticalAlignment.Top; picture.Margin = new Thickness(0, height * .42, 0, 0); picture.Opacity = .8;
                    var head = Stack(0, Caption(i.Caption, colour), Centred(Fit(GValue(i.R, text)))); head.VerticalAlignment = VerticalAlignment.Top;
                    return Fit3D(new Grid { Width = width, Height = height, Children = { new ArtHoloPad(colour), picture, head } }, width);
                }
                var body = Stack(4);
                if (large.Count > 0) body.Children.Add(NGrid(large.Select(i => Pad(i, 140 * g, 34)), 2, 10));
                if (rest.Count > 0) body.Children.Add(NGrid(rest.Select(i => Pad(i, 96 * g, 22)), 3, 6));
                content.Children.Add(new Border { Tag = "aio-section", Child = body });
                break;
            }
            case NeoDesigns.Family.Cylinders:
            {
                var tanks = items.Select(i =>
                {
                    bool big = large.Contains(i);
                    var tank = new ArtCylinder(NAcc(i.Colour)) { Width = (big ? 54 : 42) * g, Height = (big ? 160 : 132) * g, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom };
                    tank.Set(i.R.Fraction); watchers.Add((i.R.Key, row => tank.Set(row?.Fraction)));
                    var holder = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Height = 160 * g, VerticalAlignment = VerticalAlignment.Bottom, Child = tank };
                    return (Control)Stack(6, holder, Centred(Fit(GValue(i.R, 18))), Caption(i.Caption, nLabel));
                }).ToArray();
                content.Children.Add(new Border { Tag = "aio-section", Padding = new Thickness(0, 4), Child = NGrid(tanks, Math.Min(5, tanks.Length), 8) });
                break;
            }
            case NeoDesigns.Family.Tower:
            {
                Control Tag3((AioReading R, string Caption, string Art, int Colour) i, bool left)
                {
                    var label = NText(Cap(i.Caption), 10, NAcc(i.Colour), true); label.TextTrimming = TextTrimming.CharacterEllipsis; label.LetterSpacing = 1.2;
                    var value = Fit(GValue(i.R, 18));
                    label.HorizontalAlignment = value.HorizontalAlignment = left ? HorizontalAlignment.Right : HorizontalAlignment.Left;
                    return Stack(0, label, value);
                }
                var leftSide = Stack(16, items.Where(i => i.Art is "cpu" or "ssd").Select(i => Tag3(i, true)).ToArray());
                var rightSide = Stack(16, items.Where(i => i.Art is not ("cpu" or "ssd")).Select(i => Tag3(i, false)).ToArray());
                leftSide.VerticalAlignment = rightSide.VerticalAlignment = VerticalAlignment.Center;
                var tower = new ArtTower(NAcc(0), NAcc(1), NAcc(2), NAcc(3)) { Width = 150 * g, Height = 230 * g };
                var scene = Ui.Columns("*,Auto,*", leftSide, new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, MaxWidth = 150 * g, Child = tower }, rightSide);
                scene.ColumnSpacing = 8;
                content.Children.Add(new Border { Tag = "aio-section", Padding = new Thickness(0, 6), Child = scene });
                break;
            }
            default: return false;
        }
        if (CyberCores() is { } cores) content.Children.Add(Shell(cores));
        return true;
    }

    /// <summary>Chrome numbers: a hard coloured shadow down and to the right, so the digits stand out like extruded metal.</summary>
    private Avalonia.Media.DropShadowEffect Extrusion(int colour) => new() { OffsetX = 3, OffsetY = 4, BlurRadius = 0, Color = Palette.Mix(NAcc(colour), Colors.Black, .25) };

    /// <summary>A panel of a 3D design with its heading.</summary>
    private Control DepthPanel(string? title, Control body, string? model, Color part)
    {
        Control? heading = null;
        if (title != null)
        {
            var name = NHeading(title, part); heading = name;
            if (!string.IsNullOrEmpty(model))
            {
                var device = NText(model, 11, nLabel, true); device.TextTrimming = TextTrimming.CharacterEllipsis;
                name.Text += " ·"; var row = Ui.Columns("Auto,*", name, device); row.ColumnSpacing = 6; heading = row;
            }
        }
        var inner = heading == null ? body : Stack(10, heading, body);
        if (Fam == NeoDesigns.Family.Tilted)
        {
            var lean = new ArtLean(part) { Effect = new Avalonia.Media.DropShadowEffect { BlurRadius = 14, OffsetX = 0, OffsetY = 0, Color = BlackDraw.A(part, .35) } };
            return new Border { Tag = "aio-section", Child = new Grid { Children = { lean, new Border { Padding = new Thickness(16, 12), Child = inner } } } };
        }
        var panel = new Border { Padding = new Thickness(14, 12), Child = inner, Tag = "aio-section", BorderThickness = new Thickness(1) };
        bool tinted = Fam is NeoDesigns.Family.Skyline or NeoDesigns.Family.Exploded or NeoDesigns.Family.Bars3D or NeoDesigns.Family.Hologram or NeoDesigns.Family.Tower;
        panel.CornerRadius = new CornerRadius(Fam is NeoDesigns.Family.Torus or NeoDesigns.Family.Cylinders or NeoDesigns.Family.Cubes ? 14 : 10);
        panel.BorderBrush = Palette.Brush(tinted ? BlackDraw.A(part, .2) : Palette.Mix(nSurface, Colors.White, .09));
        panel.Background = Palette.Brush(SeeThrough(tinted ? Palette.Mix(nSurface, part, .05) : Palette.Mix(nSurface, Colors.White, .035)));
        if (Dash) return DashPanel(panel, part);
        if (Fam == NeoDesigns.Family.Chrome) panel.BoxShadow = new BoxShadows(new BoxShadow { OffsetY = 1, Blur = 0, Color = Color.FromArgb(40, 255, 255, 255), IsInset = true });
        return panel;
    }
}
