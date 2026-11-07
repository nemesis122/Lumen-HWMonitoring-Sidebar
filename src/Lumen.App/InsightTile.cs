using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

// Complete reading registers share one data and layout contract.
public sealed class InsightTile : SummaryTile
{
    public static readonly WidgetStyle[] ColorStyles={WidgetStyle.ClarityColor,WidgetStyle.ClarityRows,WidgetStyle.ClarityTiles};
    public static readonly WidgetStyle[] Styles={WidgetStyle.Clarity,WidgetStyle.ControlDeck,WidgetStyle.ClarityColor,WidgetStyle.ClarityRows,WidgetStyle.ClarityTiles};
    private readonly WidgetStyle style;
    private bool Colorful=>ColorStyles.Contains(style);
    private readonly bool focused;
    private readonly StackPanel body;
    private readonly List<Cell> cells=new();
    private readonly AreaChart firstPlot,secondPlot;
    private readonly Control firstLane,secondLane;
    private string shape="";
    public IReadOnlyList<string> RenderedKeys{get;private set;}=Array.Empty<string>();
    public sealed record Cell(string Key,TextBlock Label,TextBlock? Text,FocusReadout? Focus)
    {
        public string Value=>Focus?.ValueText??Text?.Text??"";
        public Control ValueControl=>(Control?)Focus??Text!;
    }
    public IReadOnlyList<Cell> Readouts=>cells;

    public InsightTile(WidgetKind kind,int index,Palette p,Metrics m,Settings s,WidgetStyle style):base(kind,index,p,m,s,false)
    {
        this.style=style;focused=style==WidgetStyle.ControlDeck;
        Padding=new Thickness(m.Pad,m.Pad*.6);
        CornerRadius=new CornerRadius(m.Radius*(focused?.75:.5));
        body=Ui.Stack(m.Gap*.6);body.Tag="elastic";
        foreach(var text in Head.GetLogicalDescendants().OfType<TextBlock>())
        {text.TextWrapping=TextWrapping.Wrap;text.TextTrimming=TextTrimming.None;}
        firstPlot=new AreaChart{Height=m.Chart*(focused?.42:.23),LineWidth=1.5,FillOpacity=focused?.1:0};
        secondPlot=new AreaChart{Height=firstPlot.Height,LineWidth=1.5,FillOpacity=firstPlot.FillOpacity};
        if(!focused)firstPlot.Tag=secondPlot.Tag="fixed";
        firstLane=Lane(firstPlot);secondLane=Lane(secondPlot);
        Child=Ui.Stack(m.Gap*.6,Head,body);Child.Tag="elastic";
    }

    private Control Lane(AreaChart chart)
    {
        // The name never breaks inside a word ("Leistung"); both lanes share the width of the longer name.
        var label=Text("",9.5,P.Soft);label.MinWidth=focused?42:36;label.TextWrapping=TextWrapping.NoWrap;label.Margin=new Thickness(0,0,6,0);
        var lane=new Grid{ColumnDefinitions={new ColumnDefinition(GridLength.Auto){SharedSizeGroup="lane"},new ColumnDefinition(1,GridUnitType.Star)}};
        Grid.SetColumn(chart,1);lane.Children.Add(label);lane.Children.Add(chart);
        lane.Tag=label;return lane;
    }

    protected override void Show(Summary summary)
    {
        var rows=CompleteReadings.For(Kind,Index,Last.Snap,Last.History,S,P);
        RenderedKeys=rows.Select(r=>r.Key).ToArray();
        string next=string.Join("\n",rows.Select(r=>r.Key+"|"+r.Label));
        if(shape!=next||cells.Count!=rows.Count){shape=next;Build(rows);}
        for(int i=0;i<rows.Count;i++)
        {
            var cell=cells[i];
            if(cell.Focus!=null)cell.Focus.Set(rows[i].Value);
            else cell.Text!.Text=rows[i].Value;
            ToolTip.SetTip(cell.ValueControl,rows[i].Label+": "+rows[i].Value);
        }
        var pair=TwinFocusMetrics.Pair(Kind,Index,summary,Last.Snap,Last.History,S);
        SetLane(firstLane,firstPlot,pair.Primary,WidgetSensors.PrimaryKey(Kind),Colorful?ReadingColor(WidgetSensors.PrimaryKey(Kind)):P.Cool);
        SetLane(secondLane,secondPlot,pair.Secondary,WidgetSensors.SecondaryKey(Kind),Colorful?ReadingColor(WidgetSensors.SecondaryKey(Kind)):P.Calm);
        if(firstLane.Parent is Control chartGroup)chartGroup.IsVisible=firstLane.IsVisible||secondLane.IsVisible;
    }

