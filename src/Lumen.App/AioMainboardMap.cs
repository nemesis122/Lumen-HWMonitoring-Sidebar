using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Lumen.Core;

namespace Lumen.App;

public static class MainboardMapColors
{
    public static Color Cyan=>Color.Parse("#25D5DD");
    public static Color Violet=>Color.Parse("#AD87E8");
    public static Color For(string part)=>part is "gpu" or "psu"?Violet:part is "cpu" or "ram"?Cyan:Color.Parse("#64707D");
    public static int Number(string part)=>part switch{"cpu"=>1,"gpu"=>2,"ram"=>3,"psu"=>4,_=>0};
}

public sealed partial class AioTile
{
    private static readonly Lazy<Bitmap> mapArtwork = new(() => new Bitmap(AssetLoader.Open(new Uri("avares://Lumen/Assets/Aio/mainboard-map.png"))));

    private void MainboardMap()
    {
        drivesPlaced = true;
        coreBars = null;
        mainboardCores = null;
        Padding = new Thickness(8);
        CornerRadius = new CornerRadius(9);
        BorderBrush = Palette.Brush(Palette.Mix(track, Cyan, .55));
        var left = new MainboardDriveGrid();
        var right = new MainboardDriveGrid();
        var bottom = Stack(12);
        var driveCards = new List<Control>();
        var targets = new List<(Control Card, Point Target)>();

        Control Row(AioReading r)
        {
            string label = r.Key.StartsWith("aio.system.disk.") ? "Temp" : r.Key.EndsWith(".power") ? "Power" : r.Key.EndsWith(".junction") ? "VRAM °C" : RefLabel(r);
            var caption=Text(label,11,muted);caption.Tag="map-label:"+r.Key;
            var row = Ui.Columns("*,1.6*", FitLeft(caption), FitRight(V(r, 13)));
            row.ColumnSpacing = 6;
            return row;
        }
        Control Box(string title, string art, IEnumerable<Control> rows, Point target, string model = "",Color? tone=null,bool identify=true)
        {
            var accent=tone??MainboardMapColors.For(art);int number=identify?MainboardMapColors.Number(art):0;
            var header = Ui.Columns("Auto,*", Part(art, 26, 24), FitLeft(Text((number>0?number+" ":"")+title, 15, ink)));
            header.ColumnSpacing = 5;
            var body = Stack(5,new Border{Height=3,Background=Palette.Brush(accent)},header);
            if(model.Length > 0)
            {
                var name=Text(model,10,muted); name.TextWrapping=TextWrapping.NoWrap; name.TextTrimming=TextTrimming.CharacterEllipsis;
                ToolTip.SetTip(name,model); body.Children.Add(name);
            }
            foreach(var row in rows) body.Children.Add(row);
            var card = new Border { Child = body, Padding = new Thickness(10), CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(number>0?1.5:1), BorderBrush = Palette.Brush(accent), Background = Palette.Brush(SeeThrough(back)), Tag="map-"+art };
            targets.Add((card,target));
            return card;
        }
        if(Rs("CPU").Length > 0)
        {
            var owner=source.Widgets.FirstOrDefault(w=>w.Kind==WidgetKind.Cpu);
            var rows=new List<Control>();
            var temp=Rs("CPU").FirstOrDefault(r=>r.Key.EndsWith(".temp"));
            if(temp!=null) rows.Add(FitLeft(V(temp,34,ink)));
            rows.AddRange(Rs("CPU").Where(r=>r!=temp).Select(Row));
            left.Children.Add(Box(Naming.Title(owner,0,"CPU"),"cpu",rows,new Point(.45,.30),PartModel(WidgetKind.Cpu,0,snapshot.Cpu?.Name??"")));
        }
        int gpuIndex=0;
        foreach(var gpu in Rs("GPU").GroupBy(r=>r.Device))
        {
            var owner=source.Widgets.FirstOrDefault(w=>w.Kind==WidgetKind.Gpu);
            var rows=new List<Control>();var temp=gpu.FirstOrDefault(r=>r.Key.EndsWith(".temp"));
            if(temp!=null) rows.Add(FitLeft(V(temp,34,ink)));
            rows.AddRange(gpu.Where(r=>r!=temp).Select(Row));
            left.Children.Add(Box(Naming.Title(owner,gpuIndex,"GPU"+(gpuIndex==0?"":" "+(gpuIndex+1))),"gpu",rows,new Point(.40,.64),
                PartModel(WidgetKind.Gpu,gpuIndex,snapshot.Gpus.FirstOrDefault(g=>g.Id==gpu.Key)?.Name??"")));
            gpuIndex++;
        }
        Control network = new Border();
        if(Rs("Network").Length>0)
        {
            var values=Rs("Network").Select(r=>(Control)Stack(3,
                FitLeft(Text(r.Key.EndsWith(".down")?"↓ DL":r.Key.EndsWith(".up")?"↑ UP":"Link",10,muted)),FitLeft(V(r,13,r.Key.EndsWith(".up")?MainboardMapColors.Violet:r.Key.EndsWith(".link")?ink:MainboardMapColors.Cyan)))).ToArray();
            network=Box("NETWORK","router",new[]{FixedCols(values.Length,values)},new Point(.05,.22));
        }
        if(R("ram","System") is {} ram)
        {
            var load=V(ram,32,ink);
            var capacity=Text(Amount(ram),12,ink,true); fields[ram.Key].Add(capacity); formats[capacity]=Amount;
            right.Children.Add(Box("RAM","ram",new Control[]{FitLeft(load),FitLeft(capacity),PartMeter(ram,Cyan)},new Point(.76,.28)));
        }
        if(Rs("Rails").Length>0) right.Children.Add(Box("PSU","psu",Rs("Rails").Select(Row),new Point(.93,.34)));
        int driveIndex=0;
        foreach(var drive in PartDrives())
        {
            var tone=driveIndex%2==0?MainboardMapColors.Cyan:MainboardMapColors.Violet;
            var rows=new List<Control>();
            if(drive.Temp!=null) rows.Add(FitLeft(V(drive.Temp,22,ink)));
            rows.AddRange(drive.Rows.Select(Row));
            if(drive.Rows.FirstOrDefault(r=>r.Key.EndsWith(".used")) is {} used) rows.Add(PartMeter(used,tone));
            driveCards.Add(Box(AioReadings.DriveName(snapshot,drive.Id),"ssd",rows,new Point(driveIndex++%2==0?.42:.74,.88),tone:tone));
        }
        if(lastCoreLoads.Count>=2 && AioReadings.Selected(settings,"aio.cpu.coreloads") && Rs("CPU").Length>0)
        {
            mainboardCores=new MainboardCoreGrid(ink,muted,track,Cyan){Loads=lastCoreLoads,TwoTone=true};
            bottom.Children.Add(Box("CORE LOAD · %","cpu",new Control[]{mainboardCores},new Point(.43,.30),identify:false));
        }
        if(Rs("Cooling").Length>0) bottom.Children.Add(Box("FANS","fan0",new[]{FanGrid(Rs("Cooling"),40,true,true,true)},new Point(.72,.98)));
        if(Rs("Cores").Length>0) bottom.Children.Add(Box("CORE TEMP","cpu",Rs("Cores").Select(Row),new Point(.43,.30),identify:false));
        var image=new Image{Source=mapArtwork.Value,Stretch=Stretch.Uniform,Tag="mainboard-artwork"};
        var drives=new MainboardDriveGrid(driveCards);
        // Bound the map before FitPanel searches for a scale. Otherwise reducing text size
        // widens the unscaled board again, feeding back into another reduction.
        content.Children.Add(new MainboardMapLayout(left,image,right,drives,network,bottom,targets,Cyan)
            {Tag="aio-section",MaxWidth=1400,HorizontalAlignment=HorizontalAlignment.Center});
    }
}

