using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Lumen.App;
using Lumen.Core;
using Xunit;
using Xunit.Abstractions;

// Language, typeface and motion are app-wide switches; tests that flip them must not overlap.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

// The real sidebar, rendered headless on one dedicated interface thread. A test that hangs
// there (an endless layout loop) ends with a timeout instead of blocking the whole run.
public static class UiHost
{
    private static readonly BlockingCollection<Action> queue = new();

    static UiHost()
    {
        using var ready = new ManualResetEventSlim();
        Exception? failed = null;
        var thread = new Thread(() =>
        {
            try
            {
                AppBuilder.Configure<TestApp>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).WithInterFont().SetupWithoutStarting();
                Motion.Enabled = false;
                Avalonia.Logging.Logger.Sink = new LayoutSink();
            }
            catch (Exception e) { failed = e; }
            ready.Set();
            foreach (var job in queue.GetConsumingEnumerable()) job();
        }) { IsBackground = true, Name = "Lumen UI tests" };
        thread.Start();
        ready.Wait();
        if (failed != null) throw failed;
    }

    public static T Run<T>(Func<T> work, int seconds = 90)
    {
        var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        queue.Add(() => { try { done.SetResult(work()); } catch (Exception e) { done.SetException(e); } });
        if (!done.Task.Wait(TimeSpan.FromSeconds(seconds)))
            throw new TimeoutException($"The interface did not finish within {seconds} s – the layout probably never comes to rest.");
        return done.Task.GetAwaiter().GetResult();
    }

    // Avalonia reports a layout that does not converge ("Layout cycle detected") as a warning.
    public static readonly ConcurrentQueue<string> LayoutWarnings = new();

    private sealed class LayoutSink : Avalonia.Logging.ILogSink
    {
        public bool IsEnabled(Avalonia.Logging.LogEventLevel level, string area) => level >= Avalonia.Logging.LogEventLevel.Warning && area == Avalonia.Logging.LogArea.Layout;
        public void Log(Avalonia.Logging.LogEventLevel level, string area, object? source, string messageTemplate) => LayoutWarnings.Enqueue(messageTemplate);
        public void Log(Avalonia.Logging.LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues) => LayoutWarnings.Enqueue(messageTemplate);
    }

    private sealed class TestApp : Application
    {
        public override void Initialize() => Styles.Add(new FluentTheme());
    }
}

// What every UI test needs: demo data, the view in a window, and pumping the dispatcher.
public static class Render
{
    public static (Snapshot, History) Demo()
    {
        var src = new DemoSource();
        var h = new History();
        Snapshot s = src.Read();
        for (int i = 0; i < 160; i++) { s = src.Read(); h.Record(s); }
        return (s with { Battery = null }, h);
    }

    // The same arrangements the screenshots use: every widget in one style, as a three-row bar
    // at the top and as the sidebar down the right edge.
    public static Settings Bar(WidgetStyle st)
    {
        var s = new Settings { Language = "de", Edge = Edge.Top, BarHeight = BarRow(st), Columns = 3, Glass = false };
        foreach (var w in s.Widgets) { w.Visible = w.Kind != WidgetKind.Notes; if (Settings.HasStyles(w.Kind)) w.Style = st; }
        return s;
    }

    public static Settings Side(WidgetStyle st)
    {
        var s = new Settings { Language = "de", Glass = false };
        foreach (var w in s.Widgets) { w.Visible = w.Kind is not (WidgetKind.Notes or WidgetKind.Battery or WidgetKind.Volumes or WidgetKind.DataUsage); if (Settings.HasStyles(w.Kind)) w.Style = st; }
        return s;
    }

    public static double BarRow(WidgetStyle st) => st switch
    {
        WidgetStyle.Status => 44, WidgetStyle.Compact => 72, WidgetStyle.Gauge or WidgetStyle.Segments or WidgetStyle.DualRing => 150, _ => 220
    };

    public static (Window Window, SidebarView View) Show(Settings settings, double width, double height)
    {
        L.Use(settings.Language);
        Ui.UseFont(settings.Font);
        var view = new SidebarView(settings, Palette.For(true, settings.Glass, settings.Opacity, null, settings.HighContrast, settings.Background));
        var w = new Window { Width = width, Height = height, Content = view };
        w.Show();
        return (w, view);
    }

    public static (int Width, int Height) Size(Settings s) => s.Horizontal
        ? (1600, (int)(s.BarHeight * s.Rows + (s.Rows - 1) * 10 + 56))
        : ((int)SidebarView.WindowWidth(s), 1392);
}