    private void SetLane(Control lane,AreaChart chart,LiveMetric metric,string key,Color color)
    {
        bool visible=RenderedKeys.Contains(key)&&S.Shows(key+".history")&&metric.Series.Any(double.IsFinite)
            &&(Summaries.ChartKey(Kind) is not string group||S.Shows(group));
        lane.IsVisible=visible;
        ((TextBlock)lane.Tag!).Text=key=="net.down"?"DL":key=="net.up"?"UP":key.EndsWith(".temp")?"Temp":metric.Label;
        chart.Max=metric.Maximum;chart.Set(visible?metric.Series:Array.Empty<double>(),color);
    }

    private TextBlock Text(string text,double size,Color color,bool numeric=false)
    {
        var block=Ui.Text(text,size,color,numeric?FontWeight.SemiBold:FontWeight.Normal,numeric);
        block.TextTrimming=TextTrimming.None;block.TextWrapping=TextWrapping.Wrap;return block;
    }

    private static bool IsMetadata(CompleteReading row)=>row.Address||row.Key.EndsWith(":model")||row.Key is
        "system.os" or "system.boot" or "clock.date" or "calendar.date" or "weather.place" or "weather.sky" or "media.title" or "media.artist" or "media.source" or "fps.app";

    private bool Focuses(CompleteReading row)
    {
        if(!focused||IsMetadata(row)||row.Value.Length==0)return false;
        return (row.Key==WidgetSensors.PrimaryKey(Kind)||row.Key==WidgetSensors.SecondaryKey(Kind))
            &&(char.IsDigit(row.Value[0])||row.Value[0] is '—' or '-' or '+' or '−');
    }

    private (string Key,string Title,string Label) Group(CompleteReading row)
    {
        int separator=row.Label.LastIndexOf(" · ",StringComparison.Ordinal);
        string prefix=separator>0?row.Label[..separator]:row.Label;
        string suffix=separator>0?row.Label[(separator+3)..]:row.Label;
        if(row.Key.StartsWith("drive:")||row.Key.StartsWith("volume:")||row.Key.StartsWith("process:")||row.Key.StartsWith("world:"))
            return (row.Key[..row.Key.LastIndexOf(':')],prefix,Short(suffix));
        if(row.Key.StartsWith("gpu.vram."))return ("vram","VRAM",L.T(row.Key.EndsWith(".used")?"Used":"Total"));
        if(row.Key.StartsWith("fan:"))return (row.Key.EndsWith(":percent")?row.Key[..^8]:row.Key,prefix,row.Key.EndsWith(":percent")?L.T("Speed"):"RPM");
        if(row.Key.StartsWith("temp:"))return ("board",L.T("Board"),row.Label);
        return ("metrics","",Short(row.Label));
    }
    private static string Short(string label)=>label switch
    {"Temperature"=>"Temp","Clock speed"=>"Clock","Available"=>"Avail.","Installed"=>"Total","2-min peak"=>"Peak 2m","DL peak · 2 min"=>"DL max","Link speed"=>"Link","Link use"=>"Traffic",_=>label};