/// <summary>Sidebar: full-width board followed by CPU/GPU and RAM/PSU rows. Wide windows keep the map callouts.</summary>
public sealed class MainboardMapLayout : Panel
{
    private readonly Control board,network,bottom;
    private readonly MainboardDriveGrid left,right;
    private readonly MainboardDriveGrid drives;
    private readonly MapConnections connections;
    public bool Wide { get; private set; }
    internal bool InTopPair(Control card)=>left.Children.IndexOf(card) is 0 or 1;
    internal bool InFooter(Control card)=>card.Parent==bottom||(!Wide&&ReferenceEquals(card,network));
    internal bool IsDrive(Control card)=>drives.Children.Contains(card);
    public MainboardMapLayout(MainboardDriveGrid left,Control image,MainboardDriveGrid right,MainboardDriveGrid drives,Control network,Control bottom,IReadOnlyList<(Control Card,Point Target)> targets,Color color)
    {
        this.left=left;this.right=right;this.drives=drives;this.network=network;this.bottom=bottom;
        var art=new Grid{RowDefinitions=new RowDefinitions("Auto,*"),RowSpacing=6};
        art.Children.Add(Ui.Text("MAINBOARD",12,color));Grid.SetRow(image,1);art.Children.Add(image);
        board=new Border{Child=art,Padding=new Thickness(8),CornerRadius=new CornerRadius(6),BorderThickness=new Thickness(1),BorderBrush=Palette.Brush(color),Tag="mainboard-panel"};
        connections=new MapConnections(this,image,targets,color);
        Children.Add(left);Children.Add(board);Children.Add(right);Children.Add(drives);Children.Add(network);Children.Add(bottom);Children.Add(connections);
    }
    protected override Size MeasureOverride(Size available)
    {
        double width=double.IsFinite(available.Width)?available.Width:1100;
        Wide=width>=780;
        double side=Wide?Math.Max(180,(width-32)*.235):Math.Max(0,width-24);
        double middle=Wide?width-side*2-32:width;
        int columns=Wide?1:2;
        foreach(var grid in new[]{left,right,drives}) if(grid.MaxColumns!=columns){grid.MaxColumns=columns;grid.InvalidateMeasure();}
        left.Measure(new Size(side,double.PositiveInfinity));right.Measure(new Size(side,double.PositiveInfinity));
        double boardHeight=Wide?Math.Min(600,middle):Math.Clamp(width*.72,160,330);
        board.Measure(new Size(middle,boardHeight));
        drives.Measure(new Size(Wide?side:width,double.PositiveInfinity));
        network.Measure(new Size(Wide?side:width,double.PositiveInfinity));bottom.Measure(new Size(width,double.PositiveInfinity));
        double height=Wide?Math.Max(boardHeight,Math.Max(left.DesiredSize.Height+12+network.DesiredSize.Height,right.DesiredSize.Height+12+drives.DesiredSize.Height)):
            boardHeight+12+left.DesiredSize.Height+12+right.DesiredSize.Height+12+drives.DesiredSize.Height+12+network.DesiredSize.Height;
        return new Size(width,height+16+bottom.DesiredSize.Height);
    }
    protected override Size ArrangeOverride(Size final)
    {
        Wide=final.Width>=780;
        double side=Wide?Math.Max(180,(final.Width-32)*.235):Math.Max(0,final.Width-24);
        double middle=Wide?final.Width-side*2-32:final.Width;
        double boardHeight=Wide?Math.Min(600,middle):Math.Clamp(final.Width*.72,160,330);
        board.Arrange(new Rect(Wide?side+16:0,0,middle,boardHeight));
        double height;
        if(Wide)
        {
            left.Arrange(new Rect(0,0,side,left.DesiredSize.Height));
            right.Arrange(new Rect(final.Width-side,0,side,right.DesiredSize.Height));
            network.Arrange(new Rect(0,left.DesiredSize.Height+12,side,network.DesiredSize.Height));
            drives.Arrange(new Rect(final.Width-side,right.DesiredSize.Height+12,side,drives.DesiredSize.Height));
            height=Math.Max(boardHeight,Math.Max(left.DesiredSize.Height+12+network.DesiredSize.Height,right.DesiredSize.Height+12+drives.DesiredSize.Height));
        }
        else
        {
            double y=boardHeight+12;
            left.Arrange(new Rect(12,y,side,left.DesiredSize.Height));y+=left.DesiredSize.Height+12;
            right.Arrange(new Rect(12,y,side,right.DesiredSize.Height));y+=right.DesiredSize.Height+12;
            drives.Arrange(new Rect(0,y,final.Width,drives.DesiredSize.Height));y+=drives.DesiredSize.Height+12;
            network.Arrange(new Rect(0,y,final.Width,network.DesiredSize.Height));
            height=y+network.DesiredSize.Height;
        }
        bottom.Arrange(new Rect(0,height+16,final.Width,bottom.DesiredSize.Height));
        connections.Arrange(new Rect(final));connections.InvalidateVisual();
        return final;
    }
}

