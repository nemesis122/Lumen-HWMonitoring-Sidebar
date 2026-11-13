using Avalonia.Layout;
using Avalonia.VisualTree;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Lumen.App;

/// <summary>A changing number can shrink to fit its cell, but cannot change the row's height.</summary>
public sealed class AioReferenceValue : Decorator
{
    private readonly TextBlock number;
    public AioReferenceValue(TextBlock number,Control? face=null)
    {
        this.number=number;number.TextWrapping=TextWrapping.NoWrap;
        Child=new Viewbox{Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,Child=widest=new AioWidest{Child=face??number}};
    }
    private readonly AioWidest widest;
    protected override Size MeasureOverride(Size available)
    {
        widest.Room=available.Width;
        double height=number.FontSize*1.35;Child!.Measure(new Size(available.Width,height));
        return new Size(double.IsFinite(available.Width)?available.Width:Child.DesiredSize.Width,height);
    }
    protected override Size ArrangeOverride(Size final){Child!.Arrange(new Rect(final));return final;}
}

/// <summary>Fits its content into the column (shrinking only when needed) and tells it how wide the column is.</summary>
public sealed class AioFit : Decorator
{
    protected override Size MeasureOverride(Size available)
    {
        if(Child is Viewbox{Child:AioWidest w} box)w.Room=available.Width-box.Margin.Left-box.Margin.Right;
        Child?.Measure(available);return Child?.DesiredSize??default;
    }
}

/// <summary>
/// Holds the room of the widest content it has shown, so a value that fits its column by shrinking keeps one size:
/// "9 °C" → "100 °C" → "9 °C" no longer makes the number grow, shrink and move up and down with every update.
/// The content sits left, centred or right in that room, following the alignment of the surrounding Viewbox.
/// </summary>
public sealed class AioWidest : Decorator
{
    private double widest, highest;
    /// <summary>The width of the column the content is shown in (set by <see cref="AioFit"/> or <see cref="AioReferenceValue"/>).</summary>
    public double Room { get; set; } = double.PositiveInfinity;
    protected override Size MeasureOverride(Size available)
    {
        if(Child==null)return default;
        // Values reserve room for their widest form, but only as much as the column has to spare: first the content
        // at its natural width, then the spare room shared out among the values. A value never shrinks to make room
        // for its own reserve, and the content fills the same width whatever the values show.
        var values=Child.GetSelfAndVisualDescendants().OfType<AioValueText>().ToList();
        double room=Room;
        if(values.Count>0&&double.IsFinite(room))
        {
            foreach(var v in values){v.Cap=0;v.InvalidateMeasure();}
            Child.InvalidateMeasure();Child.Measure(Size.Infinity);
            double natural=Child.DesiredSize.Width;
            foreach(var v in values){v.Cap=double.PositiveInfinity;v.InvalidateMeasure();}
            Child.InvalidateMeasure();Child.Measure(Size.Infinity);
            double reserved=Child.DesiredSize.Width;
            if(reserved>room)
            {
                // The widest form of the values does not fit: it would have to shrink the moment such a value shows up.
                // So it shrinks by that one steady amount right away instead of a little more or less with every value.
            }
            else
            {
                // Values side by side share the spare room; values stacked on top of each other each get all of it.
                foreach(var v in values){v.Cap=v.Natural+(room-natural)/SideBySide(v,values);v.InvalidateMeasure();}
            }
            Child.InvalidateMeasure();
        }
        Child.Measure(Size.Infinity);
        widest=Math.Max(widest,Child.DesiredSize.Width);highest=Math.Max(highest,Child.DesiredSize.Height);
        return new Size(widest,highest);
    }
    /// <summary>How many values share the line of this one (1 when it is alone on its line, e.g. in a stack of rates).</summary>
    private int SideBySide(AioValueText value,List<AioValueText> values)
    {
        for(Visual? v=value.GetVisualParent();v!=null&&v!=this;v=v.GetVisualParent())
            if(v is StackPanel{Orientation:Orientation.Horizontal}||v is Grid{ColumnDefinitions.Count:>1})
                return Math.Max(1,values.Count(x=>x.GetVisualAncestors().Contains(v)));
        return 1;
    }
    protected override Size ArrangeOverride(Size final)
    {
        if(Child==null)return final;
        var align=this.FindAncestorOfType<Viewbox>()?.HorizontalAlignment??HorizontalAlignment.Center;
        double w=Child.DesiredSize.Width,h=Child.DesiredSize.Height;
        double x=align switch{HorizontalAlignment.Left=>0,HorizontalAlignment.Right=>final.Width-w,_=>(final.Width-w)/2};
        // Values keep a fixed room; their text sits on the side the value is aligned to. A number followed by its unit
        // ("38 °C") hugs the unit, so the unit stays put.
        foreach(var v in Child.GetSelfAndVisualDescendants().OfType<AioValueText>())
        {
            bool beforeUnit=v.Parent is StackPanel{Orientation:Orientation.Horizontal} row&&row.Children.IndexOf(v)<row.Children.Count-1&&row.Children[^1] is TextBlock unit&&unit is not AioValueText&&unit.Text is "°C" or "°F";
            // In a small label/value table, values are left-aligned beside their names.
            v.TextAlignment=beforeUnit?TextAlignment.Right:v.Parent is Grid g&&Grid.GetColumn(v)>0&&g.ColumnDefinitions.Count==2&&g.ColumnDefinitions[0].Width.IsAuto?TextAlignment.Left:align switch{HorizontalAlignment.Left=>TextAlignment.Left,HorizontalAlignment.Right=>TextAlignment.Right,_=>TextAlignment.Center};
        }
        Child.Arrange(new Rect(x,(final.Height-h)/2,w,h));return final;
    }
}

