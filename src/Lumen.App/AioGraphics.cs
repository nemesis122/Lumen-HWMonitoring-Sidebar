using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Lumen.App;

/// <summary>Vector-only decoration and meters. No sensor value is baked into these drawings.</summary>
public sealed class AioMeter : Control
{
    private readonly Color color,track; private readonly int mode; private double? level;private double[] samples=Array.Empty<double>();
    public AioMeter(Color color,Color track,int mode) {this.color=color;this.track=track;this.mode=mode;}
    public bool Historical=>mode==1;
    public void Set(double? value,double[]? series=null) {level=value;samples=series??Array.Empty<double>();InvalidateVisual();}
    public override void Render(DrawingContext c)
    {
        double w=Bounds.Width,h=Bounds.Height; if(w<=0||h<=0)return;
        if(mode==0) {c.DrawRectangle(Palette.Brush(track),null,new RoundedRect(new Rect(0,0,w,h),h/2)); if(level is double f&&f>0)c.DrawRectangle(Palette.Brush(color),null,new RoundedRect(new Rect(0,0,w*f,h),h/2));return;}
        int count=Math.Max(1,(int)(w/(mode==2?9:5))); double step=w/count;
        for(int i=0;i<count;i++)
        {
            int sampleIndex=samples.Length-count+i;
            double sample=sampleIndex>=0&&sampleIndex<samples.Length?samples[sampleIndex]:double.NaN;
            bool on=mode==1?double.IsFinite(sample):level.HasValue && i<count*level.Value;var brush=Palette.Brush(on?color:track);
            if(mode==2)c.DrawEllipse(brush,null,new Point(step*(i+.5),h/2),Math.Min(3.4,step*.35),Math.Min(3.4,h/2));
            else {double height=mode==3?h:on?Math.Max(1,h*Math.Clamp(sample,0,1)):h*.12;c.DrawRectangle(brush,null,new Rect(i*step,h-height,Math.Max(1,step-2),height));}
        }
    }
}
public sealed class AioRing : Control
{
    private readonly Color color,track; private readonly bool segmented,openArc; private double? level;
    public AioRing(Color color,Color track,bool segmented=false,bool openArc=false){this.color=color;this.track=track;this.segmented=segmented;this.openArc=openArc;}
    public void Set(double? value){level=value;InvalidateVisual();}
    public override void Render(DrawingContext c)
    {
        var center=new Point(Bounds.Width/2,Bounds.Height/2);double radius=Math.Max(0,Math.Min(Bounds.Width,Bounds.Height)/2-6);
        if(segmented)
        {
            for(int i=0;i<35;i++)
            {
                double angle=(135+i*270d/34)*Math.PI/180;var fill=level.HasValue&&i<level.Value*35?Palette.Mix(Color.Parse("#20D8D8"),Color.Parse("#D276E9"),i/34d):track;
                c.DrawLine(new Pen(Palette.Brush(fill),4),new Point(center.X+Math.Cos(angle)*(radius-8),center.Y+Math.Sin(angle)*(radius-8)),new Point(center.X+Math.Cos(angle)*radius,center.Y+Math.Sin(angle)*radius));
            }
            return;
        }
        if(openArc)
        {
            c.DrawGeometry(null,new Pen(Palette.Brush(track),7,lineCap:PenLineCap.Round),Rings.Arc(center,radius,140,260));
            if(level is double f2&&f2>0)c.DrawGeometry(null,new Pen(Palette.Brush(color),7,lineCap:PenLineCap.Round),Rings.Arc(center,radius,140,260*f2));return;
        }
        c.DrawEllipse(null,new Pen(Palette.Brush(track),5),center,radius,radius);
        if(level is not double f||f<=0)return;
        var geo=new StreamGeometry();using(var g=geo.Open())
        {
            g.BeginFigure(Pt(-Math.PI/2),false);int n=Math.Max(2,(int)(f*80));for(int i=1;i<=n;i++)g.LineTo(Pt(-Math.PI/2+Math.PI*2*f*i/n));g.EndFigure(false);
        }
        c.DrawGeometry(null,new Pen(Palette.Brush(color),5,lineCap:PenLineCap.Round),geo);
        Point Pt(double a)=>new(center.X+Math.Cos(a)*radius,center.Y+Math.Sin(a)*radius);
    }
}
public sealed class AioOrbitLines : Control
{
    private readonly Color a,b; public AioOrbitLines(Color a,Color b){this.a=a;this.b=b;}
    public override void Render(DrawingContext c)
    {
        var center=new Point(160,160);c.DrawEllipse(null,new Pen(Palette.Brush(a),1),center,107,107);
        foreach(var p in new[]{new Point(160,53),new Point(53,160),new Point(160,267),new Point(267,160)})c.DrawLine(new Pen(Palette.Brush(b),1.4),center,p);
        c.DrawRectangle(Palette.Brush(Color.Parse("#26263B")),new Pen(Palette.Brush(b),2),new RoundedRect(new Rect(145,145,30,30),6));
        for(int i=0;i<4;i++){c.DrawLine(new Pen(Palette.Brush(b),1),new Point(150+i*6,138),new Point(150+i*6,145));c.DrawLine(new Pen(Palette.Brush(b),1),new Point(150+i*6,175),new Point(150+i*6,182));}
    }
}
public sealed class AioRibbon : Control
{
    private readonly Color a,b;public AioRibbon(Color a,Color b){this.a=a;this.b=b;}
    public override void Render(DrawingContext c)
    {
        double w=Bounds.Width,h=Bounds.Height;
        foreach(var (color,phase) in new[]{(a,0d),(b,Math.PI)})
        {
            var geo=new StreamGeometry();using(var g=geo.Open()){g.BeginFigure(new Point(0,h/2),false);for(int i=1;i<=50;i++)g.LineTo(new Point(w*i/50,h/2+Math.Sin(i/50d*Math.PI*2+phase)*h*.35));g.EndFigure(false);}
            c.DrawGeometry(null,new Pen(Palette.Brush(Color.FromArgb(35,color.R,color.G,color.B)),8),geo);c.DrawGeometry(null,new Pen(Palette.Brush(color),1.5),geo);
        }
    }
}
public sealed class AioFan : Control
{
    private readonly Color color;public AioFan(Color color){this.color=color;}
    public override void Render(DrawingContext c)
    {
        var p=new Point(Bounds.Width/2,Bounds.Height/2);double r=Math.Min(Bounds.Width,Bounds.Height)*.46;
        c.DrawEllipse(null,new Pen(Palette.Brush(color),1),p,r,r);
        for(int i=0;i<5;i++){double a=i*Math.PI*2/5;var q=new Point(p.X+Math.Cos(a)*r*.54,p.Y+Math.Sin(a)*r*.54);c.DrawEllipse(Palette.Brush(color),null,q,r*.32,r*.22);}
        c.DrawEllipse(Palette.Brush(Color.Parse("#CFE8F3")),null,p,r*.16,r*.16);
    }
}
public sealed class AioDevice : Control
{
    private readonly string kind;private readonly Color color;
    public AioDevice(string kind,Color color){this.kind=kind;this.color=color;}
    public override void Render(DrawingContext c)
    {
        double w=Bounds.Width,h=Bounds.Height;
        void Polygon(string path,Color fill,Color? edge=null)
        {
            var geometry=StreamGeometry.Parse(path);
            var brush=new LinearGradientBrush {GradientStops={new GradientStop(Palette.Mix(fill,Colors.White,.2),0),new GradientStop(fill,.45),new GradientStop(Palette.Mix(fill,Colors.Black,.35),1)}};
            if(edge.HasValue)c.DrawGeometry(null,new Pen(Palette.Brush(Color.FromArgb(30,edge.Value.R,edge.Value.G,edge.Value.B)),6),geometry);
            c.DrawGeometry(brush,edge.HasValue?new Pen(Palette.Brush(edge.Value),1.3):null,geometry);
        }
        if(kind=="GPU")
        {
            Polygon("M7,48 L87,18 L102,31 L102,112 L22,144 L7,130 Z",Color.Parse("#272A41"),color);
            Polygon("M7,48 L87,18 L102,31 L22,62 Z",Color.Parse("#4D5368"),color);
            for(int i=0;i<3;i++){var p=new Point(32+i*24,91-i*9);c.DrawEllipse(Palette.Brush(Color.Parse("#11151E")),new Pen(Palette.Brush(color),1),p,14,23);c.DrawEllipse(Palette.Brush(color),null,p,3,5);}
        }
        else
        {
            Polygon("M7,48 L63,18 L101,40 L45,72 Z",Color.Parse("#505E6D"),color);
            Polygon("M7,48 L45,72 L45,88 L7,64 Z",Color.Parse("#273341"),color);
            Polygon("M45,72 L101,40 L101,56 L45,88 Z",Color.Parse("#1E2938"),color);
            c.DrawLine(new Pen(Palette.Brush(color),3),new Point(12,52),new Point(44,72));
            c.DrawLine(new Pen(Palette.Brush(color),3),new Point(48,72),new Point(97,44));
            if(kind=="RAM")for(int i=0;i<4;i++)c.DrawRectangle(Palette.Brush(Color.Parse("#121B24")),new Pen(Palette.Brush(color),1),new Rect(25+i*13,42-i*3,9,12));
            else c.DrawRectangle(Palette.Brush(Color.Parse("#1B2532")),new Pen(Palette.Brush(color),1),new RoundedRect(new Rect(37,37,29,17),3));
        }
        c.DrawLine(new Pen(Palette.Brush(color),1),new Point(w*.9,h*.7),new Point(w,h*.7));
    }
}
