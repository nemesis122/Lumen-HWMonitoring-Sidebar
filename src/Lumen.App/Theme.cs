using Avalonia;
using Avalonia.Media;

namespace Lumen.App;

// Lumen colours by state, not by category: every reading moves along one cool-to-hot ramp,
// so a glance down the sidebar shows what is working hard without reading a single number.
public sealed class Palette
{
    public required bool Dark { get; init; }
    public required Color Surface { get; init; }
    public required Color Card { get; init; }
    public required Color Stroke { get; init; }
    public required Color Ink { get; init; }
    public required Color Soft { get; init; }
    public required Color Faint { get; init; }
    public required Color Track { get; init; }
    public required Color[] Ramp { get; init; }

    public Color Cool => Ramp[0];
    public Color Calm => Ramp[1];

    public static Palette For(bool dark, bool glass) => For(dark, glass, .55, null);

    public static Palette For(bool dark, bool glass, double opacity, Color? accent) => For(dark, glass, opacity, accent, false);

    // opacity applies only with glass; accent replaces the cool half of the ramp while
    // amber and red stay, so a hot part still stands out in any colour scheme.
    // Secondary text is kept bright enough to read on glass over any wallpaper;
    // high contrast lifts every label close to the main text colour.
    public static Palette For(bool dark, bool glass, double opacity, Color? accent, bool highContrast)
    {
        byte A(double a) => glass ? (byte)Math.Round(Math.Clamp(a, 0, 1) * 255) : (byte)0xFF;
        var ramp = dark
            ? new[] { Color.Parse("#4FB3FF"), Color.Parse("#45D3A8"), Color.Parse("#F5B544"), Color.Parse("#FF5D5D") }
            : new[] { Color.Parse("#1F7AE0"), Color.Parse("#129C76"), Color.Parse("#C98200"), Color.Parse("#D93B3B") };
        if (accent is Color c)
        {
            var tone = dark ? Mix(c, Colors.White, .18) : Mix(c, Colors.Black, .08);
            ramp[0] = dark ? Mix(tone, Colors.White, .35) : Mix(tone, Colors.White, .25);
            ramp[1] = tone;
        }
        return dark
            ? new Palette
            {
                Dark = true,
                Surface = Color.FromArgb(A(opacity), 0x0B, 0x0E, 0x14),
                Card = Color.FromArgb(A(opacity * .98), 0x17, 0x1C, 0x25),
                Stroke = Color.FromArgb(0x1C, 0xFF, 0xFF, 0xFF),
                Ink = Color.Parse("#EEF2F7"),
                Soft = Color.Parse(highContrast ? "#E1E7EF" : "#BAC4D1"),
                Faint = Color.Parse(highContrast ? "#C9D2DE" : "#919DAD"),
                Track = Color.FromArgb(0x16, 0xFF, 0xFF, 0xFF),
                Ramp = ramp
            }
            : new Palette
            {
                Dark = false,
                Surface = Color.FromArgb(A(opacity), 0xEC, 0xEF, 0xF4),
                Card = Color.FromArgb(A(opacity + .22), 0xFF, 0xFF, 0xFF),
                Stroke = Color.FromArgb(0x16, 0x10, 0x18, 0x28),
                Ink = Color.Parse("#12161C"),
                Soft = Color.Parse(highContrast ? "#1E252E" : "#46505E"),
                Faint = Color.Parse(highContrast ? "#2E3640" : "#5F6B7A"),
                Track = Color.FromArgb(0x12, 0x10, 0x18, 0x28),
                Ramp = ramp
            };
    }

    public static readonly (string Hex, string Name)[] Presets =
        { ("#3B82F6", "Blue"), ("#8B5CF6", "Violet"), ("#10B981", "Green"), ("#F97316", "Orange"), ("#EC4899", "Pink") };

    // 0 → blue, .5 → mint, .75 → amber, 1 → red.
    public Color At(double t)
    {
        t = double.IsFinite(t) ? Math.Clamp(t, 0, 1) : 0;
        double[] stops = { 0, .5, .75, 1 };
        for (int i = 1; i < stops.Length; i++)
            if (t <= stops[i]) return Mix(Ramp[i - 1], Ramp[i], (t - stops[i - 1]) / (stops[i] - stops[i - 1]));
        return Ramp[^1];
    }

    public static Color Mix(Color a, Color b, double t) => Color.FromArgb(
        (byte)(a.A + (b.A - a.A) * t), (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));

    public static Color Alpha(Color c, double a) => Color.FromArgb((byte)Math.Clamp(a * 255, 0, 255), c.R, c.G, c.B);

    public static IBrush Brush(Color c) => new SolidColorBrush(c);
}

// Sizes for the two densities, so both stay on one consistent grid.
public sealed record Metrics(double Pad, double Gap, double Radius, double Hero, double Chart, double Skyline, bool Subtitles)
{
    public static Metrics For(Lumen.Core.Density d) => d == Lumen.Core.Density.Compact
        ? new Metrics(12, 8, 14, 30, 34, 20, false)
        : new Metrics(16, 10, 16, 40, 52, 28, true);
}

