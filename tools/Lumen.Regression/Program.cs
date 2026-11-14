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
foreach(var style in Settings.Selectable)
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
    Widgets=new(){new WidgetSlot{Kind=WidgetKind.Cpu,Style=WidgetStyle.Pill,Page=2,Name="PRIVATE_NAME_TEST"}} };
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
Assert(L.T("Executable")=="Executable", "English path label"); // English only
var location=ExecutableLocation.Create(palette,true);
Assert(location.GetLogicalDescendants().OfType<Button>().Any(b=>b.Content?.ToString()=="Open folder"),"open-folder button");
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
ds.Widgets=new(){new WidgetSlot{Kind=WidgetKind.Cpu,Style=WidgetStyle.Pill},new WidgetSlot{Kind=WidgetKind.System,Style=WidgetStyle.Standard}};
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
foreach(var style in Settings.Selectable)
foreach(var edge in new[]{Edge.Left,Edge.Right,Edge.Top,Edge.Bottom}) {
    var ws=new Settings{Edge=edge,TextScale=1.5,Density=Density.Fit};
    foreach(var kind in Enum.GetValues<WidgetKind>().Where(Settings.HasStyles)) {
        var tile=Cards.Create(kind,style,0,palette,Metrics.For(ws.Density),ws);
        // Native ScrollViewer templates need a visual root to measure their content.
        var measureHost = new Window { Content = tile, Width = 280, Height = 1000, ShowActivated = false, ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual, Position = new PixelPoint(-20000, -20000) };
        measureHost.Show(); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        ((IWidget)tile).Update(demoSnap,hist);tile.Measure(new Size(280,double.PositiveInfinity));tile.Arrange(new Rect(0,0,280,tile.DesiredSize.Height));
        Assert(double.IsFinite(tile.DesiredSize.Height) && tile.DesiredSize.Height>0,$"{style}/{kind}/{edge} bindings");
        measureHost.Close();
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

string fatalDir=Path.Combine(testDir,"fatal-session");
var psi=new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true};
psi.ArgumentList.Add("--fatal-session");psi.ArgumentList.Add(fatalDir);
using(var child=System.Diagnostics.Process.Start(psi)!){Assert(child.WaitForExit(20000)&&child.ExitCode==0,"fatal session fixture");}
using(var fatalMarker=JsonDocument.Parse(File.ReadAllText(Path.Combine(fatalDir,"lumen-session.json"))))
    Assert(!fatalMarker.RootElement.GetProperty("CleanExit").GetBoolean()&&fatalMarker.RootElement.GetProperty("Fatal").GetBoolean(),"fatal session remains unclean");


var cpuSum=Summaries.For(WidgetKind.Cpu,0,demoSnap,hist,new Settings(),palette);
var pair=TwinFocusMetrics.Pair(WidgetKind.Cpu,0,cpuSum,demoSnap,hist,new Settings());
Assert(pair.Primary.Series.SequenceEqual(hist.Read("cpu.temp"))&&pair.Secondary.Series.SequenceEqual(hist.Read("cpu.load")),"Twin Focus has independent temperature/load histories");
var fahrenheitPair=TwinFocusMetrics.Pair(WidgetKind.Cpu,0,cpuSum,demoSnap,hist,new Settings{Fahrenheit=true});
Assert(Math.Abs(fahrenheitPair.Primary.Series.Last()-(hist.Read("cpu.temp").Last()*1.8+32))<.001,"Fahrenheit history converted consistently");
var uploadPair=TwinFocusMetrics.Pair(WidgetKind.Network,0,Summaries.For(WidgetKind.Network,0,demoSnap,hist,new Settings(),palette),demoSnap,hist,new Settings());
Assert(uploadPair.Primary.Series.SequenceEqual(hist.Read("net.down"))&&uploadPair.Secondary.Series.SequenceEqual(hist.Read("net.up")),"network down/up independent histories");

// Standard retains its header readings; TwinFocus retains its two main values.
// (The overview has no header reading; it is a list in every design.)
foreach (var kind in Enum.GetValues<WidgetKind>().Where(Settings.HasStyles).Where(k => k != WidgetKind.Overview))
{
    var tile = (Card)Cards.Create(kind, WidgetStyle.Standard, 0, palette, Metrics.For(Density.Fit), new Settings());
    tile.Update(demoSnap, hist);
    var aside = (TextBlock)typeof(Card).GetField("aside", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(tile)!;
    Assert(aside.Parent != null, $"Standard/{kind}: header reading remains attached");
}
var twinFocus = Cards.Create(WidgetKind.Cpu, WidgetStyle.TwinFocus, 0, palette, Metrics.For(Density.Fit), new Settings());
((IWidget)twinFocus).Update(demoSnap, hist);
Assert(twinFocus.GetLogicalDescendants().OfType<BoldValue>().Count() == 2, "TwinFocus retains both primary value blocks");
foreach (var style in Settings.Selectable.Where(s => s is not (WidgetStyle.Standard or WidgetStyle.TwinFocus) && !AioDesigns.Contains(s)))
// (Clock, calendar, weather, now playing and top apps use InfoTile in every design; InfoWidgetTests cover them.)
foreach (var kind in Enum.GetValues<WidgetKind>().Where(Settings.HasStyles).Where(k => k != WidgetKind.Overview && !InfoTile.Handles(k)))
{
    var tile = (SummaryTile)Cards.Create(kind, style, 0, palette, Metrics.For(Density.Fit), new Settings());
    tile.Update(demoSnap, hist); tile.Update(demoSnap, hist);
    var caption = (TextBlock)typeof(SummaryTile).GetField("Caption", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(tile)!;
    Assert(!caption.IsVisible, $"{style}/{kind}: no header reading after repeated updates");
}
// AIO is a complete Overview card, not a SummaryTile repeated for every hardware widget.
foreach(var style in AioDesigns.Styles)
{
    var config=Settings.Starter();foreach(var slot in config.Widgets)slot.Style=style;
    var only=config.Layout(null,1).SelectMany(c=>c).ToArray();
    Assert(only.Length==1&&only[0].Kind==WidgetKind.Overview,$"{style}: one AIO card on HW register");
    var tile=Cards.Create(WidgetKind.Overview,style,0,palette,Metrics.For(Density.Fit),config);
    ((Lumen.App.INamed)tile).Own(only[0],0);((IWidget)tile).Update(demoSnap,hist);((IWidget)tile).Update(demoSnap,hist);
    foreach(string key in new[]{"aio.cpu.temp","aio.network.down","aio.network.up"})
        Assert(tile.GetLogicalDescendants().OfType<TextBlock>().Count(t=>SensorDetails.KeyOf(t)==key)==1,$"{style}: {key} displayed once");
}
var traffic = demoSnap with { Network = new NetworkInfo("Demo Ethernet", 4_000_000, 750_000, 100_000_000, "192.0.2.1") };
foreach (var hidden in new[] { "", "net.down", "net.up" })
{
    var config = new Settings();
    var slot = config.Widgets.Single(w => w.Kind == WidgetKind.Network);
    if (hidden.Length > 0) slot.HiddenSensors.Add(hidden);
    var tile = (SegmentTile)Cards.Create(WidgetKind.Network, WidgetStyle.Segments, 0, palette, Metrics.For(Density.Fit), config);
    tile.Update(traffic, hist);
    T Field<T>(string field) => (T)typeof(SegmentTile).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(tile)!;
    var downLabel = Field<TextBlock>("firstLabel"); var upLabel = Field<TextBlock>("secondLabel");
    Assert(downLabel.Text == "DL" && upLabel.Text == "UP", "Segments network labels identify both directions");
    Assert(((Control)downLabel.Parent!).IsVisible == (hidden != "net.down"), "Download visibility is independent");
    Assert(Field<Grid>("secondRow").IsVisible == (hidden != "net.up"), "Upload visibility is independent");
    var downMeter = Field<Meter>("first"); var upMeter = Field<Meter>("second");
    Assert(downMeter.Color == palette.Cool && upMeter.Color == palette.Cool && !downMeter.RampBySegment && !upMeter.RampBySegment, "Both traffic segment bars stay blue");
    if (hidden != "net.down") Assert(Field<TextBlock>("firstValue").Text == Format.Rate(4_000_000), "Download uses its own rate");
    if (hidden != "net.up") Assert(Field<TextBlock>("secondValue").Text == Format.Rate(750_000), "Upload uses its own rate");
    if (hidden.Length == 0) Assert(downMeter.Value > upMeter.Value && upMeter.Value > 0, "Both segment bars use independent traffic levels");

    var pill = (PillTile)Cards.Create(WidgetKind.Network, WidgetStyle.Pill, 0, palette, Metrics.For(Density.Fit), config);
    pill.Update(traffic, hist);
    TextBlock PillField(string field) => (TextBlock)typeof(PillTile).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(pill)!;
    var downloadValue = PillField("value"); var uploadValue = PillField("uploadValue");
    Assert(pill.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == "DL") && pill.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == "UP"), "Pill has DL and UP labels");
    Assert((downloadValue.IsVisible && downloadValue.GetLogicalAncestors().OfType<Control>().All(c => c.IsVisible)) == (hidden != "net.down"), "Pill download visibility is independent");
    Assert((uploadValue.IsVisible && uploadValue.GetLogicalAncestors().OfType<Control>().All(c => c.IsVisible)) == (hidden != "net.up"), "Pill upload visibility is independent");
    if (hidden != "net.down") Assert(downloadValue.Text == Format.Split(Format.Rate(4_000_000)).Number, "Pill download uses the correct rate");
    if (hidden != "net.up") Assert(uploadValue.Text == Format.Split(Format.Rate(750_000)).Number, "Pill upload uses the correct rate");
}
var previewGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto"), Background = Palette.Brush(palette.Surface) };
var previewKinds = new[] { WidgetKind.Cpu, WidgetKind.Gpu, WidgetKind.Memory, WidgetKind.Drives, WidgetKind.Network };
for (int column = 0; column < 3; column++)
for (int row = 0; row < previewKinds.Length; row++)
{
    var style = column == 0 ? WidgetStyle.Table : column == 1 ? WidgetStyle.Segments : WidgetStyle.Pill;
    var config = new Settings();
    config.Widgets.Single(w => w.Kind == previewKinds[row]).GraphicScale = .5;
    var tile = Cards.Create(previewKinds[row], style, 0, palette, Metrics.For(Density.Fit), config);
    ((IWidget)tile).Update(traffic, hist); tile.Margin = new Thickness(10);
    Grid.SetColumn(tile, column); Grid.SetRow(tile, row); previewGrid.Children.Add(tile);
}
previewGrid.Measure(new Size(1080, double.PositiveInfinity));
var previewSize = new Size(1080, Math.Ceiling(previewGrid.DesiredSize.Height));
previewGrid.Arrange(new Rect(previewSize));
using (var image = new RenderTargetBitmap(new PixelSize(1080, (int)previewSize.Height)))
{
    image.Render(previewGrid);
    image.Save(Path.Combine(testDir, "table-segments-preview.png"));
    if (Environment.GetEnvironmentVariable("LUMEN_LAYOUT_PREVIEW") is string file) image.Save(file);
}
var labelSettings = new Settings();
var stress = demoSnap with {
    Cpu=demoSnap.Cpu! with { CoreLoads=Enumerable.Range(0,129).Select(i=>(double)(i%101)).ToArray() },
    Disks=Enumerable.Range(0,12).Select(i=>new DiskInfo("stress-"+i,"Long storage device name "+i,40+i,20+i,i*1024,i*2048){Letters="Disk "+i}).ToArray(),
    Fans=Enumerable.Range(0,12).Select(i=>new FanInfo("Fan "+i,i*100,i)).ToArray(),
    BoardTemps=Enumerable.Range(0,10).Select(i=>new TempReading("Board "+i,30+i)).ToArray(),
    Network=new NetworkInfo("Long network adapter name",0,1_234_567_890,100_000_000_000,"2001:db8:1234:5678:90ab:cdef:1234:5678")
};
Assert(CompleteReadings.For(WidgetKind.Cpu,0,stress,hist,new Settings(),palette).Count(r=>r.Key.StartsWith("cpu.core:"))==0,"Individual CPU core rows removed as requested");
Assert(CompleteReadings.For(WidgetKind.Cooling,0,stress,hist,new Settings(),palette).Count==34,"All fans and board temperatures retained");
foreach(var style in ComparisonTile.Styles)
foreach(var kind in Enum.GetValues<WidgetKind>().Where(Settings.HasStyles).Where(k => k != WidgetKind.Overview && !InfoTile.Handles(k)))
foreach(double scale in new[]{.5,1d,2d}) {
    // Duplex lists drives line by line (letter, bar, temperature) as asked; the full reading table does not apply.
    if(style==WidgetStyle.Duplex&&kind==WidgetKind.Drives)continue;
    var config=new Settings();var slot=config.Widgets.Single(w=>w.Kind==kind);slot.ValueScale=slot.LabelScale=slot.GraphicScale=scale;
    if(kind==WidgetKind.Network)slot.HiddenSensors.Remove("net.address");
    var expected=CompleteReadings.Body(kind,0,stress,hist,config.ForWidget(kind),palette,style);
    var tile=(ComparisonTile)Cards.Create(kind,style,0,palette,Metrics.For(Density.Fit),config);
    var window=new Window{Content=tile,Width=320,Height=1000,ShowActivated=false,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual,Position=new PixelPoint(-20000,-20000)};
    tile.Update(stress,hist);window.Show();Avalonia.Threading.Dispatcher.UIThread.RunJobs();tile.Update(stress,hist);Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    Assert(tile.RenderedKeys.SequenceEqual(expected.Select(r=>r.Key)),$"{style}/{kind}/{scale}: all selected readings");
    var texts=tile.GetLogicalDescendants().OfType<TextBlock>().Where(t=>(t.Tag as string)?.StartsWith("complete-reading:")==true).ToArray();
    Assert(texts.Length==expected.Count,"Every reading has a visible text control");
    foreach(var reading in expected){var text=texts.Single(t=>Equals(t.Tag,"complete-reading:"+reading.Key));Assert(text.Text==reading.Value&&text.TextTrimming==Avalonia.Media.TextTrimming.None,"Full value and unit retained");Assert(double.IsFinite(text.Bounds.Width)&&text.Bounds.Width<=tile.Bounds.Width+.1,"Reading width fits widget");}
    if(kind==WidgetKind.Network){Assert(texts.Single(t=>Equals(t.Tag,"complete-reading:net.address")).Text==stress.Network!.Address,"IPv6 retains every character");Assert(texts.Single(t=>Equals(t.Tag,"complete-reading:net.down")).Text==Format.Rate(0),"Valid zero download preserved");}
    window.Close();
}
foreach(var style in ComparisonTile.Styles) {
    var config=new Settings();var slot=config.Widgets.Single(w=>w.Kind==WidgetKind.Cpu);slot.HiddenSensors.Add("cpu.temp");slot.HiddenSensors.Add("cpu.load.history");
    var tile=(ComparisonTile)Cards.Create(WidgetKind.Cpu,style,0,palette,Metrics.For(Density.Fit),config);tile.Update(stress,hist);
    Assert(!tile.RenderedKeys.Contains("cpu.temp"),"Sensor off removes value");Assert(!tile.GetLogicalDescendants().OfType<AreaChart>().Any(c=>c.IsVisible),"Primary hidden and secondary history off hide both plots");
    foreach(var edge in new[]{Edge.Left,Edge.Right,Edge.Top,Edge.Bottom}) {
        var sc=new Settings{Edge=edge,BarHeight=280,Widgets=new(){new(){Kind=WidgetKind.Cpu,Style=style}}};var view=new SidebarView(sc,palette);view.Update(stress,hist);
        Assert(view.GetLogicalDescendants().OfType<FitPanel>().Any(p=>p.ClipToBounds),"Normal automatic fitting is active in all four placements");
    }
}
foreach(var style in ComparisonTile.Styles) {
    double[] heights=new double[2];
    for(int i=0;i<2;i++) {
        var config=new Settings{Density=i==0?Density.Compact:Density.Comfortable};
        var tile=(ComparisonTile)Cards.Create(WidgetKind.Cpu,style,0,palette,Metrics.For(config.Density),config);tile.Update(demoSnap,hist);
        tile.Measure(new Size(300,double.PositiveInfinity));tile.Arrange(new Rect(0,0,300,tile.DesiredSize.Height));heights[i]=tile.DesiredSize.Height;
        Assert(tile.RenderedKeys.Count==6,"Exactly six CPU readings, without individual cores");
        Assert(tile.Padding.Left==Metrics.For(config.Density).Pad,"Density controls native padding");
    }
    Assert(heights[0]<heights[1],$"{style}: Compact uses less height than Comfortable");
    foreach(var density in new[]{Density.Fit,Density.Compact,Density.Comfortable})
    foreach(var edge in new[]{Edge.Left,Edge.Top}) {
        var config=new Settings{Density=density,Edge=edge,BarHeight=200,Widgets=new[]{WidgetKind.Cpu,WidgetKind.Gpu,WidgetKind.Memory,WidgetKind.Network}.Select(k=>new WidgetSlot{Kind=k,Style=style}).ToList()};
        var view=new SidebarView(config,palette);var window=new Window{Content=view,Width=config.Horizontal?1280:300,Height=config.Horizontal?250:density==Density.Fit?340:560,ShowActivated=false,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual,Position=new PixelPoint(-20000,-20000)};
        window.Show();view.Update(demoSnap,hist);for(int pass=0;pass<8;pass++){Avalonia.Threading.Dispatcher.UIThread.RunJobs();window.CaptureRenderedFrame();}
        var tiles=view.GetLogicalDescendants().OfType<ComparisonTile>().ToArray();foreach(var key in new[]{"cpu.temp","gpu.temp","mem.load","net.down"})Assert(tiles.Any(t=>t.RenderedKeys.Contains(key)),"Every selected widget remains present in every density");
        Assert(tiles.All(t=>!t.RenderedKeys.Any(k=>k.StartsWith("cpu.core:"))),"Core rows remain absent in all layouts"); if(density==Density.Fit&&!config.Horizontal)Assert(tiles.All(t=>t.Padding.Left==Metrics.For(Density.Compact).Pad),"Automatic mode actually rebuilds with compact metrics under pressure");
        if(density==Density.Fit || config.Horizontal) {
            foreach(var value in view.GetLogicalDescendants().OfType<TextBlock>().Where(t=>(t.Tag as string)?.StartsWith("complete-reading:")==true)) {
                value.BringIntoView(); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                var bottom=value.TranslatePoint(new Point(0,value.Bounds.Height),view);
                Assert(bottom.HasValue&&bottom.Value.Y<=view.Bounds.Height+1,"Automatic fitting keeps values inside the available height");
            }
        }
        window.Close();
    }
}
foreach(var style in InsightTile.Styles) {
    foreach(var kind in Enum.GetValues<WidgetKind>().Where(Settings.HasStyles).Where(k => k != WidgetKind.Overview && !InfoTile.Handles(k)))
    foreach(double scale in new[]{.5,1d,2d}) {
        var config=new Settings();var slot=config.Widgets.Single(w=>w.Kind==kind);slot.ValueScale=slot.LabelScale=slot.GraphicScale=scale;
    if(kind==WidgetKind.Network)slot.HiddenSensors.Remove("net.address");
        var expected=CompleteReadings.Body(kind,0,stress,hist,config.ForWidget(kind),palette,style);
        var tile=(InsightTile)Cards.Create(kind,style,0,palette,Metrics.For(Density.Fit),config);
        var window=new Window{Content=tile,Width=320,Height=1000,ShowActivated=false,ShowInTaskbar=false,Position=new PixelPoint(-20000,-20000)};
        tile.Update(stress,hist);window.Show();Avalonia.Threading.Dispatcher.UIThread.RunJobs();tile.Update(stress,hist);Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert(tile.RenderedKeys.SequenceEqual(expected.Select(r=>r.Key)),"Insight retains every selected reading in source order");
        Assert(tile.RenderedKeys.Distinct().Count()==tile.RenderedKeys.Count,"No duplicate insight reading keys");
        foreach(var reading in expected){var cell=tile.Readouts.Single(c=>c.Key==reading.Key);Assert(cell.Value==reading.Value,"Full values and units preserved at all scales");Assert(double.IsFinite(cell.ValueControl.Bounds.Width)&&cell.ValueControl.Bounds.Width<=tile.Bounds.Width+.1,"Insight value fits card width");}
        if(kind==WidgetKind.Network){Assert(tile.Readouts.Single(c=>c.Key=="net.address").Value==stress.Network!.Address,"Long IPv6 preserved");Assert(tile.Readouts.Single(c=>c.Key=="net.down").Value==Format.Rate(0),"Zero rate preserved");}
        window.Close();
    }
    double[] heights=new double[2];
    for(int i=0;i<2;i++){
        var config=new Settings{Density=i==0?Density.Compact:Density.Comfortable};var tile=(InsightTile)Cards.Create(WidgetKind.Cpu,style,0,palette,Metrics.For(config.Density),config);tile.Update(demoSnap,hist);tile.Measure(new Size(300,double.PositiveInfinity));heights[i]=tile.DesiredSize.Height;
    }
    Assert(heights[0]<heights[1],"Insight Compact is shorter than Comfortable");
    var hidden=new Settings();hidden.Widgets.Single(w=>w.Kind==WidgetKind.Cpu).HiddenSensors.AddRange(new[]{"cpu.temp","cpu.load.history","cpu.power"});
    var filtered=(InsightTile)Cards.Create(WidgetKind.Cpu,style,0,palette,Metrics.For(Density.Fit),hidden);filtered.Update(stress,hist);
    Assert(!filtered.RenderedKeys.Contains("cpu.temp")&&!filtered.RenderedKeys.Contains("cpu.power"),"Insight honors individual sensor switches");
    Assert(!filtered.GetLogicalDescendants().OfType<AreaChart>().Any(c=>c.IsEffectivelyVisible),"Hidden sensor and history switches hide both trends");
    foreach(double width in new[]{280d,320d}){
        var rates=(InsightTile)Cards.Create(WidgetKind.Network,style,0,palette,Metrics.For(Density.Compact),new Settings());double? stableHeight=null;
        foreach(double rate in new[]{9d,999d,1024d,999_999d,1_000_000d,999_999_999d,10_000_000_000d}){
            rates.Update(stress with{Network=stress.Network! with{DownloadBytesPerSecond=rate}},hist);
            rates.Measure(new Size(width,double.PositiveInfinity));rates.Arrange(new Rect(0,0,width,rates.DesiredSize.Height));
            stableHeight??=rates.DesiredSize.Height;Assert(Math.Abs(stableHeight.Value-rates.DesiredSize.Height)<.1,"Changing traffic digits and units do not move insight rows");
        }
    }
    foreach(var density in new[]{Density.Fit,Density.Compact,Density.Comfortable})
    foreach(var edge in new[]{Edge.Left,Edge.Top})
    foreach(int columns in new[]{1,2,3,4}){
        var config=new Settings{Density=density,Columns=columns,Edge=edge,BarHeight=250,Widgets=new[]{WidgetKind.Cpu,WidgetKind.Gpu,WidgetKind.Memory,WidgetKind.Network}.Select(k=>new WidgetSlot{Kind=k,Style=style}).ToList()};
        var view=new SidebarView(config,palette);var window=new Window{Content=view,Width=config.Horizontal?1280:300*columns,Height=config.Horizontal?300:density==Density.Fit?340:700,ShowActivated=false,ShowInTaskbar=false,Position=new PixelPoint(-20000,-20000)};
        window.Show();view.Update(demoSnap,hist);for(int pass=0;pass<8;pass++){Avalonia.Threading.Dispatcher.UIThread.RunJobs();window.CaptureRenderedFrame();}
        var tiles=view.GetLogicalDescendants().OfType<InsightTile>().ToArray();Assert(tiles.Length>=4,"All four widget kinds present in each density, placement and column count");
        foreach(var tile in tiles){Assert(tile.RenderedKeys.Distinct().Count()==tile.RenderedKeys.Count,"No duplicates after automatic rebuilding");
            if(density==Density.Fit||config.Horizontal)foreach(var cell in tile.Readouts){cell.ValueControl.BringIntoView();Avalonia.Threading.Dispatcher.UIThread.RunJobs();var end=cell.ValueControl.TranslatePoint(new Point(cell.ValueControl.Bounds.Width,cell.ValueControl.Bounds.Height),view);Assert(end.HasValue&&end.Value.Y<=view.Bounds.Height+1&&end.Value.X<=view.Bounds.Width+1,"Normal automatic fitting contains every value");}}
        window.Close();
    }
    foreach(var preset in ThemeFile.BuiltIn)
    foreach(bool dark in new[]{true,false}){
        var sc=new Settings();preset.Theme.ApplyTo(sc);sc.Widgets.Single(w=>w.Kind==WidgetKind.Network).HiddenSensors.Remove("net.address");var themed=Palette.For(sc,dark,null);var tile=(InsightTile)Cards.Create(WidgetKind.Network,style,0,themed,Metrics.For(Density.Compact),sc);tile.Update(stress,hist);tile.Measure(new Size(320,double.PositiveInfinity));tile.Arrange(new Rect(0,0,320,tile.DesiredSize.Height));
        Assert(tile.RenderedKeys.Contains("net.address")&&tile.RenderedKeys.Contains("net.up")&&tile.RenderedKeys.Contains("net.down"),"All eight themes retain complete insight network values in light and dark");
        if(InsightTile.ColorStyles.Contains(style)){
            foreach(string key in new[]{"cpu.temp","cpu.load","cpu.freq","cpu.power","cpu.voltage","cpu.fan","net.down","net.up","net.peak","mem.total"}){
                var color=tile.ReadingColor(key);Assert(color.A==255,"Category foregrounds stay fully opaque");Assert(InsightTile.Contrast(color,themed.Card)>=4.5&&InsightTile.Contrast(color,Palette.Mix(themed.Card,color,.07))>=4.5,$"{preset.Name}/{dark}/{key}: category colour remains legible on plain and tinted surfaces");
                if(sc.Colors=="mono")Assert(color==themed.Ink,"Mono theme does not add category hues");
            }
            var address=tile.Readouts.Single(c=>c.Key=="net.address").ValueControl;Assert(address is TextBlock text&&text.Text==stress.Network!.Address,"Coloured registers keep addresses as unmodified text");
        }
    }
    if(InsightTile.ColorStyles.Contains(style))foreach(string font in new[]{"inter","segoe","segoe-variable","bahnschrift","bahnschrift-narrow","mono"}){
        Ui.UseFont(font);var config=new Settings();var cpu=config.Widgets.Single(w=>w.Kind==WidgetKind.Cpu);cpu.Name="Very long processor title with a complete model designation";cpu.LabelScale=2;cpu.ValueScale=1.5;
        var tile=(InsightTile)Cards.Create(WidgetKind.Cpu,style,0,palette,Metrics.For(Density.Compact),config);tile.Update(stress,hist);tile.Measure(new Size(280,double.PositiveInfinity));tile.Arrange(new Rect(0,0,280,tile.DesiredSize.Height));
        Assert(tile.RenderedKeys.Count==6,"Every supported font keeps all CPU readings at enlarged label and value scales");foreach(var cell in tile.Readouts)Assert(cell.ValueControl.Bounds.Width<=280,"Enlarged font readouts remain within narrow cards");
    }
    Ui.UseFont("inter");
}
// Regression for tall two-column desktops: free height belongs inside new cards.
var fillingSnap=demoSnap with{Gpus=demoSnap.Gpus.Take(1).ToArray(),Disks=demoSnap.Disks.Concat(new[]{demoSnap.Disks[0] with{Id="third-demo",Letters="F:"}}).ToArray()};
foreach(var style in ComparisonTile.Styles.Concat(InsightTile.Styles))
foreach(bool histories in new[]{true,false}){
    // Test surplus space with the recommended starting selection; enabling every optional SMART
    // statistic is a different (overflow) scenario and may legitimately leave no surplus to expand.
    var config=new Settings{Columns=2,Density=Density.Fit,Widgets=new[]{WidgetKind.Cpu,WidgetKind.Gpu,WidgetKind.Memory,WidgetKind.Drives,WidgetKind.Network}.Select(k=>new WidgetSlot{Kind=k,Style=style,HiddenSensors=SensorDefaults.For(k)}).ToList()};
    if(!histories)foreach(var slot in config.Widgets){slot.HiddenSensors.Add(WidgetSensors.PrimaryKey(slot.Kind)+".history");slot.HiddenSensors.Add(WidgetSensors.SecondaryKey(slot.Kind)+".history");}
    var view=new SidebarView(config,palette);var window=new Window{Content=view,Width=630,Height=1028,ShowActivated=false,ShowInTaskbar=false,Position=new PixelPoint(-20000,-20000)};
    window.Show();view.Update(fillingSnap,hist);
    foreach(double height in new[]{1028d,380d,700d,1028d}){
        window.Height=height;for(int pass=0;pass<12;pass++){Avalonia.Threading.Dispatcher.UIThread.RunJobs();window.CaptureRenderedFrame();}
        var fitted=view.GetLogicalDescendants().OfType<FitPanel>().Single();var columns=fitted.Children.OfType<StackPanel>().Where(c=>c.Children.Any(x=>x.IsVisible&&x is IWidget)).ToArray();
        Assert(columns.Length==2,"Automatic layout retains both selected columns");
        foreach(var column in columns){Assert(column.Spacing<=17,"Automatic mode fills cards instead of leaving oversized widget gaps");var last=column.Children.Last(c=>c.IsVisible&&c is IWidget);var bottom=last.TranslatePoint(new Point(0,last.Bounds.Height),fitted)!.Value.Y;Assert(Math.Abs(bottom-fitted.Bounds.Height)<12,$"{style}/{histories}/{height}: column bottom {bottom:0.00}, available {fitted.Bounds.Height:0.00}");}
        var cards=view.GetLogicalDescendants().OfType<SummaryTile>().ToArray();Assert(cards.Length==5,"Resizing retains all five widgets");
        foreach(var card in cards){var keys=card is InsightTile insight?insight.RenderedKeys:((ComparisonTile)card).RenderedKeys;Assert(keys.Count==keys.Distinct().Count(),"Automatic expansion adds no duplicate readings");}
        foreach(var value in view.GetLogicalDescendants().OfType<Control>().Where(c=>c.IsEffectivelyVisible&&((c.Tag as string)?.StartsWith("insight-value:")==true||(c.Tag as string)?.StartsWith("complete-reading:")==true))){var end=value.TranslatePoint(new Point(0,value.Bounds.Height),fitted)!.Value;Assert(end.Y<=fitted.Bounds.Height+1,$"{style}/{histories}/{height}: every value stays inside the viewport");}
        if(height==1028)Assert(cards.Any(c=>c.DesiredSize.Height-fitted.NaturalHeight(c)>30),$"{style}/{histories}: surplus height expands content inside cards");
    }
    window.Close();
}
var elasticRows=new StatPanel(Array.Empty<double>(),Enumerable.Range(0,6).Select(i=>(Control)new TextBlock{Text="Reading "+i}).ToArray()){Tag="elastic",MaxColumns=2};
var elasticList=new StackPanel{Children={elasticRows}};var elasticFit=new FitPanel(new(){elasticList},1,true,10);
elasticFit.Measure(new Size(300,900));elasticFit.Arrange(new Rect(0,0,300,900));Assert(elasticRows.RowSpacing>100,"Single-card row registers absorb spare automatic height");
elasticFit.InvalidateMeasure();elasticFit.Measure(new Size(300,double.PositiveInfinity));Assert(elasticRows.RowSpacing==0,"Unconstrained measurement resets automatic row expansion");
foreach(var retired in Enum.GetValues<WidgetStyle>().Where(s=>s==WidgetStyle.Rings||s.ToString().EndsWith("Remastered"))) {
    var config=new Settings{Notes="Keep notes",Width=345};var slot=config.Widgets.Single(w=>w.Kind==WidgetKind.Cpu);slot.Style=retired;slot.Name="Keep custom CPU";slot.ValueScale=1.35;slot.HiddenSensors.Add("cpu.power");
    string path=Path.Combine(testDir,"retired-style.json");config.Save(path);var loaded=Settings.Load(path);var restored=loaded.Widgets.Single(w=>w.Kind==WidgetKind.Cpu);
    Assert(!Settings.Selectable.Contains(retired),"Removed ring and Remastered designs are unavailable");Assert(restored.Style==WidgetStyle.Standard,"Removed saved designs migrate to Standard");Assert(restored.Name=="Keep custom CPU"&&restored.ValueScale==1.35&&restored.HiddenSensors.Contains("cpu.power")&&loaded.Notes=="Keep notes"&&loaded.Width==345,"All other preferences survive design removal");Assert(Cards.Create(WidgetKind.Cpu,retired,0,palette,Metrics.For(Density.Fit),loaded) is CpuCard,"Retired renderer falls back to native Standard");
}
foreach(var old in new[]{WidgetStyle.LedgerV,WidgetStyle.MatrixV,WidgetStyle.TraceV,WidgetStyle.RailH,WidgetStyle.SplitH,WidgetStyle.BandH})Assert(Settings.Successor(old)==WidgetStyle.Standard,"Discarded designs stay retired");

foreach (double labelScale in new[] { .5, 1d, 2d })
{
    var config = new Settings();
    var slot = config.Widgets.Single(w => w.Kind == WidgetKind.Cpu);
    slot.GraphicScale = .5; slot.ValueScale = .5; slot.LabelScale = labelScale;
    var pill = (PillTile)Cards.Create(WidgetKind.Cpu, WidgetStyle.Pill, 0, palette, Metrics.For(Density.Fit), config);
    pill.Update(demoSnap, hist); pill.Measure(new Size(280, double.PositiveInfinity)); pill.Arrange(new Rect(0, 0, 280, pill.DesiredSize.Height));
    var title = (TextBlock)typeof(PillTile).GetField("name", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(pill)!;
    var surface = pill.GetLogicalDescendants().OfType<Panel>().Single(p => p.Classes.Contains("pill-surface"));
    var position = title.TranslatePoint(new Point(), surface)!.Value;
    Assert(position.Y >= 0 && position.Y + title.Bounds.Height <= surface.Bounds.Height + .1, "Pill processor title stays inside capsule at every label scale");
}
var stableTraffic = (PillTile)Cards.Create(WidgetKind.Network, WidgetStyle.Pill, 0, palette, Metrics.For(Density.Fit), new Settings());
double? trafficWidth = null;
foreach (double rate in new[] { 9d, 999d, 1024d, 999_999d, 1_000_000d, 999_999_999d, 10_000_000_000d })
{
    stableTraffic.Update(traffic with { Network = new NetworkInfo("Demo", rate, rate / 3, 100_000_000_000, "192.0.2.1") }, hist);
    stableTraffic.Measure(new Size(280, double.PositiveInfinity));
    stableTraffic.Arrange(new Rect(0, 0, 280, stableTraffic.DesiredSize.Height));
    var digits = (TextBlock)typeof(PillTile).GetField("value", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(stableTraffic)!;
    var box = (Viewbox)digits.Parent!;
    trafficWidth ??= box.Bounds.Width;
    Assert(Math.Abs(trafficWidth.Value - box.Bounds.Width) < .01, "Traffic digit column stays fixed across digit and unit changes");
    Assert(digits.TextTrimming == Avalonia.Media.TextTrimming.None && box.StretchDirection == Avalonia.Media.StretchDirection.DownOnly, "Long traffic rates shrink without ellipsis");
}
foreach (var kind in new[] { WidgetKind.Cpu, WidgetKind.Gpu })
{
    var card = Cards.Create(kind, WidgetStyle.Standard, 0, palette, Metrics.For(Density.Fit), new Settings());
    ((IWidget)card).Update(demoSnap, hist);
    var heroes = card.GetLogicalDescendants().OfType<Hero>().ToArray();
    Assert(heroes.Length == 2, $"Standard/{kind}: temperature and load use Hero displays");
    Assert(heroes[0].Children.OfType<TextBlock>().First().FontSize == heroes[1].Children.OfType<TextBlock>().First().FontSize, $"Standard/{kind}: temperature and load have the same number size");
}
var labelSlot = labelSettings.Widgets.Single(w => w.Kind == WidgetKind.Cpu);
labelSlot.LabelScale = 2;
labelSlot.HiddenSensors.Add("cpu.core:0");
Settings.Normalize(labelSettings);
Assert(!labelSlot.HiddenSensors.Any(k => k.StartsWith("cpu.core:")), "Legacy per-core switches are cleared");
Assert(!WidgetSensors.Options(WidgetKind.Cpu, demoSnap).Any(x => x.Key.StartsWith("cpu.core:")), "CPU settings omit individual core switches");
var labelTile = Cards.Create(WidgetKind.Cpu, WidgetStyle.Table, 0, palette, Metrics.For(Density.Fit), labelSettings);
((IWidget)labelTile).Update(demoSnap, hist);
var titleText = HardwareWidgetTitle.TitleOf(labelTile) ?? throw new Exception("Native widget title missing");
Assert(Math.Abs(titleText.FontSize - 25) < .001, "Widget title scales to 200 percent");
((IWidget)labelTile).Update(demoSnap, hist);
Assert(Math.Abs(titleText.FontSize - 25) < .001, "Label scale does not accumulate");
labelSlot.LabelScale = .5;
((IWidget)labelTile).Update(demoSnap, hist);
Assert(Math.Abs(titleText.FontSize - 6.25) < .001, "Widget title scales to 50 percent");
string labelPath = Path.Combine(testDir, "label-settings.json");
labelSettings.Save(labelPath);
Assert(Settings.Load(labelPath).Widgets.Single(w => w.Kind == WidgetKind.Cpu).LabelScale == .5, "Label scale persists");
Diagnostics.Complete();
using(var marker=JsonDocument.Parse(File.ReadAllText(Path.Combine(testDir,"lumen-session.json"))))
    Assert(marker.RootElement.GetProperty("CleanExit").GetBoolean(),"clean exit persisted");
Console.WriteLine($"DIAGNOSTICS: {diagnosticsChecks} assertions passed; screenshots: {testDir}");

Console.WriteLine($"RESULT: {cases} cases, {failures} failures");
if(expectFixed && failures>0) Environment.Exit(1);



class FakePlatform : Platform { public override bool AutostartEnabled => false; public override bool IsAdministrator => false; }











sealed class RegressionApp : Application { public override void Initialize() => Styles.Add(new FluentTheme()); }

