using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed class ComparisonTile : SummaryTile
{
    public static readonly WidgetStyle[] Styles = { WidgetStyle.Duplex, WidgetStyle.SignalRows, WidgetStyle.OpenPanel };
    private readonly WidgetStyle style;
    private readonly StackPanel body;
    private readonly List<(TextBlock Label,TextBlock Value)> cells = new();
    private readonly Dictionary<string,AreaChart> charts = new();
    private string shape = "";
    private readonly double gap;
    private readonly DriveRows? drives;
    public IReadOnlyList<string> RenderedKeys { get; private set; } = Array.Empty<string>();

    public ComparisonTile(WidgetKind kind,int index,Palette p,Metrics m,Settings s,WidgetStyle style) : base(kind,index,p,m,s,false)
    {
        this.style=style;
        gap=m.Gap*.4;
        body=Ui.Stack(gap);body.Tag="elastic";
        Padding=new Thickness(m.Pad,m.Pad*.6);
        CornerRadius=new CornerRadius(style==WidgetStyle.Duplex?m.Radius*.65:style==WidgetStyle.SignalRows?m.Radius*.3:0);
        BorderThickness=new Thickness(style==WidgetStyle.SignalRows?1:0);
        if(style==WidgetStyle.OpenPanel)Background=Brushes.Transparent;
        foreach(var text in Head.GetLogicalDescendants().OfType<TextBlock>())
        {text.TextTrimming=TextTrimming.None;text.TextWrapping=TextWrapping.Wrap;}
        if(style==WidgetStyle.Duplex&&kind==WidgetKind.Drives)
        {
            // Duplex drives: one line per drive (letter, bar, temperature), then reading and writing.
            drives=new DriveRows(m.Gap*.6,10.5,M.Subtitles?13:12,p.Soft,()=>new FillBack{Height=6,Radius=3,Track=Palette.Mix(p.Card,p.Ink,p.Dark?.1:.08)},
                (c,r)=>((FillBack)c).Animate(r.Fraction,r.Color),r=>r.Color);
            Child=Ui.Stack(gap*1.5,Head,drives,Extras);Child.Tag="elastic";
            return;
        }
        Child=Ui.Stack(gap*1.5,Head,body);Child.Tag="elastic";
    }

    protected override void Show(Summary sum)
    {
        if(drives!=null){drives.Show(DriveRows.Of(Index,Last.Snap,Last.History,S,P));RenderedKeys=Array.Empty<string>();return;}
        var data=CompleteReadings.For(Kind,Index,Last.Snap,Last.History,S,P);
        RenderedKeys=data.Select(r=>r.Key).ToArray();
        string next=string.Join("\n",RenderedKeys);
        if(shape!=next || cells.Count!=data.Count){shape=next;Build(data);}
        for(int i=0;i<data.Count;i++){cells[i].Label.Text=DisplayLabel(data[i]);cells[i].Value.Text=data[i].Value;}
        var pair=TwinFocusMetrics.Pair(Kind,Index,sum,Last.Snap,Last.History,S);
        UpdateChart(WidgetSensors.PrimaryKey(Kind),pair.Primary,P.Cool);
        UpdateChart(WidgetSensors.SecondaryKey(Kind),pair.Secondary,P.Calm);
    }

    private void UpdateChart(string key,LiveMetric metric,Color color)
    {
        if(!charts.TryGetValue(key,out var plot))return;
        bool visible=S.Shows(key)&&S.Shows(key+".history")&&metric.Series.Any(double.IsFinite);
        plot.IsVisible=visible;plot.Max=metric.Maximum;plot.Set(visible?metric.Series:Array.Empty<double>(),color);
    }

    private TextBlock Text(string text,double size,bool number=false,Color? color=null)
    {
        var block=Ui.Text(text,size,color??P.Ink,number?FontWeight.SemiBold:FontWeight.Normal,number);
        block.TextTrimming=TextTrimming.None;block.TextWrapping=TextWrapping.Wrap;return block;
    }

    private AreaChart Plot(string key,Color color)
    {
        var chart=new AreaChart{Height=M.Chart*(style==WidgetStyle.Duplex?.45:.35),Color=color,FillOpacity=.08,LineWidth=1.4};
        // Parallel charts retain their geometry; elastic reading rows absorb free height.
        chart.Tag="fixed";
        charts[key]=chart;return chart;
    }

    private Control Aligned(TextBlock label,TextBlock value)
    {
        // Name left, value right; neither breaks inside a word ("Cyberpunk2|077"): when both do not fit
        // side by side the value goes under the name.
        value.TextAlignment=TextAlignment.Right;
        return new StableReadingRow(label,value,S,M,0);
    }

    private Control Details(TextBlock label,TextBlock value)
    {
        if(!M.Subtitles || style==WidgetStyle.SignalRows)return Aligned(label,value);
        return Ui.Stack(gap*.4,label,value);
    }

    private string DisplayLabel(CompleteReading row)
    {
        if(Kind!=WidgetKind.Drives||row.Key.EndsWith(":model"))return row.Label;
        string label=row.Label.Contains(" · ")?row.Label[(row.Label.IndexOf(" · ")+3)..]:row.Label;
        return label=="Temperature"?"Temp":label;
    }

    private static StackPanel Elastic(StackPanel panel){panel.Tag="elastic";return panel;}
    private void Build(List<CompleteReading> data)
    {
        body.Children.Clear();cells.Clear();charts.Clear();
        var primaryKey=WidgetSensors.PrimaryKey(Kind);var secondaryKey=WidgetSensors.SecondaryKey(Kind);
        var primary=new List<Control>();var other=new List<Control>();var addresses=new List<Control>();
        var drives=new Dictionary<string,List<(Control Block,bool Model)>>();
        foreach(var row in data)
        {
            bool main=row.Key==primaryKey||row.Key==secondaryKey;
            bool first=row.Key==primaryKey;
            var label=Text(DisplayLabel(row),M.Subtitles?10.5:10,false,P.Soft);
            double mainSize=style==WidgetStyle.OpenPanel?(M.Subtitles?14:12.5):M.Hero*(Kind==WidgetKind.Network?.45:.5);
            var value=Text(row.Value,main?mainSize:M.Subtitles?12.5:11.5,true,main?(first?P.Cool:P.Calm):P.Ink);
            value.Tag="complete-reading:"+row.Key;
            cells.Add((label,value));
            Control block;
            if(main)
            {
                var chart=Plot(row.Key,first?P.Cool:P.Calm);
                if(style==WidgetStyle.SignalRows)
                    block=new SignalMetricPanel(label,chart,value,S,M);
                else if(style==WidgetStyle.OpenPanel)
                    block=Ui.Stack(gap*.4,Aligned(label,value),chart);
                else
                {
                    block=Ui.Stack(gap*.4,label,value,chart);
                }
                primary.Add(block);
            }
            else if(Kind==WidgetKind.Drives&&row.Key.StartsWith("drive:"))
            {
                bool model=row.Key.EndsWith(":model");
                block=model?Ui.Stack(gap*.4,label,value):Details(label,value);
                block.Margin=new Thickness(0,gap*.5,gap,0);
                string disk=row.Key[..row.Key.LastIndexOf(':')];
                if(!drives.TryGetValue(disk,out var group))drives[disk]=group=new();
                group.Add((block,model));
            }
            else if(row.Address)
            {block=Ui.Stack(gap*.4,label,value);addresses.Add(block);}
            else if(style==WidgetStyle.OpenPanel)
            {block=Aligned(label,value);other.Add(block);}
            else
            {block=Details(label,value);block.Margin=new Thickness(0,gap*.5,gap,0);other.Add(block);}
        }
        if(primary.Count>0)
        {
            if(style==WidgetStyle.Duplex)
            {
                foreach(var control in primary)control.Margin=new Thickness(0,0,gap,0);
                body.Children.Add(new StatPanel(Array.Empty<double>(),primary.ToArray()){Tag="elastic",MaxColumns=2,MinimumColumnWidth=Kind==WidgetKind.Network?116:102});
            }
            else {var main=Ui.Stack(gap,primary.ToArray());main.Tag="elastic";body.Children.Add(main);}
        }
        if(other.Count>0)
            body.Children.Add(style==WidgetStyle.OpenPanel?Elastic(Ui.Stack(gap,other.ToArray())):new StatPanel(Array.Empty<double>(),other.ToArray()){Tag="elastic",MaxColumns=6,MinimumColumnWidth=style==WidgetStyle.SignalRows||!M.Subtitles?132:90});
        foreach(var disk in drives.Values)
        {
            var group=Ui.Stack(gap);
            foreach(var model in disk.Where(x=>x.Model))group.Children.Add(model.Block);
            group.Children.Add(new StatPanel(Array.Empty<double>(),disk.Where(x=>!x.Model).Select(x=>x.Block).ToArray()){Tag="elastic",MaxColumns=6,MinimumColumnWidth=style==WidgetStyle.SignalRows||!M.Subtitles?132:90});
            body.Children.Add(group);
        }
        if(addresses.Count>0)body.Children.Add(new StatPanel(Array.Empty<double>(),addresses.ToArray()){Tag="elastic",MaxColumns=2,MinimumColumnWidth=160});
    }
}

