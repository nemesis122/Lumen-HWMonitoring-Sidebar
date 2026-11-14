using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Lumen.Core;

namespace Lumen.App;

public sealed partial class AioTile
{
    internal static bool MainboardDesign(WidgetStyle value) => value is WidgetStyle.AioMainboard or WidgetStyle.AioMainboardMap or WidgetStyle.AioMainboardCores
        or WidgetStyle.AioMainboardStrips or WidgetStyle.AioMainboardGauges or WidgetStyle.AioMainboardMatrix
        or WidgetStyle.AioMainboardType or WidgetStyle.AioMainboardDesktop;

    private void MainboardVariant()
    {
        drivesPlaced=true;coreBars=null;mainboardCores=null;
        Padding=new Thickness(8);CornerRadius=new CornerRadius(8);content.Spacing=8;
        bool matrix=style==WidgetStyle.AioMainboardMatrix, gauges=style==WidgetStyle.AioMainboardGauges,
            type=style==WidgetStyle.AioMainboardType;
        var cyan=MainboardMapColors.Cyan;var violet=MainboardMapColors.Violet;
        var body=Stack(8);body.MaxWidth=1000;body.HorizontalAlignment=HorizontalAlignment.Center;
        body.Children.Add(new MainboardOverview(mapArtwork.Value,ink,back,
            Rs("CPU").Length>0,Rs("GPU").Length>0,R("ram","System")!=null,Rs("Rails").Length>0));

        string Caption(AioReading r)=>r.Key.StartsWith("aio.system.disk.")&&r.Key.EndsWith(".temp")?"Temp":r.Key.EndsWith(".junction")?"VRAM °C":r.Key.EndsWith(".power")?"Power":RefLabel(r);
        Control Row(AioReading r)
        {
            var label=Text(Caption(r),11,muted);label.Tag="map-label:"+r.Key;
            var row=Ui.Columns("*,1.5*",FitLeft(label),FitRight(V(r,13)));row.ColumnSpacing=6;return row;
        }
        Control Cells(IEnumerable<AioReading> readings,double size=13)=>new MainboardValueGrid(readings.Select(r=>
        {
            var label=Text(Caption(r),10,muted);label.Tag="map-label:"+r.Key;
            return (Control)Stack(2,FitLeft(label),FitLeft(V(r,size)));
        }),78);
        Control Section(string title,Color accent,Control child)
        {
            var heading=Text(title,13,accent);heading.FontWeight=FontWeight.SemiBold;
            return new Border{Tag="mainboard-section",Padding=new Thickness(9,8),
                BorderBrush=Palette.Brush(matrix?track:Palette.Mix(track,accent,.45)),
                BorderThickness=matrix?new Thickness(0,1,0,0):type?new Thickness(3,0,0,0):new Thickness(1),
                CornerRadius=new CornerRadius(gauges?10:2),Background=Palette.Brush(SeeThrough(Palette.Mix(back,accent,.035))),
                Child=Stack(matrix?4:7,heading,child)};
        }
        Control Processor(string title,AioReading[] rows,Color accent,bool reverse)
        {
            var temp=rows.FirstOrDefault(r=>r.Key.EndsWith(".temp"));
            var rest=rows.Where(r=>r!=temp).ToArray();
            if(matrix)return Section(title,accent,Stack(4,rows.Select(Row).ToArray()));
            if(gauges)
            {
                Control face=temp==null?new Border():GaugeFace(temp,accent);
                return Section(title,accent,Stack(7,face,Stack(5,rest.Select(Row).ToArray())));
            }
            var hero=temp==null?(Control)new Border():FitLeft(V(temp,type?52:26,accent));
            if(type)
            {
                var details=Stack(5,rest.Select(Row).ToArray());
                var layout=reverse?Ui.Columns("1.25*,*",details,hero):Ui.Columns("*,1.25*",hero,details);
                layout.ColumnSpacing=10;return Section(title,accent,layout);
            }
            var vram=rest.FirstOrDefault(r=>r.Key.EndsWith(".vram"));
            var detailsBand=vram==null?Cells(rest):Stack(7,Cells(rest.Where(r=>r!=vram)),Row(vram));
            var band=Ui.Columns("86,*",hero,detailsBand);band.ColumnSpacing=8;
            return Section(title,accent,band);
        }
        var processors=new List<Control>();
        if(Rs("CPU").Length>0)processors.Add(Processor("1 · CPU",Rs("CPU"),cyan,false));
        int device=0;
        foreach(var gpu in Rs("GPU").GroupBy(r=>r.Device))processors.Add(Processor("2 · GPU"+(device++==0?"":" "+device),gpu.ToArray(),violet,true));
        if(gauges)body.Children.Add(new MainboardValueGrid(processors,150,2));
        else foreach(var processor in processors)body.Children.Add(processor);

        var utilities=new List<Control>();
        if(R("ram","System") is {} ram)
        {
            Control percentage=gauges?GaugeFace(ram,cyan,true):FitLeft(V(ram,type?26:22,cyan));
            var capacity=Text(Amount(ram),12,ink,true);fields[ram.Key].Add(capacity);formats[capacity]=Amount;
            var row=Ui.Columns(gauges?"88,*":"Auto,*",percentage,Stack(5,FitRight(capacity),PartMeter(ram,cyan)));
            row.ColumnSpacing=10;utilities.Add(Section("3 · RAM",cyan,row));
        }
        if(Rs("Rails").Length>0)utilities.Add(Section("4 · PSU",violet,gauges?Stack(5,Rs("Rails").Select(Row).ToArray()):Cells(Rs("Rails"))));
        if(gauges)body.Children.Add(new MainboardValueGrid(utilities,150,2));
        else foreach(var utility in utilities)body.Children.Add(utility);

        foreach(var drive in PartDrives())
        {
            var values=new List<AioReading>();if(drive.Temp!=null)values.Add(drive.Temp);values.AddRange(drive.Rows);
            body.Children.Add(Section(AioReadings.DriveName(snapshot,drive.Id),cyan,Cells(values)));
        }
        if(Rs("Network").Length>0)body.Children.Add(Section("NETWORK",cyan,Cells(Rs("Network"),type?22:14)));
        if(lastCoreLoads.Count>=2&&AioReadings.Selected(settings,"aio.cpu.coreloads")&&Rs("CPU").Length>0)
        {
            mainboardCores=new MainboardCoreGrid(ink,muted,track,cyan){Loads=lastCoreLoads,TwoTone=true};
            body.Children.Add(Section("CORE LOAD · %",cyan,mainboardCores));
        }
        if(Rs("Cooling").Length>0)body.Children.Add(Section("FANS",violet,gauges
            ?FanGrid(Rs("Cooling"),30,true,true,true):Cells(Rs("Cooling"))));
        if(Rs("Cores").Length>0)body.Children.Add(Section("CORE TEMP",cyan,Cells(Rs("Cores"))));
        content.Children.Add(body);
    }

