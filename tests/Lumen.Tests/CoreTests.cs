using Lumen.Core;
using Xunit;

public class FormatTests
{
    [Theory]
    [InlineData(0d, "0 B/s")]
    [InlineData(940d * 1024, "940 KB/s")]
    [InlineData(1.4 * 1024 * 1024, "1.4 MB/s")]
    [InlineData(112d * 1024 * 1024, "112 MB/s")]
    [InlineData(-5d, "0 B/s")]
    public void Rate(double value, string expected) => Assert.Equal(expected, Format.Rate(value));

    [Fact] public void MissingValuesShowDash()
    {
        Assert.Equal(Format.Missing, Format.Temperature(null, false));
        Assert.Equal(Format.Missing, Format.Rate(double.NaN));
        Assert.Equal(Format.Missing, Format.Clock(0));
    }

    [Fact] public void Units()
    {
        Assert.Equal("62", Format.Temperature(62.4, false));
        Assert.Equal("144", Format.Temperature(62.4, true));
        Assert.Equal("4.10 GHz", Format.Clock(4100));
        Assert.Equal("800 MHz", Format.Clock(800));
        Assert.Equal("10 Gbit/s", Format.Link(10e9));
        Assert.Equal("2.5 Gbit/s", Format.Link(2.5e9));
        Assert.Equal("1000 Mbit/s", Format.Link(999.6e6));
        Assert.Equal(("1.4", "MB/s"), Format.Split("1.4 MB/s"));
    }

    [Fact] public void HeatIsClamped()
    {
        Assert.Equal(0, Format.Heat(10));
        Assert.Equal(1, Format.Heat(120));
        Assert.Equal(0, Format.Heat(null));
    }
}

public class HistoryTests
{
    [Fact] public void KeepsOrderAndCapacity()
    {
        var h = new History();
        for (int i = 0; i < History.Capacity + 5; i++) h.Add("x", i);
        var v = h.Read("x");
        Assert.Equal(History.Capacity, v.Length);
        Assert.Equal(5, v[0]);
        Assert.Equal(History.Capacity + 4, v[^1]);
    }

    [Fact] public void GapsStayGaps()
    {
        var h = new History();
        h.Add("x", 1); h.Add("x", null); h.Add("x", 3);
        Assert.True(double.IsNaN(h.Read("x")[1]));
        Assert.Empty(h.Read("unknown"));
    }

    [Fact] public void RecordsDemoSnapshot()
    {
        var h = new History();
        using var demo = new DemoSource();
        h.Record(demo.Read());
        Assert.Single(h.Read("cpu.temp"));
        Assert.Single(h.Read("net.down"));
    }
}

public class SettingsTests
{
    [Fact] public void NormalizeClampsAndCompletesWidgets()
    {
        var s = Settings.Normalize(new Settings
        {
            Width = 9000, IntervalMs = 1, TemperatureWarning = double.NaN, Screen = -3,
            Widgets = new() { new() { Kind = WidgetKind.Network }, new() { Kind = WidgetKind.Network, Visible = false } }
        });
        Assert.Equal(440, s.Width);
        Assert.Equal(500, s.IntervalMs);
        Assert.Equal(85, s.TemperatureWarning);
        Assert.Equal(0, s.Screen);
        Assert.Equal(Enum.GetValues<WidgetKind>().Length, s.Widgets.Count);
        var network = s.Widgets.Single(w => w.Kind == WidgetKind.Network);
        Assert.True(network.Visible);
    }

    [Fact] public void RoundTripsThroughJson()
    {
        string path = Path.Combine(Path.GetTempPath(), $"lumen-{Guid.NewGuid():N}.json");
        try
        {
            var s = new Settings { Edge = Edge.Left, Density = Density.Compact, Fahrenheit = true, Width = 320 };
            s.Widgets.Reverse();
            s.Save(path);
            var back = Settings.Load(path);
            Assert.Equal(Edge.Left, back.Edge);
            Assert.Equal(Density.Compact, back.Density);
            Assert.True(back.Fahrenheit);
            Assert.Equal(320, back.Width);
            Assert.Equal(Enum.GetValues<WidgetKind>()[^1], back.Widgets[0].Kind);
            Assert.Contains("\"Compact\"", File.ReadAllText(path));
        }
        finally { File.Delete(path); }
    }

