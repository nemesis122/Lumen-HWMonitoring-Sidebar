namespace Lumen.Core;

/// <summary>Appended persisted IDs. AIO is a single hardware card, not one duplicate per widget.</summary>
public static class AioDesigns
{
    public static readonly WidgetStyle[] Styles =
    {
        WidgetStyle.AioMainboard, WidgetStyle.AioMainboardMap,
        WidgetStyle.AioMainboardCores,
        WidgetStyle.AioMainboardDesktop,
        WidgetStyle.AioTypeBlock, WidgetStyle.AioOrbitNetwork, WidgetStyle.AioThermalGrid,
        WidgetStyle.AioSignalRack, WidgetStyle.AioIsometricCore, WidgetStyle.AioPrecision,
        WidgetStyle.AioStudioLight, WidgetStyle.AioInstrument,
        WidgetStyle.AioAuroraFusion, WidgetStyle.AioCarbonPulse,
        WidgetStyle.AioIsometricAtlas, WidgetStyle.AioIsometricTiles, WidgetStyle.AioIsometricRig, WidgetStyle.AioIsometricPedestals, WidgetStyle.AioIsometricCompact, WidgetStyle.AioIsometricRings,
        WidgetStyle.AioPanel, WidgetStyle.AioPanelRings, WidgetStyle.AioPanelBadges, WidgetStyle.AioPanelHeader, WidgetStyle.AioPanelPlain,
        WidgetStyle.AioBlackDashboard, WidgetStyle.AioBlackRings, WidgetStyle.AioBlackGraph, WidgetStyle.AioBlackClassic, WidgetStyle.AioBlackTiles, WidgetStyle.AioBlackXRay
    };
    /// <summary>The sensor-panel family: hardware pictures beside red-label values, colour and gauge style chosen in Customise.</summary>
    public static bool Panel(WidgetStyle style) => style is WidgetStyle.AioPanel or WidgetStyle.AioPanelRings or WidgetStyle.AioPanelBadges or WidgetStyle.AioPanelHeader or WidgetStyle.AioPanelPlain;
    public static readonly (string Key,string Name)[] PanelThemes={("red","Red"),("cyan","Cyber cyan"),("purple","Neon purple"),("gold","Gold"),("carbon","Carbon orange"),("glass","Glass red"),("lime","Toxic lime"),("silver","Ice silver")};
    public static readonly (string Key,string Name)[] PanelGauges={("dots","Dot rings"),("segments","Segment rings"),("twin","Double rings"),("leds","LED bars"),("hex","Hexagons"),("columns","Dot columns"),("grid","10 × 10 grid"),("tiles","Number tiles"),("rays","Ray rings"),("diamonds","Diamonds"),("tubes","Thermometers"),("strip","Compact strip")};
    /// <summary>The black family: one layout each; colour, cards, graphics and font are chosen per design in Customise.</summary>
    public static bool Black(WidgetStyle style) => style is WidgetStyle.AioBlackDashboard or WidgetStyle.AioBlackRings or WidgetStyle.AioBlackGraph
        or WidgetStyle.AioBlackClassic or WidgetStyle.AioBlackTiles or WidgetStyle.AioBlackXRay;
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
    /// <summary>The look a black design starts with (the user's favourite of each layout).</summary>
    public static BlackLook BlackDefault(WidgetStyle style)=>style switch
    {
        WidgetStyle.AioBlackRings=>new("gold","plain","real","inter"),
        WidgetStyle.AioBlackGraph=>new("silver","plain","real","inter"),
        WidgetStyle.AioBlackClassic=>new("yellow","carbon","real","orbitron"),
        WidgetStyle.AioBlackTiles=>new("silver","neon","real","inter"),
        WidgetStyle.AioBlackXRay=>new("amber","carbon","real","inter"),
        _=>new("red","plain","real","orbitron")
    };
    public static BlackLook BlackLookOf(Settings s,WidgetStyle style)
    {
        var d=BlackDefault(style);
        var look=s.BlackLooks.TryGetValue(style,out var text)?BlackLook.Parse(text)??d:d;
        static string Pick((string Key,string Name)[] options,string value,string fallback)=>options.Any(o=>o.Key==value)?value:fallback;
        return new BlackLook(BlackColours.Any(c=>c.Key==look.Colour)?look.Colour:d.Colour,Pick(BlackCards,look.Cards,d.Cards),Pick(BlackArts,look.Art,d.Art),Pick(BlackFonts,look.Font,d.Font));
    }
    public static bool Contains(WidgetStyle style) => Styles.Contains(style);
    public static bool Hardware(WidgetKind kind) => kind is WidgetKind.Cpu or WidgetKind.Gpu or WidgetKind.Cooling
        or WidgetKind.Power or WidgetKind.Memory or WidgetKind.Drives or WidgetKind.Network or WidgetKind.Overview;
    public static bool SensorDefault(WidgetStyle style,string key)
    {
        if(key.StartsWith("aio.gpu.")&&key.EndsWith(".extra")) return false; // a second graphics card is off by default
        if(key=="aio.graphs"&&Panel(style)) return false; // Panel designs show values only; history charts can be switched on
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
        if(key=="aio.system.ram")return WidgetKind.Memory;
        if(key.StartsWith("aio.system.disk.")||In("aio.drives"))return WidgetKind.Drives;
        if(In("aio.network"))return WidgetKind.Network;
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
        WidgetStyle.AioPanel => "Panel", WidgetStyle.AioPanelRings => "Panel Rings", WidgetStyle.AioPanelBadges => "Panel Badges", WidgetStyle.AioPanelHeader => "Panel Header", WidgetStyle.AioPanelPlain => "Panel Plain", WidgetStyle.AioBlackDashboard => "Black Dashboard", WidgetStyle.AioBlackRings => "Black Rings", WidgetStyle.AioBlackGraph => "Black Graph", WidgetStyle.AioBlackClassic => "Black Classic", WidgetStyle.AioBlackTiles => "Black Tiles", WidgetStyle.AioBlackXRay => "Black X-Ray", _ => style.ToString()
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
