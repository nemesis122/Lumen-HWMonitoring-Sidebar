using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Lumen.App;

// Native vector graphics: all plotted samples and levels come from actual readings.
public sealed class WaveChart : Control
{
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
            Wave(ctx, w, h);
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
}
