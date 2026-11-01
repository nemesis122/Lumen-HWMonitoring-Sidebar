using Avalonia.Platform.Storage;
using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;
using static Lumen.App.L;

namespace Lumen.App;

// Every change applies to the sidebar immediately; there is no "Apply" step to forget.
// Six topics, the most used first; a search finds any setting; a dot marks what differs from the default.
public sealed class SettingsWindow : Window
{
    private readonly Settings s;
    private readonly Action changed;
    private readonly Action weatherChanged;
    private readonly Func<IReadOnlyList<string>> screens;
    private readonly Platform platform;
    private readonly Palette p;
    private readonly StackPanel content = new() { Spacing = 22, Margin = new Thickness(28, 20, 32, 28) };
    private readonly StackPanel nav = new() { Spacing = 2, Margin = new Thickness(14, 4, 10, 14) };
    private readonly ScrollViewer scroller = new();
    // The page shown, kept across rebuilds and reopening.
    private static int pageIndex;
    private readonly TextBlock title, lead;
    private readonly Button reset, resetPage, done;
    private readonly TextBox search;
    private readonly HashSet<WidgetKind> expanded = new();
    private string weatherStatus = "";
    private bool confirmReset;

    // Filled while a page is built: which settings it holds (for its dot and "Reset this page"),
    // and every row's label and help (for the search).
    private int building;
    private readonly Dictionary<int, List<string>> pageProps = new();
    private readonly List<(int Page, string Label, string Help)> searchIndex = new();
    private static readonly Settings Defaults = new();

    // The live preview on the Appearance page, redrawn while a slider moves.
    private readonly Border previewHost = new();

    private readonly Func<string>? report;
    private readonly Func<Snapshot?> snapshot;

    private static readonly WidgetKind[] InfoKinds = { WidgetKind.Clock, WidgetKind.WorldClocks, WidgetKind.Calendar, WidgetKind.Weather, WidgetKind.Media, WidgetKind.Notes };

    public SettingsWindow(Settings settings, Palette palette, Action onChanged, Func<IReadOnlyList<string>> screenNames, Platform platform, Action? onWeather = null, Func<string>? report = null, Func<Snapshot?>? snapshot = null)
    {
        this.report = report;
        this.snapshot = snapshot ?? (() => null);
        s = settings; p = palette; changed = onChanged; screens = screenNames; this.platform = platform; weatherChanged = onWeather ?? (() => { });
        Width = 940; Height = 780; MinWidth = 760; MinHeight = 520;
        FontFamily = Ui.Inter;
        Background = Palette.Brush(p.Dark ? Color.Parse("#14181F") : Color.Parse("#F3F5F8"));
        RequestedThemeVariant = p.Dark ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Icon = new WindowIcon(Avalonia.Platform.AssetLoader.Open(new Uri("avares://Lumen/Assets/lumen.ico")));

        // "Reset all" asks once more: the first click turns it into the question.
        reset = new Button { Padding = new Thickness(14, 7), CornerRadius = new CornerRadius(8), Background = Brushes.Transparent, BorderThickness = new Thickness(1), BorderBrush = Palette.Brush(Color.Parse("#B3FF5D5D")), Foreground = Palette.Brush(p.Dark ? Color.Parse("#FF8A8A") : Color.Parse("#C62828")) };
        reset.Click += (_, _) =>
        {
            if (!confirmReset)
            {
                confirmReset = true;
                reset.Content = T("Sure? Click again");
                Avalonia.Threading.DispatcherTimer.RunOnce(() => { confirmReset = false; reset.Content = T("Reset all …"); }, TimeSpan.FromSeconds(4));
                return;
            }
            confirmReset = false;
            Reset();
        };
        resetPage = new Button { Padding = new Thickness(14, 7), CornerRadius = new CornerRadius(8), Margin = new Thickness(10, 0, 0, 0) };
        resetPage.Click += (_, _) => ResetPage(pageIndex);
        done = new Button { Padding = new Thickness(22, 7), CornerRadius = new CornerRadius(8), Classes = { "accent" } };
        done.Click += (_, _) => Close();
        var footer = new Border
        {
            BorderBrush = Palette.Brush(p.Stroke),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(28, 14),
            Child = Ui.Columns("Auto,Auto,*,Auto", reset, resetPage, new Control(), done)
        };
        title = Ui.Text("", 22, p.Ink, FontWeight.SemiBold);
        lead = Ui.Text("", 12.5, p.Soft);
        // The search sits above the topics; typing lists every matching setting.
        search = new TextBox { CornerRadius = new CornerRadius(8), MinWidth = 0, Margin = new Thickness(0, 14, 0, 0), InnerLeftContent = Ui.Glyph("M7,2.5 A4.5,4.5 0 1 1 6.99,2.5 Z M10.3,10.3 L14,14", p.Soft, 13) };
        if (search.InnerLeftContent is Control glass) glass.Margin = new Thickness(10, 0, 2, 0);
        search.TextChanged += (_, _) => Build();
        var head = Ui.Stack(2, title, lead, search);
        head.Margin = new Thickness(26, 24, 16, 12);
        // Left: the topics; right: the settings of the chosen topic.
        var side = new DockPanel { Width = 220 };
        DockPanel.SetDock(head, Dock.Top);
        side.Children.Add(head);
        side.Children.Add(new ScrollViewer { Content = nav });
        var sideBorder = new Border
        {
            Background = Palette.Brush(p.Dark ? Color.Parse("#10141A") : Color.Parse("#E9EDF2")),
            BorderBrush = Palette.Brush(p.Stroke),
            BorderThickness = new Thickness(0, 0, 1, 0),
            Child = side
        };
        scroller.Content = content;
        var dock = new DockPanel();
        DockPanel.SetDock(sideBorder, Dock.Left);
        DockPanel.SetDock(footer, Dock.Bottom);
        dock.Children.Add(sideBorder);
        dock.Children.Add(footer);
        dock.Children.Add(scroller);
        Content = dock;
        Build();
    }

    private void Reset()
    {
        var fresh = new Settings();
        foreach (var prop in typeof(Settings).GetProperties().Where(x => x.CanWrite)) prop.SetValue(s, prop.GetValue(fresh));
        changed();
        weatherChanged();
        Build();
    }

    // --- What differs from the default ------------------------------------------------------------

    private static PropertyInfo? Prop(string name) => typeof(Settings).GetProperty(name);

    // Compared as JSON, so lists and widget slots count as changed only when their content differs.
    private bool IsChanged(string name) =>
        Prop(name) is { } prop && JsonSerializer.Serialize(prop.GetValue(s), Settings.Json) != JsonSerializer.Serialize(prop.GetValue(Defaults), Settings.Json);

    private void Restore(IEnumerable<string> names)
    {
        var fresh = new Settings();
        foreach (var name in names) if (Prop(name) is { CanWrite: true } prop) prop.SetValue(s, prop.GetValue(fresh));
        changed();
        weatherChanged();
        Build();
    }

    private void ResetPage(int page) => Restore(pageProps.TryGetValue(page, out var list) ? list : new List<string>());

    // Registers a setting as part of the page being built.
    private void Owns(params string[] names)
    {
        if (!pageProps.TryGetValue(building, out var list)) pageProps[building] = list = new();
        foreach (var n in names) if (!list.Contains(n)) list.Add(n);
    }

