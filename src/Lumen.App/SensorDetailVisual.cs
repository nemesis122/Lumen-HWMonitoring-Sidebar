using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.LogicalTree;
using Lumen.Core;

namespace Lumen.App;

// Additional readings inherit the native design's visual vocabulary. Metadata remains text.
public sealed class SensorDetailVisual
{
    public enum Family { Plain, Segments, Table, Capsule, Ring, Level, Neon, Tiles, Trend }
    public static Family For(WidgetStyle style) => style switch
    {
        WidgetStyle.Segments or WidgetStyle.SegmentGraph or WidgetStyle.PureSegments or WidgetStyle.PixelDeck => Family.Segments,
        WidgetStyle.Table or WidgetStyle.TableGraph or WidgetStyle.BadgeListGraph or WidgetStyle.Vector => Family.Table,
        WidgetStyle.Pill or WidgetStyle.PillRow or WidgetStyle.PillRowGraph or WidgetStyle.FillBars or WidgetStyle.FillBarsGraph or WidgetStyle.RibbonFlow => Family.Capsule,
        WidgetStyle.RingPair or WidgetStyle.Orbit or WidgetStyle.Aperture or WidgetStyle.Pure or WidgetStyle.PureBar or WidgetStyle.PureWidgetsRight => Family.Ring,
        WidgetStyle.LevelColumns or WidgetStyle.Thermostat or WidgetStyle.Thermal => Family.Level,
        WidgetStyle.NeonHud or WidgetStyle.NeonHudGraph or WidgetStyle.PurpleDash or WidgetStyle.PurpleList or WidgetStyle.SciFi or WidgetStyle.Circuit => Family.Neon,
        WidgetStyle.NeonTiles or WidgetStyle.PureWidgets or WidgetStyle.ClarityTiles or WidgetStyle.MosaicPro or WidgetStyle.SplitLens or WidgetStyle.SmartPanel or WidgetStyle.BadgeList => Family.Tiles,
        WidgetStyle.Dial or WidgetStyle.HistoryGraph or WidgetStyle.TwinFocus or WidgetStyle.PerformanceView or WidgetStyle.HorizonSync or WidgetStyle.Panorama => Family.Trend,
        _ => Family.Plain
    };

    public Control Root { get; }
    public Control? Graphic { get; }
    private readonly Palette palette;
    private readonly WidgetKind kind;
    private readonly Settings settings;
    private readonly List<double> samples = new();
    private DateTime time;
    private readonly Action<Reading>? nativeUpdate;
    private readonly Action<double>? nativePaint;
    private readonly TextBlock valueText;

