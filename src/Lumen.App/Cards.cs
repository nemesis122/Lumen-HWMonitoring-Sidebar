using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;
using static Lumen.App.L;

namespace Lumen.App;

public interface IWidget
{
    void Update(Snapshot s, History h);
}

// Every card shares one skeleton: icon, title and one key figure on the right; a quiet
// subtitle with the model; then the card's own content. Temperature leads wherever there is one.
public abstract class Card : Border, IWidget
{
    protected readonly Palette P;
    protected readonly Metrics M;
    protected readonly Settings S;
    protected readonly StackPanel Body;
    private readonly TextBlock aside, subtitle, title;

    protected Card(Palette p, Metrics m, Settings s, string glyph, string titleText)
    {
        P = p; M = m; S = s;
        Background = Palette.Brush(p.Card);
        BorderBrush = Palette.Brush(p.Stroke);
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(m.Radius);
        Padding = new Thickness(m.Pad, m.Pad - 2, m.Pad, m.Pad);

        title = Ui.Text(titleText, 12.5, p.Ink, FontWeight.SemiBold);
        aside = Ui.Text("", 12, p.Soft, FontWeight.Medium, true);
        aside.HorizontalAlignment = HorizontalAlignment.Right;
        var header = Ui.Columns("Auto,*,Auto", Ui.Glyph(glyph, p.Soft, 14), title, aside);
        title.Margin = new Thickness(8, 0, 8, 0);

        subtitle = Ui.Text("", 11.5, p.Soft);
        subtitle.IsVisible = false;
        Body = Ui.Stack(m.Gap, Ui.Stack(1, header, subtitle));
        // FitPanel may widen the row spacing to fill a column exactly.
        Body.Tag = "elastic";
        Child = Body;
    }

    protected string Title { set => title.Text = value; }
    protected string Aside { set => aside.Text = value; }

    protected string Subtitle
    {
        // Hardware names follow their own switch, not the density, so they never vanish just
        // because the sidebar had to shrink.
        set { subtitle.Text = Names.Clean(value); subtitle.IsVisible = S.ShowNames && !string.IsNullOrWhiteSpace(value); }
    }

    protected bool Shows(string key) => S.Shows(key);
    protected string Temp(double? c) => Format.Temperature(c, S.Fahrenheit);
    protected string TempWithUnit(double? c) => c is null ? Format.Missing : Temp(c) + " " + TempUnit;
    protected string TempUnit => Format.TemperatureUnit(S.Fahrenheit);
    protected Color HeatColor(double? c) => c is not double v || !double.IsFinite(v) ? P.Faint
        : v >= S.TemperatureWarning ? P.Ramp[^1] : P.At(Format.Heat(v));

    public abstract void Update(Snapshot s, History h);

    protected static TextBlock Label(string text, Palette p)
    {
        var l = Ui.Text(text.ToUpper(L.Culture), 10, p.Faint, FontWeight.SemiBold);
        l.LetterSpacing = .6;
        return l;
    }

    // Hero on the left, a secondary reading stacked on the right, the history chart below.
    protected (Hero hero, TextBlock side, AreaChart? chart) HeroBlock(string sideLabel, string chartKey)
    {
        var hero = new Hero(M.Hero, P);
        var label = Label(sideLabel, P);
        label.HorizontalAlignment = HorizontalAlignment.Right;
        var side = Ui.Text(Format.Missing, Math.Round(M.Hero * .5), P.Ink, FontWeight.Medium, true);
        side.HorizontalAlignment = HorizontalAlignment.Right;
        var right = Ui.Stack(0, label, side);
        right.VerticalAlignment = VerticalAlignment.Bottom;
        right.Margin = new Thickness(0, 0, 0, M.Hero * .12);
        Body.Children.Add(Ui.Columns("*,Auto", hero, right));
        AreaChart? chart = null;
        if (Shows(chartKey))
        {
            chart = new AreaChart { Height = M.Chart, Max = 100, Margin = new Thickness(0, -2, 0, 0) };
            Body.Children.Add(chart);
        }
        return (hero, side, chart);
    }

    // Adds only the stats the user has not hidden; returns them by key for updating.
    protected Dictionary<string, Stat> Stats(params (string key, string label)[] items)
    {
        var shown = items.Where(i => Shows(i.key)).ToList();
        var map = shown.ToDictionary(i => i.key, i => new Stat(i.label, P));
        if (map.Count > 0) Body.Children.Add(StatGrid.Of(map.Values.ToArray()));
        return map;
    }

    protected static void Put(Dictionary<string, Stat> stats, string key, string value, Color? color = null)
    {
        if (!stats.TryGetValue(key, out var s)) return;
        s.Value = value;
        if (color is Color c) s.Color = c;
    }
}

public sealed class CpuCard : Card
{
    private readonly Hero hero; private readonly TextBlock load; private readonly AreaChart? chart;
    private readonly Skyline? cores; private readonly Dictionary<string, Stat> stats;