public sealed class MainboardDriveGrid : Panel
{
    public int MaxColumns { get; set; }=2;
    private double[] rows=Array.Empty<double>();
    public MainboardDriveGrid(IEnumerable<Control>? cards=null){foreach(var card in cards??Array.Empty<Control>())Children.Add(card);}
    protected override Size MeasureOverride(Size available)
    {
        double width=double.IsFinite(available.Width)?available.Width:380;
        int columns=Math.Max(1,Math.Min(MaxColumns,Children.Count));
        double cell=Math.Max(0,(width-12*(columns-1))/columns);
        rows=new double[(Children.Count+columns-1)/columns];
        for(int i=0;i<Children.Count;i++)
        {
            Children[i].Measure(new Size(cell,double.PositiveInfinity));
            rows[i/columns]=Math.Max(rows[i/columns],Children[i].DesiredSize.Height);
        }
        return new Size(width,rows.Sum()+Math.Max(0,rows.Length-1)*12);
    }
    protected override Size ArrangeOverride(Size final)
    {
        int columns=Math.Max(1,Math.Min(MaxColumns,Children.Count));
        double cell=Math.Max(0,(final.Width-12*(columns-1))/columns),y=0;
        for(int row=0;row<rows.Length;row++)
        {
            for(int col=0;col<columns;col++)
            {
                int i=row*columns+col;if(i>=Children.Count)break;
                Children[i].Arrange(new Rect(col*(cell+12),y,cell,rows[row]));
            }
            y+=rows[row]+12;
        }
        return final;
    }
}

