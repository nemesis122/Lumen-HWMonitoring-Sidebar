using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lumen.Core;

public enum Edge { Right, Left }
// Fit scales the cards so they fill the height between the screen top and the taskbar exactly.
public enum Density { Fit, Comfortable, Compact }
public enum ThemeMode { System, Dark, Light }

// The order here is the default order in the sidebar.
public enum WidgetKind { Clock, WorldClocks, Calendar, Weather, Media, Battery, Cpu, Gpu, Fps, Cooling, Power, Memory, Processes, System, Drives, Volumes, Network, Ping, DataUsage, Notes }

// Standard is the full card; Ring is a half-width tile (two sit side by side); Minimal is one slim line.
public enum WidgetStyle { Standard, Ring, Minimal }

public sealed class WidgetSlot
{
    public WidgetKind Kind { get; set; }
    public bool Visible { get; set; } = true;
    public WidgetStyle Style { get; set; }
    // 0 = placed automatically, 1 or 2 = pinned to that column (only used with two columns).
    public int Column { get; set; }
    // Register (tab) the widget is shown on, 1..Pages.
    public int Page { get; set; } = 1;
}

public sealed class Settings
{
    public const string StateColors = "state";
    public const string WindowsAccent = "windows";
    public static readonly string[] Languages = { "de", "en", "fr", "it", "es", "pt" };

    public Edge Edge { get; set; } = Edge.Right;
    public int Screen { get; set; }
    public double Width { get; set; } = 300;
    public Density Density { get; set; } = Density.Fit;
    public ThemeMode Theme { get; set; } = ThemeMode.System;
    public bool Glass { get; set; } = true;
    public bool HighContrast { get; set; }
    public bool ShowNames { get; set; } = true;
    public bool SmoothTransitions { get; set; }
    public double Opacity { get; set; } = .55;
    public double TextScale { get; set; } = 1;
    public string Colors { get; set; } = StateColors;
    public string Language { get; set; } = "auto";
    public bool Topmost { get; set; }
    public bool ReserveSpace { get; set; }
    public bool Fahrenheit { get; set; }
    public int IntervalMs { get; set; } = 1000;
    public double TemperatureWarning { get; set; } = 85;
    public bool WarningPopup { get; set; } = true;
    public bool ShowIntegratedGpu { get; set; } = true;
    public string WeatherPlace { get; set; } = "";
    public double? WeatherLatitude { get; set; }
    public double? WeatherLongitude { get; set; }
    public List<WidgetSlot> Widgets { get; set; } = DefaultWidgets();
    public List<string> HiddenValues { get; set; } = new();
    public int Columns { get; set; } = 1;
    public int Pages { get; set; } = 1;
    // Covers the whole screen up to the taskbar instead of a strip at one edge.
    public bool FullScreen { get; set; }
    public double PricePerKwh { get; set; } = .30;
    public string Currency { get; set; } = "CHF";
    public double BaseWatts { get; set; } = 50;
    public string PingTarget { get; set; } = "1.1.1.1";
    public List<string> WorldClocks { get; set; } = new() { "America/New_York", "Asia/Tokyo" };
    public string Notes { get; set; } = "";
    public bool ClickThrough { get; set; }
    // Own names for fans and temperature probes, keyed "fan:<sensor name>" or "temp:<sensor name>".
    // An entry of "-" hides that reading.
    public Dictionary<string, string> Labels { get; set; } = new();

    // The original hardware set is on by default; everything else is opt-in, so the
    // sidebar never gets crowded by itself.
    public static bool VisibleByDefault(WidgetKind k) => k is WidgetKind.Clock or WidgetKind.Battery or WidgetKind.Cpu or WidgetKind.Gpu
        or WidgetKind.Cooling or WidgetKind.Memory or WidgetKind.Drives or WidgetKind.Network;

    // Kinds whose reading can be shown as a ring or a slim line.
    public static bool HasStyles(WidgetKind k) => k is WidgetKind.Cpu or WidgetKind.Gpu or WidgetKind.Fps or WidgetKind.Memory or WidgetKind.Battery
        or WidgetKind.Cooling or WidgetKind.Network or WidgetKind.Ping or WidgetKind.Power or WidgetKind.Weather or WidgetKind.System or WidgetKind.DataUsage;

