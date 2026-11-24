using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

/// <summary>
/// The ten studio designs (5.4.0, drafts 11–20): VFD stereo, synthwave, radar, hex grid, 8-bit pixel, lab tubes, mission
/// control, Swiss, chalkboard and equalizer. They use the panel or ring skeleton of the Cyberpunk/Neumorph builder with a
/// look and, where the draft has one, a layout of their own on top. Every value stays bound to its sensor.
/// </summary>
public sealed partial class AioTile
{
    private bool Studio => Fam >= NeoDesigns.Family.Vfd;
    /// <summary>Text sizes of the design relative to the others: the 8-bit face is wide, the chalk hand small.</summary>
    private double nScale = 1;
    private int swissNumber;

    private void StudioLook()
    {
        swissNumber = 0;
        nScale = Fam switch { NeoDesigns.Family.Pixel => .72, NeoDesigns.Family.Chalk => 1.18, _ => 1 };
        nInk = lightSurface ? ink : Color.Parse(Fam switch
        {
            NeoDesigns.Family.Vfd => "#DFFCF7", NeoDesigns.Family.Synth => "#FCE9F6", NeoDesigns.Family.Radar => "#D9FFE1", NeoDesigns.Family.Hex => "#E6F2FF",
            NeoDesigns.Family.Mission => "#E8E8E8", NeoDesigns.Family.Chalk => "#F4F1E6", NeoDesigns.Family.Equalizer => "#E6FFE9", _ => "#F4F4F4"
        });
        nLabel = lightSurface ? muted : Fam is NeoDesigns.Family.Swiss or NeoDesigns.Family.Mission or NeoDesigns.Family.Tubes or NeoDesigns.Family.Pixel
            ? Color.Parse("#8E8E8E") : Fam == NeoDesigns.Family.Chalk ? Color.Parse("#B8B3A4") : Neutralize(Palette.Mix(NA, Color.Parse("#8A8F96"), .55));
        nTrack = lightSurface ? track : Fam is NeoDesigns.Family.Vfd or NeoDesigns.Family.Radar or NeoDesigns.Family.Equalizer or NeoDesigns.Family.Synth
            ? Palette.Mix(NA, nSurface, .84) : Palette.Mix(nSurface, Colors.White, .1);
        nPanel = Palette.Mix(nSurface, Colors.White, .04);
        FontFamily F(string key) => Ui.DesignFont(Ui.FamilyFor(key));
        (nFont, nValueFont, nBigFont) = Fam switch
        {
            NeoDesigns.Family.Vfd => (F("share-tech-mono"), F("share-tech-mono"), Ui.DesignFont(Ui.Dseg)),
            NeoDesigns.Family.Synth => (F("chakra-petch"), Ui.DesignFont(Ui.Orbitron), Ui.DesignFont(Ui.Orbitron)),
            NeoDesigns.Family.Radar => (F("share-tech-mono"), F("share-tech-mono"), F("share-tech-mono")),
            NeoDesigns.Family.Hex => (F("chakra-petch"), F("exo-2"), F("exo-2")),
            NeoDesigns.Family.Pixel => (Ui.DesignFont(Ui.Pixel), Ui.DesignFont(Ui.Pixel), Ui.DesignFont(Ui.Pixel)),
            NeoDesigns.Family.Mission => (F("ibm-plex-mono"), F("ibm-plex-mono"), F("ibm-plex-mono")),
            NeoDesigns.Family.Chalk => (Ui.DesignFont(Ui.Chalk), Ui.DesignFont(Ui.Chalk), Ui.DesignFont(Ui.Chalk)),
            NeoDesigns.Family.Equalizer => (F("share-tech-mono"), F("roboto-mono"), F("roboto-mono")),
            _ => (Ui.DesignFont(Ui.Inter), Ui.DesignFont(Ui.Inter), Ui.DesignFont(Ui.Inter))
        };
        BorderBrush = Palette.Brush(Fam switch
        {
            NeoDesigns.Family.Synth or NeoDesigns.Family.Radar => BlackDraw.A(NA, .32), NeoDesigns.Family.Vfd or NeoDesigns.Family.Hex or NeoDesigns.Family.Equalizer => BlackDraw.A(NA, .16),
            NeoDesigns.Family.Pixel => Color.FromArgb(90, 255, 255, 255), NeoDesigns.Family.Chalk => Color.Parse("#3A3326"), _ => Palette.Mix(nSurface, Colors.White, .08)
        });
        BorderThickness = new Thickness(Fam == NeoDesigns.Family.Chalk ? 3 : 1);
        CornerRadius = new CornerRadius(Fam switch
        {
            NeoDesigns.Family.Pixel or NeoDesigns.Family.Swiss or NeoDesigns.Family.Mission => 0, NeoDesigns.Family.Radar or NeoDesigns.Family.Hex => 4,
            NeoDesigns.Family.Chalk => 8, _ => 14
        });
        Padding = new Thickness(Fam == NeoDesigns.Family.Swiss ? 14 : 10);
        content.Spacing = Fam switch { NeoDesigns.Family.Swiss => 14, NeoDesigns.Family.Tubes or NeoDesigns.Family.Chalk => 8, _ => 10 };
    }

