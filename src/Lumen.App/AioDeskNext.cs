using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

/// <summary>
/// 5.9.0: twenty more full-screen desktop designs (drafts F-01 … F-20). Each shows the main value of every part in a scene of
/// its own (a power flow, a loadout, holo cards, liquid metal, a magazine, lava lamps, ambilight, versus, an esports broadcast,
/// vital signs, a globe, a clock, an EV console, a server rack, turntables, the ocean, a pit wall, a trading floor, big type, a
/// space station); everything else follows in the panels of the other desktop designs, so every value appears exactly once.
/// </summary>
public sealed partial class AioTile
{
    private bool DeskNext => designStyle >= WidgetStyle.AioDeskPowerGrid && designStyle <= WidgetStyle.AioDeskStation;
    private enum DeskSkin { Default, Metal, Holo, Square, Glass, Lava, Space, Rack }
    private DeskSkin deskSkin;
    private Control? deskBackdrop;
    private bool deskMetal;

    private void DeskNextLook()
    {
        FontFamily F(string key) => Ui.DesignFont(Ui.FamilyFor(key));
        FontFamily inter = Ui.DesignFont(Ui.Inter);
        (nFont, nValueFont, nBigFont) = designStyle switch
        {
            WidgetStyle.AioDeskLoadout or WidgetStyle.AioDeskBroadcast or WidgetStyle.AioDeskRack => (F("rajdhani"), F("rajdhani"), F("rajdhani")),
            WidgetStyle.AioDeskHoloDeck or WidgetStyle.AioDeskLavaShelf or WidgetStyle.AioDeskStation => (F("exo-2"), F("exo-2"), F("exo-2")),
            WidgetStyle.AioDeskMetalHall => (Ui.DesignFont(Ui.Orbitron), Ui.DesignFont(Ui.Orbitron), Ui.DesignFont(Ui.Orbitron)),
            WidgetStyle.AioDeskMagazine => (inter, inter, F("bebas-neue")),
            WidgetStyle.AioDeskVitals or WidgetStyle.AioDeskTrading => (F("roboto-mono"), F("roboto-mono"), F("roboto-mono")),
            WidgetStyle.AioDeskPitWall => (F("barlow-condensed"), F("barlow-condensed"), F("barlow-condensed")),
            _ => (inter, inter, inter)
        };
        deskSkin = designStyle switch
        {
            WidgetStyle.AioDeskMetalHall => DeskSkin.Metal,
            WidgetStyle.AioDeskHoloDeck => DeskSkin.Holo,
            WidgetStyle.AioDeskLoadout or WidgetStyle.AioDeskBroadcast or WidgetStyle.AioDeskPitWall or WidgetStyle.AioDeskTrading => DeskSkin.Square,
            WidgetStyle.AioDeskOcean => DeskSkin.Glass,
            WidgetStyle.AioDeskLavaShelf => DeskSkin.Lava,
            WidgetStyle.AioDeskStation => DeskSkin.Space,
            WidgetStyle.AioDeskRack => DeskSkin.Rack,
            _ => DeskSkin.Default
        };
        deskPlain = designStyle is WidgetStyle.AioDeskMagazine or WidgetStyle.AioDeskVitals or WidgetStyle.AioDeskClock or WidgetStyle.AioDeskBigType;
        deskMetal = designStyle == WidgetStyle.AioDeskMetalHall;
        deskBackdrop = designStyle switch
        {
            WidgetStyle.AioDeskOcean => new ArtOceanNight(),
            WidgetStyle.AioDeskStation => new ArtStation(),
            WidgetStyle.AioDeskVersus => new ArtVersus(NAcc(0), NAcc(1)),
            WidgetStyle.AioDeskLavaShelf => new Border { Tag = "graphic", Background = new RadialGradientBrush { Center = new RelativePoint(.5, .3, RelativeUnit.Relative), GradientOrigin = new RelativePoint(.5, .3, RelativeUnit.Relative),
                GradientStops = { new(SeeThrough(Color.Parse("#1A0B1E")), 0), new(SeeThrough(nSurface), 1) } } },
            _ => null
        };
    }

    /// <summary>A card in the dress of one of the 5.9.0 desktop designs.</summary>
    private Border DeskSkinned(Control body, Thickness? padding)
    {
        var card = new Border { Child = body, ClipToBounds = true, BorderThickness = new Thickness(1), Padding = padding ?? new Thickness(20, 14, 20, 14) };
        switch (deskSkin)
        {
            case DeskSkin.Metal:
                card.Background = BlackDraw.V(SeeThrough(Color.Parse("#121315")), SeeThrough(Color.Parse("#08090A"))); card.BorderBrush = Palette.Brush(Color.Parse("#2A2D32")); card.CornerRadius = new CornerRadius(14);
                break;
            case DeskSkin.Holo:
                card.Background = BlackDraw.D((SeeThrough(Color.Parse("#141824")), 0), (SeeThrough(Color.Parse("#07080C")), .7)); card.BorderBrush = ArtHoloBar.Foil(); card.BorderThickness = new Thickness(1.5);
                card.CornerRadius = new CornerRadius(16);
                break;
            case DeskSkin.Square:
                card.Background = BlackDraw.V(SeeThrough(Color.Parse("#0D1019")), SeeThrough(Color.Parse("#07080C"))); card.BorderBrush = Palette.Brush(Color.Parse("#232838")); card.CornerRadius = new CornerRadius(3);
                break;
            case DeskSkin.Glass:
                card.Background = BlackDraw.D((Color.FromArgb(40, 255, 255, 255), 0), (Color.FromArgb(10, 255, 255, 255), 1)); card.BorderBrush = Palette.Brush(Color.FromArgb(60, 255, 255, 255));
                card.CornerRadius = new CornerRadius(22);
                break;
            case DeskSkin.Lava:
                card.Background = Palette.Brush(SeeThrough(Color.Parse("#0B0A10"))); card.BorderBrush = Palette.Brush(Color.Parse("#2A2438")); card.CornerRadius = new CornerRadius(18);
                break;
            case DeskSkin.Space:
                card.Background = BlackDraw.D((SeeThrough(Color.Parse("#1A2030")), 0), (SeeThrough(Color.Parse("#0B0E16")), 1)); card.BorderBrush = Palette.Brush(Color.Parse("#2A3040")); card.BorderThickness = new Thickness(2);
                card.CornerRadius = new CornerRadius(26);
                break;
            case DeskSkin.Rack:
                card.Background = BlackDraw.V(SeeThrough(Color.Parse("#1C1F24")), SeeThrough(Color.Parse("#121418"))); card.BorderBrush = Palette.Brush(Color.Parse("#2A2D32")); card.CornerRadius = new CornerRadius(4);
                break;
        }
        return card;
    }

    /// <summary>The panels of the other desktop designs, some of them left out where the design shows them itself.</summary>
    private void DeskPanelsBut(HashSet<string> top, bool net = true, bool fans = true)
    {
        DeskDetails(top); DeskCores(top); DeskMemoryRest(top); DeskStorage(top);
        if (net) DeskNetwork(top);
        if (fans) DeskFans(top);
        DeskRails(top);
    }

