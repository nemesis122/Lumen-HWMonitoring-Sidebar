using System.IO.Compression;
using Lumen.App;
using Lumen.Core;
using Xunit;

public class ThemeFileTests
{
    [Fact]
    public void A_theme_round_trips_and_touches_only_the_look()
    {
        var source = new Settings { Theme = ThemeMode.Light, Colors = "#F97316", Background = "mocha", Glass = false, Opacity = .8, HighContrast = false, TextScale = 1.25, Font = "bahnschrift-narrow", SmoothTransitions = true };
        string json = ThemeFile.From(source, "Sunset").ToJson();

        var target = new Settings { Edge = Edge.Bottom, Columns = 2, Notes = "keep me", WeatherPlace = "Zürich" };
        var theme = ThemeFile.Parse(json);
        Assert.NotNull(theme);
        Assert.Equal("Sunset", theme!.Name);
        theme.ApplyTo(target);

        Assert.Equal(ThemeMode.Light, target.Theme);
        Assert.Equal("#F97316", target.Colors);
        Assert.Equal("mocha", target.Background);
        Assert.False(target.Glass);
        Assert.Equal(.8, target.Opacity);
        Assert.Equal(1.25, target.TextScale);
        Assert.Equal("bahnschrift-narrow", target.Font);
        Assert.True(target.SmoothTransitions);
        // Layout and personal entries stay as they were.
        Assert.Equal(Edge.Bottom, target.Edge);
        Assert.Equal(2, target.Columns);
        Assert.Equal("keep me", target.Notes);
        Assert.Equal("Zürich", target.WeatherPlace);
        Assert.DoesNotContain("Notes", json);
        Assert.DoesNotContain("Widgets", json);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"type\":\"something-else\",\"version\":1}")]
    [InlineData("{\"Theme\":\"Dark\"}")]
    public void Anything_else_is_refused(string text) => Assert.Null(ThemeFile.Parse(text));

    [Fact]
    public void Out_of_range_values_are_clamped()
    {
        var t = ThemeFile.Parse("{\"type\":\"lumen-theme\",\"Version\":1,\"Opacity\":7,\"TextScale\":-3,\"Colors\":\"red\",\"Font\":\"Comic Sans\"}")!;
        var s = new Settings();
        t.ApplyTo(s);
        Assert.Equal(1, s.Opacity);
        Assert.Equal(.5, s.TextScale);
        Assert.Equal(Settings.StateColors, s.Colors);
        Assert.Equal("inter", s.Font);
    }

    [Fact]
    public void Unknown_font_in_settings_falls_back()
    {
        var s = new Settings { Font = " MONO " };
        Settings.Normalize(s);
        Assert.Equal("mono", s.Font);
        s.Font = "wingdings";
        Settings.Normalize(s);
        Assert.Equal("inter", s.Font);
    }
}

public class GuardTests
{
    private static string TempFile() => Path.Combine(Path.GetTempPath(), "lumen-test-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void At_most_three_restarts_in_half_an_hour()
    {
        string path = TempFile();
        try
        {
            var watch = new FreezeWatch(() => { }, path);
            var t = new DateTime(2026, 11, 1, 22, 0, 0);
            Assert.True(watch.AllowRestart(t));
            Assert.True(watch.AllowRestart(t.AddMinutes(5)));
            Assert.True(watch.AllowRestart(t.AddMinutes(10)));
            Assert.False(watch.AllowRestart(t.AddMinutes(15)));
            // Once the first one is older than 30 minutes there is room again.
            Assert.True(watch.AllowRestart(t.AddMinutes(31)));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void The_report_leaves_personal_entries_out()
    {
        var s = new Settings { Notes = "PIN 1234", WeatherPlace = "Bern", WeatherLatitude = 46.9, WeatherLongitude = 7.4, PingTarget = "192.168.1.10" };
        s.Widgets[0].Name = "Peters PC";
        var a = ErrorReport.Anonymous(s);
        Assert.Equal("(removed)", a.Notes);
        Assert.Equal("(removed)", a.WeatherPlace);
        Assert.Equal(0, a.WeatherLatitude);
        Assert.Equal("(removed)", a.PingTarget);
        Assert.Equal("(own name)", a.Widgets[0].Name);
        // The real settings are untouched.
        Assert.Equal("PIN 1234", s.Notes);
        Assert.Equal("Peters PC", s.Widgets[0].Name);
    }

    [Fact]
    public void The_report_is_one_zip_with_four_files()
    {
        string dir = TempFile();
        try
        {
            string zip = ErrorReport.Create(new Settings { Notes = "secret" }, "=== LibreHardwareMonitor ===\n[Cpu] Test", new[] { "Screen 1 · 1920×1080" }, dir);
            using var archive = ZipFile.OpenRead(zip);
            Assert.Equal(new[] { "lumen-crash.log", "sensors.txt", "settings.json", "system.txt" }, archive.Entries.Select(e => e.Name).OrderBy(n => n));
            string settings = new StreamReader(archive.GetEntry("settings.json")!.Open()).ReadToEnd();
            Assert.DoesNotContain("secret", settings);
            string system = new StreamReader(archive.GetEntry("system.txt")!.Open()).ReadToEnd();
            Assert.Contains("1920×1080", system);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
