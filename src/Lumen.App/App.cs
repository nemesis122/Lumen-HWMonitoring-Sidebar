using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Lumen.Core;

namespace Lumen.App;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // One sidebar per user session.
        using var single = new Mutex(true, @"Local\Lumen.Sidebar", out bool first);
        if (!first && !args.Contains("--elevated")) return 0;
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<LumenApp>().UsePlatformDetect().WithInterFont();
}

public sealed class LumenApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        Name = "Lumen";
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            new Controller(desktop, desktop.Args?.Contains("--demo") == true).Start();
        base.OnFrameworkInitializationCompleted();
    }
}

// Owns the settings, the sensor source, the history and the one sidebar window.
public sealed class Controller
{
    private readonly IClassicDesktopStyleApplicationLifetime desktop;
    private readonly Platform platform = new();
    private readonly Settings settings;
    private readonly History history = new();
    private readonly ISnapshotSource source;
    private readonly bool demo;
    private readonly DispatcherTimer timer = new();
    private readonly DispatcherTimer weatherTimer = new() { Interval = TimeSpan.FromMinutes(20) };
    private readonly WarningTracker warnings = new();
    private readonly UsageMeter usage;
    private readonly IMediaSource? media;
    private PingMonitor? ping;
    private FpsMonitor? fps;
    private SidebarWindow? window;
    private SettingsWindow? settingsWindow;
    private TrayIcon? tray;
    private Snapshot? last;
    private WeatherInfo? weather;
    private bool reading;
    private string trayLanguage = "";

    public Controller(IClassicDesktopStyleApplicationLifetime desktop, bool demo)
    {
        this.desktop = desktop;
        this.demo = demo || !OperatingSystem.IsWindows();
        settings = Settings.Load(platform.SettingsPath);
        L.Use(settings.Language);
        source = this.demo ? new DemoSource() : new WindowsSource(platform.IsAdministrator);
        usage = new UsageMeter(this.demo ? null : Path.Combine(Path.GetDirectoryName(platform.SettingsPath) ?? ".", "lumen-usage.json"));
#if WINRT
        if (!this.demo) media = new WindowsMedia();
#endif
        NotesCard.Changed += _ => Save();
        MediaCard.Command += c => media?.Send(c);
    }

    public void Start()
    {
        CreateTray();
        Rebuild();
        if (Application.Current is { } app)
        {
            app.ActualThemeVariantChanged += (_, _) => { if (settings.Theme == ThemeMode.System) Rebuild(); };
            if (app.PlatformSettings is { } ps) ps.ColorValuesChanged += (_, _) => { if (settings.Colors == Settings.WindowsAccent) Rebuild(); };
        }
        timer.Tick += (_, _) => Tick();
        timer.Interval = TimeSpan.FromMilliseconds(settings.IntervalMs);
        timer.Start();
        weatherTimer.Tick += (_, _) => RefreshWeather();
        weatherTimer.Start();
        RefreshWeather();
        Tick();
    }

    private Palette CurrentPalette()
    {
        bool dark = settings.Theme switch
        {
            ThemeMode.Dark => true,
            ThemeMode.Light => false,
            _ => Application.Current?.ActualThemeVariant != ThemeVariant.Light
        };
        Color? accent = settings.Colors switch
        {
            Settings.StateColors => null,
            Settings.WindowsAccent => Application.Current?.PlatformSettings?.GetColorValues().AccentColor1,
            var hex => Color.Parse(hex)
        };
        return Palette.For(dark, settings.Glass, settings.Opacity, accent, settings.HighContrast, settings.Background);
    }

    private void Rebuild()
    {
        var palette = CurrentPalette();
        Motion.Enabled = settings.SmoothTransitions;
        // Charts scroll for most of each update interval, so they move continuously.
        Motion.ScrollDuration = Math.Min(900, settings.IntervalMs * .9);
        Motion.Duration = Math.Min(450, settings.IntervalMs * .6);
        if (Application.Current is { } app) app.RequestedThemeVariant = settings.Theme switch { ThemeMode.Dark => ThemeVariant.Dark, ThemeMode.Light => ThemeVariant.Light, _ => ThemeVariant.Default };
        window ??= new SidebarWindow();
        var view = new SidebarView(settings, palette);
        view.SettingsRequested += OpenSettings;
        view.ElevateRequested += platform.RestartElevated;
        window.Apply(settings, view);
        if (!window.IsVisible) window.Show();
        if (last != null) view.Update(last, history);
        timer.Interval = TimeSpan.FromMilliseconds(settings.IntervalMs);
        if (trayLanguage != L.Language) CreateTray();
    }

