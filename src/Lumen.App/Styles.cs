using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;
using static Lumen.App.L;

namespace Lumen.App;

// The one reading a widget boils down to, for the Ring and Minimal styles.
public sealed record Summary(string Glyph, string Title, string Value, string Unit, Color Color, double Fraction, string Caption, double[] Series, double? SeriesMax)
{
    // The hardware's model name, shortened to fit a tile: "Ryzen 9 5900X", "RTX 3080".
    public string Name { get; init; } = "";

    public static readonly Summary Empty = new(Glyphs.Cpu, "", Format.Missing, "", Colors.Gray, 0, "", Array.Empty<double>(), null);
}

public static class Names
{
    // Full names without trademark clutter: "Intel(R) Ethernet Server Adapter X520-2" → "Intel Ethernet X520-2".
    public static string Clean(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        string n = System.Text.RegularExpressions.Regex.Replace(name, @"\((R|TM|tm|r)\)|®|™", "");
        n = System.Text.RegularExpressions.Regex.Replace(n, @"\s+\d+-Core Processor$|\s+Processor$|\s+CPU\s*@.*$", "");
        n = n.Replace("Ethernet Server Adapter", "Ethernet").Replace("Ethernet Controller", "Ethernet").Replace("Ethernet Connection", "Ethernet");
        return System.Text.RegularExpressions.Regex.Replace(n, @"\s{2,}", " ").Trim();
    }

    // "Fan #2" → "Fan 2", "System Fan #1" → "System 1", "Chipset Fan" → "Chipset", "AIO Pump" → "AIO Pump".
    public static string Fan(string raw, Settings s)
    {
        if (s.Label("fan:" + raw) is string own) return own;
        var (word, number) = SensorPicker.FanName(raw);
        string w = L.T(word);
        return number.Length > 0 ? w + " " + number : w;
    }

    public static string Temp(string raw, Settings s) => s.Label("temp:" + raw) ?? L.T(raw);

    // Drops maker names, trademarks and clock suffixes; the model is what tells parts apart.
    public static string Short(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        string n = System.Text.RegularExpressions.Regex.Replace(name, @"\((R|TM|tm|r)\)|®|™", "");
        n = System.Text.RegularExpressions.Regex.Replace(n, @"\s+(CPU\s*)?@.*$|\s+\d+-Core Processor$|\s+Processor$", "");
        foreach (var prefix in new[] { "AMD ", "NVIDIA GeForce ", "NVIDIA ", "Intel Core ", "Intel ", "ASUS ", "MSI ", "Gigabyte ", "ASRock " })
            if (n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) { n = n[prefix.Length..]; break; }
        n = System.Text.RegularExpressions.Regex.Replace(n, @"\s{2,}", " ").Trim();
        return n.Replace("Ethernet Server Adapter", "Ethernet").Replace("Ethernet Controller", "Ethernet");
    }
}