    [Fact] public void BrokenFileFallsBackToDefaults()
    {
        string path = Path.Combine(Path.GetTempPath(), $"lumen-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, "{ not json");
        try { Assert.Equal(300, Settings.Load(path).Width); }
        finally { File.Delete(path); }
    }
}

public class DemoTests
{
    [Fact] public void IsDeterministicAndPlausible()
    {
        using var a = new DemoSource(); using var b = new DemoSource();
        for (int i = 0; i < 200; i++)
        {
            var x = a.Read(); var y = b.Read();
            Assert.Equal(x.Cpu!.Temperature, y.Cpu!.Temperature);
            Assert.InRange(x.Cpu.Load!.Value, 0, 100);
            Assert.All(x.Cpu.CoreLoads, l => Assert.InRange(l, 0, 100));
            Assert.InRange(x.Memory!.Load, 0, 100);
            Assert.True(x.Network!.DownloadBytesPerSecond >= 0);
        }
    }
}

public class NewFeatureTests
{
    [Fact] public void OldSettingsGainNewWidgetsInPlace()
    {
        // A file from the first version only knows the original six widgets, in a custom order.
        var s = Settings.Normalize(new Settings
        {
            Widgets = new() { new() { Kind = WidgetKind.Network }, new() { Kind = WidgetKind.Clock }, new() { Kind = WidgetKind.Cpu }, new() { Kind = WidgetKind.Gpu }, new() { Kind = WidgetKind.Memory }, new() { Kind = WidgetKind.Drives } }
        });
        Assert.Equal(Enum.GetValues<WidgetKind>().Length, s.Widgets.Count);
        Assert.Equal(WidgetKind.Network, s.Widgets[0].Kind);
        Assert.False(s.Widgets.Single(w => w.Kind == WidgetKind.Weather).Visible);
        Assert.False(s.Widgets.Single(w => w.Kind == WidgetKind.Processes).Visible);
        Assert.True(s.Widgets.Single(w => w.Kind == WidgetKind.Cooling).Visible);
        Assert.True(s.Widgets.FindIndex(w => w.Kind == WidgetKind.Cooling) > s.Widgets.FindIndex(w => w.Kind == WidgetKind.Gpu));
    }

    [Fact] public void NormalizeCleansNewFields()
    {
        var s = Settings.Normalize(new Settings { Colors = "pink", Language = "xx", Opacity = 7, TextScale = 0, HiddenValues = new() { "cpu.fan", "cpu.fan", "" } });
        Assert.Equal(Settings.StateColors, s.Colors);
        Assert.Equal("auto", s.Language);
        Assert.Equal(1, s.Opacity);
        Assert.Equal(.85, s.TextScale);
        Assert.Equal(new[] { "cpu.fan" }, s.HiddenValues);
        Assert.Equal("#8B5CF6", Settings.Normalize(new Settings { Colors = "#8B5CF6" }).Colors);
        Assert.Equal(Density.Fit, new Settings().Density);
    }

    private static Snapshot Hot(double cpu, DateTime t) => new DemoSource().Read() with { Time = t, Cpu = new CpuInfo("CPU", cpu, 50, Array.Empty<double>(), null, null, null, null), Gpus = Array.Empty<GpuInfo>(), Disks = Array.Empty<DiskInfo>() };

    [Fact] public void WarningFiresOnceUntilCooled()
    {
        var w = new WarningTracker();
        var t = new DateTime(2026, 1, 1, 12, 0, 0);
        Assert.Empty(w.Check(Hot(80, t), 85));
        Assert.Single(w.Check(Hot(90, t.AddSeconds(1)), 85));
        Assert.Empty(w.Check(Hot(91, t.AddSeconds(2)), 85));
        Assert.Empty(w.Check(Hot(83, t.AddSeconds(3)), 85));   // not yet 5 °C below
        Assert.Empty(w.Check(Hot(79, t.AddSeconds(4)), 85));   // re-armed
        Assert.Empty(w.Check(Hot(90, t.AddMinutes(2)), 85));   // still in cooldown
        Assert.Single(w.Check(Hot(90, t.AddMinutes(6)), 85));
    }

    [Fact] public void ParsesOpenMeteo()
    {
        const string json = """
        {"current":{"time":"2026-10-31T10:00","temperature_2m":9.4,"apparent_temperature":7.1,"relative_humidity_2m":78,"weather_code":61,"wind_speed_10m":11.2,"is_day":0},
         "daily":{"time":["2026-10-31","2026-11-01"],"weather_code":[61,3],"temperature_2m_max":[11.8,null],"temperature_2m_min":[5.2,4.1]}}
        """;
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var w = WeatherClient.Parse(doc.RootElement, "Zürich", new DateTime(2026, 10, 31))!;
        Assert.Equal(9.4, w.TemperatureC);
        Assert.False(w.IsDay);
        Assert.Equal("rain", WeatherClient.Group(w.Code));
        Assert.Single(w.Days);
        using var place = System.Text.Json.JsonDocument.Parse("""{"results":[{"name":"Zürich","latitude":47.37,"longitude":8.55,"admin1":"Zürich","country":"Schweiz"}]}""");
        var p = WeatherClient.ParsePlace(place.RootElement)!;
        Assert.Equal("Schweiz", p.Region);
        using var none = System.Text.Json.JsonDocument.Parse("{}");
        Assert.Null(WeatherClient.ParsePlace(none.RootElement));
    }

    [Theory]
    [InlineData(0, "clear")] [InlineData(3, "overcast")] [InlineData(45, "fog")] [InlineData(53, "drizzle")]
    [InlineData(66, "freezing")] [InlineData(75, "snow")] [InlineData(81, "showers")] [InlineData(96, "storm")]
    public void WeatherGroups(int code, string group) => Assert.Equal(group, WeatherClient.Group(code));

    [Fact] public void EveryTextIsTranslated()
    {
        var src = File.ReadAllText(Path.Combine(FindRoot(), "src", "Lumen.App", "Strings.cs"));
        var lines = src[(src.IndexOf("\"\"\"") + 3)..src.LastIndexOf("\"\"\"")].Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Trim().Length > 0).ToList();
        Assert.All(lines, l => Assert.Equal(6, l.Split('|').Length));
        Assert.Equal(lines.Count, lines.Select(l => l.Split('|')[0]).Distinct().Count());
        // Every T("...") and F("...") key used in the app exists in the table.
        var keys = lines.Select(l => l.Split('|')[0]).ToHashSet();
        var used = Directory.GetFiles(Path.Combine(FindRoot(), "src", "Lumen.App"), "*.cs")
            .SelectMany(f => System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(f), @"\b[TF]\(""((?:[^""\\]|\\.)*)""").Select(m => m.Groups[1].Value))
            .Distinct().ToList();
        Assert.Equal(Array.Empty<string>(), used.Where(k => !keys.Contains(k)).ToArray());
        Lumen.App.L.Use("de");
        Assert.Equal("Prozessor", Lumen.App.L.T("Processor"));
        Lumen.App.L.Use("en");
        Assert.Equal("Processor", Lumen.App.L.T("Processor"));
    }

