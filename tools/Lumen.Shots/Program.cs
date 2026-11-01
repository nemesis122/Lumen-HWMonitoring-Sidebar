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

// Renders the real sidebar views with demo data into PNG files, no Windows needed.
// The acrylic is imitated with a pre-blurred copy of the wallpaper behind the sidebar.
string outDir = args.Length > 0 ? args[0] : "shots";
string assets = Path.Combine(AppContext.BaseDirectory, "Assets");
Directory.CreateDirectory(outDir);
// LUMEN_SHOT_LANG=en renders every screenshot in one language (for the README).
string? shotLang = Environment.GetEnvironmentVariable("LUMEN_SHOT_LANG");
AppBuilder.Configure<ShotsApp>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).WithInterFont().SetupWithoutStarting();

const int ScreenW = 2560, ScreenH = 1440, Taskbar = 48;
// Still pictures show the final values, not a frame mid-transition.
Motion.Enabled = false;

// "--anim": frames of the sidebar while it updates, to check the transitions.
if (args.Contains("--anim"))
{
    Motion.Enabled = true;
    L.Use("de");
    var src = new DemoSource();
    var hist = new History();
    for (int i = 0; i < 150; i++) hist.Record(src.Read());
    var settings = new Settings { Language = "de", Density = Density.Comfortable, Widgets = Only(WidgetKind.Cpu, WidgetKind.Network) };
    var view = new SidebarView(settings, Palette.For(true, true));
    var back = new Image { Source = Wall(true, true), Stretch = Stretch.None, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
    var w = new Window { Width = 300, Height = 640, Content = new Panel { ClipToBounds = true, Children = { back, view } } };
    w.Show();
    int frame = 0;
    for (int second = 0; second < 4; second++)
    {
        var s = src.Read(); hist.Record(s); view.Update(s, hist);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 1000)
        {
            Thread.Sleep(50);
            Motion.Pump();
            Dispatcher.UIThread.RunJobs();
            w.CaptureRenderedFrame()!.Save(Path.Combine(outDir, $"anim-{frame++:000}.png"));
        }
    }
    Console.WriteLine($"frames {frame}");
    return;
}

(Snapshot, History) Demo()
{
    var src = new DemoSource();
    var h = new History();
    Snapshot s = src.Read();
    for (int i = 0; i < 160; i++) { s = src.Read(); h.Record(s); }
    return (s, h);
}

// A desktop PC has no battery; the laptop shot adds it back.
Snapshot Desktop(Snapshot s) => s with { Battery = null };

Bitmap Wall(bool dark, bool blur) => new(Path.Combine(assets, $"wall-{(dark ? "dark" : "light")}{(blur ? "-blur" : "")}.jpg"));

Palette PaletteFor(Settings s, bool dark) => Palette.For(dark, s.Glass, s.Opacity,
    s.Colors == Settings.StateColors ? null : s.Colors == Settings.WindowsAccent ? Color.Parse("#0078D4") : Color.Parse(s.Colors), s.HighContrast, s.Background);

SidebarView View(Settings settings, bool dark, Func<Snapshot, Snapshot> tweak)
{
    if (shotLang != null) settings.Language = shotLang;
    L.Use(settings.Language);
    var (snap, hist) = Demo();
    var view = new SidebarView(settings, PaletteFor(settings, dark));
    view.Update(tweak(snap), hist);
    return view;
}

void Save(Window w, string name)
{
    if (!w.IsVisible) w.Show();
    Dispatcher.UIThread.RunJobs();
    Dispatcher.UIThread.RunJobs();
    w.CaptureRenderedFrame()!.Save(Path.Combine(outDir, name + ".png"));
    w.Close();
    Console.WriteLine(name);
}

