using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

// Pure Widgets with every supplementary CPU/GPU reading in the right-hand tile.
// The natural height follows the selected rows; no facts are cut off or moved below the ring.
public sealed class RightWidgetsTile : AppleTile
{
    private readonly TextBlock name, hardware, main, secondary;
    private readonly AppleRing temperatureRing = new() { Thickness = .11 };
    private readonly AppleRing loadRing = new() { Thickness = .11, Margin = new Thickness(15) };
    private readonly Panel readings;
    private readonly Dictionary<string, TextBlock> cells = new();
    private string shape = "";
    public IReadOnlyList<string> RightKeys { get; private set; } = Array.Empty<string>();

    public RightWidgetsTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s) : base(kind, index, p, m, s)
    {
        Padding = new Thickness(12);
        readings = s.Horizontal
            ? new StatPanel(Array.Empty<double>(), Array.Empty<Control>()) { MaxColumns = 2, MinimumColumnWidth = 100, Balanced = true, RowSpacing = 6, VerticalAlignment = VerticalAlignment.Center }
            : new StackPanel { Spacing = 7, VerticalAlignment = VerticalAlignment.Center };
        var header = Header(); name = header.Name; hardware = header.Hardware;
        header.Row.Margin = new Thickness(6, 2, 6, 0);
        main = Text("—", 28, Ink, FontWeight.DemiBold, null, true);
        main.HorizontalAlignment = HorizontalAlignment.Center;
        secondary = Text("—", 12, Ink, FontWeight.Medium, null, true);
        secondary.HorizontalAlignment = HorizontalAlignment.Center;
        var centre = Ui.Stack(1, Fit(main, HorizontalAlignment.Center), secondary);
        centre.VerticalAlignment = VerticalAlignment.Center;
        centre.Margin = new Thickness(25);
        var ring = new Panel { Children = { temperatureRing, loadRing, centre }, MinHeight = 115, Tag = "graphic" };
        var left = Tile(new Square { Child = ring, MaxSide = HorizontalGraphicLimit });
        left.Tag = "right-widgets-ring";
        var right = Tile(readings);
        right.Tag = "right-widgets-values";
        var content = Ui.Columns(s.Horizontal ? "Auto,*" : "*,*", left, right);
        content.ColumnSpacing = 8;
        Child = Ui.Stack(10, header.Row, content);
        Child.Tag = "elastic";
    }

    private Border Tile(Control child) => new()
    {
        Background = Palette.Brush(P.DesignTile("#2a2a2d")),
        CornerRadius = new CornerRadius(18),
        Padding = new Thickness(12),
        Child = child
    };

    protected override void Show(Summary sum)
    {
        ShowHeader(name, hardware, sum);
        main.Text = sum.Value == "—" ? "—" : sum.Value + (sum.Unit is "°C" or "°F" ? "°" : sum.Unit);
        secondary.Text = sum.Second is double load ? Format.Percent(load * 100) : "—";
        secondary.IsVisible = S.Shows(WidgetSensors.SecondaryKey(Kind));
        temperatureRing.IsVisible = S.Shows(WidgetSensors.PrimaryKey(Kind));
        loadRing.IsVisible = secondary.IsVisible;
        temperatureRing.Set(sum.Value == "—" ? 0 : sum.Fraction, Opaque(Shade(sum)), RingTrack);
        loadRing.Set(sum.Second ?? 0, Tint, RingTrack);
        var rows = CompleteReadings.For(Kind, Index, Last.Snap, Last.History, S, P)
            .Where(r => r.Key != WidgetSensors.PrimaryKey(Kind) && r.Key != WidgetSensors.SecondaryKey(Kind)).ToList();
        if (Kind == WidgetKind.Gpu && S.Shows("gpu.junction") && GpuGroup.Visible(Last.Snap, S).ElementAtOrDefault(Index)?.MemoryJunction is double junction)
            rows.Add(new("gpu.junction", L.T("Memory junction"), Format.Temperature(junction, S.Fahrenheit) + " " + Format.TemperatureUnit(S.Fahrenheit)));
        rows = Lumen.App.Steady.Keep("right-widget-readings|" + Kind + "|" + Index, S, Last.Snap, rows, r => r.Key, r => r.Value.Length == 0 || r.Value.StartsWith("—"));
        RightKeys = rows.Select(r => r.Key).ToArray();
        string next = string.Join("|", rows.Select(r => r.Key + ":" + r.Label));
        if (next != shape)
        {
            shape = next; readings.Children.Clear(); cells.Clear();
            foreach (var row in rows)
            {
                var label = Text(row.Label, 11.5, Soft);
                label.TextWrapping = TextWrapping.Wrap;
                label.TextTrimming = TextTrimming.None;
                var value = Text("—", 15.5, Ink, FontWeight.DemiBold, null, true);
                value.Tag = "right-reading:" + row.Key;
                cells.Add(row.Key, value);
                readings.Children.Add(Ui.Stack(1, label, Fit(value)));
            }
        }
        foreach (var row in rows) { cells[row.Key].Text = row.Value; ToolTip.SetTip(cells[row.Key], row.Label + ": " + row.Value); }
    }
}
