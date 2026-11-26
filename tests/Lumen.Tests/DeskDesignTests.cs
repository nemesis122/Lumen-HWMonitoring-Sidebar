using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lumen.App;
using Lumen.Core;
using Xunit;

/// <summary>5.8.0: the full-screen desktop designs on every monitor shape.</summary>
public class DeskDesignTests
{
        // Real work areas (the taskbar taken off): 16:9, the same at 125 % (1536 × 864 logical), 1440p, 21:9, 5:4 and upright.
    internal static readonly Size[] Screens = { new(1920, 1040), new(1536, 824), new(2560, 1400), new(3440, 1400), new(1280, 984), new(1080, 1880) };
    public static IEnumerable<object[]> Styles => NeoDesigns.All.Where(l => l.Kind == NeoDesigns.Family.Desk).Select(l => new object[] { l.Style });

    /// <summary>The sidebar as after choosing the design in the settings: full screen, one column.</summary>
    internal static Settings FullScreen(WidgetStyle style)
    {
        var s = Settings.Starter(); s.HiddenValues.Clear(); DesignWidgetSelection.Apply(s, style); AioDesigns.ShowAllParts(s);
        foreach (var w in s.Widgets) w.HiddenSensors.Clear();
        foreach (var slot in s.Widgets.Where(w => AioDesigns.Hardware(w.Kind))) slot.Style = style;
        s.FullScreen = true; s.Floating = false; s.Columns = 1; s.Glass = false;
        return s;
    }

    internal static (SidebarView View, Window Window) Show(WidgetStyle style, string hardware, Size screen, Action<Settings>? change = null)
    {
        var s = FullScreen(style);
        var (snap, h) = HardwareVariants.For(hardware);
        if (Environment.GetEnvironmentVariable("LUMEN_DESK_ALL") == "1")
        {
            s.ShowIntegratedGpu = true;
            foreach (var w in s.Widgets.Where(w => w.Kind == WidgetKind.Overview)) w.AioEnabledSensors = AioReadings.Options(snap).Select(o => o.Key).ToList();
        }
        change?.Invoke(s);
        var view = new SidebarView(s, Palette.For(s, true, null));
        var window = new Window { Width = screen.Width, Height = screen.Height, Content = view };
        window.Show();
        for (int i = 0; i < 3; i++) { view.Update(snap, h); Dispatcher.UIThread.RunJobs(); }
        return (view, window);
    }

    public static IEnumerable<object[]> StylesOnHardware => from l in NeoDesigns.All where l.Kind == NeoDesigns.Family.Desk
                                                            from h in HardwareVariants.Names select new object[] { l.Style, h };

