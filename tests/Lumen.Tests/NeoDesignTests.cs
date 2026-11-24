using Avalonia.VisualTree;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Lumen.App;
using Lumen.Core;
using Xunit;

/// <summary>
/// The Cyberpunk and Neumorph designs (5.1.0): every design shows all parts of every hardware variant, nothing overlaps or
/// leaves the card, values do not jump, the chosen value/label colours and fonts reach them, a chosen accent replaces the
/// design colours, the opacity setting works. LUMEN_WRITE_NEO=&lt;folder&gt; writes a picture of each design.
/// </summary>
public class NeoDesignTests
{
    public static IEnumerable<object[]> Styles => NeoDesigns.All.Select(l => new object[] { l.Style });
    public static IEnumerable<object[]> StylesOnHardware => NeoDesigns.All.SelectMany(l => HardwareVariants.Names.Select(h => new object[] { l.Style, h }));

    internal static Settings Sidebar(WidgetStyle style)
    {
        var s = Settings.Starter(); s.HiddenValues.Clear(); DesignWidgetSelection.Apply(s, style); AioDesigns.ShowAllParts(s);
        foreach (var w in s.Widgets) w.HiddenSensors.Clear();
        foreach (var slot in s.Widgets.Where(w => AioDesigns.Hardware(w.Kind))) slot.Style = style;
        return s;
    }

    internal static (AioTile Card, Window Window) Show(WidgetStyle style, string hardware = "demo", Action<Settings>? change = null, double width = 340)
    {
        var s = Sidebar(style); change?.Invoke(s);
        var slot = s.Widgets.Single(w => w.Kind == WidgetKind.Overview);
        var (snap, h) = HardwareVariants.For(hardware);
        var card = new AioTile(style, Palette.For(s, true, null), Metrics.For(Density.Fit), s.ForSlot(slot)); card.Own(slot, 0);
        card.Update(snap, h);
        var window = new Window { Width = width, Height = 4000, Content = new ScrollViewer { Content = card } };
        window.Show(); card.Update(snap, h); Dispatcher.UIThread.RunJobs();
        return (card, window);
    }

    [Fact]
    public void Writes_pictures_on_request() => UiHost.Run(() =>
    {
        var folder = Environment.GetEnvironmentVariable("LUMEN_WRITE_NEO");
        if (string.IsNullOrEmpty(folder)) return true;
        Directory.CreateDirectory(folder);
        var hardware = Environment.GetEnvironmentVariable("LUMEN_NEO_HARDWARE") ?? "demo";
        var only = Environment.GetEnvironmentVariable("LUMEN_NEO_STYLES")?.Split(',', StringSplitOptions.RemoveEmptyEntries);
        foreach (var look in NeoDesigns.All.Where(l => only == null || only.Contains(l.Style.ToString())))
        {
            var s = Sidebar(look.Style); s.Glass = false; s.Width = 340;
            var view = new SidebarView(s, Palette.For(s, true, null));
            var (snap, h) = HardwareVariants.For(hardware);
            var window = new Window { Width = SidebarView.WindowWidth(s), Height = 2400, Content = view };
            try
            {
                window.Show();
                for (int i = 0; i < 3; i++) { view.Update(snap, h); Dispatcher.UIThread.RunJobs(); }
                var card = view.GetLogicalDescendants().OfType<AioTile>().First();
                window.Height = Math.Min(2400, card.Bounds.Height + 120); Dispatcher.UIThread.RunJobs();
                using var frame = window.CaptureRenderedFrame()!;
                frame.Save(Path.Combine(folder, $"{look.Style}-{hardware}.png"));
            }
            finally { window.Close(); }
        }
        return true;
    });
}

public class NeoDesignChecks
{
    private static IEnumerable<TextBlock> Texts(Control card) => card.GetLogicalDescendants().OfType<TextBlock>()
        .Where(t => !string.IsNullOrWhiteSpace(t.Text) && t.Text != "⁠" && SensorDetails.Visible(t, card) && t.Bounds.Width > 0);

    private static Rect On(Control c, Visual card)
    {
        var at = c.TranslatePoint(new Point(0, 0), card) ?? new Point();
        // A value that is shrunk to fit is drawn smaller than its layout box: use what is drawn.
        var m = c.TransformToVisual(card);
        return m is { } t ? new Rect(c.Bounds.Size).TransformToAABB(t) : new Rect(at, c.Bounds.Size);
    }

    [Theory, MemberData(nameof(NeoDesignTests.StylesOnHardware), MemberType = typeof(NeoDesignTests))]
    public void Every_reading_is_shown_once_inside_the_card_and_no_text_overlaps(WidgetStyle style, string hardware) => UiHost.Run(() =>
    {
        foreach (double width in new[] { 300.0, 420.0 })
        {
            var (card, window) = NeoDesignTests.Show(style, hardware, width: width);
            try
            {
                var s = NeoDesignTests.Sidebar(style); var slot = s.Widgets.Single(w => w.Kind == WidgetKind.Overview);
                var (snap, _) = HardwareVariants.For(hardware);
                var shown = AioSensorSelectionTests.Shown(card).Select(i => i.Key).ToHashSet();
                foreach (var r in AioReadings.For(snap, s.ForSlot(slot)))
                {
                    if (r.Group == "Cores" && !AioReadings.Selected(s.ForSlot(slot), "aio.cpu.cores")) continue;
                    Assert.True(shown.Contains(r.Key), $"{style} {hardware} {width}: {r.Key} ({r.Label}) is not shown");
                }
                var texts = Texts(card).Select(t => (t, Box: On(t, card))).ToList();
                var inner = new Rect(card.Bounds.Size).Inflate(.75);
                foreach (var (t, box) in texts)
                    Assert.True(inner.Contains(box), $"{style} {hardware} {width}: '{t.Text}' leaves the card ({box} in {card.Bounds.Size})");
                for (int i = 0; i < texts.Count; i++)
                    for (int j = i + 1; j < texts.Count; j++)
                    {
                        var a = texts[i].Box.Deflate(1); var b = texts[j].Box.Deflate(1);
                        if (a.Width <= 0 || b.Width <= 0 || a.Height <= 0 || b.Height <= 0) continue;
                        Assert.False(a.Intersects(b), $"{style} {hardware} {width}: '{texts[i].t.Text}' overlaps '{texts[j].t.Text}' ({a} / {b})");
                    }
            }
            finally { window.Close(); }
        }
        return true;
    });

