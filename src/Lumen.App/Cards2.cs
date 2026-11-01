using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Lumen.Core;
using static Lumen.App.L;

namespace Lumen.App;

public sealed class PingCard : Card
{
    private readonly Hero hero; private readonly TextBlock router; private readonly AreaChart? chart; private readonly Dictionary<string, Stat> stats;

    public PingCard(Palette p, Metrics m, Settings s) : base(p, m, s, Glyphs.Ping, "Ping")
    {
        (hero, router, chart) = HeroBlock(T("Router"), "ping.chart");
        if (chart != null) { chart.Max = null; chart.Height = m.Chart * .8; }
        stats = Stats(("ping.jitter", T("Jitter")), ("ping.loss", T("Loss")), ("ping.target", T("Target")));
    }

    public override void Update(Snapshot s, History h)
    {
        var pi = s.Ping;
        var (num, unit) = Format.Split(Format.Milliseconds(pi?.Ms));
        hero.Set(num, unit, Summaries.PingColor(pi?.Ms, P));
        Motion.Text(router, Format.Milliseconds(pi?.GatewayMs));
        if (chart != null)
        {
            var series = h.Read("ping");
            chart.Max = Math.Max(40, series.Where(double.IsFinite).DefaultIfEmpty(0).Max() * 1.2);
            chart.Set(series, Summaries.PingColor(pi?.Ms, P));
        }
        Put(stats, "ping.jitter", Format.Milliseconds(pi?.JitterMs));
        Put(stats, "ping.loss", pi is null ? Format.Missing : Format.Number(pi.LossPercent, pi.LossPercent is > 0 and < 10 ? "0.0" : "0") + " %",
            pi is { LossPercent: > 0 } ? P.Ramp[2] : P.Ink);
        Put(stats, "ping.target", pi?.Target ?? S.PingTarget);
    }
}

public sealed class PowerCard : Card
{
    private readonly Hero hero; private readonly TextBlock today; private readonly AreaChart? chart; private readonly Dictionary<string, Stat> stats;
    private readonly TextBlock note;

    public PowerCard(Palette p, Metrics m, Settings s) : base(p, m, s, Glyphs.Power, T("Power"))
    {
        (hero, today, chart) = HeroBlock(T("Today"), "power.chart");
        if (chart != null) { chart.Max = null; chart.Height = m.Chart * .8; }
        var items = Shows("power.cost") ? new[] { ("power.today", T("Today")), ("power.week", T("This week")), ("power.month", T("This month")) } : Array.Empty<(string, string)>();
        stats = Stats(items);
        note = Ui.Text(F("CPU + GPU + {0} W for the rest (estimate)", Format.Number(s.BaseWatts)), 11, p.Faint);
        note.IsVisible = Shows("power.note");
        Body.Children.Add(note);
    }

    public override void Update(Snapshot s, History h)
    {
        var u = s.Usage;
        hero.Set(Format.Number(u?.WattsNow), "W", P.At(Math.Clamp((u?.WattsNow ?? 0) / 600, 0, 1)));
        Motion.Text(today, u is null ? Format.Missing : Format.Number(u.KwhToday, "0.00") + " kWh");
        chart?.Set(h.Read("power"), P.At(Math.Clamp((u?.WattsNow ?? 0) / 600, 0, 1)));
        if (u == null) return;
        Put(stats, "power.today", Format.Money(u.KwhToday * S.PricePerKwh, S.Currency));
        Put(stats, "power.week", Format.Money(u.KwhWeek * S.PricePerKwh, S.Currency));
        Put(stats, "power.month", Format.Money(u.KwhMonth * S.PricePerKwh, S.Currency));
    }
}

public sealed class DataUsageCard : Card
{
    private readonly Hero down, up;
    private readonly Dictionary<string, (TextBlock down, TextBlock up)> rows = new();

