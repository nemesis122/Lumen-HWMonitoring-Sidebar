using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Lumen.Core;
using Edition=Lumen.Core.CaseEditionDesigns.Arrangement;

namespace Lumen.App;

/// <summary>Native sensor cards over a clean chassis photograph. Placement never hides selected cards.</summary>
public sealed class CaseEditionPanel : Panel
{
    public sealed record Widget(Control Control,string Component,int Instance,bool Integrated);
    public sealed record Connector(string Component,int Instance,Point Target,IReadOnlyList<Point> Points);
    private readonly List<Widget> widgets=new();
    private readonly List<Connector> connectors=new();
    private readonly Bitmap portrait,landscape;
    private readonly Color accent;
    private readonly Surface surface;
    public CaseEditionDesigns.Look Look {get;}
    public int Columns {get;}
    public bool WideMode {get;}
    public IReadOnlyList<Widget> Widgets=>widgets;
    public IReadOnlyList<Connector> Connectors=>connectors;
    public Control? Caption {get;private set;}
    public Rect ArtworkBounds {get;private set;}
    public CaseEditionPanel(CaseEditionDesigns.Look look,Bitmap portrait,Bitmap landscape,int columns,bool wide,Color accent)
    {
        Look=look;this.portrait=portrait;this.landscape=landscape;Columns=Math.Clamp(columns,1,6);WideMode=wide;this.accent=accent;
        ClipToBounds=true;surface=new(this);Children.Add(surface);
    }
    public void SetCaption(Control caption){Caption=caption;Children.Add(caption);}
    public void AddWidget(Control control,string component,int instance,bool integrated)
    {widgets.Add(new(control,component,instance,integrated));Children.Add(control);}
    protected override Size MeasureOverride(Size availableSize)
    {
        double width=double.IsFinite(availableSize.Width)?Math.Max(1,availableSize.Width):600;
        surface.Measure(availableSize);Caption?.Measure(new Size(Math.Max(1,width-24),20));
        double height=WideMode?780:Math.Clamp(widgets.Count*140,240,1050);
        for(int i=0;i<24;i++){double needed=Place(width,height,false);if(needed<=height+.1)break;height=needed;}
        return new Size(width,height);
    }
    private double Place(double width,double height,bool arrange)
    {
        double pad=12,gap=14,top=Caption==null?pad:38,bottom=top;
        var placed=new HashSet<Widget>();
        double Put(Widget w,double x,double y,double size)
        {
            size=Math.Max(1,size);if(!arrange)w.Control.Measure(new Size(size,double.PositiveInfinity));
            double h=w.Control.DesiredSize.Height;if(arrange)w.Control.Arrange(new Rect(x,y,size,h));
            placed.Add(w);bottom=Math.Max(bottom,y+h);return y+h+gap;
        }
        double List(IEnumerable<Widget> list,double x,double y,double size)
        {foreach(var w in list)y=Put(w,x,y,size);return y;}
        IEnumerable<Widget> Group(params string[] kinds)=>widgets.Where(w=>kinds.Contains(w.Component));
        double Pair(IEnumerable<Widget> items,double y,int columns)
        {
            var heights=Enumerable.Repeat(y,columns).ToArray();double size=(width-pad*2-gap*(columns-1))/columns;
            foreach(var w in items){int at=Array.IndexOf(heights,heights.Min());heights[at]=Put(w,pad+at*(size+gap),heights[at],size);}
            return heights.Max();
        }
        double Footer(IEnumerable<Widget> items,double y,int columns)
        {
            var list=items.ToArray();if(list.Length==0)return y;
            double size=(width-pad*2-gap*(columns-1))/columns;
            var heights=new double[columns];
            foreach(var w in list){if(!arrange)w.Control.Measure(new Size(size,double.PositiveInfinity));int at=Array.IndexOf(heights,heights.Min());heights[at]+=w.Control.DesiredSize.Height+gap;}
            return Pair(list,Math.Max(y,height-pad-(heights.Max()-gap)),columns);
        }
        bool command=WideMode&&Look.Layout==Edition.Command&&width>=850;
        if(command)
        {
            double left=List(Group("cpu","board"),pad,top,width*.23-pad);
            double fans=List(Group("fan"),width*.28,top,width*.43);
            double ram=List(Group("ram"),width*.56,Math.Max(fans,top+height*.18),width*.20);
            double disks=List(Group("ssd"),width*.80,top,width*.20-pad);
            double gpu=List(Group("gpu"),width*.17,Math.Max(Math.Max(left,ram),height*.62),width*.59);
            Footer(Group("power","net"),Math.Max(gpu,disks),2);
        }
        else if(WideMode)
        {
            double y=List(Group("fan"),pad,top,width-2*pad);
            int cols=Math.Clamp(Columns,2,6);
            Pair(widgets.Where(w=>!placed.Contains(w)&&w.Component is not ("power" or "net")),y,cols);
            Footer(Group("power","net"),bottom+gap,2);
        }
        else if(Look.Layout==Edition.Studio)
        {
            double y=Pair(Group("fan","ram"),top,2);
            y=Pair(Group("cpu","gpu"),y,2);
            y=Pair(Group("ssd"),y,2);
            Footer(Group("board","power","net"),y,width>=500?3:2);
        }
        else
        {
            double y=top,half=(width-pad*2-gap)/2;
            if(Look.Layout==Edition.Intel||Look.Layout==Edition.Command)
            {
                y=List(Group("fan","cpu"),pad,y,width-pad*2);
                double left=List(Group("gpu","board"),pad,y,half);
                double right=List(Group("ram","ssd"),pad+half+gap,y,half);y=Math.Max(left,right);
            }
            else if(Look.Layout==Edition.Nvidia)
            {
                double left=List(Group("fan","cpu","board"),pad,y,half);
                double right=List(Group("ram","gpu"),pad+half+gap,y,half);
                y=Pair(Group("ssd"),Math.Max(left,right),2);
            }
            else
            {
                y=List(Group("fan"),pad,y,width-pad*2);
                double left=List(Group("cpu","gpu","board"),pad,y,half);
                double right=List(Group("ram","ssd"),pad+half+gap,y,half);y=Math.Max(left,right);
            }
            Footer(Group("power","net"),y,2);
        }
        // Additional devices and future widget groups remain visible, including on narrow screens.
        Pair(widgets.Where(w=>!placed.Contains(w)).ToArray(),bottom+gap,WideMode?Math.Clamp(Columns,2,6):2);
        if(arrange&&Caption!=null)Caption.Arrange(new Rect(pad,10,width-pad*2,20));
        return bottom+pad;
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        Place(finalSize.Width,finalSize.Height,true);surface.Arrange(new Rect(finalSize));
        var bitmap=WideMode||Look.Layout==Edition.Command?landscape:portrait;
        double scale=Math.Max(finalSize.Width/bitmap.PixelSize.Width,finalSize.Height/bitmap.PixelSize.Height);
        var size=new Size(bitmap.PixelSize.Width*scale,bitmap.PixelSize.Height*scale);
        ArtworkBounds=new Rect((finalSize.Width-size.Width)/2,(finalSize.Height-size.Height)/2,size.Width,size.Height);
        connectors.Clear();var obstacles=widgets.Select(w=>w.Control.Bounds).ToList();if(Caption!=null)obstacles.Add(Caption.Bounds);
        foreach(var w in widgets)
        {
            if(w.Component is "net" or "power"||w.Component=="ssd"&&w.Instance>1||w.Component=="gpu"&&w.Instance>0&&!w.Integrated)continue;
            string component=w.Integrated?"cpu":w.Component;bool wide=WideMode||Look.Layout==Edition.Command;
            var uv=component switch
            {
                "cpu"=>wide?(.40,.44):(.46,.40),"gpu"=>wide?(.48,.68):(.50,.58),"ram"=>wide?(.51,.43):(.65,.39),
                "fan"=>wide?(.49,.19):(.52,.14),"ssd"=>wide?(w.Instance==0?(.68,.51):(.68,.58)):(w.Instance==0?(.42,.72):(.46,.51)),
                _=>wide?(.34,.55):(.40,.50)
            };
            var target=new Point(ArtworkBounds.X+uv.Item1*ArtworkBounds.Width,ArtworkBounds.Y+uv.Item2*ArtworkBounds.Height);
            var spread=component switch{"cpu"=>(.045,.035),"gpu"=>(.18,.025),"ram"=>(.045,.10),"ssd"=>(.075,.018),"fan"=>(.15,.025),_=>(.04,.04)};
            var origin=target;
            var visible=(from dx in Enumerable.Range(-4,9) from dy in Enumerable.Range(-4,9)
                let point=new Point(origin.X+dx/4d*spread.Item1*ArtworkBounds.Width,origin.Y+dy/4d*spread.Item2*ArtworkBounds.Height)
                where new Rect(finalSize).Contains(point)&&!obstacles.Any(r=>r.Inflate(3).Contains(point))
                orderby Math.Abs(point.X-origin.X)+Math.Abs(point.Y-origin.Y) select point).ToArray();
            if(visible.Length==0)continue;target=visible[0];
            var r=w.Control.Bounds;var start=new Point(target.X>=r.Center.X?r.Right:r.Left,Math.Clamp(target.Y,r.Top+5,r.Bottom-5));
            if(target.Y<r.Top)start=new Point(Math.Clamp(target.X,r.Left+5,r.Right-5),r.Top);
            else if(target.Y>r.Bottom)start=new Point(Math.Clamp(target.X,r.Left+5,r.Right-5),r.Bottom);
            var points=CaseConnectorRouter.Route(start,target,obstacles,new Rect(finalSize));
            if(points.Count>1)connectors.Add(new(w.Component,w.Instance,target,points));
        }
        surface.InvalidateVisual();return finalSize;
    }
    private void Draw(DrawingContext context)
    {
        context.DrawRectangle(Brushes.Black,null,new Rect(Bounds.Size));
        var bitmap=WideMode||Look.Layout==Edition.Command?landscape:portrait;
        context.DrawImage(bitmap,new Rect(bitmap.Size),ArtworkBounds);
        var pen=new Pen(Palette.Brush(accent),1.2);
        foreach(var c in connectors){for(int i=1;i<c.Points.Count;i++)context.DrawLine(pen,c.Points[i-1],c.Points[i]);context.DrawEllipse(Palette.Brush(accent),null,c.Target,2,2);}
    }
    private sealed class Surface(CaseEditionPanel owner):Control
    {public override void Render(DrawingContext context)=>owner.Draw(context);}
}