    /// <summary>Mission control: the panel names of a flight console.</summary>
    private static string MissionName(string cyber) => cyber switch
    {
        "CPU_CORE" => "FLIGHT COMPUTER · CPU", "MEM_ALLOC" => "LIFE SUPPORT · MEMORY", "STORAGE_ARRAY" => "CARGO · STORAGE", "NET_UPLINK" => "DOWNLINK",
        "COOLING_ARRAY" => "THRUSTERS · FANS", "POWER_RAILS" => "POWER · PSU", "MISC_SENSORS" => "AUX · SENSORS", _ => cyber
    };

    /// <summary>The layouts of their own on top; false where the design keeps the rings of the builder (chalkboard).</summary>
    private bool StudioTop(List<(AioReading R, string Caption, string Art, int Colour)> items, HashSet<string> top)
    {
        if (!Studio || Fam == NeoDesigns.Family.Chalk) return false;
        string Cap(string c) => Upper ? c.ToUpperInvariant() : c;
        double g = settings.ActiveWidget?.Graphics ?? 1;
        switch (Fam)
        {
            case NeoDesigns.Family.Synth:
            {
                var sun = new ArtSynthSun(NA, NAcc(1) == NA ? Palette.Mix(NA, Colors.Yellow, .55) : Color.Parse("#FFD86B"), SeeThrough(nSurface)) { Height = 124 * g };
                content.Children.Add(new Border { Tag = "graphic", Child = sun, Margin = new Thickness(-10, 0), ClipToBounds = true });
                var body = Stack(10);
                // Processor and graphics large (the first two of them), the rest as bars: a part switched off never moves another from bar to number.
                var large = items.Where(i => i.Art is "cpu" or "gpu").Take(2).ToList();
                var big = large.Select((i, n) =>
                {
                    var label = NText(Cap(i.Caption), 11, nLabel, true);
                    var value = Fit(GValue(i.R, 34)); var column = Stack(0, label, value);
                    if (n == 1) { label.HorizontalAlignment = HorizontalAlignment.Right; value.HorizontalAlignment = HorizontalAlignment.Right; }
                    return (Control)column;
                }).ToArray();
                if (big.Length > 0) body.Children.Add(NGrid(big, 2, 14));
                foreach (var i in items.Where(i => !large.Contains(i))) body.Children.Add(StudioBar(i.R, Cap(i.Caption), NAcc(i.Colour), false));
                content.Children.Add(NPanel(null, body));
                break;
            }
            case NeoDesigns.Family.Radar:
            {
                double size = 236 * g;
                var radar = new ArtRadar(NA) { Width = size, Height = size };
                foreach (var i in items)
                {
                    int index = radar.Add(Cap(i.Caption), i.R.Fraction);
                    watchers.Add((i.R.Key, row => radar.Set(index, row?.Fraction)));
                }
                var scope = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, MaxWidth = size, HorizontalAlignment = HorizontalAlignment.Center, Child = radar };
                var tiles = items.Select(i => (Control)Stack(0, NText(Cap(i.Caption), 10, nLabel, true), FitLeft(GValue(i.R, 20)))).ToArray();
                content.Children.Add(new Border { Tag = "aio-section", Child = Stack(10, scope, NPanel(null, NGrid(tiles, 3, 10))) });
                break;
            }
            case NeoDesigns.Family.Hex:
                content.Children.Add(new Border { Tag = "aio-section", Child = Honeycomb(items.Select(i => (i.R, Cap(i.Caption), NAcc(i.Colour))).ToArray()) });
                break;
            case NeoDesigns.Family.Tubes:
            {
                var tubes = items.Select(i =>
                {
                    var tube = new ArtTube(NAcc(i.Colour), Palette.Mix(nSurface, Colors.White, .05)) { Width = 34 * g, Height = 128 * g, HorizontalAlignment = HorizontalAlignment.Center };
                    tube.Set(i.R.Fraction); watchers.Add((i.R.Key, row => tube.Set(row?.Fraction)));
                    var value = Fit(GValue(i.R, 18));
                    var label = NText(Cap(i.Caption), 9.5, nLabel, true); label.HorizontalAlignment = HorizontalAlignment.Center; label.TextTrimming = TextTrimming.CharacterEllipsis;
                    return (Control)Stack(6, new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, MaxHeight = 128 * g, Child = tube }, value, label);
                }).ToArray();
                content.Children.Add(new Border { Tag = "aio-section", Padding = new Thickness(0, 6, 0, 4), Child = NGrid(tubes, Math.Min(5, tubes.Length), 8) });
                break;
            }
            case NeoDesigns.Family.Pixel:
            {
                content.Children.Add(new Border { Tag = "graphic", Child = new ArtHearts(Color.Parse("#FF3B5C")) { Width = 90, HorizontalAlignment = HorizontalAlignment.Left } });
                foreach (var i in items.Where(i => i.Art is "cpu" or "gpu")) content.Children.Add(NPanel(null, StudioBar(i.R, Cap(i.Caption), NAcc(i.Colour), true), null, i.Colour));
                var small = items.Where(i => i.Art is not ("cpu" or "gpu")).Select(i => NPanel(null, StudioBar(i.R, Cap(i.Caption), NAcc(i.Colour), true, true), null, i.Colour)).ToArray();
                if (small.Length > 0) content.Children.Add(new Border { Tag = "aio-section", Child = NGrid(small, 2, 8) });
                break;
            }
            case NeoDesigns.Family.Equalizer:
            {
                if (lastCoreLoads.Count >= 2 && AioReadings.Selected(settings, "aio.cpu.coreloads") && Rs("CPU").Length > 0)
                {
                    coreBars = new AioCoreBars(nInk, nLabel, nTrack) { Loads = lastCoreLoads, Tint = NA, OneColour = true, Tag = "graphic" };
                    TextColours.Bind(coreBars, "aio.cpu.coreloads");
                    var caption = NText("CORE EQ", 10.5, nLabel, true); TextFonts.Mark(caption, TextRole.Title);
                    content.Children.Add(NPanel(null, Stack(6, caption, coreBars)));
                }
                content.Children.Add(NPanel(null, Stack(12, items.Select(i => StudioBar(i.R, Cap(i.Caption), NAcc(i.Colour), true)).ToArray())));
                return true;
            }
            default: return false;
        }
        if (CyberCores() is { } cores) content.Children.Add(Shell(cores));
        return true;
    }

    /// <summary>Caption and value, a bar below: smooth (synthwave), or in blocks (8-bit, equalizer); memory adds its amount.</summary>
    private Control StudioBar(AioReading r, string caption, Color colour, bool blocks, bool small = false)
    {
        var label = NText(caption, small ? 10 : 11.5, blocks && Fam == NeoDesigns.Family.Pixel ? colour : nLabel, true); label.TextTrimming = TextTrimming.CharacterEllipsis;
        label.VerticalAlignment = VerticalAlignment.Center;
        var value = new AioReferenceValue(GValue(r, small ? 18 : blocks ? 26 : 16)) { HorizontalAlignment = HorizontalAlignment.Right };
        Control head;
        // A small tile: caption above its value, so neither squeezes the other.
        if (small) { value.HorizontalAlignment = HorizontalAlignment.Left; head = Stack(4, label, value); }
        else { var line = Ui.Columns("*,Auto", label, value); line.ColumnSpacing = 8; head = line; }
        var meter = new AioMeter(colour, nTrack, blocks ? 3 : 0) { Height = blocks ? (small ? 8 : 12) : 6 };
        meters.Add((r.Key, meter));
        var body = Stack(small ? 6 : 8, head, meter);
        if (r.Key == "aio.system.ram")
        {
            var amount = NText(Amount(r), 10.5, nLabel, false, true); formats[amount] = Amount; AddField(r.Key, amount); body.Children.Add(FitLeft(amount));
        }
        return body;
    }

    /// <summary>Hex grid: the parts in hexagons, rows of one and two cells in a honeycomb.</summary>
    private Control Honeycomb((AioReading R, string Caption, Color Colour)[] items)
    {
        const double W = 112, H = 126;
        var rows = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        int n = 0, row = 0;
        while (n < items.Length)
        {
            int count = row % 2 == 0 ? 1 : 2;
            var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = W * .06, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, row == 0 ? 0 : -H * .22, 0, 0) };
            for (int k = 0; k < count && n < items.Length; k++, n++) line.Children.Add(HexCell(items[n], W, H));
            rows.Children.Add(line); row++;
        }
        return new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, MaxWidth = W * 3, HorizontalAlignment = HorizontalAlignment.Center, Child = rows };
    }

    private Control HexCell((AioReading R, string Caption, Color Colour) item, double w, double h)
    {
        var shape = new Polygon
        {
            Points = new Avalonia.Collections.AvaloniaList<Point> { new(w / 2, 1), new(w - 1, h * .25), new(w - 1, h * .75), new(w / 2, h - 1), new(1, h * .75), new(1, h * .25) },
            Stroke = Palette.Brush(item.Colour), StrokeThickness = 2, Fill = Palette.Brush(SeeThrough(Palette.Mix(nSurface, item.Colour, .07))), Tag = "graphic"
        };
        var value = Fit(GValue(item.R, 24)); value.HorizontalAlignment = HorizontalAlignment.Center;
        var label = NText(item.Caption, 9.5, nLabel, true); label.HorizontalAlignment = HorizontalAlignment.Center; label.TextTrimming = TextTrimming.CharacterEllipsis; label.LetterSpacing = 1.2;
        var face = Stack(0, value, label); face.VerticalAlignment = VerticalAlignment.Center; face.Margin = new Thickness(w * .16, 0);
        return new Grid { Width = w, Height = h, Children = { shape, face } };
    }

    /// <summary>A panel of a studio design with its heading.</summary>
    private Control StudioPanel(string? title, Control body, string? model, Color part)
    {
        Control? heading = null;
        if (title != null)
        {
            switch (Fam)
            {
                case NeoDesigns.Family.Swiss:
                {
                    var number = NText((++swissNumber).ToString("00") + " — " + title, 11, nLabel, true); TextFonts.Mark(number, TextRole.Title); number.TextTrimming = TextTrimming.CharacterEllipsis;
                    heading = number;
                    if (!string.IsNullOrEmpty(model)) { var name = NText(model, 11, nLabel); name.TextTrimming = TextTrimming.CharacterEllipsis; var line = Ui.Columns("Auto,*", number, name); line.ColumnSpacing = 10; name.TextAlignment = TextAlignment.Right; heading = line; }
                    break;
                }
                case NeoDesigns.Family.Mission:
                {
                    var name = NHeading(title, part);
                    // The part's name in a text of its own (as the hardware or the user names it), between title and status.
                    Control left = name;
                    if (!string.IsNullOrEmpty(model))
                    {
                        name.Text += " ·"; var device = NText(model, 10, nLabel, true); device.TextTrimming = TextTrimming.CharacterEllipsis;
                        var both = Ui.Columns("Auto,*", name, device); both.ColumnSpacing = 6; left = both;
                    }
                    var status = NText("● NOMINAL", 10, Color.Parse("#4BE36B"), true); status.Margin = new Thickness(8, 0, 0, 0);
                    heading = new Border
                    {
                        Background = Palette.Brush(SeeThrough(Palette.Mix(nSurface, Colors.White, .07))), Padding = new Thickness(10, 5), Margin = new Thickness(-12, -10, -12, 0),
                        Child = Ui.Columns("*,Auto", left, status)
                    };
                    break;
                }
                default:
                {
                    heading = NHeading(title, part);
                    if (!string.IsNullOrEmpty(model))
                    {
                        var name = NText(model, 11, nLabel, true); name.TextTrimming = TextTrimming.CharacterEllipsis;
                        ((TextBlock)heading).Text += " ·";
                        var row = Ui.Columns("Auto,*", heading, name); row.ColumnSpacing = 6; heading = row;
                    }
                    if (Fam == NeoDesigns.Family.Chalk) heading = Stack(3, heading, new ArtBrushStroke(Palette.Mix(nInk, nSurface, .35)));
                    break;
                }
            }
        }
        var inner = heading == null ? body : Stack(Fam == NeoDesigns.Family.Mission ? 8 : 10, heading, body);
        var panel = new Border { Padding = new Thickness(14, 12), Child = inner, Tag = "aio-section" };
        switch (Fam)
        {
            case NeoDesigns.Family.Vfd:
                panel.CornerRadius = new CornerRadius(10); panel.BorderThickness = new Thickness(1); panel.BorderBrush = Palette.Brush(Palette.Mix(nSurface, NA, .14));
                panel.Background = Palette.Brush(SeeThrough(Palette.Mix(nSurface, Colors.White, .03)));
                panel.BoxShadow = new BoxShadows(new BoxShadow { OffsetY = 2, Blur = 8, Color = Color.FromArgb(170, 0, 0, 0), IsInset = true });
                break;
            case NeoDesigns.Family.Synth:
                panel.CornerRadius = new CornerRadius(12); panel.BorderThickness = new Thickness(1.5); panel.BorderBrush = Palette.Brush(BlackDraw.A(part, .6));
                panel.Background = Palette.Brush(SeeThrough(Palette.Mix(nSurface, part, .06)));
                panel.BoxShadow = new BoxShadows(new BoxShadow { Blur = 12, Color = BlackDraw.A(part, .3) });
                break;
            case NeoDesigns.Family.Radar:
                panel.CornerRadius = new CornerRadius(2); panel.BorderThickness = new Thickness(1); panel.BorderBrush = Palette.Brush(BlackDraw.A(part, .45));
                panel.Background = Palette.Brush(SeeThrough(Palette.Mix(nSurface, part, .05))); panel.Padding = new Thickness(12, 10);
                break;
            case NeoDesigns.Family.Hex:
                panel.BorderThickness = new Thickness(1); panel.BorderBrush = Palette.Brush(BlackDraw.A(part, .4)); panel.Background = Palette.Brush(SeeThrough(Palette.Mix(nSurface, part, .04)));
                break;
            case NeoDesigns.Family.Pixel:
                panel.BorderThickness = new Thickness(3); panel.BorderBrush = Palette.Brush(Color.FromArgb(235, 255, 255, 255)); panel.Padding = new Thickness(12, 10);
                return new Border { BorderThickness = new Thickness(1), BorderBrush = Palette.Brush(Color.FromArgb(90, 255, 255, 255)), Padding = new Thickness(2), Child = panel, Tag = "aio-section" };
            case NeoDesigns.Family.Tubes:
            case NeoDesigns.Family.Chalk:
                panel.BorderThickness = new Thickness(0, 0, 0, 1); panel.BorderBrush = Palette.Brush(Fam == NeoDesigns.Family.Chalk ? BlackDraw.A(nInk, .22) : Color.FromArgb(30, 255, 255, 255));
                panel.Padding = new Thickness(4, 8, 4, 10);
                break;
            case NeoDesigns.Family.Mission:
                panel.BorderThickness = new Thickness(1); panel.BorderBrush = Palette.Brush(Palette.Mix(nSurface, Colors.White, .14)); panel.Padding = new Thickness(12, 10);
                break;
            case NeoDesigns.Family.Swiss:
                panel.BorderThickness = new Thickness(0, 2, 0, 0); panel.BorderBrush = Palette.Brush(nInk); panel.Padding = new Thickness(0, 10, 0, 2);
                break;
            case NeoDesigns.Family.Equalizer:
                panel.CornerRadius = new CornerRadius(6); panel.BorderThickness = new Thickness(1); panel.BorderBrush = Palette.Brush(BlackDraw.A(part, .15));
                panel.Background = Palette.Brush(SeeThrough(Palette.Mix(nSurface, Colors.White, .035)));
                break;
        }
        return panel;
    }
}
