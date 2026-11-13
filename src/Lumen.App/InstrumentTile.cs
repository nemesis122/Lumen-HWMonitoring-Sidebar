using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public static class InstrumentDesigns
{
    public static readonly WidgetStyle[] Styles = { WidgetStyle.SplitFlap, WidgetStyle.MeasureTape, WidgetStyle.HardwareMap };
    public static bool Contains(WidgetStyle style) => Styles.Contains(style);
    public static void Frame(Border frame, WidgetStyle style, Palette p, Metrics m)
    {
        frame.Padding = new Thickness(m.Pad, 12);
        frame.CornerRadius = new CornerRadius(style == WidgetStyle.SplitFlap ? 5 : 0);
        frame.BorderThickness = style == WidgetStyle.HardwareMap ? new Thickness(0, 0, 0, 1) : new Thickness(1);
        frame.BorderBrush = Palette.Brush(p.Stroke);
        frame.Background = Palette.Brush(p.DesignBack(style switch { WidgetStyle.SplitFlap => "#22211d", WidgetStyle.MeasureTape => "#202323", _ => "#131916" }));
        if (style == WidgetStyle.SplitFlap) frame.Background = new LinearGradientBrush { StartPoint = new RelativePoint(0,0,RelativeUnit.Relative), EndPoint = new RelativePoint(1,1,RelativeUnit.Relative), GradientStops = { new GradientStop(p.DesignBack("#35332d"),0), new GradientStop(p.DesignBack("#191916"),1) } };
        if (style == WidgetStyle.MeasureTape && !p.Dark) frame.Background = Palette.Brush(Palette.Mix(p.Card, Color.Parse("#d9dedb"), .2));
    }
}

// Each native reading is attached to its canonical sensor ID. A shape change rebuilds the fields;
// ordinary samples only update text/plots. Extra selected fields use the very same native renderer.
public sealed class InstrumentTile : SummaryTile
{
    private readonly WidgetStyle style;
    private readonly TextBlock heading, hardware;
    private readonly StackPanel body = new() { Spacing = 7 };
    private readonly Dictionary<string, TextBlock> values = new();
    private readonly List<(string Key, InstrumentScale Scale)> scales = new();
    private readonly List<(string Key, AreaChart Chart)> charts = new();
    private readonly List<(string Id, InstrumentScale Scale)> usage = new();
    private InstrumentScale? memoryLevel;
    private readonly DriveTransferPairs transfers = new();
    private string shape = "";
    private readonly ContentControl symbol = new();
    private string symbolKey = "";
    public static bool Fits(WidgetKind kind) => kind is WidgetKind.Cpu or WidgetKind.Gpu or WidgetKind.Memory or WidgetKind.Drives or WidgetKind.Network or WidgetKind.Cooling or WidgetKind.Power or WidgetKind.Battery or WidgetKind.Fps or WidgetKind.Volumes or WidgetKind.Ping;

