using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Lumen.App;
using Lumen.Core;
using Xunit;

public class ColourAssignmentTests
{
    [Fact]
    public void Memory_used_gigabytes_bar_matches_load_colour_and_keeps_sensor_override()=>UiHost.Run(()=>
    {
        var s=Settings.Starter();var slot=s.Widgets.Single(w=>w.Kind==WidgetKind.Memory);slot.Style=WidgetStyle.Table;
        slot.HiddenSensors.Clear();var p=Palette.For(true,false);var(snapshot,history)=Render.Demo();
        var card=Cards.Create(slot.Kind,slot.Style,0,p,Metrics.For(Density.Comfortable),s.ForSlot(slot));
        ((Lumen.App.INamed)card).Own(slot,0);
        foreach(var used in new[]{10d,60d,95d})
        {
            var data=snapshot with{Memory=new MemoryInfo(used,100)};((IWidget)card).Update(data,history);TextColours.Apply(card,s);
            var bars=card.GetLogicalDescendants().OfType<Meter>().ToArray();
            var gb=Assert.Single(bars,b=>b.GetValue(TextColours.SensorProperty)=="mem.used");
            var expected=p.At(Format.Share(data.Memory.Load)*.9);Assert.Equal(expected,gb.Color);
            Assert.Contains(bars,b=>b!=gb&&b.Color==expected);
            slot.Colours=new(){InheritGlobal=false,GraphColor="#D946EF"};TextColours.Apply(card,s);Assert.Equal(Color.Parse("#D946EF"),gb.Tint);
            slot.SensorColours["mem.used"]=new(){GraphColor="#FDE047"};TextColours.Apply(card,s);Assert.Equal(Color.Parse("#FDE047"),gb.Tint);
            slot.SensorColours.Clear();slot.Colours=new();((IWidget)card).Update(data,history);TextColours.Apply(card,s);Assert.Equal(expected,gb.Color);
        }
        return true;
    });
    [Fact]
    public void Editable_notes_take_global_and_widget_value_colours_without_changing_text()=>UiHost.Run(()=>
    {
        var s=Settings.Starter();s.Notes="Keep this note";s.ValueColor="#10B981";var notes=s.Widgets.Single(w=>w.Kind==WidgetKind.Notes);
        var card=Cards.Create(WidgetKind.Notes,WidgetStyle.Standard,0,Palette.For(true,false),Metrics.For(Density.Comfortable),s.ForSlot(notes));
        var input=Assert.Single(card.GetLogicalDescendants().OfType<TextBox>());var original=((ISolidColorBrush)input.Foreground!).Color;
        TextColours.Apply(card,s);Assert.Equal(Color.Parse("#10B981"),((ISolidColorBrush)input.Foreground!).Color);
        notes.Colours=new(){InheritGlobal=false,ValueColor="#F97316"};TextColours.Apply(card,s);Assert.Equal(Color.Parse("#F97316"),((ISolidColorBrush)input.Foreground!).Color);
        notes.Colours=new();TextColours.Apply(card,s);Assert.Equal(Color.Parse("#10B981"),((ISolidColorBrush)input.Foreground!).Color);
        s.ValueColor="";TextColours.Apply(card,s);Assert.Equal(original,((ISolidColorBrush)input.Foreground!).Color);Assert.Equal("Keep this note",input.Text);
        return true;
    });
    [Fact]
    public void Render_panel_ram_reproduction()=>UiHost.Run(()=>
    {
        var path=Environment.GetEnvironmentVariable("LUMEN_RAM_COLOUR_OUTPUT");if(string.IsNullOrEmpty(path))return true;
        var s=Settings.Starter();DesignWidgetSelection.Apply(s,WidgetStyle.AioPanelHeader);AioDesigns.ShowAllParts(s);
        var memory=s.Widgets.Single(w=>w.Kind==WidgetKind.Memory);memory.Colours=new(){InheritGlobal=false,TitleColor="#F97316",LabelColor="#F97316",ValueColor="#10B981",GraphColor=Graph};
        var overview=s.Widgets.Single(w=>w.Kind==WidgetKind.Overview);var card=Cards.Create(WidgetKind.Overview,WidgetStyle.AioPanelHeader,0,Palette.For(true,false),Metrics.For(Density.Comfortable),s.ForSlot(overview));
        ((Lumen.App.INamed)card).Own(overview,0);var (snapshot,history)=Render.Demo();((IWidget)card).Update(snapshot,history);TextColours.Apply(card,s);
        var window=new Window{Width=440,Height=1500,Content=card,Background=Brushes.Black};
        try
        {
            window.Show();Dispatcher.UIThread.RunJobs();using var image=window.CaptureRenderedFrame();Assert.NotNull(image);Directory.CreateDirectory(path);image.Save(System.IO.Path.Combine(path,"Panel-Farbzuweisung-Fix.png"));
            var ram=card.GetLogicalDescendants().OfType<PanelBar>().First(b=>!b.Leds&&b.GetValue(TextColours.SensorProperty)=="aio.system.ram");
            Assert.True(ColouredPixels(ram)>0);
            var frame=ram.GetLogicalAncestors().OfType<Border>().First(b=>b.GetLogicalDescendants().OfType<TextBlock>().Any(t=>t.Text=="RAM"));
            using var crop=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(frame.Bounds.Width),(int)Math.Ceiling(frame.Bounds.Height)),new Vector(96,96));crop.Render(frame);crop.Save(System.IO.Path.Combine(path,"RAM-Farbzuweisung-Fix.png"));
        }
        finally{window.Close();}
        return true;
    });
    [Fact]
    public void History_and_bar_picker_overrides_inherit_then_yield_to_reading_override()
    {
        var s=Settings.Starter();var cpu=s.Widgets.Single(w=>w.Kind==WidgetKind.Cpu);
        cpu.SensorColours["cpu.chart"]=new(){GraphColor="#D946EF"};
        Assert.Equal(Graph,ColourRules.Resolve(s,cpu,"cpu.load",ColourRole.Graph).Hex);
        cpu.SensorColours["cpu.load.history"]=new(){GraphColor="#F97316"};
        Assert.Equal("#F97316",ColourRules.Resolve(s,cpu,"cpu.load",ColourRole.Graph).Hex);
        cpu.SensorColours["cpu.load"]=new(){GraphColor="#22D3EE"};
        Assert.Equal("#22D3EE",ColourRules.Resolve(s,cpu,"cpu.load",ColourRole.Graph).Hex);
        Assert.Equal("",ColourRules.Resolve(s,cpu,"cpu.load",ColourRole.Value).Hex);
    }
    [Theory]
    [InlineData("dots")][InlineData("segments")][InlineData("hex")][InlineData("rays")][InlineData("diamonds")]
    [InlineData("strip")][InlineData("grid")][InlineData("leds")][InlineData("columns")][InlineData("tubes")][InlineData("tiles")]
    public void Full_gauges_keep_the_selected_graph_colour(string mode)=>UiHost.Run(()=>
    {
        var s=Settings.Starter();var cpu=s.Widgets.Single(w=>w.Kind==WidgetKind.Cpu);cpu.Colours=new(){InheritGlobal=false,GraphColor=Graph,AutomaticWarningColors=false};
        var gauge=TextColours.Bind(new PanelGauge{Mode=mode,Width=120,Height=120},"cpu.load");gauge.Set(.99);
        var root=new Border{Child=gauge};TextColours.Own(root,cpu);root.Measure(new Size(120,120));root.Arrange(new Rect(0,0,120,120));
        TextColours.Apply(root,s);Assert.True(ColouredPixels(gauge)>10);
        return true;
    });
    [Fact]
    public void Two_series_and_two_widgets_keep_independent_colours()=>UiHost.Run(()=>
    {
        var s=Settings.Starter();var cpu=s.Widgets.Single(w=>w.Kind==WidgetKind.Cpu);var memory=s.Widgets.Single(w=>w.Kind==WidgetKind.Memory);
        var original=PanelTheme.For("red");cpu.Colours=new(){InheritGlobal=false,GraphColor=Graph};memory.Colours=new(){InheritGlobal=false,GraphColor="#22D3EE"};
        cpu.SensorColours["cpu.load"]=new(){GraphColor="#22D3EE"};
        var gauge=TextColours.Bind(new PanelGauge{Mode="twin",Theme=original,Width=120,Height=120},"cpu.temp","cpu.load");gauge.Set(.6,.4);
        var bar=TextColours.Bind(new PanelBar{Theme=original,Width=200,Height=10},"aio.system.ram");bar.Set(.6);
        var root=Ui.Stack(0,gauge,bar);TextColours.Own(root,s.Widgets.Single(w=>w.Kind==WidgetKind.Overview));root.Measure(new Size(200,150));root.Arrange(new Rect(0,0,200,150));
        TextColours.Apply(root,s);Assert.True(ColouredPixels(gauge)>10);Assert.True(ColouredPixels(gauge,true)>10);Assert.True(ColouredPixels(bar,true)>100);Assert.Equal(Color.Parse("#FF2238"),original.Accent);
        return true;
    });
    private const string Graph="#D946EF";
    private static string ReadingKey(TextBlock text)=>text.GetValue(TextColours.SensorProperty) is {Length:>0} key ? key : SensorDetails.KeyOf(text);
    private static int ColouredPixels(Control graphic,bool cyan=false,bool yellow=false)
    {
        using var image=new RenderTargetBitmap(new PixelSize(Math.Max(1,(int)Math.Ceiling(graphic.Bounds.Width)),Math.Max(1,(int)Math.Ceiling(graphic.Bounds.Height))),new Vector(96,96));
        image.Render(graphic);
        var bytes=new byte[image.PixelSize.Width*image.PixelSize.Height*4];var pin=GCHandle.Alloc(bytes,GCHandleType.Pinned);
        try{image.CopyPixels(new PixelRect(0,0,image.PixelSize.Width,image.PixelSize.Height),pin.AddrOfPinnedObject(),bytes.Length,image.PixelSize.Width*4);}
        finally{pin.Free();}
        int count=0;
        for(int i=0;i<bytes.Length;i+=4)
        {double b=bytes[i],g=bytes[i+1],r=bytes[i+2];if(yellow ? r>20&&g>r*.75&&g<r*1.1&&b<g*.9 : cyan ? b>20&&g>20&&r<g*.4&&b>g*.8 : r>20&&b>20&&g<r*.6&&b>r*.8&&b<r*1.4)count++;}
        return count;
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Panel_memory_bar_draws_widget_colour_then_sensor_colour_and_restores(bool leds)=>UiHost.Run(()=>
    {
        var s=Settings.Starter();var memory=s.Widgets.Single(w=>w.Kind==WidgetKind.Memory);
        memory.Colours=new(){InheritGlobal=false,GraphColor=Graph};
        var bar=TextColours.Bind(new PanelBar{Leds=leds,Width=200,Height=12},"aio.system.ram");bar.Set(.5);
        var root=new Border{Child=bar};TextColours.Own(root,memory);
        root.Measure(new Size(200,12));root.Arrange(new Rect(0,0,200,12));
        Assert.Equal(0,ColouredPixels(bar));TextColours.Apply(root,s);Assert.True(ColouredPixels(bar)>100);
        memory.SensorColours["aio.system.ram"]=new(){GraphColor="#22D3EE"};TextColours.Apply(root,s);Assert.Equal(0,ColouredPixels(bar));
        memory.SensorColours.Clear();memory.Colours=new();TextColours.Apply(root,s);Assert.Equal(0,ColouredPixels(bar));Assert.Null(bar.FillColor);
        return true;
    });
    public static IEnumerable<object[]> Designs=>Settings.Selectable.Select(s=>new object[]{s});
    private static bool Active(Control graphic)
    {
        // A single history sample has no drawable line segment yet.
        if(graphic is AreaChart area && area.Values.Count(double.IsFinite)<2)return false;
        static bool Positive(object? value)=>value switch{double d=>double.IsFinite(d)&&d>0, float f=>f>0, System.Collections.IEnumerable values when value is not string=>values.Cast<object?>().Any(v=>v is double d&&double.IsFinite(d)&&d>0),_=>false};
        static bool Data(string name)=>new[]{"value","fraction","first","second","load","shown","data","series","down","up","upper","lower","level"}.Any(k=>name.Contains(k,StringComparison.OrdinalIgnoreCase))&&!name.Contains("max",StringComparison.OrdinalIgnoreCase);
        return graphic.GetType().GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Any(f=>Data(f.Name)&&Positive(f.GetValue(graphic)))
            || graphic.GetType().GetProperties().Where(p=>p.GetIndexParameters().Length==0&&p.DeclaringType==graphic.GetType()&&Data(p.Name)).Any(p=>Positive(p.GetValue(graphic)));
    }
    [Theory,MemberData(nameof(Designs))]
    public void Assigned_colours_reach_rendered_graphs_and_all_text_roles(WidgetStyle style)=>UiHost.Run(()=>
    {
        var s=Settings.Starter();s.HiddenValues.Clear();s.AutomaticWarningColors=false;
        foreach(var slot in s.Widgets){slot.HiddenSensors.Clear();slot.Colours=new(){InheritGlobal=false,TitleColor="#F97316",LabelColor="#22D3EE",ValueColor="#10B981",GraphColor=Graph,AutomaticWarningColors=false};}
        var (snapshot,history)=Render.Demo();
        var kinds=Enum.GetValues<WidgetKind>().Where(k=>!AioDesigns.Contains(style)||k==WidgetKind.Overview||!AioDesigns.Switches.Contains(k)).ToArray();
        int checkedTexts=0,checkedValues=0,checkedGraphs=0;
        foreach(var kind in kinds)
        {
            var slot=s.Widgets.Single(w=>w.Kind==kind);var scoped=s.ForSlot(slot);
            var card=Cards.Create(kind,style,0,Palette.For(true,false),Metrics.For(Density.Comfortable),scoped);
            if(card is Lumen.App.INamed named)named.Own(slot,0);
            for(int tick=0;tick<3;tick++)((IWidget)card).Update(snapshot,history);TextColours.Apply(card,s);
            var window=new Window{Width=440,Height=2200,Content=card,Background=Brushes.Black};
            try
            {
                window.Show();Dispatcher.UIThread.RunJobs();using var frame=window.CaptureRenderedFrame();
                foreach(var text in card.GetLogicalDescendants().OfType<TextBlock>().Where(t=>!string.IsNullOrWhiteSpace(t.Text)&&t.Foreground is ISolidColorBrush))
                {
                    var wanted=TextFonts.RoleOf(text) switch{TextRole.Title=>"#F97316",TextRole.Value=>"#10B981",TextRole.Graph=>Graph,_=>"#22D3EE"};
                    var actual=((ISolidColorBrush)text.Foreground!).Color;
                    Assert.True(actual.R==Color.Parse(wanted).R&&actual.G==Color.Parse(wanted).G&&actual.B==Color.Parse(wanted).B,$"{style}/{kind}: {TextFonts.RoleOf(text)} '{text.Text}' is {actual}, expected {wanted}");
                    checkedTexts++;
                }
                if(kind==WidgetKind.Notes)foreach(var input in card.GetLogicalDescendants().OfType<TextBox>())Assert.Equal(Color.Parse("#10B981"),((ISolidColorBrush)input.Foreground!).Color);
                var boundValues=card.GetLogicalDescendants().OfType<TextBlock>().Where(t=>TextFonts.RoleOf(t)==TextRole.Value&&ReadingKey(t).Length>0&&!ReadingKey(t).Contains('|')).ToArray();
                foreach(var key in boundValues.Select(ReadingKey).Distinct())slot.SensorColours[key]=new(){ValueColor="#FFFFFF",LabelColor="#FDE047",AutomaticWarningColors=false};
                TextColours.Apply(card,s);
                foreach(var text in boundValues)Assert.True(text.Foreground is ISolidColorBrush c&&c.Color.R==255&&c.Color.G==255&&c.Color.B==255,$"{style}/{kind}/{ReadingKey(text)}: sensor value colour missing");
                checkedValues+=boundValues.Length;
                slot.SensorColours.Clear();TextColours.Apply(card,s);
                foreach(var graphic in card.GetLogicalDescendants().OfType<Control>().Where(c=>c is IColourGraph&&c.IsVisible&&c.Bounds.Width>4&&c.Bounds.Height>2&&Active(c)))
                {
                    Assert.True(ColouredPixels(graphic)>0,$"{style}/{kind}/{graphic.GetType().Name}: active graphic did not draw assigned {Graph}; bounds={graphic.Bounds}; {(graphic is AreaChart area ? $"values={area.Values.Length}, color={area.Color}" : "")}");
                    string key=(string)typeof(TextColours).GetMethod("Key",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object[]{graphic,card,true})!;
                    Assert.True(key.Length>0,$"{style}/{kind}/{graphic.GetType().Name}: sensor binding missing");
                    slot.SensorColours[key]=new(){GraphColor="#FDE047",AutomaticWarningColors=false};TextColours.Apply(card,s);
                    Assert.True(ColouredPixels(graphic,yellow:true)>0,$"{style}/{kind}/{graphic.GetType().Name}/{key}: sensor graph override was not rendered");
                    slot.SensorColours.Remove(key);TextColours.Apply(card,s);Assert.True(ColouredPixels(graphic)>0,$"{style}/{kind}/{key}: widget colour was not restored");
                    checkedGraphs++;
                }
            }
            finally{window.Close();}
        }
        if(Environment.GetEnvironmentVariable("LUMEN_COLOUR_AUDIT") is {} audit)File.AppendAllText(audit,$"{style},{kinds.Length},{checkedTexts},{checkedValues},{checkedGraphs}\n");
        return true;
    });
}