    public CpuCard(Palette p, Metrics m, Settings s) : base(p, m, s, Glyphs.Cpu, T("Processor"))
    {
        (hero, load, chart) = HeroBlock(T("Load"), "cpu.chart");
        if (Shows("cpu.cores")) { cores = new Skyline { Height = m.Skyline, Palette = p }; Body.Children.Add(cores); }
        stats = Stats(("cpu.power", T("Power")), ("cpu.voltage", T("Voltage")), ("cpu.fan", T("Fan")));
    }

    public override void Update(Snapshot s, History h)
    {
        var c = s.Cpu;
        Subtitle = c?.Name ?? "";
        Aside = Format.Clock(c?.ClockMhz);
        hero.Set(Temp(c?.Temperature), TempUnit, HeatColor(c?.Temperature));
        Motion.Text(load, Format.Percent(c?.Load));
        chart?.Set(h.Read("cpu.load"), P.At(Format.Share(c?.Load) * .9));
        if (cores != null)
        {
            cores.Loads = c?.CoreLoads ?? Array.Empty<double>();
            cores.IsVisible = cores.Loads.Count > 0;
            cores.InvalidateVisual();
        }
        Put(stats, "cpu.power", Format.Watts(c?.PowerW));
        Put(stats, "cpu.voltage", Format.Volts(c?.Voltage));
        Put(stats, "cpu.fan", Format.Rpm(c?.FanRpm));
    }
}

public sealed class GpuCard : Card
{
    private readonly int index;
    private readonly Hero hero; private readonly TextBlock load; private readonly AreaChart? chart;
    private readonly TextBlock? vram; private readonly Meter? vramBar; private readonly Dictionary<string, Stat> stats;

    public GpuCard(Palette p, Metrics m, Settings s, int index) : base(p, m, s, Glyphs.Gpu, T("Graphics"))
    {
        this.index = index;
        (hero, load, chart) = HeroBlock(T("Load"), "gpu.chart");
        if (Shows("gpu.vram"))
        {
            vram = Ui.Text(Format.Missing, 11.5, p.Soft, FontWeight.Medium, true);
            vram.HorizontalAlignment = HorizontalAlignment.Right;
            vramBar = new Meter { Height = 5, Palette = p };
            Body.Children.Add(Ui.Stack(5, Ui.Columns("*,Auto", Label("VRAM", p), vram), vramBar));
        }
        stats = Stats(("gpu.power", T("Power")), ("gpu.hotspot", T("Hot spot")), ("gpu.fan", T("Fan")));
    }

    public override void Update(Snapshot s, History h) => Show(GpuGroup.Visible(s, S).ElementAtOrDefault(index), h);

    private void Show(GpuInfo? g, History h)
    {
        // With two adapters the title has to tell them apart even when subtitles are hidden.
        Title = g?.Integrated == true ? $"{T("Graphics")} · {T("Integrated")}" : T("Graphics");
        Subtitle = g?.Name ?? "";
        Aside = Format.Clock(g?.ClockMhz);
        hero.Set(Temp(g?.Temperature), TempUnit, HeatColor(g?.Temperature));
        Motion.Text(load, Format.Percent(g?.Load));
        chart?.Set(g is null ? Array.Empty<double>() : h.Read($"gpu.{g.Id}.load"), P.At(Format.Share(g?.Load) * .9));
        if (vram != null && vramBar != null)
        {
            if (g?.MemoryUsedMb is double used && g.MemoryTotalMb is double total && total > 0)
            {
                Motion.Text(vram, $"{Format.Number(used / 1024, "0.0")} / {Format.Gigabytes(total / 1024)}");
                vramBar.Set(used / total, P.At(used / total * .85));
            }
            else { vram.Text = Format.Missing; vramBar.Set(0, P.Cool); }
        }
        Put(stats, "gpu.power", Format.Watts(g?.PowerW));
        Put(stats, "gpu.hotspot", TempWithUnit(g?.HotSpot), g?.HotSpot is null ? P.Ink : HeatColor(g.HotSpot - 10));
        Put(stats, "gpu.fan", Format.Rpm(g?.FanRpm));
    }
}

// Dedicated graphics first, integrated after (unless switched off).
public static class GpuGroup
{
    public static IReadOnlyList<GpuInfo> Visible(Snapshot snap, Settings settings) =>
        snap.Gpus.Where(g => settings.ShowIntegratedGpu || !g.Integrated || snap.Gpus.All(x => x.Integrated))
            .OrderBy(g => g.Integrated ? 1 : 0).ToList();
}

public sealed class MemoryCard : Card
{
    private readonly Hero hero; private readonly TextBlock load; private readonly Meter? bar;
    private readonly Dictionary<string, Stat> stats;

    public MemoryCard(Palette p, Metrics m, Settings s) : base(p, m, s, Glyphs.Memory, T("Memory"))
    {
        hero = new Hero(m.Hero * .8, p);
        load = Ui.Text(Format.Missing, Math.Round(m.Hero * .5), p.Ink, FontWeight.Medium, true);
        load.VerticalAlignment = VerticalAlignment.Bottom;
        load.Margin = new Thickness(0, 0, 0, m.Hero * .1);
        Body.Children.Add(Ui.Columns("*,Auto", hero, load));
        if (Shows("mem.bar")) { bar = new Meter { Height = 8, Palette = p, Segments = 24, RampBySegment = true }; Body.Children.Add(bar); }
        stats = Stats(("mem.free", T("Available")), ("mem.total", T("Installed")), ("mem.peak", T("2-min peak")));
    }