    public InstrumentTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s, WidgetStyle style) : base(kind,index,p,m,s,false)
    {
        this.style = style; InstrumentDesigns.Frame(this,style,p,m);
        heading = Ui.Text("", 14, p.Ink, FontWeight.SemiBold);
        hardware = Ui.Text("", 11, p.Soft);
        HardwareWidgetTitle.Register(this, heading, hardware);
        var header = Ui.Columns("Auto,*", symbol, heading); heading.Margin = new Thickness(8,0,0,0);
        var content = Ui.Stack(10, header, body, Extras, transfers);
        content.Tag = "elastic";
        if (style == WidgetStyle.HardwareMap)
        {
            var rail = new Border { BorderThickness = new Thickness(2,0,0,0), BorderBrush = Palette.Brush(Accent), Padding = new Thickness(12,0,0,0), Child = content };
            Child = rail;
        }
        else Child = content;
    }

    private Color Accent => (Slot ?? S.ActiveWidget)?.Accent is { Length: > 0 } ? Accents.For(Kind,Slot ?? S.ActiveWidget,P)
        : Accents.Design(style == WidgetStyle.SplitFlap ? "#e0cda7" : style == WidgetStyle.MeasureTape ? "#d87761" : "#b8d99d", P);
    protected override List<(string Label,string Value)> AdjustExtras(List<(string Label,string Value)> items) => new();

    protected override void Show(Summary sum)
    {
        heading.Text = Naming.Title(Slot ?? S.ActiveWidget,Index,sum.Title);
        string iconKey = sum.Glyph + Accent;
        if (symbolKey != iconKey) { symbolKey = iconKey; symbol.Content = new Viewbox { Width = style == WidgetStyle.HardwareMap ? 34 : 17, Height = style == WidgetStyle.HardwareMap ? 34 : 17, Tag = "graphic", Child = Ui.Glyph(sum.Glyph,Accent,17) }; }
        var all = CompleteReadings.For(Kind,Index,Last.Snap,Last.History,S,P);
        var rows = all.Where(r => Kind != WidgetKind.Drives || !r.Key.EndsWith(":model")).ToList();
        string next = string.Join('|',rows.Select(r => r.Key + r.Label)) + "|" + S.Shows("drives.bar") + "|" + S.Shows("mem.bar")
            + "|" + S.Shows("cpu.temp.history") + S.Shows("cpu.load.history") + S.Shows("gpu.temp.history") + S.Shows("gpu.load.history")
            + S.Shows("net.down.history") + S.Shows("net.up.history") + (Summaries.ChartKey(Kind) is string group ? S.Shows(group) : true);
        if (shape != next) { shape = next; Build(rows); }
        foreach (var row in rows)
        {
            if (values.TryGetValue(row.Key,out var field)) { field.Text = row.Value; ToolTip.SetTip(field,row.Label + ": " + row.Value); }
        }
        transfers.Update(rows.Where(DriveTransferPairs.IsRate).ToArray(), style, P, Last.Snap);
        foreach (var (key,scale) in scales) scale.Set(Level(rows.First(r => r.Key == key)), Accent, P.Soft);
        foreach (var (id,scale) in usage) scale.Set(Last.Snap.Disks.FirstOrDefault(d => d.Id == id)?.UsedPercent / 100, Accent, P.Soft);
        memoryLevel?.Set(Last.Snap.Memory?.Load / 100, Accent, P.Soft);
        foreach (var (key,chart) in charts)
        {
            var gpu = GpuGroup.Visible(Last.Snap,S).ElementAtOrDefault(Index);
            string source = key.StartsWith("gpu.") && gpu != null ? "gpu." + gpu.Id + key[3..] : key;
            var samples = Last.History.Read(source); chart.IsVisible = samples.Any(double.IsFinite);
            chart.Max = key is "net.down" or "net.up" ? null : 100; chart.Set(samples, key == "net.up" ? P.Calm : Accent);
        }
    }

    private double? Level(CompleteReading row)
    {
        if (row.Key == "cpu.temp") return Last.Snap.Cpu?.Temperature / 100;
        if (row.Key == "gpu.temp") return GpuGroup.Visible(Last.Snap,S).ElementAtOrDefault(Index)?.Temperature / 100;
        if (row.Key.StartsWith("drive:") && row.Key.EndsWith(":temp")) return Last.Snap.Disks.FirstOrDefault(d => "drive:" + d.Id + ":temp" == row.Key)?.Temperature / 100;
        return row.Fraction;
    }

    private void Build(List<CompleteReading> rows)
    {
        body.Children.Clear(); values.Clear(); scales.Clear(); charts.Clear(); usage.Clear(); memoryLevel = null;
        var lead = rows.Where(r => Kind == WidgetKind.Drives ? r.Key.EndsWith(":temp") : r.Key == WidgetSensors.PrimaryKey(Kind) || r.Key == WidgetSensors.SecondaryKey(Kind)).ToArray();
        if (lead.Length > 0)
            body.Children.Add(new StatPanel(Array.Empty<double>(),lead.Select((r,i) => { var field=Field(r,true); if(i<lead.Length-1) field.Margin=new Thickness(0,0,8,0); return field; }).ToArray()) { MaxColumns = 2, MinimumColumnWidth = 110, RespectIntrinsicWidth = true, Balanced = true, RowSpacing = 8 });
        foreach (var row in rows.Where(r => !lead.Contains(r) && !DriveTransferPairs.IsRate(r))) body.Children.Add(Field(row,false));
        if (Kind == WidgetKind.Memory && S.Shows("mem.bar"))
        {
            memoryLevel = new InstrumentScale { Height = 18, Tag = "graphic" };
            body.Children.Add(memoryLevel);
        }
        if (Kind == WidgetKind.Drives && S.Shows("drives.bar"))
        {
            foreach (var disk in DriveOrdering.Order(Last.Snap.Disks,DriveOrdering.Mode(S)).Where(d => S.Shows("drive:" + d.Id)))
            {
                var scale = new InstrumentScale { Height = 18, Tag = "graphic" };
                usage.Add((disk.Id,scale)); ToolTip.SetTip(scale,disk.Letters + " · " + L.T("Used"));
                body.Children.Add(Ui.Stack(1,Ui.Text(disk.Letters + " · " + L.T("Used"),10,P.Soft),scale));
            }
        }
        var plotKeys = Kind switch { WidgetKind.Cpu => new[] {"cpu.temp","cpu.load"}, WidgetKind.Gpu => new[] {"gpu.temp","gpu.load"}, WidgetKind.Network => new[] {"net.down","net.up"}, _ => Array.Empty<string>() };
        foreach (var key in plotKeys.Where(k => rows.Any(r => r.Key == k) && S.Shows(k + ".history") && (Summaries.ChartKey(Kind) is not string g || S.Shows(g))))
        {
            var chart = new AreaChart { Height = 20, FillOpacity = .05, LineWidth = 1.1 }; charts.Add((key,chart));
            body.Children.Add(chart); ToolTip.SetTip(chart,rows.First(r => r.Key == key).Label + " · " + L.T("History chart"));
        }
        // The common transfer footer owns the bottom of the widget and the per-device identities.
    }

    private Control Field(CompleteReading row, bool main)
    {
        var label = Ui.Text(row.Label,10.5,P.Soft,FontWeight.Medium); label.TextWrapping = TextWrapping.Wrap;
        double size = main ? Kind == WidgetKind.Network ? 25 : row.Key == "mem.available" ? 24 : 30 : 13;
        var value = SensorDetails.Value(Ui.Text(row.Value,size,P.Ink,FontWeight.SemiBold,numbers:true),row.Key);
        value.TextWrapping = TextWrapping.Wrap; value.TextTrimming = TextTrimming.None;
        values.Add(row.Key,value);
        if (style == WidgetStyle.SplitFlap)
        {
            if (!main) return new StableReadingRow(label,value,S,M,90);
            value.FontFamily = Ui.FamilyFor("mono"); value.TextAlignment = TextAlignment.Center; value.LetterSpacing = 1.1;
            return Ui.Stack(4,label,new SplitFlapSurface(value,P));
        }
        if (style == WidgetStyle.MeasureTape)
        {
            if (Level(row).HasValue && !row.Address && Kind != WidgetKind.Memory)
            {
                var scale = new InstrumentScale { Height = main ? 32 : 18, Tag = "graphic", Labels = main, Temperature = row.Key.EndsWith(".temp") || row.Key.EndsWith(":temp"), Fahrenheit = S.Fahrenheit }; scales.Add((row.Key,scale));
                return main ? Ui.Stack(3,label,value,scale) : Ui.Stack(2,Ui.Columns("*,Auto",label,value),scale);
            }
            return main ? Ui.Stack(3,label,value) : new StableReadingRow(label,value,S,M,90);
        }
        var dot = new Border { Width = 5, Height = 5, CornerRadius = new CornerRadius(3), Background = Palette.Brush(Accent), VerticalAlignment = VerticalAlignment.Center };
        var branch = new Border { Height = 1, Width = 10, Tag = "graphic", Background = Palette.Brush(P.Stroke), VerticalAlignment = VerticalAlignment.Center };
        var text = main ? Ui.Stack(3,label,value) : (Control)new StableReadingRow(label,value,S,M,90);
        return Ui.Columns("Auto,Auto,*",dot,branch,new Border { Padding = new Thickness(7,0,0,0), Child = text });
    }
}

