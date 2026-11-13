using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System.Globalization;
using Lumen.Core;

namespace Lumen.App;

public sealed partial class AioTile
{
    private void IsometricHeader()
    {
        CornerRadius=new CornerRadius(10);
        var caption=Text(string.IsNullOrWhiteSpace(slot?.Name)?"":slot.Name,20,ink);caption.FontWeight=FontWeight.Bold;
        // No "● Live" badge and no settings button: the sidebar already has its own settings at the top (user request).
        var header=Stack(2);
        if(caption.Text!.Length>0)header.Children.Add(caption);
        if(settings.ShowNames&&slot is {ShowHardware:true,HardwareName.Length:>0})header.Children.Add(Text(slot.HardwareName,12,muted));
        if(header.Children.Count>0){content.Children.Add(new Border{Padding=new Thickness(9,8),Background=IsometricGlass(),Child=header,Tag="aio-header"});content.Children.Add(Divider());}
    }
    private void BuildIsometricCard()
    {
        if(Math.Max(settings.ActiveWidget?.Labels??1,Math.Max(settings.ActiveWidget?.Hero??1,settings.ActiveWidget?.Values??1))>1.15)
        {
            // At large text sizes, put the callouts into normal document flow.
            RefSection(Fit(new AioIsometricArtwork(isometricArt.Value){Width=334,Height=500,FanCount=0,ShowCpu=Rs("CPU").Length>0,ShowGpu=Rs("GPU").Length>0,ShowRam=R("ram","System")!=null,ShowDisk=Rs("System").Any(r=>!r.Key.EndsWith("ram"))}),Cyan);
            foreach(var group in referenceRows.Where(r=>r.Group is "CPU" or "GPU").GroupBy(r=>r.Group+":"+r.Device))
                RefSection(Stack(7,new[]{Text(group.First().Group,18)}.Cast<Control>().Concat(group.Select(r=>(Control)Ui.Columns("*,Auto",Text(RefLabel(r),13),V(r,r.Key.EndsWith(".temp")?20:15)))).ToArray()),Accent(group.First()));
            ReferenceCooling(true);IsometricRails();
            var systems=SystemWithoutDrives();if(systems.Length>0)RefSection(Cols(2,systems.Select(r=>(Control)Stack(4,Text(r.Label,13),V(r,20,Accent(r)))).ToArray()),Cyan);
            ReferenceDrives();if(Rs("Network").Length>0)RefSection(ReferenceNetwork(),Cyan,true);return;
        }
        var fans=Rs("Cooling");
        var canvas=new Canvas{Width=334,Height=fans.Length==0?500:660+Math.Max(0,(fans.Length-1)/5)*112};
        canvas.Children.Add(new AioIsometricConnections{Width=334,Height=590,FanCount=fans.Length,ShowCpu=Rs("CPU").Length>0,ShowGpu=Rs("GPU").Length>0,ShowRam=R("ram","System")!=null,ShowDisk=Rs("System").Any(r=>!r.Key.EndsWith("ram"))});
        canvas.Children.Add(new AioIsometricArtwork(isometricArt.Value){Width=334,Height=590,FanCount=fans.Length,ShowCpu=Rs("CPU").Length>0,ShowGpu=Rs("GPU").Length>0,ShowRam=R("ram","System")!=null,ShowDisk=Rs("System").Any(r=>!r.Key.EndsWith("ram"))});

        void Box(IEnumerable<AioReading> source,double x,double y,double width,string caption,Color edge)
        {
            var rows=source.ToArray();if(rows.Length==0)return;
            var title=Text(caption,18,ink);title.FontWeight=FontWeight.Bold;
            var cells=rows.Select(r=>
            {
                Color color=r.Key.EndsWith(".temp")||r.Key.EndsWith("ram")?Cyan:r.Group=="System"?Palette.Mix(Amber,ink,.55):ink;
                var number=V(r,r.Key.EndsWith(".temp")||rows.Length==1?20:15,color);
                number.FontWeight=FontWeight.Bold;return Fit(number);
            }).ToArray();
            var face=Stack(3,title,FixedCols(rows.Length==1?1:2,cells));
            var box=new Border{Width=width,Padding=rows.Length==1?new Thickness(6,4):new Thickness(9,6),Background=IsometricGlass(),BorderBrush=Palette.Brush(edge),BorderThickness=new Thickness(1.2),CornerRadius=new CornerRadius(6),Child=face};
            Place(box,x,y);ToolTip.SetTip(box,caption);
        }
        void Place(Control c,double x,double y){Canvas.SetLeft(c,x);Canvas.SetTop(c,y);canvas.Children.Add(c);}
        bool Canonical(AioReading r)=>new[]{"temp","load","voltage","power"}.Any(s=>r.Key.EndsWith("."+s));
        Box(Rs("CPU").Where(Canonical),190,25,142,"CPU",C("#3A4651"));
        var gpu=Rs("GPU");string? primary=gpu.FirstOrDefault()?.Device;
        Box(gpu.Where(r=>r.Device==primary&&Canonical(r)),190,145,142,"GPU",C("#3A4651"));
        int badgeIndex=0;
        foreach(var r in gpu.Where(r=>r.Device==primary&&(r.Key.EndsWith(".hotspot")||r.Key.EndsWith(".junction"))))
        {
            var text=Text(RefLabel(r),14,C("#11131C"));text.FontWeight=FontWeight.Bold;
            var value=V(r,18,C("#11131C"));value.FontWeight=FontWeight.Bold;
            var line=Ui.Columns("*,Auto",FitLeft(text),value);line.ColumnSpacing=5;
            var badge=new Border{Width=130,Padding=new Thickness(6,3),CornerRadius=new CornerRadius(4),Background=Palette.Brush(r.Key.EndsWith(".hotspot")?Coral:Purple),Child=line};
            Place(badge,202,244+badgeIndex++*40);
        }
        Box(Rs("System").Where(r=>r.Key.EndsWith("ram")),258,344,74,"RAM",Palette.Mix(Cyan,back,.6));
        var firstDisk=Rs("System").FirstOrDefault(r=>!r.Key.EndsWith("ram"));if(firstDisk!=null)diagramDisks.Add(firstDisk.Key);
        Box(firstDisk==null?Array.Empty<AioReading>():new[]{firstDisk},258,411,74,firstDisk?.Label??"SSD",Palette.Mix(Amber,back,.55));
        for(int i=0;i<fans.Length;i++)
        {
            var fan=fans[i];double x=i%5*66.8,y=590+i/5*112;
            if(i>=5)Place(new AioReferenceFan{Color=ColorsFor(i),Width=44,Height=44},x+11,y-47);
            var name=Text(fan.Label.Replace("GPU ","GPU"),12,muted);name.TextAlignment=TextAlignment.Center;
            var value=V(fan,15,ink);value.FontWeight=FontWeight.Bold;value.TextAlignment=TextAlignment.Center;
            formats[value]=r=>r.Number?.ToString("N0",L.Language=="de"?CultureInfo.GetCultureInfo("de-DE"):L.Culture)??"—";value.Text=formats[value](fan);
            var unit=Text("rpm",12,muted);unit.TextAlignment=TextAlignment.Center;
            var label=Stack(3,name,value,unit);label.Width=66;Place(label,x,y);
        }
        RefSection(Fit(canvas),Cyan);
        IsometricRails();
        ReferenceDrives();
        var rates=Rs("Network");
        if(rates.Length>0)
        {
            var icon=Ui.Glyph("M8,2 a2,2 0 1 0 .01,0 M3,11 a2,2 0 1 0 .01,0 M13,11 a2,2 0 1 0 .01,0 M7,6 L4,11 M9,6 L12,11 M5,13 H11",ink,23);
            // Heading on its own line, the rates below at full size (no shrinking on a narrow sidebar).
            var heading=Ui.Columns("Auto,*",icon,Text(L.T("Network"),13,muted));heading.ColumnSpacing=8;
            var rows=Cols(2,rates.Where(r=>!r.Key.EndsWith(".link")).Select(r=>
            {
                var arrow=Text(r.Key.EndsWith("down")?"↓":"↑",20,r.Key.EndsWith("down")?Cyan:Amber);
                var row=Ui.Columns("Auto,*",arrow,FitLeft(V(r,15,ink)));row.ColumnSpacing=3;return (Control)row;
            }).ToArray());
            var link=rates.FirstOrDefault(r=>r.Key.EndsWith(".link"));
            RefSection(link==null?Stack(6,heading,rows):Stack(6,heading,rows,NetLine(link,20,13,ink)),Cyan,true);
        }
    }
    private IBrush IsometricGlass()=>new LinearGradientBrush{StartPoint=new RelativePoint(0,0,RelativeUnit.Relative),EndPoint=new RelativePoint(1,1,RelativeUnit.Relative),GradientStops={new(SeeThrough(Palette.Mix(back,ink,.07)),0),new(SeeThrough(back),1)}};
    private void IsometricRails()
    {
        var rails=Rs("Rails");if(rails.Length==0)return;
        var badges=rails.Select((r,i)=>
        {
            Color tint=new[]{Cyan,Purple,Amber}[i%3];
            var body=Ui.Columns("Auto,*",Ui.Glyph(Glyphs.Power,tint,18),Fit(V(r,18,ink)));body.ColumnSpacing=4;
            return (Control)new Border{Margin=new Thickness(0,0,5,0),Padding=new Thickness(5,3),Background=IsometricGlass(),BorderBrush=Palette.Brush(Palette.Mix(tint,track,.75)),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(5),Child=body};
        }).ToArray();
        RefSection(Stack(10,Text(L.Language=="de"?"Versorgungsspannungen (Netzteil)":"Supply voltages (PSU)",13,muted),Cols(3,badges)),Cyan);
    }
}
