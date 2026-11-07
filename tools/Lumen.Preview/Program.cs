using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Lumen.App;
using Lumen.Core;

// Renders the new designs with demo data: one sidebar per design (dark and light) and one bar.
string output = Path.GetFullPath(args.FirstOrDefault() ?? "preview");
Directory.CreateDirectory(output);
AppBuilder.Configure<PreviewApp>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).WithInterFont().SetupWithoutStarting();
Motion.Enabled = false;
string language = args.Contains("--en") ? "en" : "de";
L.Use(language); Ui.UseFont("inter");
using var source = new DemoSource();
var history = new History();
Snapshot snapshot = source.Read();
for (int i = 0; i < 160; i++) { snapshot = source.Read(); history.Record(snapshot); }
snapshot = snapshot with { Battery = null };
var styles = Environment.GetEnvironmentVariable("STYLES") is string only ? only.Split(",").Select(Enum.Parse<WidgetStyle>).ToArray() : Settings.Selectable.ToArray();
var kinds = Environment.GetEnvironmentVariable("KINDS") is string k && k.Length > 0 ? k.Split(",").Select(Enum.Parse<WidgetKind>).ToArray() : new[] { WidgetKind.Cpu, WidgetKind.Gpu, WidgetKind.Memory, WidgetKind.Drives, WidgetKind.Network };

void Shot(Control content, string name, double width, double height, Palette p)
{
    var window = new Window { Content = content, Width = width, Height = height, Background = Palette.Brush(p.Surface) };
    window.Show();
    for (int i = 0; i < 4; i++) { Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame(); }
    using var image = window.CaptureRenderedFrame() ?? throw new InvalidOperationException(name);
    image.Save(Path.Combine(output, name + ".png"));
    window.Close();
}