    public override void Update(Snapshot s, History h)
    {
        var m = s.Memory;
        var (num, unit) = Format.Split(Format.Gigabytes(m?.UsedGb));
        hero.Set(num, unit + " " + T("used"), P.Ink);
        Motion.Text(load, Format.Percent(m?.Load));
        load.Foreground = Palette.Brush(P.At(Format.Share(m?.Load) * .9));
        bar?.Set(Format.Share(m?.Load), P.Cool);
        Put(stats, "mem.free", Format.Gigabytes(m?.AvailableGb));
        Put(stats, "mem.total", Format.Gigabytes(m?.TotalGb));
        var finite = h.Read("mem.load").Where(double.IsFinite).ToArray();
        Put(stats, "mem.peak", finite.Length == 0 ? Format.Missing : Format.Percent(finite.Max()));
    }
}

public sealed class DrivesCard : Card
{
    private readonly StackPanel rows = new();
    private readonly Dictionary<string, (TextBlock name, TextBlock temp, Meter used, TextBlock usedText, TextBlock read, TextBlock write)> map = new();

    public DrivesCard(Palette p, Metrics m, Settings s) : base(p, m, s, Glyphs.Drive, T("Drives"))
    {
        rows.Spacing = m.Gap + 2;
        Body.Children.Add(rows);
    }

    public override void Update(Snapshot s, History h)
    {
        Aside = $"{s.Disks.Count} {(s.Disks.Count == 1 ? T("drive") : T("drives"))}";
        foreach (var d in s.Disks)
        {
            if (!map.TryGetValue(d.Id, out var r))
            {
                var name = Ui.Text("", 12, P.Ink, FontWeight.Medium);
                var temp = Ui.Text("", 12, P.Ink, FontWeight.SemiBold, true);
                var used = new Meter { Height = 5, Palette = P };
                var usedText = Ui.Text("", 11.5, P.Soft, numbers: true);
                // "R" and "W" stand still; only the figure beside each changes, right-aligned in a
                // fixed width, so nothing in the row moves sideways.
                TextBlock Rate()
                {
                    var tb = Ui.Text("", 11.5, P.Soft, numbers: true);
                    tb.Width = 64;
                    tb.TextAlignment = TextAlignment.Right;
                    return tb;
                }
                TextBlock Letter(string text, double left)
                {
                    var l = Ui.Text(text, 11.5, P.Faint, FontWeight.SemiBold);
                    l.Margin = new Thickness(left, 0, 4, 0);
                    return l;
                }
                var read = Rate();
                var write = Rate();
                var rates = Ui.Columns("Auto,Auto,Auto,Auto", Letter("R", 0), read, Letter("W", 10), write);
                rates.IsVisible = Shows("drives.io");
                rows.Children.Add(Ui.Stack(5, Ui.Columns("*,Auto", name, temp), used, Ui.Columns("*,Auto", usedText, rates)));
                map[d.Id] = r = (name, temp, used, usedText, read, write);
            }
            // Two identical models are told apart by their drive letters.
            r.name.Text = d.Letters.Length > 0 ? $"{ShortName(d.Name)} · {d.Letters}" : ShortName(d.Name);
            Motion.Text(r.temp, TempWithUnit(d.Temperature));
            r.temp.Foreground = Palette.Brush(d.Temperature is null ? P.Faint : HeatColor(d.Temperature + 10));
            double share = Format.Share(d.UsedPercent);
            r.used.Set(share, share >= .9 ? P.Ramp[^1] : share >= .8 ? P.Ramp[2] : P.Cool);
            r.usedText.Text = d.UsedPercent is null ? "" : $"{Format.Percent(d.UsedPercent)} {T("used")}";
            // Disk activity comes in short bursts; the mean of the last three seconds reads calmly.
            r.read.Text = Format.Rate(Recent(h.Read($"disk.{d.Id}.read"), d.ReadBytesPerSecond));
            r.write.Text = Format.Rate(Recent(h.Read($"disk.{d.Id}.write"), d.WriteBytesPerSecond));
        }
    }

    private static double? Recent(double[] series, double? current)
    {
        var last = series.Skip(Math.Max(0, series.Length - 3)).Where(double.IsFinite).ToArray();
        return last.Length > 0 ? last.Average() : current;
    }

    // "Samsung SSD 970 EVO Plus 2TB" → "970 EVO Plus 2TB": the maker is rarely what tells drives apart.
    private static string ShortName(string name)
    {
        foreach (var prefix in new[] { "Samsung SSD ", "Samsung ", "WDC ", "WD ", "KINGSTON ", "Crucial " })
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return name[prefix.Length..];
        return name;
    }
}

