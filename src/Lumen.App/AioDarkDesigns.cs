using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Lumen.Core;
using Layout = Lumen.Core.DarkAioDesigns.Arrangement;
using Variant = Lumen.Core.DarkAioDesigns.Variant;

namespace Lumen.App;

public sealed partial class AioTile
{
    private static readonly Dictionary<string, Bitmap> darkArtwork = new();
    private WidgetSlot? DriveOwner => source.Widgets.FirstOrDefault(w => w.Kind == WidgetKind.Drives);
    private static bool DiskReading(AioReading row) => row.Group == "Drives" || row.Key.StartsWith("aio.system.disk.");

    private Color DarkAccent(DarkAioDesigns.Look look, WidgetKind kind)
    {
        var owner = source.Widgets.FirstOrDefault(w => w.Kind == kind);
        var hue = Color.TryParse(owner?.Accent, out var local) ? local
            : Color.TryParse(slot?.Accent, out var custom) ? custom : skin ?? C(look.Accent);
        return Accents.Readable(hue, palette);
    }

    private Color DarkSurface(double shade = .045) =>
        SeeThrough(Palette.Mix(Color.FromRgb(back.R, back.G, back.B), ink, shade));

    private Border DarkCard(Control body, string id, double pad = 11) => new()
    {
        Child = body, Padding = new Thickness(pad), Background = Palette.Brush(DarkSurface()),
        CornerRadius = new CornerRadius(11), ClipToBounds = true, Tag = "dashboard-part:" + id
    };

    private Control DarkPicture(string asset, double minHeight = 0, double horizontalFocus = .5, double verticalFocus = .5)
    {
        if (!darkArtwork.TryGetValue(asset, out var bitmap))
        {
            using var stream = AssetLoader.Open(new Uri("avares://Lumen/Assets/Aio/Dark/" + asset + ".png"));
            darkArtwork[asset] = bitmap = new Bitmap(stream);
        }
        return new DarkArtCrop(bitmap, DarkSurface(.025), settings.ActiveWidget?.Graphics ?? 1, horizontalFocus, verticalFocus)
        {
            Name = "dark-art-" + asset, Tag = "graphic", MinHeight = minHeight
        };
    }

    private TextBlock DarkCaption(string label, Color color, double size, bool title = false)
    {
        var text = DashboardText(label, size, title);
        text.Foreground = Palette.Brush(color);
        return text;
    }

    private TextBlock DarkValue(AioReading row, double size) => V(row, size, ink);

    private Control DarkLine(AioReading row, Color accent)
    {
        var label = TextColours.Bind(DarkCaption(EditionLabel(row), muted, 10.5), row.Key);
        var grid = Ui.Columns("1.1*,1*", label, FitRight(DarkValue(row, 12)));
        grid.ColumnSpacing = 5;
        return grid;
    }

    private Control DarkMeter(AioReading row, Color accent)
    {
        var meter = TextColours.Bind(new AioMeter(accent, track, 0) { Height = 4, Tag = "graphic" }, row.Key);
        meters.Add((row.Key, meter));
        return meter;
    }

