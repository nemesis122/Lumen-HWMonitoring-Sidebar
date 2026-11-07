using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Lumen.App;
using Lumen.Core;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

string output=Path.GetFullPath(args.FirstOrDefault()??"docs/images/styles-3.0.0");
Directory.CreateDirectory(output);
AppBuilder.Configure<GalleryApp>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions{UseHeadlessDrawing=false}).WithInterFont().SetupWithoutStarting();
Motion.Enabled=false;L.Use("en");Ui.UseFont("inter");
using var source=new DemoSource();var history=new History();Snapshot snapshot=source.Read();
for(int i=0;i<160;i++){snapshot=source.Read();history.Record(snapshot);}
var styles=Settings.Selectable.ToArray();
string version=typeof(Settings).Assembly.GetName().Version!.ToString(3);
string Name(WidgetStyle style)=>style.ToString();
Control Card(WidgetStyle style,Palette palette) {
    var settings=new Settings{Language="en",Glass=false,ShowNames=true};
    var card=Cards.Create(WidgetKind.Cpu,style,0,palette,Metrics.For(Density.Fit),settings);
    ((IWidget)card).Update(snapshot,history);return card;
}
void Save(Control content,string name,double width,double? height=null,Palette? theme=null) {
    content.Measure(new Size(width,double.PositiveInfinity));
    double h=height??Math.Max(1,Math.Ceiling(content.DesiredSize.Height));
    var window=new Window{Content=content,Width=width,Height=h,Background=Palette.Brush((theme??Palette.For(true,false)).Surface)};
    window.Show();
    if(!height.HasValue) for(int pass=0;pass<3;pass++){Dispatcher.UIThread.RunJobs();window.CaptureRenderedFrame();content.InvalidateMeasure();content.Measure(new Size(width,double.PositiveInfinity));double measured=Math.Ceiling(content.DesiredSize.Height)+20;if(Math.Abs(window.Height-measured)<1)break;window.Height=measured;}
    for(int i=0;i<3;i++){Dispatcher.UIThread.RunJobs();window.CaptureRenderedFrame();}
    using var image=window.CaptureRenderedFrame()??throw new InvalidOperationException("No rendered frame: "+name);
    image.Save(Path.Combine(output,name+".png"));window.Close();
}
if(args.Contains("--autofit-verify")) {
    L.Use("de");var p=Palette.For(true,false);
    var demo=snapshot with{Gpus=snapshot.Gpus.Take(1).ToArray(),Disks=snapshot.Disks.Concat(new[]{snapshot.Disks[0] with{Id="third-demo",Letters="F:",Name="Demo SSD 4 TB"}}).ToArray()};
    foreach(var style in ComparisonTile.Styles.Concat(InsightTile.Styles)){
        var config=new Settings{Language="de",Glass=false,Density=Density.Fit,Columns=2,Widgets=new[]{WidgetKind.Cpu,WidgetKind.Gpu,WidgetKind.Memory,WidgetKind.Drives,WidgetKind.Network}.Select(k=>new WidgetSlot{Kind=k,Style=style}).ToList()};
        var view=new SidebarView(config,p);view.Update(demo,history);
        Save(view,"autofit-two-columns-"+style,630,1028,p);
    }
    Console.WriteLine("Eight native automatic two-column previews rendered with demo data.");return;
}
if(args.Contains("--insights-only") || args.Contains("--color-registers")) {
    L.Use("de");var p=Palette.For(true,false);var designs=args.Contains("--color-registers")?InsightTile.ColorStyles:new[]{WidgetStyle.Clarity,WidgetStyle.ControlDeck};
    var board=new Grid{ColumnDefinitions=new ColumnDefinitions(string.Join(",",designs.Select(_=>"*")))};
    foreach(var style in designs){
        var sc=new Settings{Language="de",Glass=false};
        var column=Ui.Stack(8,Ui.Text(L.T(style.ToString()),23,p.Ink,FontWeight.Bold),Ui.Text("Native Vorschau · Demodaten",10,p.Soft));
        foreach(var kind in new[]{WidgetKind.Cpu,WidgetKind.Gpu,WidgetKind.Memory,WidgetKind.Drives,WidgetKind.Network}){
            var card=Cards.Create(kind,style,0,p,Metrics.For(Density.Comfortable),sc);((IWidget)card).Update(snapshot,history);column.Children.Add(card);
        }
        var border=new Border{Padding=new Thickness(12),Child=column};Grid.SetColumn(border,Array.IndexOf(designs,style));board.Children.Add(border);
    }
    Save(board,args.Contains("--color-registers")?"klartext-farben-vorschau":"klartext-leitstand-vorschau",360*designs.Length,theme:p);
    foreach(var style in designs){
        var densities=new Grid{ColumnDefinitions=new ColumnDefinitions("*,*,*")};int col=0;
        foreach(var density in new[]{Density.Fit,Density.Compact,Density.Comfortable}){
            var sc=new Settings{Language="de",Glass=false,Density=density,Widgets=new[]{WidgetKind.Cpu,WidgetKind.Gpu,WidgetKind.Memory,WidgetKind.Network}.Select(k=>new WidgetSlot{Kind=k,Style=style}).ToList()};
            var view=new SidebarView(sc,p);view.Update(snapshot,history);
            var column=Ui.Stack(8,Ui.Text(L.T(density.ToString()),14,p.Soft),view);view.Height=850;
            Grid.SetColumn(column,col++);densities.Children.Add(column);
        }
        Save(densities,"density-"+style,960,900,p);
        foreach(double scale in new[]{.5,2d}){
            var sc=new Settings{Language="de",Glass=false};var slot=sc.Widgets.Single(w=>w.Kind==WidgetKind.Network);slot.Style=style;slot.ValueScale=slot.LabelScale=slot.GraphicScale=scale;
            var card=Cards.Create(WidgetKind.Network,style,0,p,Metrics.For(Density.Comfortable),sc);((IWidget)card).Update(snapshot,history);
            Save(new Border{Padding=new Thickness(12),Child=Ui.Stack(5,Ui.Text(L.T(style.ToString())+" · "+scale*100+"% · Demo",12,p.Soft),card)},"scale-"+style+"-"+scale*100,320,theme:p);
        }
    }
    foreach(bool dark in new[]{true,false}){
        var paletteNew=Palette.For(dark,false);
        foreach(var style in designs)Save(new Border{Padding=new Thickness(12),Child=Card(style,paletteNew)},$"card-{style}-{(dark?"dark":"light")}",360,theme:paletteNew);
        for(int page=0;page<2;page++){
            var themes=new Grid{ColumnDefinitions=new ColumnDefinitions("*,*,*,*")};
            for(int col=0;col<4;col++){
                var preset=ThemeFile.BuiltIn[page*4+col];var sc=new Settings{Language="de"};preset.Theme.ApplyTo(sc);Ui.UseFont(sc.Font);var themed=Palette.For(sc,dark,null);
                var column=Ui.Stack(8,Ui.Text(L.T(preset.Name)+" · Demo",20,themed.Ink,FontWeight.Bold));
                foreach(var style in designs){column.Children.Add(Ui.Text(L.T(style.ToString()),13,themed.Soft));foreach(var kind in new[]{WidgetKind.Cpu,WidgetKind.Network}){var card=Cards.Create(kind,style,0,themed,Metrics.For(Density.Compact),sc);((IWidget)card).Update(snapshot,history);column.Children.Add(card);}}
                var border=new Border{Padding=new Thickness(12),Background=Palette.Brush(themed.Surface),Child=column};Grid.SetColumn(border,col);themes.Children.Add(border);
            }
            Save(themes,$"insight-themes-{page+1}-{(dark?"dark":"light")}",1360,theme:paletteNew);
        }
    }
    var settingsCheck=new Settings{Language="de"};var settingsUi=new SettingsWindow(settingsCheck,p,()=>{},()=>new[]{"Demo"},new GalleryPlatform(),snapshot:()=>snapshot);
    var parts=(Control[])typeof(SettingsWindow).GetMethod("WidgetsPage",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(settingsUi,null)!;
    var picker=parts.SelectMany(c=>c.GetLogicalDescendants().Prepend(c)).OfType<Avalonia.Controls.Primitives.UniformGrid>().Single();
    if(picker.Children.Count!=18)throw new Exception("Design picker must offer exactly 18 styles");
    foreach(var style in designs){var button=picker.Children.OfType<Button>().Single(b=>b.GetLogicalDescendants().OfType<TextBlock>().Any(t=>t.Text==L.T(style.ToString())));button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));if(settingsCheck.Widgets.Where(w=>Settings.HasStyles(w.Kind)).Any(w=>w.Style!=style))throw new Exception("Global design choice failed");}
    settingsUi.Width=1000;settingsUi.Height=900;settingsUi.Show();for(int i=0;i<3;i++){Dispatcher.UIThread.RunJobs();settingsUi.CaptureRenderedFrame();}
    using(var frame=settingsUi.CaptureRenderedFrame()!)frame.Save(Path.Combine(output,"settings-design-library.png"));settingsUi.Close();
    var list=styles.Select(s=>new{Id=s.ToString(),Name=Name(s),Dark=$"card-{s}-dark.png",Light=$"card-{s}-light.png"}).ToArray();
    File.WriteAllText(Path.Combine(output,"manifest.json"),JsonSerializer.Serialize(new{Version=version,Count=list.Length,Styles=list},new JsonSerializerOptions{WriteIndented=true}));
    Console.WriteLine("Native insight previews rendered.");return;
}
if(args.Contains("--themes-only")) {
    L.Use("de");int checks=0;
    void Verify(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
    double Lum(Color c){double Linear(double v)=>v<=.04045?v/12.92:Math.Pow((v+.055)/1.055,2.4);return .2126*Linear(c.R/255d)+.7152*Linear(c.G/255d)+.0722*Linear(c.B/255d);}
    double Contrast(Color a,Color b){double x=Lum(a),y=Lum(b);return (Math.Max(x,y)+.05)/(Math.Min(x,y)+.05);}
    var fresh=ThemeFile.BuiltIn.Skip(4).ToArray();Verify(ThemeFile.BuiltIn.Length==8&&fresh.Length==4,"Exactly four additional themes");
    foreach(var preset in ThemeFile.BuiltIn) {
        var sc=new Settings{TextScale=1.3,Notes="Keep notes"};var slot=sc.Widgets.Single(w=>w.Kind==WidgetKind.Cpu);slot.Style=WidgetStyle.Table;slot.ValueScale=1.4;slot.HiddenSensors.Add("cpu.power");
        preset.Theme.ApplyTo(sc);Verify(slot.Style==WidgetStyle.Table&&slot.ValueScale==1.4&&slot.HiddenSensors.Contains("cpu.power")&&sc.Notes=="Keep notes","Themes preserve widget choices and personal content");
        var saved=ThemeFile.Parse(ThemeFile.From(sc,preset.Name).ToJson());Verify(saved!=null,"Theme export parses");var restored=new Settings();saved!.ApplyTo(restored);Verify(restored.Background==sc.Background&&restored.Colors==sc.Colors&&restored.Font==sc.Font&&restored.Glass==sc.Glass,"Theme file roundtrip retains appearance");
    }
    foreach(bool dark in new[]{true,false}) {
        Application.Current!.RequestedThemeVariant=dark?Avalonia.Styling.ThemeVariant.Dark:Avalonia.Styling.ThemeVariant.Light;
        var board=new Grid{ColumnDefinitions=new ColumnDefinitions("*,*,*,*")};
        for(int col=0;col<fresh.Length;col++) {
            var preset=fresh[col];var sc=new Settings{Language="de"};preset.Theme.ApplyTo(sc);var p=Palette.For(sc,dark,null);
            Verify(p.Dark==dark,"Both light and dark palettes supported");foreach(var ink in new[]{p.Ink,p.Soft,p.Faint}.Concat(p.Ramp))Verify(Contrast(ink,p.Card)>=4.5,preset.Name+" text contrast on card");
            var content=Ui.Stack(10,Ui.Text(L.T(preset.Name),23,p.Ink,FontWeight.Bold),Ui.Text("Native Vorschau · Demodaten",10,p.Soft));
            foreach(var kind in new[]{WidgetKind.Cpu,WidgetKind.Gpu,WidgetKind.Memory,WidgetKind.Network}){var card=Cards.Create(kind,WidgetStyle.Segments,0,p,Metrics.For(Density.Compact),sc);((IWidget)card).Update(snapshot,history);content.Children.Add(card);}
            var column=new Border{Padding=new Thickness(12),Background=Palette.Brush(p.Surface),Child=content};Grid.SetColumn(column,col);board.Children.Add(column);
        }
        Save(board,dark?"themes-new-dark":"themes-new-light",1360,theme:Palette.For(dark,false));
    }
    Application.Current!.RequestedThemeVariant=Avalonia.Styling.ThemeVariant.Dark;
    var config=new Settings{Language="de",TextScale=1.35};var cpu=config.Widgets.Single(w=>w.Kind==WidgetKind.Cpu);cpu.Name="Keep CPU";cpu.Style=WidgetStyle.Table;cpu.LabelScale=1.2;
    var window=new SettingsWindow(config,Palette.For(true,false),()=>{},()=>new[]{"Demo"},new GalleryPlatform(),snapshot:()=>snapshot);
    var section=(Control)typeof(SettingsWindow).GetMethod("ThemeSection",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,null)!;
    var names=ThemeFile.BuiltIn.Select(t=>L.T(t.Name)).ToArray();var buttons=section.GetLogicalDescendants().OfType<Button>().Where(b=>b.Content is string text&&names.Contains(text)).ToArray();Verify(buttons.Length==8,"All eight theme choices appear in the requested menu");Verify(section.GetLogicalDescendants().OfType<Button>().Count()==8,"Theme section has only preset buttons, with no save or load actions");Verify(typeof(SettingsWindow).GetMethod("SaveTheme",BindingFlags.Instance|BindingFlags.NonPublic)==null&&typeof(SettingsWindow).GetMethod("LoadTheme",BindingFlags.Instance|BindingFlags.NonPublic)==null,"No theme file dialogs remain");
    foreach(var button in buttons){button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));Verify(config.TextScale==1.35&&cpu.Name=="Keep CPU"&&cpu.Style==WidgetStyle.Table&&cpu.LabelScale==1.2,"Actual theme buttons preserve global size and per-widget choices");}
    Save(new Border{Padding=new Thickness(20),Background=Palette.Brush(Palette.For(true,false).Surface),Child=section},"themes-menu-eight",1000,theme:Palette.For(true,false));window.Close();
    Console.WriteLine($"THEMES: {checks} checks passed; 8 preset-only menu choices; no save/load; WCAG 4.5:1 text contrast in both modes.");return;
}
foreach(bool dark in new[]{true,false}) {
    Application.Current!.RequestedThemeVariant=dark?Avalonia.Styling.ThemeVariant.Dark:Avalonia.Styling.ThemeVariant.Light;
    var p=Palette.For(dark,false);
    for(int page=0;page<(styles.Length+5)/6;page++) {
        var grid=new Grid{ColumnDefinitions=new ColumnDefinitions("*,*,*"),RowDefinitions=new RowDefinitions("Auto,Auto")};
        for(int i=0;i<6&&page*6+i<styles.Length;i++) {
            var style=styles[page*6+i];
            var stack=Ui.Stack(8,Ui.Text(Name(style),14,p.Soft,FontWeight.DemiBold),Card(style,p));
            stack.Margin=new Thickness(14,12);Grid.SetColumn(stack,i%3);Grid.SetRow(stack,i/3);grid.Children.Add(stack);
        }
        var sheet=Ui.Stack(4,new Border{Padding=new Thickness(14,14,14,0),Child=Ui.Stack(3,Ui.Text($"Lumen {version} · Style gallery {page+1}",21,p.Ink,FontWeight.Bold),Ui.Text("Demo readings · Choose a different design for each widget",12,p.Soft))},grid);
        Save(sheet,$"overview-{page+1:00}-{(dark?"dark":"light")}",1140,theme:p);
    }
    foreach(var style in styles) {
        var sheet=new Border{Padding=new Thickness(12),Child=Ui.Stack(8,Ui.Text(Name(style),14,p.Soft,FontWeight.DemiBold),Card(style,p))};
        Save(sheet,$"card-{style}-{(dark?"dark":"light")}",360,theme:p);
    }
}
// Check the largest supported size in the same native controls as the app.
foreach (double scale in new[] { 0.5, 2.0 })
{
    var p = Palette.For(true, false);
    for (int page = 0; page < (styles.Length + 5) / 6; page++)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), RowDefinitions = new RowDefinitions("Auto,Auto") };
        for (int i = 0; i < 6 && page * 6 + i < styles.Length; i++)
        {
            var style = styles[page * 6 + i];
            var settings = new Settings { Glass = false };
            var slot = settings.Widgets.Single(w => w.Kind == WidgetKind.Cpu);
            slot.Style = style; slot.GraphicScale = scale; slot.ValueScale = scale;
            var card = Cards.Create(WidgetKind.Cpu, style, 0, p, Metrics.For(Density.Fit), settings);
            ((IWidget)card).Update(snapshot, history);
            var stack = Ui.Stack(8, Ui.Text(Name(style) + $" · {scale * 100:0}%", 14, p.Soft), card);
            stack.Margin = new Thickness(14, 12); Grid.SetColumn(stack, i % 3); Grid.SetRow(stack, i / 3); grid.Children.Add(stack);
        }
        Save(grid, $"scale-{scale * 100:0}-{page + 1:00}", 1140, theme: p);
    }
}
Application.Current!.RequestedThemeVariant=Avalonia.Styling.ThemeVariant.Dark;
var palette=Palette.For(true,false);
var mixed=new Settings{Language="en",Glass=false,ShowNames=true,Widgets=new(){
    new(){Kind=WidgetKind.Cpu,Style=WidgetStyle.Pill},new(){Kind=WidgetKind.Gpu,Style=WidgetStyle.TwinFocus},
    new(){Kind=WidgetKind.Memory,Style=WidgetStyle.FillBars},new(){Kind=WidgetKind.Network,Style=WidgetStyle.Segments}}};