    private static string FindRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d != null && !File.Exists(Path.Combine(d.FullName, "Directory.Build.props"))) d = d.Parent;
        return d!.FullName;
    }
}

public class ColumnsAndUsageTests
{
    [Fact] public void TwoColumnsBalanceByHeightAndHonourPins()
    {
        var s = Settings.Normalize(new Settings { Columns = 2 });
        var cols = s.Layout();
        Assert.Equal(2, cols.Count);
        Assert.Equal(s.Widgets.Count(w => w.Visible), cols.Sum(c => c.Count));
        double a = cols[0].Sum(Settings.Weight), b = cols[1].Sum(Settings.Weight);
        Assert.True(Math.Abs(a - b) <= 2.1, $"{a} vs {b}");
        s.Widgets.Single(w => w.Kind == WidgetKind.Clock).Column = 2;
        Assert.Contains(s.Layout()[1], w => w.Kind == WidgetKind.Clock);
        Assert.Single(new Settings().Layout());
    }

    [Fact] public void NormalizeKeepsStylesOnlyWhereSupported()
    {
        var s = new Settings();
        s.Widgets.Single(w => w.Kind == WidgetKind.Clock).Style = WidgetStyle.Ring;
        s.Widgets.Single(w => w.Kind == WidgetKind.Cpu).Style = WidgetStyle.Ring;
        s.Widgets.Single(w => w.Kind == WidgetKind.Cpu).Column = 9;
        Settings.Normalize(s);
        Assert.Equal(WidgetStyle.Standard, s.Widgets.Single(w => w.Kind == WidgetKind.Clock).Style);
        Assert.Equal(WidgetStyle.Ring, s.Widgets.Single(w => w.Kind == WidgetKind.Cpu).Style);
        Assert.Equal(2, s.Widgets.Single(w => w.Kind == WidgetKind.Cpu).Column);
        Assert.False(s.Widgets.Single(w => w.Kind == WidgetKind.Media).Visible);
    }

