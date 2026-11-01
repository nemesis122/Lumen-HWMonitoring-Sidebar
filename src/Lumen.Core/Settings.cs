using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lumen.Core;

public enum Edge { Right, Left, Top, Bottom }
// Fit scales the cards so they fill the height between the screen top and the taskbar exactly.
public enum Density { Fit, Comfortable, Compact }
public enum ThemeMode { System, Dark, Light }

// The order here is the default order in the sidebar.
public enum WidgetKind { Clock, WorldClocks, Calendar, Weather, Media, Battery, Cpu, Gpu, Fps, Cooling, Power, Memory, Processes, System, Drives, Volumes, Network, Ping, DataUsage, Notes }

// Standard is the full card; Ring is a half-width tile (two sit side by side); Minimal is one slim line.
// Ring and Tile are half width: two of them share a line.
public enum WidgetStyle { Standard, Ring, Minimal, Bar, Graph, Tile, Compact, Status, Gauge, Segments, DualRing }

public sealed class WidgetSlot
{
    public WidgetKind Kind { get; set; }
    public bool Visible { get; set; } = true;
    public WidgetStyle Style { get; set; }
    // 0 = placed automatically, 1 or 2 = pinned to that column (only used with two columns).
    public int Column { get; set; }
    // Register (tab) the widget is shown on, 1..Pages.
    public int Page { get; set; } = 1;
    // Own title and hardware name; empty keeps the built-in one. Either can be hidden.
    public string Name { get; set; } = "";
    public string HardwareName { get; set; } = "";
    public bool ShowTitle { get; set; } = true;
    public bool ShowHardware { get; set; } = true;
}

public sealed class Settings
{
    public const string StateColors = "state";
    public const string WindowsAccent = "windows";
    public static readonly string[] Languages = { "de", "en", "fr", "it", "es", "pt" };

