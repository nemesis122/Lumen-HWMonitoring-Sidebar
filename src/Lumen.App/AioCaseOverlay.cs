using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Lumen.Core;

namespace Lumen.App;

public sealed partial class AioTile
{
    private static readonly Dictionary<string, Bitmap> caseBackgrounds = new();
    private Bitmap CaseBackground(CaseDesigns.Look look)
    {
        string asset = look.Layout == 0 && source.Columns == 2 ? look.Asset + "-two-column" : look.Asset;
        if (!caseBackgrounds.TryGetValue(asset, out var bitmap))
            caseBackgrounds[asset] = bitmap = new Bitmap(AssetLoader.Open(new Uri("avares://Lumen/Assets/Aio/Cases/" + asset + ".png")));
        return bitmap;
    }

    private void BuildCaseOverlay(List<AioReading> rows)
    {
        dashboardLimits.Clear(); dashboardTraffic = null; coreBars = null; mainboardCores = null;
        DashboardHeightGrowth = 0;
        Padding = new Thickness(0); BorderThickness = new Thickness(0); CornerRadius = new CornerRadius(5); content.Spacing = 0;
        var look = CaseDesigns.For(style);
        bool refined = look.Layout == 0 && source.Columns <= 2;
        Color accent = Color.TryParse(slot?.Accent, out var chosen) ? chosen : skin ?? C(look.Accent);
        var scene = new CaseOverlayPanel(CaseBackground(look), look.Layout, Math.Clamp(source.Columns, 1, 6), accent, source.Width);
        var caption = Stack(2);
        if (slot?.ShowTitle != false && !string.IsNullOrWhiteSpace(slot?.Name)) caption.Children.Add(DashboardText(slot!.Name, 15, true));
        if (settings.ShowNames && slot is { ShowHardware: true, HardwareName.Length: > 0 }) caption.Children.Add(DashboardText(slot.HardwareName, 11));
        if (caption.Children.Count > 0) scene.SetCaption(caption);
        var parts = DashboardParts(rows);
        var cooling = parts.FirstOrDefault(p => p.Id == "fan");
        if (cooling != null)
        {
            parts.Remove(cooling);
            var boardRows = cooling.Rows.Where(r => r.Group == "Board").ToList();
            if (boardRows.Count > 0) parts.Add(new("board", L.T("Mainboard"), cooling.Model, boardRows));
            var fanRows = cooling.Rows.Where(r => r.Group != "Board").ToList();
            foreach (var device in snapshot.Gpus)
            {
                var own = fanRows.Where(r => r.Key.Contains(device.Id, StringComparison.OrdinalIgnoreCase) || r.Label.Contains(device.Name, StringComparison.OrdinalIgnoreCase)).ToList();
                int gpu = parts.FindIndex(p => p.Id == "gpu");
                if (gpu >= 0) { parts[gpu].Rows.AddRange(own.Select(r => r with { Device = device.Id })); fanRows.RemoveAll(r => own.Contains(r)); }
            }
            int cpu = parts.FindIndex(p => p.Id == "cpu");
            var cpuFans = fanRows.Where(r => r.Key.EndsWith(".cpu") || r.Label.Equals("CPU", StringComparison.OrdinalIgnoreCase)).ToList();
            if (cpu >= 0) { parts[cpu].Rows.AddRange(cpuFans); fanRows.RemoveAll(r => cpuFans.Contains(r)); }
            if (fanRows.Count > 0) parts.Add(cooling with { Title = L.T("Cooling"), Rows = fanRows });
        }
        // One model-specific card per GPU and drive, while keeping every selected sensor identity.
        var cards = new List<DashboardPart>();
        foreach (var part in parts)
        {
            if (part.Id is "gpu" or "ssd")
            {
                foreach (var group in part.Rows.GroupBy(r => r.Device))
                {
                    string model = part.Id == "gpu" ? snapshot.Gpus.FirstOrDefault(g => g.Id == group.Key)?.Name ?? ""
                        : Naming.Short(snapshot.Disks.FirstOrDefault(d => d.Id == group.Key)?.Name ?? "");
                    int index = part.Id == "gpu" ? snapshot.Gpus.ToList().FindIndex(g => g.Id == group.Key) : snapshot.Disks.ToList().FindIndex(d => d.Id == group.Key);
                    cards.Add(part with { Model = PartModel(part.Id == "gpu" ? WidgetKind.Gpu : WidgetKind.Drives, Math.Max(0,index), model), Rows = group.ToList() });
                }
            }
            else cards.Add(part);
        }
        foreach (var part in cards)
        {
            var body = Stack(refined || look.Layout == 8 ? 2 : 3);
            int ordinal = cards.Where(p => p.Id == part.Id).ToList().IndexOf(part) + 1;
            bool customDriveTitle = source.Widgets.Any(w => w.Kind == WidgetKind.Drives && !string.IsNullOrWhiteSpace(w.Name));
            string heading = refined && part.Id == "ssd" && !customDriveTitle ? "SSD " + ordinal : part.Title;
            if (refined && part.Id == "gpu" && cards.Count(p => p.Id == "gpu") > 1) heading += " " + ordinal;
            var title = DashboardText((look.Terminal ? "> " : "") + heading, look.LargeValues ? 14 : refined ? 13 : 12, true);
            title.Foreground = Palette.Brush(accent);
            ToolTip.SetTip(title, part.Model.Length > 0 ? heading + " · " + part.Model : heading);
            if (look.Terminal) title.FontFamily = Ui.DesignFont(Ui.FamilyFor("mono"));
            if (part.Title.Length > 0) body.Children.Add(title);
            WidgetKind ownerKind = part.Id switch { "cpu" => WidgetKind.Cpu, "gpu" => WidgetKind.Gpu, "ram" => WidgetKind.Memory, "ssd" => WidgetKind.Drives, "fan" or "board" => WidgetKind.Cooling, "net" => WidgetKind.Network, _ => WidgetKind.Power };
            bool customModel = source.Widgets.Any(w => w.Kind == ownerKind && w.HardwareName.Length > 0);
            if (settings.ShowNames && part.Model.Length > 0 && (!refined || customModel || part.Id == "gpu" && cards.Count(p => p.Id == "gpu") > 1))
            {
                var model = DashboardText(part.Model, 9); model.Foreground = Palette.Brush(C("#BFCBD6"));
                ToolTip.SetTip(model, part.Model); body.Children.Add(model);
            }
            bool first = true;
            foreach (var row in part.Rows.Where(r => r.Group != "Cores"))
            {
                string rowLabel = refined && row.Group == "Cooling" && part.Id is "cpu" or "gpu" ? L.T("Fan")
                    : refined && row.Key == "aio.system.ram" ? L.T("Load") : CaseSensorLabel(row);
                var label = DashboardText(rowLabel, look.Layout == 8 ? 9.5 : refined ? 10.5 : 10);
                label.Foreground = Palette.Brush(C("#D2D9E0"));
                if (look.Terminal) label.FontFamily = Ui.DesignFont(Ui.FamilyFor("mono"));
                ToolTip.SetTip(label, DashboardLabel(row));
                var value = V(row, first && look.LargeValues ? 22 : look.Layout == 8 ? 11 : refined ? 13 : 12,
                    TextColours.Parse(settings.ValueColor) ?? (refined || first && look.LargeValues ? Colors.White : accent));
                value.TextWrapping = TextWrapping.NoWrap;
                if (look.Terminal) value.FontFamily = Ui.DesignFont(Ui.FamilyFor("mono"));
                var line = new Grid { ColumnDefinitions = new ColumnDefinitions(first && look.LargeValues ? "*" : refined ? ".46*,.54*" : ".85*,1.15*"), ColumnSpacing = 6 };
                if (first && look.LargeValues)
                {
                    var hero = Stack(1, label, FitLeft(value)); line.Children.Add(hero);
                }
                else { line.Children.Add(label); Grid.SetColumn(label, 0); var fit = refined ? FitLeft(value) : FitRight(value); line.Children.Add(fit); Grid.SetColumn(fit, 1); }
                body.Children.Add(line); first = false;
            }
            var cores = part.Rows.Where(r => r.Group == "Cores").ToArray();
            if (cores.Length > 0)
                body.Children.Add(FixedCols(2, cores.Select(r => (Control)Stack(1, DashboardText(r.Label, 9), FitRight(V(r, 10, accent)))).ToArray()));
            if (part.Id == "cpu" && lastCoreLoads.Count >= 2 && AioReadings.Selected(settings, "aio.cpu.coreloads"))
            {
                coreBars = new AioCoreBars(Colors.White, C("#BBC6D1"), C("#29313E")) { Loads = lastCoreLoads, Tint = accent, Compact = refined, Heatmap = true };
                body.Children.Add(coreBars);
            }
            if (look.Graphs && part.Rows.FirstOrDefault(r => r.Number.HasValue) is { } chartRow && AioReadings.Graphs(settings, chartRow.Key))
            {
                var plot = new CaseSparkline(accent) { Height = 22, Tag = "case-sparkline" };
                casePlots.Add((chartRow.Key, plot)); body.Children.Add(plot);
            }
            var surface = new Border
            {
                Child = body, Padding = new Thickness(look.Metal ? 11 : refined ? 10 : 8),
                CornerRadius = new CornerRadius(look.Terminal ? 1 : look.Layout == 6 ? 12 : 7),
                BorderThickness = new Thickness(look.Layout == 4 ? 1.8 : 1), BorderBrush = Palette.Brush(refined ? Palette.Mix(accent,C("#1C2A34"),.7) : accent),
                Background = look.Metal ? new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                    GradientStops = { new GradientStop(SeeThrough(C("#3B4249")), 0), new GradientStop(SeeThrough(C("#161B20")), 1) } }
                    : refined ? Palette.Brush(SeeThrough(C("#FA0D151E"))) : Palette.Brush(SeeThrough(Color.FromArgb((byte)(look.Layout == 6 ? 230 : 248), 12, 18, 24))),
                Tag = "case-widget:" + part.Id
            };
            if (look.Layout == 5) title.Background = Palette.Brush(C("#5E1822"));
            scene.AddWidget(surface, part.Id);
        }
        dashboardLayout = scene; content.Children.Add(scene);
    }

    private string CaseSensorLabel(AioReading row) => row.Key == "aio.system.ram" ? L.T("Memory")
        : row.Key.StartsWith("aio.system.disk.") ? L.T("Temperature") : L.T(row.Label);
    private readonly List<(string Key, CaseSparkline Plot)> casePlots = new();
    private void UpdateCasePlots()
    {
        foreach (var (key, plot) in casePlots)
        {
            var row = readings.GetValueOrDefault(key);
            var saved = row is { History.Length: > 0 } ? history.Read(row.History) : Array.Empty<double>();
            plot.Set(saved.Length > 0 ? saved : samples.Read(key));
        }
    }
}

