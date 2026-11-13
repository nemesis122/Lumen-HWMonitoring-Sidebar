using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Lumen.Core;

namespace Lumen.App;

public sealed partial class AioTile
{
    public event Action? SettingsRequested;
    private readonly List<(string Key,string? Second,AioReferenceHistory Plot)> referencePlots=new();
    private readonly HashSet<string> referenceUsed=new();
    private List<AioReading> referenceRows=new();
    private bool drivesPlaced;
    private readonly HashSet<string> diagramDisks=new();
    private static readonly Lazy<Bitmap> isometricArt=new(()=>new Bitmap(AssetLoader.Open(new Uri("avares://Lumen/Assets/Aio/reference-isometric.png"))));
    private Color Cyan=>C("#24D5DD");
    private Color Purple=>C("#AA79EE");
    private Color Amber=>C("#F5B94D");
    private Color Coral=>C("#FA7E74");
    private bool TableReference=>style is WidgetStyle.AioPrecision or WidgetStyle.AioChromatic or WidgetStyle.AioStudioLight or WidgetStyle.AioInstrument;
    private AioReading? R(string suffix,string group="CPU",string? device=null)=>referenceRows.FirstOrDefault(r=>r.Group==group&&r.Key.EndsWith("."+suffix)&&(device==null||r.Device==device));
    private AioReading[] Rs(string group)=>referenceRows.Where(r=>r.Group==group).ToArray();
    private string RefLabel(AioReading r)=>!TableReference&&r.Key.EndsWith(".junction")?(L.Language=="de"?"Speicher":L.T("Memory junction")):!TableReference&&r.Key.EndsWith(".power")?L.T("Power"):Translate(L.T(r.Label));
    private TextBlock V(AioReading r,double size=17,Color? color=null)
    {
        referenceUsed.Add(r.Key);var t=Value(r,size,color);
        if(r.Group=="Cooling"){formats[t]=row=>row.Number.HasValue?Format.Number(row.Number)+" rpm":"—";t.Text=formats[t](r);}return t;
    }
    private Control Fit(Control c)=>c is TextBlock text?new AioReferenceValue(text):new Viewbox{Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,HorizontalAlignment=HorizontalAlignment.Stretch,Child=new AioWidest{Child=c}};
    /// <summary>A label that never breaks inside a word: it shrinks to its column instead, aligned left.</summary>
    private Control FitLeft(TextBlock text){var fit=Fit(text);if(fit is Decorator{Child:Viewbox box})box.HorizontalAlignment=HorizontalAlignment.Left;return fit;}
    private Control Cols(int count,params Control[] children)=>new StatPanel(Array.Empty<double>(),children){MaxColumns=count,MinimumColumnWidth=count>=4?48:count==3?70:105,RespectIntrinsicWidth=false,Balanced=false,RowSpacing=7};
    private Grid FixedCols(int count,params Control[] children)
    {
        var g=new Grid{ColumnDefinitions=new ColumnDefinitions(string.Join(',',Enumerable.Repeat("*",count))),ColumnSpacing=5,RowSpacing=5};
        for(int i=0;i<children.Length;i++){if(i%count==0)g.RowDefinitions.Add(new RowDefinition(GridLength.Auto));Grid.SetColumn(children[i],i%count);Grid.SetRow(children[i],i/count);g.Children.Add(children[i]);}return g;
    }
    // Shapes drawn on top of the Orbit lines stay opaque, so no ring or connector runs through their values.
    private static Color Solid(Color c)=>Color.FromArgb(255,c.R,c.G,c.B);
    private Control Frame(Control c,Color? color=null,double padding=8,double radius=9)=>new Border{Background=Palette.Brush(color??SeeThrough(back)),CornerRadius=new CornerRadius(radius),Padding=new Thickness(padding),Child=c};
    private Control RefRow(AioReading r,bool bar=false,int mode=0,Color? color=null)
    {
        referenceUsed.Add(r.Key);var value=V(r,16);value.TextAlignment=TextAlignment.Right;
        if(!bar)
        {
            bool sculpted=style is WidgetStyle.AioAuroraFusion or WidgetStyle.AioPopConsole or WidgetStyle.AioCarbonPulse;
            if(sculpted)value.FontSize=13;var row=Ui.Columns("*,Auto",FitLeft(Text(RefLabel(r),sculpted?11:14)),value);row.ColumnSpacing=6;return row;
        }
        var m=new AioMeter(color??Accent(r),track,mode){Height=mode==1?20:mode==2?10:7,MinWidth=35};meters.Add((r.Key,m));
        if(r.Group=="Cooling")
        {
            value.FontSize=14;var fanLabel=Text(Translate(L.T(r.Label)),12);fanLabel.MaxWidth=105;fanLabel.MinWidth=52;var fan=Ui.Columns("Auto,*,Auto",fanLabel,m,value);fan.ColumnSpacing=6;return fan;
        }
        Control label=FitLeft(Text(Translate(L.T(r.Label)),13));
        if(style==WidgetStyle.AioInstrument){var withGlyph=Ui.Columns("Auto,*",Ui.Glyph(RefGlyph(r),Accent(r),15),label);withGlyph.ColumnSpacing=3;label=withGlyph;}
        var grid=Ui.Columns(".9*,1.4*,1.1*",label,Fit(value),m);grid.ColumnSpacing=6;return grid;
    }
    private static string RefGlyph(AioReading r)=>r.Group=="Cooling"?Glyphs.Fan:r.Group=="Drives"?Glyphs.Drive:r.Group=="Rails"||r.Key.EndsWith("voltage")||r.Key.EndsWith("power")?Glyphs.Power:r.Key.EndsWith("load")?Glyphs.Fps:r.Key.EndsWith("clock")?Glyphs.Ping:r.Key.EndsWith("vram")||r.Key.EndsWith("ram")?Glyphs.Memory:
        "M6,3 A2,2 0 0 1 10,3 V9 A4,4 0 1 1 6,9 Z M8,5 V11 M8,11 m-1,0 a1,1 0 1 0 2,0 a1,1 0 1 0 -2,0";
    private Control MainTemp(AioReading r,double size=67,Color? color=null)
    {
        var t=V(r,size,color??Accent(r));formats[t]=row=>Format.Temperature(row.Number,settings.Fahrenheit);t.Text=formats[t](r);
        var unit=Text(Format.TemperatureUnit(settings.Fahrenheit),size*.34,color??Accent(r));unit.VerticalAlignment=VerticalAlignment.Bottom;unit.Margin=new Thickness(1,0,0,size*.13);
        var line=new StackPanel{Orientation=Orientation.Horizontal,Spacing=0,Children={t,unit}};
        return new AioReferenceValue(t,line);
    }
    private Control HistoryPlot(AioReading r,double height=32,string mode="area",Color? color=null,AioReading? second=null)
    {
        if(!settings.Shows("aio.graphs"))return new Border();
        referenceUsed.Add(r.Key);if(second!=null)referenceUsed.Add(second.Key);
        var graph=new AioReferenceHistory{Color=color??Accent(r),OtherColor=Amber,Mode=mode,Height=height,Tag="graphic"};referencePlots.Add((r.Key,second?.Key,graph));return graph;
    }
    private void UpdateReferencePlots(History h)
    {
        double[] Series(string key){var r=readings.GetValueOrDefault(key);var values=r!=null&&r.History.Length>0?h.Read(r.History):samples.Read(key);return values.Length>0?values:samples.Read(key);}
        foreach(var (key,second,plot) in referencePlots)plot.Set(Series(key),second==null?null:Series(second));
    }
    private Control RefHeading(string title,string glyph,Color color)=>Ui.Columns("Auto,*",new Border{Margin=new Thickness(0,0,7,0),Child=Ui.Glyph(glyph,color,21)},Text(Translate(title),19,color));
    private void RefSection(Control body,Color color,bool last=false)
    {
        Control section;
        // Aurora Fusion: no glowing wavy frame per section (user request); plain sections with thin dividers.
        if(style==WidgetStyle.AioCarbonPulse)section=new AioReferenceSurface{Alpha=cardAlpha/255d,Mode="carbon",Light=lightSurface,Tint=color,Padding=new Thickness(13,19,13,12),Child=body};
        else if(style==WidgetStyle.AioPopConsole)section=new AioReferenceSurface{Alpha=cardAlpha/255d,Mode="pop",Dark=!lightSurface,Tint=color,Padding=new Thickness(12,14),Child=body};
        else if(style==WidgetStyle.AioPrecision)section=new Border{BorderBrush=Palette.Brush(C("#34445E")),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(12),Padding=new Thickness(9,8),Child=body};
        else section=new Border{Padding=new Thickness(9,style==WidgetStyle.AioSignalRack?5:10),Child=body};
        section.Tag="aio-section";content.Children.Add(section);
        if(!last && style is not (WidgetStyle.AioPrecision or WidgetStyle.AioCarbonPulse or WidgetStyle.AioPopConsole))
        {var divider=Divider();if(style==WidgetStyle.AioSignalRack)divider.Margin=new Thickness(0,2);content.Children.Add(divider);}
    }
    private void ReferenceHeader()
    {
        if(slot?.ShowTitle==false)return;
        if(style==WidgetStyle.AioIsometricCore){IsometricHeader();return;}
        int number=style switch{WidgetStyle.AioTypeBlock=>51,WidgetStyle.AioOrbitNetwork=>52,WidgetStyle.AioThermalGrid=>53,WidgetStyle.AioSignalRack=>54,WidgetStyle.AioIsometricCore=>55,WidgetStyle.AioPrecision=>33,WidgetStyle.AioChromatic=>34,WidgetStyle.AioStudioLight=>35,WidgetStyle.AioInstrument=>36,WidgetStyle.AioAuroraFusion=>40,WidgetStyle.AioPopConsole=>41,_=>42};
        bool modern=number is >=40 and <=42;
        // No design name as title (user request); a name the user gave the widget is still shown.
        string title=string.IsNullOrWhiteSpace(slot?.Name)?"":slot.Name;
        // No "● Live" badge and no settings button: the sidebar already has its own settings at the top (user request).
        // Only a name the user gave the card, or its hardware name, still gets a header line.
        var header=Stack(0);
        if(title.Length>0)header.Children.Add(Text(title,TableReference?23:modern?17:20));
        if(settings.ShowNames&&slot is {ShowHardware:true,HardwareName.Length:>0})header.Children.Add(Text(slot.HardwareName,12,muted));
        if(header.Children.Count>0){content.Children.Add(new Border{Padding=new Thickness(9,10),Child=header,Tag="aio-header"});content.Children.Add(Divider());}
    }
    private void BuildReference(List<AioReading> rows)
    {
        content.Children.Clear();fields.Clear();formats.Clear();meters.Clear();charts.Clear();rings.Clear();heat.Clear();referencePlots.Clear();referenceUsed.Clear();referenceRows=rows;drivesPlaced=false;diagramDisks.Clear();
        Padding=new Thickness(style is WidgetStyle.AioAuroraFusion or WidgetStyle.AioPopConsole?5:3);content.Spacing=style is WidgetStyle.AioPrecision or WidgetStyle.AioCarbonPulse?4:0;content.Tag=null;
        CornerRadius=new CornerRadius(style==WidgetStyle.AioTypeBlock?8:20);
        ReferenceHeader();
        ReferenceModelNames();
        switch(style)
        {
            case WidgetStyle.AioTypeBlock: ReferenceType();break;
            case WidgetStyle.AioOrbitNetwork: ReferenceOrbit();break;
            case WidgetStyle.AioThermalGrid: ReferenceThermal();break;
            case WidgetStyle.AioSignalRack: ReferenceRack();break;
            case WidgetStyle.AioIsometricCore: ReferenceIsometric();break;
            case WidgetStyle.AioIsometricAtlas: IsometricAtlas();break;
            case WidgetStyle.AioIsometricTiles: IsometricTiles();break;
            case WidgetStyle.AioIsometricRig: IsometricRig();break;
            case WidgetStyle.AioIsometricPedestals: IsometricPedestals();break;
            case WidgetStyle.AioIsometricCompact: IsometricCompact();break;
            case WidgetStyle.AioIsometricRings: IsometricRings();break;
            case WidgetStyle.AioPrecision:case WidgetStyle.AioChromatic:case WidgetStyle.AioStudioLight:case WidgetStyle.AioInstrument: ReferenceTables();break;
            default:ReferenceSculpted();break;
        }
        ReferenceDrives();
        ReferenceCoreBars();
        // Explicitly selected additional devices/sensors stay available without duplicating the reference's main values.
        var extras=rows.Where(r=>!referenceUsed.Contains(r.Key)).ToArray();
        foreach(var group in extras.GroupBy(r=>r.Group+":"+r.Device))
            RefSection(Stack(6,group.Select(r=>RefRow(r)).ToArray()),Accent(group.First()),true);
    }
    private void ReferenceModelNames()
    {
        if(!settings.ShowNames||slot?.ShowHardware==false)return;
        var names=Stack(2);bool diagram=style is WidgetStyle.AioOrbitNetwork or WidgetStyle.AioThermalGrid or WidgetStyle.AioSignalRack or WidgetStyle.AioIsometricCore or WidgetStyle.AioPopConsole or WidgetStyle.AioIsometricRig or WidgetStyle.AioIsometricPedestals or WidgetStyle.AioIsometricRings;
        void Add(WidgetKind kind,string group,string fallback,string model,int index=0)
        {
            if(!referenceRows.Any(r=>r.Group==group))return;
            var owned=source.Widgets.FirstOrDefault(w=>w.Kind==kind);if(!Naming.ShowHardware(owned,settings))return;
            var label=Naming.Title(owned,index,fallback);var hardware=Naming.Hardware(owned,index,model);if(hardware.Length==0)return;
            names.Children.Add(Ui.Columns("Auto,*",Text(label.Length==0?"":label+" · ",10,muted),Text(hardware,10,muted)));
        }
        if(diagram)
        {
            if(snapshot.Cpu!=null)Add(WidgetKind.Cpu,"CPU","CPU",snapshot.Cpu.Name);
            var gpus=GpuGroup.Visible(snapshot,settings);for(int i=0;i<gpus.Count;i++)Add(WidgetKind.Gpu,"GPU","GPU",gpus[i].Name,i);
        }
        // Drive and network model names are not listed in AIO cards (user request); drives are named by their letters.
        if(names.Children.Count>0)content.Children.Add(new Border{Padding=new Thickness(9,2,9,7),Child=names});
    }
    private void ReferenceType()
    {
        foreach(var group in referenceRows.Where(r=>r.Group is "CPU" or "GPU").GroupBy(r=>r.Group+":"+r.Device))
        {
            var rs=group.ToArray();string name=rs[0].Group;var body=Stack(4);
            var heading=Header(name,name=="CPU"?snapshot.Cpu?.Name:snapshot.Gpus.FirstOrDefault(g=>g.Id==rs[0].Device)?.Name);
            var temp=rs.FirstOrDefault(r=>r.Key.EndsWith(".temp"));var detail=Stack(12,rs.Where(r=>!new[]{"temp","hotspot","junction"}.Any(k=>r.Key.EndsWith("."+k))).Select(r=>RefRow(r)).ToArray());
            if(temp!=null)body.Children.Add(Cols(2,Stack(0,heading,MainTemp(temp,99,Cyan)),new Border{Padding=new Thickness(5,5,0,0),Child=detail}));else body.Children.Add(Stack(3,heading,detail));
            foreach(var r in rs.Where(r=>r.Key.EndsWith(".hotspot")||r.Key.EndsWith(".junction")))
            {
                var line=Ui.Columns("*,Auto",Text(RefLabel(r),23,r.Key.EndsWith(".hotspot")?C("#10131B"):ink),V(r,30,r.Key.EndsWith(".hotspot")?C("#10131B"):ink));
                body.Children.Add(r.Key.EndsWith(".hotspot")?new AioReferenceSurface{Alpha=cardAlpha/255d,Mode="banner",Tint=Coral,Padding=new Thickness(12,4),Child=line}:line);
            }
            RefSection(body,Cyan);
        }
        ReferenceCooling();ReferenceRails();ReferenceSummary();
    }
    private void ReferenceTables()
    {
        foreach(var group in referenceRows.Where(r=>r.Group is "CPU" or "GPU").GroupBy(r=>r.Group+":"+r.Device))
        {
            string name=group.First().Group;Color color=name=="CPU"?(style==WidgetStyle.AioStudioLight?C("#0065FF"):Cyan):style==WidgetStyle.AioStudioLight?Coral:C("#8BEA43");
            var owned=source.Widgets.FirstOrDefault(w=>w.Kind==(name=="CPU"?WidgetKind.Cpu:WidgetKind.Gpu));
            var body=Stack(3,RefHeading(Naming.Title(owned,0,name),name=="CPU"?Glyphs.Cpu:Glyphs.Gpu,color));
            if(settings.ShowNames&&slot?.ShowHardware!=false)
            {string? model=name=="CPU"?snapshot.Cpu?.Name:snapshot.Gpus.FirstOrDefault(g=>g.Id==group.First().Device)?.Name;model=Naming.ShowHardware(owned,settings)?Naming.Hardware(owned,0,model??""):"";if(!string.IsNullOrEmpty(model))body.Children.Add(Text(model,10,muted));}
            if(style==WidgetStyle.AioChromatic)body.Children.Add(ReferenceTiles(group));
            else foreach(var r in group)body.Children.Add(RefRow(r,true,style==WidgetStyle.AioInstrument&&settings.Shows("aio.graphs")?1:0));
            RefSection(body,color);
        }
        ReferenceCooling();ReferenceRails();
        var system=SystemWithoutDrives();var systemBody=Stack(6,RefHeading(L.T("System"),Glyphs.Drive,Cyan));
        if(style==WidgetStyle.AioChromatic)systemBody.Children.Add(ReferenceTiles(system));else foreach(var r in system)systemBody.Children.Add(RefRow(r,true,style==WidgetStyle.AioInstrument?2:0));
        if(system.Length>0){systemBody.Children.Add(ReferenceNetwork());RefSection(systemBody,Cyan,true);}else if(Rs("Network").Length>0)RefSection(ReferenceNetwork(),Cyan,true);
    }
    private Control ReferenceTiles(IEnumerable<AioReading> rs)
    {
        return Cols(2,rs.Select(r=>
        {
            Color color=TileColor(r);var line=Ui.Columns("Auto,*",Ui.Glyph(RefGlyph(r),C("#DDF8FF"),24),Stack(0,Text(Translate(L.T(r.Label)),12,C("#E1D9FC")),Fit(V(r,18,Colors.White))));line.ColumnSpacing=9;
            var gradient=new LinearGradientBrush{GradientStops={new(Palette.Mix(color,Colors.White,.08),0),new(Palette.Mix(color,Colors.Black,.25),1)}};
            return (Control)new Border{Background=gradient,BorderBrush=Palette.Brush(Palette.Mix(color,Colors.White,.2)),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Padding=new Thickness(9,5),Margin=new Thickness(0,0,4,0),Child=line};
        }).ToArray());
    }
    private void ReferenceCooling(bool fans=false)
    {
        if(!AioReadings.Selected(settings,"aio.cooling"))return;var rows=Rs("Cooling");
        Control heading=TableReference?RefHeading(L.T("Cooling"),Glyphs.Fan,style==WidgetStyle.AioStudioLight?Purple:Cyan):Text(L.T("Cooling")+(style==WidgetStyle.AioTypeBlock?(L.Language=="de"?" (Lüfter) – Drehzahl":" (Fans) – speed"):style is WidgetStyle.AioOrbitNetwork or WidgetStyle.AioThermalGrid?" – "+L.T("Fan")+" (rpm)":""),16,ink);
        var body=Stack(8,heading);
        if(rows.Length==0)body.Children.Add(Text(L.T("No data"),12,muted));
        else if(style==WidgetStyle.AioChromatic)body.Children.Add(ReferenceTiles(rows));
        else if(fans)
        {
            body.Children.Add(Cols(5,rows.Select((r,i)=>
            {
                bool pop=style==WidgetStyle.AioPopConsole;var label=Text(r.Label,10,pop?ink:muted);label.TextAlignment=TextAlignment.Center;var value=V(r,13,ink);formats[value]=row=>Format.Number(row.Number);value.Text=formats[value](r);value.TextAlignment=TextAlignment.Center;
                var unit=Text("rpm",9,pop?ink:muted);unit.TextAlignment=TextAlignment.Center;
                return (Control)Stack(3,new AioReferenceFan{Color=pop?ink:ReferenceFanColor(i),Outline=pop,Height=42,Tag="graphic"},label,value,unit);
            }).ToArray()));
        }
        else if(style==WidgetStyle.AioStudioLight)body.Children.Add(new StatPanel(Array.Empty<double>(),rows.Select(r=>(Control)new Border{Padding=new Thickness(0,0,12,0),Child=RefRow(r)}).ToArray()){MaxColumns=2,MinimumColumnWidth=165,RespectIntrinsicWidth=false,Balanced=false,RowSpacing=7});
        else foreach(var (r,i) in rows.Select((r,i)=>(r,i)))body.Children.Add(RefRow(r,true,style==WidgetStyle.AioInstrument?2:style is WidgetStyle.AioThermalGrid or WidgetStyle.AioSignalRack or WidgetStyle.AioCarbonPulse?3:0,ReferenceFanColor(i)));
        RefSection(body,style==WidgetStyle.AioPopConsole?C("#D7FF16"):Cyan);
    }
    private Color ReferenceFanColor(int index)=>style is WidgetStyle.AioAuroraFusion or WidgetStyle.AioCarbonPulse or WidgetStyle.AioPopConsole?new[]{Cyan,Purple,C("#5496F5"),C("#BADA43"),Coral}[index%5]:ColorsFor(index);
    private void ReferenceRails()
    {
        var rows=Rs("Rails");if(rows.Length==0)return;
        var body=Stack(8,TableReference?RefHeading(L.T("Voltages"),Glyphs.Power,style==WidgetStyle.AioStudioLight?Coral:Cyan):Text(L.Language=="de"?"Versorgungsspannungen (Netzteil)":"Supply voltages (PSU)",13,style==WidgetStyle.AioPopConsole?ink:muted));
        if(style is WidgetStyle.AioPrecision or WidgetStyle.AioInstrument)foreach(var r in rows)body.Children.Add(RefRow(r,true,style==WidgetStyle.AioInstrument?2:0,C("#7BEF43")));
        else body.Children.Add(Cols(3,rows.Select((r,i)=>
        {
            Color tint=new[]{Cyan,Purple,Amber}[i%3];Control item=Stack(1,Text(r.Label,12,style==WidgetStyle.AioChromatic?Colors.White:ink),V(r,style==WidgetStyle.AioTypeBlock?23:18,style==WidgetStyle.AioChromatic?Colors.White:style==WidgetStyle.AioPopConsole?ink:tint));
            if(style==WidgetStyle.AioChromatic)item=Frame(item,C("#289526"));
            else if(style is WidgetStyle.AioOrbitNetwork or WidgetStyle.AioIsometricCore or WidgetStyle.AioCarbonPulse or WidgetStyle.AioAuroraFusion)item=new Border{BorderBrush=Palette.Brush(tint),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Padding=new Thickness(5),Child=item};
            else if(style==WidgetStyle.AioPopConsole)item=new AioReferenceSurface{Alpha=cardAlpha/255d,Mode="pop",Dark=!lightSurface,Tint=new[]{C("#284BFF"),C("#FF9336"),C("#C7AFF5")}[i%3],Padding=new Thickness(8),Child=item};
            return item;
        }).ToArray()));
        RefSection(body,style==WidgetStyle.AioPopConsole?C("#F1F1ED"):Amber);
    }
    private Control ReferenceNetwork()
    {
        var rows=Rs("Network");return Cols(2,rows.Select(r=>NetLine(r,20,14)).ToArray());
    }
    /// <summary>Download/upload carry an arrow; the link speed is a plain labelled value (no graph).</summary>
    private Control NetLine(AioReading r,double arrow,double size,Color? color=null)
    {
        if(r.Key.EndsWith(".link"))
        {
            return Stack(0,FitLeft(Text(L.T("Link speed"),Math.Max(10,size-4),muted)),FitLeft(V(r,size,color)));
        }
        // The value shrinks instead of wrapping: a wrapped "0 kbit/s" made the lines below jump with every update.
        return Ui.Columns("Auto,*",Text(r.Key.EndsWith("down")?"↓":"↑",arrow,r.Key.EndsWith("down")?Cyan:Amber),FitLeft(V(r,size,color)));
    }
    /// <summary>Read, write and fill level per drive. Temperatures not already shown by the design join their drive.</summary>
    // Drive temperatures are shown with their drive in the Drives part, in the same column for every drive.
    private AioReading[] SystemWithoutDrives()=>Rs("System").Where(r=>!r.Key.StartsWith("aio.system.disk.")).ToArray();
    private Control? DrivesBody()
    {
        var rows=Rs("Drives");
        // The readings already follow the user's drive order.
        var order=referenceRows.Where(r=>r.Group=="Drives"||r.Key.StartsWith("aio.system.disk.")).Select(r=>r.Device).Distinct().ToList();
        var ids=order.Where(id=>rows.Any(r=>r.Device==id)||Rs("System").Any(r=>r.Key=="aio.system.disk."+id&&(!referenceUsed.Contains(r.Key)||diagramDisks.Contains(r.Key)))).ToArray();
        if(ids.Length==0)return null;
        // Same columns for every drive (Temp. · Read · Write · Used) so the values line up; a drive
        // without one of them keeps an empty cell instead of shifting the others to the left.
        var perDrive=ids.Select(id=>
        {
            var items=new List<AioReading>();
            var temp=Rs("System").FirstOrDefault(r=>r.Key=="aio.system.disk."+id&&(!referenceUsed.Contains(r.Key)||diagramDisks.Contains(r.Key)));
            if(temp!=null)items.Add(temp with {Label="Temp."});
            items.AddRange(rows.Where(r=>r.Device==id));return (id,items);
        }).Where(d=>d.items.Count>0).ToArray();
        string Part(AioReading r)=>r.Key.StartsWith("aio.system.disk.")?"temp":r.Key[(r.Key.LastIndexOf('.')+1)..];
        var parts=new[]{"temp","read","write","used"}.Where(k=>perDrive.Any(d=>d.items.Any(r=>Part(r)==k))).ToArray();
        var body=Stack(8);
        foreach(var (id,items) in perDrive)
        {
            var title=Text(AioReadings.DriveName(snapshot,id),TableReference?14:13,style==WidgetStyle.AioPopConsole?ink:Amber);title.FontWeight=FontWeight.SemiBold;
            body.Children.Add(title);
            if(style==WidgetStyle.AioChromatic)body.Children.Add(ReferenceTiles(items));
            else if(TableReference)foreach(var r in items)body.Children.Add(RefRow(r,true,style==WidgetStyle.AioInstrument?2:0));
            else
            {
                bool sculpted=style is WidgetStyle.AioAuroraFusion or WidgetStyle.AioPopConsole or WidgetStyle.AioCarbonPulse;
                // Rates need more room than temperature and fill level; the same weights for every drive keep the columns aligned.
                var driveColumns=Ui.Columns(string.Join(',',parts.Select(part=>part is "read" or "write"?"1.2*":".8*")),parts.Select(part=>
                {
                    var r=items.FirstOrDefault(x=>Part(x)==part);if(r==null)return (Control)new Border();
                    // Value left-aligned under its caption, so every drive's column starts at the same edge.
                    var value=Fit(V(r,style==WidgetStyle.AioTypeBlock?17:15,style==WidgetStyle.AioPopConsole?ink:Accent(r)));
                    if(value is Decorator{Child:Viewbox box})box.HorizontalAlignment=HorizontalAlignment.Left;
                    Control cell=Stack(1,FitLeft(Text(Translate(L.T(r.Label)),sculpted?10:11,style==WidgetStyle.AioPopConsole?ink:muted)),value);
                    if(sculpted&&part=="used")cell=Stack(4,cell,Meter(r));
                    return cell;
                }).ToArray());driveColumns.ColumnSpacing=4;body.Children.Add(driveColumns);
            }
        }
        // A temperature row consumed here is the original reading; mark it so it is not repeated.
        foreach(var id in ids)referenceUsed.Add("aio.system.disk."+id);
        return body.Children.Count==0?null:body;
    }
    private void ReferenceDrives()
    {
        if(drivesPlaced)return;drivesPlaced=true;
        var body=DrivesBody();if(body==null)return;
        Control heading=TableReference?RefHeading(L.T("Drives"),Glyphs.Drive,Cyan):Text(L.T("Drives"),style==WidgetStyle.AioTypeBlock?16:13,style==WidgetStyle.AioPopConsole?ink:muted);
        bool divided=style is not (WidgetStyle.AioPrecision or WidgetStyle.AioCarbonPulse or WidgetStyle.AioPopConsole);
        if(divided&&content.Children.LastOrDefault() is not Border {Height:1})content.Children.Add(Divider());
        RefSection(Stack(8,heading,body),style==WidgetStyle.AioPopConsole?C("#F3C969"):Amber,true);
    }
    private void ReferenceSummary()
    {
        var systems=SystemWithoutDrives();var cells=systems.Select(r=>
        {
            Control cell=Stack(4,Text(r.Label,15),V(r,25,Accent(r)));
            if(style is WidgetStyle.AioAuroraFusion or WidgetStyle.AioCarbonPulse or WidgetStyle.AioPopConsole)cell=Stack(5,cell,Meter(r));
            if(style==WidgetStyle.AioPopConsole)cell=new AioReferenceSurface{Alpha=cardAlpha/255d,Mode="pop",Dark=!lightSurface,Tint=r.Key.EndsWith("ram")?C("#2945FF"):C("#CDB8F7"),Padding=new Thickness(9),Child=cell};return cell;
        }).ToList();
        if(style==WidgetStyle.AioPopConsole)
            for(int i=0;i<cells.Count;i++)foreach(var text in cells[i].GetLogicalDescendants().OfType<TextBlock>())text.Foreground=Palette.Brush(i==0?Colors.White:ink);
        if(Rs("Network").Length>0)
        {
            Control network=Stack(4,Text(L.T("Network"),13),Stack(4,Rs("Network").Select(r=>NetLine(r,17,12)).ToArray()));
            if(style==WidgetStyle.AioPopConsole)network=new AioReferenceSurface{Alpha=cardAlpha/255d,Mode="pop",Dark=!lightSurface,Tint=C("#D9FF18"),Padding=new Thickness(7),Child=network};cells.Add(network);
        }
        if(cells.Count>0)RefSection(Cols(3,cells.ToArray()),style==WidgetStyle.AioPopConsole?C("#ECECE8"):Cyan,true);
    }
    private void ReferenceOrbit()
    {
        var map=new Canvas{Width=334,Height=375};map.Children.Add(new AioOrbitLines(Cyan,Purple){Width=320,Height=320,Margin=new Thickness(7,16,0,0)});
        var nodes=new[]{(R("temp"),new Point(113,4),"CPU",Cyan),(R("ram","System"),new Point(3,128),"RAM",Purple),(R("temp","GPU"),new Point(113,234),"GPU",Cyan),(Rs("System").FirstOrDefault(r=>!r.Key.EndsWith("ram")),new Point(243,128),"",Amber)};
        foreach(var (r,point,name,color) in nodes)
        {
            if(r==null)continue;
            string caption=name.Length>0?name:r.Label;if(r.Group=="System"&&!r.Key.EndsWith("ram"))diagramDisks.Add(r.Key);
            var face=Stack(1,Text(caption,16,ink),V(r,name is "CPU" or "GPU"?31:25,color));
            var secondary=R("load",r.Group,r.Device);if(secondary!=null&&secondary!=r)face.Children.Add(V(secondary,17));
            foreach(var t in face.Children.OfType<TextBlock>())t.TextAlignment=TextAlignment.Center;
            double diameter=r.Group=="System"?88:108;
            var circle=new Border{Width=diameter,Height=diameter,CornerRadius=new CornerRadius(diameter/2),Background=Palette.Brush(Solid(back)),BorderBrush=Palette.Brush(color),BorderThickness=new Thickness(2),BoxShadow=new BoxShadows(new BoxShadow{Blur=10,Color=Color.FromArgb(70,color.R,color.G,color.B)}),Child=new Viewbox{Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,Margin=new Thickness(9),Child=new AioWidest{Child=face}}};
            Canvas.SetLeft(circle,point.X);Canvas.SetTop(circle,point.Y);map.Children.Add(circle);
        }
        var satellites=referenceRows.Where(r=>r.Key.EndsWith(".voltage")||r.Key.EndsWith(".power")).ToArray();
        var gpuIds=Rs("GPU").Select(r=>r.Device).Distinct().ToList();
        string Caption(AioReading r)
        {
            string device=r.Group=="CPU"?"CPU":"GPU"+(gpuIds.Count>1?" "+(gpuIds.IndexOf(r.Device)+1):"");
            return r.Key.EndsWith(".voltage")?(r.Group=="CPU"?"Vcore":device+" V"):device+" W";
        }
        // Wider value boxes (user request): the value always fits on one line at full size.
        Border Satellite(AioReading r)=>new Border{Width=104,Background=Palette.Brush(C("#242B35")),BorderBrush=Palette.Brush(C("#46505F")),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(16),Padding=new Thickness(10,5),
            Child=Stack(0,Text(Caption(r),11,muted),FitLeft(V(r,17,C("#EEF4FF"))))};
        // Four fixed corners; with a second GPU the remaining values continue below the map instead of covering each other.
        for(int i=0;i<Math.Min(4,satellites.Length);i++)
        {
            var panel=Satellite(satellites[i]);Canvas.SetLeft(panel,i%2==0?0:230);Canvas.SetTop(panel,i<2?16:292);map.Children.Add(panel);
        }
        // GPU hotspot and memory junction belong to the GPU: a row of pills directly under its circle.
        double pillTop=350;
        foreach(var gpuRows in referenceRows.Where(r=>r.Key.EndsWith(".hotspot")||r.Key.EndsWith(".junction")).GroupBy(r=>r.Device))
        {
            var pills=gpuRows.Select(r=>
            {
                Color tint=r.Key.EndsWith(".hotspot")?Coral:muted;
                string caption=(gpuIds.Count>1?"GPU "+(gpuIds.IndexOf(r.Device)+1)+" · ":"")+RefLabel(r)+" ";
                return (Control)new Border{BorderBrush=Palette.Brush(tint),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(16),Padding=new Thickness(9,3),Margin=new Thickness(3,0),Background=Palette.Brush(SeeThrough(back)),
                    Child=new Viewbox{Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,Child=new AioWidest{Child=Ui.Columns("Auto,Auto",Text(caption,13,tint),V(r,16,tint))}}};
            }).ToArray();
            var row=FixedCols(pills.Length,pills);row.Width=334;Canvas.SetLeft(row,0);Canvas.SetTop(row,pillTop);map.Children.Add(row);pillTop+=36;
        }
        map.Height=Math.Max(375,pillTop+4);
        RefSection(Fit(map),Cyan);
        if(satellites.Length>4)RefSection(Cols(4,satellites.Skip(4).Select(r=>(Control)Satellite(r)).ToArray()),Cyan);
        ReferenceCooling(true);ReferenceRails();if(Rs("Network").Length>0)RefSection(Stack(8,Text(L.T("Network"),13,muted),ReferenceNetwork()),Cyan,true);
    }
    private void ReferenceThermal()
    {
        var left=Stack(7);var right=Stack(9,Stack(0,Fit(Text(Translate(L.T("Voltages")),12,muted)),Fit(Text("& "+L.T("Power"),12,muted))));
        var cores=Rs("Cores");
        // Without per-core readings (e.g. many Ryzen CPUs) the grid is left out instead of showing "No data".
        if(AioReadings.Selected(settings,"aio.cpu.cores")&&cores.Length>0)
        {
            left.Children.Add(Text("CPU · "+Translate("Core temperatures")+" ("+Format.TemperatureUnit(settings.Fahrenheit)+")",12));
            left.Children.Add(new StatPanel(Array.Empty<double>(),cores.Select((r,i)=>
            {
                var label=Text("K"+(i+1),12);label.TextAlignment=TextAlignment.Center;var value=V(r,22);formats[value]=row=>Format.Temperature(row.Number,settings.Fahrenheit)+"°";value.Text=formats[value](r);value.TextAlignment=TextAlignment.Center;
                var box=new Border{Padding=new Thickness(2,12),Margin=new Thickness(0,0,3,0),BorderBrush=Palette.Brush(track),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(3),Child=Stack(1,label,value)};heat.Add((r.Key,box));return (Control)box;
            }).ToArray()){MaxColumns=4,MinimumColumnWidth=43,RespectIntrinsicWidth=false,RowSpacing=4});
        }
        foreach(var r in referenceRows.Where(r=>r.Key.EndsWith(".voltage")||r.Key.EndsWith(".power")||r.Group=="Rails"))
            right.Children.Add(Stack(2,FitLeft(Text(r.Group=="Rails"?r.Label:r.Group+" · "+L.T(r.Label),10,muted)),Fit(V(r,15))));
        foreach(var group in referenceRows.Where(r=>r.Group is "CPU" or "GPU").GroupBy(r=>r.Group+":"+r.Device))
        {
            var first=group.First();var t=group.FirstOrDefault(r=>r.Key.EndsWith(".temp"));var load=group.FirstOrDefault(r=>r.Key.EndsWith(".load"));
            // CPU and every GPU use the same three columns (name · temperature · load), so the values line up.
            string caption=first.Group=="GPU"&&Rs("GPU").Select(r=>r.Device).Distinct().Count()>1?"GPU "+(Rs("GPU").Select(r=>r.Device).Distinct().ToList().IndexOf(first.Device)+1):first.Group;
            Control body=FixedCols(3,FitLeft(Text(caption,22)),t==null?new Border():Fit(V(t,24,Cyan)),load==null?new Border():Fit(V(load,19)));
            left.Children.Add(body);
            var hot=group.FirstOrDefault(r=>r.Key.EndsWith(".hotspot"));if(hot!=null)left.Children.Add(Frame(Ui.Columns("*,Auto",Text("Hotspot",14,C("#21151C")),V(hot,22,C("#21151C"))),Coral,6,3));
            var junction=group.FirstOrDefault(r=>r.Key.EndsWith(".junction"));if(junction!=null)left.Children.Add(Frame(Ui.Columns("*,Auto",Text(L.T("Memory junction"),14,C("#151021")),V(junction,22,C("#151021"))),Purple,6,3));
        }
        var rail=new Border{BorderBrush=Palette.Brush(track),BorderThickness=new Thickness(1,0,0,0),Padding=new Thickness(7,0,0,0),Child=right};
        var split=Ui.Columns("3*,1*",left,rail);split.Tag="aio-thermal-split";split.ColumnSpacing=8;RefSection(split,Cyan);
        ReferenceCooling();ReferenceSummary();
    }
    private void ReferenceRack()
    {
        int index=0;
        void Band(string caption,Control body)
        {
            var line=Ui.Columns("Auto,*",Text((++index).ToString("00")+"  ",13,muted),Text(caption,14));
            RefSection(Stack(5,line,body),Cyan);
        }
        foreach(var r in referenceRows.Where(r=>r.Key=="aio.cpu.load"||r.Group=="GPU"&&r.Key.EndsWith(".load")))
        {
            var t=R("temp",r.Group,r.Device);RefSection(Ui.Columns("*,Auto",Stack(5,Text((++index).ToString("00")+"   "+r.Group+" · "+L.T("Load"),14),HistoryPlot(r,32,"area",r.Group=="CPU"?Cyan:Coral)),Stack(2,V(r,19),t==null?new Border():V(t,16))),Cyan);
        }
        foreach(var r in referenceRows.Where(r=>r.Key.EndsWith(".power")).OrderBy(r=>r.Group=="GPU"?0:1))RefSection(Stack(4,Ui.Columns("*,Auto",Text((++index).ToString("00")+"   "+L.T("Power")+" ("+r.Group+")",14),V(r,16)),HistoryPlot(r,25,"bars",r.Group=="GPU"?Amber:Purple)),Cyan);
        var ram=R("ram","System");if(ram!=null)RefSection(Stack(4,Ui.Columns("*,Auto",Text((++index).ToString("00")+"   RAM · "+L.T("Load"),14),V(ram,18)),HistoryPlot(ram,29,"area",Purple)),Cyan);
        foreach(var r in referenceRows.Where(r=>r.Key.EndsWith(".voltage")))RefSection(Ui.Columns("*,Auto",Text((++index).ToString("00")+"   "+r.Group+" · "+L.T("Voltage"),13),V(r,18)),Cyan);
        var fans=Rs("Cooling");if(AioReadings.Selected(settings,"aio.cooling"))Band(L.T("Cooling")+" (rpm)",fans.Length==0?Text(L.T("No data"),12):Stack(7,fans.Select((r,i)=>RefRow(r,true,3,ColorsFor(i))).ToArray()));
        var rails=Rs("Rails");if(rails.Length>0)Band(Translate(L.T("Voltages")),Cols(3,rails.Select((r,i)=>(Control)new Border{BorderBrush=Palette.Brush(ColorsFor(i)),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(6),Padding=new Thickness(4),Child=V(r,17)}).ToArray()));
        drivesPlaced=true;if(DrivesBody() is Control drives)Band(L.T("Drives"),drives);
        var down=R("down","Network");var up=R("up","Network");var link=R("link","Network");
        if(down!=null||up!=null)Band(L.T("Network"),Ui.Columns("2*,1*",HistoryPlot(down??up!,64,up!=null&&down!=null?"mirror":"area",Cyan,down!=null?up:null),Stack(12,Rs("Network").Where(r=>r!=link).Select(r=>FitLeft(V(r,13,r==down?Cyan:Amber))).ToArray())));
        if(link!=null)RefSection(Ui.Columns("*,Auto",Text((++index).ToString("00")+"   "+L.T("Link speed"),13),V(link,16)),Cyan);
    }
    private void ReferenceIsometric()=>BuildIsometricCard();
    private void ReferenceSculpted()
    {
        bool pop=style==WidgetStyle.AioPopConsole,carbon=style==WidgetStyle.AioCarbonPulse;
        var cpu=Rs("CPU");if(cpu.Length>0)
        {
            var body=Stack(4);if(!pop)body.Children.Add(Header("CPU",snapshot.Cpu?.Name));var temp=R("temp");var load=R("load");
            var details=Stack(5,cpu.Where(r=>r!=temp&&r!=load).Select(r=>RefRow(r)).ToArray());
            Control lead=temp==null?new Border():MainTemp(temp,66,pop?Colors.White:carbon?ink:Cyan);
            if(carbon)
            {
                if(load!=null)details.Children.Insert(0,RefRow(load));
                // Narrow sidebar: the history goes below, so the detail labels keep a readable width.
                if(settings.Width<340){body.Children.Add(Cols(2,lead,details));if(temp!=null)body.Children.Add(HistoryPlot(temp,48));}
                else body.Children.Add(Cols(3,lead,temp==null?new Border():HistoryPlot(temp,68),details));
            }
            else
            {
                if(pop)lead=new AioReferenceSurface{Alpha=cardAlpha/255d,Mode="pop",Dark=!lightSurface,Tint=C("#253CFE"),Padding=new Thickness(10,12),Child=Stack(0,Text("CPU",16,Colors.White),lead)};
                Control side=load==null?details:settings.Width<340?Stack(6,Gauge(load,83,L.T("Load")),details):FixedCols(2,Gauge(load,83,L.T("Load")),details);
                if(pop)side=new AioReferenceSurface{Alpha=cardAlpha/255d,Mode="pop",Dark=!lightSurface,Tint=C("#DAFF14"),Padding=new Thickness(10,14),Child=side};
                if(load!=null)referenceUsed.Add(load.Key);body.Children.Add(new StatPanel(new[]{1d,1.8d},new[]{lead,side}){MaxColumns=2,MinimumColumnWidth=110,RowSpacing=6});
            }
            RefSection(body,pop?C("#F2F2EF"):Cyan);
        }
        foreach(var group in Rs("GPU").GroupBy(r=>r.Device))
        {
            var rows=group.ToArray();var temp=rows.FirstOrDefault(r=>r.Key.EndsWith(".temp"));var load=rows.FirstOrDefault(r=>r.Key.EndsWith(".load"));
            var body=Stack(7);if(!pop)body.Children.Add(Header("GPU",snapshot.Gpus.FirstOrDefault(g=>g.Id==group.Key)?.Name));
            var specials=Stack(8,rows.Where(r=>r.Key.EndsWith(".hotspot")||r.Key.EndsWith(".junction")).Select(r=>(Control)new Border{Background=Palette.Brush(SeeThrough(C("#26243A"))),CornerRadius=new CornerRadius(14),BorderBrush=Palette.Brush(Palette.Mix(Accent(r),back,.5)),BorderThickness=new Thickness(1),Padding=new Thickness(9,4),Child=Ui.Columns("*,Auto",Text(L.T(r.Label),11,C("#D3CBE8")),V(r,18,Amber))}).ToArray());
            Control main=temp==null?new Border():MainTemp(temp,68,pop?(lightSurface?C("#17121C"):C("#FFB27A")):carbon?Purple:C("#DA62EF"));
            if(pop)main=new AioReferenceSurface{Alpha=cardAlpha/255d,Mode="pop",Dark=!lightSurface,Tint=C("#FF8B2E"),Padding=new Thickness(9,8),Child=Stack(0,Text("GPU",16,ink),main)};
            if(carbon&&load!=null)
            {
                referenceUsed.Add(load.Key);body.Children.Add(Cols(2,Gauge(load,159),Stack(6,main,specials)));
            }
            else
            {
                body.Children.Add(Cols(2,main,specials));
                if(load!=null)body.Children.Add(Ui.Columns("Auto,*",Stack(1,Text(L.T("Load"),12),V(load,30)),HistoryPlot(load,55,"area",pop?C("#7424DC"):C("#D94DED"))));
            }
            var stats=rows.Where(r=>!new[]{"temp","load","hotspot","junction"}.Any(k=>r.Key.EndsWith("."+k))).Select(r=>(Control)new Border{Padding=new Thickness(0,0,5,0),Child=Stack(3,FitLeft(Text(L.T(r.Label),10,muted)),Fit(V(r,14)))}).ToArray();body.Children.Add(Cols(4,stats));
            RefSection(body,pop?C("#D5C0F4"):Purple);
        }
        ReferenceCooling(!carbon);ReferenceRails();ReferenceSummary();
    }
}