    [Fact] public void UsageAddsUpAndSurvivesRestart()
    {
        string path = Path.Combine(Path.GetTempPath(), $"lumen-usage-{Guid.NewGuid():N}.json");
        try
        {
            var t = new DateTime(2026, 10, 31, 12, 0, 0);
            Snapshot At(int sec) => new Snapshot(t.AddSeconds(sec), new CpuInfo("c", 50, 10, Array.Empty<double>(), null, 100, null, null),
                Array.Empty<GpuInfo>(), null, Array.Empty<DiskInfo>(), new NetworkInfo("n", 1000, 500, null, null), SensorAccess.Full);
            var meter = new UsageMeter(path);
            UsageInfo u = meter.Record(At(0), 50);
            for (int i = 1; i <= 3600; i++) u = meter.Record(At(i), 50);
            Assert.Equal(150, u.WattsNow);
            Assert.Equal(.15, u.KwhToday, 3);
            Assert.Equal(3_600_000, u.DownToday, 0);
            meter.Record(At(3700), 50);        // a 100 s gap (sleep) is not counted
            meter.Save(t);
            var again = new UsageMeter(path).Summarize(t, 0);
            Assert.Equal(.15, again.KwhMonth, 3);
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData(16e6, "16.0 Mbit/s")] [InlineData(676e6, "676 Mbit/s")] [InlineData(1.25e9, "1.25 Gbit/s")] [InlineData(800e3, "800 kbit/s")]
    public void Bits(double v, string expected) => Assert.Equal(expected, Format.Bits(v));

    [Fact] public void Durations()
    {
        Assert.Equal("2 d 3 h", Format.Duration(TimeSpan.FromHours(51.3)));
        Assert.Equal("3 h 12 m", Format.Duration(TimeSpan.FromMinutes(192)));
        Assert.Equal("1.82 TB", Format.Capacity(1863));
        Assert.Equal("CHF 0.55", Format.Money(.552, "CHF"));
    }
}

public class NameTests
{
    [Theory]
    [InlineData("AMD Ryzen 9 5900X 12-Core Processor", "Ryzen 9 5900X")]
    [InlineData("NVIDIA GeForce RTX 3080", "RTX 3080")]
    [InlineData("Intel(R) Core(TM) i7-9700K CPU @ 3.60GHz", "i7-9700K")]
    [InlineData("13th Gen Intel(R) Core(TM) i9-13900K", "13th Gen Intel Core i9-13900K")]
    [InlineData("Intel(R) Ethernet Server Adapter X520-2", "Ethernet X520-2")]
    [InlineData("ASUS ROG STRIX X570-E GAMING", "ROG STRIX X570-E GAMING")]
    [InlineData(null, "")]
    public void Shortens(string? input, string expected) => Assert.Equal(expected, Lumen.App.Names.Short(input));
}

public class MergeTests
{
    private static GpuInfo Gpu(string name, double? temp = null, double? load = null, double? hot = null) => new("id-" + name, name, temp, hot, load, null, null, null, null, null);

    [Fact] public void FillsOnlyMissingGpuValues()
    {
        var log = new List<string>();
        var merged = SensorMerge.Gpus(new[] { Gpu("NVIDIA GeForce RTX 3080", temp: 60, load: null) }, new[] { Gpu("NVIDIA GeForce RTX 3080", temp: 99, load: 40, hot: 75) }, log);
        Assert.Single(merged);
        Assert.Equal(60, merged[0].Temperature);   // first source wins
        Assert.Equal(40, merged[0].Load);          // gap filled
        Assert.Equal(75, merged[0].HotSpot);
        Assert.Contains(log, l => l.Contains("Load") && l.Contains("HotSpot"));
    }

    [Fact] public void AddsHardwareOnlyTheSecondSourceSees()
    {
        var merged = SensorMerge.Gpus(new[] { Gpu("Intel UHD Graphics 770") with { Integrated = true } }, new[] { Gpu("AMD Radeon RX 7900 XTX", temp: 55) });
        Assert.Equal(2, merged.Count);
        Assert.Contains(merged, g => g.Name.Contains("7900") && g.Temperature == 55);
    }

    [Fact] public void SingleDedicatedCardsPairEvenWithDifferentNames()
    {
        var merged = SensorMerge.Gpus(new[] { Gpu("NVIDIA GeForce RTX 4070") }, new[] { Gpu("GeForce RTX 4070 Laptop", temp: 50) });
        Assert.Single(merged);
        Assert.Equal(50, merged[0].Temperature);
    }

    [Fact] public void IdenticalDrivesArePairedOneByOne()
    {
        DiskInfo D(string id, double? t) => new(id, "Samsung SSD 980 PRO 2TB", t, null, null, null);
        var merged = SensorMerge.Disks(new[] { D("a", null), D("b", null) }, new[] { D("x", 41), D("y", 43), new DiskInfo("z", "WDC WD40EFRX", 35, 60, null, null) });
        Assert.Equal(3, merged.Count);
        Assert.Equal(41, merged[0].Temperature);
        Assert.Equal(43, merged[1].Temperature);
        Assert.Equal("WDC WD40EFRX", merged[2].Name);
    }

    [Fact] public void DescribesSnapshot()
    {
        using var demo = new DemoSource();
        var text = SnapshotText.Describe(demo.Read());
        Assert.Contains("CPU  AMD Ryzen 9 5900X", text);
        Assert.Contains("GPU  NVIDIA GeForce RTX 3080", text);
    }
}
