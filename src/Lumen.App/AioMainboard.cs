using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed partial class AioTile
{
    private MainboardCoreGrid? mainboardCores;

    // Each value goes through V(): sensor selection, live updates, tooltips and reserved
    // number widths work exactly like the other AIO designs. Nothing is baked into an image.
    private void Mainboard()
    {
        drivesPlaced = true;
        coreBars = null;
        mainboardCores = null;
        CornerRadius = new CornerRadius(9);
        Padding = new Thickness(8);
        BorderBrush = Palette.Brush(Palette.Mix(track, Cyan, .55));
        var left = Stack(14);
        var right = Stack(14);
        var anchors = new List<Control>();
        bool wideCores=style==WidgetStyle.AioMainboardCores;
        var processors=new List<Control>();var utilities=new List<Control>();var drives=new List<Control>();
        Control? coreRow=null;Control? temperatureRow=null;

        Control Module(string title, string art, IEnumerable<Control> rows, string model = "")
        {
            var body = Stack(5);
            var heading = Ui.Columns("Auto,*", Part(art, 45, 36), FitLeft(Text(title, 15, ink)));
            heading.ColumnSpacing = 5;
            body.Children.Add(heading);
            if (model.Length > 0)
            {
                var name = Text(model, 10, muted);
                name.TextWrapping = TextWrapping.NoWrap;
                name.TextTrimming = TextTrimming.CharacterEllipsis;
                ToolTip.SetTip(name, model);
                body.Children.Add(name);
            }
            foreach (var row in rows) body.Children.Add(row);
            var frame = new Border
            {
                Background = Palette.Brush(SeeThrough(Palette.Mix(back, Cyan, .035))),
                BorderBrush = Palette.Brush(Palette.Mix(track, Cyan, .62)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8), Child = body, Tag = "aio-section"
            };
            anchors.Add(frame);
            return frame;
        }
        Control Row(AioReading r, string? caption = null)
        {
            string label = caption ?? (r.Key.EndsWith(".junction") ? "VRAM °C" :
                r.Key.EndsWith(".voltage") ? (r.Group == "CPU" ? "Vcore" : "Volt") :
                r.Key.EndsWith(".power") ? "Power" : RefLabel(r));
            var grid = Ui.Columns(".9*,1.3*", FitLeft(Text(label, 10.5, muted)), FitRight(V(r, 12, ink)));
            grid.ColumnSpacing = 4;
            return grid;
        }
        foreach (var cpu in Rs("CPU").GroupBy(r => r.Device))
        {
            var owned = source.Widgets.FirstOrDefault(w => w.Kind == WidgetKind.Cpu);
            List<Control> rows;
            if(wideCores&&cpu.FirstOrDefault(r=>r.Key.EndsWith(".temp")) is {} temp)
                rows=new List<Control>{FitLeft(V(temp,42,Cyan))}.Concat(cpu.Where(r=>r!=temp).Select(r=>Row(r))).ToList();
            else rows=cpu.Select(r=>Row(r)).ToList();
            var module=Module(Naming.Title(owned, 0, "CPU"), "cpu", rows,
                PartModel(WidgetKind.Cpu, 0, snapshot.Cpu?.Name ?? ""));
            if(wideCores)processors.Add(module);else left.Children.Add(module);
        }
        if (lastCoreLoads.Count >= 2 && AioReadings.Selected(settings, "aio.cpu.coreloads") && Rs("CPU").Length > 0)
        {
            mainboardCores = new MainboardCoreGrid(ink, muted, track, Cyan) { Loads = lastCoreLoads,PreferSingleRow=wideCores };
            var module=Module("CORES", "cpu", new Control[] { Text("Load %", 10, muted), mainboardCores });
            if(wideCores)coreRow=module;else right.Children.Add(module);
        }
        if (R("ram", "System") is { } ram)
        {
            var load = V(ram, 27, Cyan);
            formats[load] = row => Format.Percent(row.Number);
            load.Text = formats[load](ram);
            var capacity = Text(Amount(ram), 12, ink, true);
            fields[ram.Key].Add(capacity);
            formats[capacity] = Amount;
            capacity.Text = Amount(ram);
            var module=Module("RAM", "ram", new[] { FitLeft(load), FitLeft(capacity), PartMeter(ram, Cyan) });
            if(wideCores)utilities.Add(module);else right.Children.Add(module);
        }
        int gpuIndex = 0;
        var gpuIds = Rs("GPU").Select(r => r.Device).Distinct().ToArray();
        foreach (var gpu in Rs("GPU").GroupBy(r => r.Device))
        {
            var owned = source.Widgets.FirstOrDefault(w => w.Kind == WidgetKind.Gpu);
            string fallback = gpuIds.Length > 1 ? "GPU " + (gpuIndex + 1) : "GPU";
            var module=Module(Naming.Title(owned, gpuIndex, fallback), "gpu", gpu.Select(r => Row(r)),
                PartModel(WidgetKind.Gpu, gpuIndex, snapshot.Gpus.FirstOrDefault(g => g.Id == gpu.Key)?.Name ?? ""));
            if(wideCores)processors.Add(module);else left.Children.Add(module);
            gpuIndex++;
        }
        foreach (var drive in PartDrives())
        {
            var rows = new List<Control>();
            if (drive.Temp != null) rows.Add(Row(drive.Temp, "Temp"));
            rows.AddRange(drive.Rows.Select(r => Row(r, r.Key.EndsWith(".read") ? "R" : r.Key.EndsWith(".write") ? "W" : "Used")));
            var module=Module(AioReadings.DriveName(snapshot, drive.Id), "ssd", rows);
            if(wideCores)drives.Add(module);else right.Children.Add(module);
        }
        if (Rs("Rails") is { Length: > 0 } rails)
        {
            var module=Module("PSU", "psu", rails.Select(r => Row(r)));
            if(wideCores)utilities.Add(module);else left.Children.Add(module);
        }
        if (Rs("Cores") is { Length: > 0 } coreTemps)
        {
            var module=Module("CORE TEMP", "cpu", coreTemps.Select(r => Row(r)));
            if(wideCores)temperatureRow=module;else left.Children.Add(module);
        }

        var columns = Ui.Columns("*,20,*", left, new Border(), right);
        var sections = Stack(18);
        if(wideCores)
        {
            if(processors.Count>0)sections.Children.Add(new MainboardDriveGrid(processors));
            if(coreRow!=null)sections.Children.Add(coreRow);
            if(utilities.Count>0)sections.Children.Add(new MainboardDriveGrid(utilities));
            if(drives.Count>0)sections.Children.Add(new MainboardDriveGrid(drives));
            if(temperatureRow!=null)sections.Children.Add(temperatureRow);
        }
        else if (left.Children.Count > 0 || right.Children.Count > 0) sections.Children.Add(columns);
        if (Rs("Cooling") is { Length: > 0 } fans)
        {
            var cells = fans.Select((r, i) =>
            {
                var value = V(r, 15, ink);
                formats[value] = row => row.Number.HasValue ? Format.Number(row.Number) : "—";
                value.Text = formats[value](r);
                var name = Text(r.Label, 10, muted);
                name.TextAlignment = TextAlignment.Center;
                name.MaxLines = 2;
                var cell = Stack(3, Part(FanArt[i % FanArt.Length], 44, 35), name, Fit(value), Text("rpm", 9, muted));
                foreach (var text in cell.Children.OfType<TextBlock>()) text.HorizontalAlignment = HorizontalAlignment.Center;
                return (Control)cell;
            }).ToArray();
            sections.Children.Add(Module("FANS", "fan0", new[] { FixedCols(Math.Min(4, fans.Length), cells) }));
        }
        if (Rs("Network") is { Length: > 0 } network)
        {
            var cells = network.Select(r => (Control)new Border
            {
                Tag="mainboard-network-cell",Padding=new Thickness(4,0),ClipToBounds=true,
                Child=Stack(3, FitLeft(Text(r.Key.EndsWith(".down") ? "↓ DL" : r.Key.EndsWith(".up") ? "↑ UP" : "Link", 10, muted)),
                    new Viewbox{Height=21,Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,
                        HorizontalAlignment=HorizontalAlignment.Left,Child=V(r,15,Cyan)})
            }).ToArray();
            sections.Children.Add(Module("NETWORK", "router", new[] { FixedCols(cells.Length, cells) }));
        }
        var board = new Grid { Children = { new MainboardTraces(anchors, Cyan, lightSurface,wideCores?1:3), sections }, Tag = "aio-section" };
        content.Children.Add(board);
    }
}

