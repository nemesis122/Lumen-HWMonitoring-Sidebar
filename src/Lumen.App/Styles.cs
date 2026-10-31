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
            case WidgetKind.DataUsage:
            {
                var u = s.Usage;
                var (num, unit) = Format.Split(Format.Size(u?.DownToday));
                return new(Glyphs.Data, T("Data today"), num, unit, p.Cool, 0, u is null ? "" : "↑ " + Format.Size(u.UpToday), Array.Empty<double>(), null);
            }
        }
        return Summary.Empty;
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
        Child = Ui.Stack(4, Ui.Stack(0, head, name), gauge, caption);
    }

    public void Update(Snapshot snap, History h)
    {
        var sum = Summaries.For(kind, index, snap, h, s, p);
        if (glyphHost is ContentControl cc && cc.Tag as string != sum.Glyph) { cc.Content = Ui.Glyph(sum.Glyph, p.Soft, 13); cc.Tag = sum.Glyph; }
        title.Text = sum.Title;
        name.Text = sum.Name;
        name.IsVisible = s.ShowNames && sum.Name.Length > 0;
        value.Text = sum.Value;
        value.Foreground = Palette.Brush(sum.Color == p.Ink ? p.Ink : sum.Color);
        unit.Text = sum.Value == Format.Missing ? "" : sum.Unit;
        ring.Fraction = sum.Fraction;
        ring.Color = sum.Color;
        ring.InvalidateVisual();
        caption.Text = sum.Caption;
    }
}

// One slim line without a box: title and caption, the big value, a sparkline, a hairline below.
public sealed class MinimalRow : Border, IWidget
{
    private readonly WidgetKind kind; private readonly int index; private readonly Settings s; private readonly Palette p;
    private readonly TextBlock title, caption, name; private readonly Hero hero; private readonly AreaChart spark; private readonly ContentControl glyph = new();

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
        Child = Ui.Stack(0, Ui.Columns("Auto,Auto,*,Auto", glyph, title, name, caption), Ui.Columns("Auto,*", hero, spark));
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
        spark.IsVisible = sum.Series.Length > 1;
        spark.Max = sum.SeriesMax;
        spark.Set(sum.Series, sum.Color == p.Ink ? p.Cool : sum.Color);
    }
}
