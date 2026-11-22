namespace Lumen.Core;

/// <summary>Appended persisted IDs. AIO is a single hardware card, not one duplicate per widget.</summary>
public static class AioDesigns
{
    public static readonly WidgetStyle[] DashboardStyles =
    {
        WidgetStyle.AioBoardCard, WidgetStyle.AioDoubleTable,
        WidgetStyle.AioQuadrants, WidgetStyle.AioNetworkCenter
    };
    public static bool Instruments(WidgetStyle style) => style is WidgetStyle.AioTealInstruments or WidgetStyle.AioAmberInstruments;
    public static bool AtlasDriveLayout(WidgetStyle style) => Base(style) is
        WidgetStyle.AioTypeBlock or WidgetStyle.AioOrbitNetwork or WidgetStyle.AioThermalGrid or WidgetStyle.AioSignalRack
        or WidgetStyle.AioIsometricCore or WidgetStyle.AioPrecision or WidgetStyle.AioStudioLight or WidgetStyle.AioInstrument
        or WidgetStyle.AioAuroraFusion or WidgetStyle.AioCarbonPulse or WidgetStyle.AioIsometricRig
        or WidgetStyle.AioIsometricCompact or WidgetStyle.AioMainboardMap
        or WidgetStyle.AioMainboardDesktop or WidgetStyle.AioMainboardCores;
    public static bool NetworkFooterLayout(WidgetStyle style) => AtlasDriveLayout(style)
        || DashboardStyles.Contains(style) || style == WidgetStyle.AioIsometricRings;
    public static bool CompactNetworkLayout(WidgetStyle style) => Base(style) is
        WidgetStyle.AioTypeBlock or WidgetStyle.AioOrbitNetwork or WidgetStyle.AioThermalGrid
        or WidgetStyle.AioIsometricCore or WidgetStyle.AioPrecision or WidgetStyle.AioStudioLight
        or WidgetStyle.AioInstrument or WidgetStyle.AioAuroraFusion or WidgetStyle.AioCarbonPulse;
    public static bool CompactDriveLayout(WidgetStyle style) => Base(style) is
        WidgetStyle.AioTypeBlock or WidgetStyle.AioOrbitNetwork or WidgetStyle.AioThermalGrid or WidgetStyle.AioSignalRack
        or WidgetStyle.AioPrecision or WidgetStyle.AioStudioLight or WidgetStyle.AioInstrument or WidgetStyle.AioAuroraFusion
        or WidgetStyle.AioCarbonPulse or WidgetStyle.AioIsometricCompact;
    public static bool Dashboard(WidgetStyle style) => DashboardStyles.Contains(style) || CaseDesigns.Contains(style) || CaseEditionDesigns.Contains(style) || DarkAioDesigns.Contains(style) || Instruments(style);
    public static bool DashboardWide(WidgetStyle style) => style is WidgetStyle.AioBoardCard or WidgetStyle.AioDoubleTable or WidgetStyle.AioQuadrants;
    public static readonly WidgetStyle[] Styles = new[]
    {
        WidgetStyle.CaseAmdEdition,WidgetStyle.CaseNvidiaEdition,WidgetStyle.CaseIntelEdition,WidgetStyle.CaseStudio,WidgetStyle.CaseCommandCenter,
        WidgetStyle.CaseCyanGlass, WidgetStyle.AioBoardCard, WidgetStyle.AioDoubleTable,
        WidgetStyle.AioQuadrants, WidgetStyle.AioNetworkCenter,
        WidgetStyle.AioMainboardMap,
        WidgetStyle.AioMainboardCores,
        WidgetStyle.AioMainboardDesktop,
        WidgetStyle.AioTypeBlock, WidgetStyle.AioOrbitNetwork, WidgetStyle.AioThermalGrid,
        WidgetStyle.AioSignalRack, WidgetStyle.AioIsometricCore, WidgetStyle.AioPrecision,
        WidgetStyle.AioStudioLight, WidgetStyle.AioInstrument,
        WidgetStyle.AioAuroraFusion, WidgetStyle.AioCarbonPulse,
        WidgetStyle.AioIsometricAtlas, WidgetStyle.AioIsometricTiles, WidgetStyle.AioIsometricRig, WidgetStyle.AioIsometricPedestals, WidgetStyle.AioIsometricCompact, WidgetStyle.AioIsometricRings,
        WidgetStyle.AioPanel, WidgetStyle.AioPanelRings, WidgetStyle.AioPanelHeader, WidgetStyle.AioPanelPlain
        , WidgetStyle.AioMainboardMapRed, WidgetStyle.AioMainboardCoresRed, WidgetStyle.AioTypeBlockRed, WidgetStyle.AioOrbitNetworkRed, WidgetStyle.AioThermalGridRed, WidgetStyle.AioSignalRackRed, WidgetStyle.AioIsometricCoreRed, WidgetStyle.AioPrecisionRed, WidgetStyle.AioStudioLightRed, WidgetStyle.AioInstrumentRed, WidgetStyle.AioAuroraFusionRed, WidgetStyle.AioCarbonPulseRed, WidgetStyle.AioIsometricAtlasRed, WidgetStyle.AioIsometricTilesRed, WidgetStyle.AioIsometricRigRed, WidgetStyle.AioIsometricPedestalsRed, WidgetStyle.AioIsometricCompactRed, WidgetStyle.AioIsometricRingsRed
    }.Concat(DarkAioDesigns.All.Select(item => item.Style)).Concat(new[] { WidgetStyle.AioTealInstruments, WidgetStyle.AioAmberInstruments })
        .Concat(NeoDesigns.All.Select(item => item.Style)).ToArray();
    /// <summary>The sensor-panel family: hardware pictures beside red-label values, colour and gauge style chosen in Customise.</summary>
    public static bool Panel(WidgetStyle style) => Base(style) is WidgetStyle.AioPanel or WidgetStyle.AioPanelRings or WidgetStyle.AioPanelBadges or WidgetStyle.AioPanelHeader or WidgetStyle.AioPanelPlain;
    public static readonly (string Key,string Name)[] PanelThemes={("red","Red"),("cyan","Cyber cyan"),("purple","Neon purple"),("gold","Gold"),("carbon","Carbon orange"),("glass","Glass red"),("lime","Toxic lime"),("silver","Ice silver")};
    public static readonly (string Key,string Name)[] PanelGauges={("dots","Dot rings"),("segments","Segment rings"),("twin","Double rings"),("leds","LED bars"),("hex","Hexagons"),("columns","Dot columns"),("grid","10 × 10 grid"),("tiles","Number tiles"),("rays","Ray rings"),("diamonds","Diamonds"),("tubes","Thermometers"),("strip","Compact strip")};
    /// <summary>The black family: one layout each; colour, cards, graphics and font are chosen per design in Customise.</summary>
    public static bool Black(WidgetStyle style) => BlackDesigns.Any(d => d.Style == Base(style));
    /// <summary>Every black design: its layout and its fixed look (colour, cards, graphics, font). User request: one design per look.</summary>
    public static readonly (WidgetStyle Style, string Name, WidgetStyle Layout, BlackLook Look)[] BlackDesigns =
    {
        (WidgetStyle.AioBlackDashboard, "Red Dashboard", WidgetStyle.AioBlackDashboard, new("red","plain","real","orbitron")),
        (WidgetStyle.AioBlackRings, "Gold Rings", WidgetStyle.AioBlackRings, new("gold","plain","real","inter")),
        (WidgetStyle.AioBlackGraph, "Silver Graph", WidgetStyle.AioBlackGraph, new("silver","plain","real","inter")),
        (WidgetStyle.AioBlackClassic, "Carbon Yellow Classic", WidgetStyle.AioBlackClassic, new("yellow","carbon","real","orbitron")),
        (WidgetStyle.AioBlackTiles, "Neon Silver Tiles", WidgetStyle.AioBlackTiles, new("silver","neon","real","inter")),
        (WidgetStyle.AioBlackXRay, "Carbon Amber X-Ray", WidgetStyle.AioBlackXRay, new("amber","carbon","real","inter")),
        (WidgetStyle.AioBlackWhiteDashboard, "Carbon White Dashboard", WidgetStyle.AioBlackDashboard, new("white","carbon","real","orbitron")),
        (WidgetStyle.AioBlackGoldClassic, "Neon Gold Classic", WidgetStyle.AioBlackClassic, new("gold","neon","real","inter")),
        (WidgetStyle.AioBlackWhiteGraph, "Neon White Graph", WidgetStyle.AioBlackGraph, new("white","neon","real","inter")),
        (WidgetStyle.AioBlackRedTiles, "Line Red Tiles", WidgetStyle.AioBlackTiles, new("red","line","line","inter")),
        (WidgetStyle.AioBlackWhiteTiles, "Edge White Tiles", WidgetStyle.AioBlackTiles, new("white","edge","real","mono")),
        (WidgetStyle.AioBlackBlueDashboard, "Line Blue Dashboard", WidgetStyle.AioBlackDashboard, new("blue","line","line","inter")),
        (WidgetStyle.AioBlackAmberClassic, "Amber Classic", WidgetStyle.AioBlackClassic, new("amber","plain","real","inter")),
        (WidgetStyle.AioBlackLimeDashboard, "Edge Lime Dashboard", WidgetStyle.AioBlackDashboard, new("lime","edge","iso","inter")),
        (WidgetStyle.AioBlackCrimsonDashboard, "Crimson Dashboard", WidgetStyle.AioBlackDashboard, new("crimson","plain","real","inter")),
        (WidgetStyle.AioBlackCopperRings, "Copper Rings", WidgetStyle.AioBlackRings, new("copper","plain","real","inter"))
    };
    public static WidgetStyle BlackLayout(WidgetStyle style) => BlackDesigns.FirstOrDefault(d => d.Style == Base(style)).Layout;
    public static readonly (string Key,string Name,string A,string B)[] BlackColours =
    {
        ("red","Red","#EF4444","#F97316"),("gold","Gold","#E5B84B","#F5DEB3"),("silver","Silver","#E5E7EB","#9CA3AF"),("yellow","Yellow","#FACC15","#F97316"),
        ("ice","Ice","#38BDF8","#A5F3FC"),("green","Green","#22C55E","#A3E635"),("magenta","Magenta","#E879F9","#22D3EE"),("orange","Orange","#FB923C","#F43F5E"),
        ("purple","Purple","#A78BFA","#E879F9"),("cyan","Cyan","#22D3EE","#67E8F9"),("crimson","Crimson","#DC2626","#FCA5A5"),("copper","Copper","#D97745","#F0B27A"),
        ("lime","Lime","#A3E635","#4ADE80"),("rose","Rose","#F43F5E","#FB7185"),("blue","Blue","#3B82F6","#60A5FA"),("amber","Amber","#F59E0B","#FDE68A"),
        ("teal","Teal","#14B8A6","#5EEAD4"),("pink","Pink","#EC4899","#F9A8D4"),("white","White","#FFFFFF","#A1A1AA"),("violet","Violet","#8B5CF6","#C4B5FD")
    };
    public static readonly (string Key,string Name)[] BlackCards={("plain","Black"),("carbon","Carbon"),("neon","Neon outline"),("line","Top line"),("edge","Gradient edge"),("glass","Glass")};
    public static readonly (string Key,string Name)[] BlackArts={("real","Realistic"),("line","Line art"),("iso","3D")};
    public static readonly (string Key,string Name)[] BlackFonts={("inter","Inter"),("orbitron","Orbitron"),("mono","Mono")};
    public sealed record BlackLook(string Colour,string Cards,string Art,string Font)
    {
        public override string ToString()=>Colour+"|"+Cards+"|"+Art+"|"+Font;
        public static BlackLook? Parse(string? text)
        {
            var p=(text??"").Split('|');
            return p.Length==4?new BlackLook(p[0],p[1],p[2],p[3]):null;
        }
    }
    public static BlackLook BlackDefault(WidgetStyle style) => Red(style) ? (BlackDefault(Base(style)) with { Colour = "red", Font = "orbitron" })
        : BlackDesigns.FirstOrDefault(d => d.Style == style).Look ?? BlackDesigns[0].Look;
    /// <summary>Each black design has its own fixed look.</summary>
    public static BlackLook BlackLookOf(Settings s, WidgetStyle style) => BlackDefault(style);
    /// <summary>The "Red" designs: the same layout in the red Panel Header look (red, dark grey, Orbitron). User request.</summary>
    public static readonly (WidgetStyle Style, WidgetStyle Base)[] RedDesigns =
    {
        (WidgetStyle.AioMainboardRed, WidgetStyle.AioMainboard),
        (WidgetStyle.AioMainboardMapRed, WidgetStyle.AioMainboardMap),
        (WidgetStyle.AioMainboardDesktopRed, WidgetStyle.AioMainboardDesktop),
        (WidgetStyle.AioMainboardCoresRed, WidgetStyle.AioMainboardCores),
        (WidgetStyle.AioTypeBlockRed, WidgetStyle.AioTypeBlock),
        (WidgetStyle.AioOrbitNetworkRed, WidgetStyle.AioOrbitNetwork),
        (WidgetStyle.AioThermalGridRed, WidgetStyle.AioThermalGrid),
        (WidgetStyle.AioSignalRackRed, WidgetStyle.AioSignalRack),
        (WidgetStyle.AioIsometricCoreRed, WidgetStyle.AioIsometricCore),
        (WidgetStyle.AioPrecisionRed, WidgetStyle.AioPrecision),
        (WidgetStyle.AioStudioLightRed, WidgetStyle.AioStudioLight),
        (WidgetStyle.AioInstrumentRed, WidgetStyle.AioInstrument),
        (WidgetStyle.AioAuroraFusionRed, WidgetStyle.AioAuroraFusion),
        (WidgetStyle.AioCarbonPulseRed, WidgetStyle.AioCarbonPulse),
        (WidgetStyle.AioIsometricAtlasRed, WidgetStyle.AioIsometricAtlas),
        (WidgetStyle.AioIsometricTilesRed, WidgetStyle.AioIsometricTiles),
        (WidgetStyle.AioIsometricRigRed, WidgetStyle.AioIsometricRig),
        (WidgetStyle.AioIsometricPedestalsRed, WidgetStyle.AioIsometricPedestals),
        (WidgetStyle.AioIsometricCompactRed, WidgetStyle.AioIsometricCompact),
        (WidgetStyle.AioIsometricRingsRed, WidgetStyle.AioIsometricRings),
        (WidgetStyle.AioBlackRingsRed, WidgetStyle.AioBlackRings),
        (WidgetStyle.AioBlackGraphRed, WidgetStyle.AioBlackGraph),
        (WidgetStyle.AioBlackClassicRed, WidgetStyle.AioBlackClassic),
        (WidgetStyle.AioBlackTilesRed, WidgetStyle.AioBlackTiles),
        (WidgetStyle.AioBlackXRayRed, WidgetStyle.AioBlackXRay),
        (WidgetStyle.AioBlackWhiteDashboardRed, WidgetStyle.AioBlackWhiteDashboard),
        (WidgetStyle.AioBlackGoldClassicRed, WidgetStyle.AioBlackGoldClassic),
        (WidgetStyle.AioBlackWhiteGraphRed, WidgetStyle.AioBlackWhiteGraph),
        (WidgetStyle.AioBlackRedTilesRed, WidgetStyle.AioBlackRedTiles),
        (WidgetStyle.AioBlackWhiteTilesRed, WidgetStyle.AioBlackWhiteTiles),
        (WidgetStyle.AioBlackBlueDashboardRed, WidgetStyle.AioBlackBlueDashboard),
        (WidgetStyle.AioBlackAmberClassicRed, WidgetStyle.AioBlackAmberClassic),
        (WidgetStyle.AioBlackLimeDashboardRed, WidgetStyle.AioBlackLimeDashboard),
        (WidgetStyle.AioBlackCrimsonDashboardRed, WidgetStyle.AioBlackCrimsonDashboard),
        (WidgetStyle.AioBlackCopperRingsRed, WidgetStyle.AioBlackCopperRings)
    };
    public static bool Red(WidgetStyle style) => RedDesigns.Any(d => d.Style == style);
    /// <summary>The layout a design is built from: a Red design is its base design in another look.</summary>
    public static WidgetStyle Base(WidgetStyle style) => RedDesigns.FirstOrDefault(d => d.Style == style) is { Style: not WidgetStyle.Standard } red ? red.Base : style;
    public static bool Contains(WidgetStyle style) => Styles.Contains(style);
    public static bool Hardware(WidgetKind kind) => kind is WidgetKind.Cpu or WidgetKind.Gpu or WidgetKind.Cooling
        or WidgetKind.Power or WidgetKind.Memory or WidgetKind.Drives or WidgetKind.Network or WidgetKind.Overview;
    public static bool IsGraphSwitch(string key) => key is "aio.cpu.history" or "aio.gpu.history" or "aio.cooling.history" or "aio.system.ram.history" or "aio.drives.history" or "aio.network.history";
    public static bool SensorDefault(WidgetStyle style,string key)
    {
        if (key is "aio.performance.fps" or "aio.performance.refresh") return false;
        style=Base(style); // a Red design starts with the sensors of its base design
        // Per-part history charts (aio.cpu.history, aio.network.history …): on, except in the Panel designs, which show values only.
        if(IsGraphSwitch(key)) return !Panel(style);
        if (Instruments(style) && (key is "aio.cpu.power" or "aio.network.link" || key.StartsWith("aio.system.ram.") ||
            key.StartsWith("aio.system.disk.") || key.StartsWith("aio.drives.") && !key.EndsWith(".used"))) return false;
        if((CaseEditionDesigns.Contains(style)||DarkAioDesigns.Contains(style))&&key.StartsWith("aio.drives.")&&
            (key.EndsWith(".life")||key.EndsWith(".hours")||key.EndsWith(".activity")))return false;
        if(key.StartsWith("aio.gpu.")&&key.EndsWith(".extra")) return false; // a second graphics card is off by default
        // (history switches are decided at the top)
        if(key=="aio.cpu.cores"||key.StartsWith("aio.cpu.core.")) return style==WidgetStyle.AioThermalGrid;
        if(style==WidgetStyle.AioSignalRack)
            return !key.EndsWith("clock") && !key.EndsWith("hotspot") && !key.EndsWith("junction")
                && !key.EndsWith("vram") && !key.StartsWith("aio.system.disk.");
        if(style is WidgetStyle.AioTypeBlock or WidgetStyle.AioOrbitNetwork or WidgetStyle.AioThermalGrid or WidgetStyle.AioIsometricCore)
            return !key.EndsWith("clock")&&!key.EndsWith("vram");
        return true;
    }
    /// <summary>The ordinary widget whose on/off switch shows or hides this part of an AIO card.</summary>
    public static WidgetKind? SwitchFor(string key)
    {
        bool In(string group)=>key==group||key.StartsWith(group+".");
        if(In("aio.cpu"))return WidgetKind.Cpu;
        if(In("aio.gpu"))return WidgetKind.Gpu;
        if(In("aio.cooling"))return WidgetKind.Cooling;
        if(In("aio.rails"))return WidgetKind.Power;
        if(key=="aio.system.ram" || key.StartsWith("aio.system.ram."))return WidgetKind.Memory;
        if(In("aio.board"))return WidgetKind.Cooling;
        if(key.StartsWith("aio.system.disk.")||In("aio.drives"))return WidgetKind.Drives;
        if(In("aio.network"))return WidgetKind.Network;
        if(In("aio.performance"))return WidgetKind.Gpu; // FPS and refresh rate of the Instruments designs
        if(key=="aio.system")return WidgetKind.Memory;  // the old part switch (RAM and drive temperatures), no longer listed
        return null;
    }
    public static readonly WidgetKind[] Switches={WidgetKind.Cpu,WidgetKind.Gpu,WidgetKind.Cooling,WidgetKind.Power,WidgetKind.Memory,WidgetKind.Drives,WidgetKind.Network};
    /// <summary>An AIO card shows a part only while its widget is switched on.</summary>
    public static bool SwitchedOn(Settings settings,string key)
    {
        if(!settings.AioWidgetSwitches)return true;
        var kind=SwitchFor(key);if(kind==null)return true;
        var widget=settings.Widgets.FirstOrDefault(w=>w.Kind==kind);return widget==null||widget.Visible;
    }
    /// <summary>Choosing an AIO design shows every hardware part; the user then switches parts off.</summary>
    public static void ShowAllParts(Settings settings)
    {
        foreach(var w in settings.Widgets.Where(w=>Switches.Contains(w.Kind)))w.Visible=true;
        settings.AioWidgetSwitches=true;
    }
    public static string Name(WidgetStyle style) => style switch
    {
        _ when DarkAioDesigns.Contains(style) => DarkAioDesigns.For(style).Name,
        _ when NeoDesigns.Contains(style) => NeoDesigns.For(style).Name,
        WidgetStyle.AioTealInstruments => "Teal Instruments",
        WidgetStyle.AioAmberInstruments => "Amber Instruments",
        _ when CaseEditionDesigns.Contains(style) => CaseEditionDesigns.For(style).Name,
        _ when CaseDesigns.Contains(style) => CaseDesigns.For(style).Name,
        WidgetStyle.AioSensorTable => "Sensor Table",
        WidgetStyle.AioTwoHalves => "Two Halves",
        WidgetStyle.AioSummaryDetails => "Summary + Details",
        WidgetStyle.AioCasePlan => "Case Plan",
        WidgetStyle.AioBoardCard => "Mainboard Card",
        WidgetStyle.AioDoubleTable => "Double Table",
        WidgetStyle.AioQuadrants => "Quadrants",
        WidgetStyle.AioMinMax => "Min–Max",
        WidgetStyle.AioNetworkCenter => "Network Center",
        WidgetStyle.AioMainboard => "Mainboard",
        WidgetStyle.AioMainboardCores => "Mainboard Cores",
        WidgetStyle.AioMainboardMap => "Mainboard Map",
        WidgetStyle.AioMainboardStrips => "Mainboard · Strips",
        WidgetStyle.AioMainboardGauges => "Mainboard · Instruments",
        WidgetStyle.AioMainboardMatrix => "Mainboard · Data Matrix",
        WidgetStyle.AioMainboardType => "Mainboard · Typography",
        WidgetStyle.AioMainboardDesktop => "Mainboard Map Desktop",
        WidgetStyle.AioTypeBlock => "Type Block", WidgetStyle.AioOrbitNetwork => "Orbit Network",
        WidgetStyle.AioThermalGrid => "Thermal Grid", WidgetStyle.AioSignalRack => "Signal Rack",
        WidgetStyle.AioIsometricCore => "Isometric Core", WidgetStyle.AioPrecision => "Precision",
        WidgetStyle.AioChromatic => "Chromatic", WidgetStyle.AioStudioLight => "Studio Light",
        WidgetStyle.AioInstrument => "Instrument", WidgetStyle.AioAuroraFusion => "Aurora Fusion",
        WidgetStyle.AioPopConsole => "Pop Console", WidgetStyle.AioCarbonPulse => "Carbon Pulse",
        WidgetStyle.AioIsometricAtlas => "Isometric Atlas", WidgetStyle.AioIsometricTiles => "Isometric Tiles", WidgetStyle.AioIsometricRig => "Isometric Rig", WidgetStyle.AioIsometricPedestals => "Isometric Pedestals", WidgetStyle.AioIsometricCompact => "Isometric Compact", WidgetStyle.AioIsometricRings => "Isometric Rings",
        WidgetStyle.AioPanel => "Panel", WidgetStyle.AioPanelRings => "Panel Rings", WidgetStyle.AioPanelBadges => "Panel Badges", WidgetStyle.AioPanelHeader => "Panel Header", WidgetStyle.AioPanelPlain => "Panel Plain",
        _ when Red(style) => Name(Base(style)) + " Red",
        _ when Black(style) => BlackDesigns.First(d => d.Style == style).Name,  _ => style.ToString()
    };

