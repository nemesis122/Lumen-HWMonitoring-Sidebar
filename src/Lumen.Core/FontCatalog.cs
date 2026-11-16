namespace Lumen.Core;

public sealed record FontOption(string Key, string Name, string Group);

/// <summary>Stable settings keys and bundled family names, independent of the UI.</summary>
public static class FontCatalog
{
    public static readonly FontOption[] Bundled =
    {
        new("roboto", "Roboto", "Clear"), new("open-sans", "Open Sans", "Clear"),
        new("lato", "Lato", "Clear"), new("noto-sans", "Noto Sans", "Clear"),
        new("source-sans-3", "Source Sans 3", "Clear"),
        new("rajdhani", "Rajdhani", "Technical"), new("exo-2", "Exo 2", "Technical"),
        new("oxanium", "Oxanium", "Technical"), new("chakra-petch", "Chakra Petch", "Technical"),
        new("roboto-mono", "Roboto Mono", "Numbers"), new("jetbrains-mono", "JetBrains Mono", "Numbers"),
        new("ibm-plex-mono", "IBM Plex Mono", "Numbers"), new("share-tech-mono", "Share Tech Mono", "Numbers"),
        new("bebas-neue", "Bebas Neue", "Bold titles"), new("anton", "Anton", "Bold titles"),
        new("oswald", "Oswald", "Bold titles"), new("barlow-condensed", "Barlow Condensed", "Bold titles"),
        new("rubik-spray-paint", "Rubik Spray Paint", "Graffiti"), new("rubik-wet-paint", "Rubik Wet Paint", "Graffiti"),
        new("permanent-marker", "Permanent Marker", "Graffiti"), new("rock-salt", "Rock Salt", "Graffiti"),
        new("black-ops-one", "Black Ops One", "Bold titles")
    };

    public static string Normalize(string? key, string fallback = "") =>
        key?.Trim().ToLowerInvariant() is { } normalized && Settings.Fonts.Contains(normalized) ? normalized : fallback;
}