if (args.Contains("--jump"))
{
    var styleArg = Environment.GetEnvironmentVariable("STYLE");
    var densities = (Environment.GetEnvironmentVariable("DENS") ?? "Fit,Comfortable,Compact").Split(',').Select(Enum.Parse<Density>).ToArray();
    foreach (var style in styleArg == "all" ? Settings.Selectable.ToArray() : new[] { Enum.Parse<WidgetStyle>(styleArg ?? "TwinFocus") })
    foreach (var density in densities)
    {
        int cols = int.TryParse(Environment.GetEnvironmentVariable("COLS"), out int cc) ? cc : 2;
        int width = cols == 1 ? 300 : 600;
        var s = new Settings { Language = language, Density = density, Columns = cols, Width = width };
        var shown = Environment.GetEnvironmentVariable("KINDS") is string ks ? ks.Split(',').Select(Enum.Parse<WidgetKind>).ToArray() : new[] { WidgetKind.Cpu, WidgetKind.Gpu, WidgetKind.Memory, WidgetKind.Drives, WidgetKind.Network, WidgetKind.Cooling };

        foreach (var w in s.Widgets) { w.Visible = shown.Contains(w.Kind); if (Settings.HasStyles(w.Kind)) w.Style = style; }
        var p = Palette.For(true, false);
        var view = new SidebarView(s, p);
        var h2 = new History();
        var window = new Window { Content = new Border { Child = view }, Width = width, Height = 1025, Background = Palette.Brush(p.Surface) };
        window.Show();
        var first = new Dictionary<string, Rect>();
        var innerFirst = new Dictionary<string, Rect>();
        var innerMoves = new Dictionary<string, double>();
        var moves = new Dictionary<string, double>();
        double minBottom = 1e9, maxBottom = 0;
        for (int i = 0; i < 60; i++)
        {
            var sn = source.Read();
            if (Environment.GetEnvironmentVariable("CHAOS") == "1")
            {
                // Real-life swings: rates jump between bytes and megabytes, fans stop and start.
                var rnd = new Random(i * 7919);
                double Rate() => rnd.Next(5) == 0 ? 0 : Math.Pow(10, rnd.NextDouble() * 8);
                sn = sn with
                {
                    Network = sn.Network! with { DownloadBytesPerSecond = Rate(), UploadBytesPerSecond = Rate() },
                    Disks = sn.Disks.Select(d => d with { ReadBytesPerSecond = Rate(), WriteBytesPerSecond = Rate() }).ToArray(),
                    Cpu = sn.Cpu! with { Load = rnd.NextDouble() * 100, Temperature = 30 + rnd.NextDouble() * 60, PowerW = rnd.NextDouble() * 200, ClockMhz = 800 + rnd.NextDouble() * 4000, Voltage = 0.7 + rnd.NextDouble() * 0.7, FanRpm = rnd.Next(2) == 0 ? 0 : rnd.Next(400, 2600) },
                    Gpus = sn.Gpus.Select(g => g with { Load = rnd.NextDouble() * 100, Temperature = 30 + rnd.NextDouble() * 60, HotSpot = 30 + rnd.NextDouble() * 70, PowerW = rnd.NextDouble() * 350, ClockMhz = 200 + rnd.NextDouble() * 2000, FanRpm = rnd.Next(2) == 0 ? 0 : rnd.Next(400, 2600), MemoryUsedMb = rnd.NextDouble() * 10000 }).ToArray(),
                    Memory = sn.Memory! with { UsedGb = 4 + rnd.NextDouble() * 27 },
                    Fans = sn.Fans.Select(f => f with { Rpm = rnd.Next(2) == 0 ? 0 : rnd.Next(400, 2600) }).ToArray()
                };
            }
            h2.Record(sn); view.Update(sn with { Gpus = sn.Gpus.Take(1).ToArray(), Battery = null }, h2);
            Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame();
            if (i < 5) continue;
            var cards = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(view).OfType<Control>().Where(c => c is Card || c is SummaryTile).Where(c => c.IsVisible).ToList();
            double colBottomL = 0, colBottomR = 0;
            foreach (var c in cards)
            {
                var tl = c.TranslatePoint(new Point(0, 0), view) ?? default; var br = c.TranslatePoint(new Point(c.Bounds.Width, c.Bounds.Height), view) ?? default;
                var r = new Rect(tl, br); string key = c.GetType().Name + "@" + (int)tl.X + ":" + cards.IndexOf(c);
                if (!first.ContainsKey(key)) first[key] = r;
                var f = first[key]; double d = Math.Max(Math.Abs(f.Top - r.Top), Math.Abs(f.Bottom - r.Bottom));
                moves[key] = Math.Max(moves.GetValueOrDefault(key), d);
                if (Environment.GetEnvironmentVariable("PARTS") == key)
                {
                    var hs = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(c).OfType<Control>().Select(x => x.GetType().Name + (x is TextBlock tb ? "'" + tb.Text + "'" : "") + "=" + x.Bounds.Height.ToString("F1")).ToList();
                    Console.WriteLine($"  F{i}: " + string.Join(" ", hs.Where(h => !h.Contains("=0.0")).Take(400)));
                }
                if (Environment.GetEnvironmentVariable("MOVES") == "1" && d > 1.5) Console.WriteLine($"  frame {i} {key} first={f.Top:F0}-{f.Bottom:F0} now={r.Top:F0}-{r.Bottom:F0}");
                if (r.Left < 250) colBottomL = Math.Max(colBottomL, r.Bottom); else colBottomR = Math.Max(colBottomR, r.Bottom);
            }
            minBottom = Math.Min(minBottom, Math.Min(colBottomL, colBottomR)); maxBottom = Math.Max(maxBottom, Math.Max(colBottomL, colBottomR));
            if (i == 59) Console.Write($"{style,-14} {density,-11} L={colBottomL,6:F1} R={colBottomR,6:F1} ");
            if (Environment.GetEnvironmentVariable("INNER") == "1")
            {
                foreach (var c in cards)
                {
                    int n = 0;
                    foreach (var x in Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(c).OfType<Control>())
                    {
                        n++;
                        if (!x.IsEffectivelyVisible || x.Bounds.Width <= 0) continue;
                        bool bar = x is FillBack or Meter or GradientBar or SegmentBar or LevelBar or AreaChart or CoreBars;
                        bool label = x is TextBlock tb && tb.FontFeatures == null;
                        if (!bar && !label && x is not TextBlock) continue;
                        var tl = x.TranslatePoint(new Point(0, 0), c) ?? default;
                        var r = new Rect(tl, x.Bounds.Size);
                        string key = c.GetType().Name + cards.IndexOf(c) + "/" + n + ":" + x.GetType().Name + (x is TextBlock t2 && label ? "'" + t2.Text + "'" : "");
                        if (!innerFirst.TryGetValue(key, out var f0)) { innerFirst[key] = r; continue; }
                        double d = Math.Abs(f0.Top - r.Top);
                        if (bar || label) d = Math.Max(d, Math.Max(Math.Abs(f0.Left - r.Left), bar ? Math.Abs(f0.Width - r.Width) : 0));
                        if (d > 1.5) innerMoves[key] = Math.Max(innerMoves.GetValueOrDefault(key), d);
                    }
                }
            }
        }
        Console.WriteLine($"range {minBottom:F1}..{maxBottom:F1} maxmove {moves.Values.DefaultIfEmpty(0).Max():F1}");
        foreach (var kv in innerMoves.OrderByDescending(k => k.Value).Take(25)) Console.WriteLine($"   inner {kv.Value,5:F1}  {kv.Key}");
        using var image = window.CaptureRenderedFrame()!; image.Save(Path.Combine(output, $"jump-{style}-{density}.png"));
        window.Close();
    }
    return;
}
if (args.Contains("--dial2"))
{
    foreach (var density in new[] { Density.Fit, Density.Comfortable })
    {
        var s = new Settings { Language = language, Glass = false, Theme = ThemeMode.Dark, Density = density, Columns = 2, Width = 620 };
        var shown = new[] { WidgetKind.Cpu, WidgetKind.Gpu, WidgetKind.Memory, WidgetKind.Drives, WidgetKind.Network };
        foreach (var w in s.Widgets) { w.Visible = shown.Contains(w.Kind); if (Settings.HasStyles(w.Kind)) w.Style = WidgetStyle.Dial; }
        var p = Palette.For(s, true, null);
        var view = new SidebarView(s, p);
        var h2 = new History();
        for (int i = 0; i < 90; i++) { var sn = source.Read(); h2.Record(sn); view.Update(sn with { Gpus = sn.Gpus.Take(1).ToArray() }, h2); }
        Shot(new Border { Child = view }, "dial2-" + density, 620, 1025, p);
    }
    Console.WriteLine("Preview rendered: " + output);
    return;
}
if (args.Contains("--evo") || args.Contains("--dial"))
{
    var evoStyle = args.Contains("--dial") ? WidgetStyle.Dial : WidgetStyle.Evolution;
    var hw = new[] { WidgetKind.Clock, WidgetKind.Cpu, WidgetKind.Gpu, WidgetKind.Memory, WidgetKind.Network, WidgetKind.Drives, WidgetKind.Cooling };
    foreach (var (name, dark, floating) in new[] { ("evo-side-dark", true, false), ("evo-side-light", false, false), ("evo-float", true, true) })
    {
        var s = new Settings { Language = language, Glass = false, Theme = dark ? ThemeMode.Dark : ThemeMode.Light, Density = Density.Fit, Floating = floating };
        if (evoStyle == WidgetStyle.Dial) s.Width = 330;
        foreach (var w in s.Widgets) { w.Visible = hw.Contains(w.Kind); if (Settings.HasStyles(w.Kind)) w.Style = evoStyle; }
        var p = Palette.For(s, dark, null);
        if (floating)
        {
            var open = (HashSet<string>)typeof(EvolutionTile).GetField("expanded", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.GetValue(null)!;
            open.Add("Cpu#0"); open.Add("Network#0");
            s.FloatPositions["0:Clock#0"] = new double[] { 40, 40 };
        }
        var view = new SidebarView(s, p);
        if (floating || evoStyle == WidgetStyle.Dial) { var h2 = new History(); for (int i = 0; i < 400; i++) { var sn = source.Read(); h2.Record(sn); view.Update(sn with { Battery = null }, h2); } }
        view.Update(snapshot, history);
        var host = new Border { Child = view, Background = floating ? new Avalonia.Media.LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative), GradientStops = { new Avalonia.Media.GradientStop(Avalonia.Media.Color.Parse("#1d3b5c"), 0), new Avalonia.Media.GradientStop(Avalonia.Media.Color.Parse("#4a2a5a"), 1) } } : null };
        Shot(host, (evoStyle == WidgetStyle.Dial ? "dial" : "") + name, floating ? 1440 : 330, floating ? 900 : (evoStyle == WidgetStyle.Dial ? 2000 : 1060), p);
    }
    {
        var field = typeof(SettingsWindow).GetField("pageIndex", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var s = Environment.GetEnvironmentVariable("STARTER") == "1" ? Settings.Starter() : new Settings(); s.Language = language;
        var cpu = s.Widgets.First(w => w.Kind == WidgetKind.Cpu);
        cpu.Style = WidgetStyle.Evolution;
        var p = Palette.For(true, false);
        foreach (int page in new[] { 0, 1, 2, 3 })
        {
            field.SetValue(null, page);
            var window = new SettingsWindow(s, p, () => { }, () => new[] { "Monitor 1", "Monitor 2" }, new PreviewPlatform(), snapshot: () => snapshot);
            ((HashSet<WidgetKind>)typeof(SettingsWindow).GetField("expanded", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(window)!).Add(WidgetKind.Cpu);
            typeof(SettingsWindow).GetMethod("Build", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(window, null);
            window.Height = 2400;
            window.Show();
            for (int i = 0; i < 4; i++) { Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame(); }
            using var image = window.CaptureRenderedFrame()!;
            image.Save(Path.Combine(output, $"evo-settings-{page}.png"));
            window.Close();
        }
    }
    Console.WriteLine("Preview rendered: " + output);
    return;
}
foreach (var style in styles)
foreach (bool dark in new[] { true, false })
{
    if (!dark && !args.Contains("--light")) continue;
    var s = new Settings { Language = language, Glass = false, Theme = dark ? ThemeMode.Dark : ThemeMode.Light, Density = Density.Fit, Columns = 2 };
    if (Environment.GetEnvironmentVariable("BG") is string bg) s.Background = bg;
    if (Environment.GetEnvironmentVariable("SCHEME") is string scheme) s.Colors = scheme;
    foreach (var w in s.Widgets) { w.Visible = kinds.Contains(w.Kind); if (Settings.HasStyles(w.Kind)) w.Style = style; }
    var p = Palette.For(s, dark, null);
    var view = new SidebarView(s, p);
    view.Update(snapshot, history);
    Shot(view, $"side-{style}-{(dark ? "dark" : "light")}", 640, 1040, p);
}
if (args.Contains("--bar"))
foreach (var style in styles)
{
    double barH = double.Parse(Environment.GetEnvironmentVariable("BAR_H") ?? "260");
    double barW = double.Parse(Environment.GetEnvironmentVariable("BAR_W") ?? "1600");
    var s = new Settings { Language = language, Glass = false, Edge = Edge.Top, BarHeight = barH, Density = Density.Fit, Columns = int.Parse(Environment.GetEnvironmentVariable("COLS") ?? "1") };
    foreach (var w in s.Widgets) { w.Visible = kinds.Contains(w.Kind); if (Settings.HasStyles(w.Kind)) w.Style = style; }
    var p = Palette.For(s, true, null);
    var view = new SidebarView(s, p);
    view.Update(snapshot, history);
    view.Update(snapshot, history);
    Shot(view, $"bar-{style}", (int)barW, (int)barH + 40, p);
}
if (args.Contains("--settings"))
{
    var field = typeof(SettingsWindow).GetField("pageIndex", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
    int count = args.Contains("--pages6") ? 6 : 6;
    for (int page = 0; page < count; page++)
    {
        field.SetValue(null, page);
        var s = Environment.GetEnvironmentVariable("STARTER") == "1" ? Settings.Starter() : new Settings(); s.Language = language;
        var p = Palette.For(true, false);
        var window = new SettingsWindow(s, p, () => { }, () => new[] { "Monitor 1", "Monitor 2" }, new PreviewPlatform(), snapshot: () => snapshot);
        window.Height = double.Parse(Environment.GetEnvironmentVariable("SETTINGS_H") ?? "780");
        window.Show();
        for (int i = 0; i < 4; i++) { Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame(); }
        using var image = window.CaptureRenderedFrame()!;
        image.Save(Path.Combine(output, $"settings-{page}.png"));
        window.Close();
    }
}
if (args.Contains("--cards"))
{
    if (int.TryParse(Environment.GetEnvironmentVariable("DRIVES"), out int driveCount) && snapshot.Disks.Count > 0)
    {
        var letters = new[] { "C:", "G:", "F:", "H:", "I:", "J:", "K:", "L:" };
        var temps = new double[] { 56, 59, 34, 41, 38, 45, 36, 49 };
        var template = snapshot.Disks[0];
        snapshot = snapshot with { Disks = Enumerable.Range(0, driveCount).Select(i => template with { Id = "demo" + i, Letters = letters[i % 8], Temperature = temps[i % 8], UsedPercent = 20 + 9 * i }).ToArray() };
    }
    var pairs = new List<(WidgetStyle, WidgetKind)>();
    foreach (var item in Environment.GetEnvironmentVariable("CARDS")!.Split(','))
    { var bits = item.Split(':'); pairs.Add((Enum.Parse<WidgetStyle>(bits[0]), Enum.Parse<WidgetKind>(bits[1]))); }
    Cardshots.Run(output, snapshot, history, pairs);
}
if (args.Contains("--dupes")) Dupes.Run(output, snapshot, history);
Console.WriteLine("Preview rendered: " + output);

sealed class PreviewPlatform : Lumen.App.Platform { public override bool IsAdministrator => false; public override bool AutostartEnabled => false; }
sealed class PreviewApp : Application { public override void Initialize() => Styles.Add(new FluentTheme()); }