public static class Summaries
{
    public static Summary For(WidgetKind kind, int index, Snapshot s, History h, Settings set, Palette p)
    {
        string T2(double? c) => Format.Temperature(c, set.Fahrenheit);
        string U = Format.TemperatureUnit(set.Fahrenheit);
        Color Heat(double? c) => c is not double v || !double.IsFinite(v) ? p.Faint : v >= set.TemperatureWarning ? p.Ramp[^1] : p.At(Format.Heat(v));
        switch (kind)
        {
            case WidgetKind.Cpu:
            {
                var c = s.Cpu;
                return new(Glyphs.Cpu, T("Processor"), T2(c?.Temperature), U, Heat(c?.Temperature), Format.Heat(c?.Temperature),
                    $"{T("Load")} {Format.Percent(c?.Load)}", h.Read("cpu.load"), 100) { Name = Names.Short(c?.Name) };
            }
            case WidgetKind.Gpu:
            {
                var g = GpuGroup.Visible(s, set).ElementAtOrDefault(index);
                string title = g?.Integrated == true ? T("Integrated") : T("Graphics");
                return new(Glyphs.Gpu, title, T2(g?.Temperature), U, Heat(g?.Temperature), Format.Heat(g?.Temperature),
                    $"{T("Load")} {Format.Percent(g?.Load)}", g is null ? Array.Empty<double>() : h.Read($"gpu.{g.Id}.load"), 100) { Name = Names.Short(g?.Name) };
            }
            case WidgetKind.Fps:
            {
                var f = s.Fps;
                return new(Glyphs.Fps, "FPS", Format.Number(f?.Fps), "fps", FpsCard.Color(f?.Fps, p), Math.Clamp((f?.Fps ?? 0) / 144, 0, 1),
                    f is null ? T("No game in the foreground.") : f.App, h.Read("fps"), null);
            }
            case WidgetKind.Memory:
            {
                var m = s.Memory;
                return new(Glyphs.Memory, T("Memory"), Format.Number(m?.Load), "%", p.At(Format.Share(m?.Load) * .9), Format.Share(m?.Load),
                    m is null ? "" : $"{Format.Number(m.UsedGb, "0.0")} / {Format.Gigabytes(m.TotalGb)}", h.Read("mem.load"), 100);
            }
            case WidgetKind.Battery:
            {
                var b = s.Battery;
                var color = b is null || b.Charging || b.OnAc ? p.Calm : p.At(Math.Clamp((60 - b.Percent) / 50, 0, 1) * .5 + (b.Percent < 15 ? .5 : 0));
                return new(Glyphs.Battery, T("Battery"), Format.Number(b?.Percent), "%", color, (b?.Percent ?? 0) / 100,
                    b is null ? "" : b.Charging ? T("Charging") : b.OnAc ? T("Plugged in") : T("On battery"), Array.Empty<double>(), null);
            }
            case WidgetKind.Cooling:
            {
                var hot = s.BoardTemps.OrderByDescending(t => t.Celsius).FirstOrDefault();
                return new(Glyphs.Fan, T("Cooling"), T2(hot?.Celsius), U, Heat(hot?.Celsius + 5), Format.Heat(hot?.Celsius + 5),
                    s.Fans.Count == 0 ? "" : $"{T("Fans")} {s.Fans.Count(f => f.Rpm > 0)}/{s.Fans.Count}", Array.Empty<double>(), null) { Name = Names.Short(s.BoardName) };
            }
            case WidgetKind.Network:
            {
                var n = s.Network;
                var (num, unit) = Format.Split(Format.Rate(n?.DownloadBytesPerSecond));
                double share = n?.LinkBitsPerSecond is double link && link > 0 ? (n.DownloadBytesPerSecond ?? 0) * 8 / link : 0;
                return new(Glyphs.Network, T("Network"), num, unit, p.Cool, Math.Sqrt(Math.Clamp(share, 0, 1)),
                    "↑ " + Format.Rate(n?.UploadBytesPerSecond), h.Read("net.down"), null) { Name = Names.Short(n?.Name) };
            }
            case WidgetKind.Ping:
            {
                var pi = s.Ping;
                var (num, unit) = Format.Split(Format.Milliseconds(pi?.Ms));
                return new(Glyphs.Ping, "Ping", num, unit, PingColor(pi?.Ms, p), Math.Clamp((pi?.Ms ?? 0) / 150, 0, 1),
                    pi is null ? "" : $"{T("Jitter")} {Format.Milliseconds(pi.JitterMs)}", h.Read("ping"), null);
            }
            case WidgetKind.Power:
            {
                var u = s.Usage;
                return new(Glyphs.Power, T("Power"), Format.Number(u?.WattsNow), "W", p.At(Math.Clamp((u?.WattsNow ?? 0) / 600, 0, 1)), Math.Clamp((u?.WattsNow ?? 0) / 600, 0, 1),
                    u is null ? "" : $"{T("Today")} {Format.Number(u.KwhToday, "0.00")} kWh", h.Read("power"), null);
            }
            case WidgetKind.Weather:
            {
                var w = s.Weather;
                return new(Glyphs.Cloud, w?.Place ?? T("Weather"), T2(w?.TemperatureC), U, p.At(Math.Clamp(((w?.TemperatureC ?? 0) + 10) / 45, 0, 1)), Math.Clamp(((w?.TemperatureC ?? 0) + 10) / 45, 0, 1),
                    w is null ? T("Set a place in the settings.") : T(WeatherClient.Group(w.Code)), Array.Empty<double>(), null);
            }
            case WidgetKind.System:
            {
                var y = s.System;
                var (num, unit) = y is null ? (Format.Missing, "") : SplitDuration(y.Uptime);
                return new(Glyphs.System, T("Uptime"), num, unit, p.Ink, Math.Clamp((y?.Uptime.TotalDays ?? 0) / 7, 0, 1),
                    y?.Os ?? "", Array.Empty<double>(), null);
            }
            case WidgetKind.Drives:
            {
                // The hottest drive leads; the caption counts them.
                var shown = s.Disks.Where(d => set.Shows("drive:" + d.Id)).ToList();
                var hot = shown.Where(d => d.Temperature != null).OrderByDescending(d => d.Temperature).FirstOrDefault();
                string count = $"{shown.Count} {(shown.Count == 1 ? T("drive") : T("drives"))}";
                return new(Glyphs.Drive, T("Drives"), T2(hot?.Temperature), U, Heat(hot?.Temperature), Format.Heat(hot?.Temperature),
                    count, Array.Empty<double>(), null);
            }
            case WidgetKind.DataUsage:
            {
                var u = s.Usage;
                var (num, unit) = Format.Split(Format.Size(u?.DownToday));
                return new(Glyphs.Data, T("Data today"), num, unit, p.Cool, 0, u is null ? "" : "↑ " + Format.Size(u.UpToday), Array.Empty<double>(), null);
            }
        }
        return Summary.Empty;
    }