    public DataUsageCard(Palette p, Metrics m, Settings s) : base(p, m, s, Glyphs.Data, T("Data usage"))
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
        // Week and month as rows: label, download, upload, so long totals never get cut off.
        var table = new StackPanel { Spacing = 4 };
        foreach (var (key, label) in new[] { ("data.week", T("This week")), ("data.month", T("This month")) })
        {
            if (!Shows(key)) continue;
            var d = Ui.Text("", 12, p.Ink, FontWeight.Medium, true);
            var u = Ui.Text("", 12, p.Soft, numbers: true);
            d.HorizontalAlignment = HorizontalAlignment.Right;
            u.HorizontalAlignment = HorizontalAlignment.Right;
            u.Width = 82;
            u.TextAlignment = TextAlignment.Right;
            table.Children.Add(Ui.Columns("*,Auto,Auto", Label(label, p), d, u));
            rows[key] = (d, u);
        }
        if (table.Children.Count > 0) Body.Children.Add(table);
    }

    public override void Update(Snapshot s, History h)
    {
        var u = s.Usage;
        Aside = T("Today");
        var (dn, du) = Format.Split(Format.Size(u?.DownToday));
        var (un, uu) = Format.Split(Format.Size(u?.UpToday));
        down.Set(dn, du, P.Ink);
        up.Set(un, uu, P.Ink);
        if (u == null) return;
        if (rows.TryGetValue("data.week", out var w)) { w.down.Text = "↓ " + Format.Size(u.DownWeek); w.up.Text = "↑ " + Format.Size(u.UpWeek); }
        if (rows.TryGetValue("data.month", out var m)) { m.down.Text = "↓ " + Format.Size(u.DownMonth); m.up.Text = "↑ " + Format.Size(u.UpMonth); }
    }
}

public sealed class VolumesCard : Card
{
    private readonly StackPanel rows = new();
    private readonly Dictionary<string, (TextBlock name, TextBlock free, Meter bar, TextBlock detail)> map = new();

    public VolumesCard(Palette p, Metrics m, Settings s) : base(p, m, s, Glyphs.Volume, T("Storage space"))
    {
        rows.Spacing = m.Gap;
        Body.Children.Add(rows);
    }

    public override void Update(Snapshot s, History h)
    {
        // Volumes switched off under Customise are left out.
        var volumes = s.Volumes.Where(v => S.Shows("volume:" + v.Name)).ToList();
        foreach (var gone in map.Keys.Except(volumes.Select(v => v.Name)).ToList())
        {
            rows.Children.Remove(map[gone].name.Parent!.Parent as Control);
            map.Remove(gone);
        }
        foreach (var v in volumes)
        {
            if (!map.TryGetValue(v.Name, out var r))
            {
                var name = Ui.Text("", 12, P.Ink, FontWeight.Medium);
                var free = Ui.Text("", 12, P.Ink, FontWeight.SemiBold, true);
                var bar = new Meter { Height = 5, Palette = P };
                var detail = Ui.Text("", 11.5, P.Soft, numbers: true);
                detail.IsVisible = Shows("volumes.detail");
                free.IsVisible = Shows("volumes.free");
                bar.IsVisible = Shows("volumes.bar");
                rows.Children.Add(Ui.Stack(5, Ui.Columns("*,Auto", name, free), bar, detail));
                map[v.Name] = r = (name, free, bar, detail);
            }
            r.name.Text = string.IsNullOrWhiteSpace(v.Label) || !Shows("volumes.label") ? v.Name : $"{v.Name}  {v.Label}";
            Motion.Text(r.free, F("{0} free", Format.Capacity(v.FreeGb)));
            double share = v.UsedPercent / 100;
            var c = share >= .9 ? P.Ramp[^1] : share >= .8 ? P.Ramp[2] : P.Cool;
            r.free.Foreground = Palette.Brush(share >= .8 ? c : P.Ink);
            r.bar.Set(share, c);
            r.detail.Text = F("{0} of {1} used", Format.Capacity(v.TotalGb - v.FreeGb), Format.Capacity(v.TotalGb));
        }
    }
}