// The sidebar alone over the blurred wallpaper it would sit on. "Automatic" uses the full
// working height of a 1440p screen; the other densities are cut to their content.
void Sidebar(string name, Settings settings, bool dark, Func<Snapshot, Snapshot>? tweak = null, int height = ScreenH - Taskbar)
{
    var view = View(settings, dark, tweak ?? Desktop);
    var back = new Image { Source = Wall(dark, true), Stretch = Stretch.None, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
    var w = new Window { Width = SidebarView.WindowWidth(settings), Height = height, Content = new Panel { ClipToBounds = true, Children = { back, view } } };
    w.Show();
    Dispatcher.UIThread.RunJobs();
    if (settings.Density != Density.Fit)
    {
        var scroll = ((DockPanel)view.Child!).Children.OfType<ScrollViewer>().First();
        w.Height = Math.Ceiling(view.Bounds.Height - scroll.Viewport.Height + ((Control)scroll.Content!).DesiredSize.Height);
    }
    Save(w, name);
}

// A whole 2560×1440 desktop with a Windows-11-like taskbar and the sidebar docked right.
void DesktopShot(string name, Settings settings, bool dark, int screenHeight = ScreenH, Control? extra = null)
{
    var view = View(settings, dark, Desktop);
    if (settings.Horizontal)
    {
        // A bar across the whole width, above the taskbar or at the top of the screen.
        var barStrip = new Panel
        {
            Height = settings.BarHeight * settings.Rows + (settings.Rows - 1) * Metrics.For(settings.Density).Gap,
            VerticalAlignment = settings.Edge == Edge.Top ? VerticalAlignment.Top : VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, settings.Edge == Edge.Top ? 0 : Taskbar),
            ClipToBounds = true,
            Children = { new Image { Source = Wall(dark, true), Stretch = Stretch.UniformToFill }, view }
        };
        var taskbar = new Border { Height = Taskbar, VerticalAlignment = VerticalAlignment.Bottom, Background = new SolidColorBrush(dark ? Color.FromArgb(0xE6, 0x1C, 0x1F, 0x26) : Color.FromArgb(0xE6, 0xF3, 0xF3, 0xF3)) };
        Save(new Window { Width = ScreenW, Height = screenHeight, Content = new Panel { Children = { new Image { Source = Wall(dark, false), Stretch = Stretch.UniformToFill }, barStrip, taskbar } } }, name);
        return;
    }
    var strip = new Panel
    {
        Width = settings.FullScreen ? ScreenW : SidebarView.WindowWidth(settings),
        HorizontalAlignment = HorizontalAlignment.Right,
        Margin = new Thickness(0, 0, 0, Taskbar),
        ClipToBounds = true,
        Children = { new Image { Source = Wall(dark, true), Stretch = Stretch.None, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top }, view }
    };
    var icons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    foreach (var c in new[] { "#3B82F6", "#F2F4F7", "#F59E0B", "#22C55E", "#A855F7", "#EF4444" })
        icons.Children.Add(new Border { Width = 26, Height = 26, CornerRadius = new CornerRadius(6), Background = new SolidColorBrush(Color.Parse(c)), Opacity = .9 });
    var bar = new Border
    {
        Height = Taskbar,
        VerticalAlignment = VerticalAlignment.Bottom,
        Background = new SolidColorBrush(dark ? Color.FromArgb(0xE6, 0x1C, 0x1F, 0x26) : Color.FromArgb(0xE6, 0xF3, 0xF3, 0xF3)),
        BorderBrush = new SolidColorBrush(Color.FromArgb(0x22, 0x80, 0x80, 0x80)),
        BorderThickness = new Thickness(0, 1, 0, 0),
        Child = icons
    };
    var root = new Panel { Children = { new Image { Source = Wall(dark, false), Stretch = Stretch.UniformToFill }, strip, bar } };
    if (extra != null) root.Children.Add(extra);
    Save(new Window { Width = ScreenW, Height = screenHeight, Content = root }, name);
}

bool barSettings = false;

void SettingsShot(string name, bool dark, string language, params WidgetKind[] open) => SettingsPage(name, dark, language, 0, open);