/// <summary>A real panel: live child controls above a clean background, with connectors recomputed after layout.</summary>
public sealed class CaseOverlayPanel : Panel
{
    public sealed record Widget(Control Control, string Component, int Lane, double Order, int Instance = 0);
    public sealed record Connector(string Component, Point Target, IReadOnlyList<Point> Points, int Instance = 0);
    private readonly Bitmap bitmap;
    private readonly int layout;
    private readonly Color accent;
    private readonly double naturalPictureHeight;
    private readonly CaseSurface surface;
    private readonly List<Widget> widgets = new();
    private readonly List<Connector> connectors = new();
    public IReadOnlyList<Widget> Widgets => widgets;
    public IReadOnlyList<Connector> Connectors => connectors;
    public int Columns { get; }
    public bool Refined => layout == 0 && Columns <= 2;
    public int LaneCount => Columns == 1 ? 2 : Columns;
    private Control? caption;
    public void SetCaption(Control value) { caption = value; Children.Add(value); }
    public Rect ArtworkBounds { get; private set; }
    public CaseOverlayPanel(Bitmap bitmap, int layout, int columns, Color accent, double referenceWidth = 440)
    {
        this.bitmap = bitmap; this.layout = layout; Columns = columns; this.accent = accent;
        naturalPictureHeight = Math.Max(620, referenceWidth * (Refined ? 2 : 2.25));
        ClipToBounds = true; Background = Brushes.Black;
        surface = new CaseSurface(this); Children.Add(surface);
    }
    public void AddWidget(Control control, string component)
    {
        int index = widgets.Count;
        double order = component switch { "fan" => .12, "cpu" => .34, "ram" => layout == 4 ? .46 : .29, "board" => .53, "gpu" => .60, "ssd" => .80, "power" => .88, "net" => .94, _ => .96 };
        bool right = component is "ram" or "ssd" or "net" or "power";
        if (layout == 0 && !Refined && component == "fan" || layout == 5 && component == "gpu") right = true;
        if (layout == 1 && component == "gpu" || layout == 8 && component == "ram") right = false;
        int lane = Columns <= 2 ? (layout == 4 ? 0 : right ? 1 : 0)
            : Enumerable.Range(0, LaneCount).MinBy(i => widgets.Where(w => w.Lane == i).Sum(w => w.Control is Border b && b.Child is StackPanel s ? s.Children.Count + 3 : 5));
        // Blue modules keep a single rail at one column, but use all requested rails when wider.
        if (layout == 4 && Columns > 1) lane = index % LaneCount;
        int instance = widgets.Count(w => w.Component == component);
        widgets.Add(new(control, component, lane, order + index * .0001, instance)); Children.Add(control);
    }
    private static bool Docked(Widget widget) => widget.Component is "net" or "power" or "other";
    private double DockWidth(double width)
    {
        int count = Math.Max(1,widgets.Count(Docked));
        return Math.Max(50,Math.Min(Refined && Columns > 1 ? 240 : double.PositiveInfinity,(width-24-10*(count-1))/count));
    }
    private double RailWidth(double width) => Math.Max(50, Math.Min(Refined && Columns > 1 ? 240 : double.PositiveInfinity,
        (width - 24 - (LaneCount - 1) * (Columns == 1 ? width * .16 : 18)) / LaneCount));
    protected override Size MeasureOverride(Size available)
    {
        double width = double.IsFinite(available.Width) ? available.Width : 440;
        double rail = RailWidth(width);
        caption?.Measure(new Size(Math.Max(1,width-24),double.PositiveInfinity));
        foreach (var w in widgets) w.Control.Measure(new Size(Refined && Docked(w) ? DockWidth(width) : rail, double.PositiveInfinity));
        double sum = Enumerable.Range(0, LaneCount).Select(i => widgets.Where(w => w.Lane == i).Sum(w => w.Control.DesiredSize.Height + 14)).DefaultIfEmpty(0).Max();
        double height = Math.Max(naturalPictureHeight, sum + 70);
        for (int attempt = 0; attempt < 40 && !Place(width,height,false); attempt++) height += 80;
        return new Size(width, height);
    }
    private bool PlaceRefined(double width, double height, bool arrange)
    {
        double aspect=bitmap.PixelSize.Width/(double)bitmap.PixelSize.Height;
        double imageWidth=Math.Max(width,height*aspect), imageHeight=Math.Max(height,width/aspect);
        ArtworkBounds=new Rect((width-imageWidth)/2,(height-imageHeight)/2,imageWidth,imageHeight);
        var dock=widgets.Where(Docked).ToArray();
        double dockHeight=dock.Select(w=>w.Control.DesiredSize.Height).DefaultIfEmpty(0).Max();
        double bottom=height-(dock.Length>0?dockHeight+26:12);
        double rail=RailWidth(width), top=caption!=null?caption.DesiredSize.Height+24:14;
        double cpuX=NominalPoint("cpu").X, ramX=NominalPoint("ram").X;
        double left=Columns==1?12:Math.Clamp(cpuX-rail-22,12,Math.Max(12,width/2-rail-12));
        double right=Columns==1?width-rail-12:Math.Clamp(ramX+22,width/2+12,Math.Max(width/2+12,width-rail-12));
        for(int lane=0;lane<2;lane++)
        {
            double Desired(Widget w) => w.Component switch
            {
                "fan" => .13*height,
                "ram" => (Columns==1?.25:.36)*height,
                "cpu" => .39*height,
                "board" => .53*height,
                "gpu" => .63*height,
                "ssd" => (w.Instance==0?.74:.52)*height,
                _ => .65*height
            };
            var list=widgets.Where(w=>!Docked(w)&&w.Lane==lane).OrderBy(Desired).ToArray();
            double remaining=list.Sum(w=>w.Control.DesiredSize.Height+10), previous=top;
            if(remaining+top>bottom) return false;
            foreach(var w in list)
            {
                double cardHeight=w.Control.DesiredSize.Height;
                double y=Math.Clamp(Desired(w)-cardHeight/2,previous,Math.Max(previous,bottom-remaining));
                if(arrange)w.Control.Arrange(new Rect(lane==0?left:right,y,rail,cardHeight));
                previous=y+cardHeight+10; remaining-=cardHeight+10;
            }
        }
        double dockWidth=DockWidth(width), dockLeft=(width-(dock.Length*dockWidth+Math.Max(0,dock.Length-1)*10))/2;
        if(arrange)
            for(int i=0;i<dock.Length;i++)dock[i].Control.Arrange(new Rect(dockLeft+i*(dockWidth+10),height-dockHeight-12,dockWidth,dock[i].Control.DesiredSize.Height));
        return true;
    }
    private bool Place(double width, double height, bool arrange)
    {
        if (Refined) return PlaceRefined(width, height, arrange);
        double rail = RailWidth(width), top = caption != null ? caption.DesiredSize.Height + 24 : 16;
        // Keep horizontal landmarks stable when FitPanel allocates extra vertical space. Otherwise
        // a component can cross into a widget rail only during arrange and invalidate the measured plan.
        double pictureWidth = Columns == 1 ? width : Math.Min(width, naturalPictureHeight / 2.25);
        ArtworkBounds = new Rect((width - pictureWidth) / 2, 0, pictureWidth, height);
        var targets = widgets.Select(w=>w.Component).Distinct().Select(id=>NominalPoint(id)).ToArray();
        for (int lane = 0; lane < LaneCount; lane++)
        {
            var list = widgets.Where(w => w.Lane == lane).OrderBy(w => w.Order).ToArray();
            double x = LaneCount == 1 ? 12 : 12 + lane * (width - 24 - rail) / (LaneCount - 1);
            var blocks = targets.Where(p=>p.X>=x-6 && p.X<=x+rail+6).Select(p=>(Top:p.Y-10,Bottom:p.Y+10)).OrderBy(p=>p.Top).ToArray();
            double previous=top; var ys=new double[list.Length];
            double First(double y,double cardHeight)
            {
                foreach(var block in blocks) if(y+cardHeight>block.Top && y<block.Bottom) y=block.Bottom+3;
                return y;
            }
            for(int i=0;i<list.Length;i++)
            {
                ys[i]=First(previous,list[i].Control.DesiredSize.Height);previous=ys[i]+list[i].Control.DesiredSize.Height+14;
            }
            if(previous>height+2) return false;
            for(int i=list.Length-1;i>=0;i--)
            {
                double cardHeight=list[i].Control.DesiredSize.Height;
                double limit=(i==list.Length-1?height-12:ys[i+1]-14)-cardHeight;
                double preferred=Math.Clamp(list[i].Order*height-cardHeight/2,ys[i],Math.Max(ys[i],limit));
                var options=blocks.Select(b=>b.Top-cardHeight-3).Append(preferred).Where(y=>y>=ys[i] && y<=limit && y<=preferred)
                    .Where(y=>blocks.All(b=>y+cardHeight<=b.Top || y>=b.Bottom)).ToArray();
                if(options.Length>0)ys[i]=options.Max();
                if(arrange)list[i].Control.Arrange(new Rect(x,ys[i],rail,cardHeight));
            }
        }
        return true;
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        surface.Arrange(new Rect(finalSize));
        caption?.Arrange(new Rect(12,8,Math.Max(1,finalSize.Width-24),caption.DesiredSize.Height));
        Place(finalSize.Width,finalSize.Height,true);
        connectors.Clear();
        foreach (var w in widgets)
        {
            if (Refined && Docked(w)) continue;
            Point target = ComponentPoint(w.Component, w.Instance);
            Rect rect = w.Control.Bounds;
            var start = new Point(target.X >= rect.Center.X ? rect.Right : rect.Left, Math.Clamp(target.Y, rect.Top + 5, rect.Bottom - 5));
            if (target.Y < rect.Top) start = new Point(Math.Clamp(target.X, rect.Left + 5, rect.Right - 5), rect.Top);
            else if (target.Y > rect.Bottom) start = new Point(Math.Clamp(target.X, rect.Left + 5, rect.Right - 5), rect.Bottom);
            var obstacles = widgets.Select(q => q.Control.Bounds).ToArray();
            var route = CaseConnectorRouter.Route(start, target, obstacles, new Rect(finalSize));
            connectors.Add(new(w.Component, target, route, w.Instance));
        }
        surface.InvalidateVisual(); return finalSize;
    }
    private Point NominalPoint(string component, int instance = 0)
    {
        // Coordinates measured on the ten source previews; image edits preserve these positions.
        var cpu = new[] { (.46,.40),(.52,.39),(.50,.39),(.50,.38),(.64,.37),(.50,.42),(.52,.41),(.51,.38),(.51,.35),(.57,.37) }[layout];
        var gpu = new[] { (.50,.58),(.50,.59),(.50,.60),(.50,.56),(.64,.63),(.50,.68),(.53,.60),(.51,.59),(.51,.59),(.52,.62) }[layout];
        var ram = new[] { (.65,.39),(.76,.40),(.71,.40),(.76,.36),(.83,.41),(.76,.36),(.77,.41),(.83,.38),(.80,.35),(.80,.37) }[layout];
        var ssd = new[] { (.42,.72),(.64,.72),(.50,.72),(.49,.73),(.68,.75),(.51,.78),(.53,.75),(.61,.73),(.59,.73),(.61,.77) }[layout];
        var p = component switch { "cpu" => cpu, "gpu" => gpu, "ram" => ram, "ssd" => ssd, "fan" => (.52,.13), "power" => (.51,.90), "net" => (.20,.31), _ => (.40,.50) };
        if(Refined)
        {
            if(Columns==2) p=component switch {"cpu"=>(.485,.365),"gpu"=>(.45,.605),"ram"=>(.67,.37),"ssd"=>instance==0?(.445,.713):(.46,.49),"fan"=>(.50,.14),_=>(.60,.51)};
            else if(component=="board")p=(.50,.51);
            else if(component=="ssd"&&instance>0)p=(.45,.51);
        }
        return new Point(ArtworkBounds.X + p.Item1 * ArtworkBounds.Width, ArtworkBounds.Y + p.Item2 * ArtworkBounds.Height);
    }
    public Point ComponentPoint(string component, int instance = 0)
    {
        var origin=NominalPoint(component,instance);
        var spread = component switch { "ram" => (.055,.10), "gpu" => (.25,.018), "ssd" => (.14,.018), "fan" => (.24,.038), "cpu" => (.045,.035), _ => (.04,.04) };
        // A pointer may land anywhere on its component's visible surface, never behind a value panel.
        var candidates = from dx in Enumerable.Range(-5,11) from dy in Enumerable.Range(-5,11)
            let point = new Point(origin.X + dx / 5d * spread.Item1 * ArtworkBounds.Width, origin.Y + dy / 5d * spread.Item2 * ArtworkBounds.Height)
            where !widgets.Any(w => w.Control.Bounds.Inflate(3).Contains(point))
            orderby Math.Abs(point.X-origin.X)+Math.Abs(point.Y-origin.Y)
            select point;
        return candidates.FirstOrDefault(origin);
    }
    internal void DrawSurface(DrawingContext context)
    {
        // The chassis continues across wide sidebars; no black gutters outside the central hardware view.
        var metal = new LinearGradientBrush { StartPoint = new RelativePoint(0,0,RelativeUnit.Relative), EndPoint = new RelativePoint(1,0,RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.Parse("#10151B"),0), new GradientStop(Color.Parse("#222B33"),.48), new GradientStop(Color.Parse("#121820"),1) } };
        context.DrawRectangle(metal,null,new Rect(Bounds.Size));
        if(!Refined && ArtworkBounds.Width < Bounds.Width - 4)
        {
            var seam=new Pen(Palette.Brush(Color.Parse("#303B45")),1);
            for(double x=18;x<Bounds.Width;x+=24) context.DrawLine(seam,new Point(x,20),new Point(x,Bounds.Height-20));
            context.DrawRectangle(Palette.Brush(Color.Parse("#343C44")),seam,new Rect(0,0,Bounds.Width,12));
            context.DrawRectangle(Palette.Brush(Color.Parse("#343C44")),seam,new Rect(0,Bounds.Height-12,Bounds.Width,12));
        }
        context.DrawImage(bitmap, new Rect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height), ArtworkBounds);
        var pen = new Pen(Palette.Brush(accent), layout == 4 ? 2 : 1.3);
        foreach (var connector in connectors)
        {
            var points = connector.Points;
            for (int i = 1; i < points.Count; i++) context.DrawLine(pen, points[i - 1], points[i]);
            if (points.Count < 2) continue;
            var end = points[^1]; var delta = new Vector(end.X - points[^2].X, end.Y - points[^2].Y); if (delta.Length < .1) continue;
            delta /= delta.Length; var perpendicular = new Vector(-delta.Y, delta.X);
            if(!Refined)
            {
                context.DrawLine(pen, end, end - delta * 7 + perpendicular * 3);
                context.DrawLine(pen, end, end - delta * 7 - perpendicular * 3);
            }
            context.DrawEllipse(Palette.Brush(accent), null, end, 2, 2);
        }
        if (layout == 7)
            foreach (var w in widgets)
                foreach (var p in new[] { w.Control.Bounds.TopLeft + new Vector(4,4), w.Control.Bounds.TopRight + new Vector(-4,4) })
                    context.DrawEllipse(Brushes.Gray, new Pen(Brushes.Black, 1), p, 2, 2);
    }
    private sealed class CaseSurface(CaseOverlayPanel owner) : Control
    {
        public override void Render(DrawingContext context) => owner.DrawSurface(context);
    }
}