/// <summary>Reference-specific decoration only; values and sensor controls remain native children.</summary>
public sealed class AioReferenceSurface : Decorator
{
    public string Mode { get; init; } = "carbon";
    public Color Tint { get; init; } = Color.Parse("#22D9DE");
    public bool Light { get; init; }
    /// <summary>Pop shapes on a dark sidebar: a dark panel tinted by the colour, outlined in the colour.</summary>
    public bool Dark { get; init; }
    /// <summary>The card's opacity (0..1): panel fills are as see-through as the card itself.</summary>
    public double Alpha { get; init; } = 1;
    public override void Render(DrawingContext c)
    {
        double w=Bounds.Width,h=Bounds.Height;if(w<4||h<4)return;
        var g=new StreamGeometry();
        using(var p=g.Open())
        {
            if(Mode=="banner")
            {p.BeginFigure(new Point(15,1),true);p.LineTo(new Point(w-1,1));p.LineTo(new Point(w-15,h-1));p.LineTo(new Point(1,h-1));}
            else if(Mode=="pop")
            {
                p.BeginFigure(new Point(24,2),true);p.CubicBezierTo(new Point(w*.45,-1),new Point(w*.80,2),new Point(w-15,12));
                p.CubicBezierTo(new Point(w+12,27),new Point(w-3,h*.4),new Point(w-5,h-24));
                p.CubicBezierTo(new Point(w-11,h+6),new Point(w*.6,h-1),new Point(22,h-3));
                p.CubicBezierTo(new Point(-9,h-6),new Point(2,h*.6),new Point(3,25));p.CubicBezierTo(new Point(4,9),new Point(11,3),new Point(24,2));
            }
            else if(Mode=="aurora")
            {
                p.BeginFigure(new Point(27,15),true);p.CubicBezierTo(new Point(w*.4,-12),new Point(w*.7,33),new Point(w-27,15));
                p.CubicBezierTo(new Point(w-13,11),new Point(w-12,21),new Point(w-10,36));
                p.CubicBezierTo(new Point(w+1,h*.35),new Point(w-3,h*.7),new Point(w-13,h-30));
                p.CubicBezierTo(new Point(w-17,h-8),new Point(w-24,h-12),new Point(w-35,h-10));
                p.CubicBezierTo(new Point(w*.65,h+10),new Point(w*.35,h-38),new Point(28,h-14));
                p.CubicBezierTo(new Point(13,h-5),new Point(12,h-24),new Point(11,h-40));
                p.CubicBezierTo(new Point(0,h*.7),new Point(4,h*.3),new Point(12,34));
                p.CubicBezierTo(new Point(15,23),new Point(14,18),new Point(27,15));
            }
            else
            {
                p.BeginFigure(new Point(20,1),true);p.LineTo(new Point(w*.56,1));p.LineTo(new Point(w*.61,9));p.LineTo(new Point(w-14,9));
                p.LineTo(new Point(w-1,24));p.LineTo(new Point(w-1,h-20));p.LineTo(new Point(w-20,h-1));p.LineTo(new Point(15,h-1));
                p.LineTo(new Point(1,h-15));p.LineTo(new Point(1,20));
            }
            p.EndFigure(true);
        }
        if(Mode=="pop"&&Dark){c.DrawGeometry(new SolidColorBrush(Palette.Mix(Color.Parse("#11151C"),Tint,.22)){Opacity=Alpha},new Pen(Palette.Brush(Tint),1.6),g);return;}
        if(Mode is "pop" or "banner"){c.DrawGeometry(Palette.Brush(Tint),null,g);return;}
        if(Mode=="aurora")
        {
            var glow=new LinearGradientBrush{StartPoint=new RelativePoint(0,0,RelativeUnit.Relative),EndPoint=new RelativePoint(1,1,RelativeUnit.Relative),GradientStops={new(Color.Parse("#14EEEA"),0),new(Color.Parse("#922FE5"),.45),new(Color.Parse("#FD875E"),.72),new(Color.Parse("#C72CEF"),1)}};
            for(int i=34;i>=2;i-=2)c.DrawGeometry(null,new Pen(glow,i){Brush=WithOpacity(glow,(36-i)/260d)},g);
            c.DrawGeometry(Palette.Brush(Color.Parse(Light?"#F3F5FB":"#101124")),new Pen(glow,1.5),g);
            using(c.PushGeometryClip(g))
            {
                foreach(var (x,y,color) in new[]{(8d,h*.48,Color.Parse("#00F0F0")),(w-8,h*.24,Color.Parse("#F96D49")),(w*.78,h-8,Color.Parse("#B92CF0"))})
                    c.DrawEllipse(new RadialGradientBrush{GradientStops={new(Color.FromArgb(100,color.R,color.G,color.B),0),new(Color.FromArgb(0,color.R,color.G,color.B),1)}},null,new Point(x,y),34,h*.55);
            }
            c.DrawGeometry(null,new Pen(WithOpacity(glow,.14),22),g);
            c.DrawGeometry(null,new Pen(WithOpacity(glow,.3),8),g);
            return;
        }
        var fill=new LinearGradientBrush{StartPoint=new RelativePoint(0,0,RelativeUnit.Relative),EndPoint=new RelativePoint(0,1,RelativeUnit.Relative),GradientStops={new(Color.Parse(Light?"#F4F6FC":"#30343F"),0),new(Color.Parse(Light?"#DBE2EE":"#10131D"),.13),new(Color.Parse(Light?"#F0F3F8":"#090D16"),.8),new(Color.Parse(Light?"#C6CFDF":"#222735"),1)},Opacity=Alpha};
        c.DrawGeometry(fill,new Pen(Palette.Brush(Color.Parse("#59606D")),1),g);
        using(c.PushGeometryClip(g))
        {
            for(int y=0;y<h;y+=5)for(int x=0;x<w;x+=8)
                c.DrawLine(new Pen(Palette.Brush(Color.FromArgb(11,160,175,195)),1),new Point(x+(y%10==0?0:4),y),new Point(x+4+(y%10==0?0:4),y+2));
        }
        c.DrawLine(new Pen(Palette.Brush(Tint),2),new Point(20,3),new Point(w*.53,3));
    }
    private static IBrush WithOpacity(LinearGradientBrush source,double opacity)=>new LinearGradientBrush{StartPoint=source.StartPoint,EndPoint=source.EndPoint,GradientStops=source.GradientStops,Opacity=opacity};
}

