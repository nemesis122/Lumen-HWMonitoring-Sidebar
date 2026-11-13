namespace Lumen.Core;

/// <summary>Appended persisted IDs. AIO is a single hardware card, not one duplicate per widget.</summary>
public static class AioDesigns
{
    public static readonly WidgetStyle[] Styles =
    {
        WidgetStyle.AioTypeBlock, WidgetStyle.AioOrbitNetwork, WidgetStyle.AioThermalGrid,
        WidgetStyle.AioSignalRack, WidgetStyle.AioIsometricCore, WidgetStyle.AioPrecision,
        WidgetStyle.AioChromatic, WidgetStyle.AioStudioLight, WidgetStyle.AioInstrument,
        WidgetStyle.AioAuroraFusion, WidgetStyle.AioPopConsole, WidgetStyle.AioCarbonPulse,
        WidgetStyle.AioIsometricAtlas, WidgetStyle.AioIsometricTiles, WidgetStyle.AioIsometricRig, WidgetStyle.AioIsometricPedestals, WidgetStyle.AioIsometricCompact, WidgetStyle.AioIsometricRings
    };
    public static bool Contains(WidgetStyle style) => Styles.Contains(style);
    public static bool Hardware(WidgetKind kind) => kind is WidgetKind.Cpu or WidgetKind.Gpu or WidgetKind.Cooling
        or WidgetKind.Power or WidgetKind.Memory or WidgetKind.Drives or WidgetKind.Network or WidgetKind.Overview;
    public static bool SensorDefault(WidgetStyle style,string key)
    {
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
        WidgetStyle.AioTypeBlock => "Type Block", WidgetStyle.AioOrbitNetwork => "Orbit Network",
        WidgetStyle.AioThermalGrid => "Thermal Grid", WidgetStyle.AioSignalRack => "Signal Rack",
        WidgetStyle.AioIsometricCore => "Isometric Core", WidgetStyle.AioPrecision => "Precision",
        WidgetStyle.AioChromatic => "Chromatic", WidgetStyle.AioStudioLight => "Studio Light",
        WidgetStyle.AioInstrument => "Instrument", WidgetStyle.AioAuroraFusion => "Aurora Fusion",
        WidgetStyle.AioPopConsole => "Pop Console", WidgetStyle.AioCarbonPulse => "Carbon Pulse",
        WidgetStyle.AioIsometricAtlas => "Isometric Atlas", WidgetStyle.AioIsometricTiles => "Isometric Tiles", WidgetStyle.AioIsometricRig => "Isometric Rig", WidgetStyle.AioIsometricPedestals => "Isometric Pedestals", WidgetStyle.AioIsometricCompact => "Isometric Compact", WidgetStyle.AioIsometricRings => "Isometric Rings", _ => style.ToString()
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
