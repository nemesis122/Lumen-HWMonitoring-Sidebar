using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Lumen.App;
using Lumen.Core;
using Xunit;

public class ColourOverrideTests
{
    [Fact]
    public void Network_series_keep_separate_colours_and_restore_without_changing_other_widgets()=>UiHost.Run(()=>
    {
        var s=Settings.Starter();var network=s.Widgets.Single(w=>w.Kind==WidgetKind.Network);
        network.HiddenSensors.Clear();s.HiddenValues.Clear();
        network.SensorColours["net.down"]=new(){GraphColor="#34D399"};network.SensorColours["net.up"]=new(){GraphColor="#A78BFA"};
        var card=Cards.Create(WidgetKind.Network,WidgetStyle.Standard,0,Palette.For(true,false),Metrics.For(Density.Fit),s);
        var (snapshot,history)=Render.Demo();((IWidget)card).Update(snapshot,history);TextColours.Apply(card,s);
        var graph=Assert.Single(card.GetLogicalDescendants().OfType<MirrorChart>());
        Assert.Equal(Color.Parse("#34D399"),graph.DownTint);Assert.Equal(Color.Parse("#A78BFA"),graph.UpTint);
        network.SensorColours.Clear();TextColours.Apply(card,s);Assert.Null(graph.DownTint);Assert.Null(graph.UpTint);
        Assert.All(s.Widgets.Where(w=>w.Kind!=WidgetKind.Network),w=>Assert.Empty(w.SensorColours));
        return true;
    });
    [Fact]
    public void Every_selectable_design_renders_with_widget_colours_and_can_reset()=>UiHost.Run(()=>
    {
        var (snapshot,history)=Render.Demo();
        foreach(var style in Settings.Selectable)
        {
            var s=Settings.Starter();s.HiddenValues.Clear();s.TitleColor="#22D3EE";s.GraphColor="#A78BFA";
            foreach(var w in s.Widgets){w.HiddenSensors.Clear();w.Colours.InheritGlobal=false;w.Colours.ValueColor="#34D399";w.Colours.LabelColor="#D6DAE1";}
            var kinds=AioDesigns.Contains(style)?new[]{WidgetKind.Overview}:new[]{WidgetKind.Cpu,WidgetKind.Gpu,WidgetKind.Memory,WidgetKind.Drives,WidgetKind.Cooling,WidgetKind.Network,WidgetKind.Power};
            foreach(var kind in kinds)
            {
                var card=Cards.Create(kind,style,0,Palette.For(true,false),Metrics.For(Density.Fit),s);
                ((IWidget)card).Update(snapshot,history);TextColours.Apply(card,s);
                card.Measure(new Size(440,1100));card.Arrange(new Rect(0,0,440,Math.Max(1,card.DesiredSize.Height)));
                Assert.True(double.IsFinite(card.Bounds.Height),$"{style}/{kind}");
                s.TitleColor=s.GraphColor="";foreach(var w in s.Widgets)w.Colours=new();TextColours.Apply(card,s);
                s.TitleColor="#22D3EE";s.GraphColor="#A78BFA";
            }
        }
        return true;
    },seconds:180);
    [Fact]
    public void Render_actual_colour_settings_preview()=>UiHost.Run(()=>
    {
        var path=Environment.GetEnvironmentVariable("LUMEN_COLOUR_PREVIEW");if(string.IsNullOrEmpty(path))return true;
        var s=Settings.Starter();var cpu=s.Widgets.Single(w=>w.Kind==WidgetKind.Cpu);cpu.HiddenSensors.Clear();s.HiddenValues.Clear();
        cpu.Colours=new(){InheritGlobal=false,TitleColor="#22D3EE",LabelColor="#D6DAE1",ValueColor="#34D399",GraphColor="#A78BFA"};
        cpu.SensorColours["cpu.temp"]=new(){ValueColor="#F97316",AutomaticWarningColors=false};
        var palette=Palette.For(true,false);var settings=new SettingsWindow(s,palette,()=>{},()=>new[]{"Demo"},new TestPlatform());
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var section=(Expander)typeof(SettingsWindow).GetMethod("WidgetColourSection",flags)!.Invoke(settings,new object[]{cpu})!;section.IsExpanded=true;
        var rows=Ui.Stack(12,Ui.Text("Widgets  /  CPU",22,palette.Ink),section,Ui.Text("Sensor values",14,palette.Ink));
        foreach(var (key,label) in new[]{("cpu.temp","Temperature"),("cpu.load","Load"),("cpu.power","Power"),("cpu.voltage","Voltage")})
            rows.Children.Add((Control)typeof(SettingsWindow).GetMethod("SensorColourRow",flags)!.Invoke(settings,new object[]{cpu,key,new CheckBox{Content=label,IsChecked=true}})!);
        var card=Cards.Create(WidgetKind.Cpu,WidgetStyle.Standard,0,palette,Metrics.For(Density.Comfortable),s);
        var (snap,history)=Render.Demo();((IWidget)card).Update(snap,history);TextColours.Apply(card,s);
        var right=Ui.Stack(16,Ui.Text("Live preview",22,palette.Ink),card,Ui.Text("Sensor colour · Temperature",14,palette.Ink),
            (Control)typeof(SettingsWindow).GetMethod("ColourPalette",flags)!.Invoke(settings,new object[]{(Func<string>)(()=>"#F97316"),(Action<string>)(_=>{}),"Like widget"})!);
        var window=new Window{Width=1020,Height=760,RequestedThemeVariant=Avalonia.Styling.ThemeVariant.Dark,Background=Palette.Brush(palette.Surface),Content=new Border{Padding=new Thickness(24),Child=Ui.Columns("1.2*,*",rows,right)}};
        try{window.Show();Dispatcher.UIThread.RunJobs();using var image=window.CaptureRenderedFrame();Assert.NotNull(image);image.Save(path);}
        finally{window.Close();settings.Close();}
        return true;
    });
    [Fact]
    public void Sensor_overrides_widget_which_overrides_global_and_each_role_inherits_independently()
    {
        var s=Settings.Starter();s.ValueColor="#FFFFFF";s.LabelColor="#AAAAAA";
        var cpu=s.Widgets.Single(w=>w.Kind==WidgetKind.Cpu);cpu.Colours.InheritGlobal=false;cpu.Colours.ValueColor="#22D3EE";
        cpu.SensorColours["cpu.temp"]=new(){ValueColor="#F97316"};
        Assert.Equal("#F97316",ColourRules.Resolve(s,cpu,"cpu.temp",ColourRole.Value).Hex);
        Assert.Equal("#22D3EE",ColourRules.Resolve(s,cpu,"cpu.load",ColourRole.Value).Hex);
        Assert.Equal("#AAAAAA",ColourRules.Resolve(s,cpu,"cpu.temp",ColourRole.Label).Hex);
        cpu.Colours.InheritGlobal=true;
        Assert.Equal("#FFFFFF",ColourRules.Resolve(s,cpu,"cpu.load",ColourRole.Value).Hex);
        Assert.Equal("#F97316",ColourRules.Resolve(s,cpu,"cpu.temp",ColourRole.Value).Hex);
        cpu.SensorColours.Clear();Assert.Equal("#FFFFFF",ColourRules.Resolve(s,cpu,"cpu.temp",ColourRole.Value).Hex);
    }
    [Fact]
    public void Aio_hardware_sections_follow_their_widget_and_exact_device_sensor_exceptions()
    {
        var s=Settings.Starter();var aio=s.Widgets.Single(w=>w.Kind==WidgetKind.Overview);var cpu=s.Widgets.Single(w=>w.Kind==WidgetKind.Cpu);
        aio.Colours.InheritGlobal=false;aio.Colours.ValueColor="#FFFFFF";
        cpu.Colours.InheritGlobal=false;cpu.Colours.ValueColor="#22D3EE";cpu.SensorColours["cpu.temp"]=new(){ValueColor="#F97316"};
        Assert.Equal("#F97316",ColourRules.Resolve(s,aio,"aio.cpu.temp",ColourRole.Value).Hex);
        Assert.Equal("#22D3EE",ColourRules.Resolve(s,aio,"aio.cpu.load",ColourRole.Value).Hex);
        aio.SensorColours["aio.cpu.temp"]=new(){ValueColor="#A78BFA"};
        Assert.Equal("#A78BFA",ColourRules.Resolve(s,aio,"aio.cpu.temp",ColourRole.Value).Hex);
        aio.SensorColours["aio.gpu.first.temp"]=new(){ValueColor="#34D399"};
        Assert.Equal("#34D399",ColourRules.Resolve(s,aio,"aio.gpu.first.temp",ColourRole.Value).Hex);
        Assert.Equal("#FFFFFF",ColourRules.Resolve(s,aio,"aio.gpu.second.temp",ColourRole.Value).Hex);
    }
    [Fact]
    public void Profiles_and_themes_preserve_overrides_without_sharing_mutable_colour_objects()
    {
        var s=Settings.Starter();s.TitleColor="#a78bfa";s.GraphColor="#22d3ee";s.AutomaticWarningColors=false;
        var cpu=s.Widgets.Single(w=>w.Kind==WidgetKind.Cpu);cpu.Colours.InheritGlobal=false;cpu.Colours.ValueColor="#34d399";
        cpu.SensorColours["aio.cpu.temp"]=new(){ValueColor="#f97316",GraphColor="#60a5fa",AutomaticWarningColors=false};
        var profile=Settings.Normalize(JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(s,Settings.Json),Settings.Json)!);
        Assert.Equal("#A78BFA",profile.TitleColor);Assert.False(profile.AutomaticWarningColors);
        var restored=profile.Widgets.Single(w=>w.Kind==WidgetKind.Cpu);Assert.Equal("#34D399",restored.Colours.ValueColor);Assert.Equal("#F97316",restored.SensorColours["aio.cpu.temp"].ValueColor);
        var theme=ThemeFile.From(profile);var target=Settings.Starter();ThemeFile.Parse(theme.ToJson())!.ApplyTo(target);
        var themed=target.Widgets.Single(w=>w.Kind==WidgetKind.Cpu);Assert.Equal("#60A5FA",themed.SensorColours["aio.cpu.temp"].GraphColor);Assert.False(themed.Colours.InheritGlobal);
        restored.SensorColours["aio.cpu.temp"].ValueColor="#FFFFFF";Assert.Equal("#F97316",themed.SensorColours["aio.cpu.temp"].ValueColor);
        var legacy=ThemeFile.Parse("{\"type\":\"lumen-theme\",\"name\":\"Legacy\"}")!;legacy.ApplyTo(target);Assert.Equal("#60A5FA",themed.SensorColours["aio.cpu.temp"].GraphColor);
        themed.Colours.ValueColor="invalid";themed.SensorColours["bad"]=new(){ValueColor="red"};Settings.Normalize(target);Assert.Equal("",themed.Colours.ValueColor);Assert.DoesNotContain("bad",themed.SensorColours.Keys);
    }
    private static Color Ink(TextBlock t)=>((ISolidColorBrush)t.Foreground!).Color;
    private static TextBlock Value(Control root,string key)=>root.GetLogicalDescendants().OfType<TextBlock>().Single(t=>SensorDetails.KeyOf(t)==key);
    [Theory]
    [InlineData(WidgetStyle.CaseCyanGlass)]
    [InlineData(WidgetStyle.AioBoardCard)]
    [InlineData(WidgetStyle.AioThermalGrid)]
    [InlineData(WidgetStyle.AioPanelHeader)]
    public void Aio_colours_are_live_sensor_scoped_and_warning_colours_can_be_disabled(WidgetStyle style)=>UiHost.Run(()=>
    {
        var s=Settings.Starter();DesignWidgetSelection.Apply(s,style);AioDesigns.ShowAllParts(s);
        var slot=s.Widgets.Single(w=>w.Kind==WidgetKind.Overview);var cpu=s.Widgets.Single(w=>w.Kind==WidgetKind.Cpu);
        cpu.Colours.InheritGlobal=false;cpu.Colours.ValueColor="#22D3EE";cpu.Colours.LabelColor="#D6DAE1";cpu.Colours.GraphColor="#A78BFA";
        cpu.SensorColours["aio.cpu.temp"]=new(){ValueColor="#34D399"};
        var (snapshot,history)=Render.Demo();var card=new AioTile(style,Palette.For(true,false),Metrics.For(Density.Fit),s);card.Own(slot,0);
        card.Update(snapshot with{Cpu=snapshot.Cpu! with{Temperature=41}},history);TextColours.Apply(card,s);
        Assert.Equal(Color.Parse("#34D399"),Ink(Value(card,"aio.cpu.temp")));
        Assert.Equal(Color.Parse("#22D3EE"),Ink(Value(card,"aio.cpu.load")));
        card.Update(snapshot with{Cpu=snapshot.Cpu! with{Temperature=99}},history);TextColours.Apply(card,s);
        Assert.NotEqual(Color.Parse("#34D399"),Ink(Value(card,"aio.cpu.temp")));
        cpu.SensorColours["aio.cpu.temp"].AutomaticWarningColors=false;TextColours.Apply(card,s);Assert.Equal(Color.Parse("#34D399"),Ink(Value(card,"aio.cpu.temp")));
        var bars=card.GetLogicalDescendants().OfType<AioCoreBars>().FirstOrDefault();if(bars!=null)Assert.Equal(Color.Parse("#A78BFA"),bars.Tint);
        return true;
    });
    [Fact]
    public void Native_cpu_graph_and_temperature_use_their_own_sensor_colours_and_restore()=>UiHost.Run(()=>
    {
        var s=Settings.Starter();var cpu=s.Widgets.Single(w=>w.Kind==WidgetKind.Cpu);cpu.Colours.InheritGlobal=false;cpu.Colours.ValueColor="#22D3EE";cpu.Colours.GraphColor="#A78BFA";
        cpu.HiddenSensors.Remove("cpu.chart");s.HiddenValues.Remove("cpu.chart");
        cpu.SensorColours["cpu.load"]=new(){GraphColor="#34D399"};cpu.SensorColours["cpu.temp"]=new(){ValueColor="#F97316",AutomaticWarningColors=false};
        var card=Cards.Create(WidgetKind.Cpu,WidgetStyle.Standard,0,Palette.For(true,false),Metrics.For(Density.Fit),s);
        var (snapshot,history)=Render.Demo();((IWidget)card).Update(snapshot with{Cpu=snapshot.Cpu! with{Temperature=99}},history);TextColours.Apply(card,s);
        var hero=card.GetLogicalDescendants().OfType<Hero>().First();Assert.Contains(hero.GetLogicalDescendants().OfType<TextBlock>(),t=>Ink(t)==Color.Parse("#F97316"));
        var chart=Assert.Single(card.GetLogicalDescendants().OfType<AreaChart>());Assert.Equal(Color.Parse("#34D399"),chart.Color);
        cpu.SensorColours.Clear();cpu.Colours=new();TextColours.Apply(card,s);Assert.NotEqual(Color.Parse("#34D399"),chart.Color);
        return true;
    });
    private sealed class TestPlatform:Lumen.App.Platform { public override bool IsAdministrator=>false;public override bool AutostartEnabled=>false; }
    [Fact]
    public void Sensor_picker_validates_hex_applies_and_resets_without_changing_layout_or_selection()=>UiHost.Run(()=>
    {
        var s=Settings.Starter();s.Columns=6;s.Pages=3;s.Width=440;var cpu=s.Widgets.Single(w=>w.Kind==WidgetKind.Cpu);
        var before=cpu.HiddenSensors.ToArray();var window=new SettingsWindow(s,Palette.For(true,false),()=>Settings.Normalize(s),()=>new[]{"Demo"},new TestPlatform());
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        try
        {
            Control Row()=>(Control)typeof(SettingsWindow).GetMethod("SensorColourRow",flags)!.Invoke(window,new object[]{cpu,"cpu.temp",new CheckBox{Content="Temperature",IsChecked=true}})!;
            var button=Assert.Single(Row().GetLogicalDescendants().OfType<Button>(),b=>b.Name?.StartsWith("colour-sensor-")==true);
            var popup=(Control)((Flyout)button.Flyout!).Content!;
            var hex=popup.GetLogicalDescendants().OfType<TextBox>().First(t=>t.Name=="colour-hex");
            var apply=popup.GetLogicalDescendants().OfType<Button>().First(b=>b.Name=="colour-apply");
            hex.Text="invalid";apply.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));Assert.Empty(cpu.SensorColours);
            hex.Text="#22d3ee";apply.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));Assert.Equal("#22D3EE",cpu.SensorColours["cpu.temp"].ValueColor);
            button=Assert.Single(Row().GetLogicalDescendants().OfType<Button>(),b=>b.Name?.StartsWith("colour-sensor-")==true);popup=(Control)((Flyout)button.Flyout!).Content!;
            popup.GetLogicalDescendants().OfType<Button>().Single(b=>b.Name=="colour-sensor-reset").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));Assert.Empty(cpu.SensorColours);
            var section=(Expander)typeof(SettingsWindow).GetMethod("WidgetColourSection",flags)!.Invoke(window,new object[]{cpu})!;section.IsExpanded=true;
            section.GetLogicalDescendants().OfType<ToggleSwitch>().Single().IsChecked=false;
            var rebuilt=(Expander)typeof(SettingsWindow).GetMethod("WidgetColourSection",flags)!.Invoke(window,new object[]{cpu})!;Assert.True(rebuilt.IsExpanded);
            Assert.Equal(before,cpu.HiddenSensors);Assert.Equal(6,s.Columns);Assert.Equal(3,s.Pages);Assert.Equal(440,s.Width);
        }
        finally{Dispatcher.UIThread.RunJobs();window.Close();}
        return true;
    });
    [Fact]
    public void Settings_expose_widget_inheritance_and_sensor_picker_without_changing_selection()=>UiHost.Run(()=>
    {
        var s=Settings.Starter();var cpu=s.Widgets.Single(w=>w.Kind==WidgetKind.Cpu);cpu.Colours.InheritGlobal=false;
        var window=new SettingsWindow(s,Palette.For(true,false),()=>{},()=>new[]{"Demo"},new TestPlatform());
        try
        {
            var section=(Control)typeof(SettingsWindow).GetMethod("WidgetColourSection",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,new object[]{cpu})!;
            Assert.Contains(section.GetLogicalDescendants().OfType<Button>(),b=>b.Name=="colour-value-Cpu");
            var row=(Control)typeof(SettingsWindow).GetMethod("SensorColourRow",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,new object[]{cpu,"cpu.temp",new CheckBox{Content="Temperature",IsChecked=true}})!;
            Assert.Single(row.GetLogicalDescendants().OfType<Button>(),b=>b.Name=="colour-sensor-Cpu-cpu-temp");
            Assert.Empty(cpu.SensorColours);Assert.True(Assert.Single(row.GetLogicalDescendants().OfType<CheckBox>()).IsChecked);
        }
        finally{window.Close();}
        return true;
    });
}
