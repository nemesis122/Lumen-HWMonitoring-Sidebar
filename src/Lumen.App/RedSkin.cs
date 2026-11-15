using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.LogicalTree;
using Avalonia.Media;

namespace Lumen.App;

/// <summary>
/// The look of the "Red" designs: every coloured part of a built card (text, frames, fills, the colours of its drawn
/// controls) takes the red of the Panel designs at the same brightness. Greys, white and black stay as they are, so the
/// layout, contrast and every reading of the base design are unchanged.
/// </summary>
public static class RedSkin
{
    /// <summary>The colour in red: same brightness and strength, red hue; greys are kept.</summary>
    public static Color Redden(Color c)
    {
        double r = c.R / 255d, g = c.G / 255d, b = c.B / 255d;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        double saturation = max <= 0 ? 0 : (max - min) / max;
        if (saturation < .28 || max < .12) return c;
        // Hue 354° (#FF2238 is the Panel red); lighter colours become light reds.
        double v = max, s = Math.Min(1, saturation * 1.05), h = 354 / 60d;
        int i = (int)Math.Floor(h) % 6; double f = h - Math.Floor(h);
        double p = v * (1 - s), q = v * (1 - f * s), t = v * (1 - (1 - f) * s);
        (r, g, b) = i switch { 0 => (v, t, p), 1 => (q, v, p), 2 => (p, v, t), 3 => (p, q, v), 4 => (t, p, v), _ => (v, p, q) };
        return Color.FromArgb(c.A, (byte)Math.Round(r * 255), (byte)Math.Round(g * 255), (byte)Math.Round(b * 255));
    }

    public static IBrush? Redden(IBrush? brush) => brush switch
    {
        ISolidColorBrush solid => new SolidColorBrush(Redden(solid.Color), solid.Opacity),
        LinearGradientBrush linear => new LinearGradientBrush
        {
            StartPoint = linear.StartPoint, EndPoint = linear.EndPoint, SpreadMethod = linear.SpreadMethod, Opacity = linear.Opacity,
            GradientStops = new GradientStops().With(linear.GradientStops)
        },
        RadialGradientBrush radial => new RadialGradientBrush
        {
            Center = radial.Center, GradientOrigin = radial.GradientOrigin, RadiusX = radial.RadiusX, RadiusY = radial.RadiusY,
            SpreadMethod = radial.SpreadMethod, Opacity = radial.Opacity, GradientStops = new GradientStops().With(radial.GradientStops)
        },
        _ => brush
    };
    private static GradientStops With(this GradientStops target, IEnumerable<GradientStop> stops)
    {
        foreach (var stop in stops) target.Add(new GradientStop(Redden(stop.Color), stop.Offset));
        return target;
    }

    /// <summary>Turns a built card red (the card itself and everything in it).</summary>
    public static void Apply(Control root)
    {
        foreach (var control in root.GetLogicalDescendants().OfType<Control>().Prepend(root).ToList())
        {
            switch (control)
            {
                case TextBlock text: text.Foreground = Redden(text.Foreground); break;
                case Border border: border.Background = Redden(border.Background); border.BorderBrush = Redden(border.BorderBrush); break;
                case Panel panel: panel.Background = Redden(panel.Background); break;
                case Shape shape: shape.Fill = Redden(shape.Fill); shape.Stroke = Redden(shape.Stroke); break;
            }
            if (control is AioCoreBars bars) bars.Red = true;
            if (control is MainboardCoreGrid grid) grid.Red = true;
            // The design's own drawn controls keep their colours in fields: turn those red too.
            var type = control.GetType();
            if (type.Namespace != typeof(RedSkin).Namespace || control is AioTile) continue;
            foreach (var field in Fields(type))
            {
                object? value = field.GetValue(control);
                object? red = value switch
                {
                    Color c => Redden(c),
                    IBrush b => Redden(b),
                    Color[] colors => colors.Select(Redden).ToArray(),
                    _ => null
                };
                if (red != null) field.SetValue(control, red);
            }
            control.InvalidateVisual();
        }
    }

    private static readonly Dictionary<Type, FieldInfo[]> cache = new();
    private static FieldInfo[] Fields(Type type)
    {
        lock (cache)
        {
            if (cache.TryGetValue(type, out var known)) return known;
            var list = new List<FieldInfo>();
            for (var t = type; t != null && t.Namespace == typeof(RedSkin).Namespace; t = t.BaseType)
                list.AddRange(t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Where(f => f.FieldType == typeof(Color) || f.FieldType == typeof(Color?) || f.FieldType == typeof(Color[]) || typeof(IBrush).IsAssignableFrom(f.FieldType)));
            return cache[type] = list.ToArray();
        }
    }
}