public sealed record MapConnectionPath(string Part,Control Card,Rect Component,IReadOnlyList<Point> Points);

/// <summary>Variant 1: independent orthogonal cyan paths, hollow contacts and component contours.</summary>
public sealed class MapConnections : Control
{
    private readonly MainboardMapLayout layout;
    private readonly Control board;
    private readonly IReadOnlyList<(Control Card,Point Target)> targets;
    private readonly Color color;
    public MapConnections(MainboardMapLayout layout,Control board,IReadOnlyList<(Control Card,Point Target)> targets,Color color)
    {this.layout=layout;this.board=board;this.targets=targets;this.color=color;IsHitTestVisible=false;}
    public IReadOnlyList<MapConnectionPath> GetPaths()
    {
        var paths=new List<MapConnectionPath>();
        var origin=board.TranslatePoint(default,this);if(origin==null||board.Bounds.Width<=0||board.Bounds.Height<=0)return paths;
        // Image.Stretch=Uniform letterboxes the artwork. Map against the actual drawn bitmap, not the Image control's bounds.
        var source=board is Image{Source:{} image}?image.Size:board.Bounds.Size;
        if(source.Width<=0||source.Height<=0)return paths;
        double scale=Math.Min(board.Bounds.Width/source.Width,board.Bounds.Height/source.Height);
        var art=new Rect(origin.Value.X+(board.Bounds.Width-source.Width*scale)/2,
            origin.Value.Y+(board.Bounds.Height-source.Height*scale)/2,source.Width*scale,source.Height*scale);
        foreach(var (card,target) in targets)
        {
            var start=card.TranslatePoint(default,this);if(start==null||layout.InFooter(card))continue;
            string part=(card.Tag as string??"").Replace("map-","");
            var normalized=part switch
            {
                "cpu"=>new Rect(.345,.18,.22,.23),
                "gpu"=>new Rect(.07,.525,.67,.25),
                "ram"=>new Rect(.66,.055,.20,.42),
                "psu"=>new Rect(.90,.21,.06,.20),
                _=>default
            };
            bool hardware=normalized.Width>0;
            if(!layout.Wide&&!hardware)continue;
            var component=hardware?new Rect(art.X+normalized.X*art.Width,art.Y+normalized.Y*art.Height,
                normalized.Width*art.Width,normalized.Height*art.Height):default;
            var end=new Point(art.X+art.Width*target.X,art.Y+art.Height*target.Y);
            var points=new List<Point>();
            if(layout.Wide)
            {
                bool left=start.Value.X<art.X;
                var p=new Point(start.Value.X+(left?card.Bounds.Width:0),start.Value.Y+Math.Min(38,card.Bounds.Height/2));
                double elbow=left?art.Left-8:art.Right+8;
                if(part=="ram")points.AddRange(new[]{p,new Point(elbow,p.Y),new Point(elbow,art.Top+4),new Point(end.X,art.Top+4),end});
                else points.AddRange(new[]{p,new Point(elbow,p.Y),new Point(elbow,end.Y),end});
            }
            else
            {
                bool left=start.Value.X+card.Bounds.Width/2<Bounds.Width/2;
                if(layout.InTopPair(card))
                {
                    // Both upper cards leave through the empty strip above their borders.
                    var p=new Point(start.Value.X+card.Bounds.Width/2,start.Value.Y);
                    double lane=left?art.Left-8:art.Right+8;
                    points.AddRange(new[]{p,new Point(p.X,p.Y-6),new Point(lane,p.Y-6),new Point(lane,end.Y),end});
                }
                else
                {
                    // Lower cards use reserved outer gutters, never the CPU/GPU text area.
                    var p=new Point(start.Value.X+(left?0:card.Bounds.Width),start.Value.Y+18);
                    double lane=left?4:Bounds.Width-4;
                    if(part=="ram")points.AddRange(new[]{p,new Point(lane,p.Y),new Point(lane,art.Top+4),new Point(end.X,art.Top+4),end});
                    else points.AddRange(new[]{p,new Point(lane,p.Y),new Point(lane,end.Y),end});
                }
            }
            paths.Add(new MapConnectionPath(part,card,component,points));
        }
        return paths;
    }
    public override void Render(DrawingContext c)
    {
        var contact=Palette.Brush(Color.Parse("#0C1017"));
        foreach(var path in GetPaths())
        {
            var accent=MainboardMapColors.For(path.Part);
            var brush=Palette.Brush(accent);var pen=new Pen(brush,2.4);var outline=new Pen(brush,2.5);
            if(path.Component.Width>0)c.DrawRectangle(null,outline,path.Component,2,2);
            for(int i=1;i<path.Points.Count;i++)c.DrawLine(pen,path.Points[i-1],path.Points[i]);
            c.DrawEllipse(contact,pen,path.Points[0],3,3);
            var end=path.Points[^1];var previous=path.Points[^2];var delta=end-previous;
            double length=Math.Sqrt(delta.X*delta.X+delta.Y*delta.Y);
            if(length>0)
            {
                var direction=delta/length;var cross=new Vector(-direction.Y,direction.X);
                var arrow=new StreamGeometry();using(var geometry=arrow.Open())
                {
                    geometry.BeginFigure(end,true);geometry.LineTo(end-direction*8+cross*4.5);geometry.LineTo(end-direction*8-cross*4.5);geometry.EndFigure(true);
                }
                c.DrawGeometry(brush,null,arrow);
            }
            int number=MainboardMapColors.Number(path.Part);
            if(number>0&&path.Component.Width>0)
            {
                string label=number+" "+(path.Part=="psu"?"ATX":path.Part.ToUpperInvariant());
                var text=new FormattedText(label,System.Globalization.CultureInfo.InvariantCulture,FlowDirection.LeftToRight,
                    new Typeface(Ui.Font,FontStyle.Normal,FontWeight.SemiBold),10,brush);
                double x=path.Part=="psu"?path.Component.Right+4:path.Component.Left;
                double y=path.Part=="psu"?path.Component.Top:path.Component.Top-text.Height-6;
                x=Math.Clamp(x,1,Math.Max(1,Bounds.Width-text.Width-9));y=Math.Max(1,y);
                var badge=new Rect(x,y,text.Width+8,text.Height+4);
                c.DrawRectangle(contact,new Pen(brush,1.3),badge,3,3);c.DrawText(text,new Point(x+4,y+2));
            }
        }
    }
}