public sealed class NetworkCard : Card
{
    private readonly Hero down, up; private readonly MirrorChart? chart; private readonly Dictionary<string, Stat> stats;

    public NetworkCard(Palette p, Metrics m, Settings s) : base(p, m, s, Glyphs.Network, T("Network"))
    {
        down = new Hero(Math.Round(m.Hero * .58), p);
        up = new Hero(Math.Round(m.Hero * .58), p);
        Control Labelled(string text, Color color, Hero hero)
        {
            var dot = new Border { Width = 6, Height = 6, CornerRadius = new CornerRadius(3), Background = Palette.Brush(color), VerticalAlignment = VerticalAlignment.Center };
            return Ui.Stack(1, Ui.Row(5, dot, Label(text, p)), hero);
        }
        var upBlock = Labelled(T("Upload"), p.Calm, up);
        upBlock.HorizontalAlignment = HorizontalAlignment.Right;
        Body.Children.Add(Ui.Columns("*,Auto", Labelled(T("Download"), p.Cool, down), upBlock));
        if (Shows("net.chart")) { chart = new MirrorChart { Height = m.Chart + 6, Palette = p }; Body.Children.Add(chart); }
        var items = new[] { ("net.use", T("Link use")), ("net.peak", T("2-min peak")), ("net.address", T("Address")) }.Where(i => Shows(i.Item1)).ToList();
        stats = items.ToDictionary(i => i.Item1, i => new Stat(i.Item2, p));
        // The address needs the most room, so its column is wider.
        if (stats.Count > 0) Body.Children.Add(StatGrid.Weighted(string.Join(",", stats.Keys.Select(k => k == "net.address" ? "1.35*" : "*")), stats.Values.ToArray()));
        // How full the link is, as a thin bar under the figures.
        if (stats.ContainsKey("net.use")) { linkBar = new Meter { Height = 3, Palette = p, Margin = new Thickness(0, -4, 0, 0) }; Body.Children.Add(linkBar); }
    }

    private readonly Meter? linkBar;

    public override void Update(Snapshot s, History h)
    {
        var n = s.Network;
        Subtitle = n?.Name ?? "";
        Aside = Shows("net.link") ? Format.Link(n?.LinkBitsPerSecond) : "";
        var (dn, du) = Format.Split(Format.Rate(n?.DownloadBytesPerSecond));
        var (un, uu) = Format.Split(Format.Rate(n?.UploadBytesPerSecond));
        down.Set(dn, du, P.Ink);
        up.Set(un, uu, P.Ink);
        var downs = h.Read("net.down");
        chart?.SetData(downs, h.Read("net.up"));
        // In bits per second, the unit of the link speed itself: 16 Mbit/s of 10 Gbit/s.
        double bits = Math.Max(n?.DownloadBytesPerSecond ?? 0, n?.UploadBytesPerSecond ?? 0) * 8;
        Put(stats, "net.use", n is null ? Format.Missing : Format.Bits(bits));
        if (linkBar != null)
        {
            double share = n?.LinkBitsPerSecond is double link && link > 0 ? bits / link : 0;
            linkBar.Set(share, share >= .9 ? P.Ramp[^1] : share >= .7 ? P.Ramp[2] : P.Cool);
        }
        var finite = downs.Where(double.IsFinite).ToArray();
        Put(stats, "net.peak", finite.Length == 0 ? Format.Missing : Format.Rate(finite.Max()));
        Put(stats, "net.address", n?.Address ?? Format.Missing);
    }
}

public sealed class ClockCard : Card
{
    private readonly TextBlock time, seconds, date, dayText; private readonly Control dayBlock; private readonly Meter day;

    public ClockCard(Palette p, Metrics m, Settings s) : base(p, m, s, Glyphs.Clock, T("Today"))
    {
        time = Ui.Text("", m.Hero * 1.15, p.Ink, FontWeight.Light, true);
        time.LetterSpacing = -m.Hero * .04;
        seconds = Ui.Text("", m.Hero * .42, p.Soft, FontWeight.Normal, true);
        seconds.VerticalAlignment = VerticalAlignment.Bottom;
        seconds.Margin = new Thickness(4, 0, 0, m.Hero * .2);
        seconds.IsVisible = Shows("clock.seconds");
        date = Ui.Text("", 12.5, p.Soft, FontWeight.Medium);
        date.IsVisible = Shows("clock.date");
        Body.Children.Add(Ui.Row(0, time, seconds));
        Body.Children.Add(date);
        day = new Meter { Height = 4, Palette = p };
        dayText = Ui.Text("", 11, p.Soft, numbers: true);
        dayText.HorizontalAlignment = HorizontalAlignment.Right;
        dayBlock = Ui.Stack(5, Ui.Columns("*,Auto", Label(T("Day"), p), dayText), day);
        dayBlock.IsVisible = Shows("clock.day");
        Body.Children.Add(dayBlock);
    }

