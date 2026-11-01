using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace Lumen.App;

// Small builders so every card uses the same type scale and spacing.
public static class Ui
{
    public static readonly FontFamily Font = new("fonts:Inter#Inter, $Default");
    private static readonly FontFeatureCollection Tabular = new() { FontFeature.Parse("tnum"), FontFeature.Parse("cv11") };

    public static TextBlock Text(string text, double size, Color color, FontWeight weight = FontWeight.Normal, bool numbers = false) => new()
    {
        Text = text,
        FontFamily = Font,
        FontSize = size,
        FontWeight = weight,
        Foreground = Palette.Brush(color),
        FontFeatures = numbers ? Tabular : null,
        TextTrimming = TextTrimming.CharacterEllipsis,
        VerticalAlignment = VerticalAlignment.Center
    };

    public static PathIcon Icon(string data, Color color, double size = 15) => new()
    {
        Data = StreamGeometry.Parse(data),
        Width = size,
        Height = size,
        Foreground = Palette.Brush(color)
    };

    // The icon is stroked rather than filled, so it reads as a line drawing like the charts.
    public static Control Glyph(string data, Color color, double size = 15) => new Avalonia.Controls.Shapes.Path
    {
        Data = StreamGeometry.Parse(data),
        Stroke = Palette.Brush(color),
        StrokeThickness = 1.4,
        StrokeLineCap = PenLineCap.Round,
        StrokeJoin = PenLineJoin.Round,
        Width = size,
        Height = size,
        Stretch = Stretch.Uniform,
        VerticalAlignment = VerticalAlignment.Center
    };

    public static Grid Columns(string definition, params Control[] children)
    {
        var g = new Grid { ColumnDefinitions = ColumnDefinitions.Parse(definition) };
        for (int i = 0; i < children.Length; i++) { Grid.SetColumn(children[i], i); g.Children.Add(children[i]); }
        return g;
    }

    public static StackPanel Stack(double spacing, params Control[] children)
    {
        var s = new StackPanel { Spacing = spacing };
        s.Children.AddRange(children);
        return s;
    }

    public static StackPanel Row(double spacing, params Control[] children)
    {
        var s = new StackPanel { Orientation = Orientation.Horizontal, Spacing = spacing };
        s.Children.AddRange(children);
        return s;
    }
}

// A big reading with its unit set smaller on the same baseline: "62 °C", "14.6 GB".
public sealed class Hero : StackPanel
{
    private readonly TextBlock number, unit;

    public Hero(double size, Palette p)
    {
        Orientation = Orientation.Horizontal;
        Spacing = 3;
        number = Ui.Text(Lumen.Core.Format.Missing, size, p.Ink, FontWeight.SemiBold, true);
        number.LetterSpacing = -size * .03;
        number.VerticalAlignment = VerticalAlignment.Bottom;
        unit = Ui.Text("", Math.Max(10.5, Math.Round(size * .4)), p.Soft, FontWeight.Medium);
        unit.VerticalAlignment = VerticalAlignment.Bottom;
        unit.Margin = new Thickness(0, 0, 0, size * .16);
        Children.Add(number);
        Children.Add(unit);
    }

    public void Set(string value, string unitText, Color color)
    {
        Motion.Text(number, value);
        number.Foreground = Palette.Brush(color);
        unit.Text = value == Lumen.Core.Format.Missing ? "" : unitText;
    }
}

// Label above value: "POWER / 98 W". Laid out in equal columns under each card.
public sealed class Stat : StackPanel
{
    private readonly TextBlock value;

    public Stat(string label, Palette p)
    {
        Spacing = 1;
        // Keeps neighbouring labels from running into each other.
        Margin = new Thickness(0, 0, 6, 0);
        var l = Ui.Text(label.ToUpperInvariant(), 10, p.Faint, FontWeight.SemiBold);
        l.LetterSpacing = .6;
        value = Ui.Text(Lumen.Core.Format.Missing, 12.5, p.Ink, FontWeight.Medium, true);
        Children.Add(l);
        Children.Add(value);
    }

    public string Value { set => Motion.Text(this.value, value); }
    public Color Color { set => this.value.Foreground = Palette.Brush(value); }
}

public static class StatGrid
{
    public static Grid Of(params Stat[] stats) => Weighted(string.Join(",", stats.Select(_ => "*")), stats);

    // Equal columns by default; a wider last column when its value is long, such as an IP address.
    public static Grid Weighted(string columns, params Stat[] stats) => Ui.Columns(columns, stats);
}