    private Control GaugeFace(AioReading r,Color accent,bool small=false)
    {
        double size=small?82:132;
        var ring=new AioRing(accent,track,false,!small){Width=size,Height=size};rings.Add((r.Key,ring));
        var face=new Grid{Height=size,MaxWidth=size,HorizontalAlignment=HorizontalAlignment.Center};face.Children.Add(ring);
        var value=Fit(V(r,small?20:27,accent));value.VerticalAlignment=VerticalAlignment.Center;
        value.Margin=new Thickness(small?14:18,small?0:8,small?14:18,0);face.Children.Add(value);return face;
    }
}

/// <summary>Natural-height cells wrap before their values become unreadable.</summary>
public sealed class MainboardValueGrid : Panel
{
    private readonly double cellWidth;
    private readonly int maxColumns;
    private int columns;
    private double[] heights=Array.Empty<double>();
    public MainboardValueGrid(IEnumerable<Control> controls,double cellWidth,int maxColumns=4)
    {this.cellWidth=cellWidth;this.maxColumns=maxColumns;foreach(var control in controls)Children.Add(control);}
    protected override Size MeasureOverride(Size available)
    {
        double width=double.IsFinite(available.Width)?available.Width:400;
        columns=Math.Max(1,Math.Min(Children.Count,Math.Min(maxColumns,(int)((width+8)/(cellWidth+8)))));
        double cell=Math.Max(0,(width-8*(columns-1))/columns);
        heights=new double[(Children.Count+columns-1)/columns];
        for(int i=0;i<Children.Count;i++){Children[i].Measure(new Size(cell,double.PositiveInfinity));heights[i/columns]=Math.Max(heights[i/columns],Children[i].DesiredSize.Height);}
        return new Size(width,heights.Sum()+Math.Max(0,heights.Length-1)*8);
    }
    protected override Size ArrangeOverride(Size final)
    {
        double cell=Math.Max(0,(final.Width-8*(columns-1))/columns),y=0;
        for(int row=0;row<heights.Length;row++)
        {
            for(int col=0;col<columns;col++){int i=row*columns+col;if(i<Children.Count)Children[i].Arrange(new Rect(col*(cell+8),y,cell,heights[row]));}
            y+=heights[row]+8;
        }
        return final;
    }
}