    [Theory, MemberData(nameof(NeoDesignTests.Styles), MemberType = typeof(NeoDesignTests))]
    public void Chosen_value_and_label_colours_and_fonts_reach_every_text(WidgetStyle style) => UiHost.Run(() =>
    {
        var value = Color.Parse("#00FF66"); var label = Color.Parse("#FF00CC");
        var (card, window) = NeoDesignTests.Show(style, change: s => { s.ValueColor = "#00FF66"; s.LabelColor = "#FF00CC"; s.ValueFont = "roboto-mono"; s.LabelFont = "lato"; });
        try
        {
            var s = NeoDesignTests.Sidebar(style); s.ValueColor = "#00FF66"; s.LabelColor = "#FF00CC"; s.ValueFont = "roboto-mono"; s.LabelFont = "lato";
            TextColours.Apply(card, s); Dispatcher.UIThread.RunJobs();
            var values = card.ValueTexts.Where(t => SensorDetails.Visible(t, card) && !string.IsNullOrWhiteSpace(t.Text)).ToList();
            Assert.NotEmpty(values);
            foreach (var t in values)
            {
                var c = ((ISolidColorBrush)t.Foreground!).Color;
                Assert.True(c.R == value.R && c.G == value.G && c.B == value.B, $"{style}: value '{t.Text}' is {c}, not the chosen value colour");
                Assert.Contains("Roboto Mono", t.FontFamily.ToString());
            }
            int labels = 0;
            foreach (var t in Texts(card).Where(t => !card.ValueTexts.Contains(t)))
            {
                var c = ((ISolidColorBrush)t.Foreground!).Color;
                if (TextFonts.RoleOf(t) == TextRole.Title) continue; // headings keep the design colour
                if (c.R == label.R && c.G == label.G && c.B == label.B) { labels++; continue; }
                // A unit after a number (°C, %) and the black text on a coloured badge are part of the design.
                Assert.False(TextColours.Neutral(c) && t.Text is not ("%" or "°C" or "°F"), $"{style}: label '{t.Text}' kept {c}");
            }
            Assert.True(labels > 5, $"{style}: only {labels} labels took the label colour");
        }
        finally { window.Close(); }
        return true;
    });

    [Theory, MemberData(nameof(NeoDesignTests.Styles), MemberType = typeof(NeoDesignTests))]
    public void A_chosen_accent_replaces_the_design_colours_and_the_opacity_is_kept(WidgetStyle style) => UiHost.Run(() =>
    {
        var (card, window) = NeoDesignTests.Show(style, change: s => { s.Widgets.Single(w => w.Kind == WidgetKind.Overview).Accent = "#00FF88"; s.Glass = true; s.Opacity = .4; });
        try
        {
            var look = NeoDesigns.For(style);
            var accent = Color.Parse("#00FF88");
            var titles = card.GetLogicalDescendants().OfType<TextBlock>().Where(t => TextFonts.RoleOf(t) == TextRole.Title && SensorDetails.Visible(t, card)).ToList();
            if (look.Kind == NeoDesigns.Family.Cyber)
                Assert.All(titles, t => Assert.Equal(accent, ((ISolidColorBrush)t.Foreground!).Color));
            foreach (var c in look.Accents.Select(Color.Parse).Where(c => c != accent))
                Assert.DoesNotContain(card.GetLogicalDescendants().OfType<Border>(), b => b.BorderBrush is ISolidColorBrush brush && brush.Color == c && b.BorderThickness.Left >= 3);
            var back = Assert.IsAssignableFrom<ISolidColorBrush>(card.Background);
            Assert.InRange(back.Color.A, 90, 115);
        }
        finally { window.Close(); }
        return true;
    });

    [Theory, MemberData(nameof(NeoDesignTests.Styles), MemberType = typeof(NeoDesignTests))]
    public void Values_do_not_move_when_the_digits_change(WidgetStyle style) => UiHost.Run(() =>
    {
        var (card, window) = NeoDesignTests.Show(style);
        try
        {
            var (snap, h) = HardwareVariants.For("demo");
            Rect Place(TextBlock t) => On(t, card);
            var before = Texts(card).ToDictionary(t => t, Place);
            var changed = snap with
            {
                Cpu = snap.Cpu! with { Temperature = 95, Load = 100 },
                Gpus = snap.Gpus.Select(g => g with { Temperature = 99, Load = 7, PowerW = 412 }).ToList()
            };
            card.Update(changed, h); Dispatcher.UIThread.RunJobs();
            foreach (var (t, box) in before)
            {
                if (t.GetVisualRoot() == null || card.ValueTexts.Contains(t)) continue;
                var now = Place(t);
                Assert.True(Math.Abs(now.X - box.X) < .6 && Math.Abs(now.Y - box.Y) < .6, $"{style}: '{t.Text}' moved from {box.TopLeft} to {now.TopLeft} when the readings changed");
            }
        }
        finally { window.Close(); }
        return true;
    });
}
