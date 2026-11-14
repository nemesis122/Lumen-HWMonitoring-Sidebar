using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

// Reuses the design's own vector renderer for every selected device/direction.
public sealed class DesignValueFaces : UniformGrid
{
    private sealed record Cell(TextBlock Number, TextBlock Unit, Control? Graphic, TextBlock? Secondary);
    private readonly Dictionary<string, Cell> cells = new();
    private string shape = "";
    private bool enlarged;
    public bool Enlarged { get => enlarged; set { if (enlarged != value) { enlarged = value; InvalidateMeasure(); } } }

    protected override Size MeasureOverride(Size available)
    {
        double minimum = 1;
        foreach (var child in Children.Where(c => c.IsVisible))
        {
            child.Measure(Size.Infinity);
            double request = child.GetLogicalDescendants().OfType<Viewbox>().Where(v => Equals(v.Tag,"graphic") && double.IsFinite(v.Width)).Select(v => v.Width + 6).DefaultIfEmpty(0).Max();
            minimum = Math.Max(minimum, Math.Max(child.DesiredSize.Width, request));
        }
        int wanted = Children.Count > 1 && (!enlarged || !double.IsFinite(available.Width) || available.Width >= minimum * 2) ? 2 : 1;
        if (Columns != wanted) Columns = wanted;
        if (double.IsFinite(available.Width))
            foreach (var box in Children.SelectMany(c => c.GetLogicalDescendants().OfType<Viewbox>()).Where(v => Equals(v.Tag,"graphic")))
                box.MaxWidth = Math.Max(1, available.Width / wanted - 6);
        return base.MeasureOverride(available);
    }

    public static List<Reading> Readings(WidgetKind kind, int index, Snapshot snapshot, History history, Settings settings, Palette palette) =>
        CompleteReadings.For(kind, index, snapshot, history, settings, palette)
            .Where(r => kind == WidgetKind.Drives ? r.Key.StartsWith("drive:") && r.Key.EndsWith(":temp") : r.Key is "net.down" or "net.up")
            .Select(r => new Reading(kind == WidgetKind.Drives ? r.Label.Split(" · ")[0] + " · " + L.T("Temp") : L.T(r.Key == "net.down" ? "Download" : "Upload"),
                r.Value, SegmentScale.Fraction(kind, r.Label, r.Value, snapshot) ?? 0, palette.Ink) { Key = r.Key }).ToList();