public sealed class SystemCard : Card
{
    private readonly Hero hero; private readonly Dictionary<string, Stat> stats;

    public SystemCard(Palette p, Metrics m, Settings s) : base(p, m, s, Glyphs.System, T("System"))
    {
        hero = new Hero(M.Hero * .8, p);
        var label = Label(T("Uptime"), p);
        Body.Children.Add(Ui.Stack(0, label, hero));
        stats = Stats(("system.boot", T("Started")), ("system.processes", T("Processes")), ("system.threads", T("Threads")));
    }

    public override void Update(Snapshot s, History h)
    {
        var y = s.System;
        Subtitle = y is null ? "" : $"{y.Os} · {y.Build}";
        string d = y is null ? Format.Missing : Format.Duration(y.Uptime);
        int space = d.IndexOf(' ');
        hero.Set(space < 0 ? d : d[..space], space < 0 ? "" : d[(space + 1)..], P.Ink);
        if (y == null) return;
        Put(stats, "system.boot", y.BootTime.ToString(y.Uptime.TotalDays >= 1 ? "ddd HH:mm" : "HH:mm", L.Culture));
        Put(stats, "system.processes", Format.Number(y.Processes));
        Put(stats, "system.threads", Format.Number(y.Threads));
    }
}

public sealed class WorldClocksCard : Card
{
    private readonly StackPanel rows = new() { Spacing = 8 };
    private readonly List<(TimeZoneInfo zone, TextBlock time, TextBlock info, Border dot)> clocks = new();

    public WorldClocksCard(Palette p, Metrics m, Settings s) : base(p, m, s, Glyphs.Globe, T("World clocks"))
    {
        foreach (var id in s.WorldClocks)
        {
            if (!TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone)) continue;
            var name = Ui.Text(City(id), 13, p.Ink, FontWeight.Medium);
            var info = Ui.Text("", 11.5, p.Soft, numbers: true);
            var dot = new Border { Width = 7, Height = 7, CornerRadius = new CornerRadius(4), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            var time = Ui.Text("", 20, p.Ink, FontWeight.Light, true);
            var left = Ui.Stack(0, name, info);
            rows.Children.Add(Ui.Columns("Auto,*,Auto", dot, left, time));
            clocks.Add((zone, time, info, dot));
        }
        if (clocks.Count == 0) rows.Children.Add(Ui.Text(T("Choose cities in the settings."), 12, p.Soft));
        Body.Children.Add(rows);
    }

    public static string City(string id) => id[(id.LastIndexOf('/') + 1)..].Replace('_', ' ');

    public override void Update(Snapshot s, History h)
    {
        var localOffset = TimeZoneInfo.Local.GetUtcOffset(s.Time);
        var utc = s.Time - localOffset;
        foreach (var (zone, time, info, dot) in clocks)
        {
            var there = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone);
            time.Text = there.ToString("HH:mm", L.Culture);
            double diff = (zone.GetUtcOffset(utc) - localOffset).TotalHours;
            string offset = diff == 0 ? "±0 h" : (diff > 0 ? "+" : "−") + Math.Abs(diff).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " h";
            string day = there.Date > s.Time.Date ? T("tomorrow") : there.Date < s.Time.Date ? T("yesterday") : T("today");
            info.Text = $"{day} · {offset}";
            bool daytime = there.Hour is >= 7 and < 19;
            dot.Background = Palette.Brush(daytime ? P.Ramp[2] : P.Cool);
        }
    }
}

public sealed class CalendarCard : Card
{
    private readonly Grid grid = new();
    private DateTime shown;

    public CalendarCard(Palette p, Metrics m, Settings s) : base(p, m, s, Glyphs.Calendar, "")
    {
        Body.Children.Add(grid);
    }

