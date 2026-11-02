using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed class BoldValue : StackPanel
{
    private readonly TextBlock number, unit;
    private readonly Palette palette;
    public BoldValue(double size, Palette p)
    {
        palette=p;Orientation=Orientation.Horizontal;Spacing=3;
        number=Ui.Text("—",size,p.Ink,FontWeight.Black,true);
        number.LetterSpacing=-size*.05;number.VerticalAlignment=VerticalAlignment.Bottom;
        unit=Ui.Text("",Math.Max(16,size*.29),p.Cool,FontWeight.Bold,true);
        unit.VerticalAlignment=VerticalAlignment.Bottom;unit.Margin=new Thickness(0,0,0,size*.14);
        Children.Add(number);Children.Add(unit);
    }
    public void Set(string text,string units,Color accent,bool warning)
    {
        Motion.Text(number,text);number.Foreground=Palette.Brush(warning?accent:palette.Ink);
        unit.Text=text=="—"?"":units;unit.Foreground=Palette.Brush(accent);
    }
}

public sealed class ProTile : SummaryTile
{
    private readonly WidgetStyle style;
    private readonly TextBlock label,hardware,detail,badgeText;
    private readonly BoldValue value;
    private readonly ProGraphic graphic;
    private readonly ContentControl glyph=new();
    private readonly Border badge;
    private string glyphKey="";
    public ProTile(WidgetKind kind,int index,Palette p,Metrics m,Settings s,WidgetStyle style)
        : base(kind,index,p,m,s,false)
    {
        this.style=style;Padding=new Thickness(16,12);CornerRadius=new CornerRadius(18);ClipToBounds=true;
        label=Ui.Text("",13,p.Ink,FontWeight.Bold);
        hardware=Ui.Text("",11.5,p.Soft);
        detail=Ui.Text("",12,p.Soft,FontWeight.Medium,true);
        badgeText=Ui.Text("",11.5,p.Ink,FontWeight.DemiBold,true);
        foreach(var text in new[]{label,hardware,detail,badgeText}) { text.TextTrimming=TextTrimming.CharacterEllipsis;text.MaxLines=1; }
        badge=new Border{Child=badgeText,Padding=new Thickness(7,3),CornerRadius=new CornerRadius(6),MaxWidth=126,Margin=new Thickness(8,0,0,0)};
        double size=style switch { WidgetStyle.Prism=>s.Horizontal?54:64,WidgetStyle.RailPro=>s.Horizontal?52:60,WidgetStyle.Spectrum=>s.Horizontal?48:54,_=>s.Horizontal?48:56 };
        if(!m.Subtitles)size-=6;
        value=new BoldValue(size,p);
        var number=new Viewbox{Child=value,StretchDirection=StretchDirection.DownOnly,MaxWidth=style==WidgetStyle.Prism?230:174,HorizontalAlignment=HorizontalAlignment.Left};
        graphic=new ProGraphic{Theme=p,IsHitTestVisible=false,Tag="fixed",Mode=style switch{
            WidgetStyle.RailPro=>ProGraphicMode.Segments,WidgetStyle.OrbitPro=>ProGraphicMode.Orbit,
            WidgetStyle.Prism=>ProGraphicMode.Prism,WidgetStyle.Spectrum=>ProGraphicMode.Bars,_=>ProGraphicMode.Wave}};
        glyph.Width=18;glyph.Height=18;glyph.VerticalAlignment=VerticalAlignment.Center;
        Grid Header() => Ui.Columns("*,Auto",label,badge);
        switch(style) {
            case WidgetStyle.SignalPro:
                graphic.Height=s.Horizontal?68:78;graphic.Margin=new Thickness(12,0,0,0);
                Child=Ui.Stack(6,Header(),Ui.Columns("Auto,*",Ui.Stack(0,number,hardware),graphic));
                break;
            case WidgetStyle.RailPro:
                graphic.Width=52;graphic.Height=s.Horizontal?72:80;graphic.Margin=new Thickness(14,0,0,0);
                Child=Ui.Stack(4,Ui.Columns("*,Auto",Ui.Stack(2,label,hardware),glyph),Ui.Columns("*,Auto",number,graphic),detail);
                break;
            case WidgetStyle.OrbitPro:
                number.MaxWidth=double.PositiveInfinity;number.HorizontalAlignment=HorizontalAlignment.Stretch;number.Margin=new Thickness(0,0,12,0);
                graphic.Width=s.Horizontal?94:104;graphic.Height=s.Horizontal?94:104;
                var orbit=new Panel{Width=graphic.Width,Height=graphic.Height,Children={graphic}};
                glyph.Width=22;glyph.Height=22;glyph.HorizontalAlignment=HorizontalAlignment.Center;
                orbit.Children.Add(glyph);
                Child=Ui.Stack(5,Header(),Ui.Columns("*,Auto",number,orbit),hardware);
                break;
            case WidgetStyle.Prism:
                Padding=new Thickness(0);
                var foreground=new Border{Padding=new Thickness(16,12),Child=Ui.Stack(3,Ui.Columns("*,Auto",label,glyph),number,detail,hardware)};
                Child=new Panel{Children={graphic,foreground}};
                break;
            default:
                graphic.Height=s.Horizontal?34:44;
                Child=Ui.Stack(5,Header(),number,graphic,hardware);
                break;
        }
    }
    protected override void Show(Summary sum)
    {
        var accent=Shade(sum);
        label.Text=Naming.Title(Slot,Index,sum.Title);
        hardware.Text=Naming.Hardware(Slot,Index,sum.Name);
        hardware.IsVisible=Naming.ShowHardware(Slot,S)&&!string.IsNullOrWhiteSpace(hardware.Text);
        ToolTip.SetTip(hardware,hardware.Text);
        string secondary=sum.Second.HasValue&&sum.SecondLabel.Length>0?L.T("Load")+" "+sum.SecondLabel:sum.Caption;
        badgeText.Text=secondary;detail.Text=secondary;badge.IsVisible=secondary.Length>0;
        ToolTip.SetTip(badge,secondary);
        badge.Background=Palette.Brush(Palette.Alpha(accent,P.Dark ? .17 : .12));
        value.Set(sum.Value,sum.Unit,accent,sum.Color==P.Ramp[^1]);
        var key=Summaries.ChartKey(Kind);
        var history=(key==null||S.Shows(key))?sum.Series:Array.Empty<double>();
        graphic.Set(history,sum.SeriesMax,sum.Second??sum.Fraction,accent);
        string next=sum.Glyph+accent;
        if(next!=glyphKey){glyphKey=next;glyph.Content=Ui.Glyph(sum.Glyph,accent,style==WidgetStyle.OrbitPro?22:18);}
    }
}