    public override void Update(Snapshot s, History h)
    {
        var t = s.Time;
        time.Text = t.ToString("HH:mm", L.Culture);
        seconds.Text = t.ToString("ss", L.Culture);
        string d = t.ToString(L.Culture.DateTimeFormat.LongDatePattern.Contains("dddd") ? L.Culture.DateTimeFormat.LongDatePattern : "dddd, " + L.Culture.DateTimeFormat.LongDatePattern, L.Culture);
        date.Text = d.Length > 0 ? char.ToUpper(d[0], L.Culture) + d[1..] : d;
        Aside = T("Week") + " " + System.Globalization.ISOWeek.GetWeekOfYear(t);
        double share = t.TimeOfDay.TotalHours / 24;
        day.Set(share, P.Cool);
        var left = TimeSpan.FromHours(24) - t.TimeOfDay;
        dayText.Text = $"{Format.Number(share * 100)} %  ·  {F("{0} h {1:00} m left", (int)left.TotalHours, left.Minutes)}";
    }
}

public sealed class WeatherCard : Card
{
    private readonly Hero hero; private readonly WeatherIcon icon; private readonly TextBlock sky, empty;
    private readonly Grid? forecast; private readonly Dictionary<string, Stat> stats;
    private readonly Control content;

    public WeatherCard(Palette p, Metrics m, Settings s) : base(p, m, s, Glyphs.Cloud, T("Weather"))
    {
        hero = new Hero(M.Hero, p);
        icon = new WeatherIcon { Width = M.Hero * 1.15, Height = M.Hero * 1.15, Palette = p, VerticalAlignment = VerticalAlignment.Center };
        sky = Ui.Text("", 12.5, p.Soft, FontWeight.Medium);
        var left = Ui.Stack(0, hero, sky);
        var top = Ui.Columns("*,Auto", left, icon);
        var panel = Ui.Stack(M.Gap, top);
        stats = new();
        var items = new[] { ("weather.feels", T("Feels like")), ("weather.wind", T("Wind")), ("weather.humidity", T("Humidity")) }.Where(i => Shows(i.Item1)).ToList();
        foreach (var (k, l) in items) stats[k] = new Stat(l, p);
        if (stats.Count > 0) panel.Children.Add(StatGrid.Of(stats.Values.ToArray()));
        if (Shows("weather.forecast"))
        {
            forecast = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("*,*,*"), Margin = new Thickness(0, 2, 0, 0) };
            panel.Children.Add(forecast);
        }
        content = panel;
        empty = Ui.Text(T("Set a place in the settings."), 12, p.Soft);
        empty.TextWrapping = TextWrapping.Wrap;
        empty.TextTrimming = TextTrimming.None;
        Body.Children.Add(content);
        Body.Children.Add(empty);
    }

    public override void Update(Snapshot s, History h)
    {
        var w = s.Weather;
        content.IsVisible = w != null;
        empty.IsVisible = w == null;
        if (w == null) { Aside = ""; return; }
        Aside = w.Place;
        hero.Set(Temp(w.TemperatureC), TempUnit, P.At(Math.Clamp((w.TemperatureC + 10) / 45, 0, 1)));
        string group = WeatherClient.Group(w.Code);
        icon.Set(group, w.IsDay);
        sky.Text = T(group);
        Put(stats, "weather.feels", TempWithUnit(w.FeelsLikeC));
        Put(stats, "weather.wind", w.WindKmh is double k ? Format.Number(S.Fahrenheit ? k / 1.609 : k) + (S.Fahrenheit ? " mph" : " km/h") : Format.Missing);
        Put(stats, "weather.humidity", Format.Percent(w.Humidity));
        if (forecast == null) return;
        forecast.Children.Clear();
        var days = w.Days.Where(d => d.Date.Date > w.Updated.Date).Take(3).ToList();
        for (int i = 0; i < days.Count; i++)
        {
            var d = days[i];
            var name = Label(d.Date.ToString("ddd", L.Culture).TrimEnd('.'), P);
            var ic = new WeatherIcon { Width = 22, Height = 22, Palette = P, HorizontalAlignment = HorizontalAlignment.Left };
            ic.Set(WeatherClient.Group(d.Code), true);
            var range = Ui.Row(5, Ui.Text(Temp(d.MaxC) + "°", 12.5, P.Ink, FontWeight.Medium, true), Ui.Text(Temp(d.MinC) + "°", 12.5, P.Soft, numbers: true));
            var cell = Ui.Stack(3, name, Ui.Row(6, ic, range));
            Grid.SetColumn(cell, i);
            forecast.Children.Add(cell);
        }
    }
}

public sealed class BatteryCard : Card
{
    private readonly Hero hero; private readonly TextBlock state; private readonly Meter bar; private readonly Dictionary<string, Stat> stats;

    public BatteryCard(Palette p, Metrics m, Settings s) : base(p, m, s, Glyphs.Battery, T("Battery"))
    {
        hero = new Hero(M.Hero * .9, p);
        state = Ui.Text("", 12.5, p.Soft, FontWeight.Medium);
        state.VerticalAlignment = VerticalAlignment.Bottom;
        state.Margin = new Thickness(0, 0, 0, M.Hero * .14);
        Body.Children.Add(Ui.Columns("*,Auto", hero, state));
        bar = new Meter { Height = 8, Palette = p, Segments = 20 };
        Body.Children.Add(bar);
        stats = Stats(("battery.time", T("Time left")), ("battery.rate", T("Rate")), ("battery.health", T("Health")));
    }

