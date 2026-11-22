using Avalonia.Headless;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Lumen.App;
using Lumen.Core;
using Xunit;

/// <summary>
/// The living "Ocean" background behind the cards: off by default, lies under every design, follows temperature and load,
/// and makes the cards see-through so it can be seen. LUMEN_WRITE_OCEAN=&lt;folder&gt; writes preview pictures of the favourite
/// designs at idle and under gaming load.
/// </summary>
public class LiveBackgroundTests
{
    private static readonly WidgetStyle[] Favourites =
    {
        WidgetStyle.AioIsometricTiles, WidgetStyle.AioIsometricTilesRed, WidgetStyle.AioIsometricAtlas,
        WidgetStyle.AioAuroraFusion, WidgetStyle.AioPanel, WidgetStyle.AioMainboardMap
    };

    private static Settings Sidebar(WidgetStyle style, string live)
    {
        var s = Settings.Starter(); s.LiveBackground = live; AioDesigns.ShowAllParts(s);
        foreach (var slot in s.Widgets.Where(w => AioDesigns.Hardware(w.Kind))) slot.Style = style;
        return s;
    }

    private static Snapshot Gaming(Snapshot s) => s with
    {
        Cpu = s.Cpu! with { Temperature = 79, Load = 64, CoreLoads = s.Cpu!.CoreLoads.Select((_, i) => 35.0 + (i * 37 % 60)).ToList() },
        Gpus = s.Gpus.Select(g => g with { Temperature = 73, HotSpot = 86, Load = 98, PowerW = 296 }).ToList()
    };

    [Fact]
    public void Off_by_default_and_only_known_backgrounds_survive()
    {
        Assert.Equal("off", new Settings().LiveBackground);
        var s = new Settings { LiveBackground = "lava" }; Settings.Normalize(s); Assert.Equal("off", s.LiveBackground);
        s.LiveBackground = "ocean"; Settings.Normalize(s); Assert.Equal("ocean", s.LiveBackground);
    }

    [Fact]
    public void Ocean_lies_under_the_cards_of_every_favourite_design() => UiHost.Run(() =>
    {
        var (snap, history) = Render.Demo();
        foreach (var style in Favourites)
        {
            foreach (var live in new[] { "off", "ocean" })
            {
                var s = Sidebar(style, live);
                var view = new SidebarView(s, Palette.For(s, true, null)); view.Update(snap, history);
                var window = new Window { Width = SidebarView.WindowWidth(s), Height = 1200, Content = view };
                try
                {
                    window.Show(); Dispatcher.UIThread.RunJobs();
                    var backdrops = view.GetLogicalDescendants().OfType<LiveBackdrop>().ToArray();
                    if (live == "off") { Assert.Empty(backdrops); continue; }
                    var backdrop = Assert.Single(backdrops);
                    var panel = Assert.IsType<Panel>(backdrop.Parent);
                    Assert.Same(backdrop, panel.Children[0]);                       // under everything else
                    Assert.False(backdrop.IsHitTestVisible);                        // clicks still reach the cards
                    Assert.Equal(view.Bounds.Width - view.BorderThickness.Left - view.BorderThickness.Right, backdrop.Bounds.Width, 1);
                    using var frame = window.CaptureRenderedFrame();
                }
                finally { window.Close(); }
            }
        }
        return true;
    });

    [Fact]
    public void Water_rises_with_temperature_and_waves_follow_the_load() => UiHost.Run(() =>
    {
        var (snap, _) = Render.Demo();
        var calm = snap with { Cpu = snap.Cpu! with { Temperature = 40, Load = 5 }, Gpus = snap.Gpus.Select(g => g with { Load = 3 }).ToList() };
        var backdrop = new LiveBackdrop("ocean");
        backdrop.Feed(calm); for (int i = 0; i < 300; i++) backdrop.Advance(1.0 / 30);
        double calmLevel = backdrop.Level, calmEnergy = backdrop.Energy;
        backdrop.Feed(Gaming(snap)); for (int i = 0; i < 300; i++) backdrop.Advance(1.0 / 30);
        Assert.True(backdrop.Level > calmLevel + 0.25, $"Water should rise when hot ({calmLevel:F2} → {backdrop.Level:F2}).");
        Assert.True(backdrop.Energy > calmEnergy + 0.8, $"Waves should grow under load ({calmEnergy:F2} → {backdrop.Energy:F2}).");
        // One step never jumps: the water glides.
        backdrop.Feed(calm); double before = backdrop.Level; backdrop.Advance(1.0 / 30);
        Assert.InRange(before - backdrop.Level, 0.0, 0.03);
        // Missing sensors leave a calm, low sea instead of an error.
        var none = snap with { Cpu = null, Gpus = Array.Empty<GpuInfo>() };
        backdrop.Feed(none); for (int i = 0; i < 300; i++) backdrop.Advance(1.0 / 30);
        Assert.InRange(backdrop.Level, 0.2, 0.4); Assert.InRange(backdrop.Energy, 0.0, 0.05);
        return true;
    });

