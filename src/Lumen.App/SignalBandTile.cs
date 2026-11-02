using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed class SignalBandTile : SummaryTile
{
    private readonly TextBlock label, hardware;
    private readonly Hero value;
    private readonly AreaChart trend;
    private readonly Meter meter;
    public SignalBandTile(WidgetKind kind,int index,Palette p,Metrics m,Settings s)
        : base(kind,index,p,m,s,false)
    {
        Padding=new Thickness(14,10);
        label=Ui.Text("",12,p.Soft,FontWeight.DemiBold);
        hardware=Ui.Text("",11,p.Faint);
        foreach(var text in new[]{label,hardware}) {text.TextTrimming=TextTrimming.CharacterEllipsis;text.MaxLines=1;}
        value=new Hero(s.Horizontal?26:32,p);
        var number=new Viewbox{Child=value,StretchDirection=StretchDirection.DownOnly,MaxWidth=148,HorizontalAlignment=HorizontalAlignment.Right};
        var header=Ui.Columns("*,Auto",Ui.Stack(3,label,hardware),number);
        number.Margin=new Thickness(10,0,0,0);
        trend=new AreaChart{Height=s.Horizontal?18:26,LineWidth=1.8,FillOpacity=0.12,Tag="fixed"};
        meter=new Meter{Height=3,Palette=p,VerticalAlignment=VerticalAlignment.Center};
        Child=Ui.Stack(s.Horizontal?6:10,header,new Panel{Height=s.Horizontal?18:26,Children={trend,meter}});
    }
    protected override void Show(Summary sum)
    {
        label.Text=Naming.Title(Slot,Index,sum.Title);
        var name=Naming.Hardware(Slot,Index,sum.Name);
        hardware.Text=Naming.ShowHardware(Slot,S)&&name.Length>0?name:sum.Caption;
        value.Set(sum.Value,sum.Unit,sum.Color);
        var key=Summaries.ChartKey(Kind);
        bool history=sum.Series.Length>1&&(key==null||S.Shows(key));
        trend.IsVisible=history;meter.IsVisible=!history;
        if(history){trend.Max=sum.SeriesMax;trend.Set(sum.Series,Shade(sum));}
        else meter.Set(sum.Second??sum.Fraction,Shade(sum));
    }
}