    private Control DarkReadout(DashboardPart part, Color accent, bool hero, double minimum = 0)
    {
        var values = Stack(4);
        if (part.Title.Length > 0) values.Children.Add(DarkCaption(part.Title, accent, 13, true));
        if (part.Model.Length > 0)
        {
            var model = DarkCaption(part.Model, muted, 9.5); model.TextWrapping = TextWrapping.Wrap; model.MaxLines = 2; model.TextTrimming = TextTrimming.CharacterEllipsis;
            ToolTip.SetTip(model, part.Model);
            values.Children.Add(model);
        }
        var lead = hero ? part.Rows.FirstOrDefault(r => r.Key.EndsWith(".temp") && r.Group != "Cores")
            ?? part.Rows.FirstOrDefault(r => r.Key.EndsWith(".load") || r.Key == "aio.system.ram")
            ?? part.Rows.FirstOrDefault() : null;
        var load = lead != null ? part.Rows.FirstOrDefault(r => r != lead && r.Key.EndsWith(".load")) : null;
        var display = DarkAioDesigns.For(style).Display;
        if (hero && display == Variant.Gallery && lead != null)
        {
            var number = DarkValue(lead, 23);
            number.TextAlignment = TextAlignment.Right;
            var figures = Stack(3, FitRight(number));
            if (load != null)
            {
                var label = TextColours.Bind(DarkCaption(EditionLabel(load), accent, 9), load.Key);
                var value = DarkValue(load, 11);
                value.Foreground = Palette.Brush(accent);
                var pair = Ui.Row(4, label, value);
                pair.HorizontalAlignment = HorizontalAlignment.Right;
                figures.Children.Add(new Viewbox { Child = pair, StretchDirection = StretchDirection.DownOnly,
                    HorizontalAlignment = HorizontalAlignment.Right });
            }
            else
            {
                var caption = lead.Key == "aio.system.ram" ? L.T("Used") : EditionLabel(lead);
                var title = TextColours.Bind(DarkCaption(caption, muted, 9), lead.Key);
                title.HorizontalAlignment = HorizontalAlignment.Right;
                figures.Children.Add(title);
            }
            foreach (var row in part.Rows.Where(r => r != lead && r != load)) values.Children.Add(DarkLine(row, accent));
            return new Border { Child = DarkPair(values, figures, "1.5*,1*", 8), Padding = new Thickness(8), MinHeight = minimum };
        }
        if (hero && display == Variant.Original)
        {
            if (lead != null)
            {
                var primary = FitLeft(DarkValue(lead, 23));
                values.Children.Add(load == null ? primary : Ui.Columns("1.2*,1*", primary,
                    Ui.Columns("*,*", TextColours.Bind(DarkCaption(EditionLabel(load), muted, 9), load.Key), FitRight(DarkValue(load, 12)))));
            }
            foreach (var row in part.Rows.Where(r => r != lead && r != load))
                values.Children.Add(DarkLine(row, accent));
        }
        else if (lead != null)
        {
            var primary = new Viewbox
            {
                Child = DarkValue(lead, 23), MaxWidth = 70,
                StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = HorizontalAlignment.Left
            };
            Control? adjacent = null;
            if (load != null)
            {
                var label = TextColours.Bind(DarkCaption(EditionLabel(load), accent, 10), load.Key);
                var number = DarkValue(load, 12);
                number.Foreground = Palette.Brush(accent);
                adjacent = new Border
                {
                    Child = Ui.Row(4, label, number), Padding = new Thickness(6, 3), CornerRadius = new CornerRadius(5),
                    Background = Palette.Brush(SeeThrough(Palette.Mix(Color.FromRgb(back.R, back.G, back.B), accent, .16)))
                };
            }
            else if (lead.Key.EndsWith(".load") || lead.Key == "aio.system.ram")
            {
                primary.MaxWidth = 57;
                var caption = lead.Key == "aio.system.ram" ? L.T("Used") : EditionLabel(lead);
                adjacent = TextColours.Bind(DarkCaption(caption, muted, 10), lead.Key);
            }
            if (adjacent == null) values.Children.Add(primary);
            else
            {
                var next = new Viewbox { Child = adjacent, StretchDirection = StretchDirection.DownOnly,
                    HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
                var pair = Ui.Columns("Auto,*", primary, next);
                pair.ColumnSpacing = 5;
                values.Children.Add(pair);
            }
            var detail = new WrapPanel();
            foreach (var row in part.Rows.Where(r => r != lead && r != load))
            {
                var label = TextColours.Bind(DarkCaption(EditionLabel(row), muted, 9), row.Key);
                var reading = DarkValue(row, 11);
                var item = Ui.Row(4, label, reading);
                item.Margin = new Thickness(0, 2, 9, 2);
                detail.Children.Add(item);
            }
            if (detail.Children.Count > 0) values.Children.Add(detail);
        }
        else
        {
            foreach (var row in part.Rows) values.Children.Add(DarkLine(row, accent));
        }
        return new Border { Child = values, Padding = new Thickness(10, 8), MinHeight = minimum };
    }

    private List<DashboardPart> DarkParts(List<AioReading> rows)
    {
        return DashboardParts(rows).Where(p => p.Id != "ssd").SelectMany(part =>
        {
            if (part.Id != "gpu") return new[] { part };
            var owner = source.Widgets.FirstOrDefault(w => w.Kind == WidgetKind.Gpu);
            return part.Rows.GroupBy(r => r.Device).Select((device, index) => part with
            {
                Title = owner?.ShowTitle == false ? "" : Naming.Title(owner, index, index == 0 ? "GPU" : "GPU " + (index + 1)),
                Model = PartModel(WidgetKind.Gpu, index, snapshot.Gpus.FirstOrDefault(g => g.Id == device.Key)?.Name ?? ""),
                Rows = device.ToList()
            }).ToArray();
        }).ToList();
    }

    private Control DarkPair(Control left, Control right, string columns, double gap)
    {
        var grid = Ui.Columns(columns, left, right);
        grid.ColumnSpacing = gap;
        left.VerticalAlignment = right.VerticalAlignment = VerticalAlignment.Stretch;
        return grid;
    }

    private Control DarkHardware(DashboardPart[] parts, DarkAioDesigns.Look look)
    {
        var ordered = look.Layout == Layout.Rail ? parts.OrderBy(p => p.Id == "gpu" ? 0 : p.Id == "cpu" ? 1 : 2)
            : parts.OrderBy(p => p.Id == "cpu" ? 0 : p.Id == "gpu" ? 1 : 2);
        Control Read(DashboardPart part, double height) => DarkReadout(part, DarkAccent(look, EditionKind(part.Id)), true, height);
        if (look.Display == Variant.Gallery)
        {
            var gallery = Stack(6);
            var images = ordered.Select((part, index) =>
            {
                var focus = (index % 3) * .40 + .10;
                var picture = DarkPicture(look.Scene ?? "titanium-" + part.Id, 0, .5, look.Scene == null ? .5 : focus);
                picture.Height = 64;
                return (Control)new Border { Child = picture, CornerRadius = new CornerRadius(5),
                    ClipToBounds = true, Margin = new Thickness(2) };
            }).ToArray();
            gallery.Children.Add(new StatPanel(Array.Empty<double>(), images)
            {
                MaxColumns = 3, MinimumColumnWidth = 66, Balanced = true
            });
            foreach (var part in ordered)
            {
                gallery.Children.Add(new Border { Child = Read(part, 56), CornerRadius = new CornerRadius(7),
                    Background = Palette.Brush(DarkSurface(.022)), Tag = "dashboard-part:" + part.Id });
            }
            return DarkCard(gallery, "hardware-gallery", 7);
        }

        if (look.Layout == Layout.Rack && look.Display == Variant.Continuous)
        {
            var rows = new DashboardVertical(0);
            foreach (var part in ordered)
                rows.Add(DarkPair(DarkPicture("titanium-" + part.Id), DarkJoinedRow(Read(part, 87), rows.Children.Count), "1.02*,1.28*", 1));
            return DarkCard(rows, "hardware-strip", 0);
        }

        if (look.Layout == Layout.Rack)
            return Stack(6, ordered.Select(part => DarkCard(DarkPair(DarkPicture("titanium-" + part.Id),
                Read(part, 87), ".95*,1.35*", 1), part.Id, 0)).ToArray());

        var lanes = ordered.ToList();
        Control left = DarkPicture(look.Scene!, 100, look.Layout == Layout.Airflow ? .54 : .5);
        var metrics = new DashboardVertical(0);
        foreach (var part in lanes)
        {
            var row = Read(part, 84);
            metrics.Add(look.Display == Variant.Continuous ? DarkJoinedRow(row, metrics.Children.Count) : row);
        }
        var pair = DarkPair(left, metrics, look.Display == Variant.Continuous ? "1*,1.1*" : look.Layout == Layout.Rail ? ".88*,1.12*" : ".96*,1.04*", 1);
        return DarkCard(pair, "scene-panel", 0);
    }

    private Control DarkJoinedRow(Control content, int index) => new Border
    {
        Child = content,
        Background = Palette.Brush(DarkSurface(index % 2 == 0 ? .046 : .026)),
        BorderBrush = Palette.Brush(DarkSurface(.11)),
        BorderThickness = index == 0 ? new Thickness(0) : new Thickness(0, 1, 0, 0)
    };

    private void BuildDarkDesign(List<AioReading> rows)
    {
        dashboardLimits.Clear(); dashboardTraffic = null; coreBars = null; mainboardCores = null;
        DashboardHeightGrowth = 0; dashboardLayout = null;
        var look = DarkAioDesigns.For(style);
        Padding = new Thickness(8); content.Spacing = 8; CornerRadius = new CornerRadius(13);
        BorderThickness = new Thickness(0);
        if (slot?.ShowTitle != false && !string.IsNullOrWhiteSpace(slot?.Name))
            content.Children.Add(DarkCaption(slot.Name, ink, 16, true));
        if (settings.ShowNames && slot is { ShowHardware: true, HardwareName.Length: > 0 })
            content.Children.Add(DarkCaption(slot.HardwareName, muted, 11));
        var parts = DarkParts(rows);
        var hardware = parts.Where(p => p.Id is "cpu" or "gpu" or "ram").ToArray();
        if (hardware.Length > 0) content.Children.Add(DarkHardware(hardware, look));
        if (hardware.Any(p => p.Id == "cpu") && lastCoreLoads.Count >= 2 && AioReadings.Selected(settings, "aio.cpu.coreloads"))
        {
            var accent = DarkAccent(look, WidgetKind.Cpu);
            coreBars = new AioCoreBars(ink, muted, DarkSurface(.03)) { Loads = lastCoreLoads, Tint = accent, Heatmap = true,
                NumbersOnly = true, Compact = true, Tag = "graphic" };
            content.Children.Add(DarkCard(Stack(5, DarkCaption(L.T("Cores"), ink, 13, true),
                TextColours.Bind(coreBars, "aio.cpu.coreloads")), "cpu"));
        }
        var disks = rows.Where(DiskReading).ToArray();
        if (disks.Length > 0) content.Children.Add(DarkDrives(disks, DarkAccent(look, WidgetKind.Drives)));
        foreach (var part in parts.Where(p => p.Id is not ("cpu" or "gpu" or "ram" or "power" or "net")))
            content.Children.Add(DarkCard(DarkReadout(part, DarkAccent(look, EditionKind(part.Id)), false), part.Id, 0));
        Control? Foot(string id) => parts.FirstOrDefault(p => p.Id == id) is { } part
            ? DarkCard(DarkReadout(part, DarkAccent(look, EditionKind(part.Id)), false, 92), part.Id, 0) : null;
        var power = Foot("power"); var network = Foot("net");
        if (power != null && network != null) content.Children.Add(DarkPair(power, network, "*,*", 7));
        if (power == null && network != null) content.Children.Add(network);
        if (network == null && power != null) content.Children.Add(power);
        if (rows.Count == 0) content.Children.Add(DarkCaption(L.T("No data"), muted, 12));

        if (!settings.Horizontal)
        {
            var elements = content.Children.ToArray(); content.Children.Clear();
            var flow = new DashboardVertical(8);
            for (int i = 0; i < elements.Length; i++) flow.Add(elements[i], i == elements.Length - 1 && elements[i] is not TextBlock);
            dashboardLayout = flow; content.Children.Add(flow);
        }
    }

    private Control DarkDrives(IEnumerable<AioReading> readings, Color accent)
    {
        var models = snapshot.Disks.ToList();
        var drives = readings.GroupBy(row => row.Device).Select(group =>
        {
            int index = Math.Max(0, models.FindIndex(d => d.Id == group.Key));
            string name = DriveOwner?.ShowTitle == false ? "" : Naming.Title(DriveOwner, index, AioReadings.DriveName(snapshot, group.Key));
            string model = PartModel(WidgetKind.Drives, index, models.FirstOrDefault(d => d.Id == group.Key)?.Name ?? "");
            var rows = group.ToArray();
            AioReading? Find(string suffix) => rows.FirstOrDefault(row => row.Key.EndsWith(suffix, StringComparison.Ordinal));
            var used = Find(".used");
            var temp = rows.FirstOrDefault(row => row.Key.StartsWith("aio.system.disk."));
            var read = Find(".read");
            var write = Find(".write");
            var head = Stack(2);
            if (name.Length > 0) head.Children.Add(DarkCaption(name, ink, 14, true));
            if (model.Length > 0)
            {
                var hardware = DarkCaption(model, muted, 10);
                ToolTip.SetTip(hardware, model);
                head.Children.Add(hardware);
            }
            var body = Stack(4);
            if (temp != null) body.Children.Add(Ui.Columns("2*,*", head, FitRight(DarkValue(temp, 12))));
            else if (head.Children.Count > 0) body.Children.Add(head);
            if (used != null)
            {
                body.Children.Add(DarkLine(used, accent));
                body.Children.Add(DarkMeter(used, accent));
            }
            var rates = new[] { read, write }.OfType<AioReading>().Select(row => Stack(2,
                TextColours.Bind(DarkCaption((row == read ? "↓ " : "↑ ") + EditionLabel(row), muted, 10), row.Key),
                FitLeft(DarkValue(row, 12)))).ToArray();
            if (rates.Length > 0)
                body.Children.Add(new StatPanel(Array.Empty<double>(), rates) { MaxColumns = 2, MinimumColumnWidth = 58, Balanced = true });
            foreach (var extra in rows.Where(r => r != used && r != temp && r != read && r != write))
                body.Children.Add(DarkLine(extra, accent));
            var picture = new Border
            {
                Child = Part("ssd", 50, 38), Margin = new Thickness(0, 0, 7, 0)
            };
            return DarkCard(Ui.Columns("57,*", picture, body), "ssd");
        }).ToArray();
        return new Border { Child = Stack(5, drives), Tag = "dashboard-part:ssd" };
    }

}

internal sealed class DarkArtCrop(Bitmap image, Color wash, double scale, double horizontalFocus, double verticalFocus) : Control
{
    public override void Render(DrawingContext context)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0) return;
        double zoom = Math.Max(Bounds.Width / image.Size.Width, Bounds.Height / image.Size.Height) * Math.Max(1, scale);
        double w = Bounds.Width / zoom, h = Bounds.Height / zoom;
        var crop = new Rect((image.Size.Width - w) * horizontalFocus, (image.Size.Height - h) * verticalFocus, w, h);
        var area = new Rect(Bounds.Size);
        context.DrawImage(image, crop, area);
        if (wash.A < 255) context.DrawRectangle(Palette.Brush(Color.FromArgb((byte)((255 - wash.A) / 3), wash.R, wash.G, wash.B)), null, area);
    }
}

