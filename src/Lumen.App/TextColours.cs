using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

/// <summary>
/// The value and label colours chosen in Appearance, for every design: each neutral (white or grey) text of a card
/// takes the chosen colour, values (numbers, amounts) one and names and captions (CPU, Load, Temp …) the other.
/// Coloured text (accents, hot readings in amber and red) stays as the design draws it. Every text takes the chosen
/// colour at full strength, grey ones too (user request: all text is equally important).
/// </summary>
public static class TextColours
{
    private sealed class Seen { public Color Original, Applied; }
    private static readonly ConditionalWeakTable<TextBlock, Seen> seen = new();

    public static bool Active(Settings s) => Parse(s.ValueColor) != null || Parse(s.LabelColor) != null;

    public static Color? Parse(string? hex) => !string.IsNullOrEmpty(hex) && Settings.IsHexColor(hex) ? Color.Parse(hex) : null;

    /// <summary>Gives the card's neutral texts the chosen colours (again after an update that drew them in its own).</summary>
    public static void Apply(Control root, Settings s)
    {
        Color? value = Parse(s.ValueColor), label = Parse(s.LabelColor);
        if (value == null && label == null) return;
        var values = root is AioTile aio ? aio.ValueTexts : null;
        foreach (var drawn in root.GetLogicalDescendants().OfType<Control>())
        {
            if (drawn is AioCoreBars bars && (bars.ValueInk != value || bars.LabelInk != label)) { bars.ValueInk = value; bars.LabelInk = label; bars.InvalidateVisual(); }
            if (drawn is MainboardCoreGrid grid && (grid.ValueInk != value || grid.LabelInk != label)) { grid.ValueInk = value; grid.LabelInk = label; grid.InvalidateVisual(); }
        }
        foreach (var text in root.GetLogicalDescendants().OfType<TextBlock>().Prepend(root as TextBlock))
        {
            if (text == null || text.Foreground is not ISolidColorBrush brush) continue;
            var current = brush.Color;
            if (!seen.TryGetValue(text, out var known)) seen.Add(text, known = new Seen { Original = current, Applied = current });
            else if (current != known.Applied) known.Original = current; // the card set its own colour since: start from that
            var original = known.Original;
            if (!Neutral(original)) { known.Applied = current; continue; }
            bool isValue = values != null ? values.Contains(text) || text is AioValueText : text is AioValueText || HasDigit(text.Text);
            if ((isValue ? value : label) is not Color chosen) { known.Applied = current; continue; }
            var colour = Color.FromArgb(original.A, chosen.R, chosen.G, chosen.B);
            known.Applied = colour;
            if (colour != current) text.Foreground = new SolidColorBrush(colour, brush.Opacity);
        }
    }

    /// <summary>White, grey or black: the colours a design gives its plain text.</summary>
    public static bool Neutral(Color c)
    {
        double r = c.R / 255d, g = c.G / 255d, b = c.B / 255d;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        return c.A > 0 && (max <= 0 || (max - min) / max < .28);
    }

    private static bool HasDigit(string? text) => text != null && text.Any(char.IsDigit);
}
