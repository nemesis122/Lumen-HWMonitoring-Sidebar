using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lumen.Core;

// The look of the sidebar as a small file (*.lumentheme) to keep, swap or share:
// colours, background, glass, text size and typeface – never the layout or personal data.
public sealed class ThemeFile
{
    public const string Extension = ".lumentheme";
    public const string Kind = "lumen-theme";

    [JsonPropertyName("type")] public string Type { get; set; } = "";
    public int Version { get; set; } = 1;
    public string Name { get; set; } = "";
    public ThemeMode Theme { get; set; } = ThemeMode.Dark;
    public string Colors { get; set; } = Settings.StateColors;
    public string Background { get; set; } = "graphite";
    public bool Glass { get; set; } = true;
    public double Opacity { get; set; } = .55;
    public bool HighContrast { get; set; } = true;
    public double TextScale { get; set; } = 1;
    public string Font { get; set; } = "inter";
    public bool SmoothTransitions { get; set; }

    public static ThemeFile From(Settings s, string name = "") => new()
    {
        Type = Kind, Name = name, Theme = s.Theme, Colors = s.Colors, Background = s.Background, Glass = s.Glass, Opacity = s.Opacity,
        HighContrast = s.HighContrast, TextScale = s.TextScale, Font = s.Font, SmoothTransitions = s.SmoothTransitions
    };

    // Copies the look onto the settings; whatever is out of range is clamped by Normalize.
    public void ApplyTo(Settings s)
    {
        s.Theme = Enum.IsDefined(Theme) ? Theme : ThemeMode.Dark;
        s.Colors = Colors ?? Settings.StateColors;
        s.Background = Background ?? "graphite";
        s.Glass = Glass;
        s.Opacity = Opacity;
        s.HighContrast = HighContrast;
        s.TextScale = TextScale;
        s.Font = Font ?? "inter";
        s.SmoothTransitions = SmoothTransitions;
        Settings.Normalize(s);
    }

    public string ToJson() => JsonSerializer.Serialize(this, Settings.Json);

    // Null when the text is not a Lumen theme (wrong type, broken JSON or far too large).
    public static ThemeFile? Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 64 * 1024) return null;
        try
        {
            var t = JsonSerializer.Deserialize<ThemeFile>(json, Settings.Json);
            if (t is null || t.Type != Kind || t.Version < 1) return null;
            t.Name = (t.Name ?? "").Trim();
            if (t.Name.Length > 40) t.Name = t.Name[..40];
            return t;
        }
        catch (JsonException) { return null; }
    }
}
