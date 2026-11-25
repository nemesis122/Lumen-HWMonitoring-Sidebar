using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.LogicalTree;
using Lumen.Core;

namespace Lumen.App;

/// <summary>One live hardware card. Native text, graphs and vector drawings; no screenshot background.</summary>
public sealed partial class AioTile : Border, IWidget, INamed
{
    private readonly WidgetStyle style;
    private readonly Settings source;
    private readonly Palette palette;
    private Settings settings;
    private WidgetSlot? slot;
    private readonly StackPanel content = new() { Spacing = 9 };
    private readonly Dictionary<string,List<TextBlock>> fields = new();
    /// <summary>The texts that show a reading (for the value colour in Appearance).</summary>
    public HashSet<TextBlock> ValueTexts => fields.Values.SelectMany(x=>x).ToHashSet();
    private readonly Dictionary<TextBlock,Func<AioReading,string>> formats = new();
    private readonly List<(string Key,AioMeter Meter)> meters = new();
    /// <summary>Parts that change with a live reading beyond its text (idle fans, warning outlines).</summary>
    private readonly List<(string Key,Action<AioReading?> Apply)> watchers = new();
    private readonly List<(string Key,AreaChart Chart)> charts = new();
    private readonly List<(string Key,AioRing Ring)> rings = new();
    private readonly List<(string Key,Border Cell)> heat = new();
    private readonly History samples = new();
    private readonly Dictionary<string,AioReading> readings = new();
    private readonly Dictionary<string,AioReading> known = new();
    private readonly Dictionary<string,int> misses = new();
    private readonly Color ink, muted, track, back;
    private readonly bool lightSurface;
    private string shape="";
    private Snapshot snapshot = null!;
    private History history = null!;
    /// <summary>The design as chosen (a Red design); <see cref="style"/> is the layout it is built from.</summary>
    private readonly WidgetStyle designStyle;
    private readonly bool red;
    /// <summary>The hue the card takes: the colour chosen in Appearance (monochrome: grey), else red for a Red design.</summary>
    private readonly Color? skin;
    public AioTile(WidgetStyle style,Palette p,Metrics m,Settings s)
    {
        designStyle=style; red=AioDesigns.Red(style); style=AioDesigns.Base(style);
        skin=p.Chosen??(s.Colors=="mono"?Color.Parse("#9AA0A8"):red?RedSkin.Red:null);
        this.style=style; source=s; palette=p;
        slot=s.ActiveWidget;
        if(slot==null||slot.Style!=designStyle)
        {
            var original=slot??new WidgetSlot {Kind=WidgetKind.Overview};
            slot=new WidgetSlot {Kind=WidgetKind.Overview,Style=designStyle,HiddenSensors=original.HiddenSensors,AioEnabledSensors=original.AioEnabledSensors,
                WidgetScale=original.WidgetScale,HeroScale=original.HeroScale,GraphicScale=original.GraphicScale,ValueScale=original.ValueScale,
                LabelScale=original.LabelScale,ShowHardware=original.ShowHardware,ShowTitle=original.ShowTitle,Name=original.Name,
                HardwareName=original.HardwareName,Accent=original.Accent,DriveOrder=original.DriveOrder};
        }
        settings=s.ForSlot(slot);
        // Pop Console follows the theme: on a dark sidebar its pop shapes become dark panels with coloured outlines.
        // With the sidebar's own background the text always follows it (dark background → light text),
        // so Studio Light and Pop Console stay readable on a dark sidebar.
        // Every AIO design is dark on a dark sidebar; only a light theme makes Studio Light / Pop Console (or an own background) light.
        bool light=!p.Dark&&(p.OwnBackground||AioDesigns.Instruments(style)||style is WidgetStyle.AioStudioLight or WidgetStyle.AioPopConsole);
        lightSurface=light;
        ink=Color.Parse(light?"#151B2A":"#EEF4FF"); muted=Color.Parse(light?"#46516B":"#B2BFD1");
        back=p.OwnBackground?p.Card:light?C("#F9FAFE"):Color.Parse(style switch { WidgetStyle.AioPrecision=>"#0D172B",WidgetStyle.AioChromatic=>"#170D2D",_=>"#0C1017" });
        track=Color.Parse(light?"#DBDFE9":"#29313E");
        // Red designs: the dark sections and grey lines of the Panel designs.
        if(red&&!light){muted=C("#A9A9AE");track=C("#3A3A3D");if(!p.OwnBackground)back=C("#0E0E0F");}
        // Opacity setting: the card is as transparent as the standard designs' cards (same alpha as Palette.Card).
        cardAlpha=p.Card.A;
        Background=Palette.Brush(Color.FromArgb(cardAlpha,back.R,back.G,back.B)); BorderBrush=Palette.Brush(track); BorderThickness=new Thickness(1);
        if(style==WidgetStyle.AioAuroraFusion&&!p.OwnBackground&&!red)
            BorderBrush=new LinearGradientBrush {GradientStops={new GradientStop(C("#2EE3DA"),0),new GradientStop(C("#BC4FF1"),.45),new GradientStop(C("#FA905F"),.75),new GradientStop(C("#26D9E0"),1)}};
        CornerRadius=new CornerRadius(style==WidgetStyle.AioTypeBlock?6:18); Padding=new Thickness(12);
        Child=content;
    }
    private byte cardAlpha=255;
    /// <summary>A design colour with the card's opacity, for panels that should be see-through like the card.</summary>
    internal Color SeeThrough(Color c)=>Color.FromArgb((byte)Math.Round(c.A*cardAlpha/255d),c.R,c.G,c.B);
    public void Own(WidgetSlot owned,int index) { slot=owned; settings=source.ForSlot(owned); shape=""; TextColours.Own(this,owned,palette.Dark); }
    internal double? ColourTemperature(string key) => readings.TryGetValue(key,out var reading) ? reading.Number : null;
    public void Update(Snapshot snap,History h)
    {
        TextColours.Snapshot(this,snap);
        settings=source.ForSlot(slot);
        snapshot=snap; history=h;
        var list=AioReadings.For(snap,settings);
        if(!AioReadings.Selected(settings,"aio.cpu.cores")) list.RemoveAll(r=>r.Group=="Cores");
        var fresh=list.ToDictionary(r=>r.Key);
        foreach(var key in known.Keys.ToArray())
        {
            if(fresh.ContainsKey(key)){misses.Remove(key);continue;}
            if(!AioReadings.Selected(settings,key)||known[key].Group=="Cores"&&!AioReadings.Selected(settings,"aio.cpu.cores")||AioReadings.CardSwitchedOff(snap,settings,key)||(misses[key]=misses.GetValueOrDefault(key)+1)>SensorHold.Patience)
            {known.Remove(key);misses.Remove(key);continue;}
            fresh[key]=known[key] with {Value="—",Number=null};
        }
        // A stable order: the readings' own order, with a held value ("—") kept where it was. Before 5.0.2 the order came
        // from a dictionary that reshuffled after a sensor was switched off and on, so a design that charts "the first
        // value" of a part (Case Studio, Isometric Rings) could switch to another sensor (found by the sensor selection test).
        var canon=list.Select(r=>r.Key).ToList();var oldOrder=known.Keys.ToList();var order=new List<string>(canon);
        foreach(var held in oldOrder.Where(k=>fresh.ContainsKey(k)&&!canon.Contains(k)))
        {
            int at=oldOrder.IndexOf(held);string? before=null;
            for(int j=at-1;j>=0&&before==null;j--)if(order.Contains(oldOrder[j]))before=oldOrder[j];
            order.Insert(before==null?0:order.IndexOf(before)+1,held);
        }
        known.Clear();foreach(var k in order)known[k]=fresh[k];
        list=order.Select(k=>fresh[k]).ToList();
        readings.Clear(); foreach(var r in list) readings[r.Key]=r;
        foreach(var r in list) samples.Add(r.Key,r.Number);
        if (AioDesigns.Dashboard(style)) SampleDashboard(list, h);
        string next=string.Join('|',list.Select(r=>r.Key+":"+r.Label))+"|"+snap.Cpu?.Name+"|"+string.Join('|',snap.Gpus.Select(g=>g.Name))
            +"|"+(slot?.ShowTitle??true)+(slot?.ShowHardware??true)+(slot?.Name??"")+"|"+L.Language+"|"+settings.Fahrenheit+"|"+(AioDesigns.Panel(style)?settings.PanelTheme+settings.PanelGauges+slot?.Accent:"")+(AioDesigns.Black(style)?AioDesigns.BlackLookOf(settings,style)+slot?.Accent:"");
        if((snap.Cpu?.CoreLoads.Count??0)>=2)lastCoreLoads=snap.Cpu!.CoreLoads;
        next+="|"+lastCoreLoads.Count+AioReadings.Selected(settings,"aio.cpu.coreloads");
        next+="|"+settings.ShowNames+AioReadings.GraphState(settings)+slot?.HardwareName+"|"+string.Join('|',source.Widgets.Select(w=>w.Name+w.HardwareName+w.ShowHardware+w.ShowTitle+w.Accent+(AioDesigns.Switches.Contains(w.Kind)?w.Visible:true)));
        if (IsDashboard) next += "|columns:" + source.Columns;
        if (AioDesigns.Instruments(style)) next += "|rails:" + list.Any(r => r.Group == "Rails" && r.Number.HasValue)
            + "|" + DriveOwner?.DriveOrder + "|" + string.Join('|', snap.Disks.Select(d => d.Name + ":" + d.Letters));
        if (DarkAioDesigns.Contains(style) || AioDesigns.AtlasDriveLayout(designStyle))
            next += "|" + DriveOwner?.ShowTitle + "|" + DriveOwner?.ShowHardware
                + "|" + DriveOwner?.Name + "|" + DriveOwner?.HardwareName + "|" + DriveOwner?.DriveOrder
                + "|" + string.Join('|', snap.Disks.Select(d => d.Id + ":" + d.Name + ":" + d.Letters));
        if(next!=shape) { shape=next; Build(list); if(settings.Horizontal)Horizontalize(); }
        UpdateCoreBars();
        foreach(var (key,meter) in meters) TextColours.Bind(meter,key);
        foreach(var (key,ring) in rings) TextColours.Bind(ring,key);
        foreach(var (key,chart) in charts) TextColours.Bind(chart,key);
        foreach(var (key,cell) in heat) TextColours.BindHeat(cell,key);
        foreach(var (key,second,plot) in referencePlots) TextColours.Bind(plot,key,second);
        if(dashboardTraffic!=null)TextColours.Bind(dashboardTraffic,"aio.network.down","aio.network.up");
        foreach(var (key,plot) in casePlots) TextColours.Bind(plot,key);
        if(coreBars!=null)TextColours.Bind(coreBars,"aio.cpu.coreloads");
        if(mainboardCores!=null)TextColours.Bind(mainboardCores,"aio.cpu.coreloads");
        foreach(var r in list)
            if(fields.TryGetValue(r.Key,out var texts)) foreach(var t in texts) { TextColours.Bind(t,r.Key);t.Text=formats.TryGetValue(t,out var format)?format(r):Display(r); ToolTip.SetTip(t,L.T(r.Label)+": "+r.Value); }
        foreach(var (key,meter) in meters)
        {
            var row=readings.GetValueOrDefault(key);
            meter.Set(row?.Fraction,samples.Read(key).Select(v=>v/(row?.Maximum??100)).ToArray());
        }
        foreach(var (key,cell) in heat)
        {
            var row=readings.GetValueOrDefault(key);
            cell.Background=Palette.Brush(row?.Number is double n ? Palette.Mix(C("#77558D"),C("#E18A38"),Math.Clamp((n-45)/30,0,1)) : track);
        }
        foreach(var (key,ring) in rings) ring.Set(readings.GetValueOrDefault(key)?.Fraction);
        foreach(var (key,chart) in charts)
        {
            var r=readings.GetValueOrDefault(key); var series=r==null?Array.Empty<double>():r.History.Length>0?h.Read(r.History):samples.Read(key);
            if(series.Length==0)series=samples.Read(key);
            chart.Set(series,Accent(r));
            ToolTip.SetTip(chart,series.Any(double.IsFinite)?L.T("History chart"):L.T("No data"));
        }
        UpdateReferencePlots(h);
        foreach(var (key,apply) in watchers) apply(readings.GetValueOrDefault(key));
        UpdateCasePlots();

        if (AioDesigns.Dashboard(style)) UpdateDashboard();
        if (AioDesigns.Instruments(style)) UpdateInstrumentDashboard();
        if(AioDesigns.Panel(style))UpdatePanel(h);
        if(AioDesigns.Black(style))UpdateBlack(h);
        WidgetPresentation.Apply(this,settings);
        // Compact AIO columns can need reflow even at 100%, especially the Pop Console CPU panel.
        foreach(var grid in this.GetLogicalDescendants().OfType<ResponsiveGrid>())
            grid.Adapt=Math.Max(Math.Max(settings.ActiveWidget?.Values??1,settings.ActiveWidget?.Labels??1),Math.Max(settings.ActiveWidget?.Hero??1,settings.ActiveWidget?.Graphics??1))>1.15;
    }

