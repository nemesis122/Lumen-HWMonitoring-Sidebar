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

public static class TwinFocusMetrics
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
        var primary=new LiveMetric(sum.HeroLabel.Length>0?sum.HeroLabel:sum.Unit.Contains('°')?L.T("Temp"):sum.Unit=="%"?L.T("Load"):sum.Title,
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
            primary=primary with{Level=Summaries.NetFill(s.Network?.DownloadBytesPerSecond,s.Network?.LinkBitsPerSecond)};
            secondary=new LiveMetric(L.T("Upload"),upload.Value,upload.Unit,Summaries.NetFill(s.Network?.UploadBytesPerSecond,s.Network?.LinkBitsPerSecond),up,max);
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