/// <summary>Decorative PCB paths behind native controls. Connections follow actual module bounds.</summary>
public sealed class MainboardTraces : Control
{
    private readonly IReadOnlyList<Control> anchors;
    private readonly Color color;
    private readonly bool light;
    private readonly int sideTraces;
    public MainboardTraces(IReadOnlyList<Control> anchors, Color color, bool light,int sideTraces=3)
    { this.anchors = anchors; this.color = color; this.light = light;this.sideTraces=sideTraces; IsHitTestVisible = false; }
    public IReadOnlyList<(Point Start,Point End)> RailSegments()
    {
        // These fields may be translucent. Leaving their area out of the drawing,
        // rather than painting over the lines, keeps titles and numbers clear with glass too.
        var footers=anchors.Where(a=>a.IsVisible&&a.Bounds.Width>Bounds.Width*.7)
            .Select(a=>(Point:a.TranslatePoint(default,this),Height:a.Bounds.Height))
            .Where(a=>a.Point.HasValue).OrderBy(a=>a.Point!.Value.Y).ToArray();
        var gaps=new List<(double Top,double Bottom)>();double cursor=0;
        foreach(var footer in footers)
        {
            double top=Math.Max(0,footer.Point!.Value.Y-8);
            if(top>cursor)gaps.Add((cursor,top));
            cursor=Math.Max(cursor,footer.Point.Value.Y+footer.Height+3);
        }
        if(footers.Length==0)gaps.Add((0,Bounds.Height));
        var lines=new List<(Point Start,Point End)>();
        foreach(var gap in gaps)
        for(int i=-sideTraces;i<=sideTraces;i++)
        {
            double x=Bounds.Width/2+i*5;lines.Add((new Point(x,gap.Top),new Point(x,gap.Bottom)));
        }
        return lines;
    }
    public override void Render(DrawingContext context)
    {
        var pen = new Pen(Palette.Brush(Color.FromArgb(light ? (byte)135 : (byte)175, color.R, color.G, color.B)), 1.4);
        var faint = new Pen(Palette.Brush(Color.FromArgb(24, color.R, color.G, color.B)), 1);
        double mid = Bounds.Width / 2;
        foreach(var line in RailSegments())context.DrawLine(Math.Abs(line.Start.X-mid)<.1?pen:faint,line.Start,line.End);
        foreach (var anchor in anchors)
        {
            var point = anchor.TranslatePoint(default, this);
            if (point == null) continue;
            var p = point.Value;
            double y = p.Y + Math.Min(40, anchor.Bounds.Height / 2);
            if (anchor.Bounds.Width > Bounds.Width * .7)
            {
                context.DrawEllipse(Palette.Brush(color), null, new Point(mid, Math.Max(0, p.Y - 8)), 2.5, 2.5);
                continue;
            }
            double edge = p.X < mid ? p.X + anchor.Bounds.Width : p.X;
            context.DrawLine(pen, new Point(mid, y), new Point(edge, y));
            context.DrawEllipse(Palette.Brush(color), null, new Point(mid, y), 3, 3);
        }
    }
}