    // The small dot beside a changed setting; a click puts that one setting back.
    private Control? ChangedDot(string? prop)
    {
        if (prop is null) return null;
        Owns(prop);
        if (!IsChanged(prop)) return null;
        var b = new Button
        {
            Width = 18, Height = 18, Padding = new Thickness(0), Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
            Content = new Ellipse { Width = 8, Height = 8, Fill = Palette.Brush(p.Cool) },
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        ToolTip.SetTip(b, T("Changed – click to reset"));
        b.Click += (_, _) => Restore(new[] { prop });
        return b;
    }

    // --- The pages -------------------------------------------------------------------------------

    // The settings themselves always read in the built-in typeface; only the preview shows the chosen one.
    private void Build()
    {
        var keep = Ui.FontKey;
        Ui.UseFont("inter");
        try { BuildPages(); }
        finally { Ui.UseFont(keep); }
    }

    private void BuildPages()
    {
        Title = "Lumen – " + T("Settings");
        title.Text = T("Settings");
        lead.Text = T("Changes apply immediately.");
        search.Watermark = T("Search settings…");
        if (!confirmReset) reset.Content = T("Reset all …");
        resetPage.Content = T("Reset this page");
        done.Content = T("Done");
        content.Children.Clear();
        pageProps.Clear();
        searchIndex.Clear();
        var pages = new List<(string Name, string Glyph, Control[] Body)>();

        // 0 · Widgets – what the sidebar shows, most used first.
        building = 0;
        Owns(nameof(Settings.Widgets), nameof(Settings.HiddenValues));
        pages.Add((T("Widgets"), Glyphs.Apps, WidgetsPage()));

        // 1 · Appearance
        building = 1;
        pages.Add((T("Appearance"), Glyphs.Gear, AppearancePage()));

        // 2 · Placement – where the sidebar sits and how it is laid out.
        building = 2;
        pages.Add((T("Placement"), Glyphs.Columns, PlacementPage()));

        // 3 · Sensors – where the readings come from and how they are shown.
        building = 3;
        pages.Add((T("Sensors"), Glyphs.Cpu, new[] { Section(T("Sensors"),
            Row(T("Sensor source"), Choice(new[] { T("Built-in"), T("Automatic"), "AIDA64", "HWiNFO" }, (int)s.SensorSource, i => s.SensorSource = (SensorSource)i, rebuild: true), SourceHelp(s.SensorSource), nameof(Settings.SensorSource)),
            Row(T("Update interval"), Slide(.5, 5, .5, s.IntervalMs / 1000.0, v => s.IntervalMs = (int)Math.Round(v * 1000), v => v.ToString("0.0", L.Culture) + " s"), null, nameof(Settings.IntervalMs)),
            Row(T("Temperature"), Choice(new[] { T("Celsius (°C)"), T("Fahrenheit (°F)") }, s.Fahrenheit ? 1 : 0, i => s.Fahrenheit = i == 1, rebuild: true), null, nameof(Settings.Fahrenheit)),
            Row(T("Warn from"), Slide(50, 110, 1, s.TemperatureWarning, v => s.TemperatureWarning = v, v => $"{v:0} °C"), T("Above this the temperature turns red."), nameof(Settings.TemperatureWarning)),
            Row(T("Warning pop-up"), Toggle(s.WarningPopup, v => s.WarningPopup = v, rebuild: true), T("Shows a notice when a temperature crosses the limit."), nameof(Settings.WarningPopup)),
            Row(T("Integrated graphics"), Toggle(s.ShowIntegratedGpu, v => s.ShowIntegratedGpu = v, rebuild: true), T("Also show the graphics built into the processor."), nameof(Settings.ShowIntegratedGpu))) }));

        // 4 · Extras – weather, world clocks, power cost and ping.
        building = 4;
        Owns(nameof(Settings.WeatherPlace), nameof(Settings.WeatherLatitude), nameof(Settings.WeatherLongitude), nameof(Settings.WorldClocks));
        var price = new NumericUpDown { Value = (decimal)s.PricePerKwh, Minimum = 0, Maximum = 5, Increment = 0.01m, FormatString = "0.00", Width = 130, CornerRadius = new CornerRadius(8) };
        price.ValueChanged += (_, _) => { s.PricePerKwh = (double)(price.Value ?? 0); changed(); };
        var currency = new TextBox { Text = s.Currency, Width = 70, CornerRadius = new CornerRadius(8), MaxLength = 5 };
        currency.LostFocus += (_, _) => { s.Currency = currency.Text ?? "CHF"; changed(); };
        var target = new TextBox { Text = s.PingTarget, Width = 170, CornerRadius = new CornerRadius(8) };
        target.LostFocus += (_, _) => { s.PingTarget = target.Text ?? "1.1.1.1"; changed(); };
        Index(T("Place"), T("Weather"));
        Index(T("World clocks"), T("Up to four cities."));
        pages.Add((T("Extras"), Glyphs.Cloud, new[] {
            Section(T("Weather"), WeatherEditor()),
            Section(T("World clocks"), WorldClockPicker()),
            Section(T("Power and cost"),
                Row(T("Price per kWh"), Ui.Row(8, price, currency), null, nameof(Settings.PricePerKwh)),
                Row(T("Rest of the PC"), Slide(0, 300, 5, s.BaseWatts, v => s.BaseWatts = v, v => $"{v:0} W"), T("Screen, board, drives and fans are not measured; this estimate is added."), nameof(Settings.BaseWatts))),
            Section("Ping", Row(T("Target"), target, T("A server on the internet, e.g. 1.1.1.1 or google.com."), nameof(Settings.PingTarget))) }));
        Owns(nameof(Settings.Currency));

        // 5 · System – language, autostart, rights and the sensor list.
        building = 5;
        var languages = new List<string> { T("Automatic (Windows)") };
        languages.AddRange(L.Choices.Select(c => c.Name));
        int langIndex = s.Language == "auto" ? 0 : Array.FindIndex(L.Choices, c => c.Code == s.Language) + 1;
        var admin = platform.IsAdministrator;
        var accessText = Ui.Text(admin ? T("Running with administrator rights – all sensors are reachable.") : T("Without administrator rights the CPU temperatures are missing."), 12, admin ? p.Calm : p.Soft);
        accessText.TextWrapping = TextWrapping.Wrap;
        accessText.TextTrimming = TextTrimming.None;
        var elevate = new Button { Content = T("Restart as administrator"), Padding = new Thickness(12, 6), CornerRadius = new CornerRadius(8), IsVisible = !admin, Margin = new Thickness(0, 8, 0, 0) };
        elevate.Click += (_, _) => platform.RestartElevated();
        var autostart = Toggle(platform.AutostartEnabled, v => { if (!platform.SetAutostart(v)) Build(); }, notify: false);
        var sensors = new Button { Content = T("Show all sensors"), Padding = new Thickness(12, 6), CornerRadius = new CornerRadius(8) };
        sensors.Click += (_, _) => new SensorsWindow(p, report ?? (() => ""), System.IO.Path.GetDirectoryName(platform.SettingsPath) ?? ".").Show();
        var makeReport = new Button { Content = T("Create error report"), Padding = new Thickness(12, 6), CornerRadius = new CornerRadius(8) };
        makeReport.Click += (_, _) =>
        {
            try
            {
                string zip = ErrorReport.Create(s, report?.Invoke() ?? "", screens());
                reportMessage = F("Saved on the desktop: {0}", System.IO.Path.GetFileName(zip));
                ErrorReport.Reveal(zip);
            }
            catch (Exception e) { reportMessage = T("The file could not be read or written.") + " " + e.Message; }
            Build();
        };
        pages.Add((T("System"), Glyphs.Shield, new[] { Section(T("System"),
            Row(T("Language"), Choice(languages, langIndex, i => s.Language = i == 0 ? "auto" : L.Choices[i - 1].Code, rebuild: true), null, nameof(Settings.Language)),
            Row(T("Start with Windows"), autostart, T("Starts with administrator rights, without a prompt.")),
            Ui.Stack(0, accessText, elevate),
            Row(T("Sensors"), sensors, T("Lists everything LibreHardwareMonitor and Open Hardware Monitor found.")),
            Row(T("Error report"), makeReport, reportMessage.Length > 0 ? reportMessage : T("One ZIP file with error log, sensor lists and settings – without notes, places or own names. Nothing is sent."))),
            Ui.Text($"Lumen {typeof(SettingsWindow).Assembly.GetName().Version?.ToString(3)} · {F("Settings stored in {0}", System.IO.Path.GetFileName(platform.SettingsPath))}", 11, p.Faint) }));

        // Navigation on the left, the chosen page (or the search results) on the right.
        pageIndex = Math.Clamp(pageIndex, 0, pages.Count - 1);
        string query = (search.Text ?? "").Trim();
        nav.Children.Clear();
        for (int i = 0; i < pages.Count; i++)
        {
            int n = i;
            bool on = n == pageIndex && query.Length == 0;
            var label = Ui.Text(pages[n].Name, 13.5, on ? p.Ink : p.Soft, on ? FontWeight.SemiBold : FontWeight.Medium);
            var row = new DockPanel();
            var glyph = Ui.Glyph(pages[n].Glyph, on ? p.Cool : p.Soft, 15);
            glyph.Margin = new Thickness(0, 0, 11, 0);
            DockPanel.SetDock(glyph, Dock.Left);
            row.Children.Add(glyph);
            if (NavBadge(n) is { } badge) { DockPanel.SetDock(badge, Dock.Right); row.Children.Add(badge); }
            row.Children.Add(label);
            var item = new Button
            {
                Content = row,
                Background = on ? Palette.Brush(p.Dark ? Color.Parse("#222A35") : Colors.White) : Brushes.Transparent,
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(12, 9),
                CornerRadius = new CornerRadius(8),
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            item.Click += (_, _) => { pageIndex = n; scroller.Offset = default; if ((search.Text ?? "").Length > 0) search.Text = ""; else Build(); };
            nav.Children.Add(item);
        }
        resetPage.IsEnabled = query.Length == 0 && pageProps.TryGetValue(pageIndex, out var mine) && mine.Any(IsChanged);
        if (query.Length > 0) { content.Children.Add(SearchResults(query, pages.Select(x => x.Name).ToList())); return; }
        foreach (var c in pages[pageIndex].Body) content.Children.Add(c);
    }

    // Sensors shows whether AIDA64 or HWiNFO answers; every other topic a dot when something on it was changed.
    private Control? NavBadge(int page)
    {
        if (page == 3 && s.SensorSource != SensorSource.Builtin)
        {
            string status = ExternalSensors.Status;
            bool ok = status.StartsWith("connected:");
            string name = !ok ? "—" : status.EndsWith(nameof(SensorSource.Aida64)) ? "AIDA64" : "HWiNFO";
            var color = ok ? p.Calm : p.Ramp[2];
            return Ui.Row(5, new Ellipse { Width = 7, Height = 7, Fill = Palette.Brush(color), VerticalAlignment = VerticalAlignment.Center }, Ui.Text(name, 11, color, FontWeight.Medium));
        }
        if (pageProps.TryGetValue(page, out var list) && list.Any(IsChanged))
        {
            var dot = new Ellipse { Width = 7, Height = 7, Fill = Palette.Brush(p.Cool), VerticalAlignment = VerticalAlignment.Center };
            ToolTip.SetTip(dot, T("Changed from the default"));
            return dot;
        }
        return null;
    }

    private void Index(string label, string? help) => searchIndex.Add((building, label, help ?? ""));

    private Control SearchResults(string query, IReadOnlyList<string> pageNames)
    {
        var hits = searchIndex.Where(x => x.Label.Contains(query, StringComparison.CurrentCultureIgnoreCase) || x.Help.Contains(query, StringComparison.CurrentCultureIgnoreCase))
            .GroupBy(x => (x.Page, x.Label)).Select(g => g.First()).ToList();
        var list = new StackPanel { Spacing = 6 };
        foreach (var hit in hits)
        {
            var b = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(14, 10), CornerRadius = new CornerRadius(10),
                Background = Palette.Brush(p.Dark ? Color.Parse("#191E26") : Colors.White), BorderBrush = Palette.Brush(p.Stroke), BorderThickness = new Thickness(1),
                Content = Ui.Columns("*,Auto", Ui.Stack(2, Ui.Text(hit.Label, 13.5, p.Ink, FontWeight.Medium), Ui.Text(hit.Help, 11.5, p.Soft)), Ui.Text(pageNames[hit.Page] + "  ›", 12, p.Cool, FontWeight.Medium)),
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            int page = hit.Page;
            b.Click += (_, _) => { pageIndex = page; scroller.Offset = default; search.Text = ""; };
            list.Children.Add(b);
        }
        if (hits.Count == 0) list.Children.Add(Ui.Text(T("No setting found."), 13, p.Soft));
        return Ui.Stack(10, SectionTitle(F("Results for “{0}”", query)), list);
    }

    // --- Widgets ---------------------------------------------------------------------------------

    private Control[] WidgetsPage()
    {
        var help = Ui.Text(T("Tick what the sidebar shows; drag by the handle or use the arrows to sort. “Customise” opens style, column, register and single values."), 12, p.Soft);
        help.TextWrapping = TextWrapping.Wrap;
        help.TextTrimming = TextTrimming.None;
        Index(T("Style for all widgets"), T("One click sets every widget with styles; each one can still be changed below."));

        // One click sets the style of every widget that has styles; each can still be changed below.
        var allStyles = new Avalonia.Controls.Primitives.UniformGrid { Columns = 6 };
        // Two rows of style pictures: the six cards and the five compact styles.
        foreach (var style in Enum.GetValues<WidgetStyle>())
        {
            var styled = s.Widgets.Where(w => Settings.HasStyles(w.Kind)).ToList();
            bool on = styled.Count > 0 && styled.All(w => w.Style == style);
            var b = new Button
            {
                Content = Ui.Stack(6, StyleThumb(style), Ui.Text(StyleName(style), 12, on ? p.Ink : p.Soft, on ? FontWeight.SemiBold : FontWeight.Medium)),
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center,
                Padding = new Thickness(6, 10, 6, 8), Margin = new Thickness(0, 0, 8, 8), CornerRadius = new CornerRadius(10),
                Background = Palette.Brush(p.Dark ? Color.Parse("#191E26") : Colors.White),
                BorderBrush = Palette.Brush(on ? p.Cool : p.Stroke), BorderThickness = new Thickness(on ? 2 : 1),
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            if (b.Content is StackPanel sp) foreach (var c in sp.Children) c.HorizontalAlignment = HorizontalAlignment.Center;
            b.Click += (_, _) =>
            {
                foreach (var w in s.Widgets.Where(w => Settings.HasStyles(w.Kind))) w.Style = style;
                changed();
                Build();
            };
            allStyles.Children.Add(b);
        }

        var hardware = s.Widgets.Where(w => w.Visible && !InfoKinds.Contains(w.Kind)).ToList();
        var info = s.Widgets.Where(w => w.Visible && InfoKinds.Contains(w.Kind)).ToList();
        var hidden = s.Widgets.Where(w => !w.Visible).ToList();
        foreach (var w in s.Widgets) Index(Cards.Name(w.Kind), T("Widgets"));

        var result = new List<Control> { help, Ui.Stack(10, SectionTitle(T("Style for all widgets")), allStyles) };
        if (hardware.Count > 0) result.Add(Group(T("Hardware"), F("{0} shown", hardware.Count), hardware));
        if (info.Count > 0) result.Add(Group(T("Info"), F("{0} shown", info.Count), info));
        if (hidden.Count > 0)
        {
            var chips = new WrapPanel();
            foreach (var slot in hidden)
            {
                var c = new CheckBox { Content = Cards.Name(slot.Kind), IsChecked = false, FontSize = 13 };
                c.IsCheckedChanged += (_, _) => { slot.Visible = c.IsChecked == true; changed(); Avalonia.Threading.Dispatcher.UIThread.Post(Build); };
                chips.Children.Add(new Border
                {
                    Child = c, Padding = new Thickness(10, 0, 12, 0), Margin = new Thickness(0, 0, 8, 8), CornerRadius = new CornerRadius(9),
                    Background = Palette.Brush(p.Dark ? Color.Parse("#151A21") : Color.Parse("#F7F9FB")),
                    BorderBrush = Palette.Brush(p.Dark ? Color.Parse("#2E3644") : Color.Parse("#CDD5DF")), BorderThickness = new Thickness(1)
                });
            }
            result.Add(Ui.Stack(10, Ui.Columns("*,Auto", SectionTitle(T("Hidden")), Ui.Text(T("Tick to show"), 12, p.Soft)), chips));
        }
        return result.ToArray();
    }

    // One group of shown widgets; rows are sorted by dragging the handle or with the arrows.
    private Control Group(string name, string count, List<WidgetSlot> slots)
    {
        var list = new StackPanel { Spacing = 6 };
        for (int i = 0; i < slots.Count; i++) list.Children.Add(WidgetRow(slots, i, list));
        return Ui.Stack(10, Ui.Columns("*,Auto", SectionTitle(name), Ui.Text(count, 12, p.Soft)), list);
    }

    private TextBlock SectionTitle(string text)
    {
        var t = Ui.Text(text.ToUpper(L.Culture), 11, p.Soft, FontWeight.SemiBold);
        t.LetterSpacing = 1;
        t.VerticalAlignment = VerticalAlignment.Bottom;
        return t;
    }

    private static string StyleName(WidgetStyle style) => T(style.ToString());

    // A small picture of each style, so it can be chosen by its look.
    private Control StyleThumb(WidgetStyle style)
    {
        var c = new Canvas { Width = 64, Height = 34 };
        IBrush cool = Palette.Brush(p.Cool), calm = Palette.Brush(p.Calm), track = Palette.Brush(p.Track), tile = Palette.Brush(p.Dark ? Color.Parse("#262E3B") : Color.Parse("#E4E9F0")), soft = Palette.Brush(p.Soft);
        void Rect(double x, double y, double w, double h, IBrush fill, double r = 2)
        {
            var e = new Rectangle { Width = w, Height = h, Fill = fill, RadiusX = r, RadiusY = r };
            Canvas.SetLeft(e, x); Canvas.SetTop(e, y); c.Children.Add(e);
        }
        Control Line(string data, IBrush stroke, IBrush? fill = null) => new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse(data), Stroke = stroke, StrokeThickness = 1.6, Fill = fill, StrokeLineCap = PenLineCap.Round };
        switch (style)
        {
            case WidgetStyle.Standard:
                Rect(2, 3, 22, 9, calm); Rect(2, 17, 60, 3, track, 1.5); Rect(2, 17, 20, 3, cool, 1.5);
                c.Children.Add(Line("M2,31 L12,28 L22,30 L32,25 L42,29 L52,26 L62,28", cool));
                break;
            case WidgetStyle.Ring:
                var ring = new Ellipse { Width = 26, Height = 26, Stroke = track, StrokeThickness = 4 };
                Canvas.SetLeft(ring, 19); Canvas.SetTop(ring, 4); c.Children.Add(ring);
                c.Children.Add(Line("M32,4 A13,13 0 0 1 44.4,21", calm));
                if (c.Children[^1] is Shape arc) arc.StrokeThickness = 4;
                break;
            case WidgetStyle.Minimal:
                Rect(4, 9, 26, 14, calm); Rect(38, 13, 22, 6, soft);
                break;
            case WidgetStyle.Bar:
                Rect(2, 7, 60, 6, track, 3); Rect(2, 7, 34, 6, calm, 3); Rect(2, 21, 60, 6, track, 3); Rect(2, 21, 14, 6, cool, 3);
                break;
            case WidgetStyle.Graph:
                c.Children.Add(Line("M2,32 L2,24 L12,20 L22,26 L32,12 L42,22 L52,16 L62,20 L62,32 Z", Brushes.Transparent, Palette.Brush(Color.FromArgb(64, p.Cool.R, p.Cool.G, p.Cool.B))));
                c.Children.Add(Line("M2,24 L12,20 L22,26 L32,12 L42,22 L52,16 L62,20", cool));
                break;
            case WidgetStyle.Tile:
                Rect(2, 2, 28, 30, tile, 4); Rect(34, 2, 28, 30, tile, 4); Rect(8, 12, 16, 10, calm); Rect(40, 12, 16, 10, cool);
                break;
            case WidgetStyle.Compact:
                Rect(2, 8, 14, 4, soft, 1); Rect(2, 15, 10, 3, track, 1); Rect(20, 7, 14, 12, calm); Rect(40, 8, 22, 3, track, 1.5); Rect(40, 8, 9, 3, cool, 1.5);
                c.Children.Add(Line("M40,20 L45,18 L50,19 L55,15 L62,17", cool));
                Rect(2, 25, 60, 1, track, 0);
                break;
            case WidgetStyle.Status:
                Rect(0, 12, 64, 12, tile, 6);
                var sdot = new Ellipse { Width = 5, Height = 5, Fill = calm }; Canvas.SetLeft(sdot, 5); Canvas.SetTop(sdot, 15.5); c.Children.Add(sdot);
                Rect(13, 16, 12, 4, soft, 1); Rect(28, 15, 10, 6, calm, 1); Rect(42, 17, 16, 2, cool, 1);
                break;
            case WidgetStyle.Gauge:
                c.Children.Add(Line("M8,30 A15,15 0 0 1 38,30", track));
                if (c.Children[^1] is Shape g1) g1.StrokeThickness = 5;
                c.Children.Add(Line("M8,30 A15,15 0 0 1 28,16", calm));
                if (c.Children[^1] is Shape g2) g2.StrokeThickness = 5;
                Rect(44, 10, 18, 4, soft, 1); Rect(44, 18, 14, 3, track, 1); Rect(44, 24, 16, 3, track, 1);
                break;
            case WidgetStyle.Segments:
                for (int i = 0; i < 10; i++) { Rect(2 + i * 6.2, 8, 5, 7, i < 6 ? (i < 5 ? calm : Palette.Brush(p.Ramp[2])) : track, 1); Rect(2 + i * 6.2, 20, 5, 7, i < 3 ? cool : track, 1); }
                break;
            case WidgetStyle.DualRing:
                var o = new Ellipse { Width = 28, Height = 28, Stroke = track, StrokeThickness = 3 }; Canvas.SetLeft(o, 4); Canvas.SetTop(o, 3); c.Children.Add(o);
                c.Children.Add(Line("M18,3 A14,14 0 0 1 31,22", cool));
                if (c.Children[^1] is Shape d1) d1.StrokeThickness = 3;
                var i2 = new Ellipse { Width = 18, Height = 18, Stroke = track, StrokeThickness = 3 }; Canvas.SetLeft(i2, 9); Canvas.SetTop(i2, 8); c.Children.Add(i2);
                c.Children.Add(Line("M18,8 A9,9 0 1 1 10,21", calm));
                if (c.Children[^1] is Shape d2) d2.StrokeThickness = 3;
                Rect(40, 10, 20, 9, calm); Rect(40, 22, 14, 3, soft, 1);
                break;
        }
        return c;
    }

    private WidgetSlot? dragging;
    private double dragStart;

    private Control WidgetRow(List<WidgetSlot> group, int pos, StackPanel container)
    {
        var slot = group[pos];
        // The own title, if any, with the built-in name after it so the widget stays recognisable.
        string listName = slot.Name.Length > 0 ? $"{slot.Name}  ({Cards.Name(slot.Kind)})" : Cards.Name(slot.Kind);
        var check = new CheckBox { Content = listName, IsChecked = slot.Visible, FontSize = 13.5 };
        check.IsCheckedChanged += (_, _) => { slot.Visible = check.IsChecked == true; changed(); Avalonia.Threading.Dispatcher.UIThread.Post(Build); };
        Button Small(Control body, string tip, bool enabled, Action click)
        {
            var b = new Button { Content = body, Padding = new Thickness(8, 6), CornerRadius = new CornerRadius(7), IsEnabled = enabled, Margin = new Thickness(6, 0, 0, 0) };
            ToolTip.SetTip(b, tip);
            b.Click += (_, _) => click();
            return b;
        }
        // Moves the widget to where another one of its group is; the order is the one of the whole sidebar.
        void MoveTo(WidgetSlot target)
        {
            if (target == slot) return;
            int to = s.Widgets.IndexOf(target);
            int from = s.Widgets.IndexOf(slot);
            s.Widgets.RemoveAt(from);
            s.Widgets.Insert(to, slot);
            changed();
            Build();
        }
        bool open = expanded.Contains(slot.Kind);
        var toggle = Small(Ui.Text(T("Customise") + (open ? "  ▴" : "  ▾"), 12, p.Ink, FontWeight.Medium), T("Customise"), true, () =>
        {
            if (!expanded.Remove(slot.Kind)) expanded.Add(slot.Kind);
            Build();
        });
        // Where the widget lands, so the list reads as the layout without opening every entry.
        var place = new List<string>();
        if (s.Pages > 1) place.Add(s.PageName(Math.Min(slot.Page, s.Pages)));
        if (s.ColumnsInUse > 1 && slot.Column > 0) place.Add(s.Horizontal ? F("Row {0}", Math.Min(slot.Column, s.ColumnsInUse)) : F("Column {0}", Math.Min(slot.Column, s.ColumnsInUse)));
        if (Settings.HasStyles(slot.Kind)) place.Add(StyleName(slot.Style));
        var where = new Border
        {
            Child = Ui.Text(string.Join(" · ", place), 11.5, p.Soft),
            Background = Palette.Brush(p.Dark ? Color.Parse("#262E3B") : Color.Parse("#EEF2F6")),
            CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 3), VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0), IsVisible = place.Count > 0
        };
        // The handle: press, drag up or down, let go over the place it should go.
        var handle = new Border
        {
            Child = Ui.Glyph("M6,3.5 h0.01 M10,3.5 h0.01 M6,8 h0.01 M10,8 h0.01 M6,12.5 h0.01 M10,12.5 h0.01", p.Soft, 14),
            Background = Brushes.Transparent, Padding = new Thickness(4, 6), Margin = new Thickness(0, 0, 8, 0),
            Cursor = new Cursor(StandardCursorType.SizeNorthSouth), VerticalAlignment = VerticalAlignment.Center
        };
        if (handle.Child is Shape dots) { dots.StrokeThickness = 3; dots.StrokeLineCap = PenLineCap.Round; }
        ToolTip.SetTip(handle, T("Drag to sort"));
        var row = Ui.Columns("Auto,*,Auto,Auto,Auto,Auto", handle, check, where, toggle,
            Small(Ui.Glyph("M3,10 L8,5 L13,10", p.Soft, 12), T("Move up"), pos > 0, () => MoveTo(group[pos - 1])),
            Small(Ui.Glyph("M3,6 L8,11 L13,6", p.Soft, 12), T("Move down"), pos < group.Count - 1, () => MoveTo(group[pos + 1])));
        var panel = Ui.Stack(8, row);
        var card = new Border
        {
            Background = Palette.Brush(p.Dark ? Color.Parse("#1C222B") : Colors.White),
            BorderBrush = Palette.Brush(open ? Color.FromArgb(115, p.Cool.R, p.Cool.G, p.Cool.B) : p.Stroke),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8, 6, 8, 6),
            Child = panel
        };
        handle.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed) return;
            dragging = slot;
            dragStart = e.GetPosition(container).Y;
            e.Pointer.Capture(handle);
            card.Opacity = .75;
            card.ZIndex = 10;
            e.Handled = true;
        };
        handle.PointerMoved += (_, e) =>
        {
            if (dragging != slot) return;
            card.RenderTransform = new TranslateTransform(0, e.GetPosition(container).Y - dragStart);
        };
        handle.PointerReleased += (_, e) =>
        {
            if (dragging != slot) return;
            dragging = null;
            e.Pointer.Capture(null);
            double y = e.GetPosition(container).Y;
            int target = group.Count - 1;
            for (int i = 0; i < container.Children.Count; i++)
                if (y < container.Children[i].Bounds.Bottom) { target = i; break; }
            if (y < 0) target = 0;
            if (target != pos) MoveTo(group[target]);
            else { card.RenderTransform = null; card.Opacity = 1; card.ZIndex = 0; }
        };
        if (open)
        {
            // Own title and hardware name, each with a switch to show or hide it.
            var hwNow = HardwareNameNow(slot.Kind);
            TextBox NameBox(string text, string watermark, Action<string> set)
            {
                var box = new TextBox { Text = text, Watermark = watermark, MinWidth = 0, CornerRadius = new CornerRadius(8), MaxLength = 60 };
                box.LostFocus += (_, _) => { string v = (box.Text ?? "").Trim(); set(v); changed(); };
                return box;
            }
            CheckBox ShowBox(bool on, Action<bool> set)
            {
                var c = new CheckBox { Content = T("Show"), IsChecked = on, FontSize = 12.5, Margin = new Thickness(12, 0, 0, 0) };
                c.IsCheckedChanged += (_, _) => { set(c.IsChecked == true); changed(); };
                return c;
            }
            Control NameRow(string label, TextBox box, CheckBox show)
            {
                var l = Ui.Text(label, 12.5, p.Soft);
                l.Width = 120;
                l.VerticalAlignment = VerticalAlignment.Center;
                return Ui.Columns("Auto,*,Auto", l, box, show);
            }
            var names = Ui.Stack(6,
                NameRow(T("Title"), NameBox(slot.Name, Cards.Name(slot.Kind), v => slot.Name = v), ShowBox(slot.ShowTitle, v => slot.ShowTitle = v)),
                NameRow(T("Hardware name"), NameBox(slot.HardwareName, hwNow.Length > 0 ? hwNow : T("None"), v => slot.HardwareName = v), ShowBox(slot.ShowHardware, v => slot.ShowHardware = v)));
            names.Margin = new Thickness(28, 0, 0, 6);
            panel.Children.Add(names);
            // Style and column first, then the single values.
            var options = new WrapPanel { Margin = new Thickness(28, 0, 0, 0) };
            if (Settings.HasStyles(slot.Kind))
            {
                // The styles as buttons with their small picture: the look is chosen by seeing it.
                var chips = new WrapPanel();
                foreach (var style in Enum.GetValues<WidgetStyle>())
                {
                    bool on = slot.Style == style;
                    var chip = new Button
                    {
                        Content = Ui.Row(6, new Viewbox { Width = 32, Height = 17, Child = StyleThumb(style) }, Ui.Text(StyleName(style), 12, on ? Colors.White : p.Ink, on ? FontWeight.SemiBold : FontWeight.Normal)),
                        Padding = new Thickness(8, 4, 10, 4), Margin = new Thickness(0, 0, 6, 6), CornerRadius = new CornerRadius(7),
                        Classes = { on ? "accent" : "plain" }
                    };
                    chip.Click += (_, _) => { slot.Style = style; changed(); Build(); };
                    chips.Children.Add(chip);
                }
                var styleRow = Ui.Columns("Auto,*", Ui.Text(T("Style"), 12.5, p.Soft), chips);
                if (styleRow.Children[0] is Control sl) { sl.Margin = new Thickness(0, 6, 10, 0); sl.VerticalAlignment = VerticalAlignment.Top; }
                styleRow.Margin = new Thickness(28, 0, 0, 0);
                panel.Children.Add(styleRow);
            }
            if (s.ColumnsInUse > 1)
                options.Children.Add(Ui.Row(8, Ui.Text(s.Horizontal ? T("Row") : T("Column"), 12.5, p.Soft), Choice(new[] { T("Automatic") }.Concat(Enumerable.Range(1, s.ColumnsInUse).Select(n => n.ToString())).ToList(), Math.Min(slot.Column, s.ColumnsInUse), i => slot.Column = i, small: true)));
            if (s.Pages > 1)
                options.Children.Add(Ui.Row(8, Ui.Text(T("Register"), 12.5, p.Soft), Choice(Enumerable.Range(1, s.Pages).Select(s.PageName).ToList(), Math.Min(slot.Page, s.Pages) - 1, i => slot.Page = i + 1, small: true)));
            foreach (var o in options.Children) o.Margin = new Thickness(0, 0, 18, 6);
            if (options.Children.Count > 0) panel.Children.Add(options);
            var wrap = new WrapPanel { Margin = new Thickness(28, 0, 0, 4) };
            // Only the values the chosen style can show; the Standard card shows them all.
            foreach (var (key, label) in Cards.Values(slot.Kind).Where(v => Summaries.Supports(slot.Kind, slot.Style, v.Key)))
            {
                var c = new CheckBox { Content = label, IsChecked = s.Shows(key), FontSize = 12.5, Margin = new Thickness(0, 0, 14, 0) };
                c.IsCheckedChanged += (_, _) =>
                {
                    if (c.IsChecked == true) s.HiddenValues.Remove(key); else if (!s.HiddenValues.Contains(key)) s.HiddenValues.Add(key);
                    changed();
                };
                wrap.Children.Add(c);
            }
            panel.Children.Add(wrap);
            // Which drives or volumes the widget lists, from what this PC has right now.
            var snap = snapshot();
            var items = slot.Kind switch
            {
                WidgetKind.Drives => snap?.Disks.Select(d => ("drive:" + d.Id, d.Letters.Length > 0 ? $"{Names.Short(d.Name)} · {d.Letters}" : Names.Short(d.Name))).ToList(),
                WidgetKind.Volumes => snap?.Volumes.Select(v => ("volume:" + v.Name, string.IsNullOrWhiteSpace(v.Label) ? v.Name : $"{v.Name} {v.Label}")).ToList(),
                _ => null
            };
            if (items is { Count: > 0 })
            {
                var which = new WrapPanel { Margin = new Thickness(28, 2, 0, 4) };
                var label = Ui.Text(slot.Kind == WidgetKind.Drives ? T("Drives shown") : T("Volumes shown"), 12.5, p.Soft);
                label.Margin = new Thickness(0, 0, 0, 4);
                foreach (var (key, text) in items)
                {
                    var c = new CheckBox { Content = text, IsChecked = s.Shows(key), FontSize = 12.5, Margin = new Thickness(0, 0, 14, 0) };
                    c.IsCheckedChanged += (_, _) =>
                    {
                        if (c.IsChecked == true) s.HiddenValues.Remove(key); else if (!s.HiddenValues.Contains(key)) s.HiddenValues.Add(key);
                        changed();
                    };
                    which.Children.Add(c);
                }
                var block = Ui.Stack(0, label, which);
                block.Margin = new Thickness(28, 4, 0, 4);
                which.Margin = new Thickness(0);
                panel.Children.Add(block);
            }
            // The external IP is fetched only here, on request – one HTTPS request, never in the background.
            if (slot.Kind == WidgetKind.Network)
            {
                var value = Ui.Text("", 12.5, p.Ink, FontWeight.Medium, true);
                value.VerticalAlignment = VerticalAlignment.Center;
                var fetch = new Button { Content = T("Get"), Padding = new Thickness(14, 5), CornerRadius = new CornerRadius(8), Margin = new Thickness(12, 0, 0, 0) };
                ToolTip.SetTip(fetch, T("Asks api.ipify.org once for the public address of this connection."));
                void Show()
                {
                    value.Text = ExternalIp.Busy ? T("Asking…")
                        : ExternalIp.Address is string a ? $"{a}  ·  {ExternalIp.At:HH:mm}"
                        : ExternalIp.Failed ? T("Not reachable") : T("Not fetched yet");
                    value.Foreground = Palette.Brush(ExternalIp.Address is null ? p.Soft : p.Ink);
                    fetch.IsEnabled = !ExternalIp.Busy;
                }
                void OnChanged() => Avalonia.Threading.Dispatcher.UIThread.Post(Show);
                ExternalIp.Changed += OnChanged;
                fetch.DetachedFromVisualTree += (_, _) => ExternalIp.Changed -= OnChanged;
                fetch.Click += async (_, _) => await ExternalIp.FetchAsync();
                Show();
                var l = Ui.Text(T("External IP"), 12.5, p.Soft);
                l.Width = 120;
                l.VerticalAlignment = VerticalAlignment.Center;
                var ipRow = Ui.Columns("Auto,*,Auto", l, value, fetch);
                ipRow.Margin = new Thickness(28, 6, 0, 4);
                panel.Children.Add(ipRow);
            }
        }
        return card;
    }

    // --- Appearance ------------------------------------------------------------------------------

    private Control[] AppearancePage()
    {
        RefreshPreview();
        // The preview survives rebuilds (so a moving slider keeps it); take it out of the old page first.
        if (previewHost.Parent is Panel old) old.Children.Remove(previewHost);
        var previewNote = Ui.Text(T("Shows at once how colours, glass and text size look – without looking at the sidebar."), 12.5, p.Soft);
        previewNote.TextWrapping = TextWrapping.Wrap;
        previewNote.TextTrimming = TextTrimming.None;
        var top = Ui.Columns("*,Auto", Ui.Stack(6, SectionTitle(T("Live preview")), previewNote), previewHost);
        previewNote.Margin = new Thickness(0, 0, 18, 0);

        // Theme as three buttons side by side: the choice is visible without opening a list.
        var themes = new[] { (ThemeMode.Dark, T("Dark")), (ThemeMode.Light, T("Light")), (ThemeMode.System, T("Like Windows")) };
        var segment = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        foreach (var (mode, name) in themes)
        {
            bool on = s.Theme == mode;
            var b = new Button
            {
                Content = name, Padding = new Thickness(14, 5), CornerRadius = new CornerRadius(7), BorderThickness = new Thickness(0), FontSize = 13,
                Background = on ? Palette.Brush(p.Dark ? Color.Parse("#2A3242") : Colors.White) : Brushes.Transparent,
                FontWeight = on ? FontWeight.SemiBold : FontWeight.Normal
            };
            b.Click += (_, _) => { s.Theme = mode; changed(); Build(); };
            segment.Children.Add(b);
        }
        var themeBox = new Border { Child = segment, Padding = new Thickness(3), CornerRadius = new CornerRadius(9), Background = Palette.Brush(p.Dark ? Color.Parse("#0F131A") : Color.Parse("#E4E9F0")), BorderBrush = Palette.Brush(p.Stroke), BorderThickness = new Thickness(1) };

        // Colour schemes and backgrounds as swatches.
        var schemes = new WrapPanel { MaxWidth = 330, HorizontalAlignment = HorizontalAlignment.Right };
        Button Swatch(IBrush fill, bool on, string tip, Action pick, double w = 24, bool round = false)
        {
            var b = new Button
            {
                Width = w + 4, Height = 28, Padding = new Thickness(0), Margin = new Thickness(0, 0, 6, 6), CornerRadius = new CornerRadius(round ? 14 : 7),
                Background = Brushes.Transparent, BorderThickness = new Thickness(2), BorderBrush = on ? Palette.Brush(p.Ink) : Brushes.Transparent,
                Content = new Border { Width = w, Height = 24, CornerRadius = new CornerRadius(round ? 12 : 5), Background = fill, BorderBrush = Palette.Brush(round ? (p.Dark ? Color.Parse("#4A5568") : Color.Parse("#AEB8C6")) : p.Stroke), BorderThickness = new Thickness(1) },
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            ToolTip.SetTip(b, tip);
            b.Click += (_, _) => { pick(); changed(); Build(); };
            return b;
        }
        var state = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative) };
        state.GradientStops.Add(new GradientStop(p.Ramp[0], 0)); state.GradientStops.Add(new GradientStop(p.Ramp[1], .45)); state.GradientStops.Add(new GradientStop(p.Ramp[2], .75)); state.GradientStops.Add(new GradientStop(p.Ramp[3], 1));
        schemes.Children.Add(Swatch(state, s.Colors == Settings.StateColors, T("By state (blue to red)"), () => s.Colors = Settings.StateColors, 50));
        var accent = Application.Current?.PlatformSettings?.GetColorValues().AccentColor1 ?? Color.Parse("#0078D4");
        schemes.Children.Add(Swatch(Palette.Brush(accent), s.Colors == Settings.WindowsAccent, T("Windows accent colour"), () => s.Colors = Settings.WindowsAccent));
        foreach (var (hex, name) in Palette.Presets)
            schemes.Children.Add(Swatch(Palette.Brush(Color.Parse(hex)), s.Colors.Equals(hex, StringComparison.OrdinalIgnoreCase), T(name), () => s.Colors = hex));

        bool darkLook = s.Theme switch { ThemeMode.Dark => true, ThemeMode.Light => false, _ => Application.Current?.ActualThemeVariant != Avalonia.Styling.ThemeVariant.Light };
        var backgrounds = new WrapPanel { MaxWidth = 330, HorizontalAlignment = HorizontalAlignment.Right };
        foreach (var bg in Palette.Backgrounds)
            backgrounds.Children.Add(Swatch(Palette.Brush(Color.Parse(darkLook ? bg.DarkCard : bg.LightCard)), s.Background == bg.Key, T(bg.Name), () => s.Background = bg.Key, round: true));
        string bgName = T(Palette.Backgrounds.FirstOrDefault(b => b.Key == s.Background).Name ?? Palette.Backgrounds[0].Name);
        string schemeName = s.Colors == Settings.StateColors ? T("By state (blue to red)") : s.Colors == Settings.WindowsAccent ? T("Windows accent colour")
            : T(Palette.Presets.FirstOrDefault(x => x.Hex.Equals(s.Colors, StringComparison.OrdinalIgnoreCase)).Name ?? "");

        // Opacity only means something with the glass effect, so it shows only then, set in below it.
        Control? opacity = null;
        if (s.Glass)
        {
            opacity = Row(T("Opacity"), Slide(.2, 1, .05, s.Opacity, v => { s.Opacity = v; RefreshPreview(); }, v => $"{v * 100:0} %"), T("Only with the glass effect."), nameof(Settings.Opacity));
            opacity.Margin = new Thickness(16, 0, 0, 0);
        }
        else Owns(nameof(Settings.Opacity));

        return new[]
        {
            top,
            Section(T("Colours"),
                Row(T("Theme"), themeBox, null, nameof(Settings.Theme)),
                Row(T("Colour scheme"), schemes, schemeName + (s.Colors == Settings.StateColors ? "" : " · " + T("Hot readings still turn amber and red.")), nameof(Settings.Colors)),
                Row(T("Background"), backgrounds, bgName, nameof(Settings.Background))),
            Section(T("Effects"),
                Row(T("Glass effect"), Toggle(s.Glass, v => s.Glass = v, rebuild: true), T("Translucent background with blur"), nameof(Settings.Glass)),
                opacity,
                Row(T("Smooth transitions"), Toggle(s.SmoothTransitions, v => s.SmoothTransitions = v, rebuild: true), T("Numbers count, bars glide and charts scroll smoothly."), nameof(Settings.SmoothTransitions))),
            Section(T("Text"),
                Row(T("Text size"), Slide(.5, 2, .05, s.TextScale, v => { s.TextScale = v; RefreshPreview(); }, v => $"{v * 100:0} %"), T("Above 100 % the sidebar grows wider by the same amount."), nameof(Settings.TextScale)),
                Row(T("Typeface"), Choice(FontNames(), Math.Max(0, Array.IndexOf(Settings.Fonts, s.Font)), i => s.Font = Settings.Fonts[i], rebuild: true), T("Narrow fonts fit more values into the bar."), nameof(Settings.Font)),
                Row(T("High contrast"), Toggle(s.HighContrast, v => s.HighContrast = v, rebuild: true), T("All labels in full brightness."), nameof(Settings.HighContrast)),
                Row(T("Hardware names"), Toggle(s.ShowNames, v => s.ShowNames = v, rebuild: true), T("Model names under the titles, e.g. Ryzen 9 5900X."), nameof(Settings.ShowNames))),
            ThemeSection()
        };
    }

    private static string[] FontNames() => new[] { T("Inter (built in)"), "Segoe UI", "Segoe UI Variable", "Bahnschrift", T("Bahnschrift narrow"), T("Monospace (Cascadia, Consolas)") };

    // Themes as files: the whole look in one *.lumentheme file to keep, swap or pass on.
    private string themeMessage = "", reportMessage = "";
    private static readonly FilePickerFileType ThemeType = new("Lumen theme") { Patterns = new[] { "*" + ThemeFile.Extension } };

    private Control ThemeSection()
    {
        Button Action(string text, Func<Task> run)
        {
            var b = new Button { Content = text, Padding = new Thickness(12, 6), CornerRadius = new CornerRadius(8) };
            b.Click += async (_, _) =>
            {
                try { await run(); }
                catch (Exception e) { themeMessage = T("The file could not be read or written.") + " " + e.Message; Build(); }
            };
            return b;
        }
        var buttons = Ui.Row(8, Action(T("Save theme …"), SaveTheme), Action(T("Load theme …"), LoadTheme));
        var help = T("Colours, background, glass, text size and typeface – no layout and no personal data.");
        return Section(T("Themes"), Row(T("Theme file"), buttons, themeMessage.Length > 0 ? themeMessage : help));
    }

    private async Task SaveTheme()
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = T("Save theme …"), SuggestedFileName = "Lumen" + ThemeFile.Extension, DefaultExtension = ThemeFile.Extension.TrimStart('.'),
            FileTypeChoices = new[] { ThemeType }, ShowOverwritePrompt = true
        });
        if (file is null) return;
        string name = System.IO.Path.GetFileNameWithoutExtension(file.Name);
        await using (var stream = await file.OpenWriteAsync())
        {
            stream.SetLength(0);
            await using var w = new System.IO.StreamWriter(stream, new System.Text.UTF8Encoding(false));
            await w.WriteAsync(ThemeFile.From(s, name).ToJson());
        }
        themeMessage = F("Saved as {0}.", file.Name);
        Build();
    }

    private async Task LoadTheme()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = T("Load theme …"), AllowMultiple = false, FileTypeFilter = new[] { ThemeType, FilePickerFileTypes.All } });
        if (files.Count == 0) return;
        string text;
        await using (var stream = await files[0].OpenReadAsync())
        using (var r = new System.IO.StreamReader(stream))
            text = await r.ReadToEndAsync();
        if (ThemeFile.Parse(text) is not { } theme) { themeMessage = T("This is not a Lumen theme file."); Build(); return; }
        theme.ApplyTo(s);
        themeMessage = F("Theme “{0}” applied.", theme.Name.Length > 0 ? theme.Name : System.IO.Path.GetFileNameWithoutExtension(files[0].Name));
        changed();
        RefreshPreview();
        Build();
    }

    // A processor card drawn with the colours, glass and text size as they are set right now.
    private void RefreshPreview()
    {
        var keep = Ui.FontKey;
        Ui.UseFont(s.Font);
        try { DrawPreview(); }
        finally { Ui.UseFont(keep); }
    }

    private void DrawPreview()
    {
        bool dark = s.Theme switch { ThemeMode.Dark => true, ThemeMode.Light => false, _ => Application.Current?.ActualThemeVariant != Avalonia.Styling.ThemeVariant.Light };
        Color? accent = s.Colors switch
        {
            Settings.StateColors => null,
            Settings.WindowsAccent => Application.Current?.PlatformSettings?.GetColorValues().AccentColor1,
            var hex => Color.TryParse(hex, out var c) ? c : null
        };
        var look = Palette.For(dark, s.Glass, s.Opacity, accent, s.HighContrast, s.Background);
        double k = Math.Clamp(s.TextScale, .5, 2);
        var cpu = snapshot()?.Cpu;
        double temp = cpu?.Temperature ?? 56, load = cpu?.Load ?? 8;
        string name = Names.Clean(cpu?.Name) is { Length: > 0 } n ? n : "Ryzen 9 5900X";
        var head = Ui.Columns("*,Auto", Ui.Text(T("Processor"), 12 * k, look.Ink, FontWeight.SemiBold), Ui.Text(cpu?.ClockMhz is double mhz ? $"{mhz / 1000:0.00} GHz" : "3.73 GHz", 11 * k, look.Soft));
        var hw = Ui.Text(name, 11 * k, look.Soft);
        hw.IsVisible = s.ShowNames;
        var big = Ui.Row(2, Ui.Text($"{Format.Temperature(temp, s.Fahrenheit)}", 32 * k, look.At(Math.Clamp((temp - 20) / 80, 0, 1)), FontWeight.SemiBold, true), Ui.Text(Format.TemperatureUnit(s.Fahrenheit), 13 * k, look.Soft));
        var numbers = Ui.Columns("*,Auto", big, Ui.Text($"{load:0} %", 16 * k, look.Ink, FontWeight.SemiBold, true));
        numbers.Margin = new Thickness(0, 6, 0, 4);
        var line = new Polyline { Stroke = Palette.Brush(look.Cool), StrokeThickness = 1.6, Points = new List<Point> { new(0, 18), new(24, 16), new(48, 20), new(72, 13), new(96, 16), new(120, 8), new(144, 14), new(168, 11), new(192, 15), new(216, 9), new(240, 12) } };
        var chart = new Viewbox { Stretch = Stretch.Fill, Height = 26, Child = new Canvas { Width = 240, Height = 24, Children = { line } } };
        var card = new Border
        {
            Background = Palette.Brush(look.Card), BorderBrush = Palette.Brush(look.Stroke), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14),
            Padding = new Thickness(14, 12), Child = Ui.Stack(1, head, hw, numbers, chart)
        };
        // Behind the card a stand-in for the desktop, so glass and opacity show.
        var wall = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative) };
        wall.GradientStops.Add(new GradientStop(Color.Parse(dark ? "#2B3A67" : "#9DB7E8"), 0));
        wall.GradientStops.Add(new GradientStop(Color.Parse(dark ? "#5B2C4F" : "#E8B6C9"), 1));
        previewHost.Width = 300;
        previewHost.CornerRadius = new CornerRadius(16);
        previewHost.Padding = new Thickness(14);
        previewHost.Background = wall;
        previewHost.Child = card;
    }

    // --- Placement -------------------------------------------------------------------------------

    private Control[] PlacementPage()
    {
        var names = screens();
        Control? barNote = null;
        if (s.Horizontal)
        {
            var note = Ui.Text(T("Bar at the top or bottom: the widgets sit side by side."), 12, p.Soft);
            note.TextWrapping = TextWrapping.Wrap;
            note.TextTrimming = TextTrimming.None;
            barNote = note;
        }
        Owns(nameof(Settings.Width));
        if (!s.Horizontal) Owns(nameof(Settings.BarHeight), nameof(Settings.BarFullWidth));
        else Owns(nameof(Settings.FullScreen));
        if (s.Pages <= 1) Owns(nameof(Settings.PageNames));
        return new[]
        {
            Section(T("Position"),
                Row(T("Screen edge"), Choice(new[] { T("Right"), T("Left"), T("Top (bar)"), T("Bottom (bar)") }, (int)s.Edge, i =>
                {
                    // Columns at the side become rows in the bar; start with one when switching between them.
                    bool bar = i >= 2;
                    if (bar != s.Horizontal) s.Columns = 1;
                    s.Edge = (Edge)i;
                }, rebuild: true), s.Horizontal ? T("A bar across the whole width; the widgets sit side by side.") : null, nameof(Settings.Edge)),
                Row(T("Screen"), Choice(names.Count > 0 ? names : new[] { T("Main screen") }, Math.Min(s.Screen, Math.Max(0, names.Count - 1)), i => s.Screen = i, rebuild: true), null, nameof(Settings.Screen)),
                Row(T("Always on top"), Toggle(s.Topmost, v => s.Topmost = v, rebuild: true), null, nameof(Settings.Topmost)),
                Row(T("Reserve space"), Toggle(s.ReserveSpace, v => s.ReserveSpace = v, rebuild: true), T("Maximised windows stop before the sidebar."), nameof(Settings.ReserveSpace)),
                Row(T("Click-through"), Toggle(s.ClickThrough, v => s.ClickThrough = v, rebuild: true), T("Clicks go to the windows behind the sidebar; open the settings from the tray icon."), nameof(Settings.ClickThrough))),
            Section(T("Layout"),
                barNote,
                Row(s.Horizontal ? T("Rows") : T("Columns"), Choice(new[] { "1", "2", "3", "4", "5", "6" }, s.Columns - 1, i => s.Columns = i + 1, rebuild: true),
                    s.Horizontal ? T("Rows one below the other, like columns at the side; each is as tall as the row height.") : T("Several columns side by side; each is as wide as set below, everything stays large."), nameof(Settings.Columns)),
                Row(T("Registers"), Choice(new[] { "1", "2", "3", "4", "5", "6" }, s.Pages - 1, i => s.Pages = i + 1, rebuild: true), T("Pages at the top; each widget goes on one of them. Switch by click or mouse wheel."), nameof(Settings.Pages)),
                s.Pages > 1 ? WideRow(T("Register names"), PageNameEditor(), T("Shown at the top instead of 1, 2, 3; leave empty for the number."), nameof(Settings.PageNames)) : null,
                s.Horizontal
                    ? Row(T("Whole width"), Toggle(s.BarFullWidth, v => s.BarFullWidth = v, rebuild: true), T("Off: the bar is only as wide as its widgets, centred."), nameof(Settings.BarFullWidth))
                    : Row(T("Whole screen"), Toggle(s.FullScreen, v => s.FullScreen = v, rebuild: true), T("Covers the whole monitor up to the taskbar; the columns share its width."), nameof(Settings.FullScreen)),
                s.Horizontal ? Row(T("Row height"), Slide(40, 700, 4, s.BarHeight, v => s.BarHeight = v, v => $"{v:0} px"), T("Status fits from 40 px, Compact from 64 px; Ring, Tile and Bar read best in a low bar."), nameof(Settings.BarHeight)) : null,
                Row(T("Density"), Choice(new[] { T("Automatic"), T("Comfortable"), T("Compact") }, (int)s.Density, i => s.Density = (Density)i, rebuild: true),
                    s.Horizontal ? T("In the bar, Compact makes the widgets smaller and tighter.") : T("Automatic fills the height up to the taskbar, without scrolling."), nameof(Settings.Density)),
                s.Horizontal && s.BarFullWidth ? null : Row(s.Horizontal ? T("Widget width") : T("Width"), Slide(240, 440, 10, s.Width, v => s.Width = v, v => $"{v:0} px"), null, nameof(Settings.Width)))
        };
    }

    private Control WeatherEditor()
    {
        var box = new TextBox { Text = s.WeatherPlace, Watermark = "Zürich", CornerRadius = new CornerRadius(8), MinWidth = 0 };
        var search = new Button { Content = T("Search"), Padding = new Thickness(14, 6), CornerRadius = new CornerRadius(8), Margin = new Thickness(8, 0, 0, 0) };
        var status = Ui.Text(weatherStatus, 12, p.Soft);
        status.TextWrapping = TextWrapping.Wrap;
        status.TextTrimming = TextTrimming.None;
        status.IsVisible = weatherStatus.Length > 0;
        async void Find()
        {
            string query = box.Text ?? "";
            if (string.IsNullOrWhiteSpace(query))
            {
                s.WeatherPlace = ""; s.WeatherLatitude = s.WeatherLongitude = null;
                weatherStatus = ""; changed(); weatherChanged(); return;
            }
            search.IsEnabled = false;
            status.Text = T("Searching…"); status.IsVisible = true;
            try
            {
                var found = await WeatherClient.FindAsync(query, L.Language);
                if (found is null) weatherStatus = T("No place found.");
                else
                {
                    s.WeatherPlace = found.Name; s.WeatherLatitude = found.Latitude; s.WeatherLongitude = found.Longitude;
                    weatherStatus = F("Found: {0}", found.Region.Length > 0 ? $"{found.Name}, {found.Region}" : found.Name);
                    // Showing the weather is the point of searching for a place.
                    var slot = s.Widgets.First(w => w.Kind == WidgetKind.Weather);
                    slot.Visible = true;
                    changed(); weatherChanged();
                }
            }
            catch { weatherStatus = T("No connection to the weather service."); }
            Build();
        }
        search.Click += (_, _) => Find();
        box.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) Find(); };
        var note = Ui.Text(T("Weather data from Open-Meteo; only the place's coordinates are sent."), 11.5, p.Soft);
        note.TextWrapping = TextWrapping.Wrap;
        note.TextTrimming = TextTrimming.None;
        var label = Ui.Text(T("Place"), 13.5, p.Ink, FontWeight.Medium);
        label.Margin = new Thickness(0, 0, 16, 0);
        return Ui.Stack(8, Ui.Columns("Auto,*,Auto", label, box, search), status, note);
    }

    public static readonly string[] Zones =
    {
        "America/Los_Angeles", "America/Denver", "America/Chicago", "America/New_York", "America/Toronto", "America/Mexico_City", "America/Sao_Paulo",
        "Europe/London", "Europe/Zurich", "Europe/Istanbul", "Europe/Moscow", "Africa/Johannesburg", "Asia/Dubai", "Asia/Kolkata", "Asia/Bangkok",
        "Asia/Singapore", "Asia/Hong_Kong", "Asia/Shanghai", "Asia/Seoul", "Asia/Tokyo", "Australia/Sydney", "Pacific/Auckland"
    };

    private Control WorldClockPicker()
    {
        var wrap = new WrapPanel();
        foreach (var zone in Zones)
        {
            var c = new CheckBox { Content = WorldClocksCard.City(zone), IsChecked = s.WorldClocks.Contains(zone), FontSize = 12.5, Margin = new Thickness(0, 0, 10, 2), MinWidth = 128 };
            c.IsCheckedChanged += (_, _) =>
            {
                if (c.IsChecked == true && !s.WorldClocks.Contains(zone))
                {
                    if (s.WorldClocks.Count >= 4) { c.IsChecked = false; return; }
                    s.WorldClocks.Add(zone);
                }
                else if (c.IsChecked != true) s.WorldClocks.Remove(zone);
                changed();
            };
            wrap.Children.Add(c);
        }
        var note = Ui.Text(T("Up to four cities."), 11.5, p.Soft);
        return Ui.Stack(8, wrap, note);
    }

    private Control Section(string titleText, params Control?[] given)
    {
        var rows = given.Where(r => r != null).Cast<Control>().ToArray();
        var t = Ui.Text(titleText.ToUpper(L.Culture), 11, p.Soft, FontWeight.SemiBold);
        t.LetterSpacing = 1;
        var panel = new StackPanel { Spacing = 14 };
        panel.Children.AddRange(rows);
        bool plain = rows.Length == 1 && rows[0] is StackPanel { Spacing: 6 };
        var card = new Border
        {
            Background = plain ? Brushes.Transparent : Palette.Brush(p.Dark ? Color.Parse("#191E26") : Colors.White),
            BorderBrush = Palette.Brush(p.Stroke),
            BorderThickness = new Thickness(plain ? 0 : 1),
            CornerRadius = new CornerRadius(12),
            Padding = plain ? new Thickness(0) : new Thickness(18, 16),
            Child = panel
        };
        return Ui.Stack(10, t, card);
    }

    private Control Row(string label, Control editor, string? help = null, string? prop = null)
    {
        Index(label, help);
        var l = Ui.Text(label, 13.5, p.Ink, FontWeight.Medium);
        var left = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
        left.Children.Add(ChangedDot(prop) is { } dot ? Ui.Row(0, l, dot) : l);
        if (help != null)
        {
            var h = Ui.Text(help, 11.5, p.Soft);
            h.TextWrapping = TextWrapping.Wrap;
            h.TextTrimming = TextTrimming.None;
            left.Children.Add(h);
        }
        editor.VerticalAlignment = VerticalAlignment.Center;
        return Ui.Columns("*,Auto", left, editor);
    }

    // Label and help on top, the editor across the full width below (for editors wider than a dropdown).
    private Control WideRow(string label, Control editor, string? help = null, string? prop = null)
    {
        Index(label, help);
        var panel = new StackPanel { Spacing = 2 };
        var l = Ui.Text(label, 13.5, p.Ink, FontWeight.Medium);
        panel.Children.Add(ChangedDot(prop) is { } dot ? Ui.Row(0, l, dot) : l);
        if (help != null)
        {
            var h = Ui.Text(help, 11.5, p.Soft);
            h.TextWrapping = TextWrapping.Wrap;
            h.TextTrimming = TextTrimming.None;
            panel.Children.Add(h);
        }
        editor.Margin = new Thickness(0, 8, 0, 0);
        panel.Children.Add(editor);
        return panel;
    }

    private Control Toggle(bool value, Action<bool> set, bool notify = true, bool rebuild = false)
    {
        var t = new ToggleSwitch { IsChecked = value, OnContent = "", OffContent = "", MinWidth = 0, Margin = new Thickness(0, 0, -8, 0) };
        t.IsCheckedChanged += (_, _) => { set(t.IsChecked == true); if (notify) changed(); if (rebuild) Avalonia.Threading.Dispatcher.UIThread.Post(Build); };
        return t;
    }

    // One small box per register; the change shows in the sidebar as soon as a box loses focus.
    private Control PageNameEditor()
    {
        // Three boxes per line, each a third of the width, so all six fit any window size.
        var grid = new Avalonia.Controls.Primitives.UniformGrid { Columns = Math.Min(3, s.Pages) };
        for (int i = 0; i < s.Pages; i++)
        {
            int n = i;
            var box = new TextBox { Text = s.PageNames.ElementAtOrDefault(n) ?? "", Watermark = $"{T("Register")} {n + 1}", MaxLength = 16, CornerRadius = new CornerRadius(8), Margin = new Thickness(0, 0, 6, 6), MinWidth = 0 };
            box.LostFocus += (_, _) =>
            {
                while (s.PageNames.Count < 6) s.PageNames.Add("");
                string v = (box.Text ?? "").Trim();
                if (s.PageNames[n] == v) return;
                s.PageNames[n] = v;
                changed();
            };
            grid.Children.Add(box);
        }
        return grid;
    }

    // The hardware name a widget shows by default, for the hint in the empty box.
    private string HardwareNameNow(WidgetKind kind)
    {
        var snap = snapshot();
        if (snap is null) return "";
        return kind switch
        {
            WidgetKind.Cpu => Names.Clean(snap.Cpu?.Name),
            WidgetKind.Gpu => Names.Clean(snap.Gpus.FirstOrDefault()?.Name),
            WidgetKind.Cooling => Names.Clean(snap.BoardName),
            WidgetKind.Network => Names.Clean(snap.Network?.Name),
            WidgetKind.System => snap.System is { } y ? $"{y.Os} · {y.Build}" : "",
            _ => ""
        };
    }

    // What the chosen source needs, and whether Lumen reached it on the last update.
    private static string SourceHelp(SensorSource source)
    {
        if (source == SensorSource.Builtin) return T("LibreHardwareMonitor and Open Hardware Monitor, built into Lumen.");
        string status = ExternalSensors.Status;
        string how = source switch
        {
            SensorSource.Aida64 => T("In AIDA64: Preferences → Hardware Monitoring → External Applications → Enable shared memory."),
            SensorSource.HwInfo => T("In HWiNFO: Settings → Shared Memory Support (the free version turns it off after 12 hours)."),
            _ => T("Uses AIDA64 if it is running, otherwise HWiNFO, otherwise the built-in sensors.")
        };
        if (status.StartsWith("connected:"))
        {
            string name = status.EndsWith(nameof(SensorSource.Aida64)) ? "AIDA64" : "HWiNFO";
            return L.F("Connected to {0}. Readings it does not have come from the built-in sensors.", name) + " " + how;
        }
        if (status.StartsWith("missing:"))
            return T("Not reachable – the program is not running or its shared memory is off. The built-in sensors are shown meanwhile.") + " " + how;
        return T("Readings it does not have come from the built-in sensors.") + " " + how;
    }

    private Control Choice(IReadOnlyList<string> items, int selected, Action<int> set, bool rebuild = false, bool small = false)
    {
        var c = new ComboBox { ItemsSource = items, SelectedIndex = Math.Clamp(selected, 0, items.Count - 1), MinWidth = small ? 110 : 190, MaxWidth = 290, CornerRadius = new CornerRadius(8) };
        c.SelectionChanged += (_, _) =>
        {
            if (c.SelectedIndex < 0) return;
            set(c.SelectedIndex);
            changed();
            if (rebuild) Avalonia.Threading.Dispatcher.UIThread.Post(Build);
        };
        return c;
    }

    private Control Slide(double min, double max, double step, double value, Action<double> set, Func<double, string> show)
    {
        var text = Ui.Text(show(value), 12.5, p.Soft, FontWeight.Medium, true);
        text.Width = 52;
        text.TextAlignment = TextAlignment.Right;
        var slider = new Slider { Minimum = min, Maximum = max, Value = value, TickFrequency = step, IsSnapToTickEnabled = true, Width = 150 };
        slider.ValueChanged += (_, e) => { text.Text = show(e.NewValue); set(e.NewValue); changed(); };
        return Ui.Row(10, slider, text);
    }
}