    // Category colours identify related readings; they do not encode alarm thresholds.
    public Color ReadingColor(string key)
    {
        if(S.Colors=="mono")return P.Ink;
        int category=key.Contains("peak")?3:key.Contains("power")||key.Contains("voltage")||key.Contains("total")||key.Contains("installed")?2:
            key.Contains("clock")||key.EndsWith(".freq")||key.Contains("speed")||key.Contains("link")?3:
            key.Contains("load")||key.Contains("used")||key.Contains("fan")||key.Contains("write")||key=="net.up"?1:0;
        Color accent=category==3?Palette.Mix(P.Cool,P.Ramp[3],.5):P.Ramp[category];
        // Check both the card and the faint tinted row surface, including light themes.
        for(int step=0;step<48&&(Contrast(accent,P.Card)<4.6||Contrast(accent,Palette.Mix(P.Card,accent,.07))<4.6);step++)accent=Palette.Mix(accent,P.Ink,.12);
        return Color.FromRgb(accent.R,accent.G,accent.B);
    }
    public static double Contrast(Color a,Color b)
    {
        double Lum(Color c){double Lin(byte n){double v=n/255d;return v<=.04045?v/12.92:Math.Pow((v+.055)/1.055,2.4);}return .2126*Lin(c.R)+.7152*Lin(c.G)+.0722*Lin(c.B);}
        double x=Lum(a),y=Lum(b);return (Math.Max(x,y)+.05)/(Math.Min(x,y)+.05);
    }

    private Control ReadingCell(TextBlock label,TextBlock value,Color accent)
    {
        if(style==WidgetStyle.ClarityTiles){
            value.TextAlignment=TextAlignment.Right;
            return new Border{Background=Palette.Brush(Palette.Alpha(accent,.06*P.Card.A/255d)),BorderBrush=Palette.Brush(accent),BorderThickness=new Thickness(2,0,0,0),CornerRadius=new CornerRadius(3),Padding=new Thickness(M.Gap*.6,M.Gap*.3),Margin=new Thickness(0,0,M.Gap*.5,M.Gap*.4),Child=Ui.Stack(1,label,value)};
        }
        var line=new StableReadingRow(label,value,S,M,Colorful&&Ui.FontKey!="mono"?74:82){Margin=new Thickness(0,0,M.Gap*.7,M.Gap*.3)};
        if(!Colorful)return line;
        return new Border{BorderBrush=Palette.Brush(accent),BorderThickness=new Thickness(2,0,0,0),Padding=new Thickness(M.Gap*.45,style==WidgetStyle.ClarityRows?M.Gap*.2:0,0,0),Background=style==WidgetStyle.ClarityRows?Palette.Brush(Palette.Alpha(accent,.06*P.Card.A/255d)):Brushes.Transparent,Margin=new Thickness(0,0,M.Gap*.3,style==WidgetStyle.ClarityRows?M.Gap*.2:0),Child=line};
    }

