using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

/// <summary>
/// The 100 gallery designs (5.3.0): the Cyberpunk/Neumorph builder with the colour world, fonts, panel style and pattern of
/// each design, and six layouts of their own on top (rings in panels, tiles, gauges, columns, ticker rows, orbit).
/// </summary>
public sealed partial class AioTile
{
    private GalleryDesigns.Design? gal;
    private FontFamily nBigFont = Ui.Inter;
    private bool GalleryLayout(GalleryDesigns.Layout layout) => gal?.Arrangement == layout;

    private static FontFamily GalleryFont(string key) => key switch
    {
        "playfair" => Ui.Playfair, "doto" => Ui.Doto, "dseg" => Ui.Dseg, _ => Ui.FamilyFor(key)
    };

    /// <summary>A grey with a hint of the colour: a caption colour that a chosen label colour still replaces.</summary>
    private static Color Neutralize(Color c)
    {
        double grey = (c.R * .3 + c.G * .59 + c.B * .11);
        var g = Color.FromRgb((byte)grey, (byte)grey, (byte)grey);
        for (double t = 0; t <= 1; t += .1)
        {
            var m = Palette.Mix(c, g, t);
            if (TextColours.Neutral(m)) return m;
        }
        return g;
    }

    private void GalleryLook()
    {
        var g = gal!;
        nInk = lightSurface ? ink : Color.Parse(g.World.Ink);
        nLabel = lightSurface ? muted : Neutralize(Color.Parse(g.World.Muted));
        nTrack = lightSurface ? track : Palette.Mix(nSurface, Colors.White, .09);
        nPanel = Palette.Mix(nSurface, Colors.White, .045);
        nFont = Ui.DesignFont(GalleryFont(g.World.LabelFont));
        nValueFont = Ui.DesignFont(GalleryFont(g.World.ValueFont));
        nBigFont = g.Arrangement == GalleryDesigns.Layout.Segments ? Ui.DesignFont(Ui.Dseg) : nValueFont;
        BorderBrush = Palette.Brush(BlackDraw.A(NA, g.Look is GalleryDesigns.Panel.Glow or GalleryDesigns.Panel.Outline ? .3 : .14));
        CornerRadius = new CornerRadius(g.Look switch
        {
            GalleryDesigns.Panel.Pill => 30, GalleryDesigns.Panel.Glass => 22,
            GalleryDesigns.Panel.Bevel or GalleryDesigns.Panel.Cut or GalleryDesigns.Panel.Double or GalleryDesigns.Panel.Underline => 6, _ => 16
        });
        Padding = new Thickness(10);
        content.Spacing = g.Look == GalleryDesigns.Panel.Underline ? 6 : 10;
    }

    /// <summary>The layouts of their own on top: each takes the parts shown large (processor, graphics, memory, drives).</summary>
    private bool GalleryTop(List<(AioReading R, string Caption, string Art, int Colour)> items, HashSet<string> top)
    {
        if (gal == null || gal.Arrangement is not (GalleryDesigns.Layout.Rings or GalleryDesigns.Layout.Tiles or GalleryDesigns.Layout.Gauges
            or GalleryDesigns.Layout.Columns or GalleryDesigns.Layout.Ticker or GalleryDesigns.Layout.Orbit)) return false;
        string Cap(string c) => Upper ? c.ToUpperInvariant() : c;
        switch (gal.Arrangement)
        {
            case GalleryDesigns.Layout.Rings:
            {
                var dials = items.Select(i => NPanel(null, NDial(i.R, Cap(i.Caption), NAcc(i.Colour), 118), null, i.Colour)).ToArray();
                content.Children.Add(new Border { Tag = "aio-section", Child = NGrid(dials, 2, 10) });
                break;
            }
            case GalleryDesigns.Layout.Tiles:
            {
                var tiles = items.Select((i, n) => GTile(i.R, Cap(i.Caption), NAcc(i.Colour), n < 2 ? 104 : 76)).ToArray();
                content.Children.Add(new Border { Tag = "aio-section", Child = NGrid(tiles.Take(2), 2, 10) });
                if (tiles.Length > 2) content.Children.Add(new Border { Tag = "aio-section", Child = NGrid(tiles.Skip(2), 3, 8) });
                break;
            }
            case GalleryDesigns.Layout.Gauges:
            {
                var gauges = items.Select(i => NPanel(null, GGauge(i.R, Cap(i.Caption), NAcc(i.Colour)), null, i.Colour)).ToArray();
                content.Children.Add(new Border { Tag = "aio-section", Child = NGrid(gauges, 2, 10) });
                break;
            }
            case GalleryDesigns.Layout.Columns:
                content.Children.Add(NPanel(null, GColumns(items.Select(i => (i.R, Cap(i.Caption), NAcc(i.Colour))).ToArray())));
                break;
            case GalleryDesigns.Layout.Ticker:
                foreach (var i in items) content.Children.Add(NPanel(null, GTicker(i.R, Cap(i.Caption), NAcc(i.Colour)), null, i.Colour));
                break;
            case GalleryDesigns.Layout.Orbit:
            {
                var load = R("load");
                if (load != null) top.Add(load.Key);
                content.Children.Add(new Border { Tag = "aio-section", Child = GOrbit(items.Select(i => (i.R, Cap(i.Caption), NAcc(i.Colour))).ToArray(), load) });
                break;
            }
        }
        if (CyberCores() is { } cores) content.Children.Add(Shell(cores));
        return true;
    }