    private (DeskGrid[] Wide, DeskGrid[] Tall) DeskNextDesigns(HashSet<string> top, List<DeskItem> items)
    {
        var big = items.Where(i => i.Kind is "cpu" or "gpu").ToList(); var small = items.Where(i => i.Kind is not ("cpu" or "gpu")).ToList();
        DeskGrid G(string columns, string rows, params string[] areas) => DeskGrid.Of(columns, rows, areas);
        // The layouts used by most designs: the scene on the left, across the top, or across the top with a row of small parts.
        var left = new[]
        {
            G("1.7 1 1", "1 .9 .7 .6 0", "scene details details", "scene cores net", "scene storage fans", "scene ram psu", "more more more"),
            G("1.4 1 1", "1 .8 .7 0", "scene details details", "scene cores net", "storage ram fans", "psu more more"),
        };
        var across = new[]
        {
            G("1 1 1 1", "2 .75 .55 0", "scene scene scene scene", "details details cores net", "ram storage fans psu", "more more more more"),
            G("1 1 1", "1.3 .8 .7 .6 0", "scene scene scene", "details details cores", "net ram storage", "fans psu psu", "more more more"),
        };
        var withSmall = new[]
        {
            G("1 1 1 1", "1.6 .9 .65 .5 0", "scene scene scene scene", "small small details details", "cores net storage fans", "ram ram psu psu", "more more more more"),
            G("1 1 1", "1.5 .9 .8 .6 0", "scene scene scene", "small small details", "cores net storage", "ram fans psu", "more more more"),
        };
        // The large parts and the small ones side by side, both the full height of the first row.
        var pair = new[]
        {
            G("1 1 1 1", "1.35 .75 .55 0", "scene scene small small", "details details cores net", "ram storage fans psu", "more more more more"),
            G("1 1 1", "1.2 .9 .8 .6 0", "scene scene scene", "small small details", "cores net storage", "ram fans psu", "more more more"),
        };
        var tall = new[] { G("1 1", "1.5 .8 .9 .7 .6 .5 0", "scene scene", "small small", "details details", "cores ram", "storage net", "fans psu", "more more") };
        void Panels() => DeskPanels(top);
        switch (designStyle)
        {
            case WidgetStyle.AioDeskPowerGrid:
            {
                // F-01: the power supply on the left, a band to every part as wide as its watts, the part with its values on the right.
                if (items.Count > 0) deskParts["scene"] = DeskPowerScene(items, top);
                Panels();
                return (left, tall);
            }
            case WidgetStyle.AioDeskLoadout:
            {
                // F-02: the processor equipped in a frame of its temperature, every other part in an inventory slot.
                var lead = big.FirstOrDefault() ?? items.FirstOrDefault();
                if (lead != null) deskParts["scene"] = DeskEquipped(lead);
                var rest = items.Where(i => i != lead).ToList();
                if (rest.Count > 0) deskParts["small"] = DeskRow(rest.Select(DeskSlot), 160, 12);
                Panels();
                return (withSmall, tall);
            }
            case WidgetStyle.AioDeskHoloDeck:
            {
                // F-03: every part a holo card, processor and graphics large.
                if (big.Count > 0) deskParts["scene"] = DeskRow(big.Select(i => DeskHoloCard(i, true)), 260, 16);
                if (small.Count > 0) deskParts["small"] = DeskRow(small.Select(i => DeskHoloCard(i, false)), 170, 12);
                Panels();
                return (pair, tall);
            }
            case WidgetStyle.AioDeskMetalHall:
            {
                // F-04: silver parts, chrome numbers and a mercury groove under each.
                if (big.Count > 0) deskParts["scene"] = DeskRow(big.Select(i => DeskMetalFigure(i, 90, 130)), 240, 18);
                if (small.Count > 0) deskParts["small"] = DeskRow(small.Select(i => DeskMetalFigure(i, 46, 70)), 160, 12);
                Panels();
                return (pair, tall);
            }
            case WidgetStyle.AioDeskMagazine:
            {
                // F-05: a magazine cover: masthead, the first part's number huge in a serif beside its picture; the other parts as cover lines.
                var lead = big.FirstOrDefault() ?? items.FirstOrDefault();
                if (lead != null) deskParts["scene"] = DeskCover(lead);
                var rest = items.Where(i => i != lead).ToList();
                if (rest.Count > 0) deskParts["small"] = DeskRow(rest.Select(DeskCoverLine), 0, 6, true);
                Panels();
                return (new[]
                {
                    G("1.4 1 1", "1 .9 .7 .6 0", "scene small details", "scene small cores", "scene storage net", "scene ram fans", "psu more more"),
                    G("1.3 1 1", "1 .8 .7 0", "scene small details", "scene cores net", "storage ram fans", "psu more more"),
                }, tall);
            }
            case WidgetStyle.AioDeskLavaShelf:
            {
                // F-06: a lava lamp per part on one shelf.
                if (items.Count > 0) deskParts["scene"] = DeskRow(items.Select((i, k) => DeskLava(i, k)), 130, 10);
                Panels();
                return (new[]
                {
                    G("1 1 1 1", "2.7 .75 .55 0", "scene scene scene scene", "details details cores net", "ram storage fans psu", "more more more more"),
                    G("1 1 1", "1.8 .8 .7 .6 0", "scene scene scene", "details details cores", "net ram storage", "fans psu psu", "more more more"),
                }, tall);
            }
            case WidgetStyle.AioDeskAmbilight:
            {
                // F-07: every part on a dark card lit from its edge in the colour of its temperature.
                if (big.Count > 0) deskParts["scene"] = DeskRow(big.Select(i => DeskAmbiCard(i, 96)), 280, 16);
                if (small.Count > 0) deskParts["small"] = DeskRow(small.Select(i => DeskAmbiCard(i, 56)), 200, 12);
                Panels();
                return (pair, tall);
            }
            case WidgetStyle.AioDeskVersus:
            {
                // F-08: processor against graphics card, the screen split in their colours.
                if (big.Count >= 2) deskParts["scene"] = DeskVersus(big[0], big[1]);
                else if (big.Count == 1) deskParts["scene"] = DeskVersusSide(big[0], true);
                var rest = big.Skip(2).Concat(small).ToList();
                if (rest.Count > 0) deskParts["small"] = DeskRow(rest.Select(i => DeskTile(i, 40)), 220, 12);
                Panels();
                return (new[]
                {
                    G("1 1 1 1", "1.9 .8 .6 .45 0", "scene scene scene scene", "small small details details", "cores net storage fans", "ram ram psu psu", "more more more more"),
                    G("1 1 1", "1.7 .9 .8 .6 0", "scene scene scene", "small small details", "cores net storage", "ram fans psu", "more more more"),
                }, tall);
            }
            case WidgetStyle.AioDeskBroadcast:
            {
                // F-09: a broadcast scoreboard for processor and graphics card, the other parts in the event feed.
                if (big.Count > 0) deskParts["scene"] = DeskScoreboard(big);
                if (small.Count > 0) deskParts["small"] = DeskRow(small.Select(DeskFeedRow), 0, 8, true);
                Panels();
                return (new[]
                {
                    G("1 1 1 1", "1.2 .9 .7 .5 0", "scene scene scene scene", "small small details details", "cores net storage fans", "ram ram psu psu", "more more more more"),
                    G("1 1 1", "1.2 .9 .7 0", "scene scene scene", "small details details", "cores net storage", "ram fans psu"),
                }, tall);
            }
            case WidgetStyle.AioDeskVitals:
            {
                // F-10: a heart-rate trace per part, like a patient monitor.
                if (items.Count > 0) deskParts["scene"] = DeskRow(items.Select((i, k) => DeskVitalRow(i, k)), 0, 6, true);
                Panels();
                return (left, tall);
            }
            case WidgetStyle.AioDeskGlobe:
            {
                // F-11: the network on a globe in the middle, the parts around it.
                DeskGlobe();
                if (items.Count > 0) deskParts["small"] = DeskRow(items.Select(i => DeskTile(i, 40)), 0, 10, true);
                DeskPanelsBut(top, net: false);
                return (new[]
                {
                    G("1 1.5 1", "1 1 .7 .5 0", "small scene details", "small scene cores", "storage scene fans", "ram psu psu", "more more more"),
                    G("1 1.3 1", "1 1 .7 0", "small scene details", "small scene cores", "storage ram fans", "psu more more"),
                }, new[] { G("1 1", "1.4 1 .9 .7 .6 .5 0", "scene scene", "small small", "details details", "cores ram", "storage fans", "psu psu", "more more") });
            }
            case WidgetStyle.AioDeskClock:
            {
                // F-12: a large clock, the parts as a quiet line of numbers below it.
                var clock = new ArtClock(nInk, nLabel);
                if (items.Count > 0) watchers.Add((items[0].R.Key, _ => clock.Tick()));
                var body = new Grid { RowDefinitions = new RowDefinitions("*,Auto"), RowSpacing = 18, Children = { new DeskElastic { Min = 150, Child = clock } } };
                if (items.Count > 0) { var stats = DeskRow(items.Select(DeskClockStat), 150, 0); Grid.SetRow(stats, 1); body.Children.Add(stats); }
                deskParts["scene"] = body;
                Panels();
                return (new[]
                {
                    G("1 1 1 1", "2 .7 .55 0", "scene scene scene scene", "details details cores net", "ram storage fans psu", "more more more more"),
                    G("1 1 1", "1.8 .8 .7 .6 0", "scene scene scene", "details details cores", "net ram storage", "fans psu psu", "more more more"),
                }, tall);
            }
            case WidgetStyle.AioDeskConsole:
            {
                // F-13: the case from above like a car on its screen; the processor's number large beside it, the other parts as tiles.
                var lead = big.FirstOrDefault() ?? items.FirstOrDefault();
                if (lead != null) deskParts["scene"] = DeskConsole(lead, items);
                var rest = items.Where(i => i != lead).ToList();
                if (rest.Count > 0) deskParts["small"] = DeskRow(rest.Select(i => DeskTile(i, 38)), 210, 12);
                Panels();
                return (new[]
                {
                    G("1 1 1 1", "1.9 .8 .6 .45 0", "scene scene scene scene", "small small details details", "cores net storage fans", "ram ram psu psu", "more more more more"),
                    G("1 1 1", "1.7 .9 .8 .6 0", "scene scene scene", "small small details", "cores net storage", "ram fans psu", "more more more"),
                }, tall);
            }
            case WidgetStyle.AioDeskRack:
            {
                // F-14: every part a unit in a server rack with its display and status lights.
                if (items.Count > 0) deskParts["scene"] = DeskRow(items.Select(DeskRackUnit), 0, 6, true);
                Panels();
                return (left, tall);
            }
            case WidgetStyle.AioDeskTurntable:
            {
                // F-15: every fan a record with its speed on the label; the parts as the channels of a mixer.
                DeskRecords();
                if (items.Count > 0) deskParts["small"] = DeskRow(items.Select(DeskChannel), 90, 8);
                DeskPanelsBut(top, fans: false);
                return (new[]
                {
                    G("1 1 1", "1 1 .7 .55 0", "scene scene small", "scene scene details", "cores net storage", "ram psu psu", "more more more"),
                    G("1 1 1", "1 1 .7 0", "scene scene small", "scene scene details", "cores net storage", "ram psu more"),
                }, new[] { G("1 1", "1.3 1 .9 .7 .6 .5 0", "scene scene", "small small", "details details", "cores ram", "storage net", "psu psu", "more more") });
            }
            case WidgetStyle.AioDeskOcean:
            {
                // F-16: glass cards over the sea at night.
                if (big.Count > 0) deskParts["scene"] = DeskRow(big.Select(i => DeskTile(i, 64)), 280, 16);
                if (small.Count > 0) deskParts["small"] = DeskRow(small.Select(i => DeskTile(i, 40)), 200, 12);
                Panels();
                return (pair, tall);
            }
            case WidgetStyle.AioDeskPitWall:
            {
                // F-17: the screens of a pit wall: the timing tower, the telemetry of processor and graphics card.
                if (items.Count > 0) deskParts["scene"] = DeskTimingTower(items);
                if (DeskTelemetryScreen(big) is { } tele) deskParts["small"] = tele;
                Panels();
                return (new[]
                {
                    G("1 1.6 1", "1 1 .7 .55 0", "scene small details", "scene small cores", "storage net fans", "ram psu psu", "more more more"),
                    G("1 1.3 1", "1 1 .7 0", "scene small details", "scene cores net", "storage ram fans", "psu more more"),
                }, tall);
            }
            case WidgetStyle.AioDeskTrading:
            {
                // F-18: a ticker of every part across the top, the charts of processor and graphics card below.
                if (items.Count > 0) deskParts["scene"] = DeskRow(items.Select(DeskTicker), 170, 10);
                if (DeskTelemetryScreen(big) is { } charts) deskParts["small"] = charts;
                Panels();
                return (new[]
                {
                    G("1 1 1", ".5 1.2 .7 .55 0", "scene scene scene", "small small details", "cores net storage", "fans ram psu", "more more more"),
                    G("1 1 1", ".5 1 .7 .6 0", "scene scene scene", "small small details", "cores net storage", "fans ram psu", "more more more"),
                }, tall);
            }
            case WidgetStyle.AioDeskBigType:
            {
                // F-19: nothing but huge numbers under a rule.
                if (items.Count > 0) deskParts["scene"] = DeskRow(items.Select((i, k) => DeskBigStat(i, k == 0)), 220, 24);
                Panels();
                return (across, tall);
            }
            default:
            {
                // F-20 Space Station: every part a module of a station in orbit.
                if (items.Count > 0) deskParts["scene"] = DeskRow(items.Select(i => DeskTile(i with { Caption = "MODULE · " + i.Caption }, 46)), 240, 14);
                Panels();
                return (across, tall);
            }
        }
    }