public sealed class MainboardMapWindow : Window
{
    private AioTile card=null!;
    private readonly Settings settings;
    public MainboardMapWindow(Settings settings,Palette palette)
    {
        this.settings=settings;Title="Lumen · Mainboard Map";Width=1180;Height=920;MinWidth=800;MinHeight=600;
        WindowStartupLocation=WindowStartupLocation.CenterScreen;
        Icon=new WindowIcon(AssetLoader.Open(new Uri("avares://Lumen/Assets/lumen.ico")));
        Rebuild(palette);
    }
    public void Rebuild(Palette palette)
    {
        var original=settings.Widgets.FirstOrDefault(w=>w.Kind==WidgetKind.Overview);
        var slot=new WidgetSlot{Kind=WidgetKind.Overview,Style=WidgetStyle.AioMainboardMap,
            HiddenSensors=original?.HiddenSensors.ToList()??new(),AioEnabledSensors=original?.AioEnabledSensors.ToList()??new()};
        card=new AioTile(slot.Style,palette,Metrics.For(settings.Density),settings);card.Own(slot,0);
        Background=Palette.Brush(palette.Surface);
        Content=new ScrollViewer{Content=new StackPanel{Margin=new Thickness(12),Children={card}},
            HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Auto};
    }
    public void Update(Snapshot snapshot,History history)=>card.Update(snapshot,history);
}
