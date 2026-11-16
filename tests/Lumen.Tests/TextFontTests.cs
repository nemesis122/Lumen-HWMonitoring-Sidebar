using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Lumen.App;
using Lumen.Core;
using Xunit;

public class TextFontTests
{
    private sealed class TestPlatform : Lumen.App.Platform
    {
        public override bool IsAdministrator => false;
        public override bool AutostartEnabled => false;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Font_picker_text_remains_readable_after_selection_in_both_themes(bool dark) => UiHost.Run(() =>
    {
        var settings = Settings.Starter(); settings.Font = "rock-salt";
        var palette = Palette.For(dark, false);
        var settingsWindow = new SettingsWindow(settings, palette, () => { }, () => new[] { "Demo" }, new TestPlatform());
        var host = new Window
        {
            Width = 600, Height = 480, Background = Palette.Brush(palette.Card),
            RequestedThemeVariant = dark ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light
        };
        try
        {
            var method = typeof(SettingsWindow).GetMethod("FontChoice", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var rows = Ui.Stack(20);
            foreach (bool inherit in new[] { false, true })
            {
                var picker = (ComboBox)method.Invoke(settingsWindow, new object[] { inherit ? "" : settings.Font, (Action<string>)(_ => { }), inherit })!;
                rows.Children.Add(picker);
                foreach (var item in picker.Items.Cast<ComboBoxItem>())
                    Assert.Equal(palette.Ink, Assert.IsType<SolidColorBrush>(Assert.IsType<TextBlock>(item.Content).Foreground).Color);
                picker.SelectedIndex = Array.IndexOf(Settings.Fonts, "rock-salt") + (inherit ? 1 : 0);
                Assert.Contains("Rock Salt", Assert.IsType<TextBlock>(Assert.IsType<ComboBoxItem>(picker.SelectedItem).Content).Text!);
            }
            host.Content = new Border { Padding = new Avalonia.Thickness(24), Child = rows };
            host.Show(); Dispatcher.UIThread.RunJobs();
            using var image = host.CaptureRenderedFrame(); Assert.NotNull(image);
            var path = Environment.GetEnvironmentVariable("LUMEN_FONT_PREVIEW_DIR");
            if (!string.IsNullOrEmpty(path)) image!.Save(System.IO.Path.Combine(path, dark ? "Schriftauswahl-Dunkel-Fix.png" : "Schriftauswahl-Hell-Fix.png"));
        }
        finally { host.Close(); settingsWindow.Close(); Ui.UseFont("inter"); }
        return true;
    });

    [Fact]
    public void Settings_font_picker_changes_only_its_group_and_reset_restores_inheritance() => UiHost.Run(() =>
    {
        var settings = Settings.Starter(); settings.ValueFont = "jetbrains-mono"; settings.GraphFont = "rajdhani";
        Ui.UseFonts(settings);
        int changes = 0;
        var window = new SettingsWindow(settings, Palette.For(true, false), () => changes++, () => new[] { "Demo" }, new TestPlatform());
        try
        {
            var choice = typeof(SettingsWindow).GetMethod("FontChoice", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var picker = (ComboBox)choice.Invoke(window, new object[] { "", (Action<string>)(key => settings.TitleFont = key), true })!;
            picker.SelectedIndex = Array.IndexOf(Settings.Fonts, "rubik-spray-paint") + 1;
            Assert.Equal("rubik-spray-paint", settings.TitleFont);
            Assert.Equal("jetbrains-mono", settings.ValueFont); Assert.Equal("", settings.LabelFont);
            Assert.Equal(1, changes); Dispatcher.UIThread.RunJobs();
            Assert.Equal(Ui.FamilyFor(settings.GraphFont), Ui.GraphFont);
            var preview = (Control)typeof(SettingsWindow).GetMethod("FontPreview", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(window, null)!;
            var reset = Assert.Single(preview.GetLogicalDescendants().OfType<Button>());
            reset.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Assert.Equal("inter", settings.Font); Assert.False(TextFonts.Active(settings));
            Assert.Equal(2, changes);
        }
        finally { window.Close(); Ui.UseFont("inter"); }
        return true;
    });

    [Fact]
    public void Profiles_and_theme_files_keep_independent_font_choices()
    {
        var settings = Settings.Starter();
        settings.TitleFont = "rubik-spray-paint"; settings.ValueFont = "jetbrains-mono";
        settings.LabelFont = "open-sans"; settings.GraphFont = "rajdhani";
        var restored = Settings.Normalize(JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(settings, Settings.Json), Settings.Json)!);
        var themed = Settings.Starter(); ThemeFile.Parse(ThemeFile.From(restored).ToJson())!.ApplyTo(themed);
        Assert.Equal(settings.TitleFont, themed.TitleFont); Assert.Equal(settings.ValueFont, themed.ValueFont);
        Assert.Equal(settings.LabelFont, themed.LabelFont); Assert.Equal(settings.GraphFont, themed.GraphFont);
        restored.TitleFont = "unknown"; restored.ValueFont = null!; restored.LabelFont = " ROBOTO ";
        Settings.Normalize(restored);
        Assert.Equal("", restored.TitleFont); Assert.Equal("", restored.ValueFont); Assert.Equal("roboto", restored.LabelFont);
        var legacy = Settings.Normalize(JsonSerializer.Deserialize<Settings>("{\"Font\":\"orbitron\"}", Settings.Json)!);
        Assert.False(TextFonts.Active(legacy)); Assert.Equal("orbitron", legacy.Font);
    }

    [Fact]
    public void Every_bundled_font_resolves_to_its_actual_family_without_system_installation() => UiHost.Run(() =>
    {
        foreach (var font in FontCatalog.Bundled)
        {
            var face = new Typeface(Ui.FamilyFor(font.Key)).GlyphTypeface;
            Assert.StartsWith(font.Name, face.FamilyName);
            Assert.Equal(FontWeight.Normal, face.Weight);
            Assert.NotEqual((ushort)0, face.GetGlyph('8'));
            Assert.NotEqual((ushort)0, face.GetGlyph('A'));
        }
        return true;
    });

    [Fact]
    public void Model_names_with_digits_are_labels_and_explicit_titles_override_number_flags() => UiHost.Run(() =>
    {
        var label = Ui.Text("AMD Ryzen 9 5900X", 14, Colors.White);
        var title = TextFonts.Mark(Ui.Text("GPU 2", 14, Colors.White, numbers: true), TextRole.Title);
        var value = SensorDetails.Value(Ui.Text("—", 14, Colors.White), "cpu.temp");
        Assert.Equal(TextRole.Label, TextFonts.RoleOf(label)); Assert.Equal(TextRole.Title, TextFonts.RoleOf(title));
        Assert.Equal(TextRole.Value, TextFonts.RoleOf(value));
        var settings = Settings.Starter(); settings.LabelFont = "roboto"; settings.TitleFont = "permanent-marker"; settings.ValueFont = "roboto-mono";
        var panel = Ui.Stack(4, label, title, value); TextFonts.Apply(panel, settings);
        Assert.Equal(Ui.FamilyFor(settings.LabelFont), label.FontFamily);
        Assert.Equal(Ui.FamilyFor(settings.TitleFont), title.FontFamily);
        Assert.Equal(Ui.FamilyFor(settings.ValueFont), value.FontFamily);
        return true;
    });

    public static IEnumerable<object[]> Designs() => AioDesigns.Styles.Concat(new[] { WidgetStyle.Standard, WidgetStyle.Dial, WidgetStyle.SciFi, WidgetStyle.HistoryGraph }).Select(s => new object[] { s });

    [Theory, MemberData(nameof(Designs))]
    public void Font_roles_survive_live_updates_and_render_in_each_design(WidgetStyle style) => UiHost.Run(() =>
    {
        var (snapshot, history) = Render.Demo();
        var settings = Settings.Starter(); settings.TitleFont = "permanent-marker"; settings.LabelFont = "roboto";
        settings.ValueFont = "jetbrains-mono"; settings.GraphFont = "rajdhani"; Ui.UseFonts(settings);
        var kind = AioDesigns.Contains(style) ? WidgetKind.Overview : WidgetKind.Cpu;
        var card = Cards.Create(kind, style, 0, Palette.For(true, false), Metrics.For(Density.Fit), settings);
        var window = new Window { Content = card, Width = 420, Height = 1400 };
        try
        {
            window.Show();
            foreach (var temp in new[] { 9d, 100d })
            {
                ((IWidget)card).Update(snapshot with { Cpu = snapshot.Cpu! with { Temperature = temp } }, history);
                TextColours.Apply(card, settings); Dispatcher.UIThread.RunJobs();
                using var bitmap = window.CaptureRenderedFrame(); Assert.NotNull(bitmap);
                var texts = card.GetLogicalDescendants().OfType<TextBlock>().Where(t => !string.IsNullOrWhiteSpace(t.Text)).ToArray();
                Assert.Contains(texts, t => TextFonts.RoleOf(t) == TextRole.Value);
                Assert.Contains(texts, t => TextFonts.RoleOf(t) == TextRole.Label);
                Assert.Contains(texts, t => TextFonts.RoleOf(t) == TextRole.Title);
                foreach (var text in texts) Assert.Equal(Ui.FamilyFor(TextFonts.Key(settings, TextFonts.RoleOf(text))), text.FontFamily);
                Assert.Equal(Ui.FamilyFor(settings.GraphFont), Ui.GraphFont);
            }
        }
        finally { window.Close(); Ui.UseFont("inter"); }
        return true;
    });

    [Fact]
    public void Render_real_font_preview() => UiHost.Run(() =>
    {
        var path = Environment.GetEnvironmentVariable("LUMEN_FONT_PREVIEW_DIR");
        if (string.IsNullOrEmpty(path)) return true;
        TextBlock Text(string label, string key, double size) => new() { Text = label, FontFamily = Ui.FamilyFor(key), FontSize = size, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap };
        var rows = Ui.Stack(18, Text("Lumen · Schriftarten", "inter", 24));
        foreach (var font in FontCatalog.Bundled)
            rows.Children.Add(Ui.Stack(2, Text(font.Name + " · " + font.Group, "inter", 12), Text("GPU Temperatur · 62,5 °C · 1.450 RPM", font.Key, 20)));
        var window = new Window { Width = 700, Height = 1850, Background = Brush.Parse("#14171D"), Content = new Border { Padding = new Avalonia.Thickness(24), Child = rows } };
        try { window.Show(); Dispatcher.UIThread.RunJobs(); using var bitmap = window.CaptureRenderedFrame(); Directory.CreateDirectory(path); bitmap!.Save(System.IO.Path.Combine(path, "Schriftarten-Vorschau.png")); }
        finally { window.Close(); }
        return true;
    });
}