    // ------------------------------------------------------------------ F-01 power grid
    private Control DeskPowerScene(List<DeskItem> items, HashSet<string> top)
    {
        var power = items.Select(i => i.Kind switch { "cpu" => R("power"), "gpu" => R("power", "GPU", i.R.Device), _ => null }).ToArray();
        var flow = new ArtPowerFlow(items.Select(i => i.Colour).ToArray(), power.Select(p => p != null).ToArray());
        var nodes = new Grid { RowDefinitions = new RowDefinitions(string.Join(",", items.Select(_ => "*"))), RowSpacing = 8 };
        for (int k = 0; k < items.Count; k++)
        {
            var it = items[k];
            var caption = DeskCaption(it.Caption, it.Colour);
            var text = Stack(0, caption, FitLeft(GValue(it.R, 34)));
            if (power[k] is { } p)
            {
                top.Add(p.Key); flow.Set(k, p.Number); int at = k; watchers.Add((p.Key, r => flow.Set(at, r?.Number)));
                text.Children.Add(FitLeft(GValue(p, 22)));
            }
            else if (DeskUnder(it) is { } u) { u.TextTrimming = TextTrimming.CharacterEllipsis; text.Children.Add(u); }
            text.VerticalAlignment = VerticalAlignment.Center;
            var node = Ui.Columns("1*,1.6*", DeskStagePicture(it, DeskStage.Glow, 40), text); node.ColumnSpacing = 10;
            node.ColumnDefinitions[1].MinWidth = DeskNatural(caption, text.Children.OfType<TextBlock>().FirstOrDefault(t => t != caption));
            Grid.SetRow(node, k); nodes.Children.Add(node);
        }
        var source = new Grid { RowDefinitions = new RowDefinitions("*,Auto"), Children = { new DeskElastic { Min = 80, Max = 240, Child = DeskArt("psu") } } };
        var label = DeskCaption(L.T("Power"), nLabel); label.HorizontalAlignment = HorizontalAlignment.Center; Grid.SetRow(label, 1); source.Children.Add(label);
        source.VerticalAlignment = VerticalAlignment.Center;
        var scene = Ui.Columns(".8*,1*,2*", source, new DeskElastic { Min = 120, Child = flow }, nodes); scene.ColumnSpacing = 6;
        scene.ColumnDefinitions[0].MinWidth = DeskNatural(label);
        return DeskCard(scene, new Thickness(16, 12, 16, 12));
    }

