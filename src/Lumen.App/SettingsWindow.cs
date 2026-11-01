using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;
using static Lumen.App.L;

namespace Lumen.App;

// Every change applies to the sidebar immediately; there is no "Apply" step to forget.
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
    private readonly Button reset, done;
    private readonly HashSet<WidgetKind> expanded = new();
    private string weatherStatus = "";

    private readonly Func<string>? report;
    private readonly Func<Snapshot?> snapshot;

    public SettingsWindow(Settings settings, Palette palette, Action onChanged, Func<IReadOnlyList<string>> screenNames, Platform platform, Action? onWeather = null, Func<string>? report = null, Func<Snapshot?>? snapshot = null)
    {
        this.report = report;
        this.snapshot = snapshot ?? (() => null);
        s = settings; p = palette; changed = onChanged; screens = screenNames; this.platform = platform; weatherChanged = onWeather ?? (() => { });
        Width = 940; Height = 780; MinWidth = 760; MinHeight = 520;
        FontFamily = Ui.Font;
        Background = Palette.Brush(p.Dark ? Color.Parse("#14181F") : Color.Parse("#F3F5F8"));
        RequestedThemeVariant = p.Dark ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Icon = new WindowIcon(Avalonia.Platform.AssetLoader.Open(new Uri("avares://Lumen/Assets/lumen.ico")));

        reset = new Button { Padding = new Thickness(14, 7), CornerRadius = new CornerRadius(8) };
        reset.Click += (_, _) => Reset();
        done = new Button { Padding = new Thickness(22, 7), CornerRadius = new CornerRadius(8), Classes = { "accent" } };
        done.Click += (_, _) => Close();
        var footer = new Border
        {
            BorderBrush = Palette.Brush(p.Stroke),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(28, 14),
            Child = Ui.Columns("Auto,*,Auto", reset, new Control(), done)
        };
        title = Ui.Text("", 22, p.Ink, FontWeight.SemiBold);
        lead = Ui.Text("", 12.5, p.Soft);
        var head = Ui.Stack(2, title, lead);
        head.Margin = new Thickness(26, 24, 16, 18);
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

    private void Build()
    {
        Title = "Lumen – " + T("Settings");
        title.Text = T("Settings");
        lead.Text = T("Changes apply immediately.");
        reset.Content = T("Reset all");
        done.Content = T("Done");
        content.Children.Clear();
        var pages = new List<(string Name, string Glyph, Control[] Body)>();

        var languages = new List<string> { T("Automatic (Windows)") };
        languages.AddRange(L.Choices.Select(c => c.Name));
        int langIndex = s.Language == "auto" ? 0 : Array.FindIndex(L.Choices, c => c.Code == s.Language) + 1;

        var colorNames = new List<string> { T("By state (blue to red)"), T("Windows accent colour") };
        colorNames.AddRange(Palette.Presets.Select(x => T(x.Name)));
        int colorIndex = s.Colors == Settings.StateColors ? 0 : s.Colors == Settings.WindowsAccent ? 1 : Math.Max(0, Array.FindIndex(Palette.Presets, x => x.Hex.Equals(s.Colors, StringComparison.OrdinalIgnoreCase)) + 2);
        pages.Add((T("Appearance"), Glyphs.Gear, new[] { Section(T("Appearance"),
            Row(T("Language"), Choice(languages, langIndex, i => s.Language = i == 0 ? "auto" : L.Choices[i - 1].Code, rebuild: true)),
            Row(T("Theme"), Choice(new[] { T("Like Windows"), T("Dark"), T("Light") }, (int)s.Theme, i => s.Theme = (ThemeMode)i)),
            Row(T("Colours"), Choice(colorNames, colorIndex, i => s.Colors = i == 0 ? Settings.StateColors : i == 1 ? Settings.WindowsAccent : Palette.Presets[i - 2].Hex), colorIndex == 0 ? null : T("Hot readings still turn amber and red.")),
            Row(T("Background"), Choice(Palette.Backgrounds.Select(b => T(b.Name)).ToList(), Math.Max(0, Array.FindIndex(Palette.Backgrounds, b => b.Key == s.Background)), i => s.Background = Palette.Backgrounds[i].Key), T("Tone of the sidebar and the cards.")),
            Row(T("Smooth transitions"), Toggle(s.SmoothTransitions, v => s.SmoothTransitions = v), T("Numbers count, bars glide and charts scroll smoothly.")),
            Row(T("Hardware names"), Toggle(s.ShowNames, v => s.ShowNames = v), T("Model names under the titles, e.g. Ryzen 9 5900X.")),
            Row(T("High contrast"), Toggle(s.HighContrast, v => s.HighContrast = v), T("All labels in full brightness.")),
            Row(T("Glass effect"), Toggle(s.Glass, v => s.Glass = v), T("Translucent background with blur")),
            Row(T("Opacity"), Slide(.2, 1, .05, s.Opacity, v => s.Opacity = v, v => $"{v * 100:0} %")),
            Row(T("Text size"), Slide(.5, 2, .05, s.TextScale, v => s.TextScale = v, v => $"{v * 100:0} %"), T("Above 100 % the sidebar grows wider by the same amount."))) }));

        var barHeight = Row(T("Row height"), Slide(160, 700, 10, s.BarHeight, v => s.BarHeight = v, v => $"{v:0} px"), T("Ring, Tile and Bar styles read best in a low bar."));
        Control? barNote = null;
        if (s.Horizontal)
        {
            var note = Ui.Text(T("Bar at the top or bottom: the widgets sit side by side."), 12, p.Soft);
            note.TextWrapping = TextWrapping.Wrap;
            note.TextTrimming = TextTrimming.None;
            barNote = note;
        }
        pages.Add((T("Layout"), Glyphs.Columns, new[] { Section(T("Layout"),
            barNote,
            Row(s.Horizontal ? T("Rows") : T("Columns"), Choice(new[] { "1", "2", "3", "4", "5", "6" }, s.Columns - 1, i => s.Columns = i + 1, rebuild: true),
                s.Horizontal ? T("Rows one below the other, like columns at the side; each is as tall as the row height.") : T("Several columns side by side; each is as wide as set below, everything stays large.")),
            Row(T("Registers"), Choice(new[] { "1", "2", "3", "4", "5", "6" }, s.Pages - 1, i => s.Pages = i + 1, rebuild: true), T("Pages at the top; each widget goes on one of them. Switch by click or mouse wheel.")),
            s.Pages > 1 ? WideRow(T("Register names"), PageNameEditor(), T("Shown at the top instead of 1, 2, 3; leave empty for the number.")) : null,
            s.Horizontal
                ? Row(T("Whole width"), Toggle(s.BarFullWidth, v => s.BarFullWidth = v, rebuild: true), T("Off: the bar is only as wide as its widgets, centred."))
                : Row(T("Whole screen"), Toggle(s.FullScreen, v => s.FullScreen = v), T("Covers the whole monitor up to the taskbar; the columns share its width.")),
            s.Horizontal ? barHeight : null,
            Row(T("Density"), Choice(new[] { T("Automatic"), T("Comfortable"), T("Compact") }, (int)s.Density, i => s.Density = (Density)i),
                s.Horizontal ? T("In the bar, Compact makes the widgets smaller and tighter.") : T("Automatic fills the height up to the taskbar, without scrolling.")),
            s.Horizontal && s.BarFullWidth ? null : Row(s.Horizontal ? T("Widget width") : T("Width"), Slide(240, 440, 10, s.Width, v => s.Width = v, v => $"{v:0} px"))) }));

        var names = screens();
        pages.Add((T("Position"), Glyphs.System, new[] { Section(T("Position"),
            Row(T("Screen edge"), Choice(new[] { T("Right"), T("Left"), T("Top (bar)"), T("Bottom (bar)") }, (int)s.Edge, i =>
            {
                // Columns at the side become rows in the bar; start with one when switching between them.
                bool bar = i >= 2;
                if (bar != s.Horizontal) s.Columns = 1;
                s.Edge = (Edge)i;
            }, rebuild: true),
                s.Horizontal ? T("A bar across the whole width; the widgets sit side by side.") : null),
            s.Horizontal ? Row(T("Row height"), Slide(160, 700, 10, s.BarHeight, v => s.BarHeight = v, v => $"{v:0} px")) : null,
            Row(T("Screen"), Choice(names.Count > 0 ? names : new[] { T("Main screen") }, Math.Min(s.Screen, Math.Max(0, names.Count - 1)), i => s.Screen = i)),
            Row(T("Always on top"), Toggle(s.Topmost, v => s.Topmost = v)),
            Row(T("Reserve space"), Toggle(s.ReserveSpace, v => s.ReserveSpace = v), T("Maximised windows stop before the sidebar.")),
            Row(T("Click-through"), Toggle(s.ClickThrough, v => s.ClickThrough = v), T("Clicks go to the windows behind the sidebar; open the settings from the tray icon."))) }));

        pages.Add((T("Readings"), Glyphs.Cpu, new[] { Section(T("Readings"),
            Row(T("Update interval"), Slide(.5, 5, .5, s.IntervalMs / 1000.0, v => s.IntervalMs = (int)Math.Round(v * 1000), v => v.ToString("0.0", L.Culture) + " s")),
            Row(T("Temperature"), Choice(new[] { T("Celsius (°C)"), T("Fahrenheit (°F)") }, s.Fahrenheit ? 1 : 0, i => s.Fahrenheit = i == 1)),
            Row(T("Warn from"), Slide(50, 110, 1, s.TemperatureWarning, v => s.TemperatureWarning = v, v => $"{v:0} °C"), T("Above this the temperature turns red.")),
            Row(T("Warning pop-up"), Toggle(s.WarningPopup, v => s.WarningPopup = v), T("Shows a notice when a temperature crosses the limit.")),
            Row(T("Integrated graphics"), Toggle(s.ShowIntegratedGpu, v => s.ShowIntegratedGpu = v), T("Also show the graphics built into the processor."))) }));

        pages.Add((T("Weather and clocks"), Glyphs.Cloud, new[] { Section(T("Weather"), WeatherEditor()), Section(T("World clocks"), WorldClockPicker()) }));

        var price = new NumericUpDown { Value = (decimal)s.PricePerKwh, Minimum = 0, Maximum = 5, Increment = 0.01m, FormatString = "0.00", Width = 130, CornerRadius = new CornerRadius(8) };
        price.ValueChanged += (_, _) => { s.PricePerKwh = (double)(price.Value ?? 0); changed(); };
        var currency = new TextBox { Text = s.Currency, Width = 70, CornerRadius = new CornerRadius(8), MaxLength = 5 };
        currency.LostFocus += (_, _) => { s.Currency = currency.Text ?? "CHF"; changed(); };
        var target = new TextBox { Text = s.PingTarget, Width = 170, CornerRadius = new CornerRadius(8) };
        target.LostFocus += (_, _) => { s.PingTarget = target.Text ?? "1.1.1.1"; changed(); };
        pages.Add((T("Power and ping"), Glyphs.Power, new[] { Section(T("Power and cost"),
            Row(T("Price per kWh"), Ui.Row(8, price, currency)),
            Row(T("Rest of the PC"), Slide(0, 300, 5, s.BaseWatts, v => s.BaseWatts = v, v => $"{v:0} W"), T("Screen, board, drives and fans are not measured; this estimate is added."))),
            Section("Ping", Row(T("Target"), target, T("A server on the internet, e.g. 1.1.1.1 or google.com."))) }));

        var widgets = new StackPanel { Spacing = 6 };
        for (int i = 0; i < s.Widgets.Count; i++) widgets.Children.Add(WidgetRow(i));
        var widgetHelp = Ui.Text(T("Tick what the sidebar shows and order it with the arrows; Customise sets style, column, register and single values."), 12, p.Soft);
        widgetHelp.TextWrapping = TextWrapping.Wrap;
        widgetHelp.TextTrimming = TextTrimming.None;
        // One click sets the style of every widget that has styles; each can still be changed below.
        var allStyles = new WrapPanel();
        var styleNames = new[] { T("Standard"), T("Ring"), T("Minimal"), T("Bar"), T("Graph"), T("Tile") };
        for (int i = 0; i < styleNames.Length; i++)
        {
            var style = (WidgetStyle)i;
            var styled = s.Widgets.Where(w => Settings.HasStyles(w.Kind)).ToList();
            bool on = styled.Count > 0 && styled.All(w => w.Style == style);
            var b = new Button
            {
                Content = styleNames[i], Padding = new Thickness(14, 6), CornerRadius = new CornerRadius(8), Margin = new Thickness(0, 0, 8, 8),
                Classes = { on ? "accent" : "plain" }
            };
            b.Click += (_, _) =>
            {
                foreach (var w in s.Widgets.Where(w => Settings.HasStyles(w.Kind))) w.Style = style;
                changed();
                Build();
            };
            allStyles.Children.Add(b);
        }
        var allRow = WideRow(T("Style for all widgets"), allStyles, T("One click sets every widget with styles; each one can still be changed below."));
        pages.Insert(2, (T("Widgets"), Glyphs.Apps, new Control[] { widgetHelp, Section(T("Style for all widgets"), allRow), Section(T("Widgets"), widgets) }));

        var admin = platform.IsAdministrator;
        var accessText = Ui.Text(admin ? T("Running with administrator rights – all sensors are reachable.") : T("Without administrator rights the CPU temperatures are missing."), 12, admin ? p.Calm : p.Soft);
        accessText.TextWrapping = TextWrapping.Wrap;
        accessText.TextTrimming = TextTrimming.None;
        var elevate = new Button { Content = T("Restart as administrator"), Padding = new Thickness(12, 6), CornerRadius = new CornerRadius(8), IsVisible = !admin, Margin = new Thickness(0, 8, 0, 0) };
        elevate.Click += (_, _) => platform.RestartElevated();
        var autostart = Toggle(platform.AutostartEnabled, v => { if (!platform.SetAutostart(v)) Build(); }, notify: false);
        var sensors = new Button { Content = T("Show all sensors"), Padding = new Thickness(12, 6), CornerRadius = new CornerRadius(8) };
        sensors.Click += (_, _) => new SensorsWindow(p, report ?? (() => ""), Path.GetDirectoryName(platform.SettingsPath) ?? ".").Show();
        pages.Add((T("System"), Glyphs.Shield, new[] { Section(T("System"),
            Row(T("Start with Windows"), autostart, T("Starts with administrator rights, without a prompt.")),
            Ui.Stack(0, accessText, elevate),
            Row(T("Sensors"), sensors, T("Lists everything LibreHardwareMonitor and Open Hardware Monitor found."))),
            Ui.Text($"Lumen {typeof(SettingsWindow).Assembly.GetName().Version?.ToString(3)} · {F("Settings stored in {0}", Path.GetFileName(platform.SettingsPath))}", 11, p.Faint) }));

        // Navigation on the left, the chosen page on the right.
        pageIndex = Math.Clamp(pageIndex, 0, pages.Count - 1);
        nav.Children.Clear();
        for (int i = 0; i < pages.Count; i++)
        {
            int n = i;
            bool on = n == pageIndex;
            var item = new Button
            {
                Content = Ui.Row(11, Ui.Glyph(pages[n].Glyph, on ? p.Ink : p.Soft, 15), Ui.Text(pages[n].Name, 13.5, on ? p.Ink : p.Soft, on ? FontWeight.SemiBold : FontWeight.Medium)),
                Background = on ? Palette.Brush(p.Dark ? Color.Parse("#222A35") : Colors.White) : Brushes.Transparent,
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(12, 9),
                CornerRadius = new CornerRadius(8),
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
            };
            item.Click += (_, _) => { pageIndex = n; scroller.Offset = default; Build(); };
            nav.Children.Add(item);
        }
        // The section title already names the page; the page itself starts right away.
        foreach (var c in pages[pageIndex].Body) content.Children.Add(c);
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

    private Control WidgetRow(int index)
    {
        var slot = s.Widgets[index];
        var check = new CheckBox { Content = Cards.Name(slot.Kind), IsChecked = slot.Visible, FontSize = 13.5 };
        check.IsCheckedChanged += (_, _) => { slot.Visible = check.IsChecked == true; changed(); };
        Button Small(Control body, string tip, bool enabled, Action click)
        {
            var b = new Button { Content = body, Padding = new Thickness(8, 6), CornerRadius = new CornerRadius(7), IsEnabled = enabled, Margin = new Thickness(6, 0, 0, 0) };
            ToolTip.SetTip(b, tip);
            b.Click += (_, _) => click();
            return b;
        }
        void Move(int delta)
        {
            (s.Widgets[index], s.Widgets[index + delta]) = (s.Widgets[index + delta], s.Widgets[index]);
            changed();
            Build();
        }
        bool open = expanded.Contains(slot.Kind);
        var values = Small(Ui.Text(T("Customise") + (open ? "  ▴" : "  ▾"), 12, p.Soft, FontWeight.Medium), T("Customise"), true, () =>
        {
            if (!expanded.Remove(slot.Kind)) expanded.Add(slot.Kind);
            Build();
        });
        values.Padding = new Thickness(10, 5);
        // Only offer "Customise" where there is something to choose.
        values.IsVisible = Settings.HasStyles(slot.Kind) || Cards.Values(slot.Kind).Length > 0 || s.ColumnsInUse > 1 || s.Pages > 1;
        // Where the widget lands, so the list reads as the layout without opening every entry.
        var place = new List<string>();
        if (s.Pages > 1) place.Add(s.PageName(Math.Min(slot.Page, s.Pages)));
        if (s.ColumnsInUse > 1 && slot.Column > 0) place.Add(s.Horizontal ? F("Row {0}", Math.Min(slot.Column, s.ColumnsInUse)) : F("Column {0}", Math.Min(slot.Column, s.ColumnsInUse)));
        if (Settings.HasStyles(slot.Kind) && slot.Style != WidgetStyle.Standard) place.Add(T(slot.Style.ToString()));
        var where = Ui.Text(string.Join(" · ", place), 11.5, p.Soft);
        where.VerticalAlignment = VerticalAlignment.Center;
        where.Margin = new Thickness(0, 0, 10, 0);
        where.IsVisible = place.Count > 0 && slot.Visible;
        var row = Ui.Columns("*,Auto,Auto,Auto,Auto", check, where, values,
            Small(Ui.Glyph("M3,10 L8,5 L13,10", p.Soft, 12), T("Move up"), index > 0, () => Move(-1)),
            Small(Ui.Glyph("M3,6 L8,11 L13,6", p.Soft, 12), T("Move down"), index < s.Widgets.Count - 1, () => Move(1)));
        var panel = Ui.Stack(8, row);
        if (open)
        {
            // Style and column first, then the single values.
            var options = new WrapPanel { Margin = new Thickness(28, 0, 0, 0) };
            if (Settings.HasStyles(slot.Kind))
                options.Children.Add(Ui.Row(8, Ui.Text(T("Style"), 12.5, p.Soft), Choice(new[] { T("Standard"), T("Ring"), T("Minimal"), T("Bar"), T("Graph"), T("Tile") }, (int)slot.Style, i => slot.Style = (WidgetStyle)i, rebuild: true, small: true)));
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
        }
        return new Border
        {
            Background = Palette.Brush(p.Dark ? Color.Parse("#1C222B") : Colors.White),
            BorderBrush = Palette.Brush(p.Stroke),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 6, 8, 6),
            Child = panel
        };
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

    private Control Row(string label, Control editor, string? help = null)
    {
        var l = Ui.Text(label, 13.5, p.Ink, FontWeight.Medium);
        var left = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
        left.Children.Add(l);
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
    private Control WideRow(string label, Control editor, string? help = null)
    {
        var panel = new StackPanel { Spacing = 2 };
        panel.Children.Add(Ui.Text(label, 13.5, p.Ink, FontWeight.Medium));
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
