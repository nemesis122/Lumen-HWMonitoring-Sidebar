using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed partial class LocalDesignTile
{
    private readonly List<(string Key, AreaChart Chart)> drivePlots = new();

    private double? DriveLevel(string key, List<CompleteReading> rows)
    {
        if (key.EndsWith(":temp"))
        {
            string id = key[6..key.LastIndexOf(':')];
            return Last.Snap.Disks.FirstOrDefault(d => d.Id == id)?.Temperature / 100;
        }
        return S.Shows("drives.bar") ? rows.FirstOrDefault(r => r.Key == key)?.Fraction : null;
    }

    private void UpdateDrivePlots(List<CompleteReading> rows)
    {
        foreach (var plot in drivePlots)
        {
            int split = plot.Key.LastIndexOf(':');
            var series = Last.History.Read("disk." + plot.Key[6..split] + "." + plot.Key[(split + 1)..]);
            bool visible = rows.Any(r => r.Key == plot.Key) && (plot.Key.EndsWith(":read") || plot.Key.EndsWith(":write")) && series.Any(double.IsFinite);
            plot.Chart.IsVisible = visible;
            plot.Chart.Max = Math.Max(1, series.Where(double.IsFinite).DefaultIfEmpty(0).Max() * 1.12);
            plot.Chart.Set(visible ? series : Array.Empty<double>(), plot.Key.EndsWith(":write") ? P.Calm : Accent);
        }
    }

    private Control DriveTrace(CompleteReading row)
    {
        if (!row.Key.EndsWith(":read") && !row.Key.EndsWith(":write")) return new Border();
        var chart = new AreaChart { Height = 10, FillOpacity = .06, LineWidth = 1.2, Tag = "fixed" };
        ToolTip.SetTip(chart, row.Label);
        drivePlots.Add((row.Key, chart));
        return chart;
    }

    private Control DriveTraces(CompleteReading[] rows) => new StatPanel(Array.Empty<double>(), rows.Select(DriveTrace).ToArray())
        { MaxColumns = 2, MinimumColumnWidth = 40, Balanced = true };

    private Control DriveField(CompleteReading row)
    {
        bool orbit = (style is WidgetStyle.Orbit or WidgetStyle.Aperture) && row.Key.EndsWith(":used");
        var number = Value(row, 20, !orbit);
        var unit = Text("", 11, P.Soft);
        unit.VerticalAlignment = VerticalAlignment.Bottom;
        unit.Margin = new Thickness(0, 0, 0, 2);
        units.Add(row.Key, unit);
        var label = Text(row.Label.Split(" · ")[0] + " · " + (row.Key.EndsWith(":temp") ? L.T("Temp") : L.T("Used")), 10.5, P.Soft);
        var readout = Ui.Row(2, number, unit);
        var content = Ui.Stack(0, label, readout);
        if (style is WidgetStyle.Vector or WidgetStyle.Lucent or WidgetStyle.Circuit) number.Foreground = Palette.Brush(Accent);
        if (orbit)
        {
            var dial = new CompactOrbit { Width = 42, Height = 42, Tag = "graphic", HorizontalAlignment = HorizontalAlignment.Center };
            dials.Add((row.Key, dial));
            content.HorizontalAlignment = HorizontalAlignment.Center;
            content.VerticalAlignment = VerticalAlignment.Center;
            label.HorizontalAlignment = readout.HorizontalAlignment = HorizontalAlignment.Center;
            label.TextAlignment = number.TextAlignment = TextAlignment.Center;
            readout.Orientation = Orientation.Vertical;
            readout.Spacing = 0;
            number.HorizontalAlignment = unit.HorizontalAlignment = HorizontalAlignment.Center;
            unit.FontSize = 10;
            unit.Margin = new Thickness(0);
            content.Children.Remove(readout);
            readout.VerticalAlignment = VerticalAlignment.Center;
            return Ui.Stack(0, content, new Grid { Children = { dial, readout } });
        }
        if (style == WidgetStyle.PixelDeck && row.Key.EndsWith(":used"))
        {
            var grid = new PixelPercent { Width = 80, Height = 12, Rows = 5, Tag = "graphic" };
            pixels.Add((row.Key, grid)); content.Children.Add(grid);
        }
        return content;
    }

    private Control DriveBand(CompleteReading row)
    {
        var back = new FillBack { Track = P.Track, Radius = 3 };
        ribbons.Add((row.Key, back));
        var line = Ui.Columns("*,Auto", Text(row.Label.Split(" · ")[0] + " · " + (row.Key.EndsWith(":temp") ? L.T("Temp") : L.T("Used")), 10.5, P.Soft), Value(row, 20));
        return new Panel { MinHeight = 26, Children = { back, new Border { Padding = new Thickness(5, 0), Child = line } } };
    }

    private Control DriveIo(CompleteReading[] rows) => new StatPanel(Array.Empty<double>(), rows.Select(r =>
        {
            var field = TinyRow(r, style == WidgetStyle.Terminal);
            field.Margin = new Thickness(0, 0, 8, 0);
            return field;
        }).ToArray())
        { MaxColumns = 2, MinimumColumnWidth = 90, Balanced = true };

    private void BuildStyledDrives(List<CompleteReading> rows)
    {
        drivePlots.Clear(); scales.Clear(); ribbons.Clear(); pixels.Clear(); radial.Clear(); horizon = null;
        var temperatures = rows.Where(r => r.Key.EndsWith(":temp")).Select(row =>
        {
            if (style == WidgetStyle.RibbonFlow) return DriveBand(row);
            Control field = DriveField(row);
            if (style == WidgetStyle.Thermal)
            {
                var scale = new LevelBar { Width = 6, Height = 36, Margin = new Thickness(0, 0, 6, 0), Tag = "graphic" };
                scales.Add((row.Key, scale)); field = Ui.Columns("Auto,*", scale, field);
            }
            return style is WidgetStyle.Strata or WidgetStyle.Lucent or WidgetStyle.MosaicPro or WidgetStyle.SplitLens ? Surface(field, 1) : field;
        }).ToArray();
        if (temperatures.Length > 0) body.Children.Add(style == WidgetStyle.SplitLens ? new EqualPairPanel(temperatures, 90)
            : new StatPanel(Array.Empty<double>(), temperatures) { MaxColumns = 2, MinimumColumnWidth = 90, Balanced = true });
        if (style == WidgetStyle.Meridian)
        {
            foreach (var suffix in new[] { ":used" })
            {
                var metrics = rows.Where(r => r.Key.EndsWith(suffix)).Select(r =>
                    suffix == ":used" && S.Shows("drives.bar") ? Ui.Stack(1, DriveField(r), Level(r, 2)) : DriveField(r)).ToArray();
                if (metrics.Length > 0) body.Children.Add(new StatPanel(Array.Empty<double>(), metrics) { MaxColumns = 2, MinimumColumnWidth = 90, Balanced = true });
            }
        }
        foreach (var group in rows.Where(r => r.Key.StartsWith("drive:")).GroupBy(r => r.Key[..r.Key.LastIndexOf(':')]))
        {
            var all = group.ToArray();
            var model = all.FirstOrDefault(r => r.Key.EndsWith(":model"));
            var temp = all.FirstOrDefault(r => r.Key.EndsWith(":temp"));
            var used = all.FirstOrDefault(r => r.Key.EndsWith(":used"));
            var lead = all.Where(r => style != WidgetStyle.Meridian && r == used).ToArray();
            var io = all.Where(r => r != model && r != temp && r != used).Select(r => r with { Label = r.Label.Split(" · ").Last() }).ToArray();
            if (style == WidgetStyle.Meridian && io.Length == 0) continue;
            if (lead.Length == 0 && io.Length == 0) continue;
            var block = Ui.Stack(2);
            bool barAdded = style == WidgetStyle.Meridian;
            if (style == WidgetStyle.Meridian && io.Length == 0)
                block.Children.Add(new StatPanel(Array.Empty<double>(), lead.Select(DriveField).ToArray()) { MaxColumns = 2, MinimumColumnWidth = 90, Balanced = true });
            else if (style == WidgetStyle.Meridian)
            {
                var left = Ui.Stack(0, lead.Select(DriveField).ToArray());
                var right = Ui.Stack(0, io.Select(r => TinyRow(r)).ToArray());
                if (io.Length > 0) right.Children.Add(DriveTraces(io));
                if (used != null && S.Shows("drives.bar")) { right.Children.Add(Level(used, 2)); barAdded = true; }
                block.Children.Add(new StatPanel(new[] { 1d, 1.35 }, new Control[] { left, right }) { MaxColumns = 2, MinimumColumnWidth = 90 });
            }
            else if (style == WidgetStyle.SplitLens && lead.Length > 0)
            {
                var sides = new List<Control>();
                for (int i = 0; i < lead.Length; i++)
                {
                    var column = Ui.Stack(1, DriveField(lead[i]));
                    foreach (var extra in io.Where((r, n) => n % lead.Length == i)) { column.Children.Add(DriveTrace(extra)); column.Children.Add(TinyRow(extra)); }
                    sides.Add(Surface(column, 2));
                }
                block.Children.Add(new EqualPairPanel(sides.ToArray(), 105));
            }
            else if (style == WidgetStyle.RibbonFlow)
            {
                block.Children.Add(Ui.Stack(1, lead.Select(DriveBand).ToArray()));
                if (io.Length > 0) block.Children.Add(DriveIo(io));
                barAdded = true;
            }
            else if (style == WidgetStyle.Terminal)
            {
                foreach (var row in lead)
                {
                    var value = Value(row, 20);
                    value.TextAlignment = TextAlignment.Right;
                    if (Ui.FontKey == "inter") value.FontFamily = Ui.FamilyFor("mono");
                    block.Children.Add(Ui.Columns("*,Auto", Text(row.Label.Split(" · ")[0] + " · " + (row.Key.EndsWith(":temp") ? L.T("Temp") : L.T("Used")), 11, P.Soft), value));
                }
                if (io.Length > 0) block.Children.Add(DriveIo(io));
            }
            else
            {
                var fields = lead.Select(row =>
                {
                    Control field = DriveField(row);
                    if (style == WidgetStyle.Thermal)
                    {
                        var scale = new LevelBar { Width = 6, Height = 36, Margin = new Thickness(0, 0, 6, 0), Tag = "graphic" };
                        scales.Add((row.Key, scale)); field = Ui.Columns("Auto,*", scale, field);
                    }
                    return style is WidgetStyle.Strata or WidgetStyle.Lucent or WidgetStyle.MosaicPro ? Surface(field, 1) : field;
                }).ToArray();
                if (fields.Length > 0) block.Children.Add(new StatPanel(Array.Empty<double>(), fields) { MaxColumns = 2, MinimumColumnWidth = 90, Balanced = true });
                if (io.Length > 0) block.Children.Add(DriveIo(io));
                if ((style is WidgetStyle.Panorama or WidgetStyle.Circuit or WidgetStyle.HorizonSync) && io.Length > 0) block.Children.Add(DriveTraces(io));
            }
            if (!barAdded && used != null && S.Shows("drives.bar")) block.Children.Add(Level(used, 2));
            body.Children.Add(new Border { Padding = new Thickness(0, 1), Child = block });
        }
    }
}