    // ------------------------------------------------------------------ F-02 loadout
    private Control DeskEquipped(DeskItem lead)
    {
        var frame = new ArtLoadoutFrame(); var stat = new ArtSegmentStat(HeatColour.Of(Celsius(lead.R))) { Height = 14 };
        var tag = DeskCaption("Equipped", HeatColour.Of(Celsius(lead.R)), 13);
        void Heat(AioReading? row) { frame.Set(Celsius(row)); stat.Set(row?.Fraction, HeatColour.Of(Celsius(row))); tag.Foreground = Palette.Brush(HeatColour.Of(Celsius(row))); }
        Heat(lead.R); watchers.Add((lead.R.Key, Heat));
        var name = NText(lead.Caption, 40, nInk, true); name.TextTrimming = TextTrimming.CharacterEllipsis;
        var text = Stack(2, tag, name, FitLeft(GValue(lead.R, 110)));
        if (DeskUnder(lead) is { } u) { u.TextTrimming = TextTrimming.CharacterEllipsis; text.Children.Add(u); }
        text.VerticalAlignment = VerticalAlignment.Center;
        var row = Ui.Columns("1*,1.1*", DeskStagePicture(lead, DeskStage.Glow, 120), text); row.ColumnSpacing = 18;
        row.ColumnDefinitions[1].MinWidth = DeskNatural(tag, name);
        var body = new Grid { RowDefinitions = new RowDefinitions("*,Auto"), RowSpacing = 14, Children = { row } };
        Grid.SetRow(stat, 1); body.Children.Add(stat);
        return new Grid { Children = { frame, new Border { Padding = new Thickness(22, 18), Child = body } } };
    }

    private Control DeskSlot(DeskItem it)
    {
        var slot = new Border
        {
            BorderThickness = new Thickness(1, 1, 1, 4), Padding = new Thickness(8, 10, 8, 8), CornerRadius = new CornerRadius(2),
            Background = BlackDraw.V(SeeThrough(Color.Parse("#0D1019")), SeeThrough(Color.Parse("#07080C"))),
            Child = DeskFigure(it, 34, DeskStage.Glow, 60)
        };
        void Heat(AioReading? row) => slot.BorderBrush = Palette.Brush(HeatColour.Of(Celsius(row)));
        Heat(it.R); watchers.Add((it.R.Key, Heat));
        return slot;
    }

    // ------------------------------------------------------------------ F-03 holo cards
    private Control DeskHoloCard(DeskItem it, bool large)
    {
        var caption = DeskCaption(it.Caption, nInk, large ? 15 : 13);
        var head = Ui.Columns("*,Auto", caption, new AioReferenceValue(GValue(it.R, large ? 54 : 34)) { HorizontalAlignment = HorizontalAlignment.Right });
        caption.VerticalAlignment = VerticalAlignment.Center; head.ColumnSpacing = 8; head.ColumnDefinitions[0].MinWidth = DeskNatural(caption);
        var art = new Border
        {
            CornerRadius = new CornerRadius(12), Tag = "graphic", Child = DeskStagePicture(it, DeskStage.Plain, large ? 110 : 60),
            Background = new RadialGradientBrush { GradientStops = { new(SeeThrough(Color.Parse("#1D2540")), 0), new(SeeThrough(Color.Parse("#0A0C14")), 1) } }
        };
        var bar = new ArtHoloBar { Height = 8 }; bar.Set(it.R.Fraction); watchers.Add((it.R.Key, row => bar.Set(row?.Fraction)));
        var body = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto"), RowSpacing = 8, Children = { head } };
        Grid.SetRow(art, 1); body.Children.Add(art);
        if (DeskUnder(it) is { } u) { u.TextTrimming = TextTrimming.CharacterEllipsis; Grid.SetRow(u, 2); body.Children.Add(u); }
        Grid.SetRow(bar, 3); body.Children.Add(bar);
        var inner = new Grid
        {
            Children =
            {
                new Border { CornerRadius = new CornerRadius(17), Background = BlackDraw.D((SeeThrough(Color.Parse("#141824")), 0), (SeeThrough(Color.Parse("#07080C")), .6)) },
                new ArtHoloSheen(), new Border { Padding = new Thickness(16, 12, 16, 14), Child = body }
            }
        };
        return new Border { CornerRadius = new CornerRadius(19), Padding = new Thickness(2.5), Background = ArtHoloBar.Foil(), Child = inner,
            BoxShadow = new BoxShadows(new BoxShadow { Blur = 26, Color = Color.FromArgb(46, 110, 160, 255) }) };
    }