    public override void Update(Snapshot s, History h)
    {
        var b = s.Battery;
        IsVisible = b != null;
        if (b == null) return;
        // Low charge is the "hot" state here: red when nearly empty on battery.
        var color = b.Charging || b.OnAc ? P.Calm : P.At(Math.Clamp((60 - b.Percent) / 50, 0, 1) * .5 + (b.Percent < 15 ? .5 : 0));
        hero.Set(Format.Number(b.Percent), "%", color);
        state.Text = b.Charging ? T("Charging") : b.OnAc ? T("Plugged in") : T("On battery");
        bar.Set(b.Percent / 100, color);
        if (stats.TryGetValue("battery.time", out var t))
        {
            t.Value = b.MinutesLeft is double min && min > 0 ? $"{(int)(min / 60)} h {(int)(min % 60):00} m" : Format.Missing;
            ((TextBlock)t.Children[0]).Text = (b.Charging ? T("Until full") : T("Time left")).ToUpper(L.Culture);
        }
        Put(stats, "battery.rate", b.PowerW is double w ? (w > 0 ? "+" : "") + Format.Watts(w) : Format.Missing);
        Put(stats, "battery.health", b.WearPercent is double wear ? Format.Percent(100 - wear) : Format.Missing);
    }
}

public sealed class CoolingCard : Card
{
    private readonly Hero hero; private readonly TextBlock heroName, active;
    private readonly StackPanel? fans; private readonly Grid? temps;
    private readonly Dictionary<string, (TextBlock name, TextBlock rpm, Meter bar)> rows = new();

    public CoolingCard(Palette p, Metrics m, Settings s) : base(p, m, s, Glyphs.Fan, T("Cooling"))
    {
        hero = new Hero(M.Hero * .8, p);
        heroName = Label("", p);
        var left = Ui.Stack(0, heroName, hero);
        var activeLabel = Label(T("Fans"), p);
        activeLabel.HorizontalAlignment = HorizontalAlignment.Right;
        active = Ui.Text(Format.Missing, Math.Round(M.Hero * .5), p.Ink, FontWeight.Medium, true);
        active.HorizontalAlignment = HorizontalAlignment.Right;
        var right = Ui.Stack(0, activeLabel, active);
        right.VerticalAlignment = VerticalAlignment.Bottom;
        right.Margin = new Thickness(0, 0, 0, M.Hero * .1);
        Body.Children.Add(Ui.Columns("*,Auto", left, right));
        if (Shows("cooling.fans")) { fans = new StackPanel { Spacing = 7 }; Body.Children.Add(fans); }
        if (Shows("cooling.board")) { temps = new Grid(); Body.Children.Add(temps); }
    }

    public override void Update(Snapshot s, History h)
    {
        // Readings the user switched off are left out everywhere in this card.
        var fanList = s.Fans.Where(f => !S.Hidden("fan:" + f.Name)).ToList();
        var tempList = s.BoardTemps.Where(t => !S.Hidden("temp:" + t.Name)).ToList();
        IsVisible = fanList.Count > 0 || tempList.Count > 0;
        Subtitle = s.BoardName ?? "";
        var hottest = tempList.OrderByDescending(t => t.Celsius).FirstOrDefault();
        heroName.Text = (hottest is null ? T("Board") : Names.Temp(hottest.Name, S)).ToUpper(L.Culture);
        hero.Set(Temp(hottest?.Celsius), TempUnit, HeatColor(hottest?.Celsius + 5));
        Motion.Text(active, fanList.Count == 0 ? Format.Missing : $"{fanList.Count(f => f.Rpm > 0)}/{fanList.Count}");
        Aside = "";
        if (fans != null)
        {
            double top = Math.Max(1800, fanList.Select(f => f.Rpm).DefaultIfEmpty(0).Max() * 1.08);
            foreach (var r in rows.Values) ((Control)r.name.Parent!).IsVisible = false;
            foreach (var f in fanList.Take(8))
            {
                if (!rows.TryGetValue(f.Name, out var r))
                {
                    var name = Ui.Text("", 11.5, P.Soft, FontWeight.Medium);
                    name.Width = 92;
                    name.Margin = new Thickness(0, 0, 10, 0);
                    var bar = new Meter { Height = 4, Palette = P, VerticalAlignment = VerticalAlignment.Center, MinWidth = 30 };
                    var rpm = Ui.Text("", 11.5, P.Ink, FontWeight.Medium, true);
                    rpm.Width = 72;
                    rpm.TextAlignment = TextAlignment.Right;
                    // A fixed name width, wide enough for "Front unten", so every bar starts at the same place.
                    fans.Children.Add(Ui.Columns("Auto,*,Auto", name, bar, rpm));
                    rows[f.Name] = r = (name, rpm, bar);
                }
                ((Control)r.name.Parent!).IsVisible = true;
                r.name.Text = Names.Fan(f.Name, S);
                Motion.Text(r.rpm, f.Rpm > 0 ? Format.Rpm(f.Rpm) : T("Stopped"));
                r.rpm.Foreground = Palette.Brush(f.Rpm > 0 ? P.Ink : P.Faint);
                r.bar.Set(f.Rpm / top, P.Cool);
            }
        }
        if (temps != null)
        {
            string key = string.Join("|", tempList.Take(3).Select(t => t.Name + "=" + Names.Temp(t.Name, S)));
            if (!Equals(temps.Tag, key))
            {
                temps.Tag = key;
                temps.Children.Clear();
                temps.ColumnDefinitions = ColumnDefinitions.Parse(string.Join(",", tempList.Take(3).Select(_ => "*").DefaultIfEmpty("*")));
                int i = 0;
                foreach (var t in tempList.Take(3)) { var st = new Stat(Names.Temp(t.Name, S), P) { Tag = t.Name }; Grid.SetColumn(st, i++); temps.Children.Add(st); }
            }
            foreach (var st in temps.Children.OfType<Stat>())
            {
                var t = tempList.FirstOrDefault(x => x.Name == (string?)st.Tag);
                st.Value = TempWithUnit(t?.Celsius);
                st.Color = t is null ? P.Ink : HeatColor(t.Celsius + 5);
            }
        }
    }
}

