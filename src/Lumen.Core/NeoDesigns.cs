namespace Lumen.Core;

/// <summary>
/// The Cyberpunk and Neumorph designs (5.1.0, chosen by the user from the style drafts): one AIO card each, every value
/// bound to its sensor like in all AIO designs. Each design has a fixed look; a colour chosen in Appearance replaces the
/// accents.
/// </summary>
public static class NeoDesigns
{
    /// <summary>Cyber and Neumorph (5.1.0); the ten art families of 5.2.0: LED dot matrix, Tron grid, code rain, Tokyo neon,
    /// thermal camera, phone widgets, oscilloscope, stealth, black and gold, sumi-e ink.</summary>
    public enum Family { Cyber, Neumorph, Matrix, Tron, Rain, Neon, Thermal, Phone, Scope, Stealth, Gold, Ink, Gallery,
        Vfd, Synth, Radar, Hex, Pixel, Tubes, Mission, Swiss, Chalk, Equalizer,
        Skyline, Cubes, Tilted, Torus, Exploded, Chrome, Bars3D, Hologram, Cylinders, Tower,
        Race, Scada, GaugeWall, Mixer, Capsule, Floating, PartCards, Podiums, Strip, HeatBar, HeatList, Rings, Timing, Home, GlassTubes, GlassDial, ChromeBezel, GlossPills,
        /// <summary>5.8.0: full-screen desktop designs; every value in a scene that fills the screen (see AioDesigns.Desktop).</summary>
        Desk }
    /// <summary>Cyber: Stack (one panel per part) or Split (processor and graphics side by side).
    /// Neumorph: Dials (2 × 2 rings), Hero (one large processor ring) or Bars (one bar per part).</summary>
    public enum Arrangement { Stack, Split, Dials, Hero, Bars }
    /// <param name="Accents">Four accents (processor, graphics, memory, drive); Cyber designs use the first two.</param>
    /// <param name="Surface">Card colour; Neumorph also draws its light and dark shadows from it.</param>
    public sealed record Look(WidgetStyle Style, string Name, Family Kind, Arrangement Layout, string[] Accents, string Surface,
        bool Parts = false, bool RedParts = false, GalleryDesigns.Design? Gallery = null);

