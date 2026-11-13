using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

/// <summary>
/// A measured value in an AIO card. It always takes the room of the widest form its value can have
/// ("100 %", "8888 rpm", "88.8 Mbit/s" …), so "5 %" → "21 %" or "0 B/s" → "368 KB/s" no longer pushes
/// the units, arrows or neighbouring values sideways. Where its column is narrower, it simply takes the column.
/// </summary>
public sealed class AioValueText : TextBlock
{
    protected override Type StyleKeyOverride => typeof(TextBlock);

    private string? templateFor; private double templateWidth, widest;

    /// <summary>The widest form of this reading ("888 W", "8.88 GHz" …), used while it has no value yet ("—").</summary>
    public string Hint { get; set; } = "";
    /// <summary>The most room the value may take for its reserve (set by the column it sits in); its own text always fits.</summary>
    public double Cap { get; set; } = double.PositiveInfinity;
    /// <summary>The width of the text it shows right now.</summary>
    public double Natural { get; private set; }

    /// <summary>The widest form for a reading, by what it measures.</summary>
    public static string HintFor(string key, string group)
    {
        if (group == "Cooling") return "8888 rpm";
        if (group == "Rails") return "88.888 V";
        if (group == "Network") return key.EndsWith(".link") ? "8.8 Gbit/s" : "88.8 Mbit/s";
        if (group == "Cores" || key.EndsWith(".temp") || key.EndsWith(".hotspot") || key.EndsWith(".junction") || key.StartsWith("aio.system.disk.")) return "888 °C";
        if (key.EndsWith(".read") || key.EndsWith(".write")) return "888 MB/s";
        if (key.EndsWith(".load") || key.EndsWith(".used") || key == "aio.system.ram") return "888 %";
        if (key.EndsWith(".power")) return "888 W";
        if (key.EndsWith(".voltage")) return "8.888 V";
        if (key.EndsWith(".clock")) return "8888 MHz";
        if (key.EndsWith(".vram")) return "88.8 / 88.8 GB";
        return "";
    }

    /// <summary>The widest text the value can show in its current unit, written with "8" (the widest digit).</summary>
    public static string Template(string text)
    {
        if (string.IsNullOrEmpty(text) || !text.Any(char.IsDigit)) return text;
        int start = 0; while (start < text.Length && !char.IsDigit(text[start])) start++;
        string prefix = text[..start];
        var (number, unit) = Format.Split(text[start..]);
        if (unit.Length > 0 && unit.Any(char.IsDigit)) { number = text[start..]; unit = ""; }
        char sep = number.Contains(',') ? ',' : '.';
        string eights(string n) => Regex.Replace(n, @"\d", "8");
        string templ; string unitT = unit;
        switch (unit)
        {
            case "%": templ = "888"; break;
            case "rpm": templ = "8888"; break;
            case "°C" or "°F": templ = "888"; break;
            case "B/s" or "KB/s" or "MB/s" or "GB/s": templ = "888"; unitT = "MB/s"; break; // 1000–1023 KB/s is rare: then it grows once
            case "kbit/s" or "Mbit/s" or "Gbit/s": templ = "88" + sep + "8"; unitT = "Mbit/s"; break;
            case "GHz" or "MHz": templ = "8888"; unitT = "MHz"; break;
            case "W": templ = "888"; break;
            default:
                if (number.Contains(" / "))
                {
                    // "14.4 / 93.7" (RAM, VRAM): both parts as wide as the larger one.
                    var parts = number.Split(" / "); var widest = eights(parts.OrderByDescending(x => x.Length).First());
                    templ = string.Join(" / ", parts.Select(_ => widest));
                }
                else
                {
                    templ = eights(number);
                    // A bare whole number (a temperature beside its unit, a fan speed) gets room for three digits.
                    if (unit.Length == 0 && Regex.IsMatch(number, @"^\d{1,2}$")) templ = "888";
                }
                break;
        }
        // Never reserve less than the value needs right now.
        if (templ.Length < number.Length) templ = eights(number);
        return prefix + templ + (unitT.Length > 0 ? " " + unitT : "");
    }

    protected override Size MeasureOverride(Size available)
    {
        // Right- or centre-placed values keep their text on that side of the room.
        if (!IsSet(TextAlignmentProperty)) TextAlignment = HorizontalAlignment switch { HorizontalAlignment.Right => TextAlignment.Right, HorizontalAlignment.Center => TextAlignment.Center, _ => TextAlignment.Left };
        var size = base.MeasureOverride(available);
        var text = Text ?? "";
        if (templateFor != text)
        {
            templateFor = text;
            // No value yet ("—"): reserve the widest form of what this reading measures.
            var template = text.Any(char.IsDigit) ? Template(text) : Hint;
            templateWidth = template.Length == 0 ? 0 : new FormattedText(template, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(FontFamily, FontStyle, FontWeight, FontStretch), FontSize, null).WidthIncludingTrailingWhitespace * 1.04 + Padding.Left + Padding.Right;
        }
        // The room only ever grows: a value that once needed more keeps it, so it never shrinks back and forth.
        Natural = size.Width;
        widest = Math.Max(widest, Math.Max(size.Width, templateWidth));
        double width = Math.Min(widest, Math.Max(size.Width, Cap));
        if (double.IsFinite(available.Width)) width = Math.Min(width, Math.Max(size.Width, available.Width));
        return new Size(width, size.Height);
    }
}
