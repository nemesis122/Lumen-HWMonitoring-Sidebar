using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

// A device owns one read/write pair. No transfer bar or history is inserted by a fallback.
public sealed class DriveTransferPairs : StackPanel
{
    public static readonly WidgetStyle[] Styles = Settings.Selectable.ToArray();
    public static bool Applies(WidgetStyle style) => Styles.Contains(Settings.Successor(style));
    public static bool IsRate(CompleteReading row) => row.Key.StartsWith("drive:") && (row.Key.EndsWith(":read") || row.Key.EndsWith(":write"));
    private string shape = "";
    private readonly Dictionary<string, TextBlock> values = new();
    private readonly Dictionary<string, DialArc> rings = new();
    private readonly Dictionary<string, Meter> meters = new();
    private readonly Dictionary<string, FillBack> fills = new();
    private readonly Dictionary<string, GradientBar> gradients = new();
    public static bool KeepsGraph(WidgetStyle style) => style is WidgetStyle.SegmentGraph or WidgetStyle.TableGraph or WidgetStyle.FillBarsGraph or WidgetStyle.PillRowGraph or WidgetStyle.BadgeListGraph or WidgetStyle.NeonHudGraph;
    public void Update(CompleteReading[] rows, WidgetStyle style, Palette palette, Snapshot snapshot, double capsuleHeight = 44)
    {
        IsVisible = rows.Length > 0;
        string next = style + capsuleHeight.ToString(System.Globalization.CultureInfo.InvariantCulture) + string.Join('|', rows.Select(r => r.Key + r.Label));
        if (next != shape)
        {
            bool local = LocalDesigns.Contains(style);
            shape = next; Children.Clear(); values.Clear(); rings.Clear(); meters.Clear(); fills.Clear(); gradients.Clear(); Spacing = local ? 2 : 8;
            foreach (var disk in rows.GroupBy(r => r.Key[..r.Key.LastIndexOf(':')]))
            {
                string driveName = disk.First().Label.Split(" · ")[0];
                var group = Ui.Stack(local ? 1 : 3);
                if (!local) group.Children.Add(Ui.Text(driveName, 10.5, palette.Soft));
                var cells = new List<Control>();
                foreach (var row in disk)
                {
                    bool write = row.Key.EndsWith(":write");
                    Color accent = style is WidgetStyle.PurpleDash or WidgetStyle.PurpleList ? Accents.Design(write ? "#e64bd3" : "#8b2cf5", palette)
                        : style is WidgetStyle.NeonHud or WidgetStyle.NeonHudGraph or WidgetStyle.SciFi ? Accents.Design(write ? "#ec4899" : "#22d3ee", palette)
                        : write ? palette.Calm : palette.Cool;
                    bool mono = style is WidgetStyle.NeonHud or WidgetStyle.NeonHudGraph;
                    bool pure = style is WidgetStyle.Pure or WidgetStyle.PureBar or WidgetStyle.PureNumber or WidgetStyle.PureSegments or WidgetStyle.PureWidgets or WidgetStyle.PureWidgetsRight;
                    bool compact = style is WidgetStyle.CompactPro or WidgetStyle.Clarity or WidgetStyle.ClarityRows or WidgetStyle.ClarityColor
                        or WidgetStyle.Segments or WidgetStyle.SegmentGraph or WidgetStyle.Table or WidgetStyle.TableGraph;
                    var label = Ui.Text((local ? driveName + " · " : "") + L.T(write ? "Write" : "Read"), local || mono ? 10 : pure ? 11 : 10.5, palette.Soft);
                    var value = SensorDetails.Value(Ui.Text("", local ? 12 : mono ? 11 : compact ? 12.5 : pure ? 16 : 14, palette.Ink, pure ? FontWeight.DemiBold : FontWeight.SemiBold, numbers: true), row.Key);
                    value.MinWidth = 84; value.TextWrapping = TextWrapping.NoWrap;
                    values[row.Key] = value;
                    Control cell = Ui.Stack(local ? 0 : 2, label, value);
                    if (style == WidgetStyle.SplitFlap)
                    {
                        cell = new Border { Background = Palette.Brush(Palette.Mix(palette.Card,palette.Ink,.04)), BorderBrush = Palette.Brush(palette.Stroke), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), Padding = new Thickness(6,3), Child = cell };
                        value.FontFamily = Ui.FamilyFor("mono");
                    }
                    else if (style == WidgetStyle.HardwareMap)
                        cell = new Border { BorderBrush = Palette.Brush(palette.Stroke), BorderThickness = new Thickness(1,0,0,0), Padding = new Thickness(6,0,0,0), Child = cell };
                    else if (style == WidgetStyle.NeonTiles)
                    {
                        ((StackPanel)cell).Children.Clear();
                        label.Text = label.Text?.ToUpper(L.Culture);
                        label.FontFamily = new FontFamily("Cascadia Mono, Consolas, DejaVu Sans Mono, fonts:Inter#Inter, $Default");
                        label.FontSize = 8.5; label.FontWeight = FontWeight.Bold;
                        label.Foreground = Palette.Brush(Accents.Design(write ? "#ec4899" : "#22d3ee", palette));
                        label.HorizontalAlignment = HorizontalAlignment.Center;
                        label.TextTrimming = TextTrimming.CharacterEllipsis;
                        value.FontSize = 16; value.FontWeight = FontWeight.Bold;
                        cell = NeonTilesTile.MetricTile(label, value, palette);
                    }
                    else if (style is WidgetStyle.SegmentGraph or WidgetStyle.TableGraph)
                    {
                        var meter = new Meter { Height = style == WidgetStyle.SegmentGraph ? 12 : 5, Segments = style == WidgetStyle.SegmentGraph ? 20 : 0, Palette = palette, Tag = "fixed" };
                        meters[row.Key] = meter;
                        ((StackPanel)cell).Children.Add(meter);
                    }
                    else if (style == WidgetStyle.FillBarsGraph)
                    {
                        var fill = new FillBack { Radius = 10, Track = Palette.Mix(palette.Card, palette.Ink, palette.Dark ? .06 : .05), Tag = "fixed" };
                        fills[row.Key] = fill;
                        value.TextAlignment = TextAlignment.Right;
                        cell = new Panel { Height = 42, Children = { fill, new Border { Padding = new Thickness(8, 3), Child = cell } } };
                    }
                    else if (style is WidgetStyle.ClarityTiles or WidgetStyle.Dial or WidgetStyle.ControlDeck or WidgetStyle.PureWidgets or WidgetStyle.PureWidgetsRight)
                        cell = new Border { Background = Palette.Brush(Palette.Mix(palette.Card, palette.Ink, pure ? .06 : .04)), CornerRadius = new CornerRadius(pure ? 14 : style == WidgetStyle.Dial ? 8 : 5), Padding = new Thickness(pure ? 9 : 7, pure ? 8 : 5), Child = cell };
                    else if (style is WidgetStyle.ClarityColor or WidgetStyle.ClarityRows)
                        cell = new Border { BorderBrush = Palette.Brush(accent), BorderThickness = new Thickness(2, 0, 0, 0),
                            Background = style == WidgetStyle.ClarityRows ? Palette.Brush(Palette.Alpha(accent, .06)) : Brushes.Transparent,
                            Padding = new Thickness(5, 2), Child = cell };
                    else if (style == WidgetStyle.PillRowGraph)
                    {
                        ((StackPanel)cell).Children.Clear();
                        label.Text = label.Text?.ToUpper(L.Culture); label.FontSize = 9.5; label.FontWeight = FontWeight.DemiBold; label.LetterSpacing = .5;
                        label.HorizontalAlignment = value.HorizontalAlignment = HorizontalAlignment.Center;
                        value.FontSize = 13.5; value.FontWeight = FontWeight.Bold; value.TextAlignment = TextAlignment.Center;
                        var fill = new FillBack { Radius = capsuleHeight / 2, Track = Palette.Mix(palette.Card, palette.Ink, palette.Dark ? .06 : .05), Tag = "fixed" };
                        fills[row.Key] = fill;
                        cell = PillRowTile.Bubble(label, value, fill, capsuleHeight, new Thickness(0));
                    }
                    else if (style == WidgetStyle.BadgeListGraph)
                    {
                        ((StackPanel)cell).Children.Clear();
                        var badge = new Border { CornerRadius = new CornerRadius(9), Padding = new Thickness(9, 2), Background = Palette.Brush(Palette.Alpha(palette.Cool, palette.Dark ? .2 : .16)), Child = value };
                        value.TextAlignment = TextAlignment.Center;
                        var fill = new FillBack { Height = 5, Radius = 2.5, Track = palette.Track, Tag = "fixed" };
                        fills[row.Key] = fill;
                        cell = Ui.Stack(3, label, badge, fill);
                    }
                    else if (style == WidgetStyle.NeonHudGraph)
                    {
                        var gradient = new GradientBar { Height = 4, Tag = "fixed" };
                        gradients[row.Key] = gradient; ((StackPanel)cell).Children.Add(gradient);
                    }
                    else if (style is WidgetStyle.Pill or WidgetStyle.PillRow)
                    {
                        label.Text = label.Text?.ToUpper(L.Culture);
                        label.HorizontalAlignment = value.HorizontalAlignment = HorizontalAlignment.Center;
                        value.TextAlignment = TextAlignment.Center;
                        cell = new Border { Background = Palette.Brush(Palette.Mix(palette.Card, palette.Ink, .06)), CornerRadius = new CornerRadius(22), Padding = new Thickness(8, 5), Child = cell };
                    }
                    else if (style == WidgetStyle.SmartPanel)
                    {
                        ((StackPanel)cell).Children.Clear();
                        cell = Ui.Stack(3, Ui.Row(0, new Border { Width = 7, Height = 7, CornerRadius = new CornerRadius(2), Background = Palette.Brush(Accents.Design(write ? "#f472b6" : "#f59e0b", palette)), Margin = new Thickness(0, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center }, label), value);
                    }
                    if (style is WidgetStyle.Thermostat or WidgetStyle.PerformanceView)
                    { label.Text = label.Text?.ToUpper(L.Culture); label.LetterSpacing = style == WidgetStyle.Thermostat ? 1.2 : .5; }
                    if (style is WidgetStyle.NeonHud or WidgetStyle.NeonHudGraph or WidgetStyle.SciFi or WidgetStyle.Terminal)
                    { label.FontFamily = value.FontFamily = Ui.FamilyFor("mono"); }
                    if (style == WidgetStyle.SciFi) label.FontFamily = value.FontFamily = new FontFamily("Bahnschrift, DIN Alternate, fonts:Inter#Inter, $Default");
                    cells.Add(cell);
                }
                {
                    var pair = new ResponsiveGrid { ColumnDefinitions = new ColumnDefinitions(cells.Count == 2 ? "*,*" : "*") };
                    for (int i = 0; i < cells.Count; i++) { cells[i].Margin = new Thickness(i == 0 ? 0 : 6, 0, 0, 0); Grid.SetColumn(cells[i], i); pair.Children.Add(cells[i]); }
                    group.Children.Add(pair);
                }
                Children.Add(group);
            }
        }
        foreach (var row in rows)
        {
            values[row.Key].Text = row.Value; ToolTip.SetTip(values[row.Key], row.Label + ": " + row.Value);
            double fraction = SegmentScale.Fraction(WidgetKind.Drives, row.Label, row.Value, snapshot) ?? 0;
            if (meters.TryGetValue(row.Key, out var meter)) meter.Set(fraction, palette.Cool);
            if (fills.TryGetValue(row.Key, out var fill)) fill.Animate(fraction, style == WidgetStyle.BadgeListGraph ? palette.Cool : Palette.Alpha(palette.Cool, palette.Dark ? .34 : .28));
            if (gradients.TryGetValue(row.Key, out var gradient)) gradient.Set(fraction, Accents.Design("#22d3ee", palette), Accents.Design("#ec4899", palette), palette.Dark ? Color.Parse("#1a2230") : palette.Track);
            if (rings.TryGetValue(row.Key, out var ring))
            {
                ring.Set(SegmentScale.Fraction(WidgetKind.Drives, row.Label, row.Value, snapshot) ?? 0,
                    row.Key.EndsWith(":write") ? palette.Calm : palette.Cool, palette.Track);
                ToolTip.SetTip(ring, row.Label + ": " + row.Value + " · " + L.T("Automatic"));
            }
        }
    }
}