    public SensorDetailVisual(TextBlock label, TextBlock value, CompleteReading row, WidgetStyle style, WidgetKind kind, Settings settings, Palette palette, Control? nativeRoot = null)
    {
        this.palette = palette; this.settings = settings; this.kind = kind; valueText = value;
        var metrics = Metrics.For(settings.Density);
        Family family = For(style);
        if (kind == WidgetKind.Drives && row.Key.EndsWith(":model"))
        {
            Root = Naming.DriveHardwareRow(label, value, row.Label, style, palette);
            return;
        }
        bool metadata = row.Address || row.Key.EndsWith(":model") || row.Key is "drives.count" or "system.os" or "system.boot" or "media.title" or "media.artist" or "media.source" or "media.progress" or "fps.app" or "weather.place" or "weather.sky"
            || row.Key.StartsWith("clock.") || row.Key.StartsWith("calendar.") || row.Key.StartsWith("world:")
            || !row.Value.Any(char.IsDigit) && row.Value != "—";
        if (metadata) { Root = new StableReadingRow(label,value,settings,metrics,90); return; }
        var nativeDrives = kind == WidgetKind.Drives ? nativeRoot?.GetLogicalDescendants().OfType<DriveRows>().FirstOrDefault() : null;
        if (nativeDrives != null)
        {
            (Root, Graphic, nativeUpdate) = nativeDrives.Detail(label, value);
            Graphic.Tag = "fixed";
            return;
        }
        if (kind == WidgetKind.Drives && nativeRoot != null)
        {
            var source = nativeRoot.GetLogicalDescendants().OfType<Control>().FirstOrDefault(c =>
                c is FillBack or Meter or RingBar or LevelBar or GradientBar or SegmentBar
                && SensorDetails.Visible(c, nativeRoot) && !c.GetLogicalAncestors().OfType<SensorDetails>().Any());
            (Graphic, nativePaint) = source switch
            {
                FillBack f => Copy(f), Meter m => Copy(m), RingBar r => Copy(r), LevelBar l => Copy(l),
                GradientBar g => Copy(g), SegmentBar s => Copy(s), _ => (null, null)
            };
            if (Graphic != null)
            {
                Graphic.Height = source!.Height; Graphic.Width = source.Width;
                Graphic.Tag = "fixed";
                var number = nativeRoot.GetLogicalDescendants().OfType<TextBlock>().FirstOrDefault(t =>
                    SensorDetails.KeyOf(t).StartsWith("drive:") && !SensorDetails.KeyOf(t).EndsWith(":model")
                    && SensorDetails.Visible(t, nativeRoot) && !t.GetLogicalAncestors().OfType<SensorDetails>().Any());
                if (number != null) { value.Foreground = number.Foreground; value.FontWeight = number.FontWeight; value.FontFamily = number.FontFamily; }
                var line = new StableReadingRow(label, value, settings, metrics, 90);
                if (Graphic is LevelBar)
                {
                    Graphic.Margin = new Thickness(0, 0, 8, 0);
                    Root = Ui.Columns("Auto,*", Graphic, line);
                }
                else Root = Ui.Stack(4, line, Graphic);
                return;
            }
            // A text/tile design does not gain an unrelated generic graph for additional values.
            Root = new StableReadingRow(label, value, settings, metrics, 90);
            return;
        }
        if (family == Family.Plain) { Root = new StableReadingRow(label,value,settings,metrics,90); return; }
        Graphic = family switch
        {
            Family.Segments => new Meter { Palette = palette, Segments = 20, Height = 12 },
            Family.Table => style == WidgetStyle.BadgeListGraph ? new FillBack { Height = 5, Radius = 2.5, Track = palette.Track } : new Meter { Palette = palette, Height = 5 },
            Family.Capsule => new FillBack { Height = 6, Radius = 4, Track = palette.Track },
            Family.Ring => new RingBar { Height = 6 },
            Family.Level => new LevelBar { Width = 8, Height = 24 },
            Family.Neon => new SegmentBar { Height = 7 },
            Family.Tiles => new FillBack { Height = 3, Radius = 2, Track = palette.Track },
            Family.Trend => new AreaChart { Height = 18, FillOpacity = .12, LineWidth = 1.3 },
            _ => null
        };
        if (Graphic == null) { Root = new StableReadingRow(label,value,settings,metrics,90); return; }
        Graphic.Tag = "fixed";
        if (family is Family.Segments or Family.Table)
        {
            label.TextWrapping = TextWrapping.Wrap; value.TextWrapping = TextWrapping.NoWrap;
            value.TextAlignment = TextAlignment.Right;
            // Match the native name–bar–value table, with one shared value width for every row.
            value.MinWidth = 86 * (settings.ActiveWidget?.Values ?? 1);
            // Auto columns share the native table's widths, including long translated labels.
            label.MinWidth = 60 * (settings.ActiveWidget?.Labels ?? 1);
            Graphic.Margin = style == WidgetStyle.BadgeListGraph ? new Thickness(10,0,0,0)
                : family == Family.Table ? new Thickness(10,0,10,0) : new Thickness(8,0,12,0);
            Graphic.VerticalAlignment = VerticalAlignment.Center;
            Control valueCell = style == WidgetStyle.BadgeListGraph ? new Border { Child = value, CornerRadius = new CornerRadius(9),
                Padding = new Thickness(9,2), Margin = new Thickness(8,3,0,3), HorizontalAlignment = HorizontalAlignment.Right } : value;
            Root = Ui.Columns("Auto,*,Auto", label, Graphic, valueCell);
        }
        else if (family == Family.Level)
        {
            Control line = new StableReadingRow(label,value,settings,metrics,90);
            Graphic.Margin = new Thickness(0, 0, 8, 0);
            Root = Ui.Columns("Auto,*", Graphic, line);
        }
        else
        {
            Control line = new StableReadingRow(label,value,settings,metrics,90);
            Root = Ui.Stack(2, line, Graphic);
            if (family == Family.Tiles) Root = new Border { Background = Palette.Brush(Palette.Mix(palette.Card, palette.Cool, .06)),
                CornerRadius = new CornerRadius(5), Padding = new Thickness(7, 4), Child = Root };
        }
    }