foreach(var edge in new[]{Edge.Right,Edge.Top}) {
    mixed.Edge=edge;mixed.BarHeight=240;
    var view=new SidebarView(mixed,palette);view.Update(snapshot,history);
    Save(view,edge==Edge.Top?"mixed-widgets-horizontal":"mixed-widgets-vertical",edge==Edge.Top?1600:360,edge==Edge.Top?280:1100);
}
// The existing per-widget Customise controls, shown with four independently assigned designs.
mixed.Edge=Edge.Right;
typeof(SettingsWindow).GetField("pageIndex",BindingFlags.Static|BindingFlags.NonPublic)!.SetValue(null,0);
var settingsWindow=new SettingsWindow(mixed,palette,()=>{},()=>new[]{"Demo display · 1920×1080"},new GalleryPlatform(),snapshot:()=>snapshot);
settingsWindow.Width=1000;settingsWindow.Height=900;settingsWindow.Show();
for(int i=0;i<3;i++){Dispatcher.UIThread.RunJobs();settingsWindow.CaptureRenderedFrame();}
using(var frame=settingsWindow.CaptureRenderedFrame()!){frame.Save(Path.Combine(output,"settings-design-library.png"));}
var widgetParts=(Control[])typeof(SettingsWindow).GetMethod("WidgetsPage",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(settingsWindow,null)!;
var independent=new Border{Padding=new Thickness(16),Child=Ui.Stack(10,Ui.Text("Each widget can use its own design",21,palette.Ink,FontWeight.Bold),Ui.Text("Settings → Widgets → Customise · An existing Lumen feature",12,palette.Soft),widgetParts[2])};
Save(independent,"per-widget-design-selection",880);
settingsWindow.Close();
L.Use("de");
var labelWindow = new SettingsWindow(mixed, palette, () => {}, () => new[] { "Demo" }, new GalleryPlatform(), snapshot: () => snapshot);
((HashSet<WidgetKind>)typeof(SettingsWindow).GetField("expanded", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(labelWindow)!).Add(WidgetKind.Cpu);
typeof(SettingsWindow).GetMethod("Build", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(labelWindow, null);
labelWindow.Width = 1120; labelWindow.Height = 1200; labelWindow.Show();
for (int i = 0; i < 3; i++) { Dispatcher.UIThread.RunJobs(); labelWindow.CaptureRenderedFrame(); }
using (var frame = labelWindow.CaptureRenderedFrame()!) frame.Save(Path.Combine(output, "settings-label-size.png"));
labelWindow.Close(); L.Use("en");
var standardPair = new StackPanel { Spacing = 12, Margin = new Thickness(12) };
foreach (var kind in new[] { WidgetKind.Cpu, WidgetKind.Gpu })
{
    var tile = Cards.Create(kind, WidgetStyle.Standard, 0, palette, Metrics.For(Density.Fit), new Settings());
    ((IWidget)tile).Update(snapshot, history); standardPair.Children.Add(tile);
}
Save(standardPair, "standard-temperature-load", 360, theme: palette);
var comparison = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto,Auto") };
var comparisonKinds = new[] { WidgetKind.Cpu, WidgetKind.Gpu, WidgetKind.Memory, WidgetKind.Drives, WidgetKind.Network };
var comparisonStyles = new[] { WidgetStyle.Table, WidgetStyle.Segments, WidgetStyle.Pill };
for (int column = 0; column < comparisonStyles.Length; column++)
{
    var heading = Ui.Text(comparisonStyles[column].ToString(), 16, palette.Ink, FontWeight.Bold);
    heading.Margin = new Thickness(14); Grid.SetColumn(heading, column); comparison.Children.Add(heading);
    for (int row = 0; row < comparisonKinds.Length; row++)
    {
        var settings = new Settings();
        settings.Widgets.Single(w => w.Kind == comparisonKinds[row]).GraphicScale = .5;
        var tile = Cards.Create(comparisonKinds[row], comparisonStyles[column], 0, palette, Metrics.For(Density.Fit), settings);
        ((IWidget)tile).Update(snapshot with { Network = new NetworkInfo("Demo Ethernet", 4_000_000, 750_000, 100_000_000, "192.0.2.1") }, history);
        tile.Margin = new Thickness(10); Grid.SetColumn(tile, column); Grid.SetRow(tile, row + 1); comparison.Children.Add(tile);
    }
}
Save(comparison, "layout-update-preview", 1080, theme: palette);
var comparisonBoard = new Grid { ColumnDefinitions=new ColumnDefinitions("*,*,*"), RowDefinitions=new RowDefinitions("Auto,Auto,Auto,Auto,Auto,Auto") };
var trioKinds = new[] { WidgetKind.Cpu, WidgetKind.Gpu, WidgetKind.Memory, WidgetKind.Drives, WidgetKind.Network };
for(int column=0;column<ComparisonTile.Styles.Length;column++) {
    var style=ComparisonTile.Styles[column];
    string title=style==WidgetStyle.Duplex?"Doppelspur":style==WidgetStyle.SignalRows?"Signalzeilen":"Offenes Panel";
    var heading=Ui.Stack(4,Ui.Text(title,21,palette.Ink,FontWeight.Bold),Ui.Text("Native Vorschau · Demodaten · Alle gewählten Werte",10,palette.Soft));heading.Margin=new Thickness(10,16);Grid.SetColumn(heading,column);comparisonBoard.Children.Add(heading);
    for(int row=0;row<trioKinds.Length;row++) {
        var kind=trioKinds[row];var config=new Settings();
        var tile=Cards.Create(kind,style,0,palette,Metrics.For(Density.Fit),config);
        ((IWidget)tile).Update(snapshot with {Network=new NetworkInfo("Intel Ethernet X520-2",999_900_000,125_800_000,10_000_000_000,"192.168.0.119")},history);
        tile.Margin=new Thickness(10,0,10,14);tile.VerticalAlignment=VerticalAlignment.Top;
        Grid.SetColumn(tile,column);Grid.SetRow(tile,row+1);comparisonBoard.Children.Add(tile);
    }
}
Save(comparisonBoard,"trio-designs-overview",1020,theme:palette);
foreach(var design in ComparisonTile.Styles) {
    var densityBoard=new Grid{ColumnDefinitions=new ColumnDefinitions("320,320,320"),RowDefinitions=new RowDefinitions("Auto,Auto")};
    var densities=new[]{Density.Fit,Density.Compact,Density.Comfortable};
    for(int col=0;col<3;col++) {
        var label=Ui.Text(densities[col]==Density.Fit?"Automatisch":densities[col]==Density.Compact?"Kompakt":"Komfortabel",19,palette.Ink,FontWeight.Bold);label.Margin=new Thickness(12);Grid.SetColumn(label,col);densityBoard.Children.Add(label);
        var settings=new Settings{Density=densities[col],Edge=Edge.Right,Widgets=new[]{WidgetKind.Cpu,WidgetKind.Gpu,WidgetKind.Memory,WidgetKind.Drives,WidgetKind.Network}.Select(k=>new WidgetSlot{Kind=k,Style=design}).ToList()};
        var sidebar=new SidebarView(settings,palette);sidebar.Update(snapshot,history);sidebar.Height=760;sidebar.Width=320;
        Grid.SetColumn(sidebar,col);Grid.SetRow(sidebar,1);densityBoard.Children.Add(sidebar);
    }
    Save(densityBoard,"density-"+design,960,theme:palette);
}
var manifest=styles.Select(s=>new{Id=s.ToString(),Name=Name(s),Dark=$"card-{s}-dark.png",Light=$"card-{s}-light.png"}).ToArray();
File.WriteAllText(Path.Combine(output,"manifest.json"),JsonSerializer.Serialize(new{Version=version,Count=manifest.Length,Styles=manifest},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"Rendered {manifest.Length} selectable styles in dark/light themes, all overview pages and mixed-widget examples to {output}");

sealed class GalleryApp:Application { public override void Initialize()=>Styles.Add(new FluentTheme()); }
sealed class GalleryPlatform:Lumen.App.Platform {
    public override bool IsAdministrator=>false;
    public override bool AutostartEnabled=>false;
}

