using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Lumen.App;

public enum ProGraphicMode { Wave, Segments, Orbit, Prism, Bars }

// Native vector graphics: all plotted samples and levels come from actual readings.
public sealed class ProGraphic : Control
{
    public ProGraphicMode Mode { get; set; }
    public Palette Theme { get; set; } = null!;
    private double[] samples = Array.Empty<double>();
    private double fraction, maximum = 1;
    private Color accent;
    public void Set(double[] values, double? max, double level, Color color)
    {
        samples = values.Where(double.IsFinite).TakeLast(120).ToArray();
        maximum = max.HasValue && double.IsFinite(max.Value) && max.Value > 0
            ? max.Value : Math.Max(1e-9, samples.DefaultIfEmpty(1).Max() * 1.12);
        fraction = double.IsFinite(level) ? Math.Clamp(level, 0, 1) : 0;
        accent = color;
        InvalidateVisual();
    }
    public override void Render(DrawingContext ctx)
    {
        double w = Bounds.Width, h = Bounds.Height;
        if (Theme == null || !double.IsFinite(w) || !double.IsFinite(h) || w < 16 || h < 12) return;
        using (ctx.PushClip(new Rect(Bounds.Size))) {
            switch (Mode) {
                case ProGraphicMode.Wave: Wave(ctx, w, h); break;
                case ProGraphicMode.Segments: Segments(ctx, w, h); break;
                case ProGraphicMode.Orbit: Orbit(ctx, w, h); break;
                case ProGraphicMode.Prism: Prism(ctx, w, h); break;
                case ProGraphicMode.Bars: Bars(ctx, w, h); break;
            }
        }
    }
    private IBrush Brush(Color c, double opacity = 1) => Palette.Brush(Palette.Alpha(c, c.A / 255.0 * opacity));
    private void Wave(DrawingContext ctx, double w, double h)
    {
        double x0 = 6, x1 = w - 10, y0 = 10, y1 = h - 8;
        for (int i = 1; i < 3; i++) {
            double y = y0 + (y1-y0)*i/3;
            ctx.DrawLine(new Pen(Brush(Theme.Soft, .12), 1), new Point(x0,y), new Point(x1,y));
        }
        if (samples.Length < 2) { Level(ctx,w,h); return; }
        Point[] points = samples.Select((v,i) => new Point(x0+(x1-x0)*i/(samples.Length-1), y1-Math.Clamp(v/maximum,0,1)*(y1-y0))).ToArray();
        StreamGeometry Path(bool fill) {
            var path = new StreamGeometry();
            using var g = path.Open();
            g.BeginFigure(fill ? new Point(x0,y1) : points[0], fill);
            if (fill) g.LineTo(points[0]);
            for(int i=1;i<points.Length;i++) {
                var a=points[i-1];var b=points[i];double mid=(a.X+b.X)/2;
                g.CubicBezierTo(new Point(mid,a.Y),new Point(mid,b.Y),b);
            }
            if(fill) g.LineTo(new Point(x1,y1));
            g.EndFigure(fill);return path;
        }
        var gradient = new LinearGradientBrush { StartPoint = new RelativePoint(0,0,RelativeUnit.Relative), EndPoint = new RelativePoint(0,1,RelativeUnit.Relative) };
        gradient.GradientStops.Add(new GradientStop(Palette.Alpha(accent,.48),0));
        gradient.GradientStops.Add(new GradientStop(Palette.Alpha(accent,.02),1));
        ctx.DrawGeometry(gradient,null,Path(true));
        var line=Path(false);
        ctx.DrawGeometry(null,new Pen(Brush(accent,.12),9,null,PenLineCap.Round),line);
        ctx.DrawGeometry(null,new Pen(Brush(accent),2.8,null,PenLineCap.Round),line);
        var end=points[^1];
        ctx.DrawEllipse(Brush(accent,.15),null,end,8,8);
        ctx.DrawEllipse(Brush(accent),new Pen(Brush(Theme.Card),2),end,3.7,3.7);
    }
    private void Level(DrawingContext ctx,double w,double h)
    {
        double y=h/2;
        ctx.DrawRectangle(Brush(Theme.Track),null,new RoundedRect(new Rect(5,y-4,w-10,8),4));
        if(fraction>0) ctx.DrawRectangle(Brush(accent),null,new RoundedRect(new Rect(5,y-4,Math.Max(8,(w-10)*fraction),8),4));
    }
    private void Segments(DrawingContext ctx,double w,double h)
    {
        const int count=12;
        bool horizontal=w>h*1.8;
        for(int i=0;i<count;i++) {
            double share=Math.Clamp(fraction*count-i,0,1);
            var color=share>0?Palette.Mix(accent,Theme.Ink,.12*i/count):Theme.Track;
            Rect r;
            if(horizontal) {
                double step=(w-2)/count;
                r=new Rect(1+i*step,2,Math.Max(1,step-3),Math.Max(1,h-4));
            } else {
                double step=(h-2)/count, width=w*(.5+.5*i/(count-1));
                r=new Rect(w-width, h-(i+1)*step, width,Math.Max(1,step-2.6));
            }
            ctx.DrawRectangle(Brush(color,share>0?1:.65),null,new RoundedRect(r,2));
        }
    }
    private void Orbit(DrawingContext ctx,double w,double h)
    {
        var center=new Point(w/2,h/2);double radius=Math.Min(w,h)/2-9;
        if(radius<2)return;
        Point At(double angle,double r) => new Point(center.X+r*Math.Cos(angle*Math.PI/180),center.Y+r*Math.Sin(angle*Math.PI/180));
        for(int i=0;i<=24;i++) {
            double a=140+260.0*i/24;
            ctx.DrawLine(new Pen(Brush(Theme.Soft,.28),1),At(a,radius+5),At(a,radius+8));
        }
        void Arc(double sweep,IPen pen) {
            if(sweep<.1)return;
            var path=new StreamGeometry();using(var g=path.Open()) {
                g.BeginFigure(At(140,radius),false);
                g.ArcTo(At(140+sweep,radius),new Size(radius,radius),0,sweep>180,SweepDirection.Clockwise);
                g.EndFigure(false);
            }
            ctx.DrawGeometry(null,pen,path);
        }
        Arc(260,new Pen(Brush(Theme.Track),8,null,PenLineCap.Round));
        Arc(260*fraction,new Pen(Brush(accent,.13),15,null,PenLineCap.Round));
        Arc(260*fraction,new Pen(Brush(accent),8,null,PenLineCap.Round));
        if(fraction>0)ctx.DrawEllipse(Brush(Theme.Ink),null,At(140+260*fraction,radius),2.5,2.5);
    }
    private void Prism(DrawingContext ctx,double w,double h)
    {
        double end=Math.Clamp(w*fraction,0,w),slope=Math.Min(46,w*.2);
        var shape=new StreamGeometry();using(var g=shape.Open()) {
            g.BeginFigure(new Point(0,0),true);g.LineTo(new Point(end,0));
            g.LineTo(new Point(Math.Max(0,end-slope),h));g.LineTo(new Point(0,h));g.EndFigure(true);
        }
        var gradient=new LinearGradientBrush { StartPoint=new RelativePoint(0,0,RelativeUnit.Relative),EndPoint=new RelativePoint(1,1,RelativeUnit.Relative) };
        gradient.GradientStops.Add(new GradientStop(Palette.Alpha(accent,Theme.Dark ? .3 : .2),0));
        gradient.GradientStops.Add(new GradientStop(Palette.Alpha(accent,.08),1));
        ctx.DrawGeometry(gradient,null,shape);
        ctx.DrawLine(new Pen(Brush(accent,.85),3),new Point(end,0),new Point(Math.Max(0,end-slope),h));
        for(int i=0;i<3;i++) {
            double x=end+10+i*10;
            ctx.DrawLine(new Pen(Brush(accent,.09),1),new Point(x,0),new Point(x-slope,h));
        }
    }
    private void Bars(DrawingContext ctx,double w,double h)
    {
        if(samples.Length<2) { Level(ctx,w,h);return; }
        int count=Math.Min(28,samples.Length);double step=w/count;
        for(int i=0;i<count;i++) {
            int from=i*samples.Length/count,to=Math.Max(from+1,(i+1)*samples.Length/count);
            double value=0;for(int j=from;j<to;j++)value+=samples[j];value/=(to-from);
            double height=Math.Clamp(value/maximum,0,1)*(h-4);
            var r=new Rect(i*step+1,h-Math.Max(2,height)-2,Math.Max(1,step-2.8),Math.Max(2,height));
            var color=i>=count-3?accent:Palette.Mix(accent,Theme.Cool,.4);
            ctx.DrawRectangle(Brush(color,i>=count-3?1:.35+.45*i/count),null,new RoundedRect(r,Math.Min(2.5,step/2)));
        }
    }
}


