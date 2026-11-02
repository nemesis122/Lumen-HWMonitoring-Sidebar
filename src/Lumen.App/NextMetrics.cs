using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed record LiveMetric(string Label,string Value,string Unit,double? Level,double[] Series,double? Maximum);

public static class NextMetrics
{
    public static (string Value,string Unit) Split(string text)
    {
        var match=Regex.Match(text.Trim(),@"^([+\-−]?\d[\d.,/]*)(.*)$");
        return match.Success?(match.Groups[1].Value,match.Groups[2].Value.Trim()):(text,"");
    }
    public static List<(string Label,string Value)> Extras(WidgetKind kind,int index,Snapshot snapshot,History history,Settings settings)
    {
        var items=Summaries.Extras(kind,index,snapshot,history,settings)
            .Where(x=>!string.IsNullOrWhiteSpace(x.Value)&&!x.Value.Contains('—')).ToList();
        if(kind==WidgetKind.Cpu&&settings.Shows("cpu.clock")&&snapshot.Cpu?.ClockMhz is double clock&&double.IsFinite(clock))
            items.Insert(0,(L.T("Clock speed"),Format.Clock(clock)));
        return items;
    }
    public static (LiveMetric Primary,LiveMetric Secondary) Pair(WidgetKind kind,int index,Summary sum,Snapshot s,History h,Settings set)
    {
        var primary=new LiveMetric(sum.Unit.Contains('°')?L.T("Temp"):sum.Unit=="%"?L.T("Load"):sum.Title,
            sum.Value,sum.Unit,sum.Fraction,sum.Series,sum.SeriesMax);
        var secondary=new LiveMetric(L.T("Secondary"),"—","",null,Array.Empty<double>(),null);
        if(sum.Second.HasValue&&sum.SecondLabel.Length>0) {
            var split=Split(sum.SecondLabel);
            secondary=new LiveMetric(L.T("Load"),split.Value,split.Unit,sum.Second,Array.Empty<double>(),100);
        } else {
            var extra=Extras(kind,index,s,h,set).FirstOrDefault();
            if(extra!=default){var split=Split(extra.Value);secondary=new LiveMetric(extra.Label,split.Value,split.Unit,null,Array.Empty<double>(),null);}
        }
        if(kind==WidgetKind.Cpu) {
            var temperatures=h.Read("cpu.temp");if(set.Fahrenheit)temperatures=temperatures.Select(v=>v*1.8+32).ToArray();
            primary=primary with{Series=temperatures,Maximum=set.Fahrenheit?212:100,Level=s.Cpu?.Temperature is double temperature?(set.Fahrenheit?temperature*1.8+32:temperature)/(set.Fahrenheit?212:100):null};
            var load=Split(Format.Percent(s.Cpu?.Load));secondary=new LiveMetric(L.T("Load"),load.Value,load.Unit,s.Cpu?.Load/100,h.Read("cpu.load"),100);
        } else if(kind==WidgetKind.Gpu) {
            var gpu=GpuGroup.Visible(s,set).ElementAtOrDefault(index);
            if(gpu!=null){var temperatures=h.Read("gpu."+gpu.Id+".temp");if(set.Fahrenheit)temperatures=temperatures.Select(v=>v*1.8+32).ToArray();
                primary=primary with{Series=temperatures,Maximum=set.Fahrenheit?212:100,Level=gpu.Temperature is double temperature?(set.Fahrenheit?temperature*1.8+32:temperature)/(set.Fahrenheit?212:100):null};
                var load=Split(Format.Percent(gpu.Load));secondary=new LiveMetric(L.T("Load"),load.Value,load.Unit,gpu.Load/100,h.Read("gpu."+gpu.Id+".load"),100);}
        } else if(kind==WidgetKind.Network) {
            var down=h.Read("net.down");var up=h.Read("net.up");double max=Math.Max(1e-9,down.Concat(up).Where(double.IsFinite).DefaultIfEmpty(1).Max()*1.12);
            primary=primary with{Label=L.T("Download"),Series=down,Maximum=max};
            var upload=Split(Format.Rate(s.Network?.UploadBytesPerSecond));
            secondary=new LiveMetric(L.T("Upload"),upload.Value,upload.Unit,null,up,max);
        } else if(kind==WidgetKind.Memory&&s.Memory!=null&&set.Shows("mem.free")) {
            var free=Split(Format.Gigabytes(s.Memory.AvailableGb));
            secondary=new LiveMetric(L.T("Available"),free.Value,free.Unit,
                s.Memory.TotalGb>0?s.Memory.AvailableGb/s.Memory.TotalGb:null,Array.Empty<double>(),null);
        }
        var chartKey=Summaries.ChartKey(kind);
        if(chartKey!=null&&!set.Shows(chartKey)){primary=primary with{Series=Array.Empty<double>()};secondary=secondary with{Series=Array.Empty<double>()};}
        return (primary,secondary);
    }
}