public sealed class AioIsometricConnections : Control
{
    public int FanCount { get; init; } = 5;
    public bool ShowCpu { get; init; } = true;
    public bool ShowGpu { get; init; } = true;
    public bool ShowRam { get; init; } = true;
    public bool ShowDisk { get; init; } = true;
    public override void Render(DrawingContext c)
    {
        foreach(var (show,path,color) in new[]{(ShowCpu,"M160,76 L178,53 L190,53","#24D5DD"),(ShowGpu,"M170,230 L182,213 L190,213","#AA79EE"),(ShowGpu,"M170,262 L188,260 L202,260","#FA7E74"),(ShowGpu,"M154,301 L183,301 L202,300","#AA79EE"),(ShowRam,"M135,376 L230,376 L258,372","#24D5DD"),(ShowDisk,"M235,456 L246,456 L258,438","#F5B94D"),(ShowCpu&&ShowGpu,"M35,159 L11,180 L11,275","#24D5DD"),(ShowGpu&&ShowRam,FanCount>0?"M49,332 L49,358 L12,375 L12,476 L45,495 L32,504":"M49,332 L49,358 L12,375 L12,420","#24D5DD")})
        {
            if(show)Wire(path,Color.Parse(color));
        }
        var colors=new[]{"#24D5DD","#24D5DD","#AA79EE","#F5B94D","#FA7E74"};
        for(int i=0;i<Math.Min(5,FanCount);i++)
        {
            var tint=Color.Parse(colors[i]);double x=32+i*66.8;
            Wire(FormattableString.Invariant($"M{(i==0?45:x-66.8)},504 L{x},504 L{x-7},526"),tint);
            c.DrawEllipse(Palette.Brush(tint),null,new Point(x,504),2.2,2.2);
        }
        void Wire(string path,Color tint)
        {
            var geometry=StreamGeometry.Parse(path);c.DrawGeometry(null,new Pen(Palette.Brush(Color.FromArgb(35,tint.R,tint.G,tint.B)),7),geometry);c.DrawGeometry(null,new Pen(Palette.Brush(tint),1.2),geometry);
        }
        foreach(var (show,point) in new[]{(ShowCpu,new Point(178,53)),(ShowGpu,new Point(182,213)),(ShowGpu,new Point(202,260)),(ShowGpu,new Point(202,300)),(ShowRam,new Point(230,376)),(ShowDisk,new Point(246,456))})
            if(show)c.DrawEllipse(Palette.Brush(Color.Parse("#A0F2EF")),null,point,2.2,2.2);
    }
}

