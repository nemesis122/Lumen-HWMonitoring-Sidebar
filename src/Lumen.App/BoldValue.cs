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

