using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed class NextTile : SummaryTile
{
    private readonly WidgetStyle style;
    private readonly TextBlock label,hardware,detail,badgeText;
    private readonly Border badge;
    private readonly ContentControl glyph=new(){Width=18,Height=18};
    private readonly BoldValue value,secondValue;
    private readonly TextBlock firstLabel,secondLabel;
    private readonly ProGraphic firstPlot,secondPlot;
    private readonly NextGraphic graphic;
    private readonly LiveMetricGrid metricGrid;
    private readonly TextBlock[] extraLabels=new TextBlock[2],extraValues=new TextBlock[2];
    private readonly RibbonPanel? ribbon;
    private string glyphKey="";
    public NextTile(WidgetKind kind,int index,Palette p,Metrics m,Settings s,WidgetStyle style)
        :base(kind,index,p,m,s,false)
    {
        this.style=style;Padding=new Thickness(16,12);CornerRadius=new CornerRadius(18);ClipToBounds=true;
        label=Ui.Text("",13,p.Ink,FontWeight.Bold);hardware=Ui.Text("",11.5,p.Soft);
        detail=Ui.Text("",12,p.Soft,FontWeight.Medium,true);badgeText=Ui.Text("",11.5,p.Ink,FontWeight.DemiBold,true);
        foreach(var text in new[]{label,hardware,detail,badgeText})text.MaxLines=1;
        badge=new Border{Child=badgeText,Padding=new Thickness(7,3),CornerRadius=new CornerRadius(6),MaxWidth=128,Margin=new Thickness(8,0,0,0)};
        double size=style==WidgetStyle.Liquid?60:style==WidgetStyle.TwinFocus?44:style==WidgetStyle.Ribbon?48:52;
        if(!m.Subtitles)size-=4;
        value=new BoldValue(size,p);secondValue=new BoldValue(size,p);
        var number=new Viewbox{Child=value,StretchDirection=StretchDirection.DownOnly,MaxWidth=250,HorizontalAlignment=HorizontalAlignment.Left};
        firstLabel=Ui.Text("",11.5,p.Soft);secondLabel=Ui.Text("",11.5,p.Soft);
        firstPlot=new ProGraphic{Theme=p,Mode=ProGraphicMode.Wave,Height=28,Tag="fixed",IsHitTestVisible=false};
        secondPlot=new ProGraphic{Theme=p,Mode=ProGraphicMode.Wave,Height=28,Tag="fixed",IsHitTestVisible=false};
        metricGrid=new LiveMetricGrid(p,kind==WidgetKind.Cpu);
        graphic=new NextGraphic{Theme=p,Mode=style switch{WidgetStyle.Contour=>NextGraphicMode.Contour,WidgetStyle.SplitEdge=>NextGraphicMode.Split,WidgetStyle.Ribbon=>NextGraphicMode.Ribbon,_=>NextGraphicMode.Liquid},IsHitTestVisible=false};
        Grid Header()=>Ui.Columns("*,Auto",label,badge);
        switch(style) {
            case WidgetStyle.Liquid:
                Padding=new Thickness(0);
                Child=new Panel{Children={graphic,new Border{Padding=new Thickness(16,12),Child=Ui.Stack(3,Ui.Columns("*,Auto",label,glyph),number,detail,hardware)}}};
                break;
            case WidgetStyle.TwinFocus:
                var secondNumber=new Viewbox{Child=secondValue,StretchDirection=StretchDirection.DownOnly,HorizontalAlignment=HorizontalAlignment.Stretch};
                number.MaxWidth=double.PositiveInfinity;number.HorizontalAlignment=HorizontalAlignment.Stretch;
                var a=Ui.Stack(2,firstLabel,number,firstPlot);a.Margin=new Thickness(0,0,10,0);
                var b=Ui.Stack(2,secondLabel,secondNumber,secondPlot);b.Margin=new Thickness(10,0,0,0);
                var divider=new Border{Width=1,Background=Palette.Brush(p.Track),Margin=new Thickness(0,6)};
                Child=Ui.Stack(8,Ui.Columns("*,Auto",label,glyph),Ui.Columns("*,1,*",a,divider,b),hardware);
                break;
            case WidgetStyle.Contour:
                number.MaxWidth=double.PositiveInfinity;number.HorizontalAlignment=HorizontalAlignment.Stretch;
                number.VerticalAlignment=VerticalAlignment.Center;number.Margin=new Thickness(26,16);
                Child=Ui.Stack(6,Header(),new Panel{Height=104,Children={graphic,number}},hardware);
                break;
            case WidgetStyle.SplitEdge:
                Padding=new Thickness(0);number.HorizontalAlignment=HorizontalAlignment.Stretch;number.Margin=new Thickness(0,0,14,0);
                var extras=new StackPanel{Spacing=8,Margin=new Thickness(14,0,0,0),VerticalAlignment=VerticalAlignment.Center};
                for(int i=0;i<2;i++){extraLabels[i]=Ui.Text("",10.5,p.Soft);extraValues[i]=Ui.Text("—",18,p.Ink,FontWeight.Bold,true);extraLabels[i].MaxLines=1;extraValues[i].MaxLines=1;extras.Children.Add(Ui.Stack(1,extraLabels[i],extraValues[i]));}
                var body=Ui.Columns("*,*",number,extras);body.MinHeight=80;
                Child=new Panel{Children={graphic,new Border{Padding=new Thickness(16,12),Child=Ui.Stack(7,Header(),body,hardware)}}};
                break;
            case WidgetStyle.Ribbon:
                number.MaxWidth=150;number.HorizontalAlignment=HorizontalAlignment.Stretch;
                ribbon=new RibbonPanel(graphic,number);Child=Ui.Stack(5,Header(),ribbon,hardware);
                break;
            default:
                Child=Ui.Stack(5,Header(),number,metricGrid,detail,hardware);
                break;
        }
    }
    protected override void Show(Summary sum)
    {
        Color accent=Shade(sum);
        label.Text=Naming.Title(Slot,Index,sum.Title);
        hardware.Text=Naming.Hardware(Slot,Index,sum.Name);hardware.IsVisible=Naming.ShowHardware(Slot,S)&&hardware.Text.Length>0;
        ToolTip.SetTip(hardware,hardware.Text);
        string secondary=sum.Second.HasValue&&sum.SecondLabel.Length>0?L.T("Load")+" "+sum.SecondLabel:sum.Caption;
        detail.Text=secondary;badgeText.Text=secondary;badge.IsVisible=secondary.Length>0;
        badge.Background=Palette.Brush(Palette.Alpha(accent,P.Dark ? .17 : .12));ToolTip.SetTip(badge,secondary);
        if(style!=WidgetStyle.CoreGrid || Kind!=WidgetKind.Cpu) value.Set(sum.Value,sum.Unit,accent,sum.Color==P.Ramp[^1]);
        var pair=NextMetrics.Pair(Kind,Index,sum,Last.Snap,Last.History,S);
        var key=Summaries.ChartKey(Kind);
        var history=(key==null||S.Shows(key))?sum.Series:Array.Empty<double>();
        graphic.Set(style==WidgetStyle.Ribbon?pair.Primary.Series:history,style==WidgetStyle.Ribbon?pair.Primary.Maximum:sum.SeriesMax,style==WidgetStyle.Ribbon?(pair.Primary.Level??0):(sum.Second??sum.Fraction),accent);
        ribbon?.InvalidateArrange();
        if(style==WidgetStyle.TwinFocus) {
            firstLabel.Text=pair.Primary.Label;secondLabel.Text=pair.Secondary.Label;
            secondValue.Set(pair.Secondary.Value,pair.Secondary.Unit,P.Cool,false);
            firstPlot.Set(pair.Primary.Series,pair.Primary.Maximum,pair.Primary.Level??0,accent);
            secondPlot.Set(pair.Secondary.Series,pair.Secondary.Maximum,pair.Secondary.Level??0,P.Cool);
            firstPlot.IsVisible=pair.Primary.Series.Any(double.IsFinite)||pair.Primary.Level.HasValue;
            secondPlot.IsVisible=pair.Secondary.Series.Any(double.IsFinite)||pair.Secondary.Level.HasValue;
            ToolTip.SetTip(secondValue,pair.Secondary.Label+": "+pair.Secondary.Value+" "+pair.Secondary.Unit);
        } else if(style==WidgetStyle.SplitEdge) {
            var extras=NextMetrics.Extras(Kind,Index,Last.Snap,Last.History,S).Take(2).ToList();
            for(int i=0;i<2;i++){extraLabels[i].Text=i<extras.Count?extras[i].Label:"";extraValues[i].Text=i<extras.Count?extras[i].Value:"—";ToolTip.SetTip(extraValues[i],extraValues[i].Text);}
        } else if(style==WidgetStyle.CoreGrid) {
            var readings=new List<(string Label,string Value,double? Percent)>();
            if(Kind==WidgetKind.Cpu) {
                var cpu=Last.Snap.Cpu;var aggregate=NextMetrics.Split(Format.Percent(cpu?.Load));
                value.Set(aggregate.Value,aggregate.Unit,accent,false);
                var loads=cpu?.CoreLoads??Array.Empty<double>();
                if(cpu!=null&&S.Shows("cpu.cores"))for(int i=0;i<loads.Count;i++) {
                    double load=loads[i];bool valid=double.IsFinite(load)&&load>=0&&load<=100;readings.Add(((i+1).ToString(L.Culture),Format.Percent(valid?load:null),valid?load:null));
                }
                detail.Text=!S.Shows("cpu.cores")?L.T("Core readings hidden"):readings.Count==0?L.T("No core readings"):L.F("{0} cores",readings.Count);
                badgeText.Text=(sum.Value+" "+(sum.Value=="—"?"":sum.Unit)).Trim();badge.IsVisible=true;
            } else {
                if(sum.Second.HasValue)readings.Add((L.T("Load"),sum.SecondLabel,sum.Second.Value*100));
                readings.AddRange(NextMetrics.Extras(Kind,Index,Last.Snap,Last.History,S).Select(x=>(x.Label,x.Value,(double?)null)));
                detail.Text=readings.Count==0?L.T("No additional readings"):"";
            }
            metricGrid.Set(readings);detail.IsVisible=detail.Text.Length>0;
        }
        var next=sum.Glyph+accent;if(next!=glyphKey){glyphKey=next;glyph.Content=Ui.Glyph(sum.Glyph,accent,18);}
    }
}