    public void Show(WidgetStyle style, IReadOnlyList<Reading> readings, Palette palette, History? history = null, IReadOnlyList<Reading>? secondary = null)
    {
        IsVisible = readings.Count > 0;
        string next = style + "|" + string.Join('|', readings.Select(r => r.Key + r.Label)) + "|" + string.Join('|', secondary?.Select(r => r.Key) ?? Array.Empty<string>());
        if (shape != next)
        {
            shape = next; cells.Clear(); Children.Clear(); Columns = Math.Clamp(readings.Count, 1, 2);
            foreach (var reading in readings)
            {
                bool plain = style is WidgetStyle.Pure or WidgetStyle.PureBar or WidgetStyle.PureNumber or WidgetStyle.PureSegments;
                var label = Ui.Text(plain ? reading.Label : reading.Label.ToUpper(L.Culture), plain ? 11 : 12, palette.Soft, FontWeight.Medium);
                label.TextTrimming = TextTrimming.CharacterEllipsis;
                var number = SensorDetails.Value(Ui.Text("", style == WidgetStyle.HistoryGraph ? 30 : plain ? style == WidgetStyle.Pure ? 36 : 46 : 40, palette.Ink, FontWeight.DemiBold, numbers: true), reading.Key);
                if (!plain && style != WidgetStyle.HistoryGraph) number.TextAlignment = TextAlignment.Center;
                var unit = Ui.Text("", plain ? 14 : 16, palette.Soft, FontWeight.DemiBold);
                unit.VerticalAlignment = VerticalAlignment.Bottom; unit.Margin = new Thickness(3, 0, 0, 4);
                var digits = Ui.Row(0, number, unit);
                Control? graphic = null;
                TextBlock? extra = null;
                Control host;
                if (style == WidgetStyle.HistoryGraph)
                {
                    var chart = new AreaChart { Height = 84, FillOpacity = .28, LineWidth = 1.8, VerticalAlignment = VerticalAlignment.Bottom, Tag = "fixed" };
                    graphic = chart;
                    label.FontSize = 12.5; // the face is drawn at 160 px and shown at about 112 px
                    host = new Border { MinHeight = 84, ClipToBounds = true, CornerRadius = new CornerRadius(8),
                        Background = Palette.Brush(Palette.Mix(palette.Card, palette.Cool, palette.Dark ? .08 : .06)),
                        Child = new Panel { Children = { chart, new Border { Padding = new Thickness(8, 5), Child = Ui.Stack(1, label, new Viewbox { Height = 38, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = HorizontalAlignment.Left, Child = digits }) } } } };
                }
                else if (plain) host = Ui.Stack(3, label, digits);
                else
                {
                    graphic = style switch { WidgetStyle.RingPair => new RingPair(), WidgetStyle.Dial => new DialArc(), WidgetStyle.Thermostat => new KnobRing(), WidgetStyle.PurpleDash => new SweepRing(), _ => new TickRing { TickLength = .06 } };
                    graphic.Width = graphic.Height = 160;
                    digits.Children.Clear();
                    var fit = new Viewbox { Width = 128, Height = 56, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Child = new CenteredValueReadout(number, unit) };
                    label.HorizontalAlignment = HorizontalAlignment.Center;
                    var center = style == WidgetStyle.Dial ? Ui.Stack(2, fit, label) : Ui.Stack(2, label, fit); center.HorizontalAlignment = HorizontalAlignment.Center; center.VerticalAlignment = VerticalAlignment.Center;
                    if (style == WidgetStyle.RingPair && secondary?.FirstOrDefault(r => r.Key == reading.Key[..reading.Key.LastIndexOf(':')] + ":used") is { } used)
                    {
                        extra = SensorDetails.Value(Ui.Text("", 13, palette.Soft, FontWeight.DemiBold, numbers: true), used.Key);
                        extra.HorizontalAlignment = HorizontalAlignment.Center; extra.TextAlignment = TextAlignment.Center; center.Children.Add(extra);
                    }
                    var panel = new Panel { Width = 160, Height = 160, Children = { graphic, center } };
                    host = new Viewbox { Width = style == WidgetStyle.SciFi ? 128 : 112, Height = style == WidgetStyle.SciFi ? 128 : 112,
                        Stretch = Stretch.Uniform, Child = panel, HorizontalAlignment = HorizontalAlignment.Center, Tag = "graphic" };
                }
                cells[reading.Key] = new(number, unit, graphic, extra);
                Children.Add(new Border { Padding = new Thickness(3, 2), Child = host });
            }
        }
        foreach (var reading in readings)
        {
            var cell = cells[reading.Key]; var split = Format.Split(reading.Value);
            cell.Number.Text = split.Number; cell.Unit.Text = split.Unit;
            ToolTip.SetTip(cell.Number, reading.Label + ": " + reading.Value);
            if (cell.Graphic is AreaChart chart)
            {
                string id = reading.Key[6..reading.Key.LastIndexOf(':')];
                var samples = history?.Read("disk." + id + ".temp") ?? Array.Empty<double>();
                chart.IsVisible = samples.Any(double.IsFinite); chart.Max = 100; chart.Set(samples, palette.Cool);
            }
            if (cell.Graphic is RingPair pair)
            {
                var used = secondary?.FirstOrDefault(r => r.Key == reading.Key[..reading.Key.LastIndexOf(':')] + ":used");
                if (cell.Secondary != null) cell.Secondary.Text = used?.Value + " " + L.T("Used");
                cell.Number.Foreground = Palette.Brush(palette.At(reading.Fraction));
                pair.Set(reading.Fraction, palette.At(reading.Fraction), used?.Fraction, palette.Calm, Palette.Mix(palette.Card, palette.Ink, palette.Dark ? .1 : .08));
            }
            if (cell.Graphic is DialArc dial)
            {
                double? celsius = double.TryParse(split.Number, System.Globalization.NumberStyles.Float, L.Culture, out double n) ? reading.Value.Contains("°F") ? (n - 32) / 1.8 : n : null;
                Color heat = celsius.HasValue ? Accents.Readable(Color.Parse(celsius < 45 ? "#33b5f5" : celsius < 80 ? "#3ddc97" : celsius < 90 ? "#f5b33b" : "#f2555a"), palette) : palette.Ink;
                cell.Number.Foreground = Palette.Brush(heat);
                dial.Set(celsius.HasValue ? Math.Clamp((celsius.Value - 20) / 80, 0, 1) : 0, heat, palette.Dark ? Color.Parse("#2a2b2d") : Palette.Mix(palette.Card, palette.Ink, .1));
            }
            Color cool = Accents.Design("#7cc4f5", palette), warm = Accents.Design("#f6b25a", palette);
            if (cell.Graphic is KnobRing knob) knob.Set(reading.Fraction, warm, cool, palette.Dark ? Color.Parse("#23262e") : palette.Track);
            if (cell.Graphic is SweepRing sweep) sweep.Set(reading.Fraction, Accents.Design("#8b2cf5", palette), Accents.Design("#e64bd3", palette));
            if (cell.Graphic is TickRing tick) tick.Set(reading.Fraction, Accents.Design("#ec4899", palette), Accents.Design("#3b82f6", palette), palette.Dark ? Color.Parse("#2a2c33") : palette.Track);
        }
    }
}

// The number is centred on the gauge, while its unit follows the visible digits.
// A reserved numeric width must not shift the glyphs or the unit to the left.
internal sealed class CenteredValueReadout : Panel
{
    private readonly TextBlock number, unit;
    private double glyphWidth;
    public CenteredValueReadout(TextBlock number, TextBlock unit)
    {
        this.number = number; this.unit = unit;
        number.TextAlignment = TextAlignment.Center;
        unit.Margin = new Thickness(0);
        Children.Add(number); Children.Add(unit);
    }
    protected override Size MeasureOverride(Size available)
    {
        number.Measure(Size.Infinity); unit.Measure(Size.Infinity);
        var probe = new TextBlock { Text = number.Text, FontSize = number.FontSize, FontFamily = number.FontFamily,
            FontWeight = number.FontWeight, FontFeatures = number.FontFeatures, LetterSpacing = number.LetterSpacing };
        probe.Measure(Size.Infinity); glyphWidth = probe.DesiredSize.Width;
        return new Size(Math.Max(160, glyphWidth + 2 * unit.DesiredSize.Width + 12), Math.Max(number.DesiredSize.Height, unit.DesiredSize.Height + 4));
    }
    protected override Size ArrangeOverride(Size final)
    {
        double width = Math.Max(final.Width, number.DesiredSize.Width);
        number.Arrange(new Rect((final.Width - width) / 2, 0, width, final.Height));
        double x = Math.Min(final.Width - unit.DesiredSize.Width, final.Width / 2 + glyphWidth / 2 + 3);
        unit.Arrange(new Rect(x, Math.Max(0, final.Height - unit.DesiredSize.Height - 4), unit.DesiredSize.Width, unit.DesiredSize.Height));
        return final;
    }
}