public class LayoutSettleTests
{
    private readonly ITestOutputHelper output;
    public LayoutSettleTests(ITestOutputHelper output) => this.output = output;

    public static IEnumerable<object[]> Cases() =>
        Enum.GetValues<WidgetStyle>().SelectMany(st => new[] { new object[] { st, true }, new object[] { st, false } });

    // Each update may lay the sidebar out once (new numbers, new chart points); without updates
    // nothing may be measured again. A layout that keeps re-arranging itself (the Segments hang
    // of 2.0) would burn CPU forever or freeze the sidebar; an endless loop ends in UiHost's timeout.
    private static (List<int> Busy, List<int> Idle, List<string> Warnings) Measure(Settings settings, double width, double height)
    {
        return UiHost.Run(() =>
        {
            while (UiHost.LayoutWarnings.TryDequeue(out _)) { }
            var (w, view) = Render.Show(settings, width, height);
            var (snap, hist) = Render.Demo();
            var src = new DemoSource(3);
            int layouts = 0;
            w.LayoutUpdated += (_, _) => layouts++;
            List<int> Frames(int n, bool update)
            {
                var counts = new List<int>();
                for (int i = 0; i < n; i++)
                {
                    int before = layouts;
                    if (update) { var s = src.Read() with { Battery = null }; hist.Record(s); view.Update(s, hist); }
                    Dispatcher.UIThread.RunJobs();
                    w.CaptureRenderedFrame();
                    Dispatcher.UIThread.RunJobs();
                    counts.Add(layouts - before);
                }
                return counts;
            }
            view.Update(snap, hist);
            var busy = Frames(10, true);
            var idle = Frames(6, false);
            w.Close();
            return (busy, idle, UiHost.LayoutWarnings.ToList());
        });
    }

    private void Check(string what, (List<int> Busy, List<int> Idle, List<string> Warnings) r)
    {
        output.WriteLine($"{what}: layout passes with new readings {string.Join(" ", r.Busy)} · without {string.Join(" ", r.Idle)}");
        Assert.True(r.Warnings.Count == 0, $"{what}: {string.Join("; ", r.Warnings.Distinct())}");
        Assert.True(r.Busy.Skip(3).All(n => n <= 2), $"{what} lays itself out again and again on every update: {string.Join(" ", r.Busy)}");
        Assert.True(r.Idle.Skip(1).All(n => n == 0), $"{what} never comes to rest: {string.Join(" ", r.Idle)}");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Every_style_comes_to_rest(WidgetStyle style, bool bar)
    {
        var settings = bar ? Render.Bar(style) : Render.Side(style);
        var (wd, ht) = Render.Size(settings);
        Check($"{style} as {(bar ? "bar" : "sidebar")}", Measure(settings, wd, ht));
    }

    // Different bar heights, from the slimmest strip to the tallest, with every style mixed in one row.
    [Theory]
    [InlineData(40)]
    [InlineData(90)]
    [InlineData(160)]
    [InlineData(300)]
    [InlineData(700)]
    public void Mixed_bar_comes_to_rest_at_any_height(double height)
    {
        var styles = Enum.GetValues<WidgetStyle>();
        var s = new Settings { Language = "en", Edge = Edge.Bottom, BarHeight = height, Columns = 1, Glass = false };
        int i = 0;
        foreach (var w in s.Widgets) { w.Visible = w.Kind != WidgetKind.Notes; if (Settings.HasStyles(w.Kind)) w.Style = styles[i++ % styles.Length]; }
        Check($"mixed bar {height} px", Measure(s, 1920, height + 40));
    }

    // Two and three columns rebalance once by real heights, then must stay put; also with large text and narrow fonts.
    [Theory]
    [InlineData(2, 1.0, "inter")]
    [InlineData(3, 1.0, "inter")]
    [InlineData(2, 1.6, "bahnschrift-narrow")]
    [InlineData(1, 0.5, "mono")]
    public void Columns_come_to_rest(int columns, double text, string font)
    {
        var s = new Settings { Language = "fr", Columns = columns, TextScale = text, Font = font, Glass = false };
        foreach (var w in s.Widgets) w.Visible = w.Kind != WidgetKind.Notes;
        Check($"{columns} columns, text {text:P0}, {font}", Measure(s, SidebarView.WindowWidth(s), 1392));
    }
}

// Every style as bar and as sidebar, compared pixel by pixel with the approved pictures in
// Reference/<os>. Anything that moved, got cut off or changed colour shows up here.
// LUMEN_UPDATE_REFERENCES=1 writes new reference pictures after an intended change.
// Fonts render a little differently on each system, so each OS has its own set; a system
// without one is skipped.
public class ImageCompareTests
{
    private const double MaxDifferentPixels = .002;
    private const int ChannelTolerance = 40;
    private readonly ITestOutputHelper output;
    public ImageCompareTests(ITestOutputHelper output) => this.output = output;