    private Color Accent(AioReading? r)
    {
        var color=Color.TryParse(slot?.Accent,out var picked)?picked:palette.Chosen??(red?RedSkin.Red:BaseAccent(r));
        return lightSurface?Palette.Mix(color,Colors.Black,.25):color;
    }
    private Color BaseAccent(AioReading? r) => r?.Group switch
    {
        "CPU" when style==WidgetStyle.AioStudioLight=>r.Key.EndsWith("voltage")?C("#8C36E9"):C("#0868F8"),
        "GPU" when style==WidgetStyle.AioStudioLight=>r.Key.EndsWith("hotspot")||r.Key.EndsWith("power")?C("#FF6F61"):r.Key.EndsWith("junction")?C("#F5B637"):r.Key.EndsWith("clock")?C("#127CF8"):r.Key.EndsWith("voltage")||r.Key.EndsWith("load")?C("#9945DB"):C("#08BFA7"),
        "CPU" or "GPU" when (style is WidgetStyle.AioPrecision or WidgetStyle.AioStudioLight or WidgetStyle.AioInstrument or WidgetStyle.AioChromatic)
            && (r.Key.EndsWith("voltage")||r.Key.EndsWith("load"))=>C("#A34DE2"),
        "GPU" when (style is WidgetStyle.AioPrecision or WidgetStyle.AioStudioLight or WidgetStyle.AioInstrument)
            && r.Key.EndsWith("power")=>C("#E04BCB"),
        "GPU" when style==WidgetStyle.AioTypeBlock=>C("#2AD5D9"),
        "GPU" when style is WidgetStyle.AioOrbitNetwork or WidgetStyle.AioThermalGrid or WidgetStyle.AioIsometricCore=>C("#29D3DD"),
        "GPU" when r.Key.EndsWith("hotspot")=>C("#FFB542"), "GPU" when r.Key.EndsWith("junction")=>C("#FFB542"),
        "GPU" when style==WidgetStyle.AioAuroraFusion=>C("#E551EE"),
        "GPU"=>C(style is WidgetStyle.AioPrecision or WidgetStyle.AioInstrument?"#86EC50":"#B18BFF"),
        "Cooling" when r.Label.Contains("CPU",StringComparison.OrdinalIgnoreCase)=>C("#AC88F0"),
        "Cooling" when r.Label.Contains("Pump",StringComparison.OrdinalIgnoreCase)=>C("#FA7A74"),
        "Cooling" when r.Label.Contains("Gehäuse",StringComparison.OrdinalIgnoreCase)||r.Label.Contains("Chassis",StringComparison.OrdinalIgnoreCase)=>C("#EAB94A"),
        "Cooling"=>C("#40D6D8"), "Rails"=>C("#91D448"), "System" when r.Key.EndsWith("ram")=>C("#29D3DD"),
        "System" when style is WidgetStyle.AioOrbitNetwork or WidgetStyle.AioIsometricCore=>C("#EAB94A"),
        "System" when style is WidgetStyle.AioPrecision or WidgetStyle.AioStudioLight=>C("#29D3DD"),
        "System"=>C("#B18BFF"),
        "Drives" when r.Key.EndsWith("write")=>C("#F3BC4A"), "Drives" when r.Key.EndsWith("used")=>C("#B18BFF"),
        "Network" when r.Key.EndsWith("up")=>C("#F3BC4A"), "Network" when r.Key.EndsWith("link")=>C("#B18BFF"), _=>C("#29D3DD")
    };
    private static Color C(string hex)=>Color.Parse(hex);
    private string Display(AioReading r)
    {
        if(r.Key=="aio.system.ram" && style is not (WidgetStyle.AioPrecision or WidgetStyle.AioChromatic or WidgetStyle.AioStudioLight or WidgetStyle.AioInstrument)) return Format.Percent(r.Number);
        return L.Language=="de"?r.Value.Replace('.',','):r.Value;
    }
    private TextBlock Text(string text,double size=12,Color? color=null,bool number=false)
    {
        var t=Ui.Text(text,size,color??ink,number?FontWeight.SemiBold:FontWeight.Medium,numbers:number);
        // Measured values keep the room of their widest form, so nothing beside them moves when the digits change.
        if(number)t=new AioValueText{Text=t.Text,FontFamily=t.FontFamily,FontStretch=t.FontStretch,FontSize=t.FontSize,FontWeight=t.FontWeight,
            Foreground=t.Foreground,FontFeatures=t.FontFeatures,VerticalAlignment=t.VerticalAlignment};
        t.TextWrapping=TextWrapping.Wrap; t.TextTrimming=TextTrimming.None; return t;
    }
    private TextBlock Value(AioReading r,double size=13,Color? color=null)
    {
        var t=SensorDetails.Value(Text(Display(r),size,color??ink,true),r.Key);
        if(t is AioValueText value)value.Hint=AioValueText.HintFor(r.Key,r.Group);
        if(!fields.TryGetValue(r.Key,out var list)) fields[r.Key]=list=new(); list.Add(t); return t;
    }
    private Control Divider()=>new Border { Height=1,Background=Palette.Brush(track),Margin=new Thickness(0,5) };
    private StackPanel Stack(double gap,params Control[] controls)=>Ui.Stack(gap,controls);
    private Control Header(string label,string? model=null)
    {
        WidgetKind? kind=label=="CPU"?WidgetKind.Cpu:label=="GPU"?WidgetKind.Gpu:null;
        if(kind.HasValue)
        {
            var owned=source.Widgets.FirstOrDefault(w=>w.Kind==kind);
            label=Naming.Title(owned,0,label);
            model=Naming.ShowHardware(owned,settings)?Naming.HardwareShort(owned,0,model??""):"";
        }
        label=Translate(label);
        var heading=Text(label,15,C(lightSurface?"#174FA8":"#A8E5E9")); TextFonts.Mark(heading, TextRole.Title);
        Control caption=heading;
        if(style==WidgetStyle.AioTypeBlock&&kind.HasValue)
        {
            heading.FontSize=42;heading.FontWeight=FontWeight.Bold;heading.Foreground=Palette.Brush(ink);
            caption=Ui.Columns("Auto,*",new Border{Width=5,Height=28,Background=Palette.Brush(C("#FA7F72")),Margin=new Thickness(0,0,7,0)},heading);
        }
        var result=Stack(3,caption);
        if(settings.ShowNames&&(slot?.ShowHardware??true)&&!string.IsNullOrWhiteSpace(model)) result.Children.Add(Text(model,10.5,muted));
        return result;
    }
    private static string Translate(string value)=>L.Language=="de"?value switch {"Voltages"=>"Spannungen","Core temperatures"=>"Kerntemperaturen","No data"=>"Nicht verfügbar","Package"=>"Paket",_=>value}:value;

