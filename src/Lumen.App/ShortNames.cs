using System.Text.RegularExpressions;

namespace Lumen.App;

/// <summary>
/// Short names for the labels in the hardware designs: CPU, GPU, RAM, DL, UP, Link, PSU, Hot, Temp, Clk, V, R, W …
/// Model names ("AMD Radeon Graphics", "Samsung SSD 970 EVO") are left alone: single words that can be part of a
/// name are only shortened when they are the whole label.
/// </summary>
public static class ShortNames
{
    // Phrases replaced wherever they appear in a label (longest first).
    private static readonly (string Long, string Short)[] Phrases =
    {
        ("Supply voltages (PSU)", "PSU"), ("Cooling – Fan (rpm)", "Fans (rpm)"), ("Cooling (Fans) – speed", "Fans"),
        ("Memory junction", "VRAM °C"), ("VRAM Temp.", "VRAM °C"), ("VRAM Temp", "VRAM °C"), ("Power supply", "PSU"),
        ("Link speed", "Link"), ("Download", "DL"), ("Upload", "UP"), ("Temperature", "Temp"),
        ("Temp.", "Temp"), ("Clock speed", "Clock"), ("Average clock", "Clock"), ("Core clock", "Clock"), ("Core voltage", "Vcore"), ("Processor", "CPU"), ("Voltage", "Volt"), ("Read", "R"), ("Write", "W"), ("Chipset", "Chip"), ("Video memory", "VRAM"),
    };
    // Whole labels only.
    private static readonly Dictionary<string, string> Whole = new(StringComparer.Ordinal)
    {
        ["Processor"] = "CPU", ["Graphics"] = "GPU", ["Graphics card"] = "GPU", ["Memory"] = "RAM",
        ["Cooling"] = "Fans",
    };
    // Headings keep their full form except for the part names.
    private static readonly HashSet<string> Headings = new(StringComparer.Ordinal) { "Processor", "Graphics", "Graphics card", "Memory", "Cooling", "Power supply", "Supply voltages (PSU)", "Cooling – Fan (rpm)", "Cooling (Fans) – speed" };
    private static readonly Dictionary<string, Regex> patterns = Phrases.ToDictionary(p => p.Long, p => new Regex(@"(?<!\w)" + Regex.Escape(p.Long) + @"(?!\w)"));

    // "LINK SPEED" → "Link speed", matching how the names are written in the tables.
    private static string Title(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..].ToLowerInvariant();

    /// <summary>The full names a short label can stand for ("W" → Power, Package, Write …).</summary>
    public static IEnumerable<string> Longer(string text)
    {
        if (text.Any(char.IsLetter) && text == text.ToUpperInvariant() && Title(text) != text)
            foreach (var longer in Longer(Title(text))) yield return longer;
        foreach (var (longer, shorter) in Whole) if (shorter == text) yield return longer;
        foreach (var (longer, shorter) in Phrases)
            if (text.Contains(shorter, StringComparison.Ordinal))
            {
                var expanded = Regex.Replace(text, @"(?<!\w)" + Regex.Escape(shorter) + @"(?!\w)", longer.Replace("$", "$$"));
                if (expanded != text) yield return expanded;
            }
    }

    public static string Apply(string text, bool heading)
    {
        // Designs that write their labels in capitals ("PROCESSOR", "LINK SPEED") get the short names in capitals too.
        if (text.Any(char.IsLetter) && text == text.ToUpperInvariant() && text != text.ToLowerInvariant())
        {
            var title = Core(Title(text), heading);
            return title == Title(text) ? text : title.ToUpperInvariant();
        }
        return Core(text, heading);
    }

    private static string Core(string text, bool heading)
    {
        if (heading) return Headings.Contains(text) ? Core(text, false) : text;
        if (Whole.TryGetValue(text, out var whole)) return whole;
        foreach (var (longer, shorter) in Phrases)
            if (text.Contains(longer, StringComparison.Ordinal)) text = patterns[longer].Replace(text, shorter);
        return text;
    }
}
