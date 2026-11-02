using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed class RailTile : SummaryTile
{
    private readonly Border rail;
    private readonly TextBlock label,hardware,detail;
    private readonly Hero value;
    private readonly Meter meter;
    public RailTile(WidgetKind kind,int index,Palette p,Metrics m,Settings s)
        : base(kind,index,p,m,s,false)
    {
        Padding=new Thickness(0);ClipToBounds=true;
        rail=new Border{Background=Palette.Brush(p.Cool)};
        label=Ui.Text("",12,p.Ink,FontWeight.DemiBold);
        hardware=Ui.Text("",11,p.Soft);
        detail=Ui.Text("",11,p.Soft);
        foreach(var text in new[]{label,hardware,detail}){text.TextTrimming=TextTrimming.CharacterEllipsis;text.MaxLines=1;}
        value=new Hero(s.Horizontal?27:34,p);
        var number=new Viewbox{Child=value,MaxWidth=160,StretchDirection=StretchDirection.DownOnly,HorizontalAlignment=HorizontalAlignment.Left};
        var heading=Ui.Stack(3,label,hardware);
        meter=new Meter{Height=3,Palette=p};
        Control main;
        if(s.Horizontal){number.HorizontalAlignment=HorizontalAlignment.Right;number.Margin=new Thickness(12,0,0,0);main=Ui.Columns("*,Auto",heading,number);}
        else main=Ui.Stack(8,heading,number);
        var body=new Border{Padding=new Thickness(14,10),Child=Ui.Stack(s.Horizontal?6:8,main,detail,meter)};
        var grid=new Grid{ColumnDefinitions=new ColumnDefinitions(s.Horizontal?"*":"3,*"),RowDefinitions=new RowDefinitions(s.Horizontal?"3,*":"*")};
        if(s.Horizontal) Grid.SetRow(body,1);else Grid.SetColumn(body,1);
        grid.Children.Add(rail);grid.Children.Add(body);Child=grid;
    }
    protected override void Show(Summary sum)
    {
        label.Text=Naming.Title(Slot,Index,sum.Title);
        hardware.Text=Naming.Hardware(Slot,Index,sum.Name);
        hardware.IsVisible=Naming.ShowHardware(Slot,S)&&hardware.Text.Length>0;
        detail.Text=sum.Second.HasValue&&sum.SecondLabel.Length>0?L.T("Load")+" "+sum.SecondLabel:sum.Caption;
        detail.IsVisible=detail.Text.Length>0;
        value.Set(sum.Value,sum.Unit,sum.Color);
        rail.Background=Palette.Brush(Shade(sum));
        meter.Set(sum.Second??sum.Fraction,sum.Second.HasValue?P.Cool:Shade(sum));
    }
}