    private static readonly Look[] Own =
    {
        new(WidgetStyle.AioCyberArasaka, "Cyber Arasaka", Family.Cyber, Arrangement.Split, new[] { "#FF2238", "#F2F2F2" }, "#070203"),
        new(WidgetStyle.AioCyberNetrunner, "Cyber Netrunner", Family.Cyber, Arrangement.Stack, new[] { "#FF2BD6", "#00F0FF" }, "#0D0612"),
        new(WidgetStyle.AioCyberMilitech, "Cyber Militech", Family.Cyber, Arrangement.Split, new[] { "#39FF14", "#C8FFB8" }, "#030803"),
        new(WidgetStyle.AioCyberAfterlife, "Cyber Afterlife", Family.Cyber, Arrangement.Stack, new[] { "#FCEE0A", "#00F0FF" }, "#0A0A0F", Parts: true),
        new(WidgetStyle.AioCyberRedline, "Cyber Redline", Family.Cyber, Arrangement.Split, new[] { "#FF2238", "#FFFFFF" }, "#080203", Parts: true, RedParts: true),
        new(WidgetStyle.AioCyberBlackIce, "Cyber Black Ice", Family.Cyber, Arrangement.Stack, new[] { "#5CE1FF", "#E6F7FF" }, "#04090D"),
        new(WidgetStyle.AioNeoBlackRed, "Neumorph Black Red", Family.Neumorph, Arrangement.Dials, new[] { "#FF2238", "#FF2238", "#FF2238", "#FF2238" }, "#121316"),
        new(WidgetStyle.AioNeoHero, "Neumorph Hero", Family.Neumorph, Arrangement.Hero, new[] { "#FF7A59", "#59A8FF", "#59FFB2", "#C08CFF" }, "#22252B"),
        new(WidgetStyle.AioNeoParts, "Neumorph Parts", Family.Neumorph, Arrangement.Bars, new[] { "#FF7A59", "#59A8FF", "#59FFB2", "#C08CFF" }, "#22252B", Parts: true),
        new(WidgetStyle.AioNeoRedParts, "Neumorph Red Parts", Family.Neumorph, Arrangement.Bars, new[] { "#FF2238", "#FF2238", "#FF2238", "#FF2238" }, "#1C1416", Parts: true, RedParts: true),
        new(WidgetStyle.AioNeoCyanHero, "Neumorph Cyan", Family.Neumorph, Arrangement.Hero, new[] { "#22D3EE", "#22D3EE", "#22D3EE", "#22D3EE" }, "#121316"),
        new(WidgetStyle.AioNeoMono, "Neumorph Mono", Family.Neumorph, Arrangement.Bars, new[] { "#F2F2F2", "#F2F2F2", "#F2F2F2", "#F2F2F2" }, "#121316"),
        // 5.2.0: ten designs from the art drafts, all on black.
        new(WidgetStyle.AioArtDotMatrix, "LED Dot Matrix", Family.Matrix, Arrangement.Bars, new[] { "#FFA31A", "#FFA31A", "#FFA31A", "#FF6A1A" }, "#050403"),
        new(WidgetStyle.AioArtTronGrid, "Tron Grid", Family.Tron, Arrangement.Dials, new[] { "#00E5FF", "#FF7A00", "#00E5FF", "#FF7A00" }, "#000000"),
        new(WidgetStyle.AioArtCodeRain, "Code Rain", Family.Rain, Arrangement.Stack, new[] { "#3BFF6A", "#A8FFBC", "#3BFF6A", "#3BFF6A" }, "#000000"),
        new(WidgetStyle.AioArtTokyoNeon, "Tokyo Neon", Family.Neon, Arrangement.Split, new[] { "#FF3DB8", "#29F0FF", "#FFE14D", "#29F0FF" }, "#030203"),
        new(WidgetStyle.AioArtThermal, "Thermal Camera", Family.Thermal, Arrangement.Dials, new[] { "#FF8A1A", "#FFD23C", "#B4329A", "#FF8A1A" }, "#050308"),
        new(WidgetStyle.AioArtPhone, "Phone Widgets", Family.Phone, Arrangement.Dials, new[] { "#30D158", "#FF9F0A", "#0A84FF", "#BF5AF2" }, "#000000"),
        new(WidgetStyle.AioArtScope, "Oscilloscope", Family.Scope, Arrangement.Stack, new[] { "#7CFFB2", "#FFD84D", "#4DD8FF", "#7CFFB2" }, "#0A0B0A"),
        new(WidgetStyle.AioArtStealth, "Stealth", Family.Stealth, Arrangement.Stack, new[] { "#FF2A2A", "#D8D8DC", "#FF2A2A", "#FF2A2A" }, "#0D0D0E"),
        new(WidgetStyle.AioArtBlackGold, "Black & Gold", Family.Gold, Arrangement.Dials, new[] { "#D4AF6A", "#D4AF6A", "#D4AF6A", "#D4AF6A" }, "#070604"),
        new(WidgetStyle.AioArtSumiInk, "Sumi-e Ink", Family.Ink, Arrangement.Hero, new[] { "#F2EFE8", "#F2EFE8", "#F2EFE8", "#C8102E" }, "#0B0B0A"),
    };
    /// <summary>5.4.0: ten designs from the drafts 11–20, each with a layout of its own; 5.5.0: ten 3D designs.</summary>
    private static readonly Look[] Studio =
    {
        new(WidgetStyle.AioVfdStereo, "VFD Stereo", Family.Vfd, Arrangement.Stack, new[] { "#38F2DC", "#FF9A3C", "#38F2DC", "#38F2DC" }, "#040706"),
        new(WidgetStyle.AioSynthwave, "Synthwave", Family.Synth, Arrangement.Dials, new[] { "#FF5FA2", "#FF9A5F", "#C084FC", "#FF5FA2" }, "#0B0412"),
        new(WidgetStyle.AioRadar, "Radar", Family.Radar, Arrangement.Dials, new[] { "#4BE36B", "#4BE36B", "#4BE36B", "#4BE36B" }, "#030803"),
        new(WidgetStyle.AioHexGrid, "Hex Grid", Family.Hex, Arrangement.Dials, new[] { "#3FA9F5", "#F5A623", "#3FA9F5", "#F5A623" }, "#05080D"),
        new(WidgetStyle.AioPixel8, "8-Bit Pixel", Family.Pixel, Arrangement.Bars, new[] { "#3DFF6E", "#FFA53D", "#C08CFF", "#4DB2FF" }, "#000000"),
        new(WidgetStyle.AioLabTubes, "Lab Tubes", Family.Tubes, Arrangement.Dials, new[] { "#FF7A59", "#FFB347", "#4DD0F5", "#B48CFF" }, "#08090B"),
        new(WidgetStyle.AioMissionControl, "Mission Control", Family.Mission, Arrangement.Stack, new[] { "#FF8A3D", "#FF8A3D", "#FF8A3D", "#FF8A3D" }, "#060606"),
        new(WidgetStyle.AioSwiss, "Swiss", Family.Swiss, Arrangement.Stack, new[] { "#FF3B30", "#FF3B30", "#FF3B30", "#FF3B30" }, "#0A0A0A"),
        new(WidgetStyle.AioChalkboard, "Chalkboard", Family.Chalk, Arrangement.Dials, new[] { "#F2D27A", "#8EC5FF", "#F2A3C7", "#A6E3A1" }, "#121916"),
        new(WidgetStyle.AioEqualizer, "Equalizer", Family.Equalizer, Arrangement.Bars, new[] { "#3DFF6E", "#3DFF6E", "#3DFF6E", "#3DFF6E" }, "#050605"),
        // 5.5.0: the 3D designs.
        new(WidgetStyle.AioIsoSkyline, "Iso Skyline", Family.Skyline, Arrangement.Dials, new[] { "#38E1FF", "#FF4FD8", "#7CFFB2", "#FFC34D" }, "#04070A"),
        new(WidgetStyle.AioGlassCubes, "Glass Cubes", Family.Cubes, Arrangement.Dials, new[] { "#4DD8FF", "#B07CFF", "#5CFFB0", "#FFB84D" }, "#030406"),
        new(WidgetStyle.AioPerspectiveHud, "Perspective HUD", Family.Tilted, Arrangement.Stack, new[] { "#29F0FF", "#FF5FD2", "#29F0FF", "#29F0FF" }, "#02080A"),
        new(WidgetStyle.AioTorusRings, "Torus Rings", Family.Torus, Arrangement.Dials, new[] { "#FF7A59", "#59A8FF", "#59FFB2", "#C08CFF" }, "#060607"),
        new(WidgetStyle.AioExplodedStack, "Exploded Stack", Family.Exploded, Arrangement.Dials, new[] { "#5CE1FF", "#5CE1FF", "#5CE1FF", "#FFB84D" }, "#03080B"),
        new(WidgetStyle.AioChromeNumbers, "Chrome Numbers", Family.Chrome, Arrangement.Stack, new[] { "#B91C1C", "#1D4ED8", "#6B7280", "#6B7280" }, "#050505"),
        new(WidgetStyle.AioBarChart3D, "3D Bar Chart", Family.Bars3D, Arrangement.Dials, new[] { "#3DFF6E", "#3DFF6E", "#3DFF6E", "#FFD84D" }, "#020703"),
        new(WidgetStyle.AioHologramPads, "Hologram Pads", Family.Hologram, Arrangement.Dials, new[] { "#3DE7FF", "#B57CFF", "#4DFFA8", "#FFB84D" }, "#03060B"),
        new(WidgetStyle.AioGlassCylinders, "Glass Cylinders", Family.Cylinders, Arrangement.Dials, new[] { "#FF7A59", "#59A8FF", "#59FFB2", "#FFC34D" }, "#060607"),
        new(WidgetStyle.AioIsoTower, "Iso Tower", Family.Tower, Arrangement.Dials, new[] { "#FF3B5C", "#59A8FF", "#59FFB2", "#FFC34D" }, "#070304"),
        // 5.6.0: dashboards (drafts D-06 … D-09) and Capsule Glass (N-01).
        new(WidgetStyle.AioRaceTelemetry, "Race Telemetry", Family.Race, Arrangement.Dials, new[] { "#3DFF8B", "#5AA9FF", "#FFD400", "#FF3B3B" }, "#0B0B0B"),
        new(WidgetStyle.AioControlRoom, "Control Room", Family.Scada, Arrangement.Dials, new[] { "#FF8A3D", "#3DA5FF", "#3DFF8B", "#C08CFF" }, "#06080A"),
        new(WidgetStyle.AioGaugeWall, "Gauge Wall", Family.GaugeWall, Arrangement.Dials, new[] { "#FF7A3D", "#3DB4FF", "#3DFF8B", "#B27CFF" }, "#000000"),
        new(WidgetStyle.AioStudioMixer, "Studio Mixer", Family.Mixer, Arrangement.Dials, new[] { "#E3D3A6", "#E3D3A6", "#3DFF8B", "#C8102E" }, "#0C0C0C"),
        new(WidgetStyle.AioCapsuleGlass, "Capsule Glass", Family.Capsule, Arrangement.Dials, new[] { "#FF8A5B", "#5BA8FF", "#5BFFB0", "#C38BFF" }, "#05060A"),
        // 5.6.0: Living PC (drafts L-07 and L-08): the parts glow in the colour of their temperature.
        new(WidgetStyle.AioFloatingParts, "Floating Parts", Family.Floating, Arrangement.Dials, new[] { "#40DCC8", "#FFAA3C", "#40DCC8", "#FFAA3C" }, "#05070A"),
        new(WidgetStyle.AioPartCards, "Part Cards", Family.PartCards, Arrangement.Dials, new[] { "#40DCC8", "#FFAA3C", "#40DCC8", "#FFAA3C" }, "#000000"),
        new(WidgetStyle.AioFloatingPodiums, "Floating Podiums", Family.Podiums, Arrangement.Dials, new[] { "#40DCC8", "#FFAA3C", "#40DCC8", "#FFAA3C" }, "#05070A"),
        new(WidgetStyle.AioFloatingStrip, "Floating Strip", Family.Strip, Arrangement.Dials, new[] { "#40DCC8", "#FFAA3C", "#40DCC8", "#FFAA3C" }, "#05070A"),
        new(WidgetStyle.AioPartCardsHeat, "Part Cards Heat", Family.HeatBar, Arrangement.Dials, new[] { "#40DCC8", "#FFAA3C", "#40DCC8", "#FFAA3C" }, "#000000"),
        new(WidgetStyle.AioPartList, "Part List", Family.HeatList, Arrangement.Dials, new[] { "#40DCC8", "#FFAA3C", "#40DCC8", "#FFAA3C" }, "#000000"),
        // 5.7.0: designs after well-known interfaces (drafts W-01, W-07, W-24).
        new(WidgetStyle.AioActivityRings, "Pulse Rings", Family.Rings, Arrangement.Dials, new[] { "#FF7A1A", "#9B7CFF", "#2EE6B8", "#FFD60A" }, "#000000"),
        new(WidgetStyle.AioLiveTiming, "Live Timing", Family.Timing, Arrangement.Dials, new[] { "#E10600", "#27F4D2", "#FF8000", "#3671C6" }, "#000000"),
        new(WidgetStyle.AioSmartHome, "Smart Home", Family.Home, Arrangement.Dials, new[] { "#FFD60A", "#0A84FF", "#30D158", "#BF5AF2" }, "#000000"),
        // 5.7.0: glass and glossy designs (drafts G-05, G-08, G-13, G-15).
        new(WidgetStyle.AioGlassTubes, "Glass Tubes", Family.GlassTubes, Arrangement.Dials, new[] { "#FF6A3D", "#3DA5FF", "#3DFFA8", "#FFC93D" }, "#05060A"),
        new(WidgetStyle.AioGlassDial, "Glass Dial", Family.GlassDial, Arrangement.Dials, new[] { "#7CF3FF", "#FF8AD8", "#B07CFF", "#5AA9FF" }, "#05060A"),
        new(WidgetStyle.AioChromeBezels, "Chrome Bezels", Family.ChromeBezel, Arrangement.Dials, new[] { "#FF4D4D", "#4DA6FF", "#3DFF8B", "#FFC34D" }, "#000000"),
        new(WidgetStyle.AioGlossyPills, "Glossy Pills", Family.GlossPills, Arrangement.Dials, new[] { "#FF3B5C", "#3B7BFF", "#2FD17A", "#FFAE2B" }, "#000000"),
        // 5.8.0: full-screen desktop designs. Accents: processor, graphics, memory, drives.
        new(WidgetStyle.AioDeskBoard, "Desktop Mainboard", Family.Desk, Arrangement.Dials, new[] { "#40DCC8", "#B57CFF", "#40DCC8", "#5AA9FF" }, "#05080B"),
        new(WidgetStyle.AioDeskBoardLeft, "Desktop Mainboard Left", Family.Desk, Arrangement.Dials, new[] { "#40DCC8", "#B57CFF", "#40DCC8", "#5AA9FF" }, "#05080B"),
        // Living PC (drafts LP-01 … LP-10).
        new(WidgetStyle.AioDeskShowcase, "Desktop Showcase", Family.Desk, Arrangement.Dials, new[] { "#40DCC8", "#FFAA3C", "#40DCC8", "#5AA9FF" }, "#05080B"),
        new(WidgetStyle.AioDeskPodiums, "Desktop Podium Hall", Family.Desk, Arrangement.Dials, new[] { "#40DCC8", "#FFAA3C", "#40DCC8", "#5AA9FF" }, "#05080B"),
        new(WidgetStyle.AioDeskCards, "Desktop Part Cards", Family.Desk, Arrangement.Dials, new[] { "#40DCC8", "#FFAA3C", "#40DCC8", "#5AA9FF" }, "#05080B"),
        new(WidgetStyle.AioDeskHero, "Desktop Hero", Family.Desk, Arrangement.Dials, new[] { "#40DCC8", "#FFAA3C", "#40DCC8", "#5AA9FF" }, "#05080B"),
        new(WidgetStyle.AioDeskList, "Desktop Part List", Family.Desk, Arrangement.Dials, new[] { "#40DCC8", "#FFAA3C", "#40DCC8", "#5AA9FF" }, "#05080B"),
        new(WidgetStyle.AioDeskIsland, "Desktop Floating Island", Family.Desk, Arrangement.Dials, new[] { "#40DCC8", "#FFAA3C", "#40DCC8", "#5AA9FF" }, "#05080B"),
        new(WidgetStyle.AioDeskBento, "Desktop Bento", Family.Desk, Arrangement.Dials, new[] { "#40DCC8", "#FFAA3C", "#40DCC8", "#5AA9FF" }, "#05080B"),
        new(WidgetStyle.AioDeskCase, "Desktop Inside the Case", Family.Desk, Arrangement.Dials, new[] { "#40DCC8", "#FFAA3C", "#40DCC8", "#5AA9FF" }, "#05080B"),
        new(WidgetStyle.AioDeskHeatFrame, "Desktop Heat Frame", Family.Desk, Arrangement.Dials, new[] { "#40DCC8", "#FFAA3C", "#40DCC8", "#5AA9FF" }, "#05080B"),
        new(WidgetStyle.AioDeskHistory, "Desktop History Wall", Family.Desk, Arrangement.Dials, new[] { "#40DCC8", "#FFAA3C", "#40DCC8", "#5AA9FF" }, "#05080B"),
        // Effects (drafts FX-01 … FX-10).
        new(WidgetStyle.AioDeskCommand, "Desktop Command Deck", Family.Desk, Arrangement.Dials, new[] { "#40DCC8", "#B57CFF", "#40DCC8", "#5AA9FF" }, "#05080B"),
        new(WidgetStyle.AioDeskExploded, "Desktop Exploded View", Family.Desk, Arrangement.Dials, new[] { "#40DCC8", "#B57CFF", "#40DCC8", "#5AA9FF" }, "#05080B"),
        new(WidgetStyle.AioDeskThermal, "Desktop Thermal Camera", Family.Desk, Arrangement.Dials, new[] { "#40DCC8", "#B57CFF", "#40DCC8", "#5AA9FF" }, "#05080B"),
        new(WidgetStyle.AioDeskHalo, "Desktop Halo Grid", Family.Desk, Arrangement.Dials, new[] { "#40DCC8", "#B57CFF", "#40DCC8", "#5AA9FF" }, "#05080B"),
        new(WidgetStyle.AioDeskSpotlight, "Desktop Spotlight", Family.Desk, Arrangement.Dials, new[] { "#40DCC8", "#B57CFF", "#40DCC8", "#5AA9FF" }, "#05080B"),
        new(WidgetStyle.AioDeskShowroom, "Desktop Glass Showroom", Family.Desk, Arrangement.Dials, new[] { "#40DCC8", "#B57CFF", "#40DCC8", "#5AA9FF" }, "#05080B"),
        new(WidgetStyle.AioDeskFlow, "Desktop Flow Map", Family.Desk, Arrangement.Dials, new[] { "#40DCC8", "#B57CFF", "#40DCC8", "#5AA9FF" }, "#05080B"),
        new(WidgetStyle.AioDeskTelemetry, "Desktop Telemetry", Family.Desk, Arrangement.Dials, new[] { "#40DCC8", "#B57CFF", "#40DCC8", "#5AA9FF" }, "#05080B"),
        new(WidgetStyle.AioDeskTower, "Desktop Tower Cutaway", Family.Desk, Arrangement.Dials, new[] { "#40DCC8", "#B57CFF", "#40DCC8", "#5AA9FF" }, "#05080B"),
        new(WidgetStyle.AioDeskKeynote, "Desktop Keynote", Family.Desk, Arrangement.Dials, new[] { "#40DCC8", "#B57CFF", "#40DCC8", "#5AA9FF" }, "#05080B"),
    };
    /// <summary>All designs of this builder: the 22 own ones, the 100 gallery designs (5.3.0) and the ten studio designs (5.4.0).</summary>
    public static readonly Look[] All = Own.Concat(GalleryDesigns.All.Select(d => new Look(d.Style, d.Name, Family.Gallery,
        d.Arrangement switch
        {
            GalleryDesigns.Layout.Split => Arrangement.Split, GalleryDesigns.Layout.Hero => Arrangement.Hero, GalleryDesigns.Layout.Bars => Arrangement.Bars,
            GalleryDesigns.Layout.Table or GalleryDesigns.Layout.BigNumbers or GalleryDesigns.Layout.Segments => Arrangement.Stack, _ => Arrangement.Dials
        },
        d.World.Accents.Append(d.World.Accents[0]).ToArray(), d.World.Background, Gallery: d))).Concat(Studio).ToArray();
    private static readonly Dictionary<WidgetStyle, Look> ByStyle = All.ToDictionary(l => l.Style);
    public static bool Contains(WidgetStyle style) => ByStyle.ContainsKey(style);
    public static Look For(WidgetStyle style) => ByStyle[style];
}