    /// <summary>
    /// On every monitor shape (16:9, the same at 125 %, 1440p, 21:9, 5:4, upright) and every test PC: the design fills the screen
    /// without scrolling, shows every reading, draws no text below 9 px, nothing leaves the screen and no two texts overlap.
    /// </summary>
    [Theory, MemberData(nameof(StylesOnHardware))]
    public void Fills_every_screen_with_every_value_readable(WidgetStyle style, string hardware) => UiHost.Run(() =>
    {
        foreach (var screen in Screens)
        {
            var (view, window) = Show(style, hardware, screen);
            try
            {
                using var frame = window.CaptureRenderedFrame();
                var scene = view.GetVisualDescendants().OfType<DeskScene>().Single();
                var card = view.GetLogicalDescendants().OfType<AioTile>().Single();
                string at = $"{style} {hardware} {screen.Width}x{screen.Height}";
                Assert.Equal(screen.Height > screen.Width ? "tall" : "wide", scene.Mode);
                // The scene's size is already the drawn one; its own scale must not be applied twice.
                var bottom = scene.TranslatePoint(new Point(0, scene.Bounds.Height / scene.Scale), view)!.Value.Y;
                // Only a big PC (four drives, eight fans, 32 threads, rails) may need more than a 125 % screen: then the sidebar scrolls.
                if (bottom > screen.Height + 1)
                    Assert.True(hardware == "big-rig" && scene.Scale <= DeskScene.SmallestScale + .001,
                        $"{at}: the design needs {bottom:F0} px, more than the screen (scale {scene.Scale:F2}).");
                var s = FullScreen(style); var slot = s.Widgets.Single(w => w.Kind == WidgetKind.Overview);
                var (snap, _) = HardwareVariants.For(hardware);
                var shown = AioSensorSelectionTests.Shown(card).Select(i => i.Key).ToHashSet();
                foreach (var r in AioReadings.For(snap, s.ForSlot(slot)))
                {
                    if (r.Group == "Cores" && !AioReadings.Selected(s.ForSlot(slot), "aio.cpu.cores")) continue;
                    Assert.True(shown.Contains(r.Key), $"{at}: {r.Key} ({r.Label}) is not shown");
                }
                var texts = card.GetLogicalDescendants().OfType<TextBlock>()
                    .Where(t => !string.IsNullOrWhiteSpace(t.Text) && t.Text != "⁠" && SensorDetails.Visible(t, card) && t.Bounds.Width > 0)
                    .Select(t => (t, Box: new Rect(t.Bounds.Size).TransformToAABB(t.TransformToVisual(view)!.Value))).ToList();
                // Scrolled (allowed above): the texts stay inside the design instead of the screen.
                var screenBox = new Rect(0, 0, screen.Width, Math.Max(screen.Height, bottom)).Inflate(1);
                foreach (var (t, box) in texts)
                {
                    var a = t.TranslatePoint(default, view)!.Value; var b = t.TranslatePoint(new Point(0, t.FontSize), view)!.Value;
                    Assert.True(b.Y - a.Y >= 8.95, $"{at}: '{t.Text}' is drawn at {b.Y - a.Y:F1} px.");
                    Assert.True(screenBox.Contains(box), $"{at}: '{t.Text}' leaves the screen ({box}).");
                }
                for (int i = 0; i < texts.Count; i++)
                    for (int j = i + 1; j < texts.Count; j++)
                    {
                        var a = texts[i].Box.Deflate(1); var b = texts[j].Box.Deflate(1);
                        if (a.Width <= 0 || b.Width <= 0 || a.Height <= 0 || b.Height <= 0) continue;
                        Assert.False(a.Intersects(b), $"{at}: '{texts[i].t.Text}' overlaps '{texts[j].t.Text}' ({a} / {b})");
                    }
            }
            finally { window.Close(); }
        }
        return true;
    });

    /// <summary>Choosing a desktop design in the settings switches the sidebar to full screen; leaving it switches back.</summary>
    [Fact]
    public void Choosing_a_desktop_design_switches_to_full_screen()
    {
        foreach (var look in NeoDesigns.All.Where(l => l.Kind == NeoDesigns.Family.Desk)) Assert.True(AioDesigns.Desktop(look.Style));
        Assert.True(AioDesigns.Desktop(WidgetStyle.AioMainboardDesktop));
        Assert.False(AioDesigns.Desktop(WidgetStyle.AioGlassDial));
    }

    [Fact]
    public void Writes_pictures_on_request() => UiHost.Run(() =>
    {
        var folder = Environment.GetEnvironmentVariable("LUMEN_WRITE_DESK");
        if (string.IsNullOrEmpty(folder)) return true;
        Directory.CreateDirectory(folder);
        var hardware = (Environment.GetEnvironmentVariable("LUMEN_DESK_HARDWARE") ?? "demo").Split(',');
        var only = Environment.GetEnvironmentVariable("LUMEN_DESK_STYLES")?.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var sizes = Environment.GetEnvironmentVariable("LUMEN_DESK_SIZES")?.Split(',').Select(x => x.Split('x')).Select(p => new Size(double.Parse(p[0]), double.Parse(p[1]))).ToArray() ?? Screens;
        foreach (var look in NeoDesigns.All.Where(l => l.Kind == NeoDesigns.Family.Desk && (only == null || only.Contains(l.Style.ToString()))))
            foreach (var hw in hardware)
                foreach (var size in sizes)
                {
                    var (view, window) = Show(look.Style, hw, size);
                    try
                    {
                        using var frame = window.CaptureRenderedFrame()!;
                        frame.Save(Path.Combine(folder, $"{look.Style}-{hw}-{size.Width}x{size.Height}.png"));
                    }
                    finally { window.Close(); }
                }
        return true;
    });
}
