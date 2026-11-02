using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using System.IO.Compression;
using System.Text.Json;
using System.Reflection;
using Avalonia.Media.Imaging;
using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Lumen.App;
using Lumen.Core;

if(args.Length==2 && args[0]=="--fatal-session") {
    Diagnostics.Initialize(args[1]);Diagnostics.RecordException(new Exception("fatal fixture"),"fatal");Diagnostics.Complete();return;
}
AppBuilder.Configure<RegressionApp>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions{UseHeadlessDrawing=false}).WithInterFont().SetupWithoutStarting();
Motion.Enabled=false;
bool expectFixed = args.Contains("--expect-fixed");
int failures=0, cases=0;
void Check(Control root, double height, string name) {
    cases++;
    try {
        double settled=double.NaN;
        for (int i=0;i<100;i++) {
            foreach(var fit in root.GetLogicalDescendants().OfType<FitPanel>()) fit.InvalidateMeasure();
            root.InvalidateMeasure(); root.Measure(new Size(1920,height)); root.Arrange(new Rect(0,0,1920,height));
            var current=root.GetLogicalDescendants().OfType<StackPanel>().Select(p=>p.Spacing).DefaultIfEmpty(0).Max();
            if(i==90) settled=current;
            if(i>90 && Math.Abs(current-settled)>0.5) throw new Exception($"Spacing keeps changing: {settled} -> {current}");
        }
        var max = root.GetLogicalDescendants().OfType<StackPanel>().Select(p => p.Spacing).DefaultIfEmpty(0).Max();
        if(!double.IsFinite(max) || max>2000) throw new Exception($"Unbounded spacing: {max}");
    } catch(Exception e) { failures++; Console.WriteLine($"FAIL {name}: {e.GetType().Name}: {e.Message}"); }
}
foreach(var style in new[]{WidgetStyle.MiniArc, WidgetStyle.Ticker, WidgetStyle.Status, WidgetStyle.Compact,WidgetStyle.SignalBand,WidgetStyle.Rail,WidgetStyle.PillDuo,WidgetStyle.SignalPro,WidgetStyle.RailPro,WidgetStyle.OrbitPro,WidgetStyle.Prism,WidgetStyle.Spectrum,WidgetStyle.PillDuoPlus,WidgetStyle.Liquid,WidgetStyle.TwinFocus,WidgetStyle.Contour,WidgetStyle.SplitEdge,WidgetStyle.Ribbon,WidgetStyle.CoreGrid})
foreach(var edge in new[]{Edge.Top,Edge.Bottom})
foreach(double height in new[]{64.0, 140.0, 280.0})
foreach(double scale in new[]{1.0,1.5,2.0})
foreach(var density in new[]{Density.Fit,Density.Compact}) {
    var s=new Settings { Edge=edge, BarHeight=height, TextScale=scale, Density=density, Pages=2,
        Widgets=new[]{WidgetKind.Cpu,WidgetKind.Gpu,WidgetKind.Memory,WidgetKind.Network}.Select(k=>new WidgetSlot{Kind=k, Style=style}).ToList() };
    var m=Metrics.For(density);
    var cells=s.Widgets.Select(w=>new StackPanel { Children={Cards.Create(w.Kind,style,0,Palette.For(true,true),m,s)} }).ToList();
    Check(new FitPanel(cells,scale,true,m.Gap),height,$"{style}/{edge}/{height}/{scale}/{density}");
}
Check(new FitPanel(new(){new StackPanel()},1.5,true,10),280,"empty cell");
Check(new FitPanel(new(){new StackPanel{Children={new Border{Height=40,IsVisible=false}}}},1.5,true,10),280,"hidden cell");
Check(new FitPanel(new(){new StackPanel{Children={new Border{Height=40},new Border{Height=40}}}},1.5,true,10),280,"two cards");
int diagnosticsChecks=0;
void Assert(bool ok,string label) { if(!ok) throw new Exception("DIAGNOSTICS FAILED: "+label); diagnosticsChecks++; }
string testDir=Path.GetFullPath("work/dt-"+Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(testDir);
File.WriteAllText(Path.Combine(testDir,"lumen-session.json"),"{\"CleanExit\":false}");
Diagnostics.Initialize(testDir);
var ds=new Settings { Edge=Edge.Top, BarHeight=280, TextScale=1.5, Pages=2,
    Notes="PRIVATE_NOTE_TEST",WeatherPlace="PRIVATE_PLACE_TEST",PingTarget="PRIVATE_TARGET_TEST",
    Labels=new(){{"cpu.load","PRIVATE_LABEL_TEST"},{"gpu.temp","-"}},
    Widgets=new(){new WidgetSlot{Kind=WidgetKind.Cpu,Style=WidgetStyle.Ticker,Page=2,Name="PRIVATE_NAME_TEST"}} };
Diagnostics.SetSettingsPath(Path.Combine(testDir,"lumen-settings.json"));
Diagnostics.SetLayout(ds,2,"test layout");
Assert(File.ReadAllText(Path.Combine(testDir,"lumen-activity.log")).Contains("previous-session"),"unclean session detected");
File.WriteAllText(Path.Combine(testDir,"lumen-activity.log"),new string('x',1_048_600));
Diagnostics.SetLayout(ds,2,"changed layout");
Assert(File.Exists(Path.Combine(testDir,"lumen-activity.log.1")),"bounded log rotation");
var error=new InvalidOperationException("diagnostic sentinel",new ArgumentException("inner sentinel"));
CrashLog.Write(error,"ui-test");
Assert(CrashLog.LastPath!=null,"crash log written");
File.WriteAllText(Path.Combine(testDir,"lumen-restarts.txt"),"restart fixture");
string archive=ErrorReport.Create(ds,"sensor fixture",new[]{"1920x1080"},testDir);
using(var zip=ZipFile.OpenRead(archive)) {
    string Read(string name) { using var r=new StreamReader(zip.GetEntry(name)!.Open());return r.ReadToEnd(); }
    Assert(Read("system.txt").Contains(Platform.ExePath),"executable path in system report");
    Assert(Read("system.txt").Contains(Diagnostics.Build),"build marker in report");
    Assert(Read("lumen-crash.log").Contains("inner sentinel") && Read("lumen-crash.log").Contains("register: 2/2"),"stack, inner error and layout context");
    Assert(zip.GetEntry("lumen-activity.log")!=null && zip.GetEntry("lumen-previous-session.json")!=null,"session and activity included");
    Assert(Read("lumen-restarts.txt").Contains("restart fixture"),"restart history included");
    string settings=Read("settings.json");
    Assert(!settings.Contains("PRIVATE_"),"custom notes, labels, names and targets removed");
    Assert(settings.Contains("\"-\""),"hidden-label marker preserved");
}
string second=ErrorReport.Create(ds,"",Array.Empty<string>(),testDir);
Assert(archive!=second && File.Exists(archive),"reports never overwrite each other");
using(var locked=new FileStream(Path.Combine(testDir,"lumen-restarts.txt"),FileMode.Open,FileAccess.ReadWrite,FileShare.None)) {
    string lockedArchive=ErrorReport.Create(ds,"",Array.Empty<string>(),testDir);
    using var zip=ZipFile.OpenRead(lockedArchive);using var reader=new StreamReader(zip.GetEntry("report-status.txt")!.Open());
    Assert(reader.ReadToEnd().Contains("READ FAILED"),"read failures distinguished from missing logs");
}
L.Use("de");
var palette=Palette.For(true,false);
var sysCard=new SystemCard(palette,Metrics.For(Density.Fit),ds);
Assert(sysCard.GetLogicalDescendants().OfType<TextBlock>().Any(t=>t.Text==Platform.ExePath),"path in system widget");
Assert(L.T("Executable")=="Programmdatei", "German path label");
var location=ExecutableLocation.Create(palette,true);
Assert(location.GetLogicalDescendants().OfType<Button>().Any(b=>b.Content?.ToString()=="Ordner öffnen"),"open-folder button");
typeof(SettingsWindow).GetField("pageIndex",BindingFlags.NonPublic|BindingFlags.Static)!.SetValue(null,5);
var sw=new SettingsWindow(ds,palette,()=>{},()=>new[]{"Test monitor"},new FakePlatform{SettingsPath=Path.Combine(testDir,"lumen-settings.json")});
Assert(sw.GetLogicalDescendants().OfType<TextBlock>().Any(t=>t.Text==Platform.ExePath),"path in System settings");
sw.ShowInTaskbar=false;sw.ShowActivated=false;sw.WindowStartupLocation=Avalonia.Controls.WindowStartupLocation.Manual;
sw.Position=new PixelPoint(-20000,-20000);sw.Show();
Avalonia.Threading.Dispatcher.UIThread.RunJobs();
var content=(Control)sw.Content!;
content.Measure(new Size(940,780));content.Arrange(new Rect(0,0,940,780));
using(var bitmap=new RenderTargetBitmap(new PixelSize(940,780))) {bitmap.Render(content);bitmap.Save(Path.Combine(testDir,"settings-system.png"));}
sw.Close();
ds.Pages=1;
ds.Widgets=new(){new WidgetSlot{Kind=WidgetKind.Cpu,Style=WidgetStyle.Ticker},new WidgetSlot{Kind=WidgetKind.System,Style=WidgetStyle.Standard}};
foreach(var edge in new[]{Edge.Left,Edge.Top}) {
    ds.Edge=edge;var view=new SidebarView(ds,palette);var window=new Window{Content=view,Width=edge==Edge.Top?1600:350,Height=edge==Edge.Top?280:900,ShowActivated=false,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual,Position=new PixelPoint(-20000,-20000)};
    window.Show();view.Update(new DemoSource().Read(),new History());Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    Assert(view.GetLogicalDescendants().OfType<TextBlock>().Any(t=>t.Text==Platform.ExePath),"visible path in "+edge+" sidebar");
    var size=new Size(window.Width,window.Height);view.Measure(size);view.Arrange(new Rect(size));
    using(var bitmap=new RenderTargetBitmap(new PixelSize((int)size.Width,(int)size.Height))){bitmap.Render(view);bitmap.Save(Path.Combine(testDir,$"sidebar-{edge}.png"));}
    window.Close();
}
var hist=new History();var demoSource=new DemoSource();Snapshot demoSnap=demoSource.Read();
for(int i=0;i<60;i++){demoSnap=demoSource.Read();hist.Record(demoSnap);}
foreach(var style in new[]{WidgetStyle.SignalBand,WidgetStyle.Rail,WidgetStyle.PillDuo,WidgetStyle.SignalPro,WidgetStyle.RailPro,WidgetStyle.OrbitPro,WidgetStyle.Prism,WidgetStyle.Spectrum,WidgetStyle.PillDuoPlus,WidgetStyle.Liquid,WidgetStyle.TwinFocus,WidgetStyle.Contour,WidgetStyle.SplitEdge,WidgetStyle.Ribbon,WidgetStyle.CoreGrid})
foreach(var edge in new[]{Edge.Left,Edge.Right,Edge.Top,Edge.Bottom}) {
    var ws=new Settings{Edge=edge,TextScale=1.5,Density=Density.Fit};
    foreach(var kind in Enum.GetValues<WidgetKind>().Where(Settings.HasStyles)) {
        var tile=Cards.Create(kind,style,0,palette,Metrics.For(ws.Density),ws);
        ((IWidget)tile).Update(demoSnap,hist);tile.Measure(new Size(280,double.PositiveInfinity));tile.Arrange(new Rect(0,0,280,tile.DesiredSize.Height));
        Assert(double.IsFinite(tile.DesiredSize.Height) && tile.DesiredSize.Height>0,$"{style}/{kind}/{edge} bindings");
    }
    var config=new Settings{Edge=edge,Widgets=new(){new WidgetSlot{Kind=WidgetKind.Cpu,Style=style}}};
    string configPath=Path.Combine(testDir,$"{style}-{edge}.json");config.Save(configPath);
    Assert(Settings.Load(configPath).Widgets.First(w=>w.Kind==WidgetKind.Cpu).Style==style,"style persistence "+style);
    if(edge==Edge.Right || edge==Edge.Bottom) continue;
    ws.Widgets=new[]{WidgetKind.Cpu,WidgetKind.Gpu,WidgetKind.Memory,WidgetKind.Network}.Select(k=>new WidgetSlot{Kind=k,Style=style}).ToList();
    ws.TextScale=1;
    var view=new SidebarView(ws,palette);var win=new Window{Content=view,Width=edge==Edge.Top?1400:320,Height=edge==Edge.Top?240:1000,ShowActivated=false,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual,Position=new PixelPoint(-20000,-20000)};
    win.Show();view.Update(demoSnap,hist);Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    var extent=new Size(win.Width,win.Height);view.Measure(extent);view.Arrange(new Rect(extent));
    using(var image=new RenderTargetBitmap(new PixelSize((int)extent.Width,(int)extent.Height))){image.Render(view);image.Save(Path.Combine(testDir,$"{style}-{edge}.png"));}
    win.Close();
}

var duo=new PillDuoTile(WidgetKind.Cpu,0,palette,Metrics.For(Density.Fit),new Settings());
duo.Update(demoSnap,hist);duo.Measure(new Size(280,double.PositiveInfinity));duo.Arrange(new Rect(0,0,280,duo.DesiredSize.Height));
var lower=(Meter)typeof(PillDuoTile).GetField("lower",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(duo)!;
var icon=(Border)typeof(PillDuoTile).GetField("icon",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(duo)!;
var lowerPoint=lower.TranslatePoint(new Point(),duo)!.Value;var iconPoint=icon.TranslatePoint(new Point(),duo)!.Value;
Assert(lowerPoint.X>=iconPoint.X+icon.Bounds.Width,"duo secondary indicator clears icon");
Assert(lowerPoint.Y+lower.Bounds.Height<=duo.Bounds.Height,"duo indicator stays inside pill");
string fatalDir=Path.Combine(testDir,"fatal-session");
var psi=new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true};
psi.ArgumentList.Add("--fatal-session");psi.ArgumentList.Add(fatalDir);
using(var child=System.Diagnostics.Process.Start(psi)!){Assert(child.WaitForExit(20000)&&child.ExitCode==0,"fatal session fixture");}
using(var fatalMarker=JsonDocument.Parse(File.ReadAllText(Path.Combine(fatalDir,"lumen-session.json"))))
    Assert(!fatalMarker.RootElement.GetProperty("CleanExit").GetBoolean()&&fatalMarker.RootElement.GetProperty("Fatal").GetBoolean(),"fatal session remains unclean");


// New designs are additions: legacy types and enum names remain available.
foreach(var style in new[]{WidgetStyle.SignalPro,WidgetStyle.RailPro,WidgetStyle.OrbitPro,WidgetStyle.Prism,WidgetStyle.Spectrum}) {
    foreach(double width in new[]{190d,280d,440d}) {
        var control=Cards.Create(WidgetKind.Network,style,0,palette,Metrics.For(Density.Fit),new Settings());
        ((IWidget)control).Update(demoSnap,hist);control.Measure(new Size(width,double.PositiveInfinity));control.Arrange(new Rect(0,0,width,control.DesiredSize.Height));
        var graph=control.GetLogicalDescendants().OfType<ProGraphic>().Single();
        if(style==WidgetStyle.OrbitPro) {
            var digits=control.GetLogicalDescendants().OfType<BoldValue>().Single();
            double right=digits.TranslatePoint(new Point(digits.Bounds.Width,0),control)!.Value.X;
            double left=graph.TranslatePoint(new Point(),control)!.Value.X;
            Assert(right<=left+1,$"Orbit number/graphic separation at {width}px: {right} / {left}");
        }
    }
}
var plusSettings=new Settings{HiddenValues=new(){"cpu.clock","cpu.power","cpu.voltage","cpu.fan"}};
var plus=new PillDuoPlusTile(WidgetKind.Cpu,0,palette,Metrics.For(Density.Fit),plusSettings);plus.Update(demoSnap,hist);
var extraCapsule=(Border)typeof(PillDuoPlusTile).GetField("extraCapsule",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(plus)!;
Assert(!extraCapsule.IsVisible,"Plus respects hidden extra metrics");
plusSettings.HiddenValues.Clear();plus.Update(demoSnap,hist);Assert(extraCapsule.IsVisible,"Plus shows available extra metrics");
foreach(bool dark in new[]{true,false}) {
    var theme=Palette.For(dark,false);
    var sheet=new Grid{ColumnDefinitions=new ColumnDefinitions("*,*,*"),RowDefinitions=new RowDefinitions("Auto,Auto"),Background=Palette.Brush(theme.Surface),Margin=new Thickness(0)};
    var all=new[]{WidgetStyle.Liquid,WidgetStyle.TwinFocus,WidgetStyle.Contour,WidgetStyle.SplitEdge,WidgetStyle.Ribbon,WidgetStyle.CoreGrid};
    for(int i=0;i<all.Length;i++) {
        var card=Cards.Create(WidgetKind.Cpu,all[i],0,theme,Metrics.For(Density.Fit),new Settings());
        ((IWidget)card).Update(demoSnap,hist);
        var stack=Ui.Stack(8,Ui.Text(L.T(all[i].ToString()),13,theme.Soft,Avalonia.Media.FontWeight.DemiBold),card);
        stack.Margin=new Thickness(14,12);Grid.SetColumn(stack,i%3);Grid.SetRow(stack,i/3);sheet.Children.Add(stack);
    }
    var preview=new Window{Content=sheet,Width=1060,Height=580,ShowActivated=false,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual,Position=new PixelPoint(-20000,-20000)};
    preview.Show();Avalonia.Threading.Dispatcher.UIThread.RunJobs();sheet.Measure(new Size(1060,580));sheet.Arrange(new Rect(0,0,1060,580));
    using(var image=new RenderTargetBitmap(new PixelSize(1060,580))){image.Render(sheet);image.Save(Path.Combine(testDir,$"modern-designs-{(dark?"dark":"light")}.png"));}
    preview.Close();
    foreach(ProGraphicMode mode in Enum.GetValues<ProGraphicMode>()) {
        var graph=new ProGraphic{Theme=theme,Mode=mode,Width=110,Height=80};
        graph.Set(new[]{double.NaN,double.PositiveInfinity,-20d,0d,20d},double.NaN,double.NaN,theme.Cool);
        graph.Measure(new Size(110,80));graph.Arrange(new Rect(0,0,110,80));
        using var image=new RenderTargetBitmap(new PixelSize(110,80));image.Render(graph);
        Assert(true,$"finite-safe {mode} render in {(dark?"dark":"light")}");
    }
}

var nextStyles=new[]{WidgetStyle.Liquid,WidgetStyle.TwinFocus,WidgetStyle.Contour,WidgetStyle.SplitEdge,WidgetStyle.Ribbon,WidgetStyle.CoreGrid};
var missing=new Snapshot(DateTime.Now,null,Array.Empty<GpuInfo>(),null,Array.Empty<DiskInfo>(),null,SensorAccess.Limited);
foreach(var style in nextStyles) {
    var noData=Cards.Create(WidgetKind.Cpu,style,0,palette,Metrics.For(Density.Fit),new Settings());
    ((IWidget)noData).Update(missing,new History());noData.Measure(new Size(190,double.PositiveInfinity));noData.Arrange(new Rect(0,0,190,noData.DesiredSize.Height));
    Assert(double.IsFinite(noData.DesiredSize.Height),"missing sensor handling "+style);
}
foreach(int count in new[]{1,24,64,129}) {
    var loads=Enumerable.Range(0,count).Select(i=>(double)(i%101)).ToArray();
    var snap=demoSnap with{Cpu=demoSnap.Cpu! with{CoreLoads=loads,Load=31}};
    var gridTile=new NextTile(WidgetKind.Cpu,0,palette,Metrics.For(Density.Fit),new Settings(),WidgetStyle.CoreGrid);
    gridTile.Update(snap,hist);gridTile.Measure(new Size(280,double.PositiveInfinity));gridTile.Arrange(new Rect(0,0,280,gridTile.DesiredSize.Height));
    var grid=gridTile.GetLogicalDescendants().OfType<LiveMetricGrid>().Single();
    Assert(grid.ReadingCount==count,$"all {count} CPU core readings retained");
    foreach(var box in grid.Children) Assert(box.Bounds.Right<=grid.Bounds.Width+1 && box.Bounds.Bottom<=grid.Bounds.Height+1,"core cell within measured grid");
    var digits=gridTile.GetLogicalDescendants().OfType<BoldValue>().First();
    Assert(digits.Children.OfType<TextBlock>().First().Text=="31","Core Grid displays aggregate load, not temperature");
}
var hiddenCore=new NextTile(WidgetKind.Cpu,0,palette,Metrics.For(Density.Fit),new Settings{HiddenValues=new(){"cpu.cores"}},WidgetStyle.CoreGrid);
hiddenCore.Update(demoSnap,hist);Assert(hiddenCore.GetLogicalDescendants().OfType<LiveMetricGrid>().Single().ReadingCount==0,"core visibility preference");
var cpuSum=Summaries.For(WidgetKind.Cpu,0,demoSnap,hist,new Settings(),palette);
var pair=NextMetrics.Pair(WidgetKind.Cpu,0,cpuSum,demoSnap,hist,new Settings());
Assert(pair.Primary.Series.SequenceEqual(hist.Read("cpu.temp"))&&pair.Secondary.Series.SequenceEqual(hist.Read("cpu.load")),"Twin Focus has independent temperature/load histories");
var fahrenheitPair=NextMetrics.Pair(WidgetKind.Cpu,0,cpuSum,demoSnap,hist,new Settings{Fahrenheit=true});
Assert(Math.Abs(fahrenheitPair.Primary.Series.Last()-(hist.Read("cpu.temp").Last()*1.8+32))<.001,"Fahrenheit history converted consistently");
var uploadPair=NextMetrics.Pair(WidgetKind.Network,0,Summaries.For(WidgetKind.Network,0,demoSnap,hist,new Settings(),palette),demoSnap,hist,new Settings());
Assert(uploadPair.Primary.Series.SequenceEqual(hist.Read("net.down"))&&uploadPair.Secondary.Series.SequenceEqual(hist.Read("net.up")),"network down/up independent histories");
foreach(double width in new[]{190d,280d,440d}) {
    var ribbonTile=new NextTile(WidgetKind.Network,0,palette,Metrics.For(Density.Fit),new Settings(),WidgetStyle.Ribbon);
    ribbonTile.Update(demoSnap,hist);ribbonTile.Measure(new Size(width,double.PositiveInfinity));ribbonTile.Arrange(new Rect(0,0,width,ribbonTile.DesiredSize.Height));
    var panel=ribbonTile.GetLogicalDescendants().OfType<RibbonPanel>().Single();var graph=panel.Children.OfType<NextGraphic>().Single();
    var digits=panel.Children.First(c=>c is not NextGraphic);var end=graph.EndPoint(panel.Bounds.Size);
    Assert(digits.Bounds.Left>=end.X+7 && digits.Bounds.Right<=panel.Bounds.Width+1,$"Ribbon endpoint value does not overlap at {width}");
}
foreach(var dark in new[]{true,false})foreach(NextGraphicMode mode in Enum.GetValues<NextGraphicMode>()) {
    var graph=new NextGraphic{Theme=Palette.For(dark,false),Mode=mode,Width=160,Height=100};
    graph.Set(new[]{double.NaN,0d,20d,double.PositiveInfinity,30d,double.NaN},double.NaN,double.NaN,palette.Cool);
    graph.Measure(new Size(160,100));graph.Arrange(new Rect(0,0,160,100));
    using var bitmap=new RenderTargetBitmap(new PixelSize(160,100));bitmap.Render(graph);
    Assert(true,"finite-safe next graphic "+mode);
}

Diagnostics.Complete();
using(var marker=JsonDocument.Parse(File.ReadAllText(Path.Combine(testDir,"lumen-session.json"))))
    Assert(marker.RootElement.GetProperty("CleanExit").GetBoolean(),"clean exit persisted");
Console.WriteLine($"DIAGNOSTICS: {diagnosticsChecks} assertions passed; screenshots: {testDir}");

Console.WriteLine($"RESULT: {cases} cases, {failures} failures");
if(expectFixed && failures>0) Environment.Exit(1);



class FakePlatform : Platform { public override bool AutostartEnabled => false; public override bool IsAdministrator => false; }











sealed class RegressionApp : Application { public override void Initialize() => Styles.Add(new FluentTheme()); }