    public void Update(CompleteReading row, Snapshot snapshot)
    {
        if (kind == WidgetKind.Drives && DriveTransferPairs.IsRate(row)) valueText.Foreground = Palette.Brush(palette.Ink);
        if (Graphic == null) return;
        double? fraction = row.Fraction ?? SegmentScale.Fraction(kind, row.Label, row.Value, snapshot);
        double? rawCounter = null;
        // For cumulative counters the bar uses a labelled decimal auto scale, not a fabricated percent.
        if (!fraction.HasValue && row.Available && double.TryParse(Format.Split(row.Value).Number, System.Globalization.NumberStyles.Float, L.Culture, out double number))
        {
            double maximum = Math.Pow(10, Math.Max(1, Math.Ceiling(Math.Log10(Math.Max(1, Math.Abs(number))))));
            fraction = Math.Clamp(number / maximum, 0, 1);
            rawCounter = number;
            ToolTip.SetTip(Graphic, row.Label + " · " + L.T("Automatic") + ": 0–" + maximum.ToString("0", L.Culture) + " · " + row.Value);
        }
        else ToolTip.SetTip(Graphic, row.Label + ": " + (row.Available ? row.Value : L.T("Not available")));
        double level = fraction ?? 0;
        Graphic.Opacity = row.Available ? 1 : .3;
        if (nativePaint != null) { nativePaint(level); return; }
        // Legacy designs use the palette ramp. Widget accents belong to designs that opt into them.
        var accent = kind == WidgetKind.Drives ? palette.Cool : Accents.For(kind, settings.ActiveWidget, palette);
        if (nativeUpdate != null)
        {
            nativeUpdate(new Reading(row.Label, row.Value, level, row.Key.EndsWith(":temp") ? palette.At(level) : accent) { Key = row.Key });
            if (DriveTransferPairs.IsRate(row)) valueText.Foreground = Palette.Brush(palette.Ink);
            return;
        }
        switch (Graphic)
        {
            case Meter meter: meter.Set(level, accent); break;
            case FillBack fill: fill.Animate(level, accent); break;
            case RingBar ring: ring.Set(level, accent, palette.Track); break;
            case LevelBar column: column.Set(level, accent, palette.Track); break;
            case SegmentBar segments: segments.Set(level, accent, palette.Calm, palette.Track); break;
            case AreaChart chart:
                if (snapshot.Time != time) { time = snapshot.Time; samples.Add(row.Available && fraction.HasValue ? rawCounter ?? level : double.NaN); if (samples.Count > History.Capacity) samples.RemoveAt(0); }
                // Counter histories retain raw numbers when their automatic scale changes.
                chart.Max = rawCounter.HasValue ? Math.Max(1, samples.Where(double.IsFinite).DefaultIfEmpty(1).Max()) : 1;
                chart.Set(samples.ToArray(), accent); break;
        }
    }

    private static (Control, Action<double>) Copy(FillBack source)
    {
        var target = new FillBack();
        return (target, value => { target.Radius = source.Radius; target.Track = source.Track; target.Animate(value, source.Fill); });
    }
    private static (Control, Action<double>) Copy(Meter source)
    {
        var target = new Meter();
        return (target, value => { target.Palette = source.Palette; target.Segments = source.Segments; target.RampBySegment = source.RampBySegment; target.Vertical = source.Vertical; target.Set(value, source.Color); });
    }
    private static (Control, Action<double>) Copy(RingBar source) { var target = new RingBar(); return (target, value => source.CopyAppearanceTo(target, value)); }
    private static (Control, Action<double>) Copy(LevelBar source) { var target = new LevelBar(); return (target, value => source.CopyAppearanceTo(target, value)); }
    private static (Control, Action<double>) Copy(GradientBar source) { var target = new GradientBar(); return (target, value => source.CopyAppearanceTo(target, value)); }
    private static (Control, Action<double>) Copy(SegmentBar source) { var target = new SegmentBar(); return (target, value => source.CopyAppearanceTo(target, value)); }
}