/// <summary>One top board, with contacts that match the numbered sensor headings.</summary>
public sealed class MainboardOverview : Control
{
    private readonly Bitmap image;
    private readonly Color ink,back;
    private readonly bool[] shown;
    public MainboardOverview(Bitmap image,Color ink,Color back,params bool[] shown)
    {this.image=image;this.ink=ink;this.back=back;this.shown=shown;Tag="mainboard-overview";}
    protected override Size MeasureOverride(Size available)=>new(double.IsFinite(available.Width)?available.Width:400,
        Math.Clamp((double.IsFinite(available.Width)?available.Width:400)*.72,170,330));
    public override void Render(DrawingContext context)
    {
        double size=Math.Min(Bounds.Height-30,Bounds.Width-62);
        var art=new Rect((Bounds.Width-size)/2,0,size,size);context.DrawImage(image,art);
        var rectangles=new[]{new Rect(.345,.18,.22,.23),new Rect(.07,.525,.67,.25),new Rect(.66,.055,.20,.42),new Rect(.90,.21,.06,.20)};
        var names=new[]{"1 CPU","2 GPU","3 RAM","4 ATX"};
        for(int i=0;i<4;i++)
        {
            if(!shown[i])continue;
            var accent=i is 1 or 3?MainboardMapColors.Violet:MainboardMapColors.Cyan;
            var brush=Palette.Brush(accent);var pen=new Pen(brush,2);
            var r=rectangles[i];var component=new Rect(art.X+r.X*size,r.Y*size,r.Width*size,r.Height*size);
            context.DrawRectangle(null,pen,component,2,2);
            bool left=i<2;double lane=left?art.Left-8-i*7:art.Right+8+(i-2)*7;
            var start=new Point(left?component.Left:component.Right,component.Center.Y);
            double terminal=(i+.5)*Bounds.Width/4,y=Bounds.Height-21;
            context.DrawLine(pen,start,new Point(lane,start.Y));context.DrawLine(pen,new Point(lane,start.Y),new Point(lane,y-7));
            context.DrawLine(pen,new Point(lane,y-7),new Point(terminal,y-7));context.DrawLine(pen,new Point(terminal,y-7),new Point(terminal,y));
            var text=new FormattedText(names[i],System.Globalization.CultureInfo.InvariantCulture,FlowDirection.LeftToRight,
                new Typeface(Ui.Font,FontStyle.Normal,FontWeight.SemiBold),11,brush);
            var label=new Rect(terminal-text.Width/2-4,y,text.Width+8,text.Height+3);
            context.DrawRectangle(Palette.Brush(back),new Pen(brush,1),label,3,3);context.DrawText(text,new Point(label.X+4,label.Y+1));
        }
    }
}