    // The history chart a widget can show in the Graph and Minimal styles, by its value key.
    public static string? ChartKey(WidgetKind kind) => kind switch
    {
        WidgetKind.Cpu => "cpu.chart", WidgetKind.Gpu => "gpu.chart", WidgetKind.Network => "net.chart",
        WidgetKind.Fps => "fps.chart", WidgetKind.Ping => "ping.chart", WidgetKind.Power => "power.chart",
        _ => null
    };

    // The single values the compact styles can show as small figures, keyed like the Standard card's.
    public static readonly Dictionary<WidgetKind, string[]> ExtraKeys = new()
    {
        [WidgetKind.Cpu] = new[] { "cpu.power", "cpu.voltage", "cpu.fan" },
        [WidgetKind.Gpu] = new[] { "gpu.vram", "gpu.power", "gpu.hotspot", "gpu.fan" },
        [WidgetKind.Memory] = new[] { "mem.free", "mem.total", "mem.peak" },
        [WidgetKind.Battery] = new[] { "battery.time", "battery.rate", "battery.health" },
        [WidgetKind.Cooling] = new[] { "cooling.fans", "cooling.board" },
        [WidgetKind.Network] = new[] { "net.link", "net.use", "net.peak", "net.address" },
        [WidgetKind.Drives] = new[] { "drives.temps", "drives.io" },
        [WidgetKind.Fps] = new[] { "fps.frametime" },
        [WidgetKind.Ping] = new[] { "ping.jitter", "ping.loss", "ping.target" },
        [WidgetKind.Power] = new[] { "power.cost" },
        [WidgetKind.Weather] = new[] { "weather.feels", "weather.wind", "weather.humidity" },
        [WidgetKind.System] = new[] { "system.boot", "system.processes", "system.threads" },
        [WidgetKind.DataUsage] = new[] { "data.week", "data.month" },
    };

    // Which of a widget's values a style can show: all of them in Standard; in the others the
    // small figures, plus the history chart in Graph and Minimal.
    public static bool Supports(WidgetKind kind, WidgetStyle style, string key) =>
        // The Standard drives card always lists every drive; this switch is for the compact styles.
        style == WidgetStyle.Standard || !Settings.HasStyles(kind) ? key != "drives.temps"
        : ExtraKeys.TryGetValue(kind, out var keys) && keys.Contains(key)
        || style is WidgetStyle.Graph or WidgetStyle.Minimal && key == ChartKey(kind);