    private static string Os => OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsMacOS() ? "macos" : "linux";
    private static bool Updating => Environment.GetEnvironmentVariable("LUMEN_UPDATE_REFERENCES") == "1";

    // The reference folder in the source tree (when updating) or beside the test binaries.
    private static string ReferenceDir(bool source)
    {
        if (!source) return Path.Combine(AppContext.BaseDirectory, "Reference", Os);
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Lumen.Tests.csproj"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? AppContext.BaseDirectory, "Reference", Os);
    }

    public static IEnumerable<object[]> Cases() => LayoutSettleTests.Cases();

    [Theory]
    [MemberData(nameof(Cases))]
    public void Looks_like_the_reference(WidgetStyle style, bool bar)
    {
        string name = $"{(bar ? "bar" : "side")}-{style}.png";
        string reference = Path.Combine(ReferenceDir(false), name);
        if (!Updating && !Directory.Exists(ReferenceDir(false))) { output.WriteLine($"No reference pictures for {Os}; skipped."); return; }

        var (width, height, pixels) = UiHost.Run(() =>
        {
            var settings = bar ? Render.Bar(style) : Render.Side(style);
            var (wd, ht) = Render.Size(settings);
            var (w, view) = Render.Show(settings, wd, ht);
            var (snap, hist) = Render.Demo();
            for (int i = 0; i < 4; i++) { view.Update(snap, hist); Dispatcher.UIThread.RunJobs(); w.CaptureRenderedFrame(); }
            var frame = w.CaptureRenderedFrame()!;
            // Through PNG and back, so both sides are decoded the same way (same channel order).
            using var png = new MemoryStream();
            frame.Save(png);
            png.Position = 0;
            using var decoded = new Bitmap(png);
            var result = Pixels(decoded);
            if (Updating)
            {
                Directory.CreateDirectory(ReferenceDir(true));
                frame.Save(Path.Combine(ReferenceDir(true), name));
            }
            w.Close();
            return result;
        });
        if (Updating) { output.WriteLine("reference written: " + name); return; }
        Assert.True(File.Exists(reference), $"No reference picture {name}. Run the tests once with LUMEN_UPDATE_REFERENCES=1.");

        var (rw, rh, expected) = UiHost.Run(() => { using var b = new Bitmap(reference); return Pixels(b); });
        Assert.True(rw == width && rh == height, $"{name}: size {width}×{height} instead of {rw}×{rh}");
        int different = 0;
        var diff = new byte[pixels.Length];
        for (int i = 0; i < pixels.Length; i += 4)
        {
            int d = 0;
            for (int c = 0; c < 3; c++) d = Math.Max(d, Math.Abs(pixels[i + c] - expected[i + c]));
            if (d > ChannelTolerance) { different++; diff[i + 2] = 255; diff[i + 3] = 255; }
            else { byte g = (byte)(expected[i] / 4); diff[i] = diff[i + 1] = diff[i + 2] = g; diff[i + 3] = 255; }
        }
        double share = different / (double)(width * height);
        output.WriteLine($"{name}: {different} pixels differ ({share:P3})");
        if (share > MaxDifferentPixels)
        {
            string outDir = Path.Combine(AppContext.BaseDirectory, "ui-diff");
            Directory.CreateDirectory(outDir);
            UiHost.Run(() =>
            {
                using var wb = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
                using (var fb = wb.Lock()) Marshal.Copy(diff, 0, fb.Address, diff.Length);
                wb.Save(Path.Combine(outDir, "diff-" + name));
                return 0;
            });
            Assert.Fail($"{name}: {share:P2} of the picture changed (allowed {MaxDifferentPixels:P1}). The difference is marked red in {Path.Combine(outDir, "diff-" + name)}.");
        }
    }

    // BGRA bytes, rows packed.
    private static (int, int, byte[]) Pixels(Bitmap b)
    {
        int w = b.PixelSize.Width, h = b.PixelSize.Height;
        var bytes = new byte[w * h * 4];
        var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try { b.CopyPixels(new PixelRect(0, 0, w, h), handle.AddrOfPinnedObject(), bytes.Length, w * 4); }
        finally { handle.Free(); }
        return (w, h, bytes);
    }
}