    private void Build(List<CompleteReading> rows)
    {
        body.Children.Clear();cells.Clear();
        var primary=new List<Control>();
        var groups=new Dictionary<string,(string Title,List<Control> Metrics,List<Control> Meta)>();
        var addresses=new List<Control>();
        foreach(var row in rows)
        {
            var group=Group(row);
            var label=Text(group.Label,M.Subtitles?10.5:10,P.Soft);
            if(Focuses(row))
            {
                var readout=new FocusReadout(M.Hero*.7,P,S){Tag="insight-value:"+row.Key};readout.Set(row.Value);
                var block=Ui.Stack(M.Gap*.25,label,readout);block.Margin=new Thickness(0,0,M.Gap*.8,0);
                primary.Add(block);cells.Add(new(row.Key,label,null,readout));continue;
            }
            bool metadata=IsMetadata(row);
            Color accent=Colorful?ReadingColor(row.Key):P.Ink;
            var value=Text(row.Value,metadata?(M.Subtitles?11.5:11):(M.Subtitles?13:12),metadata?P.Soft:Colorful&&style!=WidgetStyle.ClarityRows?accent:P.Ink,numeric:!row.Key.EndsWith(":model"));
            value.Tag="insight-value:"+row.Key;
            cells.Add(new(row.Key,label,value,null));
            if(row.Address)
            {
                label.Text=row.Label;
                addresses.Add(Ui.Stack(1,label,value));continue;
            }
            if(!groups.TryGetValue(group.Key,out var section))
                groups[group.Key]=section=(group.Title,new(),new());
            if(metadata)
            {
                section.Meta.Add(row.Key.EndsWith(":model")?value:Ui.Stack(1,label,value));
            }
            else
            {
                section.Metrics.Add(ReadingCell(label,value,accent));
            }
        }
        var charts=Ui.Stack(M.Gap*.25,firstLane,secondLane);
        Grid.SetIsSharedSizeScope(charts,true);
        if(primary.Count>0)
            body.Children.Add(new StatPanel(Array.Empty<double>(),primary.ToArray()){Tag="elastic",MaxColumns=2,MinimumColumnWidth=122});
        if(focused)body.Children.Add(charts);
        bool separate=primary.Count>0;
        foreach(var section in groups.Values)
        {
            var content=Ui.Stack(M.Gap*.3);
            if(separate&&section.Title.Length>0)content.Children.Add(new Border{Height=1,Background=Palette.Brush(P.Stroke),Margin=new Thickness(0,M.Gap*.2)});
            if(section.Title.Length>0)content.Children.Add(Text(section.Title,10,P.Cool));
            foreach(var meta in section.Meta)content.Children.Add(meta);
            if(section.Metrics.Count>0)content.Children.Add(new StatPanel(Array.Empty<double>(),section.Metrics.ToArray()){Tag="elastic",MaxColumns=6,MinimumColumnWidth=style==WidgetStyle.ClarityRows?240:style==WidgetStyle.ClarityTiles?112:M.Subtitles?132:128});
            body.Children.Add(content);separate=true;
        }
        if(!focused)body.Children.Add(charts);
        if(addresses.Count>0)body.Children.Add(Ui.Stack(M.Gap*.4,addresses.ToArray()));
    }
}

// Stable reserved value space prevents changing digits and units from moving columns.
internal sealed class StableReadingRow : Panel
{
    private readonly TextBlock label,value;
    private readonly Settings settings;
    private readonly Metrics metrics;
    private readonly double reservedWidth;
    private double valueWidth,widest;
    public StableReadingRow(TextBlock label,TextBlock value,Settings settings,Metrics metrics,double reservedWidth=82)
    {this.label=label;this.value=value;this.settings=settings;this.metrics=metrics;this.reservedWidth=reservedWidth;Children.Add(label);Children.Add(value);}
    protected override Size MeasureOverride(Size available)
    {
        double w=double.IsFinite(available.Width)?available.Width:132;
        // A value wider than the reserved space ("1392 U/min") would wrap onto a second line and the
        // row would grow and shrink with the reading; the space instead grows to the widest value seen.
        value.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));
        widest=Math.Max(widest,Math.Ceiling(value.DesiredSize.Width));
        // The name never breaks inside a word ("Frametim|e"): it gets at least its longest word, and when
        // name and value do not fit side by side the value goes under the name (and stays there, so the
        // row does not switch back and forth).
        label.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));
        double word=LongestWord();
        double gap=metrics.Gap*.4;
        if(stacked&&Math.Abs(w-stackedAt)>1)stacked=false;
        if(!stacked&&word+gap+widest>w){stacked=true;stackedAt=w;}
        if(stacked)
        {
            valueWidth=w;
            label.Measure(new Size(w,double.PositiveInfinity));value.Measure(new Size(w,double.PositiveInfinity));
            return new Size(w,label.DesiredSize.Height+value.DesiredSize.Height);
        }
        double reserved=reservedWidth*(settings.ActiveWidget?.Values??1);
        valueWidth=Math.Min(w*.67,Math.Max(reserved,widest));
        valueWidth=Math.Max(widest,Math.Min(valueWidth,w-word-gap));
        double labelWidth=Math.Max(1,w-valueWidth-gap);
        label.Measure(new Size(labelWidth,double.PositiveInfinity));value.Measure(new Size(valueWidth,double.PositiveInfinity));
        return new Size(w,Math.Max(label.DesiredSize.Height,value.DesiredSize.Height));
    }
    private bool stacked;
    private double stackedAt;
    private double LongestWord()
    {
        string text=label.Text??"";
        if(text.Length==0)return 0;
        var probe=new TextBlock{FontSize=label.FontSize,FontFamily=label.FontFamily,FontWeight=label.FontWeight,LetterSpacing=label.LetterSpacing};
        double longest=0;
        foreach(string part in text.Split(' ',StringSplitOptions.RemoveEmptyEntries))
        {probe.Text=part;probe.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));longest=Math.Max(longest,Math.Ceiling(probe.DesiredSize.Width));}
        return longest;
    }
    protected override Size ArrangeOverride(Size final)
    {
        value.TextAlignment=TextAlignment.Right;
        if(stacked)
        {
            double h=label.DesiredSize.Height;
            label.Arrange(new Rect(0,0,final.Width,h));
            value.Arrange(new Rect(0,h,final.Width,Math.Max(0,final.Height-h)));return final;
        }
        label.Arrange(new Rect(0,0,Math.Max(0,final.Width-valueWidth-metrics.Gap*.4),final.Height));
        value.Arrange(new Rect(final.Width-valueWidth,0,valueWidth,final.Height));return final;
    }
}