    // The small figures for the compact styles: (label, value), only those the user keeps switched on.
    public static List<(string Label, string Value)> Extras(WidgetKind kind, int index, Snapshot s, History h, Settings set)
    {
        var list = new List<(string, string)>();
        void Add(string key, string label, string value) { if (set.Shows(key)) list.Add((label, value)); }
        string Tc(double? c) => c is null ? Format.Missing : Format.Temperature(c, set.Fahrenheit) + " " + Format.TemperatureUnit(set.Fahrenheit);
        double? Peak(string key) { var v = h.Read(key); return v.Length > 0 ? v.Max() : null; }
        switch (kind)
        {
            case WidgetKind.Cpu:
                Add("cpu.power", T("Power"), Format.Watts(s.Cpu?.PowerW));
                Add("cpu.voltage", T("Voltage"), Format.Volts(s.Cpu?.Voltage));
                Add("cpu.fan", T("Fan"), Format.Rpm(s.Cpu?.FanRpm));
                break;
            case WidgetKind.Gpu:
            {
                var g = GpuGroup.Visible(s, set).ElementAtOrDefault(index);
                Add("gpu.vram", "VRAM", g?.MemoryUsedMb is double u && g.MemoryTotalMb is double t && t > 0 ? $"{Format.Number(u / 1024, "0.0")}/{Format.Number(t / 1024, "0.#")} GB" : Format.Missing);
                Add("gpu.power", T("Power"), Format.Watts(g?.PowerW));
                Add("gpu.hotspot", T("Hot spot"), Tc(g?.HotSpot));
                Add("gpu.fan", T("Fan"), Format.Rpm(g?.FanRpm));
                break;
            }
            case WidgetKind.Memory:
                Add("mem.free", T("Available"), Format.Gigabytes(s.Memory?.AvailableGb));
                Add("mem.total", T("Installed"), Format.Gigabytes(s.Memory?.TotalGb));
                Add("mem.peak", T("2-min peak"), Format.Percent(Peak("mem.load")));
                break;
            case WidgetKind.Battery:
                Add("battery.time", T("Time left"), s.Battery?.MinutesLeft is double min ? Format.Duration(TimeSpan.FromMinutes(min)) : Format.Missing);
                Add("battery.rate", T("Rate"), Format.Watts(s.Battery?.PowerW));
                Add("battery.health", T("Health"), s.Battery?.WearPercent is double w ? Format.Percent(100 - w) : Format.Missing);
                break;
            case WidgetKind.Cooling:
                Add("cooling.fans", T("Fans"), s.Fans.Count == 0 ? Format.Missing : $"{s.Fans.Count(f => f.Rpm > 0)}/{s.Fans.Count}");
                if (set.Shows("cooling.board")) foreach (var t in s.BoardTemps) list.Add((Names.Temp(t.Name, set), Tc(t.Celsius)));
                break;
            case WidgetKind.Network:
            {
                var n = s.Network;
                Add("net.link", T("Link speed"), Format.Link(n?.LinkBitsPerSecond));
                Add("net.use", T("Link use"), Format.Bits(n?.DownloadBytesPerSecond * 8));
                Add("net.peak", T("2-min peak"), Format.Rate(Peak("net.down")));
                Add("net.address", T("Address"), n?.Address ?? Format.Missing);
                break;
            }
            case WidgetKind.Drives:
                // One figure per drive, by its letters (or short name), then the combined rates.
                if (set.Shows("drives.temps"))
                    foreach (var d in s.Disks.Where(d => set.Shows("drive:" + d.Id)))
                        list.Add((d.Letters.Length > 0 ? d.Letters : Names.Short(d.Name), Tc(d.Temperature)));
                Add("drives.io", T("Read"), Format.Rate(s.Disks.Where(d => set.Shows("drive:" + d.Id)).Sum(d => d.ReadBytesPerSecond ?? 0)));
                Add("drives.io", T("Write"), Format.Rate(s.Disks.Where(d => set.Shows("drive:" + d.Id)).Sum(d => d.WriteBytesPerSecond ?? 0)));
                break;
            case WidgetKind.Fps:
                Add("fps.frametime", T("Frame time"), s.Fps is { } f ? Format.Milliseconds(f.FrameTimeMs) : Format.Missing);
                break;
            case WidgetKind.Ping:
                Add("ping.jitter", T("Jitter"), Format.Milliseconds(s.Ping?.JitterMs));
                Add("ping.loss", T("Loss"), s.Ping is { } pi ? Format.Percent(pi.LossPercent) : Format.Missing);
                Add("ping.target", T("Target"), s.Ping?.Target ?? set.PingTarget);
                break;
            case WidgetKind.Power:
                Add("power.cost", T("Today"), s.Usage is { } us ? Format.Money(us.KwhToday * set.PricePerKwh, set.Currency) : Format.Missing);
                break;
            case WidgetKind.Weather:
                Add("weather.feels", T("Feels like"), Tc(s.Weather?.FeelsLikeC));
                Add("weather.wind", T("Wind"), s.Weather?.WindKmh is double wk ? Format.Number(wk) + " km/h" : Format.Missing);
                Add("weather.humidity", T("Humidity"), Format.Percent(s.Weather?.Humidity));
                break;
            case WidgetKind.System:
                Add("system.boot", T("Started"), s.System is { } sy ? sy.BootTime.ToString("ddd HH:mm", L.Culture) : Format.Missing);
                Add("system.processes", T("Processes"), s.System is { } sp ? sp.Processes.ToString(L.Culture) : Format.Missing);
                Add("system.threads", T("Threads"), s.System is { } st ? st.Threads.ToString(L.Culture) : Format.Missing);
                break;
            case WidgetKind.DataUsage:
                Add("data.week", T("This week"), s.Usage is { } dw ? Format.Size(dw.DownWeek) : Format.Missing);
                Add("data.month", T("This month"), s.Usage is { } dm ? Format.Size(dm.DownMonth) : Format.Missing);
                break;
        }
        return list;
    }

    // "2 d 3 h" → ("2", "d 3 h"); "3 h 12 m" → ("3", "h 12 m").
    private static (string, string) SplitDuration(TimeSpan t)
    {
        string d = Format.Duration(t);
        int space = d.IndexOf(' ');
        return space < 0 ? (d, "") : (d[..space], d[(space + 1)..]);
    }

    public static Color PingColor(double? ms, Palette p) => ms is not double v ? p.Faint : p.At(Math.Clamp(v / 120, 0, 1) * .95);
}

// A 270° gauge: grey track, coloured arc for the fraction, rounded ends.
public sealed class RingGauge : Control
{
    private bool started;