public static class Glyphs
{
    // 16×16 line icons drawn as geometry so they stay crisp at any scale and take the theme colour.
    public const string Cpu = "M5,3 H11 A2,2 0 0 1 13,5 V11 A2,2 0 0 1 11,13 H5 A2,2 0 0 1 3,11 V5 A2,2 0 0 1 5,3 Z M6.5,6.5 H9.5 V9.5 H6.5 Z M6,1 V3 M10,1 V3 M6,13 V15 M10,13 V15 M1,6 H3 M1,10 H3 M13,6 H15 M13,10 H15";
    public const string Gpu = "M1.5,4 H14.5 V12 H1.5 Z M6,8 m-2.2,0 a2.2,2.2 0 1 0 4.4,0 a2.2,2.2 0 1 0 -4.4,0 M10,6.5 H12.5 M10,9.5 H12.5 M3,12 V14";
    public const string Memory = "M1.5,5 H14.5 V11 H1.5 Z M4,7 V9 M6.5,7 V9 M9,7 V9 M11.5,7 V9 M3,11 V13 M6,11 V13 M10,11 V13 M13,11 V13";
    public const string Drive = "M2,4 A1.5,1.5 0 0 1 3.5,2.5 H12.5 A1.5,1.5 0 0 1 14,4 V12 A1.5,1.5 0 0 1 12.5,13.5 H3.5 A1.5,1.5 0 0 1 2,12 Z M2,9.5 H14 M11,11.5 H12";
    public const string Network = "M5,13 V3 M2.5,5.5 L5,3 L7.5,5.5 M11,3 V13 M8.5,10.5 L11,13 L13.5,10.5";
    public const string Clock = "M8,1.5 A6.5,6.5 0 1 1 7.99,1.5 Z M8,4.5 V8 L10.5,9.5";
    public const string Gear = "M8,5.5 A2.5,2.5 0 1 1 7.99,5.5 Z M8,1 V3 M8,13 V15 M1,8 H3 M13,8 H15 M3.05,3.05 L4.46,4.46 M11.54,11.54 L12.95,12.95 M3.05,12.95 L4.46,11.54 M11.54,4.46 L12.95,3.05";
    public const string Fan = "M8,8 m-1.3,0 a1.3,1.3 0 1 0 2.6,0 a1.3,1.3 0 1 0 -2.6,0 M8,6.7 C7.2,4.5 7.6,2 9.6,1.8 C11.4,1.7 11,4.6 9.2,7.2 M9.3,8 C11.5,7.2 14,7.6 14.2,9.6 C14.3,11.4 11.4,11 8.8,9.2 M8,9.3 C8.8,11.5 8.4,14 6.4,14.2 C4.6,14.3 5,11.4 6.8,8.8 M6.7,8 C4.5,8.8 2,8.4 1.8,6.4 C1.7,4.6 4.6,5 7.2,6.8";
    public const string Battery = "M2,4.5 H12.5 A1,1 0 0 1 13.5,5.5 V10.5 A1,1 0 0 1 12.5,11.5 H2 A1,1 0 0 1 1,10.5 V5.5 A1,1 0 0 1 2,4.5 Z M15,6.8 V9.2 M3.5,7 V9 M6,7 V9";
    public const string Apps = "M2,2.5 H6.5 V7 H2 Z M9.5,2.5 H14 V7 H9.5 Z M2,9 H6.5 V13.5 H2 Z M9.5,9 H14 V13.5 H9.5 Z";
    public const string Cloud = "M4.5,12.5 H11.5 A3,3 0 0 0 11.7,6.5 A4,4 0 0 0 4.1,7.6 A2.5,2.5 0 0 0 4.5,12.5 Z";
    public const string Ping = "M2,13 L5,13 L7,4 L9.5,12 L11,8 L14,8";
    public const string Power = "M9,1.5 L3.5,9 H8 L7,14.5 L12.5,7 H8 Z";
    public const string System = "M1.5,3 H14.5 V11 H1.5 Z M6,14 H10 M8,11 V14";
    public const string Data = "M8,2 V10 M5,7 L8,10 L11,7 M3,13.5 H13";
    public const string Calendar = "M2,3.5 H14 V14 H2 Z M2,6.5 H14 M5,2 V5 M11,2 V5";
    public const string Notes = "M3,2 H10 L13,5 V14 H3 Z M10,2 V5 H13 M5.5,8 H10.5 M5.5,10.5 H10.5";
    public const string Globe = "M8,1.5 A6.5,6.5 0 1 1 7.99,1.5 Z M1.5,8 H14.5 M8,1.5 C5.5,4 5.5,12 8,14.5 M8,1.5 C10.5,4 10.5,12 8,14.5";
    public const string Music = "M6,12 V3 L13,1.8 V10.5 M6,12 A2,2 0 1 1 5.99,12 Z M13,10.5 A2,2 0 1 1 12.99,10.5 Z";
    public const string Volume = "M2,4 H14 V12 H2 Z M4.5,8 H8 M11.5,8 h0.01";
    public const string Shield = "M8,1.5 L13.5,3.5 V7.5 C13.5,11 11,13.5 8,14.5 C5,13.5 2.5,11 2.5,7.5 V3.5 Z M8,5 V8.5 M8,10.5 V11";
}
