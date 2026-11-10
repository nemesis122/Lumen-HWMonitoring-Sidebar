using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Lumen.App;
using Lumen.Core;
using Xunit;

public class DesignSwitchCrashTests
{
    public static IEnumerable<object[]> Styles() => Settings.Selectable.Select(style => new object[] { style });

    [Theory, MemberData(nameof(Styles))]
    public void Design_switches_settle_in_a_narrow_fitted_sidebar(WidgetStyle style) => UiHost.Run(() =>
    {
        while (UiHost.LayoutWarnings.TryDequeue(out _)) { }
        var settings = new Settings { Width = 300, Pages = 2, Edge = Edge.Right, Density = Density.Fit };
        var active = new[] { WidgetKind.Cpu, WidgetKind.Gpu, WidgetKind.Memory, WidgetKind.Drives, WidgetKind.Network };
        foreach (var slot in settings.Widgets) { slot.Visible = active.Contains(slot.Kind); slot.Page = 1; }
        var (snap, history) = Render.Demo();
        var window = new Window { Width = 300, Height = 1080 };
        try
        {
            window.Show();
            foreach (var next in new[] { WidgetStyle.FillBarsGraph, style, WidgetStyle.PillRowGraph, style })
            {
                foreach (var slot in settings.Widgets) slot.Style = next;
                var view = new SidebarView(settings, Palette.For(true, false)); window.Content = view;
                foreach (double height in new[] { 1000d, 1080d, 1200d })
                {
                    window.Height = height;
                    for (int tick = 0; tick < 4; tick++)
                    { view.Update(snap with { Time = snap.Time.AddSeconds(tick) }, history); Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame(); }
                }
            }
            Assert.Empty(UiHost.LayoutWarnings);
        }
        finally { window.Close(); }
        return true;
    });
    [Theory]
    [InlineData(1080d)]
    [InlineData(1071.2d)]
    [InlineData(1000d)]
    [InlineData(1200d)]
    public void Capsule_graph_settles_at_the_reported_sidebar_size(double height) => UiHost.Run(() =>
    {
        while (UiHost.LayoutWarnings.TryDequeue(out _)) { }
        string? replay = Environment.GetEnvironmentVariable("LUMEN_CRASH_SETTINGS");
        var settings = replay != null ? Settings.Load(replay) : new Settings { Width = 300, Pages = 2, Edge = Edge.Right, Density = Density.Fit };
        if (replay == null)
        {
            var drives = settings.Widgets.Single(w => w.Kind == WidgetKind.Drives);
            drives.HiddenSensors.Add("drives.used"); drives.HiddenSensors.Add("drives.count");
        }
        var active = new[] { WidgetKind.Cpu, WidgetKind.Gpu, WidgetKind.Memory, WidgetKind.Drives, WidgetKind.Network };
        foreach (var slot in settings.Widgets)
        { slot.Visible = active.Contains(slot.Kind); slot.Page = 1; slot.Style = WidgetStyle.PillRowGraph; }
        var (demo, history) = Render.Demo();
        var snap = demo with {
            Cpu = demo.Cpu! with { Name = "AMD Ryzen 7 9800X3D", Temperature = 42, Load = 4 },
            Gpus = demo.Gpus.Take(1).Select(g => g with { Name = "AMD Radeon RX 9070 XT", Temperature = 52, Load = 6 }).ToArray(),
            Disks = demo.Disks.Take(2).Select((d, i) => d with { Name = i == 0 ? "Samsung SSD 990 PRO 4TB" : "Samsung SSD 990 PRO 2TB", Letters = i == 0 ? "G:" : "C:" }).ToArray()
        };
        var view = new SidebarView(settings, Palette.For(true, false));
        var window = new Window { Content = view, Width = 300, Height = height };
        int layouts = 0; window.LayoutUpdated += (_, _) => layouts++;
        try
        {
            window.Show();
            for (int tick = 0; tick < 12; tick++)
            {
                view.Update(snap with { Time = snap.Time.AddSeconds(tick) }, history);
                Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame();
            }
            int before = layouts;
            for (int frame = 0; frame < 6; frame++) { Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame(); }
            Assert.InRange(layouts - before, 0, 1);
            Assert.Empty(UiHost.LayoutWarnings);
        }
        finally { window.Close(); }
        return true;
    }, seconds: 45);
}
