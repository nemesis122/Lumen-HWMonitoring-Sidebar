using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Lumen.App;

public enum NextGraphicMode { Liquid, Contour, Split, Ribbon }

public sealed class NextGraphic : Control
{
    public NextGraphicMode Mode { get; set; }
    public Palette Theme { get; set; } = null!;
    public double RightInset { get; set; }
    private double[] series=Array.Empty<double>();
    private double level,maximum=1;
    private Color accent;
    public void Set(double[] values,double? max,double fraction,Color color)
    {
        series=values.TakeLast(120).ToArray();
        maximum=max.HasValue&&double.IsFinite(max.Value)&&max.Value>0?max.Value:
            Math.Max(1e-9,series.Where(double.IsFinite).DefaultIfEmpty(1).Max()*1.12);
        level=double.IsFinite(fraction)?Math.Clamp(fraction,0,1):0;accent=color;
        InvalidateVisual();
    }
    private IBrush Brush(Color c,double opacity=1)=>Palette.Brush(Palette.Alpha(c,c.A/255.0*opacity));
    public Point EndPoint(Size size)
    {
        int lastIndex=Array.FindLastIndex(series,double.IsFinite);
        double last=lastIndex>=0?series[lastIndex]:double.NaN;
        double fraction=double.IsFinite(last)?Math.Clamp(last/maximum,0,1):level;
        double endX=Math.Max(8,size.Width-RightInset-8);
        double x=lastIndex>=0&&series.Length>1?6+(endX-6)*lastIndex/(series.Length-1):endX;
        return new Point(x,8+(1-fraction)*Math.Max(0,size.Height-16));
    }
    public override void Render(DrawingContext ctx)
    {
        double w=Bounds.Width,h=Bounds.Height;
        if(Theme==null||!double.IsFinite(w)||!double.IsFinite(h)||w<16||h<16)return;
        using(ctx.PushClip(new Rect(Bounds.Size))) {
            switch(Mode) {
                case NextGraphicMode.Liquid: DrawLiquid(ctx,w,h);break;
                case NextGraphicMode.Contour: DrawContour(ctx,w,h);break;
                case NextGraphicMode.Split: DrawSplit(ctx,w,h);break;
                case NextGraphicMode.Ribbon: DrawRibbon(ctx,w,h);break;
            }
        }
    }
    private void DrawLiquid(DrawingContext ctx,double w,double h)
    {
        if(level<=0)return;
        StreamGeometry Surface(double phase,double offset,bool filled) {
            var geometry=new StreamGeometry();using var g=geometry.Open();
            double Y(double x)=>level>=1?0:Math.Clamp(h*(1-level)+offset+Math.Sin(x/w*Math.PI*2+phase)*Math.Min(6,h*.035),0,h);
            g.BeginFigure(new Point(0,Y(0)),filled);
            for(int i=1;i<=64;i++){double x=w*i/64;g.LineTo(new Point(x,Y(x)));}
            if(filled){g.LineTo(new Point(w,h));g.LineTo(new Point(0,h));}
            g.EndFigure(filled);return geometry;
        }
        var gradient=new LinearGradientBrush {StartPoint=new RelativePoint(0,0,RelativeUnit.Relative),EndPoint=new RelativePoint(0,1,RelativeUnit.Relative)};
        gradient.GradientStops.Add(new GradientStop(Palette.Alpha(accent,Theme.Dark ? .36 : .24),0));
        gradient.GradientStops.Add(new GradientStop(Palette.Alpha(accent,.08),1));
        ctx.DrawGeometry(Brush(accent,.07),null,Surface(level*3+1,-5,true));
        ctx.DrawGeometry(gradient,null,Surface(level*3,0,true));
        if(level<1)ctx.DrawGeometry(null,new Pen(Brush(accent,.75),2),Surface(level*3,0,false));
    }
    private void DrawContour(DrawingContext ctx,double w,double h)
    {
        Point At(double angle,double inset) {
            double c=Math.Cos(angle),s=Math.Sin(angle);
            return new Point(w/2+(w/2-inset)*Math.Sign(c)*Math.Sqrt(Math.Abs(c)),h/2+(h/2-inset)*Math.Sign(s)*Math.Sqrt(Math.Abs(s)));
        }
        StreamGeometry Path(double inset,double fraction) {
            var geometry=new StreamGeometry();using var g=geometry.Open();
            g.BeginFigure(At(Math.PI/2,inset),false);
            int steps=Math.Max(1,(int)Math.Ceiling(180*fraction));
            for(int i=1;i<=steps;i++)g.LineTo(At(Math.PI/2+Math.PI*2*fraction*i/steps,inset));
            g.EndFigure(fraction>=1);return geometry;
        }
        foreach(double inset in new[]{4d,10d,16d})ctx.DrawGeometry(null,new Pen(Brush(Theme.Soft,.12),1),Path(inset,1));
        if(level>0) {
            var active=Path(10,level);
            ctx.DrawGeometry(null,new Pen(Brush(accent,.1),10,null,PenLineCap.Round),active);
            ctx.DrawGeometry(null,new Pen(Brush(accent),3,null,PenLineCap.Round),active);
            ctx.DrawEllipse(Brush(accent),null,At(Math.PI/2+Math.PI*2*level,10),3,3);
        }
    }
    private void DrawSplit(DrawingContext ctx,double w,double h)
    {
        var geometry=new StreamGeometry();using(var g=geometry.Open()) {
            g.BeginFigure(new Point(0,0),true);g.LineTo(new Point(w*.62,0));
            g.LineTo(new Point(w*.43,h));g.LineTo(new Point(0,h));g.EndFigure(true);
        }
        ctx.DrawGeometry(Brush(accent,Theme.Dark ? .19 : .13),null,geometry);
        ctx.DrawLine(new Pen(Brush(accent,.6),2),new Point(w*.62,0),new Point(w*.43,h));
    }
    private void DrawRibbon(DrawingContext ctx,double w,double h)
    {
        double endX=Math.Max(8,w-RightInset-8);
        var end=EndPoint(Bounds.Size);
        if(series.Count(double.IsFinite)<2) {
            ctx.DrawEllipse(Brush(accent,.14),null,end,8,8);ctx.DrawEllipse(Brush(accent),null,end,3,3);return;
        }
        var gradient=new LinearGradientBrush {StartPoint=new RelativePoint(0,0,RelativeUnit.Relative),EndPoint=new RelativePoint(1,0,RelativeUnit.Relative)};
        gradient.GradientStops.Add(new GradientStop(Palette.Alpha(accent,.12),0));
        gradient.GradientStops.Add(new GradientStop(Palette.Alpha(accent,.9),1));
        int start=0;
        while(start<series.Length) {
            while(start<series.Length&&!double.IsFinite(series[start]))start++;
            int stop=start;while(stop<series.Length&&double.IsFinite(series[stop]))stop++;
            if(stop-start>=2) {
                Point PointAt(int i)=>new Point(6+(endX-6)*i/(series.Length-1),8+(1-Math.Clamp(series[i]/maximum,0,1))*(h-16));
                var path=new StreamGeometry();using(var g=path.Open()) {
                    g.BeginFigure(PointAt(start),false);
                    for(int i=start+1;i<stop;i++){var a=PointAt(i-1);var b=PointAt(i);double mid=(a.X+b.X)/2;g.CubicBezierTo(new Point(mid,a.Y),new Point(mid,b.Y),b);}
                    g.EndFigure(false);
                }
                ctx.DrawGeometry(null,new Pen(Brush(accent,.08),21,null,PenLineCap.Round),path);
                ctx.DrawGeometry(null,new Pen(gradient,12,null,PenLineCap.Round),path);
                ctx.DrawGeometry(null,new Pen(Brush(accent,.8),1.5,null,PenLineCap.Round),path);
            }
            start=stop+1;
        }
        ctx.DrawEllipse(Brush(Theme.Card),new Pen(Brush(accent),2),end,5,5);
    }
}

// Reserves a separate rectangle for the value at the actual final plotted point.
public sealed class RibbonPanel : Panel
{
    private readonly NextGraphic graphic;
    private readonly Control number;
    public RibbonPanel(NextGraphic graphic,Control number){this.graphic=graphic;this.number=number;Children.Add(graphic);Children.Add(number);}
    protected override Size MeasureOverride(Size available)
    {
        double width=double.IsFinite(available.Width)?Math.Max(1,available.Width):280;
        number.Measure(new Size(Math.Min(150,width*.48),76));
        graphic.Measure(new Size(width,100));return new Size(width,100);
    }
    protected override Size ArrangeOverride(Size final)
    {
        double width=Math.Min(Math.Min(150,final.Width*.48),Math.Max(1,number.DesiredSize.Width));
        double height=Math.Min(final.Height,Math.Max(1,number.DesiredSize.Height));
        graphic.RightInset=width+10;graphic.Arrange(new Rect(final));graphic.InvalidateVisual();
        var end=graphic.EndPoint(final);
        number.Arrange(new Rect(Math.Min(final.Width-width,end.X+10),Math.Clamp(end.Y-height/2,0,Math.Max(0,final.Height-height)),width,height));
        return final;
    }
}