public sealed class InstrumentScale : Control
{
    public bool Labels { get; init; }
    public bool Temperature { get; init; }
    public bool Fahrenheit { get; init; }
    private double? fraction;
    private Color accent, ink;
    public void Set(double? level, Color color, Color ticks) { fraction = level; accent = color; ink = ticks; InvalidateVisual(); }
    public override void Render(DrawingContext context)
    {
        double width = Bounds.Width; if (width < 2) return;
        double unit = Math.Clamp(Bounds.Height / 20,.4,3);
        double y = Math.Max(4,Bounds.Height * .38);
        var pen = new Pen(Palette.Brush(Palette.Alpha(ink,.65)),unit);
        context.DrawLine(pen,new Point(0,y),new Point(width,y));
        for (int i = 0; i <= 20; i++) context.DrawLine(pen,new Point(i * width / 20,y),new Point(i * width / 20,y + (i % 5 == 0 ? 6 : 3) * unit));
        if (Labels)
            for (int i = 0; i <= 5; i++)
            {
                double n = Temperature && Fahrenheit ? 32 + i * 36 : i * 20;
                var text = new FormattedText(n.ToString("0",L.Culture),L.Culture,FlowDirection.LeftToRight,new Typeface(Ui.Font),7 * unit,Palette.Brush(ink));
                context.DrawText(text,new Point(Math.Clamp(i * width / 5 - text.Width / 2,0,Math.Max(0,width-text.Width)),y+7*unit));
            }
        if (fraction is double f)
        {
            double x = Math.Clamp(f,0,1) * Math.Max(0,width - 8 * unit) + 4 * unit;
            var marker = new StreamGeometry(); using(var g = marker.Open()) { g.BeginFigure(new Point(x,y - unit),true); g.LineTo(new Point(x-4*unit,y-7*unit)); g.LineTo(new Point(x+4*unit,y-7*unit)); g.EndFigure(true); }
            context.DrawGeometry(Palette.Brush(accent),null,marker);
        }
    }
}