    // ------------------------------------------------------------------ F-04 liquid metal
    private Control DeskMetalFigure(DeskItem it, double big, double min)
    {
        var groove = new ArtMetalTube { Height = big > 60 ? 16 : 11 }; groove.Set(it.R.Fraction); watchers.Add((it.R.Key, row => groove.Set(row?.Fraction)));
        var grid = new Grid { RowDefinitions = new RowDefinitions("*,Auto,Auto,Auto,Auto"), RowSpacing = 4 };
        grid.Children.Add(DeskStagePicture(it, DeskStage.Plain, min));
        var caption = DeskCaption(it.Caption, nLabel, big > 60 ? 14 : 12); Grid.SetRow(caption, 1); grid.Children.Add(caption);
        var value = FitLeft(GValue(it.R, big)); Grid.SetRow(value, 2); grid.Children.Add(value);
        if (DeskUnder(it) is { } u) { u.TextTrimming = TextTrimming.CharacterEllipsis; Grid.SetRow(u, 3); grid.Children.Add(u); }
        Grid.SetRow(groove, 4); grid.Children.Add(groove);
        return grid;
    }

    // ------------------------------------------------------------------ F-05 magazine
    private Control DeskCover(DeskItem lead)
    {
        var red = Color.Parse("#FF3B3B");
        var masthead = NText("LUMEN", 120, red, false); masthead.FontFamily = Ui.DesignFont(Ui.FamilyFor("bebas-neue")); masthead.FontWeight = FontWeight.Normal; masthead.LineHeight = 108;
        TextFonts.Mark(masthead, TextRole.Title);
        var issue = DeskCaption("The live issue · your PC", nLabel, 13);
        var huge = GValue(lead.R, 200); huge.FontFamily = Ui.DesignFont(Ui.Playfair); huge.FontWeight = FontWeight.Normal;
        var hero = Ui.Columns("1.1*,1*", FitLeft(huge), DeskStagePicture(lead, DeskStage.Plain, 120)); hero.ColumnSpacing = 6;
        var kicker = DeskCaption("Cover story", red, 13);
        var headline = NText(lead.Caption + (lead.Model.Length > 0 ? " · " + lead.Model : ""), 34, nInk, false); headline.FontFamily = Ui.DesignFont(Ui.Playfair); headline.FontWeight = FontWeight.Normal;
        headline.TextWrapping = TextWrapping.Wrap; headline.MaxLines = 2; headline.TextTrimming = TextTrimming.CharacterEllipsis;
        var body = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto,Auto"), RowSpacing = 4, Children = { masthead } };
        Grid.SetRow(issue, 1); body.Children.Add(issue);
        var holder = new DeskElastic { Min = 140, Child = hero }; Grid.SetRow(holder, 2); body.Children.Add(holder);
        Grid.SetRow(kicker, 3); body.Children.Add(kicker);
        Grid.SetRow(headline, 4); body.Children.Add(headline);
        return new Border
        {
            Padding = new Thickness(22, 16), CornerRadius = new CornerRadius(4), ClipToBounds = true, Child = body,
            Background = new RadialGradientBrush { Center = new RelativePoint(.62, .55, RelativeUnit.Relative), GradientOrigin = new RelativePoint(.62, .55, RelativeUnit.Relative),
                GradientStops = { new(SeeThrough(Color.Parse("#2A0B0E")), 0), new(SeeThrough(nSurface), 1) } }
        };
    }