    public Edge Edge { get; set; } = Edge.Right;
    // Top or bottom: a bar across the whole screen width; the widgets sit side by side.
    public bool Horizontal => Edge is Edge.Top or Edge.Bottom;
    public double BarHeight { get; set; } = 300;
    // The bar spans the whole screen width; off, it is as wide as its widgets and centred.
    public bool BarFullWidth { get; set; } = true;
    // At the side edges: the number of columns. In the bar at the top or bottom the same
    // setting is the number of rows, one below the other, each BarHeight tall.
    public int ColumnsInUse => Columns;
    public int Rows => Horizontal ? Columns : 1;
    public int Screen { get; set; }
    public double Width { get; set; } = 300;
    public Density Density { get; set; } = Density.Fit;
    public ThemeMode Theme { get; set; } = ThemeMode.Dark;
    public bool Glass { get; set; } = true;
    public bool HighContrast { get; set; } = true;
    public bool ShowNames { get; set; } = true;
    public bool SmoothTransitions { get; set; }
    public double Opacity { get; set; } = .55;
    public double TextScale { get; set; } = 1;
    public string Colors { get; set; } = StateColors;
    // Tone of the sidebar and card backgrounds: graphite, black, midnight, ocean, forest, aubergine, wine, mocha, slate.
    public string Background { get; set; } = "graphite";
    public string Language { get; set; } = "auto";
    public bool Topmost { get; set; }
    public bool ReserveSpace { get; set; }
    public bool Fahrenheit { get; set; }
    public int IntervalMs { get; set; } = 1000;
    public double TemperatureWarning { get; set; } = 85;
    public bool WarningPopup { get; set; } = true;
    public bool ShowIntegratedGpu { get; set; } = true;
    // Built-in libraries, or AIDA64 / HWiNFO first where they run (see SensorSource).
    public SensorSource SensorSource { get; set; } = SensorSource.Builtin;
    public string WeatherPlace { get; set; } = "";
    public double? WeatherLatitude { get; set; }
    public double? WeatherLongitude { get; set; }
    public List<WidgetSlot> Widgets { get; set; } = DefaultWidgets();
    public List<string> HiddenValues { get; set; } = new();
    public int Columns { get; set; } = 1;
    public int Pages { get; set; } = 1;
    // Own names for the registers ("Gaming", "System" …); an empty name shows the number.
    public List<string> PageNames { get; set; } = new();
    public string PageName(int page) => page >= 1 && page <= PageNames.Count && PageNames[page - 1].Length > 0 ? PageNames[page - 1] : page.ToString();
    public bool HasPageNames => PageNames.Take(Pages).Any(n => n.Length > 0);
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
        or WidgetKind.Cooling or WidgetKind.Network or WidgetKind.Ping or WidgetKind.Power or WidgetKind.Weather or WidgetKind.System or WidgetKind.DataUsage
        or WidgetKind.Drives or WidgetKind.Volumes
        or WidgetKind.Clock or WidgetKind.WorldClocks or WidgetKind.Calendar or WidgetKind.Media or WidgetKind.Processes;

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
        s.TextScale = double.IsFinite(s.TextScale) ? Math.Clamp(s.TextScale, .5, 2) : 1;
        s.Screen = Math.Max(0, s.Screen);
        s.Colors = string.IsNullOrWhiteSpace(s.Colors) ? StateColors : s.Colors.Trim();
        s.Background = string.IsNullOrWhiteSpace(s.Background) ? "graphite" : s.Background.Trim().ToLowerInvariant();
        if (s.Colors is not (StateColors or WindowsAccent) && !IsHexColor(s.Colors)) s.Colors = StateColors;
        s.Language = Languages.Contains(s.Language) ? s.Language : "auto";
        s.WeatherPlace ??= "";
        s.Columns = Math.Clamp(s.Columns, 1, 6);
        s.Pages = Math.Clamp(s.Pages, 1, 6);
        if (!Enum.IsDefined(s.Edge)) s.Edge = Edge.Right;
        if (!Enum.IsDefined(s.SensorSource)) s.SensorSource = SensorSource.Builtin;
        // Down to 40 px: the Status and Compact styles fit a slim bar.
        s.BarHeight = double.IsFinite(s.BarHeight) ? Math.Clamp(s.BarHeight, 40, 700) : 300;
        s.PageNames = Enumerable.Range(0, 6).Select(i => (s.PageNames ?? new()).ElementAtOrDefault(i)?.Trim() ?? "")
            .Select(n => n.Length > 16 ? n[..16] : n).ToList();
        s.PricePerKwh = double.IsFinite(s.PricePerKwh) ? Math.Clamp(s.PricePerKwh, 0, 5) : .30;
        s.BaseWatts = double.IsFinite(s.BaseWatts) ? Math.Clamp(s.BaseWatts, 0, 500) : 50;
        s.Currency = string.IsNullOrWhiteSpace(s.Currency) ? "CHF" : s.Currency.Trim()[..Math.Min(5, s.Currency.Trim().Length)];
        s.PingTarget = string.IsNullOrWhiteSpace(s.PingTarget) ? "1.1.1.1" : s.PingTarget.Trim();
        s.WorldClocks = (s.WorldClocks ?? new()).Where(z => !string.IsNullOrWhiteSpace(z)).Distinct().Take(4).ToList();
        s.Notes ??= "";
        // Own sensor names are no longer offered; drop any saved by 1.5.0 so nothing stays renamed or hidden.
        s.Labels = new();
        s.HiddenValues = (s.HiddenValues ?? new()).Where(v => !string.IsNullOrWhiteSpace(v))
            // Drives are "/disk/<number>" since 2.1, whichever library found them; hidden ones stay hidden.
            .Select(v => System.Text.RegularExpressions.Regex.Replace(v, @"^drive:.*?/(nvme|hdd|ssd|ata|storage|scsi|usb)[^/]*/(\d+)$", "drive:/disk/$2"))
            .Distinct().ToList();
        var seen = new HashSet<WidgetKind>();
        s.Widgets = (s.Widgets ?? new()).Where(w => w != null && Enum.IsDefined(w.Kind) && seen.Add(w.Kind)).ToList();
        foreach (var w in s.Widgets)
        {
            w.Column = Math.Clamp(w.Column, 0, 6);
            w.Page = Math.Clamp(w.Page, 1, 6);
            w.Name = (w.Name ?? "").Trim();
            w.HardwareName = (w.HardwareName ?? "").Trim();
            if (w.Name.Length > 40) w.Name = w.Name[..40];
            if (w.HardwareName.Length > 60) w.HardwareName = w.HardwareName[..60];
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
    public static double Weight(WidgetSlot w) => w.Style switch
    {
        WidgetStyle.Ring or WidgetStyle.Tile => .5,
        WidgetStyle.Minimal => .35,
        WidgetStyle.Bar => .45,
        WidgetStyle.Graph => .7,
        WidgetStyle.Compact => .32,
        WidgetStyle.Status => .18,
        WidgetStyle.Gauge => .55,
        WidgetStyle.Segments => .55,
        WidgetStyle.DualRing => .5,
        _ => 1
    } * w.Kind switch
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
    // cards: how many cards a widget makes (each graphics adapter is one), so bar rows come out even.
    public List<List<WidgetSlot>> Layout(Func<WidgetSlot, double>? weight = null, int page = 1, Func<WidgetSlot, int>? cards = null)
    {
        weight ??= Weight;
        var visible = Widgets.Where(w => w.Visible && Math.Min(w.Page, Pages) == page).ToList();
        if (Horizontal) return BarRows(visible, cards ?? (_ => 1));
        int n = ColumnsInUse;
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

    public static bool IsHalf(WidgetSlot w) => w.Style is WidgetStyle.Ring or WidgetStyle.Tile or WidgetStyle.DualRing && HasStyles(w.Kind);

    // Columns the sidebar needs: as set, or for a bar the most any register holds.
    // Columns of cards: as set at the side; in the bar, the most cards any row holds.
    public int ColumnCount => Horizontal ? Math.Max(1, Enumerable.Range(1, Pages).SelectMany(pg => Layout(page: pg)).Select(Units).DefaultIfEmpty(1).Max()) : Math.Max(1, Columns);

    // Cards side by side in one bar row: two half-width tiles in a row share one place.
    public static int Units(List<WidgetSlot> row)
    {
        int n = 0;
        for (int i = 0; i < row.Count; i++) { n++; if (IsHalf(row[i]) && i + 1 < row.Count && IsHalf(row[i + 1])) i++; }
        return n;
    }

    // Rows of the bar, top to bottom, each read left to right. Pinned widgets stay in their row
    // (the widget's "Column" number); the others fill the rows in order, about equally many per row.
    private List<List<WidgetSlot>> BarRows(List<WidgetSlot> visible, Func<WidgetSlot, int> cards)
    {
        int n = Math.Max(1, Columns);
        int Pinned(WidgetSlot w) => w.Column > 0 ? Math.Min(w.Column, n) - 1 : -1;
        // Units: a widget, or two half-width tiles that share a place.
        var units = new List<List<WidgetSlot>>();
        for (int i = 0; i < visible.Count; i++)
        {
            if (IsHalf(visible[i]) && i + 1 < visible.Count && IsHalf(visible[i + 1]) && Pinned(visible[i]) == Pinned(visible[i + 1]))
            { units.Add(new() { visible[i], visible[i + 1] }); i++; }
            else units.Add(new() { visible[i] });
        }
        var rows = Enumerable.Range(0, n).Select(_ => new List<List<WidgetSlot>>()).ToList();
        foreach (var u in units.Where(u => Pinned(u[0]) >= 0)) rows[Pinned(u[0])].Add(u);
        // Even rows: counted in cards (a widget with two graphics adapters is two), the rest of the
        // division going to the first rows – 20 cards in 3 rows become 7, 7, 6.
        int Size(List<WidgetSlot> u) => u.Count == 2 ? 1 : Math.Max(1, cards(u[0]));
        int total = units.Sum(Size);
        var target = Enumerable.Range(0, n).Select(i => total / n + (i < total % n ? 1 : 0)).ToArray();
        var filled = rows.Select(row => row.Sum(Size)).ToArray();
        int r = 0;
        foreach (var u in units.Where(u => Pinned(u[0]) < 0))
        {
            while (r < n - 1 && filled[r] + Size(u) > target[r] && filled[r] > 0) r++;
            rows[r].Add(u);
            filled[r] += Size(u);
        }
        // Keep the original order inside each row.
        return rows.Select(row => row.OrderBy(u => visible.IndexOf(u[0])).SelectMany(u => u).ToList()).ToList();
    }

    public static bool IsHexColor(string v) => v.Length == 7 && v[0] == '#' && v[1..].All(Uri.IsHexDigit);
}
