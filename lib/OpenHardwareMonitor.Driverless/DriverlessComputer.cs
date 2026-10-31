// Modified Open Hardware Monitor integration, MPL-2.0. See README.md and LICENSE.txt.
using System;
using System.Collections.Generic;
using System.Linq;
using OpenHardwareMonitor.Hardware.Gpu;
using OpenHardwareMonitor.Hardware.Storage;

namespace OpenHardwareMonitor.Hardware;

public interface IComputer : IElement { IList<IHardware> Hardware {get;} }
public delegate void HardwareEventHandler(IHardware hardware);

public interface ISettings
{
    string GetValue(string name,string fallback);
    void SetValue(string name,string value);
    bool Contains(string name);
    void Remove(string name);
}
internal sealed class MemorySettings : ISettings
{
    private readonly Dictionary<string,string> values=new();
    public string GetValue(string name,string fallback)=>values.TryGetValue(name,out var value)?value:fallback;
    public void SetValue(string name,string value)=>values[name]=value;
    public bool Contains(string name)=>values.ContainsKey(name);
    public void Remove(string name)=>values.Remove(name);
}

/// <summary>Vendor API GPU and Windows SMART readers only. CPU/mainboard access is supplied by the host's PawnIO bridge.</summary>
public sealed class DriverlessComputer : IComputer,IDisposable
{
    private readonly List<IGroup> groups=new();
    private readonly List<string> diagnostics=new();
    private bool opened;
    private readonly bool storageOnly;
    public DriverlessComputer(bool storageOnly=false)=>this.storageOnly=storageOnly;
    public IList<IHardware> Hardware=>groups.SelectMany(g=>g.Hardware).ToArray();
    public IReadOnlyList<string> Diagnostics=>diagnostics;
    public void Open()
    {
        if(opened)return;
        var settings=new MemorySettings();
        if(!storageOnly){Add("NVIDIA",()=>new NvidiaGroup(settings));Add("AMD",()=>new AmdGpuGroup(settings));}
        Add("SMART",()=>new StorageGroup(settings));
        opened=true;
    }
    private void Add(string name,Func<IGroup> create)
    {
        try{groups.Add(create());}catch(Exception error){diagnostics.Add(name+": "+error.Message);}
    }
    public void Accept(IVisitor visitor)=>visitor.VisitComputer(this);
    public void Traverse(IVisitor visitor){foreach(var hardware in Hardware)hardware.Accept(visitor);}
    public void Dispose()
    {
        Exception first=null;
        for(int i=groups.Count-1;i>=0;i--){try{groups[i].Close();}catch(Exception error){first??=error;}}
        groups.Clear();opened=false;diagnostics.Clear();
        if(first!=null)throw first;
    }
}

internal static class OperatingSystemHelper
{
    public static bool IsUnix=>false;
    public static bool Is64Bit=>Environment.Is64BitProcess;
    public static bool IsWindows8OrGreater=>OperatingSystem.IsWindowsVersionAtLeast(6,2);
}