    private Control DeskCoverLine(DeskItem it)
    {
        var dash = new Border { Width = 14, Height = 3, Background = Palette.Brush(Color.Parse("#FF3B3B")), VerticalAlignment = VerticalAlignment.Center, Tag = "graphic" };
        var name = NText(it.Caption, 22, nInk, false); name.FontFamily = Ui.DesignFont(Ui.Playfair); name.FontWeight = FontWeight.Normal; name.TextTrimming = TextTrimming.CharacterEllipsis;
        var text = Stack(0, name); if (DeskUnder(it) is { } u) { u.TextTrimming = TextTrimming.CharacterEllipsis; text.Children.Add(u); }
        text.VerticalAlignment = VerticalAlignment.Center;
        var row = Ui.Columns("Auto,*,Auto", dash, text, new AioReferenceValue(GValue(it.R, 52)) { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center });
        row.ColumnSpacing = 12; row.ColumnDefinitions[1].MinWidth = DeskNatural(name);
        return new Border { BorderBrush = Palette.Brush(Color.Parse("#262626")), BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 10, 0, 4), Child = row };
    }

    // ------------------------------------------------------------------ F-06 lava shelf
    private Control DeskLava(DeskItem it, int k)
    {
        var lamp = new ArtLavaLamp(k * 7 + 3) { Width = 60, Height = 220 };
        lamp.Set(it.R.Fraction, Celsius(it.R)); watchers.Add((it.R.Key, row => lamp.Set(row?.Fraction, Celsius(row))));
        var grid = new Grid { RowDefinitions = new RowDefinitions("*,Auto,Auto,Auto") };
        grid.Children.Add(new DeskElastic { Min = 120, Child = new Viewbox { Stretch = Stretch.Uniform, Child = lamp } });
        var value = Fit(GValue(it.R, 48)); value.HorizontalAlignment = HorizontalAlignment.Center; Grid.SetRow(value, 1); grid.Children.Add(value);
        var caption = DeskCaption(it.Caption, it.Colour); caption.HorizontalAlignment = HorizontalAlignment.Center; Grid.SetRow(caption, 2); grid.Children.Add(caption);
        if (DeskUnder(it) is { } u) { u.HorizontalAlignment = HorizontalAlignment.Center; u.TextTrimming = TextTrimming.CharacterEllipsis; Grid.SetRow(u, 3); grid.Children.Add(u); }
        return grid;
    }

    // ------------------------------------------------------------------ F-07 ambilight
    private Control DeskAmbiCard(DeskItem it, double big)
    {
        var light = new ArtAmbilight(); var caption = DeskCaption(it.Caption, it.Colour, big > 60 ? 15 : 13);
        void Heat(AioReading? row) { light.Set(Celsius(row)); caption.Foreground = Palette.Brush(HeatColour.Of(Celsius(row))); }
        Heat(it.R); watchers.Add((it.R.Key, Heat));
        var text = Stack(2, caption, FitLeft(GValue(it.R, big)));
        var under = DeskUnder(it); if (under != null) { under.TextTrimming = TextTrimming.CharacterEllipsis; text.Children.Add(under); }
        text.VerticalAlignment = VerticalAlignment.Center;
        var row = Ui.Columns("1.2*,1*", text, DeskStagePicture(it, DeskStage.Plain, big > 60 ? 110 : 60)); row.ColumnSpacing = 10;
        row.ColumnDefinitions[0].MinWidth = DeskNatural(caption, under);
        return new Border
        {
            CornerRadius = new CornerRadius(16), ClipToBounds = true, BorderThickness = new Thickness(1), BorderBrush = Palette.Brush(Color.Parse("#12161C")),
            Background = Palette.Brush(SeeThrough(Color.Parse("#07090C"))), Child = new Grid { Children = { light, new Border { Padding = new Thickness(28, 12, 14, 12), Child = row } } }
        };
    }

    // ------------------------------------------------------------------ F-08 versus
    private Control DeskVersusSide(DeskItem it, bool leftSide)
    {
        var caption = DeskCaption(it.Caption, it.Colour, 15);
        var value = GValue(it.R, 150);
        var head = Stack(2, caption);
        if (DeskUnder(it) is { } u) { u.TextTrimming = TextTrimming.CharacterEllipsis; head.Children.Add(u); }
        var number = leftSide ? FitLeft(value) : FitRight(value); number.VerticalAlignment = VerticalAlignment.Center;
        if (!leftSide) foreach (var c in head.Children) c.HorizontalAlignment = HorizontalAlignment.Right;
        // The number and the part side by side, the part towards the middle.
        var picture = new DeskElastic { Min = 90, Child = DeskStagePicture(it, DeskStage.Glow, 90) };
        var duel = leftSide ? Ui.Columns("1.5*,1*", number, picture) : Ui.Columns("1*,1.5*", picture, number); duel.ColumnSpacing = 10;
        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), RowSpacing = 4, Children = { head } };
        Grid.SetRow(duel, 1); grid.Children.Add(duel);
        return grid;
    }
    private Control DeskVersus(DeskItem a, DeskItem b)
    {
        TextBlock Letter(string text, Color colour) { var t = NText(text, 90, colour, true); t.FontStyle = FontStyle.Italic; TextFonts.Mark(t, TextRole.Title); return t; }
        var vs = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Children = { Letter("V", a.Colour), Letter("S", b.Colour) } };
        var row = new DeskSplit { Gap = 18 }.Add(DeskVersusSide(a, true), 1).Add(vs, 0).Add(DeskVersusSide(b, false), 1);
        return new Border { Padding = new Thickness(14, 10), Child = row };
    }

    // ------------------------------------------------------------------ F-09 esports broadcast
    private Control DeskScoreboard(List<DeskItem> big)
    {
        Control Team(DeskItem it, bool leftSide)
        {
            var name = NText(it.Caption, 30, Colors.Black, true); name.LetterSpacing = 4; name.TextTrimming = TextTrimming.CharacterEllipsis; name.HorizontalAlignment = HorizontalAlignment.Center;
            var picture = DeskStagePicture(it, DeskStage.Plain, 70);
            var grid = new Grid { RowDefinitions = new RowDefinitions("*,Auto"), RowSpacing = 6, Children = { picture } };
            var band = new Border { Background = Palette.Brush(it.Colour), CornerRadius = new CornerRadius(4), Padding = new Thickness(10, 4), Child = name };
            Grid.SetRow(band, 1); grid.Children.Add(band);
            return grid;
        }
        var live = DeskCaption("● Live", Color.Parse("#FF3B5C"), 14); live.HorizontalAlignment = HorizontalAlignment.Center;
        var scores = Ui.Columns("*,Auto,*", FitRight(GValue(big[0].R, 110)), NText(":", 80, nLabel, true), big.Count > 1 ? FitLeft(GValue(big[1].R, 110)) : new Control());
        scores.ColumnSpacing = 10; ((Control)scores.Children[1]).VerticalAlignment = VerticalAlignment.Center;
        var middle = Stack(4, live, scores); middle.VerticalAlignment = VerticalAlignment.Center;
        var unders = new List<Control>();
        foreach (var it in big) if (DeskUnder(it) is { } u) { u.TextTrimming = TextTrimming.CharacterEllipsis; u.HorizontalAlignment = HorizontalAlignment.Center; unders.Add(u); }
        if (unders.Count > 0) middle.Children.Add(Ui.Columns(string.Join(",", unders.Select(_ => "*")), unders.ToArray()));
        var extra = big.Skip(2).Select(i => (Control)Stack(0, DeskCaption(i.Caption, i.Colour), FitLeft(GValue(i.R, 40)))).ToArray();
        if (extra.Length > 0) middle.Children.Add(DeskRow(extra, 120, 10));
        var row = new DeskSplit().Add(Team(big[0], true), 1).Add(middle, 2);
        if (big.Count > 1) row.Add(Team(big[1], false), 1);
        return DeskCard(row, new Thickness(18, 14));
    }

    private Control DeskFeedRow(DeskItem it)
    {
        var caption = DeskCaption(it.Caption, it.Colour, 15); caption.VerticalAlignment = VerticalAlignment.Center;
        var under = DeskUnder(it); Control text = caption;
        if (under != null) { under.TextTrimming = TextTrimming.CharacterEllipsis; text = Stack(0, caption, under); text.VerticalAlignment = VerticalAlignment.Center; }
        var row = Ui.Columns("*,Auto", text, new AioReferenceValue(GValue(it.R, 34)) { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center });
        row.ColumnSpacing = 10; row.ColumnDefinitions[0].MinWidth = DeskNatural(caption, under);
        return new Border { Background = Palette.Brush(SeeThrough(Color.Parse("#0D1018"))), BorderBrush = Palette.Brush(it.Colour), BorderThickness = new Thickness(4, 0, 0, 0), Padding = new Thickness(14, 8), Child = row };
    }

    // ------------------------------------------------------------------ F-10 vital signs
    private Control DeskVitalRow(DeskItem it, int k)
    {
        var colours = new[] { "#3DFF8B", "#FFD43D", "#40C8FF", "#FF6AD5", "#FF9A3D", "#B57CFF" };
        var colour = Color.Parse(colours[k % colours.Length]);
        var ecg = new ArtEcg(colour, k); ecg.Set(it.R.Fraction); watchers.Add((it.R.Key, row => ecg.Set(row?.Fraction)));
        var caption = DeskCaption(it.Caption, colour, 13);
        var head = Stack(0, caption); var under = DeskUnder(it); if (under != null) { under.TextTrimming = TextTrimming.CharacterEllipsis; head.Children.Add(under); }
        var trace = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), Children = { head } };
        var line = new DeskElastic { Min = 36, Child = ecg }; Grid.SetRow(line, 1); trace.Children.Add(line);
        var number = GValue(it.R, 64); number.Foreground = Palette.Brush(Palette.Mix(colour, Colors.White, .72));
        var value = FitRight(number); value.VerticalAlignment = VerticalAlignment.Center;
        // In a narrow window the picture and the number give way, never the name.
        var row = Ui.Columns(".5*,1.6*,1*", DeskStagePicture(it, DeskStage.Plain, 50), trace, value); row.ColumnSpacing = 14;
        row.ColumnDefinitions[0].MaxWidth = 86; row.ColumnDefinitions[2].MaxWidth = 180;
        row.ColumnDefinitions[1].MinWidth = DeskNatural(caption, under);
        return new Border { BorderBrush = Palette.Brush(Color.Parse("#161616")), BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(0, 6), Child = row };
    }

    // ------------------------------------------------------------------ F-11 network globe
    private void DeskGlobe()
    {
        var net = Rs("Network");
        if (net.Length == 0) return;
        var down = net.FirstOrDefault(r => r.Key.EndsWith(".down")); var up = net.FirstOrDefault(r => r.Key.EndsWith(".up")); var link = net.FirstOrDefault(r => r.Key.EndsWith(".link"));
        var globe = new ArtGlobe(NAcc(0), Color.Parse("#FFAA3C"));
        void Spin(AioReading? _) => globe.Set(down?.Fraction ?? 0, up?.Fraction ?? 0);
        Spin(null); if (down != null) watchers.Add((down.Key, Spin)); if (up != null) watchers.Add((up.Key, Spin));
        Control Column(AioReading r, string caption, Color colour, double size)
        {
            var label = DeskCaption(caption, nLabel, 12); label.HorizontalAlignment = HorizontalAlignment.Center;
            var value = NV(r, size, colour); value.TextWrapping = TextWrapping.NoWrap; var fit = Fit(value); fit.HorizontalAlignment = HorizontalAlignment.Center;
            return Stack(0, label, fit);
        }
        var columns = new List<Control>();
        if (down != null) columns.Add(Column(down, "↓ DL", NAcc(0), 48));
        if (up != null) columns.Add(Column(up, "↑ Up", Color.Parse("#FFAA3C"), 48));
        if (link != null) columns.Add(Column(link, "Link", nInk, 24));
        var body = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto,Auto"), RowSpacing = 10 };
        body.Children.Add(DeskTitle(L.T("Network"), NAcc(0)));
        if (columns.Count > 0) { var row = Ui.Columns(string.Join(",", columns.Select(_ => "*")), columns.ToArray()); row.ColumnSpacing = 12; Grid.SetRow(row, 1); body.Children.Add(row); }
        var sphere = new DeskElastic { Min = 160, Child = globe }; Grid.SetRow(sphere, 2); body.Children.Add(sphere);
        var others = net.Where(r => r != down && r != up && r != link).ToArray();
        if (others.Length > 0) { var list = DeskList(others); Grid.SetRow(list, 3); body.Children.Add(list); }
        // The history of the network under the globe, when its switch is on.
        var first = down ?? up;
        if (first != null && AioReadings.Graphs(settings, first.Key))
        {
            var plot = HistoryPlot(first, 32, "area", NAcc(0), first == down ? up : null); plot.Height = double.NaN;
            var fill = new DeskElastic { Min = 40, Max = 90, Child = plot }; Grid.SetRow(fill, 4); body.Children.Add(fill);
        }
        deskParts["scene"] = DeskCard(body);
    }

    // ------------------------------------------------------------------ F-12 ambient clock
    private Control DeskClockStat(DeskItem it)
    {
        var caption = DeskCaption(it.Caption, it.Colour, 13);
        var value = GValue(it.R, 70); value.FontWeight = FontWeight.Light;
        var text = Stack(2, caption, FitLeft(value));
        if (DeskUnder(it) is { } u) { u.TextWrapping = TextWrapping.Wrap; text.Children.Add(u); }
        return new Border { BorderBrush = Palette.Brush(Palette.Mix(nSurface, Colors.White, .14)), BorderThickness = new Thickness(1, 0, 0, 0), Padding = new Thickness(18, 4, 10, 4), Child = text };
    }

    // ------------------------------------------------------------------ F-13 EV console
    private Control DeskConsole(DeskItem lead, List<DeskItem> items)
    {
        var top = new ArtCaseTop();
        var spots = new Dictionary<string, Rect>
        {
            ["cpu"] = new(.36, .08, .30, .2), ["ram"] = new(.72, .05, .14, .32), ["gpu"] = new(.08, .38, .84, .16),
        };
        int drive = 0, gpu = 0;
        foreach (var it in items)
        {
            Rect box;
            if (it.Kind == "drive") { if (drive > 1) continue; box = new Rect(drive == 0 ? .1 : .52, .62, .38, .1); drive++; }
            else if (it.Kind == "gpu") { if (gpu > 1) continue; box = gpu == 0 ? spots["gpu"] : new Rect(.08, .78, .84, .12); gpu++; }
            else if (!spots.TryGetValue(it.Kind, out box)) continue;
            int at = top.Add(it.Caption, box); top.Set(at, Celsius(it.R)); watchers.Add((it.R.Key, row => top.Set(at, Celsius(row))));
        }
        var caption = DeskCaption(lead.Caption, lead.Colour, 15);
        var value = GValue(lead.R, 150); value.FontWeight = FontWeight.Light;
        var text = Stack(2, caption, FitLeft(value));
        if (DeskUnder(lead) is { } u) { u.TextTrimming = TextTrimming.CharacterEllipsis; text.Children.Add(u); }
        text.VerticalAlignment = VerticalAlignment.Center;
        var row = Ui.Columns("1*,1.3*", text, new DeskElastic { Min = 160, Child = top }); row.ColumnSpacing = 18;
        row.ColumnDefinitions[0].MinWidth = DeskNatural(caption);
        return DeskCard(row, new Thickness(22, 14));
    }

    // ------------------------------------------------------------------ F-14 server rack
    private Control DeskRackUnit(DeskItem it)
    {
        var caption = DeskCaption(it.Caption, nInk, 15);
        var head = Stack(0, caption); var under = DeskUnder(it); if (under != null) { under.TextTrimming = TextTrimming.CharacterEllipsis; head.Children.Add(under); }
        var lcdText = GValue(it.R, 40); lcdText.FontFamily = Ui.DesignFont(Ui.FamilyFor("roboto-mono")); lcdText.Foreground = Palette.Brush(Color.Parse("#C8FFDC"));
        lcdText.Effect = new DropShadowEffect { BlurRadius = 10, OffsetX = 0, OffsetY = 0, Color = Color.FromArgb(150, 61, 255, 139) };
        var lcd = new Border { Background = Palette.Brush(Color.Parse("#030504")), BorderBrush = Palette.Brush(Color.Parse("#1A2A1E")), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), Padding = new Thickness(10, 2), Child = FitLeft(lcdText) };
        var leds = new ArtRackLeds(Color.Parse("#3DFF8B")) { VerticalAlignment = VerticalAlignment.Center };
        void Level(AioReading? row) => leds.Set(row?.Fraction); Level(it.R); watchers.Add((it.R.Key, Level));
        lcd.HorizontalAlignment = HorizontalAlignment.Left;
        var display = Ui.Columns("1*,1.6*", lcd, leds); display.ColumnSpacing = 14; display.ColumnDefinitions[0].MaxWidth = 170;
        var middle = Stack(6, head, display); middle.VerticalAlignment = VerticalAlignment.Center;
        var row = Ui.Columns("Auto,.6*,2*,Auto", new ArtRackEar(), DeskStagePicture(it, DeskStage.Plain, 46), middle, new ArtRackEar()); row.ColumnSpacing = 12;
        row.ColumnDefinitions[1].MaxWidth = 90;
        row.ColumnDefinitions[2].MinWidth = DeskNatural(caption, under);
        return DeskCard(row, new Thickness(0, 6));
    }

    // ------------------------------------------------------------------ F-15 turntable
    private void DeskRecords()
    {
        var fans = Rs("Cooling");
        if (fans.Length == 0) return;
        var colours = new[] { "#FF4FD8", "#FFAA3C", "#B57CFF", "#40DCC8", "#5AA9FF", "#FFD24F" };
        var records = new DeskFlow { MinItem = 190, Gap = 14 };
        for (int i = 0; i < fans.Length; i++)
        {
            var f = fans[i];
            var value = NV(f, 30, Palette.Mix(Color.Parse(colours[i % colours.Length]), Colors.White, .72)); formats[value] = row => row.Number.HasValue ? Format.Number(row.Number) : "—"; value.Text = formats[value](f); value.TextWrapping = TextWrapping.NoWrap;
            var name = NText(Naming.Short(Translate(L.T(f.Label))) + " · rpm", 12, nLabel, true); name.TextTrimming = TextTrimming.CharacterEllipsis; name.HorizontalAlignment = HorizontalAlignment.Center;
            // The number under the record, in the colour of its label: readable however small the record gets.
            var number = Fit(value); number.HorizontalAlignment = HorizontalAlignment.Center;
            var disc = new DeskSquare { Share = 1, Child = new ArtRecord(Color.Parse(colours[i % colours.Length])) };
            var record = new Grid { RowDefinitions = new RowDefinitions("*,Auto,Auto"), RowSpacing = 2, Children = { new DeskElastic { Min = 90, Child = disc } } };
            Grid.SetRow(number, 1); record.Children.Add(number);
            Grid.SetRow(name, 2); record.Children.Add(name);
            records.Children.Add(record);
        }
        var body = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), RowSpacing = 10 };
        body.Children.Add(DeskTitle(L.T("Fans") + " · rpm", nInk));
        Grid.SetRow(records, 1); body.Children.Add(records);
        deskParts["scene"] = DeskCard(body);
    }

    private Control DeskChannel(DeskItem it)
    {
        var fader = new ArtFader { Width = 40, HorizontalAlignment = HorizontalAlignment.Center };
        fader.Set(it.R.Fraction); watchers.Add((it.R.Key, row => fader.Set(row?.Fraction)));
        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto"), RowSpacing = 6 };
        var caption = DeskCaption(it.Caption, it.Colour, 12); caption.HorizontalAlignment = HorizontalAlignment.Center; grid.Children.Add(caption);
        var holder = new DeskElastic { Min = 90, Child = fader }; Grid.SetRow(holder, 1); grid.Children.Add(holder);
        var value = Fit(GValue(it.R, 28)); value.HorizontalAlignment = HorizontalAlignment.Center; Grid.SetRow(value, 2); grid.Children.Add(value);
        if (DeskUnder(it) is { } u) { u.HorizontalAlignment = HorizontalAlignment.Center; u.TextAlignment = TextAlignment.Center; u.TextWrapping = TextWrapping.Wrap; Grid.SetRow(u, 3); grid.Children.Add(u); }
        return grid;
    }

    // ------------------------------------------------------------------ F-17 pit wall, F-18 trading floor
    private Control DeskTimingTower(List<DeskItem> items)
    {
        var teams = new[] { "#27F4D2", "#FF8000", "#B57CFF", "#3671C6", "#E10600", "#FFD24F" };
        var title = DeskCaption("Timing · live", Color.Parse("#E10600"), 14); TextFonts.Mark(title, TextRole.Title);
        var rows = items.Select((it, k) =>
        {
            var name = NText(it.Caption, 26, nInk, true); name.TextTrimming = TextTrimming.CharacterEllipsis; name.VerticalAlignment = VerticalAlignment.Center;
            var line = Ui.Columns("Auto,*,Auto", new ArtTimingRank(k + 1, Color.Parse(teams[k % teams.Length])) { VerticalAlignment = VerticalAlignment.Stretch }, name,
                new AioReferenceValue(GValue(it.R, 38)) { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center });
            line.ColumnSpacing = 12; line.ColumnDefinitions[1].MinWidth = DeskNatural(name);
            return (Control)new Border { MinHeight = 52, BorderBrush = Palette.Brush(Color.Parse("#1B1B24")), BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(0, 4), Child = line };
        }).ToArray();
        var list = DeskRow(rows, 0, 2, true);
        var body = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), RowSpacing = 8, Children = { title } };
        Grid.SetRow(list, 1); body.Children.Add(list);
        return DeskCard(body);
    }

    /// <summary>The history of processor and graphics cards as a screen of lines (no numbers: the values are in the tower or ticker).</summary>
    private Control? DeskTelemetryScreen(List<DeskItem> big)
    {
        var shown = big.Where(i => AioReadings.Graphs(settings, i.R.Key)).ToList();
        if (shown.Count == 0) return null;
        var body = new Grid { RowDefinitions = new RowDefinitions("Auto," + string.Join(",", shown.Select(_ => "*"))), RowSpacing = 10 };
        var title = DeskCaption(designStyle == WidgetStyle.AioDeskTrading ? "Charts" : "Telemetry", designStyle == WidgetStyle.AioDeskTrading ? Color.Parse("#26D07C") : Color.Parse("#E10600"), 14);
        TextFonts.Mark(title, TextRole.Title); body.Children.Add(title);
        for (int k = 0; k < shown.Count; k++)
        {
            var it = shown[k];
            var plot = HistoryPlot(it.R, 40, designStyle == WidgetStyle.AioDeskTrading ? "line" : "area", it.Colour); plot.Height = double.NaN;
            var lane = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), Children = { DeskCaption(it.Caption + (it.Model.Length > 0 ? " · " + it.Model : ""), it.Colour, 12) } };
            var fill = new DeskElastic { Min = 40, Child = plot }; Grid.SetRow(fill, 1); lane.Children.Add(fill);
            Grid.SetRow(lane, k + 1); body.Children.Add(lane);
        }
        return DeskCard(body);
    }

    private Control DeskTicker(DeskItem it)
    {
        var arrow = NText("▲", 16, Color.Parse("#26D07C"), true); arrow.VerticalAlignment = VerticalAlignment.Center;
        double? last = it.R.Number;
        watchers.Add((it.R.Key, row =>
        {
            if (row?.Number is not double now || last is not double before) { last = row?.Number; return; }
            bool rising = now >= before; last = now;
            arrow.Text = rising ? "▲" : "▼"; arrow.Foreground = Palette.Brush(Color.Parse(rising ? "#26D07C" : "#FF4D5A"));
        }));
        var caption = DeskCaption(it.Caption, nLabel, 13); caption.VerticalAlignment = VerticalAlignment.Center;
        var row = Ui.Columns("Auto,*,Auto", caption, FitLeft(GValue(it.R, 34)), arrow); row.ColumnSpacing = 10;
        return DeskCard(row, new Thickness(14, 8));
    }

    // ------------------------------------------------------------------ F-19 big type
    private Control DeskBigStat(DeskItem it, bool first)
    {
        var red = Color.Parse("#FF3B30");
        var caption = DeskCaption(it.Caption, first ? red : nInk, 16);
        var value = GValue(it.R, first ? 200 : 140); value.FontWeight = FontWeight.Black;
        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), RowSpacing = 4, Children = { caption } };
        var number = FitLeft(value); number.VerticalAlignment = VerticalAlignment.Top;
        Grid.SetRow(number, 1); grid.Children.Add(number);
        if (DeskUnder(it) is { } u) { u.TextTrimming = TextTrimming.CharacterEllipsis; Grid.SetRow(u, 2); grid.Children.Add(u); }
        return new Border { BorderBrush = Palette.Brush(first ? red : nInk), BorderThickness = new Thickness(0, first ? 6 : 3, 0, 0), Padding = new Thickness(0, 12, 0, 0), Child = grid };
    }
}