    /// <summary>Text of a value shown large: temperature with its unit, memory as a percentage, else as the reading reads.</summary>
    private TextBlock GValue(AioReading r, double size, bool unit = true)
    {
        var t = NV(r, size); t.FontFamily = nBigFont; t.TextWrapping = TextWrapping.NoWrap;
        if (r.Key == "aio.system.ram") { formats[t] = row => Format.Percent(row.Number); t.Text = formats[t](r); }
        else if (r.Key.EndsWith(".temp") || r.Key.StartsWith("aio.system.disk."))
        {
            formats[t] = row => Format.Temperature(row.Number, settings.Fahrenheit) + (unit ? "°" : ""); t.Text = formats[t](r);
        }
        if (Glowing) t.Effect = Glow(NA, 10);
        else if (Fam == NeoDesigns.Family.Chrome) t.Effect = Extrusion(0);
        return t;
    }

    private Control GTile(AioReading r, string caption, Color colour, double height)
    {
        double size = height > 90 ? 34 : 24;
        height *= settings.ActiveWidget?.Graphics ?? 1;
        var label = NText(caption, 10.5, colour, true); label.TextTrimming = TextTrimming.CharacterEllipsis;
        var value = new Border { VerticalAlignment = VerticalAlignment.Bottom, Child = FitLeft(GValue(r, size)) };
        var face = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), Height = Math.Max(40, height - 20), Children = { label, value } };
        Grid.SetRow(value, 1);
        return NPanel(null, face, null, Array.IndexOf(nAcc, colour) is var i and >= 0 ? i : 0);
    }

    private Control GGauge(AioReading r, string caption, Color colour)
    {
        double size = 130 * (settings.ActiveWidget?.Graphics ?? 1);
        var gauge = new ArtGauge(colour, nTrack) { Width = size, Height = size / 2 + 6 };
        gauge.Set(r.Fraction); watchers.Add((r.Key, row => gauge.Set(row?.Fraction)));
        var value = GValue(r, 24); value.HorizontalAlignment = HorizontalAlignment.Center;
        var label = NText(caption, 10, nLabel, true); label.HorizontalAlignment = HorizontalAlignment.Center;
        var shrink = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, MaxWidth = size, HorizontalAlignment = HorizontalAlignment.Center, Child = gauge, ClipToBounds = false };
        return Stack(2, shrink, Fit(value), label);
    }

    private Control GColumns((AioReading R, string Caption, Color Colour)[] items)
    {
        double height = 120 * (settings.ActiveWidget?.Graphics ?? 1);
        var grid = new Grid { ColumnSpacing = 6 };
        for (int i = 0; i < items.Length; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var (r, caption, colour) = items[i];
            var column = new ArtColumn(colour, nTrack) { Height = height, Margin = new Thickness(3, 0) };
            column.Set(r.Fraction); watchers.Add((r.Key, row => column.Set(row?.Fraction)));
            var value = Fit(GValue(r, 15));
            var label = NText(caption, 9.5, nLabel, true); label.HorizontalAlignment = HorizontalAlignment.Center; label.TextTrimming = TextTrimming.CharacterEllipsis;
            var cell = Stack(4, value, column, label);
            Grid.SetColumn(cell, i); grid.Children.Add(cell);
        }
        return grid;
    }

    private Control GTicker(AioReading r, string caption, Color colour)
    {
        var name = NText(caption, 12, colour, true); name.TextTrimming = TextTrimming.CharacterEllipsis; name.VerticalAlignment = VerticalAlignment.Center;
        Control plot = AioReadings.Graphs(settings, r.Key) ? HistoryPlot(r, 30, "area", colour) : new Border();
        var value = new AioReferenceValue(GValue(r, 22)) { VerticalAlignment = VerticalAlignment.Center };
        var row = Ui.Columns("58,*,86", name, new Border { VerticalAlignment = VerticalAlignment.Center, Child = plot }, value); row.ColumnSpacing = 8;
        return row;
    }

    /// <summary>The processor load in the centre, the parts on three orbits around it.</summary>
    private Control GOrbit((AioReading R, string Caption, Color Colour)[] items, AioReading? load)
    {
        const double W = 320, H = 300; double cx = W / 2, cy = H / 2 - 6;
        double[] radii = { 58, 96, 134 };
        var canvas = new Canvas { Width = W, Height = H };
        canvas.Children.Add(new ArtOrbits(radii, nAcc) { Width = W, Height = H - 12 });
        int Orbit(int i) => i == 0 ? 0 : items[i].R.Key.StartsWith("aio.drives.") || items[i].R.Key.StartsWith("aio.system.disk.") ? 2 : 1;
        var groups = Enumerable.Range(0, items.Length).GroupBy(Orbit).ToDictionary(g => g.Key, g => g.ToArray());
        double[] start = { -60, 20, 110 };
        foreach (var (orbit, members) in groups)
            for (int k = 0; k < members.Length; k++)
            {
                var (r, caption, colour) = items[members[k]];
                double a = (start[orbit] + k * 360.0 / members.Length) * Math.PI / 180;
                double x = cx + radii[orbit] * Math.Cos(a), y = cy + radii[orbit] * Math.Sin(a);
                var value = Fit(GValue(r, 13));
                var planet = new Border
                {
                    Width = 46, Height = 46, CornerRadius = new CornerRadius(23), BorderThickness = new Thickness(2), BorderBrush = Palette.Brush(colour),
                    Background = Palette.Brush(SeeThrough(nSurface)), Padding = new Thickness(5, 0), Child = new Border { VerticalAlignment = VerticalAlignment.Center, Child = value },
                    BoxShadow = new BoxShadows(new BoxShadow { Blur = 10, Color = BlackDraw.A(colour, .45) })
                };
                Canvas.SetLeft(planet, x - 23); Canvas.SetTop(planet, y - 23); canvas.Children.Add(planet);
                var label = NText(caption, 9.5, nLabel, true); label.Width = 60; label.TextAlignment = TextAlignment.Center; label.TextTrimming = TextTrimming.CharacterEllipsis;
                Canvas.SetLeft(label, x - 30); Canvas.SetTop(label, y + 25); canvas.Children.Add(label);
            }
        if (load != null)
        {
            var value = Fit(NV(load, 14));
            var sun = new Border
            {
                Width = 60, Height = 60, CornerRadius = new CornerRadius(30), BorderThickness = new Thickness(1.5), BorderBrush = Palette.Brush(NA),
                Background = Palette.Brush(SeeThrough(Palette.Mix(nSurface, NA, .14))), Padding = new Thickness(6, 0), Child = new Border { VerticalAlignment = VerticalAlignment.Center, Child = value }
            };
            Canvas.SetLeft(sun, cx - 30); Canvas.SetTop(sun, cy - 30); canvas.Children.Add(sun);
        }
        return new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, MaxWidth = W, HorizontalAlignment = HorizontalAlignment.Center, Child = canvas, ClipToBounds = false };
    }

    /// <summary>A panel in the gallery design's style, with its background pattern.</summary>
    private Control GalleryPanel(Control inner, Color part)
    {
        var g = gal!;
        var panel = new Border { Padding = new Thickness(14, 10), Child = inner };
        Control result = panel;
        switch (g.Look)
        {
            case GalleryDesigns.Panel.Flat: panel.CornerRadius = new CornerRadius(12); panel.Background = Palette.Brush(SeeThrough(nPanel)); break;
            case GalleryDesigns.Panel.Outline: panel.CornerRadius = new CornerRadius(10); panel.BorderThickness = new Thickness(1); panel.BorderBrush = Palette.Brush(BlackDraw.A(part, .45)); break;
            case GalleryDesigns.Panel.Glass:
                panel.CornerRadius = new CornerRadius(16); panel.BorderThickness = new Thickness(1); panel.BorderBrush = Palette.Brush(Color.FromArgb(34, 255, 255, 255));
                panel.Background = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                    GradientStops = { new(SeeThrough(Palette.Mix(nSurface, Colors.White, .09)), 0), new(SeeThrough(Palette.Mix(nSurface, Colors.White, .02)), 1) }
                };
                break;
            case GalleryDesigns.Panel.Glow:
                panel.CornerRadius = new CornerRadius(12); panel.BorderThickness = new Thickness(1.5); panel.BorderBrush = Palette.Brush(part);
                panel.Background = Palette.Brush(SeeThrough(Palette.Mix(nSurface, part, .04)));
                panel.BoxShadow = new BoxShadows(new BoxShadow { Blur = 14, Color = BlackDraw.A(part, .4) }, new[] { new BoxShadow { Blur = 10, Color = BlackDraw.A(part, .14), IsInset = true } });
                break;
            case GalleryDesigns.Panel.Bevel:
                panel.CornerRadius = new CornerRadius(10);
                panel.Background = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                    GradientStops = { new(SeeThrough(Palette.Mix(nSurface, Colors.White, .08)), 0), new(SeeThrough(Palette.Mix(nSurface, Colors.White, .02)), 1) }
                };
                panel.BoxShadow = new BoxShadows(new BoxShadow { OffsetY = 1, Color = Color.FromArgb(28, 255, 255, 255), IsInset = true },
                    new[] { new BoxShadow { OffsetY = 5, Blur = 12, Color = Color.FromArgb(170, 0, 0, 0) } });
                break;
            case GalleryDesigns.Panel.Cut:
            {
                panel.BorderThickness = new Thickness(3, 0, 0, 0); panel.BorderBrush = Palette.Brush(part);
                panel.Background = Palette.Brush(SeeThrough(Palette.Mix(nSurface, part, .07)));
                var notch = new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse("M0,0 L14,0 L14,14 Z"), Fill = Palette.Brush(SeeThrough(nSurface)), Width = 14, Height = 14,
                    HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
                result = new Grid { Children = { panel, notch } };
                break;
            }
            case GalleryDesigns.Panel.Pill: panel.CornerRadius = new CornerRadius(24); panel.Background = Palette.Brush(SeeThrough(Palette.Mix(nSurface, Colors.White, .055))); panel.Padding = new Thickness(18, 12); break;
            case GalleryDesigns.Panel.Underline: panel.BorderThickness = new Thickness(0, 0, 0, 1); panel.BorderBrush = Palette.Brush(BlackDraw.A(part, .4)); panel.Padding = new Thickness(4, 8, 4, 10); break;
            case GalleryDesigns.Panel.Double:
                panel.CornerRadius = new CornerRadius(2); panel.BorderThickness = new Thickness(1); panel.BorderBrush = Palette.Brush(BlackDraw.A(part, .35));
                result = new Border { CornerRadius = new CornerRadius(4), BorderThickness = new Thickness(1), BorderBrush = Palette.Brush(BlackDraw.A(part, .6)), Padding = new Thickness(2), Child = panel };
                break;
            case GalleryDesigns.Panel.Dashed:
            {
                panel.CornerRadius = new CornerRadius(8);
                var dash = new Rectangle { Stroke = Palette.Brush(BlackDraw.A(part, .5)), StrokeThickness = 1, StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 4, 3 }, RadiusX = 8, RadiusY = 8, IsHitTestVisible = false };
                result = new Grid { Children = { dash, panel } };
                break;
            }
        }
        if (g.Backdrop != GalleryDesigns.Pattern.None)
        {
            var pattern = new ArtPattern(g.Backdrop switch
            {
                GalleryDesigns.Pattern.Grid => ArtPattern.GalleryKind.Grid, GalleryDesigns.Pattern.Scan => ArtPattern.GalleryKind.Scan,
                GalleryDesigns.Pattern.Glow => ArtPattern.GalleryKind.Glow, GalleryDesigns.Pattern.Dots => ArtPattern.GalleryKind.Dots, _ => ArtPattern.GalleryKind.Stripes
            }, part);
            result = new Grid { Children = { pattern, result } };
        }
        result.Tag = "aio-section";
        return result;
    }
}