    // Keep saved visibility/order/individual styles intact. Switching back restores separate cards.
    public static List<WidgetSlot> Group(IEnumerable<WidgetSlot> source, WidgetSlot? overview = null)
    {
        var list = source.ToList();
        var explicitAio = list.FirstOrDefault(w=>w.Kind==WidgetKind.Overview&&Contains(w.Style));
        if(explicitAio!=null)
        {
            var single=new List<WidgetSlot>();bool added=false;
            foreach(var w in list)
                if(Hardware(w.Kind)){if(!added){single.Add(explicitAio);added=true;}}else single.Add(w);
            return single;
        }
        var result = new List<WidgetSlot>(); var seen = new HashSet<WidgetStyle>();
        foreach (var slot in list)
        {
            if (!Contains(slot.Style) || !Hardware(slot.Kind)) { result.Add(slot); continue; }
            if (!seen.Add(slot.Style)) continue;
            var presentation = overview ?? slot;
            result.Add(new WidgetSlot { Kind = WidgetKind.Overview, Style = slot.Style, Page = slot.Page,
                Column = slot.Column, Visible = true, WidgetScale = presentation.WidgetScale, GraphicScale = presentation.GraphicScale,
                HeroScale = presentation.HeroScale, ValueScale = presentation.ValueScale, LabelScale = presentation.LabelScale,
                ShowTitle = presentation.ShowTitle, ShowHardware = presentation.ShowHardware,
                Name = presentation.Name, HardwareName = presentation.HardwareName, Accent = presentation.Accent,
                HiddenSensors = (overview ?? slot).HiddenSensors.Where(k => k.StartsWith("aio.")).ToList(),
                AioEnabledSensors = (overview ?? slot).AioEnabledSensors?.ToList() ?? new() });
        }
        return result;
    }
}