    [Fact]
    public void Cards_become_see_through_only_with_a_live_background()
    {
        var off = new Settings { Glass = false, Opacity = 0.55 };
        Assert.Equal(255, Palette.For(off, true, null).Card.A);
        var on = new Settings { Glass = false, Opacity = 0.55, LiveBackground = "ocean" };
        Assert.InRange(Palette.For(on, true, null).Card.A, 120, 150);
        on.Opacity = 1.0; Assert.True(Palette.For(on, true, null).Card.A >= 249);
    }

    [Fact]
    public void Writes_previews_on_request() => UiHost.Run(() =>
    {
        var folder = Environment.GetEnvironmentVariable("LUMEN_WRITE_OCEAN");
        if (string.IsNullOrEmpty(folder)) return true;
        Directory.CreateDirectory(folder);
        var (idle, history) = Render.Demo();
        foreach (var style in Favourites)
            foreach (var (name, snap) in new[] { ("idle", idle), ("gaming", Gaming(idle)) })
            {
                var s = Sidebar(style, "ocean");
                var view = new SidebarView(s, Palette.For(s, true, null));
                var window = new Window { Width = SidebarView.WindowWidth(s), Height = 1200, Content = view };
                try
                {
                    window.Show();
                    for (int i = 0; i < 3; i++) { view.Update(snap, history); Dispatcher.UIThread.RunJobs(); }
                    var backdrop = view.GetLogicalDescendants().OfType<LiveBackdrop>().Single();
                    for (int i = 0; i < 240; i++) backdrop.Advance(1.0 / 30);
                    Dispatcher.UIThread.RunJobs();
                    using var frame = window.CaptureRenderedFrame()!;
                    frame.Save(Path.Combine(folder, $"{style}-{name}.png"));
                }
                finally { window.Close(); }
            }
        return true;
    });

    private static double Ease(double x) => x <= 0 ? 0 : x >= 1 ? 1 : x * x * (3 - 2 * x);

    private static Snapshot Between(Snapshot idle, Snapshot hot, double f)
    {
        double L(double? a, double? b) => (a ?? 0) + ((b ?? 0) - (a ?? 0)) * f;
        return idle with
        {
            Cpu = idle.Cpu! with { Temperature = L(idle.Cpu!.Temperature, hot.Cpu!.Temperature), Load = L(idle.Cpu.Load, hot.Cpu.Load),
                CoreLoads = idle.Cpu.CoreLoads.Select((c, i) => L(c, hot.Cpu.CoreLoads[i])).ToList() },
            Gpus = idle.Gpus.Select((g, i) => g with { Temperature = L(g.Temperature, hot.Gpus[i].Temperature), HotSpot = L(g.HotSpot, hot.Gpus[i].HotSpot),
                Load = L(g.Load, hot.Gpus[i].Load), PowerW = L(g.PowerW, hot.Gpus[i].PowerW) }).ToList()
        };
    }

    /// <summary>LUMEN_OCEAN_FRAMES=&lt;folder&gt;: 12 s at 30 frames per second of every favourite design, idle → gaming → idle.</summary>
    [Fact]
    public void Writes_video_frames_on_request() => UiHost.Run(() =>
    {
        var folder = Environment.GetEnvironmentVariable("LUMEN_OCEAN_FRAMES");
        if (string.IsNullOrEmpty(folder)) return true;
        var (idle, history) = Render.Demo(); var hot = Gaming(idle);
        foreach (var style in Favourites)
        {
            var dir = Path.Combine(folder, style.ToString()); Directory.CreateDirectory(dir);
            var s = Sidebar(style, "ocean");
            var view = new SidebarView(s, Palette.For(s, true, null));
            var window = new Window { Width = SidebarView.WindowWidth(s), Height = 1200, Content = view };
            try
            {
                window.Show(); view.Update(idle, history); Dispatcher.UIThread.RunJobs();
                var backdrop = view.GetLogicalDescendants().OfType<LiveBackdrop>().Single();
                for (int i = 0; i < 90; i++) backdrop.Advance(1.0 / 30);   // settled before the film starts
                for (int frame = 0; frame < 360; frame++)
                {
                    double t = frame / 30.0;
                    if (frame % 15 == 0) view.Update(Between(idle, hot, Ease((t - 2.5) / 2.5) - Ease((t - 8.5) / 2.5)), history);
                    backdrop.Advance(1.0 / 30); Dispatcher.UIThread.RunJobs();
                    using var shot = window.CaptureRenderedFrame()!;
                    shot.Save(Path.Combine(dir, $"{frame:D4}.png"));
                }
                File.AppendAllText(Path.Combine(folder, "log.txt"), $"{style} level {backdrop.Level:F2} energy {backdrop.Energy:F2}\n");
            }
            finally { window.Close(); }
        }
        return true;
    });
}
