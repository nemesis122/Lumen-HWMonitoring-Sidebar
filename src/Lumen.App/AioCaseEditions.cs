using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Lumen.Core;
using Edition = Lumen.Core.CaseEditionDesigns.Arrangement;

namespace Lumen.App;

public sealed partial class AioTile
{
    private readonly List<(string Key,TextBlock Text)> editionValues=new();
    private static readonly Dictionary<string,WeakReference<Bitmap>> editionPictures=new();
    private Bitmap EditionPicture(string asset)
    {
        if(editionPictures.TryGetValue(asset,out var weak)&&weak.TryGetTarget(out var existing))return existing;
        using var stream=AssetLoader.Open(new Uri("avares://Lumen/Assets/Aio/Cases/"+asset+".png"));
        var bitmap=new Bitmap(stream);editionPictures[asset]=new(bitmap);return bitmap;
    }
    private TextBlock EditionText(string text,double size,Color color,bool title=false)
    {
        var block=Text(text,size,color);block.FontWeight=title?FontWeight.Bold:FontWeight.Normal;
        TextFonts.Mark(block,title?TextRole.Title:TextRole.Label);return block;
    }
    private TextBlock EditionValue(AioReading row,double size=13)
    {
        var value=V(row,size,Colors.White);TextFonts.Mark(value,TextRole.Value);
        if(value is AioValueText steady&&row.Key.StartsWith("aio.system.ram.")&&row.Value.EndsWith("GB"))
        {
            string current=steady.Text??"";steady.Text="888.8 GB";steady.Measure(Size.Infinity);steady.Text=current;
        }
        editionValues.Add((row.Key,value));return value;
    }
    private string EditionLabel(AioReading r)=>r.Key=="aio.system.ram"?L.T("Load")
        :r.Group=="Board"?L.T(r.Label)
        :r.Key.EndsWith(".temp")||r.Key.StartsWith("aio.system.disk.")?L.T("Temperature")
        :r.Key.EndsWith(".hotspot")?"Hotspot":r.Key.EndsWith(".junction")?"VRAM temp."
        :r.Group=="Cooling"?L.T(r.Label):r.Key.EndsWith(".read")?L.T("Read"):r.Key.EndsWith(".write")?L.T("Write")
        :r.Key.EndsWith(".voltage")?L.T("Voltage"):r.Key.EndsWith(".power")?L.T("Power")
        :r.Key.EndsWith(".clock")?L.T("Clock"):r.Key.EndsWith(".vram")?"VRAM":L.T(r.Label);
    private Control EditionLine(AioReading row)
    {
        var label=TextColours.Bind(EditionText(EditionLabel(row),11,C("#CED6DF")),row.Key);
        var line=Ui.Columns(".49*,.51*",label,FitRight(EditionValue(row)));line.ColumnSpacing=7;return line;
    }
    private Control EditionTile(AioReading row,Color accent,double size=19)
    {
        var label=TextColours.Bind(EditionText(EditionLabel(row),10,C("#BAC5D0")),row.Key);
        return new Border{Padding=new Thickness(7,5),CornerRadius=new CornerRadius(5),Background=Palette.Brush(SeeThrough(C("#18232D"))),
            Child=Stack(3,label,FitLeft(EditionValue(row,size)))};
    }
    private Control EditionTrace(AioReading row,Color accent,double height=25)
    {
        var plot=TextColours.Bind(new CaseSparkline(accent){Height=height,Tag="graphic"},row.Key);
        casePlots.Add((row.Key,plot));return plot;
    }
    private Control EditionBar(AioReading row,Color accent)
    {
        var bar=TextColours.Bind(new AioMeter(accent,C("#303943"),0){Height=6,Tag="graphic"},row.Key);
        meters.Add((row.Key,bar));return bar;
    }
    private Control EditionDial(AioReading row,Color accent,bool open=false,double ringScale=1.2)
    {
        double diameter=78*ringScale;
        var ring=TextColours.Bind(new AioRing(accent,C("#303943"),false,open){Width=74*ringScale,Height=74*ringScale},row.Key);
        rings.Add((row.Key,ring));
        var value=EditionValue(row,15);value.HorizontalAlignment=HorizontalAlignment.Center;
        var number=new Viewbox{Child=value,MaxWidth=70,Stretch=Stretch.Uniform,HorizontalAlignment=HorizontalAlignment.Center};
        var label=TextColours.Bind(EditionText(EditionLabel(row),10,C("#C8D2DC")),row.Key);label.HorizontalAlignment=HorizontalAlignment.Center;
        var dial=new Grid{Width=diameter,Height=diameter,Children={ring,new Border{Padding=new Thickness(3),VerticalAlignment=VerticalAlignment.Center,Child=number}}};
        var scalable=new Viewbox{Child=dial,Width=diameter,Height=diameter,Stretch=Stretch.Uniform,Tag="graphic"};
        return Stack(2,label,new Viewbox{Child=scalable,Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,MaxHeight=diameter*2});
    }
    private static Control EditionCells(IEnumerable<Control> items,int columns,double minimum)
    {
        var cells=items.ToArray();foreach(var cell in cells)cell.Margin=new Thickness(3);
        return new StatPanel(Array.Empty<double>(),cells){MaxColumns=columns,MinimumColumnWidth=minimum,RowSpacing=4,Balanced=true};
    }
    private Control EditionFrame(DashboardPart part,Control body,CaseEditionDesigns.Look look,Color accent,int instance)
    {
        var stack=Stack(6);if(part.Title.Length>0)
        {
            var title=EditionText(part.Title,14,look.Layout==Edition.Command?accent:Colors.White,true);
            ToolTip.SetTip(title,part.Model);stack.Children.Add(title);
        }
        var kind=EditionKind(part.Id);var owner=source.Widgets.FirstOrDefault(w=>w.Kind==kind);
        if(part.Model.Length>0 && settings.ShowNames && slot?.ShowHardware!=false && owner?.ShowHardware!=false)
        {
            var model=EditionText(part.Model,9,C("#9FB0BE"));model.TextWrapping=TextWrapping.Wrap;ToolTip.SetTip(model,part.Model);stack.Children.Add(model);
        }
        stack.Children.Add(body);
        var frame=new Border{Child=stack,Padding=new Thickness(10),CornerRadius=new CornerRadius(look.Layout==Edition.Nvidia?10:look.Layout==Edition.Intel?5:7),
            Background=Palette.Brush(SeeThrough(C(look.Layout==Edition.Intel?"#FA071724":"#FA0C141C"))),
            BorderBrush=Palette.Brush(Palette.Mix(accent,C("#34414C"),look.Layout==Edition.Nvidia?.15:.45)),BorderThickness=new Thickness(1),
            Tag="case-widget:"+part.Id,Name="edition-"+part.Id+"-"+instance};
        if(look.Layout is Edition.Amd or Edition.Intel)
        {
            frame.Child=null;
            var grid=new Grid{Children={stack,new Border{Background=Palette.Brush(accent),Width=look.Layout==Edition.Amd?4:double.NaN,Height=look.Layout==Edition.Intel?3:double.NaN,
                HorizontalAlignment=look.Layout==Edition.Amd?HorizontalAlignment.Left:HorizontalAlignment.Stretch,VerticalAlignment=look.Layout==Edition.Intel?VerticalAlignment.Top:VerticalAlignment.Stretch,
                CornerRadius=new CornerRadius(2),Margin=new Thickness(-10)}}};frame.Child=grid;
        }
        return frame;
    }
    private static WidgetKind EditionKind(string part)=>part switch{"cpu"=>WidgetKind.Cpu,"gpu"=>WidgetKind.Gpu,"ram"=>WidgetKind.Memory,"ssd"=>WidgetKind.Drives,"fan" or "board"=>WidgetKind.Cooling,"net"=>WidgetKind.Network,_=>WidgetKind.Power};
    private List<DashboardPart> EditionParts(List<AioReading> rows)
    {
        var parts=DashboardParts(rows);var cooling=parts.FirstOrDefault(p=>p.Id=="fan");
        if(cooling!=null)
        {
            parts.Remove(cooling);var board=cooling.Rows.Where(r=>r.Group=="Board").ToList();
            if(board.Count>0)parts.Add(cooling with{Id="board",Title=Naming.Title(source.Widgets.FirstOrDefault(w=>w.Kind==WidgetKind.Cooling),0,L.T("Mainboard")),Rows=board});
            var fans=cooling.Rows.Where(r=>r.Group!="Board").ToList();if(fans.Count>0)parts.Add(cooling with{Title=cooling.Title.Length==0?"":L.T("Fans"),Rows=fans});
        }
        var result=new List<DashboardPart>();
        foreach(var part in parts)
        {
            if(part.Id is not ("gpu" or "ssd")){result.Add(part);continue;}
            var groups=part.Rows.GroupBy(r=>r.Device).ToArray();int ordinal=0;
            foreach(var group in groups)
            {
                int index=part.Id=="gpu"?snapshot.Gpus.ToList().FindIndex(g=>g.Id==group.Key):snapshot.Disks.ToList().FindIndex(d=>d.Id==group.Key);index=Math.Max(0,index);
                string model=part.Id=="gpu"?snapshot.Gpus.FirstOrDefault(g=>g.Id==group.Key)?.Name??"":snapshot.Disks.FirstOrDefault(d=>d.Id==group.Key)?.Name??"";
                var owner=source.Widgets.FirstOrDefault(w=>w.Kind==EditionKind(part.Id));
                string fallback=part.Id=="ssd"?"SSD "+(++ordinal):groups.Length>1?"GPU "+(++ordinal):"GPU";
                result.Add(part with{Title=owner?.ShowTitle==false?"":Naming.Title(owner,index,fallback),Model=PartModel(EditionKind(part.Id),index,model),Rows=group.ToList()});
            }
        }
        return result;
    }
    private Control EditionBody(DashboardPart part,CaseEditionDesigns.Look look,Color accent)
    {
        var rows=part.Rows.Where(r=>r.Group!="Cores").ToList();var used=new HashSet<string>();var body=Stack(5);
        bool plots=settings.Shows("aio.graphs");
        if(part.Id=="fan")
        {
            if(look.Layout==Edition.Studio)return EditionCells(rows.Select(r=>EditionDial(r,accent)),4,90);
            return EditionCells(rows.Select(r=>EditionTile(r,accent,17)),6,95);
        }
        if(part.Id=="ram")
        {
            var load=rows.FirstOrDefault(r=>r.Key=="aio.system.ram");
            if(look.Layout==Edition.Nvidia && load!=null)
            {used.Add(load.Key);body.Children.Add(Ui.Columns(".45*,.55*",EditionDial(load,accent),Stack(4,rows.Where(r=>r!=load).Select(EditionLine).ToArray())));foreach(var row in rows)used.Add(row.Key);}
            else if(look.Layout==Edition.Intel)
            {body.Children.Add(FixedCols(2,rows.Select(r=>EditionTile(r,accent,18)).ToArray()));foreach(var row in rows)used.Add(row.Key);}
            else if(load!=null){body.Children.Add(EditionBar(load,accent));}
        }
        if(part.Id=="cpu" && look.Layout==Edition.Intel)
        {
            var heads=rows.Where(r=>r.Key is "aio.cpu.temp" or "aio.cpu.load").ToArray();
            if(heads.Length>0){body.Children.Add(FixedCols(heads.Length,heads.Select(r=>EditionTile(r,accent,26)).ToArray()));foreach(var r in heads)used.Add(r.Key);}
        }
        if(part.Id=="cpu" && look.Layout==Edition.Studio)
        {
            var heads=rows.Where(r=>r.Key.EndsWith(".temp")||r.Key.EndsWith(".load")||r.Key.EndsWith(".clock")||r.Key.EndsWith(".power")).ToArray();
            body.Children.Add(EditionCells(heads.Select(r=>EditionTile(r,accent,20)),4,75));foreach(var r in heads)used.Add(r.Key);
        }
        if(part.Id=="gpu" && look.Layout==Edition.Command)
        {body.Children.Add(EditionCells(rows.Select(r=>EditionTile(r,accent,18)),9,84));foreach(var r in rows)used.Add(r.Key);}
        if(part.Id=="gpu" && look.Layout==Edition.Nvidia)
        {
            var trendRows=plots?rows.Where(r=>r.Key.EndsWith(".temp")||r.Key.EndsWith(".load")||r.Key.EndsWith(".power")).ToArray():Array.Empty<AioReading>();
            var stats=Stack(4,rows.Except(trendRows).Select(EditionLine).ToArray());var trends=Stack(8);
            foreach(var r in trendRows)
                trends.Children.Add(Stack(2,EditionText(EditionLabel(r),10,C("#BDCBD5")),FitRight(EditionValue(r,14)),EditionTrace(r,accent,20)));
            if(trends.Children.Count>0)body.Children.Add(new DashboardColumns(stats,trends));else body.Children.Add(stats);
            foreach(var row in rows)used.Add(row.Key);
        }
        else if(part.Id=="gpu" && look.Layout is Edition.Amd or Edition.Studio)
        {
            var temp=rows.FirstOrDefault(r=>r.Key.EndsWith(".temp"));
            if(temp!=null)
            {
                body.Children.Add(EditionTile(temp,accent,30));used.Add(temp.Key);
                if(plots)body.Children.Add(EditionTrace(temp,accent,28));
            }
        }
        if(part.Id=="power" && look.Layout==Edition.Command)
        {body.Children.Add(EditionCells(rows.Select(r=>EditionDial(r,accent,true,1.25)),3,85));foreach(var r in rows)used.Add(r.Key);}
        if(part.Id=="net" && look.Layout is Edition.Intel or Edition.Command)
        {
            var rates=rows.Where(r=>r.Key is "aio.network.down" or "aio.network.up").ToArray();
            body.Children.Add(FixedCols(Math.Max(1,rates.Length),rates.Select(r=>EditionTile(r,accent,19)).ToArray()));foreach(var r in rates)used.Add(r.Key);
        }
        foreach(var row in rows.Where(r=>!used.Contains(r.Key)))body.Children.Add(EditionLine(row));
        var cores=part.Rows.Where(r=>r.Group=="Cores").ToArray();if(cores.Length>0)body.Children.Add(FixedCols(2,cores.Select(EditionLine).ToArray()));
        if(part.Id=="cpu" && lastCoreLoads.Count>=2 && AioReadings.Selected(settings,"aio.cpu.coreloads"))
        {
            coreBars=new AioCoreBars(Colors.White,C("#BFCCD6"),C("#29343F")){Loads=lastCoreLoads,Tint=accent,Compact=true,OneColour=true,Heatmap=true,Tag="graphic"};
            TextColours.Bind(coreBars,"aio.cpu.coreloads");
            var coreContent=Stack(4,EditionText(L.T("Cores")+" ("+lastCoreLoads.Count+")",10,C("#BDCBD5")),coreBars);
            if(look.Layout==Edition.Intel)return new DashboardColumns(body,coreContent);
            body.Children.Add(coreContent);
        }
        if(plots && part.Id=="cpu" && look.Layout is Edition.Amd or Edition.Studio && rows.FirstOrDefault(r=>r.Number.HasValue) is {} primary)
            body.Children.Insert(0,EditionTrace(primary,accent,28));
        if(plots && part.Id=="net" && rows.FirstOrDefault(r=>r.Key=="aio.network.down") is {} down)body.Children.Add(EditionTrace(down,accent,24));
        return body;
    }
    private void BuildCaseEdition(List<AioReading> rows)
    {
        editionValues.Clear();casePlots.Clear();dashboardLimits.Clear();dashboardTraffic=null;coreBars=null;mainboardCores=null;DashboardHeightGrowth=0;
        var look=CaseEditionDesigns.For(style);var accent=Color.TryParse(slot?.Accent,out var own)?own:skin??C(look.Accent);
        Padding=new Thickness(0);BorderThickness=new Thickness(0);CornerRadius=new CornerRadius(5);content.Spacing=0;
        bool wide=source.FullScreen||source.Columns>1;
        var scene=new CaseEditionPanel(look,EditionPicture(look.Asset),EditionPicture(look.WideAsset),source.Columns,wide,accent);
        if(slot?.ShowTitle!=false)
        {
            string caption=slot is{Name.Length:>0}?slot.Name:look.Name.Replace("Case · ","").ToUpperInvariant();
            if(slot?.ShowHardware!=false&&!string.IsNullOrWhiteSpace(slot?.HardwareName))caption+=" · "+slot.HardwareName;
            scene.SetCaption(EditionText(caption,12,accent,true));
        }
        var instances=new Dictionary<string,int>();
        foreach(var part in EditionParts(rows))
        {
            int instance=instances.GetValueOrDefault(part.Id);instances[part.Id]=instance+1;
            bool integrated=part.Id=="gpu"&&part.Rows.Any(r=>snapshot.Gpus.Any(g=>g.Id==r.Device&&g.Integrated));
            scene.AddWidget(EditionFrame(part,EditionBody(part,look,accent),look,accent,instance),part.Id,instance,integrated);
        }
        dashboardLayout=scene;content.Children.Add(scene);
    }
    private void UpdateCaseEditionValues()
    {
        foreach(var (key,text) in editionValues)
        {
            var row=readings.GetValueOrDefault(key);bool temperature=key.EndsWith(".temp")||key.EndsWith(".hotspot")||key.EndsWith(".junction")||key.StartsWith("aio.system.disk.")||row?.Group=="Board";
            bool warnings=ColourRules.Resolve(source,slot,key,ColourRole.Value).AutomaticWarnings;
            text.Foreground=Palette.Brush(temperature&&warnings&&row?.Number>=settings.TemperatureWarning?C(row.Number>=settings.TemperatureWarning+10?"#EF4444":"#F59E0B"):Colors.White);
        }
    }
}