    private void Build(List<AioReading> list)
    {
        BuildReference(list);
    }
    private void Horizontalize()
    {
        // Keep native sections at readable sizes in a bar. Each section has its own viewport,
        // so a tall GPU section cannot shrink the entire bar and its neighbouring widgets.
        var original=content.Children.ToArray();content.Children.Clear();
        int start=0;
        // The header line (only there when the card has a name or hardware name) stays on top.
        if(original.Length>=2&&Equals(original[0].Tag,"aio-header"))
        {content.Children.Add(original[0]);start=2;}
        var strip=new StackPanel {Orientation=Orientation.Horizontal,Spacing=12};
        var group=new List<Control>();double height=Math.Max(70,settings.BarHeight-155);
        void Flush()
        {
            if(group.Count==0)return;
            var column=Stack(6,group.ToArray());group.Clear();
            var viewport=new ScrollViewer {Width=style==WidgetStyle.AioSignalRack?240:320,Height=height,Content=column,
                HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
            strip.Children.Add(viewport);
        }
        foreach(var item in original.Skip(start))
        {
            if(item is Border {Height:1}){Flush();continue;}
            if(item is AioRibbon)Flush();
            bool independent=Equals(item.Tag,"aio-section");
            if(independent)Flush();group.Add(item);if(independent)Flush();
        }
        Flush();
        content.Children.Add(new ScrollViewer {Content=strip,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled});
    }
    private Color TileColor(AioReading r)=>palette.Chosen??(r.Key.EndsWith("load")?C("#9B28DD"):r.Key.EndsWith("clock")?C("#1769D9"):
        r.Key.EndsWith("power")?C("#C832A9"):r.Key.EndsWith("voltage")?C("#7B27D9"):r.Key.EndsWith("hotspot")||r.Key.EndsWith("junction")?C("#C58712"):
        r.Group=="Rails"?C("#2FAF31"):r.Group=="Cooling"?C("#176AD5"):C("#058F9F"));
    private Color ColorsFor(int i)=>new[]{C("#31D9D9"),C("#31D9D9"),C("#AC88F0"),C("#EAB94A"),C("#FA7A74")}[i%5];
    private Control Meter(AioReading r)
    {
        var m=new AioMeter(Accent(r),track,0) {Height=6,Margin=new Thickness(0,0,7,0)}; meters.Add((r.Key,m)); return m;
    }
    private Control Gauge(AioReading r,double size=94,string? caption=null,AioReading? secondary=null)
    {
        if(style!=WidgetStyle.AioOrbitNetwork)size*=settings.ActiveWidget?.Graphics??1;
        var ring=new AioRing(style==WidgetStyle.AioPopConsole&&lightSurface?C("#252B1D"):Accent(r),track,style==WidgetStyle.AioCarbonPulse,style is WidgetStyle.AioAuroraFusion or WidgetStyle.AioPopConsole) {Width=size,Height=size}; rings.Add((r.Key,ring));
        // Only the numbers shrink into the ring; the caption keeps its size (5.7.0 audit: "Load" was drawn at 6 px).
        var label=Text(caption??(r.Group=="System"?r.Label:L.T(r.Label)),10.5,muted);label.TextAlignment=TextAlignment.Center;
        label.HorizontalAlignment=HorizontalAlignment.Center;label.MaxWidth=size*.72;label.TextTrimming=TextTrimming.CharacterEllipsis;
        var numbers=Stack(2,Value(r,size>110?27:style is WidgetStyle.AioAuroraFusion or WidgetStyle.AioPopConsole?26:20,style==WidgetStyle.AioPopConsole?ink:Accent(r)));
        if(secondary!=null)numbers.Children.Add(Value(secondary,11));
        foreach(var text in numbers.Children.OfType<TextBlock>())text.TextAlignment=TextAlignment.Center;
        var fit=new AioFit{MaxWidth=size*.68,Child=new Viewbox {Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,Child=new AioWidest{Child=numbers}}};
        var face=Stack(1,label,fit);face.HorizontalAlignment=HorizontalAlignment.Center;face.VerticalAlignment=VerticalAlignment.Center;
        return new Viewbox {MaxWidth=size,MaxHeight=size,Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,
            Child=new Grid {Width=size,Height=size,Background=style==WidgetStyle.AioOrbitNetwork?Palette.Brush(back):null,Children={ring,face}}};
    }
}