// All supplied cells are kept. Column count follows width; no mutation during measure.
public sealed class LiveMetricGrid : Panel
{
    private readonly Palette theme;
    private readonly bool compact;
    private int measuredColumns=1;
    private readonly List<(Border Box,TextBlock Label,TextBlock Value)> cells=new();
    public int ReadingCount=>cells.Count;
    public LiveMetricGrid(Palette theme,bool compact=true){this.theme=theme;this.compact=compact;}
    public void Set(IReadOnlyList<(string Label,string Value,double? Percent)> readings)
    {
        if(cells.Count!=readings.Count) {
            Children.Clear();cells.Clear();
            for(int i=0;i<readings.Count;i++) {
                var label=Ui.Text("",compact?9:10.5,theme.Soft,FontWeight.Medium);
                var value=Ui.Text("",compact?13:17,theme.Ink,FontWeight.Bold,true);
                var box=new Border{CornerRadius=new CornerRadius(5),Padding=new Thickness(5,3),Child=Ui.Stack(0,label,new Avalonia.Controls.Viewbox{Child=value,StretchDirection=Avalonia.Media.StretchDirection.DownOnly,HorizontalAlignment=Avalonia.Layout.HorizontalAlignment.Left})};
                cells.Add((box,label,value));Children.Add(box);
            }
        }
        for(int i=0;i<readings.Count;i++) {
            var reading=readings[i];var cell=cells[i];cell.Label.Text=reading.Label;cell.Value.Text=reading.Value;
            bool valid=reading.Percent.HasValue&&double.IsFinite(reading.Percent.Value);
            double fraction=valid?Math.Clamp(reading.Percent!.Value/100,0,1):0;
            var color=theme.At(fraction*.9);
            cell.Box.Background=Palette.Brush(valid?Palette.Alpha(color,theme.Dark ? .10+.30*fraction : .08+.20*fraction):theme.Track);
            ToolTip.SetTip(cell.Box,(compact?L.F("Core {0}",reading.Label):reading.Label)+": "+reading.Value);
        }
    }
    private int Columns(double width)=>Math.Max(1,(int)Math.Floor(Math.Max(1,width)/(compact?43:100)));
    protected override Size MeasureOverride(Size available)
    {
        double width=double.IsFinite(available.Width)?Math.Max(1,available.Width):280;
        int cols=Columns(width);measuredColumns=cols;double cellWidth=Math.Max(1,(width-(cols-1)*4)/cols);
        foreach(var child in Children)child.Measure(new Size(cellWidth,compact?36:44));
        return new Size(width,Children.Count==0?0:Math.Ceiling(Children.Count/(double)cols)*(compact?40:48)-4);
    }
    protected override Size ArrangeOverride(Size final)
    {
        int cols=Math.Max(1,measuredColumns);double cellWidth=Math.Max(1,(final.Width-(cols-1)*4)/cols);
        for(int i=0;i<Children.Count;i++)Children[i].Arrange(new Rect((i%cols)*(cellWidth+4),(i/cols)*(compact?40:48),cellWidth,compact?36:44));
        return final;
    }
}