internal sealed class SignalMetricPanel : Panel
{
    private readonly TextBlock label,value;
    private readonly AreaChart plot;
    private readonly Settings settings;
    private readonly Metrics metrics;
    private bool stacked;
    private double labelWidth,valueWidth,gap;
    public SignalMetricPanel(TextBlock label,AreaChart plot,TextBlock value,Settings settings,Metrics metrics)
    {this.label=label;this.plot=plot;this.value=value;this.settings=settings;this.metrics=metrics;Children.AddRange(new Control[]{label,plot,value});}
    protected override Size MeasureOverride(Size available)
    {
        double width=double.IsFinite(available.Width)?available.Width:280;
        gap=metrics.Gap*.5;
        labelWidth=44*(settings.ActiveWidget?.Labels??1);
        valueWidth=105*(settings.ActiveWidget?.Values??1);
        stacked=width<labelWidth+valueWidth+metrics.Chart;
        label.Measure(new Size(stacked?width:labelWidth,double.PositiveInfinity));
        value.Measure(new Size(stacked?width:valueWidth,double.PositiveInfinity));
        plot.Measure(new Size(stacked?width:Math.Max(0,width-labelWidth-valueWidth-gap*2),double.PositiveInfinity));
        return new Size(width,stacked?label.DesiredSize.Height+value.DesiredSize.Height+(plot.IsVisible?plot.DesiredSize.Height+gap*2:gap):Math.Max(Math.Max(label.DesiredSize.Height,value.DesiredSize.Height),plot.DesiredSize.Height));
    }
    protected override Size ArrangeOverride(Size final)
    {
        if(stacked)
        {
            value.TextAlignment=TextAlignment.Left;double y=label.DesiredSize.Height+gap;
            label.Arrange(new Rect(0,0,final.Width,label.DesiredSize.Height));value.Arrange(new Rect(0,y,final.Width,value.DesiredSize.Height));
            y+=value.DesiredSize.Height+gap;plot.Arrange(new Rect(0,y,final.Width,plot.IsVisible?plot.DesiredSize.Height:0));
        }
        else
        {
            label.Arrange(new Rect(0,0,labelWidth,final.Height));plot.Arrange(new Rect(labelWidth+gap,0,Math.Max(0,final.Width-labelWidth-valueWidth-gap*2),final.Height));
            value.TextAlignment=TextAlignment.Right;value.Arrange(new Rect(final.Width-valueWidth,0,valueWidth,final.Height));
        }
        return final;
    }
}
