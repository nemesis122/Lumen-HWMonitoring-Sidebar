using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Layout;
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
string Name(WidgetStyle style)=>style switch {WidgetStyle.PillDuo=>"Double Capsule",WidgetStyle.PillDuoPlus=>"Double Capsule Plus",WidgetStyle.BadgeList=>"List",_=>Regex.Replace(style.ToString(),"([a-z])([A-Z])","$1 $2")};
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
Application.Current!.RequestedThemeVariant=Avalonia.Styling.ThemeVariant.Dark;
var palette=Palette.For(true,false);
var mixed=new Settings{Language="en",Glass=false,ShowNames=true,Widgets=new(){
    new(){Kind=WidgetKind.Cpu,Style=WidgetStyle.Liquid},new(){Kind=WidgetKind.Gpu,Style=WidgetStyle.TwinFocus},
    new(){Kind=WidgetKind.Memory,Style=WidgetStyle.Contour},new(){Kind=WidgetKind.Network,Style=WidgetStyle.Ribbon}}};
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
var manifest=styles.Select(s=>new{Id=s.ToString(),Name=Name(s),Dark=$"card-{s}-dark.png",Light=$"card-{s}-light.png"}).ToArray();
File.WriteAllText(Path.Combine(output,"manifest.json"),JsonSerializer.Serialize(new{Version=version,Count=manifest.Length,Styles=manifest},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"Rendered {manifest.Length} selectable styles in dark/light themes, all overview pages and mixed-widget examples to {output}");

sealed class GalleryApp:Application { public override void Initialize()=>Styles.Add(new FluentTheme()); }
sealed class GalleryPlatform:Lumen.App.Platform {
    public override bool IsAdministrator=>false;
    public override bool AutostartEnabled=>false;
}