    public override void Update(Snapshot s, History h)
    {
        var today = s.Time.Date;
        if (today == shown) return;
        shown = today;
        var first = new DateTime(today.Year, today.Month, 1);
        string month = first.ToString("MMMM yyyy", L.Culture);
        Title = char.ToUpper(month[0], L.Culture) + month[1..];
        Aside = T("Week") + " " + System.Globalization.ISOWeek.GetWeekOfYear(today);
        grid.Children.Clear();
        grid.ColumnDefinitions = ColumnDefinitions.Parse("22,*,*,*,*,*,*,*");
        grid.RowDefinitions.Clear();
        var start = first.AddDays(-(((int)first.DayOfWeek + 6) % 7));
        int weeks = (int)Math.Ceiling(((first - start).TotalDays + DateTime.DaysInMonth(today.Year, today.Month)) / 7);
        for (int r = 0; r <= weeks; r++) grid.RowDefinitions.Add(new RowDefinition(r == 0 ? GridLength.Auto : new GridLength(M.Subtitles ? 24 : 20)));
        for (int c = 0; c < 7; c++)
        {
            var name = L.Culture.DateTimeFormat.AbbreviatedDayNames[(c + 1) % 7];
            var l = Label(name.TrimEnd('.')[..Math.Min(2, name.TrimEnd('.').Length)], P);
            l.HorizontalAlignment = HorizontalAlignment.Center;
            l.Margin = new Thickness(0, 0, 0, 4);
            Place(l, 0, c + 1);
        }
        for (int w = 0; w < weeks; w++)
        {
            var monday = start.AddDays(w * 7);
            var kw = Ui.Text(System.Globalization.ISOWeek.GetWeekOfYear(monday).ToString(), 10, P.Faint, numbers: true);
            kw.HorizontalAlignment = HorizontalAlignment.Left;
            Place(kw, w + 1, 0);
            for (int d = 0; d < 7; d++)
            {
                var day = monday.AddDays(d);
                bool inMonth = day.Month == today.Month, isToday = day == today;
                var text = Ui.Text(day.Day.ToString(), 12, isToday ? (P.Dark ? Color.Parse("#0B0E14") : Colors.White) : !inMonth ? P.Faint : d >= 5 ? P.Soft : P.Ink,
                    isToday ? FontWeight.Bold : FontWeight.Normal, true);
                text.HorizontalAlignment = HorizontalAlignment.Center;
                Control cell = text;
                if (isToday)
                    cell = new Border { Background = Palette.Brush(P.Cool), CornerRadius = new CornerRadius(10), Width = 22, Height = 20, Child = text, HorizontalAlignment = HorizontalAlignment.Center };
                Place(cell, w + 1, d + 1);
            }
        }
    }

    private void Place(Control c, int row, int col)
    {
        Grid.SetRow(c, row);
        Grid.SetColumn(c, col);
        grid.Children.Add(c);
    }
}

public sealed class NotesCard : Card
{
    public static event Action<string>? Changed;

    public NotesCard(Palette p, Metrics m, Settings s) : base(p, m, s, Glyphs.Notes, T("Notes"))
    {
        var box = new TextBox
        {
            Text = s.Notes,
            Watermark = T("Write a note…"),
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 64,
            MaxHeight = 220,
            FontFamily = Ui.Font,
            FontStretch = Ui.FontWidth,
            FontSize = 12.5,
            Foreground = Palette.Brush(p.Ink),
            Background = Palette.Brush(p.Track),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 6)
        };
        box.Resources["TextControlBackgroundPointerOver"] = Palette.Brush(p.Track);
        box.Resources["TextControlBackgroundFocused"] = Palette.Brush(p.Track);
        box.Resources["TextControlForegroundFocused"] = Palette.Brush(p.Ink);
        box.Resources["TextControlBorderBrushFocused"] = Palette.Brush(Palette.Alpha(p.Cool, .6));
        box.LostFocus += (_, _) => { s.Notes = box.Text ?? ""; Changed?.Invoke(s.Notes); };
        Body.Children.Add(box);
    }

    public override void Update(Snapshot s, History h) { }
}