void SettingsPage(string name, bool dark, string language, int index, params WidgetKind[] open)
{
    typeof(SettingsWindow).GetField("pageIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.SetValue(null, index);
    if (shotLang != null) language = shotLang;
    L.Use(language);
    var s = new Settings { Theme = dark ? ThemeMode.Dark : ThemeMode.Light, Language = language, Pages = 4, Columns = 4, PageNames = new() { "Hardware", "Gaming", "", "", "", "" } };
    if (barSettings) { s.Edge = Edge.Bottom; s.Pages = 1; }
    var cpuSlot = s.Widgets.First(x => x.Kind == WidgetKind.Cpu);
    cpuSlot.Name = "Gaming-PC"; cpuSlot.HardwareName = "Ryzen 9 5900X @ PBO";
    foreach (var x in s.Widgets) { if (x.Kind is WidgetKind.Gpu or WidgetKind.Fps) x.Page = 2; if (x.Kind == WidgetKind.Gpu) { x.Column = 2; x.Style = WidgetStyle.Ring; } }
    Application.Current!.RequestedThemeVariant = dark ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light;
    var w = new SettingsWindow(s, Palette.For(dark, false), () => { }, () => new[] { L.F("Screen {0}", 1) + " · 2560×1440 (" + L.T("main") + ")", L.F("Screen {0}", 2) + " · 1920×1080" }, new ShotPlatform(), null, null, () => Demo().Item1);
    // Open the "Values" list of the chosen widgets, as a user would by clicking.
    var field = typeof(SettingsWindow).GetField("expanded", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
    foreach (var k in open) ((HashSet<WidgetKind>)field.GetValue(w)!).Add(k);
    typeof(SettingsWindow).GetMethod("Build", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(w, null);
    w.Height = 860;
    w.Show();
    Save(w, name);
}

static List<WidgetSlot> Only(params WidgetKind[] kinds) =>
    kinds.Select(k => new WidgetSlot { Kind = k }).Concat(Enum.GetValues<WidgetKind>().Except(kinds).Select(k => new WidgetSlot { Kind = k, Visible = false })).ToList();

// The default set on a 1440p desktop, filling the height exactly.
DesktopShot("desktop-dark", new Settings { Language = "de" }, true);
// Two columns with many widgets, still filling the height exactly.
DesktopShot("desktop-two-columns", new Settings
{
    Language = "de", Columns = 2,
    Widgets = Only(WidgetKind.Clock, WidgetKind.Weather, WidgetKind.Cpu, WidgetKind.Gpu, WidgetKind.Cooling, WidgetKind.Memory,
        WidgetKind.Media, WidgetKind.Power, WidgetKind.Ping, WidgetKind.Volumes, WidgetKind.Network, WidgetKind.DataUsage)
}, true);
// The whole monitor, six columns, two registers.
var full = new Settings { Language = "de", Columns = 6, FullScreen = true, Pages = 2 };
foreach (var w in full.Widgets) { w.Visible = w.Kind is not (WidgetKind.Notes or WidgetKind.Processes or WidgetKind.Battery); w.Page = w.Kind is WidgetKind.WorldClocks or WidgetKind.Calendar ? 2 : 1; }
DesktopShot("desktop-full-6", full, true);
var four = new Settings { Language = "de", Columns = 4, FullScreen = true };
foreach (var w in four.Widgets) w.Visible = w.Kind is WidgetKind.Clock or WidgetKind.Weather or WidgetKind.Cpu or WidgetKind.Gpu or WidgetKind.Fps or WidgetKind.Cooling or WidgetKind.Memory or WidgetKind.Drives or WidgetKind.Network or WidgetKind.Ping or WidgetKind.Media or WidgetKind.Power;
DesktopShot("desktop-full-4", four, true);
// The user's own set in two columns: both columns must reach the bottom.
DesktopShot("desktop-two-columns-user", new Settings
{
    Language = "en", Columns = 2, Colors = "#F97316",
    Widgets = Only(WidgetKind.Cpu, WidgetKind.Gpu, WidgetKind.Cooling, WidgetKind.Memory, WidgetKind.System, WidgetKind.Drives, WidgetKind.Network),
    ShowIntegratedGpu = false
}, true);
// A bar at the bottom: rings, tiles and bars side by side.
var bottom = new Settings { Language = "de", Edge = Edge.Bottom, BarHeight = 260, Columns = 2 };
foreach (var w in bottom.Widgets)
{
    w.Visible = w.Kind is WidgetKind.Clock or WidgetKind.Cpu or WidgetKind.Gpu or WidgetKind.Memory or WidgetKind.Ping or WidgetKind.Power or WidgetKind.Network or WidgetKind.Drives or WidgetKind.Weather or WidgetKind.Volumes or WidgetKind.System or WidgetKind.DataUsage or WidgetKind.Cooling;
    w.Style = w.Kind switch { WidgetKind.Cpu or WidgetKind.Gpu => WidgetStyle.Ring, WidgetKind.Memory or WidgetKind.Ping => WidgetStyle.Tile, WidgetKind.Power or WidgetKind.Weather => WidgetStyle.Graph, WidgetKind.Volumes or WidgetKind.Drives => WidgetStyle.Bar, WidgetKind.System or WidgetKind.DataUsage => WidgetStyle.Minimal, _ => WidgetStyle.Standard };
}
DesktopShot("desktop-bar-bottom", bottom, true);
var top = new Settings { Language = "de", Edge = Edge.Top, BarHeight = 340, Columns = 2, Colors = "#06B6D4", Background = "midnight" };
foreach (var w in top.Widgets) w.Visible = w.Kind is WidgetKind.Clock or WidgetKind.Cpu or WidgetKind.Gpu or WidgetKind.Cooling or WidgetKind.Memory or WidgetKind.Network or WidgetKind.Drives or WidgetKind.Weather or WidgetKind.Ping or WidgetKind.Power or WidgetKind.Fps or WidgetKind.System;
DesktopShot("desktop-bar-top", top, true);
var barCols = new Settings { Language = "en", Edge = Edge.Bottom, BarHeight = 300, Columns = 2 };
foreach (var w in barCols.Widgets) w.Visible = w.Kind is WidgetKind.Clock or WidgetKind.Weather or WidgetKind.Cpu or WidgetKind.Gpu or WidgetKind.Memory or WidgetKind.Ping or WidgetKind.Power or WidgetKind.Network or WidgetKind.Drives or WidgetKind.DataUsage;
foreach (var w in barCols.Widgets) w.Style = w.Kind is WidgetKind.Memory or WidgetKind.Ping or WidgetKind.Power or WidgetKind.DataUsage ? WidgetStyle.Bar : WidgetStyle.Standard;
DesktopShot("desktop-bar-columns", barCols, true);
// The new styles, and some backgrounds and colours.
var newStyles = new Settings { Language = "de", Density = Density.Comfortable, Widgets = Only(WidgetKind.Cpu, WidgetKind.Gpu, WidgetKind.Memory, WidgetKind.Ping, WidgetKind.Network, WidgetKind.Drives, WidgetKind.Volumes, WidgetKind.Power) };
foreach (var w in newStyles.Widgets) w.Style = w.Kind switch { WidgetKind.Cpu => WidgetStyle.Graph, WidgetKind.Gpu => WidgetStyle.Bar, WidgetKind.Memory or WidgetKind.Ping => WidgetStyle.Tile, WidgetKind.Power => WidgetStyle.Tile, WidgetKind.Drives => WidgetStyle.Bar, WidgetKind.Volumes => WidgetStyle.Bar, WidgetKind.Network => WidgetStyle.Graph, _ => WidgetStyle.Tile };
Sidebar("sidebar-new-styles", newStyles, true);
foreach (var (bgKey, accent) in new[] { ("black", "#EF4444"), ("aubergine", "#D946EF"), ("forest", "#84CC16"), ("mocha", "#EAB308") })
    Sidebar("sidebar-bg-" + bgKey, new Settings { Language = "de", Density = Density.Comfortable, Background = bgKey, Colors = accent, Widgets = Only(WidgetKind.Clock, WidgetKind.Cpu, WidgetKind.Memory) }, true);
// The bar switched to its second register (regression: crashed in 1.9.2).
{
    // As on the user's PC: two rows, two registers, every widget still on the first one.
    var paged = new Settings { Language = "de", Edge = Edge.Top, BarHeight = 240, Pages = 2, Columns = 2 };
    foreach (var w in paged.Widgets) { w.Visible = true; w.Page = 1; }
    try
    {
        var v = View(paged, true, Desktop);
        var host = new Window { Width = 1920, Height = 490, Content = v };
        host.Show();
        Dispatcher.UIThread.RunJobs();
        var show = typeof(SidebarView).GetMethod("Show", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var (snap2, hist2) = Demo();
        for (int round = 0; round < 3; round++)
        {
            show.Invoke(v, new object[] { 2 });
            for (int k = 0; k < 5; k++) { Dispatcher.UIThread.RunJobs(); v.Update(snap2, hist2); host.CaptureRenderedFrame(); }
            show.Invoke(v, new object[] { 1 });
            for (int k = 0; k < 5; k++) { Dispatcher.UIThread.RunJobs(); v.Update(snap2, hist2); host.CaptureRenderedFrame(); }
        }
        show.Invoke(v, new object[] { 2 });
        Dispatcher.UIThread.RunJobs();
        host.CaptureRenderedFrame()!.Save(Path.Combine(outDir, "bar-page-2.png"));
        typeof(SidebarView).GetMethod("Show", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(v, new object[] { 1 });
        Dispatcher.UIThread.RunJobs();
        host.Close();
        Console.WriteLine("bar-page-2 ok");
    }
    catch (Exception e) { Console.WriteLine("bar-page-2 CRASH " + (e.InnerException ?? e)); }
}
// Storage space and drives in compact styles.
{
    var vol = new Settings { Language = "de", Density = Density.Comfortable, Widgets = Only(WidgetKind.Volumes, WidgetKind.Drives, WidgetKind.Network) };
    foreach (var w in vol.Widgets) w.Style = w.Kind switch { WidgetKind.Volumes => WidgetStyle.Bar, WidgetKind.Drives => WidgetStyle.Tile, _ => WidgetStyle.Minimal };
    Sidebar("sidebar-volumes-styles", vol, true);
}
// Own title and hardware name, and a widget with both hidden.
{
    var named2 = new Settings { Language = "de", Density = Density.Comfortable, Widgets = Only(WidgetKind.Cpu, WidgetKind.Gpu, WidgetKind.Memory) };
    var c = named2.Widgets.First(w => w.Kind == WidgetKind.Cpu); c.Name = "Gaming-PC"; c.HardwareName = "Ryzen 9 5900X @ PBO";
    var g = named2.Widgets.First(w => w.Kind == WidgetKind.Gpu); g.Style = WidgetStyle.Ring; g.HardwareName = "RTX 3080 FE";
    var m = named2.Widgets.First(w => w.Kind == WidgetKind.Memory); m.ShowTitle = false;
    Sidebar("sidebar-own-names", named2, true);
}
// Regression: bar at the top, three rows, every style for all widgets.
foreach (var st in new[] { WidgetStyle.Standard, WidgetStyle.Ring, WidgetStyle.Minimal, WidgetStyle.Bar, WidgetStyle.Graph, WidgetStyle.Tile })
{
    try
    {
        var b3 = new Settings { Language = "de", Edge = Edge.Top, BarHeight = 240, Columns = 3, Pages = 2 };
        foreach (var w in b3.Widgets) { w.Visible = true; w.Page = 1; if (Settings.HasStyles(w.Kind)) w.Style = st; }
        var v3 = View(b3, true, Desktop);
        var host3 = new Window { Width = 1920, Height = 760, Content = v3 };
        host3.Show();
        var (sn3, hi3) = Demo();
        for (int k = 0; k < 4; k++) { Dispatcher.UIThread.RunJobs(); v3.Update(sn3, hi3); host3.CaptureRenderedFrame(); }
        host3.CaptureRenderedFrame()!.Save(Path.Combine(outDir, $"bar3-{st}.png"));
        host3.Close();
        Console.WriteLine($"bar3 {st} ok");
    }
    catch (Exception e) { Console.WriteLine($"bar3 {st} CRASH " + (e.InnerException ?? e)); }
}
// Text size extremes.
Sidebar("sidebar-text-50", new Settings { Language = "de", Density = Density.Comfortable, TextScale = .5, Widgets = Only(WidgetKind.Clock, WidgetKind.Cpu, WidgetKind.Memory) }, true);
Sidebar("sidebar-text-200", new Settings { Language = "de", Density = Density.Comfortable, TextScale = 2, Widgets = Only(WidgetKind.Clock, WidgetKind.Cpu, WidgetKind.Memory) }, true);
// Named registers in a single sidebar.
var named = new Settings { Language = "de", Pages = 3, PageNames = new() { "Hardware", "Gaming", "Info", "", "", "" } };
foreach (var w in named.Widgets) w.Page = w.Kind is WidgetKind.Fps or WidgetKind.Gpu ? 2 : w.Kind is WidgetKind.Weather or WidgetKind.Calendar ? 3 : 1;
DesktopShot("desktop-named-registers", named, true);
// Ring tiles pair up, minimal lines take one slim row each.
var rings = new Settings
{
    Language = "de", Density = Density.Comfortable,
    Widgets = Only(WidgetKind.Clock, WidgetKind.Cpu, WidgetKind.Gpu, WidgetKind.Memory, WidgetKind.Ping, WidgetKind.Power, WidgetKind.Weather, WidgetKind.Network, WidgetKind.DataUsage, WidgetKind.System)
};
foreach (var w in rings.Widgets)
    w.Style = w.Kind is WidgetKind.Cpu or WidgetKind.Gpu or WidgetKind.Memory or WidgetKind.Ping or WidgetKind.Power or WidgetKind.Weather ? WidgetStyle.Ring
        : w.Kind is WidgetKind.Network or WidgetKind.DataUsage or WidgetKind.System ? WidgetStyle.Minimal : WidgetStyle.Standard;
Sidebar("sidebar-styles", rings, true);
Sidebar("sidebar-fps-names", new Settings
{
    Language = "de", Density = Density.Comfortable, Widgets = Only(WidgetKind.Fps, WidgetKind.Cooling, WidgetKind.Drives)
}, true, s => s);
Sidebar("sidebar-new-1", new Settings { Language = "de", Density = Density.Comfortable, Widgets = Only(WidgetKind.Media, WidgetKind.WorldClocks, WidgetKind.Calendar, WidgetKind.Ping) }, true);
Sidebar("sidebar-new-2", new Settings
{
    Language = "de", Density = Density.Comfortable, Notes = "Backup am Sonntag\nNeue Wärmeleitpaste bestellen",
    Widgets = Only(WidgetKind.Power, WidgetKind.DataUsage, WidgetKind.System, WidgetKind.Volumes, WidgetKind.Notes)
}, true);
DesktopShot("desktop-light", new Settings { Language = "en", Theme = ThemeMode.Light }, false);
// The same automatic layout on a 1080p screen: smaller, still no scrolling.
Sidebar("sidebar-fit-1080p", new Settings { Language = "de" }, true, height: 1080 - Taskbar);
Sidebar("sidebar-dark", new Settings { Language = "de", Density = Density.Comfortable, Widgets = Only(WidgetKind.Clock, WidgetKind.Cpu, WidgetKind.Gpu, WidgetKind.Memory, WidgetKind.Drives, WidgetKind.Network), ShowIntegratedGpu = false }, true);
Sidebar("sidebar-high-contrast", new Settings { Language = "de", Density = Density.Comfortable, HighContrast = true, Widgets = Only(WidgetKind.Clock, WidgetKind.Cpu, WidgetKind.Gpu, WidgetKind.Memory, WidgetKind.Drives, WidgetKind.Network), ShowIntegratedGpu = false }, true);
Sidebar("sidebar-light", new Settings { Language = "en", Density = Density.Comfortable, Widgets = Only(WidgetKind.Clock, WidgetKind.Cpu, WidgetKind.Gpu, WidgetKind.Memory, WidgetKind.Drives, WidgetKind.Network), ShowIntegratedGpu = false }, false);
Sidebar("sidebar-compact", new Settings { Language = "fr", Density = Density.Compact, ShowIntegratedGpu = false, Widgets = Only(WidgetKind.Clock, WidgetKind.Cpu, WidgetKind.Gpu, WidgetKind.Memory, WidgetKind.Drives, WidgetKind.Network) }, true);
Sidebar("sidebar-extras", new Settings
{
    Language = "de", Density = Density.Comfortable,
    Widgets = Only(WidgetKind.Weather, WidgetKind.Battery, WidgetKind.Cooling, WidgetKind.Processes)
}, true, s => s);
Sidebar("sidebar-accent", new Settings { Language = "it", Density = Density.Comfortable, Colors = "#8B5CF6", Widgets = Only(WidgetKind.Cpu, WidgetKind.Memory, WidgetKind.Network) }, true);
Sidebar("sidebar-gpus", new Settings { Language = "es", Density = Density.Comfortable, Widgets = Only(WidgetKind.Gpu) }, true);
Sidebar("sidebar-hidden-values", new Settings
{
    Language = "pt", Density = Density.Comfortable, Widgets = Only(WidgetKind.Cpu, WidgetKind.Gpu),
    ShowIntegratedGpu = false, HiddenValues = new() { "cpu.voltage", "cpu.fan", "gpu.chart", "gpu.fan" }
}, true);
Sidebar("sidebar-hot", new Settings { Language = "de", Density = Density.Comfortable, Widgets = Only(WidgetKind.Cpu, WidgetKind.Gpu), ShowIntegratedGpu = false }, true,
    s => Desktop(s) with { Cpu = s.Cpu! with { Temperature = 91, Load = 97, CoreLoads = s.Cpu.CoreLoads.Select((_, i) => 90.0 + i % 4 * 3).ToArray() }, Gpus = new[] { s.Gpus[0] with { Temperature = 79, HotSpot = 93, Load = 99 } } });
Sidebar("sidebar-limited", new Settings { Language = "de", Density = Density.Comfortable, Widgets = Only(WidgetKind.Cpu, WidgetKind.Memory) }, true,
    s => Desktop(s) with { Access = SensorAccess.Limited, Cpu = s.Cpu! with { Temperature = null, PowerW = null, Voltage = null, FanRpm = null } });

// The warning notice beside the sidebar.
L.Use("de");
var toastPalette = Palette.For(true, true);
var toast = new Toast(toastPalette, L.T("Temperature warning"), L.F("{0} is at {1} – above your limit of {2}.", "AMD Ryzen 9 5900X", "91 °C", "85 °C"));
var toastContent = (Control)toast.Content!;
toast.Content = null;
toastContent.Width = 340;
toastContent.HorizontalAlignment = HorizontalAlignment.Right;
toastContent.VerticalAlignment = VerticalAlignment.Bottom;
toastContent.Margin = new Thickness(0, 0, 300 + 16, Taskbar + 16);
DesktopShot("desktop-warning", new Settings { Language = "de" }, true, extra: toastContent);

for (int i = 0; i < 6; i++) SettingsPage($"settings-de-{i}", true, "de", i, WidgetKind.Cpu);
barSettings = true;
SettingsPage("settings-de-bar-layout", true, "de", 2);
SettingsPage("settings-de-bar-position", true, "de", 2);
SettingsPage("settings-en-bar-layout", true, "en", 2);
barSettings = false;
L.Use("de");
var demoSnap = Demo().Item1;
var sensorWin = new SensorsWindow(Palette.For(true, false), () => "=== LibreHardwareMonitor ===\n[Cpu] AMD Ryzen 9 5900X\n    Temperature  Core (Tctl/Tdie)                   57.1\n    Load         CPU Total                          34.2\n    ...\n\n=== Open Hardware Monitor (driverless: vendor GPU APIs, SMART) ===\n[GpuNvidia] NVIDIA GeForce RTX 3080\n    Temperature  GPU Core                           58\n    Temperature  GPU Hot Spot                       71\n\n=== Combined ===\nNVIDIA GeForce RTX 3080: HotSpot from Open Hardware Monitor\n\n=== In der Sidebar angezeigt ===\n" + SnapshotText.Describe(demoSnap), "/tmp");
sensorWin.Show();
Save(sensorWin, "sensors-window");
SettingsShot("settings-en-light", false, "en");
// The search: typing lists every matching setting across all topics.
{
    L.Use("de");
    Application.Current!.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
    var sw = new SettingsWindow(new Settings { Language = "de" }, Palette.For(true, false), () => { }, () => new[] { "Bildschirm 1" }, new ShotPlatform(), null, null, () => Demo().Item1);
    ((TextBox)typeof(SettingsWindow).GetField("search", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(sw)!).Text = "temp";
    sw.Height = 860;
    sw.Show();
    Save(sw, "settings-de-search");
}

sealed class ShotsApp : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());
}

sealed class ShotPlatform : Platform
{
    public override bool IsAdministrator => false;
    public override bool AutostartEnabled => true;
}