/// <summary>Live core-load columns; row count depends on width and core count, never on the readings.</summary>
public sealed class MainboardCoreGrid : Control
{
    public bool TwoTone { get; init; }
    public bool PreferSingleRow {get;init;}
    public int VisibleColumns=>Columns(Bounds.Width);
    private readonly Color ink, muted, track, accent;
    private IReadOnlyList<double> loads = Array.Empty<double>();
    public MainboardCoreGrid(Color ink, Color muted, Color track, Color accent)
    { this.ink = ink; this.muted = muted; this.track = track; this.accent = accent; }
    public IReadOnlyList<double> Loads
    {
        get => loads;
        set { bool changed = value.Count != loads.Count; loads = value.ToArray(); if (changed) InvalidateMeasure(); InvalidateVisual(); }
    }
    private int Columns(double width)
    {
        int maximum = Math.Max(1, Math.Min(PreferSingleRow||width>=600?16:8, (int)((width + 4) / 19)));
        int rows = Math.Max(1, (int)Math.Ceiling(loads.Count / (double)maximum));
        return Math.Max(1, (int)Math.Ceiling(loads.Count / (double)rows));
    }
    protected override Size MeasureOverride(Size available)
    {
        double width = double.IsFinite(available.Width) ? available.Width : 140;
        return new Size(width, Math.Ceiling(loads.Count / (double)Columns(width)) * RowHeight(width));
    }
    private double RowHeight(double width)=>(width / Columns(width)>30?(width<600?46:72):39)+33;
    public override void Render(DrawingContext c)
    {
        if (Bounds.Width < 1) return;
        int columns = Columns(Bounds.Width);
        double cell = (Bounds.Width - (columns - 1) * 4) / columns;
        double rowHeight=RowHeight(Bounds.Width),barHeight=rowHeight-33,barWidth=Math.Min(24,cell);
        var face = new Typeface(Ui.Font, FontStyle.Normal, FontWeight.SemiBold);
        void Center(string text, double x, double y, Color color, double size)
        {
            var t = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, size, Palette.Brush(color));
            c.DrawText(t, new Point(x + (cell - t.Width) / 2, y));
        }
        for (int i = 0; i < loads.Count; i++)
        {
            double x = (i % columns) * (cell + 4), y = (i / columns) * rowHeight;
            double barX=x+(cell-barWidth)/2;
            Center("C" + (i + 1), x, y, muted, cell>30?11:8);
            c.DrawRectangle(Palette.Brush(track), null, new Rect(barX, y + 13, barWidth, barHeight), 2, 2);
            double fraction = double.IsFinite(loads[i]) ? Math.Clamp(loads[i] / 100, 0, 1) : 0;
            double height = barHeight * fraction;
            var fill=TwoTone?(i/columns%2==0?MainboardMapColors.Cyan:MainboardMapColors.Violet):AioCoreBars.Heat(fraction);
            if(height > 0) c.DrawRectangle(Palette.Brush(fill), null, new Rect(barX, y + 13 + barHeight - height, barWidth, height), 1, 1);
            Center(double.IsFinite(loads[i]) ? Math.Round(loads[i]).ToString("0") : "—", x, y + 16 + barHeight, ink, cell>30?11:9);
        }
    }
}
