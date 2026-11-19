using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed partial class AioTile
{
    private AioReading[] atlasDriveReadings = Array.Empty<AioReading>();
    private bool atlasDrivesPlaced;
    private static readonly string[] atlasDriveMetrics = { "temp", "read", "write", "used" };

    private Color AtlasDriveAccent(bool capacity = false, bool reading = false)
    {
        if (Color.TryParse(DriveOwner?.Accent, out var selected)) return Accents.Readable(selected, palette);
        if (Color.TryParse(slot?.Accent, out selected)) return Accents.Readable(selected, palette);
        if (skin is Color hue) return Accents.Readable(hue, palette);
        if (MainboardDesign(style) || AioDesigns.Dashboard(style))
            return Accents.Readable(capacity ? MapViolet : MapCyan, palette);
        return Accents.Readable(capacity ? Purple : reading ? Cyan : Amber, palette);
    }

    private static string AtlasDrivePart(AioReading reading) => reading.Key.StartsWith("aio.system.disk.") ? "temp"
        : reading.Key[(reading.Key.LastIndexOf('.') + 1)..];

    private Control? AtlasDriveBody(IEnumerable<AioReading> readings, bool heading = true)
    {
        if (atlasDrivesPlaced) return null;
        var groups = AtlasDriveGroups(readings);
        if (groups.Length == 0) return null;
        atlasDrivesPlaced = true;
        bool compact = AioDesigns.CompactDriveLayout(designStyle);
        var body = Stack(7);
        var owner = DriveOwner;
        if (heading && slot?.ShowTitle != false && owner?.ShowTitle != false && (compact || !string.IsNullOrWhiteSpace(owner?.Name)))
        {
            string name = Naming.Title(owner, 0, L.T("Drives"));
            var title = AtlasDriveText(name, 13, AtlasDriveAccent());
            TextFonts.Mark(title, TextRole.Title);
            title.FontWeight = FontWeight.SemiBold;
            body.Children.Add(FitLeft(title));
        }
        double scale = Math.Max(settings.ActiveWidget?.Values ?? 1, settings.ActiveWidget?.Labels ?? 1);
        if (compact)
        {
            var present = groups.SelectMany(g => g).Select(AtlasDrivePart).ToHashSet();
            var columns = atlasDriveMetrics.Where(present.Contains).ToArray();
            foreach (var group in groups) body.Children.Add(AtlasDrive(group, columns, scale));
        }
        else body.Children.Add(new MainboardDriveGrid(groups.Select(group => AtlasDrive(group))) { MinimumColumnWidth = 125 * Math.Max(1, scale) });
        return body;
    }

    private IGrouping<string, AioReading>[] AtlasDriveGroups(IEnumerable<AioReading> readings)
    {
        var order = DriveOrdering.Order(snapshot.Disks, DriveOrdering.Mode(settings.ForWidget(WidgetKind.Drives)))
            .Select((d, i) => (d.Id, i)).ToDictionary(d => d.Id, d => d.i);
        return readings.GroupBy(r => r.Device).OrderBy(g => order.GetValueOrDefault(g.Key, int.MaxValue)).ToArray();
    }

    private Control AtlasDrive(IGrouping<string, AioReading> group, string[]? compactColumns = null, double scale = 1)
    {
        var disk = snapshot.Disks.FirstOrDefault(d => d.Id == group.Key);
        int index = disk == null ? 0 : snapshot.Disks.ToList().IndexOf(disk);
        var readouts = group.ToDictionary(AtlasDrivePart);
        string letters = disk?.Letters.Length > 0 ? disk.Letters : AioReadings.DriveName(snapshot, group.Key);
        string name = Naming.Title(DriveOwner, index, letters);
        string model = DriveModel(group.Key);
        var headingColor = AtlasDriveAccent();
        var badge = AtlasDriveBadge(name, headingColor, group.First().Key);
        if (compactColumns != null)
        {
            Control icon = red ? new PanelArt { Kind = "ssd", Theme = PanelTheme.For("red", headingColor),
                Width = 36, Height = 19, Tag = "graphic", HorizontalAlignment = HorizontalAlignment.Center } : Part("ssd", 40, 22);
            var left = badge == null ? Stack(2, icon) : Stack(2, badge, icon);
            var details = Stack(3);
            AddDriveModel(details, model);
            var cells = compactColumns.Select(part =>
            {
                if (!readouts.TryGetValue(part, out var reading)) return (Control)new Border();
                string label = part switch { "temp" => L.T("Temp."), "read" => "R", "write" => "W", _ => L.T("Used") };
                var color = AtlasDriveAccent(capacity: part == "used", reading: part == "read");
                return Stack(1, FitLeft(AtlasDriveCaption(reading, label)), FitLeft(AtlasDriveNumber(reading, 12, color)));
            }).ToArray();
            if (cells.Length > 0) details.Children.Add(new CompactDriveReadouts(cells, compactColumns, scale));
            if (readouts.TryGetValue("used", out var used)) details.Children.Add(AtlasCapacityMeter(used, 4));
            AddAdditionalDriveReadouts(details, readouts);
            var row = Ui.Columns("Auto,*", left, details);
            row.ColumnSpacing = 7;
            return new Border { Child = row, Tag = "atlas-drive:" + group.Key };
        }
        var art = new Grid { Children = { new Border { Margin = new Thickness(0, 6, 0, 0), Child = Part("ssd", 92, 56) } } };
        if (badge != null) art.Children.Add(badge);
        var body = Stack(5, art);
        AddDriveModel(body, model);
        if (readouts.TryGetValue("temp", out var temperature))
            body.Children.Add(AtlasDriveValue(temperature, L.T("Temp."), 19, headingColor));
        var rates = new List<Control>(2);
        foreach (var (key, label) in new[] { ("read", "R"), ("write", "W") })
        {
            if (!readouts.TryGetValue(key, out var rate)) continue;
            rates.Add(Stack(2, FitLeft(AtlasDriveCaption(rate, label)), FitLeft(AtlasDriveNumber(rate, 12, ink))));
        }
        if (rates.Count > 0)
        {
            var rateGrid = FixedCols(rates.Count, rates.ToArray());
            rateGrid.ColumnSpacing = 8;
            body.Children.Add(rateGrid);
        }
        if (readouts.TryGetValue("used", out var capacity))
        {
            body.Children.Add(AtlasDriveValue(capacity, L.T("Used"), 12, ink));
            body.Children.Add(AtlasCapacityMeter(capacity, 5));
        }
        AddAdditionalDriveReadouts(body, readouts);

        var tile = Tile(body, headingColor);
        tile.Tag = "atlas-drive:" + group.Key;
        if (MainboardDesign(style) || AioDesigns.Dashboard(style)) tile.CornerRadius = new CornerRadius(7);
        else if (style == WidgetStyle.AioTypeBlock) tile.CornerRadius = new CornerRadius(6);
        return tile;
    }

    private Border? AtlasDriveBadge(string name, Color color, string key)
    {
        if (DriveOwner?.ShowTitle == false || slot?.ShowTitle == false) return null;
        var text = TextFonts.Mark(AtlasDriveText(name, 10, color), TextRole.Title);
        ToolTip.SetTip(text, name);
        return TextColours.Frame(new Border { Child = text, Padding = new Thickness(4, 1), CornerRadius = new CornerRadius(4),
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, BorderThickness = new Thickness(1),
            BorderBrush = Palette.Brush(color), Background = Palette.Brush(SeeThrough(Palette.Mix(back, color, .12))) }, key);
    }

    private void AddDriveModel(StackPanel body, string model)
    {
        if (model.Length == 0) return;
        var label = AtlasDriveText(model, 9, muted);
        ToolTip.SetTip(label, model);
        body.Children.Add(label);
    }

    private Control AtlasCapacityMeter(AioReading capacity, double height)
    {
        var meter = new AioMeter(AtlasDriveAccent(capacity: true), track, 0) { Height = height, Tag = "graphic" };
        meters.Add((capacity.Key, meter));
        return TextColours.Bind(meter, capacity.Key);
    }

    private void AddAdditionalDriveReadouts(StackPanel body, Dictionary<string, AioReading> readouts)
    {
        foreach (var (part, reading) in readouts.Where(p => !atlasDriveMetrics.Contains(p.Key)))
            body.Children.Add(AtlasDriveValue(reading, L.T(reading.Label), 12, ink));
    }

    private TextBlock AtlasDriveText(string text, double size, Color color)
    {
        var label = Text(text, size, color);
        label.TextWrapping = TextWrapping.NoWrap;
        label.TextTrimming = TextTrimming.CharacterEllipsis;
        return label;
    }

    private TextBlock AtlasDriveNumber(AioReading r, double size, Color color)
    {
        var number = V(r, size, color);
        number.FontWeight = FontWeight.SemiBold;
        number.TextWrapping = TextWrapping.NoWrap;
        return number;
    }

    private TextBlock AtlasDriveCaption(AioReading reading, string label)
    {
        var caption = TextColours.Bind(AtlasDriveText(label, 10, muted), reading.Key);
        ToolTip.SetTip(caption, DiskReading(reading) && AtlasDrivePart(reading) == "temp" ? L.T("Temperature") : L.T(reading.Label));
        return caption;
    }

    private Control AtlasDriveValue(AioReading reading, string label, double size, Color color)
    {
        var row = Ui.Columns("*,1.5*", FitLeft(AtlasDriveCaption(reading, label)), FitRight(AtlasDriveNumber(reading, size, color)));
        row.ColumnSpacing = 5;
        return row;
    }
}

internal sealed class CompactDriveReadouts : Grid
{
    private readonly string[] columns;
    private readonly double minimumWidth;
    private bool? wrapped;

    public CompactDriveReadouts(Control[] cells, string[] columns, double scale)
    {
        this.columns = columns;
        minimumWidth = (columns.Length == 4 ? 158 : 120) * Math.Max(1, scale);
        ColumnSpacing = 4;
        RowSpacing = 3;
        Children.AddRange(cells);
    }

    protected override Size MeasureOverride(Size available)
    {
        bool wrap = columns.Length > 2 && available.Width < minimumWidth;
        if (wrapped != wrap)
        {
            wrapped = wrap;
            ColumnDefinitions = new ColumnDefinitions(wrap ? "*,*" : string.Join(',', columns.Select(key =>
                key is "read" or "write" ? "1.22*" : ".85*")));
            RowDefinitions = new RowDefinitions(wrap ? "Auto,Auto" : "Auto");
            for (int i = 0; i < Children.Count; i++)
            {
                SetColumn(Children[i], wrap ? i % 2 : i);
                SetRow(Children[i], wrap ? i / 2 : 0);
            }
        }
        return base.MeasureOverride(available);
    }
}