public sealed class MediaCard : Card
{
    public static event Action<string>? Command;

    private readonly TextBlock title, artist, time, empty; private readonly Meter progress;
    private readonly Border cover; private readonly Button play; private readonly Control content;
    private byte[]? lastCover;
    private bool coverShown;

    public MediaCard(Palette p, Metrics m, Settings s) : base(p, m, s, Glyphs.Music, T("Now playing"))
    {
        cover = new Border { Width = 56, Height = 56, CornerRadius = new CornerRadius(10), ClipToBounds = true };
        title = Ui.Text("", 13.5, p.Ink, FontWeight.SemiBold);
        artist = Ui.Text("", 12, p.Soft);
        var info = Ui.Stack(1, title, artist);
        info.VerticalAlignment = VerticalAlignment.Center;
        info.Margin = new Thickness(12, 0, 0, 0);
        progress = new Meter { Height = 4, Palette = p };
        time = Ui.Text("", 11, p.Soft, numbers: true);
        Button Ctl(string glyph, string cmd, double size)
        {
            var b = new Button
            {
                Content = Ui.Glyph(glyph, p.Ink, size), Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                Padding = new Thickness(8, 4), CornerRadius = new CornerRadius(8), Cursor = new Cursor(StandardCursorType.Hand)
            };
            b.Click += (_, _) => Command?.Invoke(cmd);
            return b;
        }
        play = Ctl("M5,3 L13,8 L5,13 Z", "toggle", 15);
        var controls = Ui.Row(2, Ctl("M12,3 V13 M11,3 L4,8 L11,13 Z", "previous", 13), play, Ctl("M4,3 V13 M5,3 L12,8 L5,13 Z", "next", 13));
        controls.HorizontalAlignment = HorizontalAlignment.Right;
        var panel = Ui.Stack(M.Gap, Ui.Columns("Auto,*", cover, info), progress, Ui.Columns("*,Auto", time, controls));
        content = panel;
        empty = Ui.Text(T("Nothing is playing."), 12, p.Soft);
        Body.Children.Add(content);
        Body.Children.Add(empty);
    }

    public override void Update(Snapshot s, History h)
    {
        var media = s.Media;
        content.IsVisible = media != null;
        empty.IsVisible = media == null;
        if (media == null) { Aside = ""; return; }
        Aside = media.App;
        title.Text = media.Title;
        artist.Text = media.Artist;
        play.Content = Ui.Glyph(media.Playing ? "M5,3 V13 M11,3 V13" : "M5,3 L13,8 L5,13 Z", P.Ink, 15);
        if (media.DurationSeconds is double total && total > 0 && media.PositionSeconds is double pos)
        {
            progress.IsVisible = true;
            progress.Set(pos / total, P.Cool);
            time.Text = $"{Clock(pos)} / {Clock(total)}";
        }
        else { progress.IsVisible = false; time.Text = ""; }
        if (!coverShown || !ReferenceEquals(media.Cover, lastCover))
        {
            coverShown = true;
            lastCover = media.Cover;
            cover.Child = null;
            cover.Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(P.Ramp[0], 0), new GradientStop(Palette.Mix(P.Ramp[0], P.Ramp[3], .55), 1) }
            };
            if (media.Cover is { Length: > 0 } bytes)
            {
                try { cover.Child = new Image { Source = new Bitmap(new MemoryStream(bytes)), Stretch = Stretch.UniformToFill }; }
                catch { }
            }
            if (cover.Child == null)
            {
                var note = Ui.Glyph(Glyphs.Music, Colors.White, 24);
                note.HorizontalAlignment = HorizontalAlignment.Center;
                cover.Child = note;
            }
        }
    }

    private static string Clock(double seconds) => $"{(int)(seconds / 60)}:{(int)(seconds % 60):00}";
}