/// <summary>Orthogonal visibility graph: a connector never runs through another sensor card.</summary>
public static class CaseConnectorRouter
{
    private static bool Inside(Point p, Rect r) => p.X > r.Left + .1 && p.X < r.Right - .1 && p.Y > r.Top + .1 && p.Y < r.Bottom - .1;
    private static bool Crosses(Point a, Point b, Rect r) => Math.Abs(a.X - b.X) < .1
        ? a.X > r.Left + .1 && a.X < r.Right - .1 && Math.Max(a.Y, b.Y) > r.Top + .1 && Math.Min(a.Y, b.Y) < r.Bottom - .1
        : a.Y > r.Top + .1 && a.Y < r.Bottom - .1 && Math.Max(a.X, b.X) > r.Left + .1 && Math.Min(a.X, b.X) < r.Right - .1;
    public static IReadOnlyList<Point> Route(Point start, Point target, IReadOnlyList<Rect> obstacles, Rect bounds)
    {
        // A selected panel may cover a component on a small viewport. Stop at its visible card edge,
        // rather than drawing a misleading pointer through an unrelated widget.
        if (obstacles.Any(r => Inside(target, r))) return Array.Empty<Point>();
        var xs = obstacles.SelectMany(r => new[] { Math.Max(2,r.Left - 4), Math.Min(bounds.Right - 2,r.Right + 4) }).Append(start.X).Append(target.X).Distinct().Order().ToArray();
        var ys = obstacles.SelectMany(r => new[] { Math.Max(2,r.Top - 4), Math.Min(bounds.Bottom - 2,r.Bottom + 4) }).Append(start.Y).Append(target.Y).Distinct().Order().ToArray();
        var points = (from x in xs from y in ys select new Point(x,y)).Where(p => !obstacles.Any(r => Inside(p,r))).ToArray();
        int from = Array.IndexOf(points,start), to = Array.IndexOf(points,target);
        if (from < 0 || to < 0) return Array.Empty<Point>();
        var distance = Enumerable.Repeat(double.PositiveInfinity,points.Length).ToArray(); var previous = Enumerable.Repeat(-1,points.Length).ToArray();
        var queue = new PriorityQueue<int,double>(); distance[from] = 0; queue.Enqueue(from,0);
        var byX = points.Select((p,i)=>(p,i)).GroupBy(v=>v.p.X).ToDictionary(g=>g.Key,g=>g.OrderBy(v=>v.p.Y).Select(v=>v.i).ToArray());
        var byY = points.Select((p,i)=>(p,i)).GroupBy(v=>v.p.Y).ToDictionary(g=>g.Key,g=>g.OrderBy(v=>v.p.X).Select(v=>v.i).ToArray());
        while (queue.TryDequeue(out int current,out double cost))
        {
            if(cost > distance[current]) continue; if(current == to) break;
            foreach(var group in new[] {byX[points[current].X],byY[points[current].Y]})
            {
                int pos=Array.IndexOf(group,current);
                foreach(int at in new[]{pos-1,pos+1}.Where(v=>v>=0&&v<group.Length))
                {
                    int next=group[at]; if(obstacles.Any(r=>Crosses(points[current],points[next],r))) continue;
                    double step=Math.Abs(points[next].X-points[current].X)+Math.Abs(points[next].Y-points[current].Y); double candidate=cost+step;
                    if(candidate>=distance[next]) continue; distance[next]=candidate; previous[next]=current;queue.Enqueue(next,candidate);
                }
            }
        }
        if(!double.IsFinite(distance[to])) return Array.Empty<Point>();
        var route=new List<Point>();for(int i=to;i>=0;i=previous[i]){route.Add(points[i]);if(i==from)break;}route.Reverse();
        for(int i=route.Count-2;i>0;i--)
            if(Math.Abs(route[i-1].X-route[i+1].X)<.1 || Math.Abs(route[i-1].Y-route[i+1].Y)<.1)route.RemoveAt(i);
        return route;
    }
}

public sealed class CaseSparkline(Color accent) : Control, IColourGraph
{
    private double[] values = Array.Empty<double>();
    public void Set(double[] data) { values = data.Where(double.IsFinite).ToArray(); InvalidateVisual(); }
    public override void Render(DrawingContext context)
    {
        if (values.Length < 2) return;
        double low = Math.Min(0, values.Min()), high = Math.Max(low + 1, values.Max() * 1.1);
        var pen = new Pen(Palette.Brush(accent), 1.3);
        Point At(int i) => new(i * Bounds.Width / (values.Length - 1), Bounds.Height - 2 - (values[i] - low) / (high - low) * (Bounds.Height - 4));
        for (int i = 1; i < values.Length; i++) context.DrawLine(pen, At(i-1), At(i));
    }
}