public sealed class ProcessesCard : Card
{
    private readonly StackPanel list = new() { Spacing = 7 };
    private readonly List<(TextBlock name, TextBlock mem, TextBlock cpu, Meter bar)> rows = new();

    public ProcessesCard(Palette p, Metrics m, Settings s) : base(p, m, s, Glyphs.Apps, T("Top apps"))
    {
        var head = Ui.Columns("*,Auto,Auto", Label("", p), Label("RAM", p), Label("CPU", p));
        ((Control)head.Children[1]).IsVisible = Shows("proc.memory");
        ((TextBlock)head.Children[1]).Width = 64;
        ((TextBlock)head.Children[1]).TextAlignment = TextAlignment.Right;
        ((TextBlock)head.Children[2]).Width = 52;
        ((TextBlock)head.Children[2]).TextAlignment = TextAlignment.Right;
        Body.Children.Add(head);
        Body.Children.Add(list);
    }

    public override void Update(Snapshot s, History h)
    {
        var top = s.Processes.Take(5).ToList();
        while (rows.Count < top.Count)
        {
            var name = Ui.Text("", 12, P.Ink, FontWeight.Medium);
            var mem = Ui.Text("", 11.5, P.Soft, numbers: true);
            mem.Width = 64; mem.TextAlignment = TextAlignment.Right; mem.IsVisible = Shows("proc.memory");
            var cpu = Ui.Text("", 12, P.Ink, FontWeight.SemiBold, true);
            cpu.Width = 52; cpu.TextAlignment = TextAlignment.Right;
            var bar = new Meter { Height = 3, Palette = P };
            list.Children.Add(Ui.Stack(4, Ui.Columns("*,Auto,Auto", name, mem, cpu), bar));
            rows.Add((name, mem, cpu, bar));
        }
        double scale = Math.Max(25, top.Select(p => p.CpuPercent).DefaultIfEmpty(0).Max());
        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            ((Control)list.Children[i]).IsVisible = i < top.Count;
            if (i >= top.Count) continue;
            var pr = top[i];
            r.name.Text = pr.Name;
            Motion.Text(r.mem, pr.MemoryMb >= 1024 ? Format.Number(pr.MemoryMb / 1024, "0.0") + " GB" : Format.Number(pr.MemoryMb) + " MB");
            Motion.Text(r.cpu, Format.Number(pr.CpuPercent, pr.CpuPercent < 10 ? "0.0" : "0") + " %");
            r.cpu.Foreground = Palette.Brush(P.At(pr.CpuPercent / 100 * .9 + .1));
            r.bar.Set(pr.CpuPercent / scale, P.At(pr.CpuPercent / 100 * .9 + .1));
        }
    }
}