    // Sensor reads can take tens of milliseconds, so they run off the UI thread.
    private async void Tick()
    {
        if (reading) return;
        reading = true;
        try
        {
            var snapshot = await Task.Run(source.Read);
            // Demo data brings its own weather until a real place is set.
            if (!demo || weather != null) snapshot = snapshot with { Weather = weather };
            if (!demo)
            {
                bool wantsPing = settings.Widgets.Any(w => w.Visible && w.Kind == WidgetKind.Ping);
                if (wantsPing && ping == null) ping = new PingMonitor(settings.PingTarget);
                if (!wantsPing && ping != null) { ping.Dispose(); ping = null; }
                if (ping != null && ping.Target != settings.PingTarget) ping.Target = settings.PingTarget;
                // PresentMon only runs while the FPS widget is shown.
                bool wantsFps = settings.Widgets.Any(w => w.Visible && w.Kind == WidgetKind.Fps) && platform.IsAdministrator;
                if (wantsFps && fps == null) { fps = new FpsMonitor(); fps.Start(); }
                if (!wantsFps && fps != null) { fps.Dispose(); fps = null; }
                snapshot = snapshot with { Ping = ping?.Read(), Usage = usage.Record(snapshot, settings.BaseWatts), Media = media?.Read(), Fps = fps?.Read() };
            }
            history.Record(snapshot);
            last = snapshot;
            window?.View?.Update(snapshot, history);
            if (settings.WarningPopup && window is { IsVisible: true })
                foreach (var w in warnings.Check(snapshot, settings.TemperatureWarning))
                    new Toast(CurrentPalette(), L.T("Temperature warning"),
                        L.F("{0} is at {1} – above your limit of {2}.", w.Name,
                            Format.Temperature(w.Celsius, settings.Fahrenheit) + " " + Format.TemperatureUnit(settings.Fahrenheit),
                            Format.Temperature(settings.TemperatureWarning, settings.Fahrenheit) + " " + Format.TemperatureUnit(settings.Fahrenheit)))
                        .ShowBeside(window, settings.Edge == Edge.Right);
        }
        catch { /* a failed read leaves the last values on screen */ }
        finally { reading = false; }
    }

    private async void RefreshWeather()
    {
        if (settings.WeatherLatitude is not double lat || settings.WeatherLongitude is not double lon) { weather = null; return; }
        try { weather = await WeatherClient.ReadAsync(settings.WeatherPlace, lat, lon) ?? weather; }
        catch { /* offline: keep the last forecast */ }
    }

    private void Changed()
    {
        Settings.Normalize(settings);
        L.Use(settings.Language);
        Save();
        Rebuild();
    }

    private void Save()
    {
        try { settings.Save(platform.SettingsPath); } catch { }
    }

    private void OpenSettings()
    {
        if (settingsWindow is { IsVisible: true }) { settingsWindow.Activate(); return; }
        settingsWindow = new SettingsWindow(settings, CurrentPalette(), Changed, ScreenNames, platform, RefreshWeather, SensorReport, () => last);
        settingsWindow.Closed += (_, _) => settingsWindow = null;
        settingsWindow.Show();
    }

    // The raw sensor lists plus what the sidebar shows from them.
    private string SensorReport()
    {
        string raw = source is ISensorReport r ? r.Report() : "Demo data – no sensors are read.\n";
        return raw + "\n=== " + L.T("Shown in the sidebar") + " ===\n" + (last is null ? "—" : SnapshotText.Describe(last));
    }

    private IReadOnlyList<string> ScreenNames()
    {
        var screens = window?.Screens.All ?? Array.Empty<Avalonia.Platform.Screen>();
        return screens.Select((s, i) => $"{L.F("Screen {0}", i + 1)} · {s.Bounds.Width}×{s.Bounds.Height}{(s.IsPrimary ? $" ({L.T("main")})" : "")}").ToList();
    }

    private void CreateTray()
    {
        trayLanguage = L.Language;
        var menu = new NativeMenu();
        var show = new NativeMenuItem(L.T("Show or hide"));
        show.Click += (_, _) => { if (window == null) return; if (window.IsVisible) window.Hide(); else { window.Show(); Rebuild(); } };
        var open = new NativeMenuItem(L.T("Settings"));
        open.Click += (_, _) => OpenSettings();
        var quit = new NativeMenuItem(L.T("Quit"));
        quit.Click += (_, _) => Quit();
        menu.Items.Add(show);
        menu.Items.Add(open);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(quit);
        tray?.Dispose();
        tray = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://Lumen/Assets/lumen.ico"))),
            ToolTipText = "Lumen",
            Menu = menu
        };
        tray.Clicked += (_, _) => OpenSettings();
        TrayIcon.SetIcons(Application.Current!, new TrayIcons { tray });
    }

    private void Quit()
    {
        timer.Stop();
        weatherTimer.Stop();
        ping?.Dispose();
        fps?.Dispose();
        if (last != null) usage.Save(last.Time);
        window?.Release();
        tray?.Dispose();
        source.Dispose();
        desktop.Shutdown();
    }
}
