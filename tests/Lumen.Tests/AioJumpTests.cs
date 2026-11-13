using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lumen.App;
using Lumen.Core;
using Xunit;

// The user saw the sidebar jump with some values: a number that shrank to fit its column moved up and down,
// and a wrapped rate ("0 kbit/s" on two lines) pushed everything below. With changing values — tiny and huge,
// readings dropping out — nothing in any AIO design may move once each value has been shown at its widest.
public class AioJumpTests
{
    public static IEnumerable<object[]> Styles() => AioDesigns.Styles.Select(s => new object[] { s });
    internal static Snapshot Vary(Snapshot s, int i)
    {
        bool hi = i % 2 == 0;
        var cpu = s.Cpu! with { Temperature = i % 4 == 0 ? 9 : 100, Load = hi ? 100 : 0, ClockMhz = i % 3 == 0 ? null : hi ? 5650 : 800, PowerW = hi ? 245 : 3, Voltage = hi ? 1.45 : 0.2 };
        var gpus = s.Gpus.Select(g => g with { Temperature = hi ? 99 : 8, Load = hi ? 100 : 0, PowerW = i % 3 == 0 ? null : hi ? 450 : 7, ClockMhz = hi ? 2950 : 210, HotSpot = hi ? 110 : 9 }).ToArray();
        var disks = s.Disks.Select(d => d with { ReadBytesPerSecond = hi ? 3.5e9 : 0, WriteBytesPerSecond = hi ? 0 : 999e6, Temperature = hi ? 70 : 9, UsedPercent = hi ? 100 : 1 }).ToArray();
        // Everyday rates too: 1.4 Mbit/s ↔ 106 Mbit/s ↔ 125 kbit/s ↔ 11.8 Mbit/s, besides 0 and the extremes.
        double[] rates = { 987e6, 0, 175e3, 13.25e6, 15.6e3, 1.475e6, 99e6, 1.2e3 };
        var net = s.Network! with { DownloadBytesPerSecond = rates[i % rates.Length], UploadBytesPerSecond = rates[(i + 3) % rates.Length] };
        return s with { Cpu = cpu, Gpus = gpus, Disks = disks, Network = net, Memory = new MemoryInfo(hi ? 63.9 : 1.2, 64), Battery = null };
    }

    private static string Fixture([System.Runtime.CompilerServices.CallerFilePath] string here = "") => Path.Combine(Path.GetDirectoryName(here)!, "Fixtures", "aio-sidebar-settings.json");

    [Theory]
    [MemberData(nameof(Styles))]
    public void Nothing_jumps_when_values_change(WidgetStyle style)
    {
      foreach (var (width, lang, real) in new[] { (260, "en", true), (240, "de", true), (320, "de", false), (240, "de", false) })
      {
        var report = UiHost.Run(() =>
        {
            // A real sidebar (narrow, filling the screen, every hardware widget as one AIO card, clocks above)
            // and a starter setup with every AIO value switched on (also link speed and core temperatures).
            var settings = real ? Settings.Load(Fixture()) : Settings.Starter();
            settings.Width = width; settings.Language = lang; settings.Density = Density.Fit;
            if (!real)
            {
                foreach (var w in settings.Widgets) { w.WidgetScale = 1.1; if (w.Kind is WidgetKind.Clock or WidgetKind.Calendar or WidgetKind.Weather) w.Visible = true; }
                AioDesigns.ShowAllParts(settings);
                var demo = new DemoSource().Read();
                foreach (var w in settings.Widgets.Where(w => w.Kind == WidgetKind.Overview)) w.AioEnabledSensors = AioReadings.Options(demo).Select(o => o.Key).ToList();
            }
            foreach (var w in settings.Widgets) if (Settings.HasStyles(w.Kind) && AioDesigns.Hardware(w.Kind)) w.Style = style;
            L.Use(settings.Language);
            var source = new DemoSource(); var history = new History();
            var view = new SidebarView(settings, Palette.For(true, false));
            var window = new Window { Content = view, Width = settings.Width, Height = 1400 };
            window.Show();
            var first = new Dictionary<Control, (double X, double Y, double W, double H, string T)>();
            var log = new List<string>();
            for (int i = 0; i < 24; i++)
            {
                var snap = source.Read(); history.Record(snap);
                view.Update(i < 6 ? snap with { Battery = null } : Vary(snap, i), history);
                for (int k = 0; k < 3; k++) { Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame(); }
                // From the third changed reading on: by then each value has been shown at its widest once (a value too wide for
                // its column shrinks once, the first time it gets that wide, and then keeps that size).
                if (i < 8) continue;
                foreach (var c in view.GetVisualDescendants().OfType<Control>().Where(c => c.IsEffectivelyVisible && (c is TextBlock || c is Panel || c is Decorator)))
                {
                    var p = c.TranslatePoint(default, view); if (p == null) continue;
                    if (!first.TryGetValue(c, out var f)) { first[c] = (p.Value.X, p.Value.Y, c.Bounds.Width, c.Bounds.Height, (c as TextBlock)?.Text ?? c.GetType().Name); continue; }
                    // Up/down and sideways: neither the place nor the size of anything may change with the values.
                    double d = new[] { p.Value.X - f.X, p.Value.Y - f.Y, c.Bounds.Width - f.W, c.Bounds.Height - f.H }.Max(Math.Abs);
                    if (d > 1.5 && log.Count < 25) log.Add($"u{i} {(c as TextBlock)?.Text ?? c.GetType().Name} ({f.T}): x {f.X:0}->{p.Value.X:0} y {f.Y:0}->{p.Value.Y:0} w {f.W:0}->{c.Bounds.Width:0} h {f.H:0}->{c.Bounds.Height:0}");
                }
            }
            window.Close();
            return string.Join("\n", log);
        });
            Assert.True(report.Length == 0, $"{style} {width} px {lang}: values moved\n{report}");
      }
    }
}