public sealed class AioReferenceHistory : Control
{
    public Color Color { get; init; }
    public Color OtherColor { get; init; } = Color.Parse("#F9BC44");
    public string Mode { get; init; } = "area";
    private double[] values=Array.Empty<double>(), other=Array.Empty<double>();
    public void Set(double[] first,double[]? second=null){values=first;other=second??Array.Empty<double>();InvalidateVisual();}
    public override void Render(DrawingContext c)
    {
        double w=Bounds.Width,h=Bounds.Height;if(w<=1||h<=1)return;
        if(Mode=="mirror"){Series(values,Color,h/2-2,h/2-2,false);Series(other,OtherColor,h/2+2,h/2-2,true);}
        else Series(values,Color,h-2,h-5,false);
        void Series(double[] data,Color color,double origin,double amplitude,bool invert)
        {
            if(data.Length<2)return;double max=Math.Max(.001,data.Where(double.IsFinite).DefaultIfEmpty(1).Max()*1.2);var stroke=new StreamGeometry();var area=new StreamGeometry();
            using(var s=stroke.Open())using(var a=area.Open())
            {
                a.BeginFigure(new Point(0,origin),true);bool started=false;
                for(int i=0;i<data.Length;i++)
                {
                    if(!double.IsFinite(data[i])){started=false;continue;}
                    double x=w*i/(data.Length-1),level=Math.Clamp(data[i]/max,0,1),y=origin+(invert?1:-1)*amplitude*level;
                    if(Mode=="bars")
                    {double slot=w/data.Length;c.DrawRectangle(Palette.Brush(Palette.Mix(color,Colors.White,level*.3)),null,new Rect(slot*i,origin-amplitude*level,Math.Max(1,slot-1),amplitude*level));continue;}
                    if(started)s.LineTo(new Point(x,y));else{s.BeginFigure(new Point(x,y),false);started=true;}a.LineTo(new Point(x,y));
                }
                if(started)s.EndFigure(false);a.LineTo(new Point(w,origin));a.EndFigure(true);
            }
            if(Mode=="bars")return;
            if(Mode!="line")c.DrawGeometry(new LinearGradientBrush{StartPoint=new RelativePoint(0,invert?1:0,RelativeUnit.Relative),EndPoint=new RelativePoint(0,invert?0:1,RelativeUnit.Relative),GradientStops={new(Color.FromArgb(150,color.R,color.G,color.B),0),new(Color.FromArgb(5,color.R,color.G,color.B),1)}},null,area);
            c.DrawGeometry(null,new Pen(Palette.Brush(Color.FromArgb(25,color.R,color.G,color.B)),7),stroke);
            c.DrawGeometry(null,new Pen(Palette.Brush(Color.FromArgb(50,color.R,color.G,color.B)),3.5),stroke);
            c.DrawGeometry(null,new Pen(Palette.Brush(color),1.4),stroke);
        }
    }
}

public sealed class AioReferenceFan : Control
{
    public Color Color { get; init; }
    public bool Outline { get; init; }
    public override void Render(DrawingContext c)
    {
        double size=Math.Min(Bounds.Width,Bounds.Height),r=size*.42;var center=new Point(Bounds.Width/2,Bounds.Height/2);
        c.DrawEllipse(null,new Pen(Palette.Brush(Color),Outline?1.4:2),center,r,r);
        if(!Outline)c.DrawEllipse(Palette.Brush(Color.FromArgb(35,Color.R,Color.G,Color.B)),null,center,r*.87,r*.87);
        for(int i=0;i<5;i++)
        {
            double a=i*Math.PI*2/5;
            Point P(double radius,double angle)=>new(center.X+radius*Math.Cos(angle),center.Y+radius*Math.Sin(angle));
            var g=new StreamGeometry();using(var p=g.Open())
            {p.BeginFigure(P(r*.18,a),true);p.CubicBezierTo(P(r*.9,a-.9),P(r*.99,a+.4),P(r*.72,a+.65));p.CubicBezierTo(P(r*.4,a+.9),P(r*.2,a+.4),P(r*.18,a));p.EndFigure(true);}
            c.DrawGeometry(Outline?null:new RadialGradientBrush{GradientStops={new(Palette.Mix(Color,Colors.White,.55),0),new(Color,.55),new(Palette.Mix(Color,Colors.Black,.35),1)}},new Pen(Palette.Brush(Color),Outline?1:0),g);
        }
        c.DrawEllipse(Palette.Brush(Color),null,center,r*.14,r*.14);
    }
}
