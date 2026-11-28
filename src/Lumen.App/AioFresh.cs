using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

/// <summary>
/// 5.9.0: seven designs from the X drafts: Liquid Metal (X-01), Power Flow (X-02), Holo Cards (X-05), Loadout (X-06),
/// Ambilight (X-07), Cover Story (X-08) and Lava Lamp (X-10). They build on the dashboard skeleton: a top of their own, the
/// part panels below in the design's dress. Every number is a value bound to its sensor; the drawings carry no numbers.
/// </summary>
public sealed partial class AioTile
{
    private bool Fresh => Fam >= NeoDesigns.Family.Metal && Fam <= NeoDesigns.Family.Liquid;

    private (FontFamily Label, FontFamily Value, FontFamily Big) FreshFonts()
    {
        FontFamily F(string key) => Ui.DesignFont(Ui.FamilyFor(key));
        return Fam switch
        {
            NeoDesigns.Family.Metal => (Ui.DesignFont(Ui.Orbitron), Ui.DesignFont(Ui.Orbitron), Ui.DesignFont(Ui.Orbitron)),
            NeoDesigns.Family.Flow or NeoDesigns.Family.Loadout => (F("rajdhani"), F("rajdhani"), F("rajdhani")),
            NeoDesigns.Family.Holo or NeoDesigns.Family.Lava or NeoDesigns.Family.Liquid => (F("exo-2"), F("exo-2"), F("exo-2")),
            NeoDesigns.Family.Cover => (Ui.DesignFont(Ui.Inter), Ui.DesignFont(Ui.Inter), F("bebas-neue")),
            _ => (Ui.DesignFont(Ui.Inter), Ui.DesignFont(Ui.Inter), Ui.DesignFont(Ui.Inter))
        };
    }