    public void Animate(double fraction, Color color)
    {
        Color = color;
        double from = started ? Fraction : fraction;
        started = true;
        if (!double.IsFinite(fraction)) fraction = 0;
        Motion.Run(this, Motion.Duration, t => { Fraction = from + (fraction - from) * t; InvalidateVisual(); });
    }

    public double Fraction { get; set; }
    public Color Color { get; set; }
    public Palette? Palette { get; set; }
    public double Thickness { get; set; } = 7;

    public override void Render(DrawingContext ctx)
    {
        if (Palette is null) return;
        double size = Math.Min(Bounds.Width, Bounds.Height), r = (size - Thickness) / 2;
        var c = new Point(Bounds.Width / 2, Bounds.Height / 2);
        Arc(ctx, c, r, 135, 270, new Pen(new SolidColorBrush(Palette.Track), Thickness, lineCap: PenLineCap.Round));
        double f = double.IsFinite(Fraction) ? Math.Clamp(Fraction, 0, 1) : 0;
        if (f > .005) Arc(ctx, c, r, 135, 270 * f, new Pen(new SolidColorBrush(Color), Thickness, lineCap: PenLineCap.Round));
    }

    private static void Arc(DrawingContext ctx, Point c, double r, double start, double sweep, Pen pen)
    {
        Point At(double deg) => new(c.X + r * Math.Cos(deg * Math.PI / 180), c.Y + r * Math.Sin(deg * Math.PI / 180));
        var g = new StreamGeometry();
        using (var s = g.Open())
        {
            s.BeginFigure(At(start), false);
            s.ArcTo(At(start + sweep), new Size(r, r), 0, sweep > 180, SweepDirection.Clockwise);
            s.EndFigure(false);
        }
        ctx.DrawGeometry(null, pen, g);
    }
}

// Half-width tile: title, ring with the value inside, one caption. Two sit side by side.
public sealed class RingTile : Border, IWidget
{
    private readonly WidgetKind kind; private readonly int index; private readonly Settings s; private readonly Palette p;
    private readonly RingGauge ring; private readonly TextBlock title, value, unit, caption, name; private readonly Control glyphHost;
    private readonly ExtrasView extras;

    public RingTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s)
    {
        this.kind = kind; this.index = index; this.s = s; this.p = p;
        Background = Palette.Brush(p.Card);
        BorderBrush = Palette.Brush(p.Stroke);
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(m.Radius);
        Padding = new Thickness(10, m.Pad - 6, 10, m.Pad - 4);
        title = Ui.Text("", 11.5, p.Ink, FontWeight.SemiBold);
        title.Margin = new Thickness(6, 0, 0, 0);
        glyphHost = new ContentControl();
        var head = Ui.Columns("Auto,*", glyphHost, title);
        name = Ui.Text("", 10.5, p.Soft);
        name.Margin = new Thickness(0, 1, 0, 0);
        double size = m.Subtitles ? 88 : 72;
        ring = new RingGauge { Width = size, Height = size, Palette = p, Thickness = m.Subtitles ? 7 : 6 };
        value = Ui.Text("", size * .3, p.Ink, FontWeight.SemiBold, true);
        value.LetterSpacing = -.5;
        unit = Ui.Text("", 10.5, p.Soft, FontWeight.Medium);
        unit.Margin = new Thickness(1, 0, 0, size * .05);
        unit.VerticalAlignment = VerticalAlignment.Bottom;
        var center = Ui.Row(0, value, unit);
        center.HorizontalAlignment = HorizontalAlignment.Center;
        center.VerticalAlignment = VerticalAlignment.Center;
        var gauge = new Panel { Width = size, Height = size, HorizontalAlignment = HorizontalAlignment.Center, Children = { ring, center } };
        caption = Ui.Text("", 11.5, p.Soft, numbers: true);
        caption.HorizontalAlignment = HorizontalAlignment.Center;
        caption.Margin = new Thickness(0, -6, 0, 0);
        extras = new ExtrasView(p, false) { Margin = new Thickness(0, 2, 0, 0) };
        Child = Ui.Stack(4, Ui.Stack(0, head, name), gauge, caption, extras);
    }