    public static List<WidgetSlot> DefaultWidgets() => Enum.GetValues<WidgetKind>().Select(k => new WidgetSlot { Kind = k, Visible = VisibleByDefault(k) }).ToList();

    public bool Shows(string value) => !HiddenValues.Contains(value);

    // The user's own name for a sensor, or null; Hidden(...) when they switched it off.
    public string? Label(string key) => Labels.TryGetValue(key, out var v) && v != "-" ? v : null;
    public bool Hidden(string key) => Labels.TryGetValue(key, out var v) && v == "-";

    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    // Portable first: beside the EXE when writable, otherwise in the user's app-data folder.
    public static string DefaultPath()
    {
        // ProcessPath, not BaseDirectory: a single-file EXE that self-extracts reports its temp folder as the base.
        string folderOfExe = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
        string beside = Path.Combine(folderOfExe, "lumen-settings.json");
        try
        {
            using (File.Open(beside, FileMode.OpenOrCreate, FileAccess.ReadWrite)) { }
            if (new FileInfo(beside).Length == 0) File.Delete(beside);
            return beside;
        }
        catch
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lumen");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "lumen-settings.json");
        }
    }

    public static Settings Load(string path)
    {
        try { return Normalize(JsonSerializer.Deserialize<Settings>(File.ReadAllText(path), Json) ?? new()); }
        catch { return new(); }
    }

    public void Save(string path)
    {
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, Json));
        File.Move(temp, path, true);
    }

    // Keeps every widget exactly once, in the saved order, and clamps numbers to sane ranges.
    // Widgets added in a newer version are inserted at their default position.
    public static Settings Normalize(Settings s)
    {
        s.Width = double.IsFinite(s.Width) ? Math.Clamp(s.Width, 240, 440) : 300;
        s.IntervalMs = Math.Clamp(s.IntervalMs, 500, 5000);
        s.TemperatureWarning = double.IsFinite(s.TemperatureWarning) ? Math.Clamp(s.TemperatureWarning, 50, 110) : 85;
        s.Opacity = double.IsFinite(s.Opacity) ? Math.Clamp(s.Opacity, .2, 1) : .55;
        s.TextScale = double.IsFinite(s.TextScale) ? Math.Clamp(s.TextScale, .85, 1.3) : 1;
        s.Screen = Math.Max(0, s.Screen);
        s.Colors = string.IsNullOrWhiteSpace(s.Colors) ? StateColors : s.Colors.Trim();
        if (s.Colors is not (StateColors or WindowsAccent) && !IsHexColor(s.Colors)) s.Colors = StateColors;
        s.Language = Languages.Contains(s.Language) ? s.Language : "auto";
        s.WeatherPlace ??= "";
        s.Columns = Math.Clamp(s.Columns, 1, 6);
        s.Pages = Math.Clamp(s.Pages, 1, 6);
        s.PricePerKwh = double.IsFinite(s.PricePerKwh) ? Math.Clamp(s.PricePerKwh, 0, 5) : .30;
        s.BaseWatts = double.IsFinite(s.BaseWatts) ? Math.Clamp(s.BaseWatts, 0, 500) : 50;
        s.Currency = string.IsNullOrWhiteSpace(s.Currency) ? "CHF" : s.Currency.Trim()[..Math.Min(5, s.Currency.Trim().Length)];
        s.PingTarget = string.IsNullOrWhiteSpace(s.PingTarget) ? "1.1.1.1" : s.PingTarget.Trim();
        s.WorldClocks = (s.WorldClocks ?? new()).Where(z => !string.IsNullOrWhiteSpace(z)).Distinct().Take(4).ToList();
        s.Notes ??= "";
        // Own sensor names are no longer offered; drop any saved by 1.5.0 so nothing stays renamed or hidden.
        s.Labels = new();
        s.HiddenValues = (s.HiddenValues ?? new()).Where(v => !string.IsNullOrWhiteSpace(v)).Distinct().ToList();
        var seen = new HashSet<WidgetKind>();
        s.Widgets = (s.Widgets ?? new()).Where(w => w != null && Enum.IsDefined(w.Kind) && seen.Add(w.Kind)).ToList();
        foreach (var w in s.Widgets)
        {
            w.Column = Math.Clamp(w.Column, 0, 6);
            w.Page = Math.Clamp(w.Page, 1, 6);
            if (!Enum.IsDefined(w.Style) || !HasStyles(w.Kind)) w.Style = WidgetStyle.Standard;
        }
        foreach (var kind in Enum.GetValues<WidgetKind>())
        {
            if (!seen.Add(kind)) continue;
            // Place it after the nearest earlier kind that is already in the list.
            int at = 0;
            for (int i = 0; i < s.Widgets.Count; i++) if (s.Widgets[i].Kind < kind) at = i + 1;
            s.Widgets.Insert(at, new WidgetSlot { Kind = kind, Visible = VisibleByDefault(kind) });
        }
        return s;
    }

    // Rough card heights, so automatic placement balances the columns by height, not by count.
    public static double Weight(WidgetSlot w) => (w.Style == WidgetStyle.Ring ? .5 : w.Style == WidgetStyle.Minimal ? .35 : 1) * w.Kind switch
    {
        WidgetKind.Cpu or WidgetKind.Gpu => 2,
        WidgetKind.Cooling or WidgetKind.Network or WidgetKind.Calendar => 1.7,
        WidgetKind.Weather or WidgetKind.Processes => 1.4,
        WidgetKind.Clock or WidgetKind.Drives => 1.2,
        WidgetKind.WorldClocks => .8,
        _ => 1.05
    };

    // Which column each visible widget of a page goes in. Pinned widgets stay in their column;
    // the free ones are spread so the tallest column is as short as possible (exactly for two
    // columns, by largest-first otherwise). Within a column the widgets keep their order.
    // weight: measured card heights once the sidebar has been laid out; the estimate before that.
    public List<List<WidgetSlot>> Layout(Func<WidgetSlot, double>? weight = null, int page = 1)
    {
        weight ??= Weight;
        var visible = Widgets.Where(w => w.Visible && Math.Min(w.Page, Pages) == page).ToList();
        int n = Columns;
        if (n < 2) return new() { visible };
        int Pinned(WidgetSlot w) => w.Column > 0 ? Math.Min(w.Column, n) - 1 : -1;
        var height = new double[n];
        foreach (var w in visible.Where(w => Pinned(w) >= 0)) height[Pinned(w)] += weight(w);
        var free = visible.Where(w => Pinned(w) < 0).ToList();
        var place = new Dictionary<WidgetSlot, int>();
        if (n == 2 && free.Count is > 0 and <= 16)
        {
            int best = 0; double bestMax = double.MaxValue;
            var weights = free.Select(weight).ToArray();
            for (int mask = 0; mask < 1 << free.Count; mask++)
            {
                double a = height[0], b = height[1];
                for (int i = 0; i < free.Count; i++) if ((mask & (1 << i)) == 0) a += weights[i]; else b += weights[i];
                double worst = Math.Max(a, b);
                if (worst < bestMax - .5) { bestMax = worst; best = mask; }
            }
            for (int i = 0; i < free.Count; i++) place[free[i]] = (best & (1 << i)) == 0 ? 0 : 1;
        }
        else
        {
            foreach (var w in free.OrderByDescending(weight))
            {
                int c = Array.IndexOf(height, height.Min());
                place[w] = c;
                height[c] += weight(w);
            }
        }
        var cols = Enumerable.Range(0, n).Select(_ => new List<WidgetSlot>()).ToList();
        foreach (var w in visible) cols[Pinned(w) >= 0 ? Pinned(w) : place[w]].Add(w);
        // Without pinned columns, read left to right: the column holding the first widget comes first.
        if (!visible.Any(w => Pinned(w) >= 0))
            cols = cols.OrderBy(c => c.Count == 0 ? int.MaxValue : visible.IndexOf(c[0])).ToList();
        return cols;
    }

    public static bool IsHexColor(string v) => v.Length == 7 && v[0] == '#' && v[1..].All(Uri.IsHexDigit);
}