    private bool FreshTop(List<(AioReading R, string Caption, string Art, int Colour)> items, HashSet<string> top)
    {
        string Cap(string c) => Upper ? c.ToUpperInvariant() : c;
        double g = settings.ActiveWidget?.Graphics ?? 1;
        var large = items.Where(i => i.Art is "cpu" or "gpu").Take(2).ToList();
        TextBlock Caption(string text, Color colour, double size = 10.5, double spacing = 1.6)
        {
            var t = NText(Cap(text), size, colour, true); t.TextTrimming = TextTrimming.CharacterEllipsis; t.LetterSpacing = spacing; return t;
        }
        Control Centred(Control c) { c.HorizontalAlignment = HorizontalAlignment.Center; return c; }
        void Heat(AioReading r, Action<double?> set) { set(Celsius(r)); watchers.Add((r.Key, row => set(Celsius(row)))); }

        switch (Fam)
        {
            case NeoDesigns.Family.Metal:
            {
                // Liquid metal: silver part, its reflection, the number in large; a mercury groove along the reading.
                var rows = items.Select(i =>
                {
                    bool big = large.Contains(i); double pw = (big ? 108 : 58) * g, ph = (big ? 80 : 42) * g;
                    var picture = Part(i.Art, pw, ph);
                    var mirror = Part(i.Art, pw, ph); mirror.RenderTransform = new ScaleTransform(1, -1); mirror.VerticalAlignment = VerticalAlignment.Top;
                    var reflection = new Border
                    {
                        Height = ph * .32, ClipToBounds = true, Opacity = .26, Tag = "graphic", Child = mirror,
                        OpacityMask = BlackDraw.V(Colors.Black, Color.FromArgb(0, 0, 0, 0))
                    };
                    var art = new StackPanel { Width = pw, Children = { new Border { Height = ph, Child = picture }, reflection }, VerticalAlignment = VerticalAlignment.Center, Tag = "graphic" };
                    var value = FitLeft(GValue(i.R, big ? 50 : 26));
                    var text = Stack(0, Caption(i.Caption, nLabel, big ? 11.5 : 10, 2.4), value); text.VerticalAlignment = VerticalAlignment.Center;
                    var head = Ui.Columns("Auto,*", art, text); head.ColumnSpacing = 14;
                    var groove = new ArtMetalTube { Height = big ? 12 : 9 }; groove.Set(i.R.Fraction); watchers.Add((i.R.Key, row => groove.Set(row?.Fraction)));
                    return (Control)Stack(6, head, groove);
                }).ToArray();
                content.Children.Add(new Border { Tag = "aio-section", Child = Stack(14, rows) });
                break;
            }
            case NeoDesigns.Family.Flow:
            {
                // Power flow: a band from the source to every part, as thick as the part's watts; parts without a power sensor get a fine line.
                var power = items.Select(i => i.Art switch
                {
                    "cpu" => R("power"),
                    "gpu" => R("power", "GPU", i.R.Device),
                    _ => null
                }).ToArray();
                double row = 62 * g;
                var flow = new ArtPowerFlow(items.Select(i => NAcc(i.Colour)).ToArray(), power.Select(p => p != null).ToArray()) { Height = row * items.Count };
                var nodes = items.Select((i, k) =>
                {
                    var picture = Part(i.Art, 50 * g, 40 * g);
                    var text = Stack(0, Caption(i.Caption, NAcc(i.Colour), 10, 1.4), FitLeft(GValue(i.R, 20)));
                    if (power[k] is { } p)
                    {
                        top.Add(p.Key);
                        flow.Set(k, p.Number); int at = k; watchers.Add((p.Key, r => flow.Set(at, r?.Number)));
                        text.Children.Add(FitLeft(GValue(p, 14)));
                    }
                    text.VerticalAlignment = VerticalAlignment.Center;
                    var node = Ui.Columns("Auto,*", new Border { Width = 50 * g, Child = picture }, text); node.ColumnSpacing = 6;
                    return (Control)new Border { Height = row, Child = node };
                }).ToArray();
                var source = Stack(4, Part("psu", 66 * g, 60 * g), Centred(Caption("POWER", nLabel, 10, 2)));
                source.VerticalAlignment = VerticalAlignment.Center;
                var layout = Ui.Columns("Auto,*,1.6*", source, flow, Stack(0, nodes)); layout.ColumnSpacing = 4;
                var title = Caption("POWER FLOW", nLabel, 11, 3); TextFonts.Mark(title, TextRole.Title);
                content.Children.Add(new Border { Tag = "aio-section", Child = Stack(8, title, layout) });
                break;
            }
            case NeoDesigns.Family.Holo:
            {
                // Holo cards: every part a collectible card in a rainbow foil frame; processor and graphics as wide cards.
                Control Card((AioReading R, string Caption, string Art, int Colour) i, bool wide)
                {
                    var head = Ui.Columns("*,Auto", Caption(i.Caption, nInk, wide ? 12 : 10.5, 2), new AioReferenceValue(GValue(i.R, wide ? 28 : 20)) { HorizontalAlignment = HorizontalAlignment.Right });
                    head.ColumnSpacing = 6; ((Control)head.Children[0]).VerticalAlignment = VerticalAlignment.Center;
                    double ah = (wide ? 104 : 70) * g;
                    var art = new Border
                    {
                        Height = ah, CornerRadius = new CornerRadius(10), Tag = "graphic", Child = Part(i.Art, ah * 1.5, ah * .92),
                        Background = new RadialGradientBrush { GradientStops = { new(SeeThrough(Color.Parse("#1D2540")), 0), new(SeeThrough(Color.Parse("#0A0C14")), 1) } }
                    };
                    var bar = new ArtHoloBar(); bar.Set(i.R.Fraction); watchers.Add((i.R.Key, row => bar.Set(row?.Fraction)));
                    var inner = new Grid
                    {
                        Children =
                        {
                            new Border { CornerRadius = new CornerRadius(13), Background = BlackDraw.D((SeeThrough(Color.Parse("#141824")), 0), (SeeThrough(nSurface), .6)) },
                            new ArtHoloSheen(),
                            new Border { Padding = new Thickness(10, 8, 10, 10), Child = Stack(6, head, art, bar) }
                        }
                    };
                    return new Border { CornerRadius = new CornerRadius(15), Padding = new Thickness(2), Background = ArtHoloBar.Foil(), Child = inner,
                        BoxShadow = new BoxShadows(new BoxShadow { Blur = 18, Color = Color.FromArgb(40, 120, 180, 255) }) };
                }
                var body = Stack(10, large.Select(i => Card(i, true)).ToArray());
                var others = items.Where(i => !large.Contains(i)).Select(i => Card(i, false)).ToArray();
                if (others.Length > 0) body.Children.Add(NGrid(others, Math.Min(2, others.Length), 10));
                content.Children.Add(new Border { Tag = "aio-section", Child = body });
                break;
            }
            case NeoDesigns.Family.Loadout:
            {
                // Loadout: the first part equipped in a frame of its temperature colour, every other part in an inventory slot.
                var title = Caption("LOADOUT", nInk, 15, 4); TextFonts.Mark(title, TextRole.Title);
                var body = Stack(10, title);
                var lead = items[0];
                var frame = new ArtLoadoutFrame(); var stat = new ArtSegmentStat(HeatColour.Of(Celsius(lead.R)));
                var tag = Caption("EQUIPPED", HeatColour.Of(Celsius(lead.R)), 10, 2.4);
                Heat(lead.R, c => { frame.Set(c); stat.Set(lead.R.Fraction, HeatColour.Of(c)); tag.Foreground = Palette.Brush(HeatColour.Of(c)); });
                watchers.Add((lead.R.Key, row => stat.Set(row?.Fraction, HeatColour.Of(Celsius(row)))));
                var text = Stack(0, tag, Caption(lead.Caption, nInk, 22, 1), FitLeft(GValue(lead.R, 46))); text.VerticalAlignment = VerticalAlignment.Center;
                var featured = Ui.Columns("Auto,*", new Border { Width = 128 * g, Child = Part(lead.Art, 128 * g, 96 * g) }, text); featured.ColumnSpacing = 10;
                body.Children.Add(new Grid { Children = { frame, new Border { Padding = new Thickness(10), Child = Stack(10, featured, stat) } } });
                var slots = items.Skip(1).Select(i =>
                {
                    var slot = new Border
                    {
                        BorderThickness = new Thickness(1, 1, 1, 3), Padding = new Thickness(6, 8, 6, 6),
                        Background = BlackDraw.V(SeeThrough(Color.Parse("#0D1019")), SeeThrough(Color.Parse("#07080C"))),
                        Child = Stack(2, new Border { Height = 46 * g, Child = Part(i.Art, 64 * g, 46 * g), Tag = "graphic" }, Centred(Caption(i.Caption, nLabel, 10, 1.6)), Centred(Fit(GValue(i.R, 19))))
                    };
                    Heat(i.R, c => slot.BorderBrush = Palette.Brush(HeatColour.Of(c)));
                    return (Control)slot;
                }).ToArray();
                if (slots.Length > 0) body.Children.Add(NGrid(slots, Math.Min(3, slots.Length), 8));
                content.Children.Add(new Border { Tag = "aio-section", Child = body });
                break;
            }
            case NeoDesigns.Family.Ambi:
            {
                // Ambilight: every part a dark card lit from its left edge in the colour of its temperature.
                var cards = items.Select(i =>
                {
                    bool big = large.Contains(i);
                    var light = new ArtAmbilight(); Heat(i.R, light.Set);
                    var label = Caption(i.Caption, nLabel, 11, 2.2);
                    Heat(i.R, c => label.Foreground = Palette.Brush(HeatColour.Of(c)));
                    var text = Stack(0, label, FitLeft(GValue(i.R, big ? 50 : 34))); text.VerticalAlignment = VerticalAlignment.Center;
                    var picture = Part(i.Art, (big ? 120 : 84) * g, (big ? 84 : 52) * g);
                    var row = Ui.Columns("*,Auto", text, picture); row.ColumnSpacing = 8;
                    return (Control)new Border
                    {
                        CornerRadius = new CornerRadius(14), ClipToBounds = true, BorderThickness = new Thickness(1), BorderBrush = Palette.Brush(Color.Parse("#12161C")),
                        Background = Palette.Brush(SeeThrough(Color.Parse("#07090C"))),
                        Child = new Grid { Children = { light, new Border { Padding = new Thickness(22, 10, 12, 10), Child = row } } }
                    };
                }).ToArray();
                content.Children.Add(new Border { Tag = "aio-section", Child = Stack(10, cards) });
                break;
            }
            case NeoDesigns.Family.Cover:
            {
                // Cover story: a magazine cover; the first part's number set huge in a serif, its picture beside it, the second large
                // part as a cover line, every other part in the contents underneath.
                var red = NAcc(0);
                var masthead = NText("LUMEN", 72, red, false); masthead.FontFamily = Ui.DesignFont(Ui.FamilyFor("bebas-neue")); masthead.FontWeight = FontWeight.Normal; masthead.LineHeight = 70;
                TextFonts.Mark(masthead, TextRole.Title);
                var issue = Caption("LIVE · YOUR PC", nLabel, 9.5, 3);
                var lead = items[0];
                var huge = GValue(lead.R, 108); huge.FontFamily = Ui.DesignFont(Ui.Playfair); huge.FontWeight = FontWeight.Normal;
                var picture = Part(lead.Art, 190 * g, 150 * g); picture.HorizontalAlignment = HorizontalAlignment.Right; picture.VerticalAlignment = VerticalAlignment.Top; picture.Margin = new Thickness(0, 6, -6, 0);
                var number = FitLeft(huge); number.Margin = new Thickness(0, 24 * g, 90 * g, 0);
                var hero = new Grid { Children = { picture, number } };
                var kicker = Caption("COVER STORY", red, 10, 3);
                var headline = NText(lead.Caption, 28, nInk, false); headline.FontFamily = Ui.DesignFont(Ui.Playfair); headline.FontWeight = FontWeight.Normal; headline.TextTrimming = TextTrimming.CharacterEllipsis;
                var story = Stack(2, kicker, headline);
                var second = large.FirstOrDefault(i => !ReferenceEquals(i.R, lead.R));
                Control lower = story;
                if (second.R != null)
                {
                    var line = Stack(0, new AioReferenceValue(GValue(second.R, 34)) { HorizontalAlignment = HorizontalAlignment.Right }, Caption(second.Caption, nLabel, 10, 1.4));
                    ((Control)line.Children[1]).HorizontalAlignment = HorizontalAlignment.Right;
                    lower = Ui.Columns("*,Auto", story, line);
                }
                var cover = new Border
                {
                    Padding = new Thickness(12, 10, 12, 12), CornerRadius = new CornerRadius(4),
                    Background = new RadialGradientBrush { Center = new RelativePoint(.62, .5, RelativeUnit.Relative), GradientOrigin = new RelativePoint(.62, .5, RelativeUnit.Relative),
                        GradientStops = { new(SeeThrough(Color.Parse("#2A0B0E")), 0), new(SeeThrough(nSurface), 1) } },
                    Child = Stack(4, masthead, issue, hero, lower, new Border { HorizontalAlignment = HorizontalAlignment.Right, Background = Brushes.White, Padding = new Thickness(5, 4), Child = new ArtBarcode() })
                };
                var body = Stack(10, cover);
                var rest = items.Where(i => !ReferenceEquals(i.R, lead.R) && !ReferenceEquals(i.R, second.R)).ToList();
                if (rest.Count > 0)
                {
                    body.Children.Add(Caption("INSIDE THIS ISSUE", red, 10, 3));
                    foreach (var i in rest)
                    {
                        var name = NText(i.Caption, 15, nInk, true); name.TextTrimming = TextTrimming.CharacterEllipsis; name.VerticalAlignment = VerticalAlignment.Center;
                        var dash = new Border { Width = 12, Height = 3, Background = Palette.Brush(red), VerticalAlignment = VerticalAlignment.Center, Tag = "graphic" };
                        var entry = Ui.Columns("Auto,*,Auto", dash, name, new AioReferenceValue(GValue(i.R, 24)) { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center });
                        entry.ColumnSpacing = 10;
                        body.Children.Add(new Border { BorderThickness = new Thickness(0, 1, 0, 0), BorderBrush = Palette.Brush(Color.Parse("#222222")), Padding = new Thickness(0, 6, 0, 2), Child = entry });
                    }
                }
                content.Children.Add(new Border { Tag = "aio-section", Child = body });
                break;
            }
            case NeoDesigns.Family.Lava:
            {
                // Lava lamps: one lamp per part, the lava rising with the reading in the colour of the temperature.
                var lamps = items.Select((i, k) =>
                {
                    bool big = large.Contains(i);
                    var lamp = new ArtLavaLamp(k * 7 + 3) { Width = (big ? 54 : 46) * g, Height = (big ? 200 : 180) * g, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom };
                    lamp.Set(i.R.Fraction, Celsius(i.R)); watchers.Add((i.R.Key, row => lamp.Set(row?.Fraction, Celsius(row))));
                    var holder = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Height = 200 * g, VerticalAlignment = VerticalAlignment.Bottom, Child = lamp };
                    var value = Centred(Fit(GValue(i.R, big ? 20 : 17)));
                    return (Control)Stack(6, holder, value, Centred(Caption(i.Caption, nLabel, 10, 1.4)));
                }).ToArray();
                content.Children.Add(new Border { Tag = "aio-section", Padding = new Thickness(0, 6), Child = NGrid(lamps, Balanced(lamps.Length), 6) });
                break;
            }
            case NeoDesigns.Family.Liquid:
            {
                // Liquid Glass (animated): a glass tube per part, filled up to the reading in the part's colour; the surface
                // waves and bubbles rise. Value above, part and name below.
                var tubes = items.Select((i, k) =>
                {
                    bool big = large.Contains(i); var colour = NAcc(i.Colour);
                    var tube = new ArtLiquidTube(colour, k) { Width = (big ? 44 : 40) * g, Height = (big ? 210 : 190) * g, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom };
                    tube.Set(i.R.Fraction); watchers.Add((i.R.Key, row => tube.Set(row?.Fraction)));
                    var holder = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Height = 210 * g, VerticalAlignment = VerticalAlignment.Bottom, Child = tube };
                    var value = Centred(Fit(GValue(i.R, big ? 20 : 17)));
                    var picture = Centred(Part(i.Art, 46 * g, 34 * g));
                    return (Control)Stack(5, value, holder, picture, Centred(Caption(i.Caption, colour, 10, 1.4)));
                }).ToArray();
                content.Children.Add(new Border { Tag = "aio-section", Padding = new Thickness(0, 6), Child = NGrid(tubes, Balanced(tubes.Length), 6) });
                break;
            }
            default: return false;
        }
        if (CyberCores() is { } cores) content.Children.Add(Shell(cores));
        return true;
    }

    /// <summary>The part panels below the top, in the dress of the design.</summary>
    private void FreshPanel(Border panel, Color part)
    {
        switch (Fam)
        {
            case NeoDesigns.Family.Metal:
                panel.CornerRadius = new CornerRadius(14); panel.Background = BlackDraw.V(SeeThrough(Color.Parse("#111214")), SeeThrough(Color.Parse("#08090A")));
                panel.BorderBrush = Palette.Brush(Color.Parse("#2A2D32"));
                break;
            case NeoDesigns.Family.Flow:
                panel.CornerRadius = new CornerRadius(12); panel.Background = Palette.Brush(SeeThrough(Color.Parse("#0A0E13"))); panel.BorderBrush = Palette.Brush(Color.Parse("#1A1F26"));
                break;
            case NeoDesigns.Family.Holo:
                panel.CornerRadius = new CornerRadius(16); panel.BorderThickness = new Thickness(1.5); panel.BorderBrush = ArtHoloBar.Foil();
                panel.Background = BlackDraw.D((SeeThrough(Color.Parse("#141824")), 0), (SeeThrough(Color.Parse("#07080C")), .7));
                break;
            case NeoDesigns.Family.Loadout:
                panel.CornerRadius = new CornerRadius(0); panel.Background = BlackDraw.V(SeeThrough(Color.Parse("#0D1019")), SeeThrough(Color.Parse("#07080C")));
                panel.BorderBrush = Palette.Brush(Color.Parse("#232838")); panel.BorderThickness = new Thickness(1, 1, 1, 3);
                break;
            case NeoDesigns.Family.Ambi:
                panel.CornerRadius = new CornerRadius(14); panel.Background = Palette.Brush(SeeThrough(Color.Parse("#07090C")));
                panel.BorderBrush = Palette.Brush(BlackDraw.A(part, .7)); panel.BorderThickness = new Thickness(4, 1, 1, 1);
                break;
            case NeoDesigns.Family.Cover:
                panel.CornerRadius = new CornerRadius(0); panel.Background = Palette.Brush(SeeThrough(nSurface)); panel.BorderBrush = Palette.Brush(Color.Parse("#2A2A2A"));
                panel.BorderThickness = new Thickness(0, 1, 0, 0); panel.Padding = new Thickness(8, 10, 8, 6);
                break;
            case NeoDesigns.Family.Lava:
                panel.CornerRadius = new CornerRadius(16); panel.Background = Palette.Brush(SeeThrough(Color.Parse("#0A0B10"))); panel.BorderBrush = Palette.Brush(Color.Parse("#1C1E26"));
                break;
            case NeoDesigns.Family.Liquid:
                // Dark glass: a faint light along the top edge.
                panel.CornerRadius = new CornerRadius(18); panel.Background = BlackDraw.V(SeeThrough(Color.Parse("#0E1218")), SeeThrough(Color.Parse("#06080B")));
                panel.BorderBrush = Palette.Brush(Color.Parse("#232A33"));
                break;
        }
    }
}