    public void Update(Snapshot snap, History h)
    {
        var sum = Summaries.For(kind, index, snap, h, s, p);
        if (glyphHost is ContentControl cc && cc.Tag as string != sum.Glyph) { cc.Content = Ui.Glyph(sum.Glyph, p.Soft, 13); cc.Tag = sum.Glyph; }
        title.Text = sum.Title;
        name.Text = sum.Name;
        name.IsVisible = s.ShowNames && sum.Name.Length > 0;
        Motion.Text(value, sum.Value);
        value.Foreground = Palette.Brush(sum.Color == p.Ink ? p.Ink : sum.Color);
        unit.Text = sum.Value == Format.Missing ? "" : sum.Unit;
        ring.Animate(sum.Fraction, sum.Color);
        caption.Text = sum.Caption;
        extras.Set(Summaries.Extras(kind, index, snap, h, s));
    }
}

// One slim line without a box: title and caption, the big value, a sparkline, a hairline below.
public sealed class MinimalRow : Border, IWidget
{
    private readonly WidgetKind kind; private readonly int index; private readonly Settings s; private readonly Palette p;
    private readonly TextBlock title, caption, name; private readonly Hero hero; private readonly AreaChart spark; private readonly ContentControl glyph = new();
    private readonly ExtrasView extras;

    public MinimalRow(WidgetKind kind, int index, Palette p, Metrics m, Settings s)
    {
        this.kind = kind; this.index = index; this.s = s; this.p = p;
        BorderBrush = Palette.Brush(p.Stroke);
        BorderThickness = new Thickness(0, 0, 0, 1);
        Padding = new Thickness(m.Pad - 6, 2, m.Pad - 6, m.Gap);
        title = Ui.Text("", 10, p.Soft, FontWeight.SemiBold);
        title.LetterSpacing = .6;
        title.Margin = new Thickness(6, 0, 0, 0);
        caption = Ui.Text("", 11.5, p.Soft, numbers: true);
        caption.HorizontalAlignment = HorizontalAlignment.Right;
        hero = new Hero(m.Subtitles ? 28 : 24, p);
        spark = new AreaChart { Height = m.Subtitles ? 30 : 24, LineWidth = 1.4, FillOpacity = .22, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(12, 0, 0, 4) };
        name = Ui.Text("", 11, p.Soft);
        name.Margin = new Thickness(8, 0, 8, 0);
        extras = new ExtrasView(p, true) { Margin = new Thickness(0, 2, 0, 0) };
        Child = Ui.Stack(0, Ui.Columns("Auto,Auto,*,Auto", glyph, title, name, caption), Ui.Columns("Auto,*", hero, spark), extras);
        Grid.SetColumn(caption, 3);
    }

    public void Update(Snapshot snap, History h)
    {
        var sum = Summaries.For(kind, index, snap, h, s, p);
        if (glyph.Tag as string != sum.Glyph) { glyph.Content = Ui.Glyph(sum.Glyph, p.Soft, 12); glyph.Tag = sum.Glyph; }
        title.Text = sum.Title.ToUpper(L.Culture);
        name.Text = sum.Name;
        name.IsVisible = s.ShowNames && sum.Name.Length > 0;
        caption.Text = sum.Caption;
        hero.Set(sum.Value, sum.Unit, sum.Color);
        spark.IsVisible = sum.Series.Length > 1 && (Summaries.ChartKey(kind) is not string key || s.Shows(key));
        extras.Set(Summaries.Extras(kind, index, snap, h, s));
        spark.Max = sum.SeriesMax;
        spark.Set(sum.Series, sum.Color == p.Ink ? p.Cool : sum.Color);
    }
}
