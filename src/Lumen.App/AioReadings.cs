using System.Text.RegularExpressions;
using Lumen.Core;

namespace Lumen.App;

public sealed record AioReading(string Key, string Group, string Label, string Value, double? Number,
    double Maximum, string History = "", string Device = "")
{
    public double? Fraction => Number.HasValue && double.IsFinite(Number.Value) ? Math.Clamp(Number.Value / Maximum,0,1) : null;
}

/// <summary>Live values only. Driver-provided identities survive ordering, equal values and missing sensors.</summary>
public static class AioReadings
{
    public static readonly (string Key,string Label)[] Groups =
    {
        ("aio.cpu","CPU"), ("aio.gpu","GPU"), ("aio.cooling","Cooling"),
        ("aio.rails","Voltages"), ("aio.system","System"), ("aio.drives","Drives"), ("aio.network","Network")
    };
    public static bool Selected(Settings settings,string key) => settings.Shows(key) && AioDesigns.SwitchedOn(settings,key)
        && Groups.Where(g => key.StartsWith(g.Key + ".")).All(g => settings.Shows(g.Key));

    public static List<AioReading> For(Snapshot s, Settings settings)
    {
        lock(KnownFans)foreach(var known in settings.KnownFans)KnownFans.Add(known);
        var rows = new List<AioReading>();
        string Temp(double? n) => n.HasValue && double.IsFinite(n.Value) ? Format.Temperature(n,settings.Fahrenheit)+" "+Format.TemperatureUnit(settings.Fahrenheit) : "—";
        void Add(string key,string group,string label,string value,double? number = null,double max = 100,string history = "",string device = "")
        {
            if (Selected(settings,key)) rows.Add(new(key,group,label,value,number.HasValue && double.IsFinite(number.Value) ? number : null,max,history,device));
        }
        var c=s.Cpu;
        Add("aio.cpu.temp","CPU","Temp.",Temp(c?.Temperature),c?.Temperature,100,"cpu.temp");
        Add("aio.cpu.load","CPU","Load",Format.Percent(c?.Load),c?.Load,100,"cpu.load");
        Add("aio.cpu.clock","CPU","Clock",Format.Clock(c?.ClockMhz),c?.ClockMhz,6000);
        Add("aio.cpu.voltage","CPU","Vcore",Volts(c?.Voltage),c?.Voltage,2);
        Add("aio.cpu.power","CPU","Package",Format.Watts(c?.PowerW),c?.PowerW,250);
        var nodes=Flatten(s.HardwareSensors).ToList();
        foreach (var node in nodes.Where(n=>n.Type=="Cpu"))
            foreach(var sensor in node.Sensors.Where(v=>v.Type=="Temperature" && Regex.IsMatch(v.Name,@"(?:Core|Kern)\s*#?\s*\d+",RegexOptions.IgnoreCase)
                && !Regex.IsMatch(v.Name,"Max|Average|Distance|Tj",RegexOptions.IgnoreCase)).OrderBy(v=>v.Index))
                Add("aio.cpu.core."+Identity(node,sensor),"Cores",sensor.Name,Temp(sensor.Value),sensor.Value,100);
        // Only the main graphics card by default; a further card appears when it is ticked in the settings.
        var visibleGpus=GpuGroup.Visible(s,settings);var mainGpu=GpuGroup.Main(visibleGpus);
        var gpus=visibleGpus.Where(g=>g==mainGpu||listingAllFans||Selected(settings,GpuGroup.ExtraKey(g))).OrderBy(g=>g==mainGpu?0:1).ToArray();
        foreach(var g in gpus)
        {
            string k="aio.gpu."+g.Id+".";
            var node=nodes.FirstOrDefault(n=>n.IsGpu && (n.Id==g.Id || n.Name==g.Name));
            var v=node?.All.Where(x=>x.Type=="Voltage" && Regex.IsMatch(x.Name,"Core|GPU|VDDC",RegexOptions.IgnoreCase)).FirstOrDefault();
            // Integrated graphics often have no temperature or voltage sensors at all: those rows are left out
            // instead of showing an empty big number ("— °C"); the load then becomes the main value.
            bool none(double? x)=>g.Integrated&&!x.HasValue;
            if(!none(g.Temperature))Add(k+"temp","GPU","Temp.",Temp(g.Temperature),g.Temperature,100,"gpu."+g.Id+".temp",g.Id);
            if(!none(g.HotSpot))Add(k+"hotspot","GPU","Hotspot",Temp(g.HotSpot),g.HotSpot,110,device:g.Id);
            if(!none(g.MemoryJunction))Add(k+"junction","GPU","VRAM Temp.",Temp(g.MemoryJunction),g.MemoryJunction,110,device:g.Id);
            Add(k+"load","GPU","Load",Format.Percent(g.Load),g.Load,100,"gpu."+g.Id+".load",g.Id);
            if(!none(v?.Value))Add(k+"voltage","GPU","Voltage",Volts(v?.Value),v?.Value,2,device:g.Id);
            Add(k+"power","GPU","Power",Format.Watts(g.PowerW),g.PowerW,450,"aio.power."+g.Id,g.Id);
            Add(k+"clock","GPU","Clock",g.ClockMhz.HasValue?Format.Number(g.ClockMhz)+" MHz":"—",g.ClockMhz,3500,device:g.Id);
            Add(k+"vram","GPU","VRAM",g.MemoryUsedMb.HasValue && g.MemoryTotalMb.HasValue ? Format.Number(g.MemoryUsedMb/1024,"0.#")+" / "+Format.Gigabytes(g.MemoryTotalMb/1024) : "—",g.MemoryUsedMb,Math.Max(1,g.MemoryTotalMb??16384),device:g.Id);
        }
        var fans=new List<(string Id,string Name,double? Rpm)>();
        var skippedFans=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // An empty fan header and a stopped fan both read 0 rpm. A board fan therefore counts as existing once it
        // has turned (remembered in the settings, also across restarts) and is then shown even at 0 rpm.
        // GPU fans are only reported for cards that have them, so they are always shown. A fan the user
        // ticked under Customise is always shown too.
        bool FanExists(string id,double rpm)
        {
            lock(KnownFans)
            {
                if(rpm>0){KnownFans.Add(id);if(!settings.KnownFans.Contains(id))settings.KnownFans.Add(id);}
                if(KnownFans.Contains(id)||settings.KnownFans.Contains(id))return true;
            }
            return listingAllFans||settings.ActiveWidget?.AioEnabledSensors?.Contains("aio.cooling."+id)==true;
        }
        foreach(var node in nodes)
            foreach(var sensor in node.Sensors.Where(v=>v.Type=="Fan"))
            {
                if(sensor.Value is not double rpm||!double.IsFinite(rpm)||rpm<0)continue;
                string id=Identity(node,sensor);
                if(node.IsGpu&&!gpus.Any(g=>g.Id==node.Id||g.Name==node.Name))continue; // fans of a card that is not shown
                if(!node.IsGpu&&!FanExists(id,rpm)){skippedFans.Add(sensor.Name.Trim());continue;}
                fans.Add((id,node.IsGpu && gpus.Length>1 ? node.Name+" · "+sensor.Name : sensor.Name,sensor.Value));
            }
        // The merged fan list (also from Open Hardware Monitor) follows the same rule and never brings back an
        // empty header that was left out above.
        foreach(var fan in s.Fans)
            if(double.IsFinite(fan.Rpm)&&fan.Rpm>=0&&!skippedFans.Contains(fan.Name.Trim())&&!fans.Any(f=>f.Name.Equals(fan.Name,StringComparison.OrdinalIgnoreCase))
               &&FanExists("merged:"+fan.Name,fan.Rpm))
                fans.Add(("merged:"+fan.Name,fan.Name,fan.Rpm));
        if(c?.FanRpm is double cpuRpm && double.IsFinite(cpuRpm) && cpuRpm>=0 && FanExists("cpu",cpuRpm) && !fans.Any(f=>f.Name.Contains("CPU",StringComparison.OrdinalIgnoreCase))) fans.Add(("cpu","CPU",c.FanRpm));
        foreach(var g in gpus)
            if(g.FanRpm.HasValue && !nodes.Any(n=>n.IsGpu&&(n.Id==g.Id||n.Name==g.Name)&&n.All.Any(v=>v.Type=="Fan"))
                && !fans.Any(f=>f.Id.Contains(g.Id,StringComparison.OrdinalIgnoreCase)||f.Name.Contains(g.Name,StringComparison.OrdinalIgnoreCase)))
                fans.Add(("gpu:"+g.Id,g.Name,g.FanRpm));
        foreach(var f in fans.DistinctBy(f=>f.Id))
            Add("aio.cooling."+f.Id,"Cooling",Names.Fan(f.Name,settings),Format.Rpm(f.Rpm),f.Rpm,3000);
        foreach(var (id,name) in new[]{("12","+12 V"),("5","+5 V"),("3.3","+3.3 V")})
        {
            var voltage=nodes.Where(n=>!n.IsGpu && n.Type!="Cpu").SelectMany(n=>n.Sensors).FirstOrDefault(v=>v.Type=="Voltage" && Rail(v.Name,id));
            Add("aio.rails."+id,"Rails",name,Volts(voltage?.Value),voltage?.Value,id=="12"?13:id=="5"?5.5:3.6);
        }
        var m=s.Memory;
        Add("aio.system.ram","System","RAM",m==null?"—":Format.Number(m.UsedGb,"0.#")+" / "+Format.Gigabytes(m.TotalGb),m?.Load,100,"mem.load");
        var drives=DriveOrdering.Order(s.Disks,DriveOrdering.Mode(settings.ForWidget(WidgetKind.Drives)));
        foreach(var d in drives)
            Add("aio.system.disk."+d.Id,"System",s.Disks.Count==1?"SSD":(d.Letters.Length>0?d.Letters:d.Name),Temp(d.Temperature),d.Temperature,100,"disk."+d.Id+".temp",d.Id);
        // Transfer rates and fill level per drive: the same identities the Drives widget records in its history.
        foreach(var d in drives)
        {
            string k="aio.drives."+d.Id+".";
            Add(k+"read","Drives","Read",Format.Rate(d.ReadBytesPerSecond),d.ReadBytesPerSecond,DriveRateMaximum,"disk."+d.Id+".read",d.Id);
            Add(k+"write","Drives","Write",Format.Rate(d.WriteBytesPerSecond),d.WriteBytesPerSecond,DriveRateMaximum,"disk."+d.Id+".write",d.Id);
            Add(k+"used","Drives","Used",Format.Percent(d.UsedPercent),d.UsedPercent,100,"",d.Id);
        }
        Add("aio.network.down","Network","Download",Format.Bits(s.Network?.DownloadBytesPerSecond*8),s.Network?.DownloadBytesPerSecond,1250000000,"net.down");
        Add("aio.network.up","Network","Upload",Format.Bits(s.Network?.UploadBytesPerSecond*8),s.Network?.UploadBytesPerSecond,1250000000,"net.up");
        Add("aio.network.link","Network","Link speed",Format.Link(s.Network?.LinkBitsPerSecond),s.Network?.LinkBitsPerSecond,10_000_000_000);
        return rows;
    }
    private const double DriveRateMaximum=2_000_000_000;
    /// <summary>Drive caption as the user sees it: letters, else model; a single drive is "SSD".</summary>
    public static string DriveName(Snapshot s,string id)
    {
        var d=s.Disks.FirstOrDefault(x=>x.Id==id);if(d==null)return id;
        return d.Letters.Length>0?d.Letters:s.Disks.Count==1?"SSD":d.Name;
    }
    private static readonly HashSet<string> KnownFans=new();
    [ThreadStatic] private static bool listingAllFans;
    private static string Volts(double? value)=>value.HasValue&&double.IsFinite(value.Value)?Format.Number(value,"0.000")+" V":"—";