internal sealed class SplitFlapSurface : Border
{
    public SplitFlapSurface(TextBlock value, Palette palette)
    {
        Padding=new Thickness(6,7); Background=Palette.Brush(palette.DesignBack("#151512"));
        BorderBrush=Palette.Brush(palette.Stroke); BorderThickness=new Thickness(1); CornerRadius=new CornerRadius(3);
        Child=new Grid { Children = { new SplitFlapPlate(value,palette), value } };
    }
}

internal sealed class SplitFlapPlate : Control
{
    private readonly TextBlock value;
    private readonly Palette palette;
    public SplitFlapPlate(TextBlock value, Palette palette)
    {
        this.value=value; this.palette=palette; IsHitTestVisible=false;
        value.PropertyChanged += (_,e) => { if (e.Property.Name is "Text" or "FontSize" or "Bounds" or "LetterSpacing") InvalidateVisual(); };
    }
    public override void Render(DrawingContext context)
    {
        if (value.TextLayout.TextLines.Count != 1) return;
        var text=value.Text ?? "";
        double advance=new FormattedText("0",L.Culture,FlowDirection.LeftToRight,new Typeface(value.FontFamily),value.FontSize,Brushes.Transparent).Width + value.LetterSpacing;
        double x=value.Bounds.X + (value.Bounds.Width-value.TextLayout.WidthIncludingTrailingWhitespace)/2;
        double y=value.Bounds.Y, h=value.Bounds.Height;
        var stroke=new Pen(Palette.Brush(palette.Stroke),1);
        for(int i=0;i<text.Length;i++)
        {
            if (!char.IsDigit(text[i])) continue;
            double left=x+i*advance-1;
            if(left<0 || left+advance>Bounds.Width) continue;
            var rect=new Rect(left,y,Math.Max(1,advance-1),h);
            context.DrawRectangle(Palette.Brush(Palette.Mix(palette.Card,palette.Ink,.035)),stroke,rect,2,2);
            context.DrawLine(stroke,new Point(left,y+h/2),new Point(left+advance-1,y+h/2));
        }
    }
}