public static class Cards
{
    public static Control Create(WidgetKind kind, WidgetStyle style, int index, Palette p, Metrics m, Settings s)
    {
        if (style == WidgetStyle.Ring && Settings.HasStyles(kind)) return new RingTile(kind, index, p, m, s);
        if (style == WidgetStyle.Minimal && Settings.HasStyles(kind)) return new MinimalRow(kind, index, p, m, s);
        if (style == WidgetStyle.Bar && Settings.HasStyles(kind)) return new BarTile(kind, index, p, m, s);
        if (style == WidgetStyle.Graph && Settings.HasStyles(kind)) return new GraphTile(kind, index, p, m, s);
        if (style == WidgetStyle.Tile && Settings.HasStyles(kind)) return new NumberTile(kind, index, p, m, s);
        return kind switch
        {
            WidgetKind.Clock => new ClockCard(p, m, s),
            WidgetKind.WorldClocks => new WorldClocksCard(p, m, s),
            WidgetKind.Calendar => new CalendarCard(p, m, s),
            WidgetKind.Weather => new WeatherCard(p, m, s),
            WidgetKind.Media => new MediaCard(p, m, s),
            WidgetKind.Battery => new BatteryCard(p, m, s),
            WidgetKind.Cpu => new CpuCard(p, m, s),
            WidgetKind.Gpu => new GpuCard(p, m, s, index),
            WidgetKind.Fps => new FpsCard(p, m, s),
            WidgetKind.Cooling => new CoolingCard(p, m, s),
            WidgetKind.Power => new PowerCard(p, m, s),
            WidgetKind.Memory => new MemoryCard(p, m, s),
            WidgetKind.Processes => new ProcessesCard(p, m, s),
            WidgetKind.System => new SystemCard(p, m, s),
            WidgetKind.Drives => new DrivesCard(p, m, s),
            WidgetKind.Volumes => new VolumesCard(p, m, s),
            WidgetKind.Ping => new PingCard(p, m, s),
            WidgetKind.DataUsage => new DataUsageCard(p, m, s),
            WidgetKind.Notes => new NotesCard(p, m, s),
            _ => new NetworkCard(p, m, s)
        };
    }

    public static string Name(WidgetKind kind) => kind switch
    {
        WidgetKind.Clock => T("Clock and date"),
        WidgetKind.WorldClocks => T("World clocks"),
        WidgetKind.Calendar => T("Calendar"),
        WidgetKind.Weather => T("Weather"),
        WidgetKind.Media => T("Now playing"),
        WidgetKind.Battery => T("Battery"),
        WidgetKind.Cpu => T("Processor"),
        WidgetKind.Gpu => T("Graphics card"),
        WidgetKind.Fps => T("FPS (games)"),
        WidgetKind.Cooling => T("Cooling and mainboard"),
        WidgetKind.Power => T("Power and cost"),
        WidgetKind.Memory => T("Memory"),
        WidgetKind.Processes => T("Top apps"),
        WidgetKind.System => T("System"),
        WidgetKind.Drives => T("Drives"),
        WidgetKind.Volumes => T("Storage space"),
        WidgetKind.Ping => "Ping",
        WidgetKind.DataUsage => T("Data usage"),
        WidgetKind.Notes => T("Notes"),
        _ => T("Network")
    };

    // The parts of each card that can be switched off one by one in the settings.
    public static (string Key, string Label)[] Values(WidgetKind kind) => kind switch
    {
        WidgetKind.Clock => new[] { ("clock.seconds", T("Seconds")), ("clock.date", T("Date")), ("clock.day", T("Day progress")) },
        WidgetKind.Weather => new[] { ("weather.forecast", T("Forecast")), ("weather.feels", T("Feels like")), ("weather.wind", T("Wind")), ("weather.humidity", T("Humidity")) },
        WidgetKind.Battery => new[] { ("battery.time", T("Time left")), ("battery.rate", T("Rate")), ("battery.health", T("Health")) },
        WidgetKind.Cpu => new[] { ("cpu.chart", T("History chart")), ("cpu.cores", T("Core bars")), ("cpu.power", T("Power")), ("cpu.voltage", T("Voltage")), ("cpu.fan", T("Fan")) },
        WidgetKind.Gpu => new[] { ("gpu.chart", T("History chart")), ("gpu.vram", "VRAM"), ("gpu.power", T("Power")), ("gpu.hotspot", T("Hot spot")), ("gpu.fan", T("Fan")) },
        WidgetKind.Cooling => new[] { ("cooling.fans", T("Fans")), ("cooling.board", T("Board temperatures")) },
        WidgetKind.Memory => new[] { ("mem.bar", T("Segment bar")), ("mem.free", T("Available")), ("mem.total", T("Installed")), ("mem.peak", T("2-min peak")) },
        WidgetKind.Processes => new[] { ("proc.memory", "RAM") },
        WidgetKind.Drives => new[] { ("drives.io", T("Read and write rates")), ("drives.temps", T("Each drive")) },
        WidgetKind.Network => new[] { ("net.link", T("Link speed")), ("net.chart", T("History chart")), ("net.use", T("Link use")), ("net.peak", T("2-min peak")), ("net.address", T("Address")) },
        WidgetKind.Fps => new[] { ("fps.chart", T("History chart")), ("fps.frametime", T("Frame time")) },
        WidgetKind.Ping => new[] { ("ping.chart", T("History chart")), ("ping.jitter", T("Jitter")), ("ping.loss", T("Loss")), ("ping.target", T("Target")) },
        WidgetKind.Power => new[] { ("power.chart", T("History chart")), ("power.cost", T("Costs")), ("power.note", T("Note on the estimate")) },
        WidgetKind.DataUsage => new[] { ("data.week", T("This week")), ("data.month", T("This month")) },
        WidgetKind.System => new[] { ("system.boot", T("Started")), ("system.processes", T("Processes")), ("system.threads", T("Threads")) },
        WidgetKind.Volumes => new[] { ("volumes.detail", T("Used of total")) },
        _ => Array.Empty<(string, string)>()
    };
}