    private static bool Rail(string name,string rail)
    {
        string normalized=name.Replace(" ","").Replace(",",".").TrimStart('+').ToLowerInvariant();
        return normalized==rail+"v" || normalized=="v"+rail || normalized==rail;
    }
    private static string Identity(SensorNode node,SensorValue sensor) => sensor.Id.Length>0 ? sensor.Id : node.Id+":"+sensor.Type+":"+sensor.Index+":"+sensor.Name;
    private static IEnumerable<SensorNode> Flatten(IEnumerable<SensorNode> nodes) => nodes.SelectMany(n=>new[]{n}.Concat(Flatten(n.Sub)));

    public static IEnumerable<(string Key,string Label)> Options(Snapshot? snapshot)
    {
        foreach(var g in Groups) yield return (g.Key,"AIO · "+L.T(g.Label));
        yield return ("aio.graphs","AIO · "+L.T("History chart"));
        yield return ("aio.cpu.coreloads","AIO · CPU · "+L.T("Core load"));
        yield return ("aio.cpu.cores","AIO · "+L.T("Core temperatures"));
        if(snapshot==null)yield break;
        var cards=GpuGroup.Visible(snapshot,new Settings());var main=GpuGroup.Main(cards);
        int number=2;
        foreach(var g in cards.Where(g=>g!=main))yield return (GpuGroup.ExtraKey(g),"AIO · GPU "+number+++" · "+g.Name);
        // The choices list every fan that reports a reading – also one that never turned, so it can be ticked.
        List<AioReading> all;
        listingAllFans=true;try{all=For(snapshot,new Settings());}finally{listingAllFans=false;}
        foreach(var r in all.Where(r=>r.Group!="Cores"))
                yield return (r.Key,r.Group=="Drives"?"AIO · "+L.T("Drives")+" · "+DriveName(snapshot,r.Device)+" · "+L.T(r.Label):"AIO · "+r.Group+" · "+L.T(r.Label));
    }
}