public sealed class FocusReadout : Panel
{
    private readonly TextBlock digits,unit;
    private readonly Settings settings;
    private bool wrapped;
    private double numberWidth;
    private const double Gap=3;
    public string ValueText{get;private set;}="";
    public FocusReadout(double size,Palette palette,Settings settings)
    {
        this.settings=settings;
        digits=Ui.Text("",size,palette.Ink,FontWeight.Bold,true);
        unit=Ui.Text("",Math.Max(11,size*.42),palette.Soft,FontWeight.Medium,true);
        digits.TextTrimming=unit.TextTrimming=TextTrimming.None;
        digits.TextWrapping=unit.TextWrapping=TextWrapping.Wrap;
        Children.Add(digits);Children.Add(unit);
    }
    public void Set(string text)
    {
        ValueText=text;
        var split=text.Contains(':')?(text,""):text.StartsWith('—')?("—",text[1..].Trim()):TwinFocusMetrics.Split(text);
        digits.Text=split.Item1;unit.Text=split.Item2;unit.IsVisible=unit.Text.Length>0;
    }
    protected override Size MeasureOverride(Size available)
    {
        double w=double.IsFinite(available.Width)?available.Width:140;
        // Choose the layout from font sizes and available space, never from live digits.
        double unitWidth=unit.FontSize*4;
        wrapped=unit.IsVisible&&w<digits.FontSize*3.1+unitWidth+Gap;
        numberWidth=unit.IsVisible&&!wrapped?Math.Max(1,w-unitWidth-Gap):w;
        digits.TextAlignment=unit.IsVisible&&!wrapped?TextAlignment.Right:TextAlignment.Left;
        digits.Measure(new Size(numberWidth,double.PositiveInfinity));unit.Measure(new Size(wrapped?w:unitWidth,double.PositiveInfinity));
        return new Size(w,wrapped?digits.DesiredSize.Height+unit.DesiredSize.Height+Gap:Math.Max(digits.DesiredSize.Height,unit.DesiredSize.Height));
    }
    protected override Size ArrangeOverride(Size final)
    {
        digits.Arrange(new Rect(0,0,Math.Min(final.Width,numberWidth),digits.DesiredSize.Height));
        if(wrapped)unit.Arrange(new Rect(0,digits.DesiredSize.Height+Gap,final.Width,unit.DesiredSize.Height));
        else unit.Arrange(new Rect(Math.Min(final.Width,numberWidth+Gap),Math.Max(0,digits.DesiredSize.Height-unit.DesiredSize.Height-2*(settings.ActiveWidget?.Values??1)),Math.Max(0,final.Width-numberWidth-Gap),unit.DesiredSize.Height));
        return final;
    }
}
