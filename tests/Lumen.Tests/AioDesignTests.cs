using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lumen.App;
using Lumen.Core;
using Xunit;

public class AioDesignTests
{
    public static IEnumerable<object[]> Styles()=>AioDesigns.Styles.Select(s=>new object[]{s});
    public static IEnumerable<object[]> Layouts()=>AioDesigns.Styles.SelectMany(s=>new[]{240d,280d,360d}.SelectMany(w=>new[]{1d,1.5d,2d}.Select(z=>new object[]{s,w,z})));
    [Fact]
    public void Isometric_art_and_live_readings_follow_group_switches()=>UiHost.Run(()=>
    {
        var (snap,h)=Render.Demo();var s=Settings.Starter();var slot=s.Widgets.Single(w=>w.Kind==WidgetKind.Overview);slot.Style=WidgetStyle.AioIsometricCore;
        var card=new AioTile(slot.Style,Palette.For(true,false),Metrics.For(Density.Fit),s);card.Own(slot,0);card.Update(snap,h);
        Assert.True(Assert.Single(card.GetLogicalDescendants().OfType<AioIsometricArtwork>()).ShowCpu);
        card.Update(snap with{Cpu=snap.Cpu! with{Temperature=73}},h);
        Assert.Contains(card.GetLogicalDescendants().OfType<TextBlock>(),t=>SensorDetails.KeyOf(t)=="aio.cpu.temp"&&t.Text!.Contains("73"));
        WidgetSensors.Select(s,slot,"aio.cpu",false,snap);WidgetSensors.Select(s,slot,"aio.cooling",false,snap);card.Update(snap,h);
        var art=Assert.Single(card.GetLogicalDescendants().OfType<AioIsometricArtwork>());Assert.False(art.ShowCpu);Assert.Equal(0,art.FanCount);
        Assert.DoesNotContain(SensorDetails.VisibleKeys(card),k=>k.StartsWith("aio.cpu.")||k.StartsWith("aio.cooling."));return true;
    });
    [Theory,MemberData(nameof(Styles))]
    public void Reference_values_are_not_repeated_and_the_card_has_no_live_badge_or_own_settings_button(WidgetStyle style)=>UiHost.Run(()=>
    {
        var (snap,h)=Render.Demo();var s=Settings.Starter();var slot=s.Widgets.Single(w=>w.Kind==WidgetKind.Overview);slot.Style=style;
        var card=new AioTile(style,Palette.For(true,false),Metrics.For(Density.Fit),s);card.Own(slot,0);card.Update(snap,h);
        // A drive temperature drawn in a diagram (Orbit circle, Isometric box) is repeated on purpose in the Drives part.
        var keys=card.GetLogicalDescendants().OfType<TextBlock>().Select(SensorDetails.KeyOf).Where(k=>k.Length>0&&!k.StartsWith("aio.system.disk.")).ToArray();
        Assert.Equal(keys.Length,keys.Distinct().Count());
        // The sidebar already shows "Live" and its settings at the top, so an AIO card repeats neither (user request).
        Assert.Empty(card.GetLogicalDescendants().OfType<Button>());
        Assert.DoesNotContain(card.GetLogicalDescendants().OfType<TextBlock>(),t=>t.Text?.Contains("Live")==true);
        return true;
    });
    [Fact]
    public void Global_aio_groups_hardware_without_mutating_saved_widgets()
    {
        var s=Settings.Starter();foreach(var slot in s.Widgets)slot.Style=WidgetStyle.AioTypeBlock;
        var before=s.Widgets.Select(w=>(w.Kind,w.Visible,w.Style,w.Page)).ToArray();
        var grouped=s.Layout(null,1).SelectMany(x=>x).ToArray();
        Assert.Equal(WidgetKind.Overview,Assert.Single(grouped).Kind);
        Assert.Equal(before,s.Widgets.Select(w=>(w.Kind,w.Visible,w.Style,w.Page)).ToArray());
        Assert.Equal(4,s.Layout(null,2).SelectMany(x=>x).Count());
        foreach(var slot in s.Widgets)slot.Style=WidgetStyle.Standard;
        Assert.Equal(5,s.Layout(null,1).SelectMany(x=>x).Count());
    }
    [Theory,MemberData(nameof(Styles))]
    public void Aio_has_unique_live_bindings_and_respects_sensor_switches(WidgetStyle style)=>UiHost.Run(()=>
    {
        var (snap,h)=Render.Demo();var s=Settings.Starter();var slot=s.Widgets.Single(w=>w.Kind==WidgetKind.Overview);slot.Style=style;
        var card=new AioTile(style,Palette.For(true,false),Metrics.For(Density.Fit),s);card.Own(slot,0);card.Update(snap,h);
        var keys=SensorDetails.VisibleKeys(card);Assert.Contains("aio.cpu.temp",keys);Assert.Contains("aio.network.down",keys);
        Assert.Contains("aio.network.up",keys);Assert.DoesNotContain("net.up",keys);
        WidgetSensors.Select(s,slot,"aio.network.up",false,snap);card.Update(snap,h);Assert.DoesNotContain("aio.network.up",SensorDetails.VisibleKeys(card));
        WidgetSensors.Select(s,slot,"aio.cpu",false,snap);card.Update(snap,h);Assert.DoesNotContain(SensorDetails.VisibleKeys(card),k=>k.StartsWith("aio.cpu."));
        WidgetSensors.Select(s,slot,"aio.network.up",true,snap);card.Update(snap,h);Assert.Contains("aio.network.up",SensorDetails.VisibleKeys(card));
        return true;
    });
    [Fact]
    public void Missing_gpu_voltage_and_core_temperatures_are_not_invented()
    {
        var (snap,_)=Render.Demo();var s=new Settings();var rows=AioReadings.For(snap with {HardwareSensors=Array.Empty<SensorNode>()},s);
        Assert.DoesNotContain(rows,r=>r.Group=="Cores");
        Assert.All(rows.Where(r=>r.Group=="GPU"&&r.Key.EndsWith("voltage")),r=>{Assert.Equal("—",r.Value);Assert.Null(r.Number);});
        Assert.All(rows.Where(r=>r.Group=="Rails"),r=>Assert.Null(r.Number));
    }
    [Fact]
    public void Rails_cores_and_gpu_voltage_come_from_their_correct_source()
    {
        var (snap,_)=Render.Demo();var gpu=snap.Gpus.First();
        var nodes=new[]{new SensorNode("Cpu","CPU","cpu",new[]{new SensorValue("Temperature","Core #1",48,1,"core1"),new SensorValue("Temperature","Core Average",60)},Array.Empty<SensorNode>()),
            new SensorNode("GpuNvidia",gpu.Name,gpu.Id,new[]{new SensorValue("Voltage","GPU Core",.975)},Array.Empty<SensorNode>()),
            new SensorNode("Motherboard","Board","board",new[]{new SensorValue("Voltage","+12V",12.08),new SensorValue("Voltage","+5V",5.02),new SensorValue("Voltage","+3.3V",3.31)},Array.Empty<SensorNode>())};
        var rows=AioReadings.For(snap with {HardwareSensors=nodes},new Settings());
        Assert.Equal(48,Assert.Single(rows.Where(r=>r.Group=="Cores")).Number);
        Assert.Equal(.975,Assert.Single(rows.Where(r=>r.Key=="aio.gpu."+gpu.Id+".voltage")).Number);
        Assert.Equal(12.08,rows.Single(r=>r.Key=="aio.rails.12").Number);
    }
    [Fact]
    public void An_explicit_aio_preserves_individual_widgets_and_aio_preferences_on_reload()
    {
        var s=Settings.Starter();var overview=s.Widgets.Single(w=>w.Kind==WidgetKind.Overview);
        overview.Visible=true;overview.Style=WidgetStyle.AioOrbitNetwork;overview.WidgetScale=1.5;overview.Name="PC";
        WidgetSensors.Select(s,overview,"aio.cpu.clock",true,null);
        WidgetSensors.Select(s,overview,"aio.network.up",false,null);
        var serialized=System.Text.Json.JsonSerializer.Serialize(s,Settings.Json);
        var loaded=Settings.Normalize(System.Text.Json.JsonSerializer.Deserialize<Settings>(serialized,Settings.Json)!);
        var only=Assert.Single(loaded.Layout(null,1).SelectMany(x=>x));
        Assert.Equal(WidgetKind.Overview,only.Kind);Assert.Equal(1.5,only.WidgetScale);Assert.Equal("PC",only.Name);
        Assert.True(loaded.ForSlot(only).Shows("aio.cpu.clock"));Assert.False(loaded.ForSlot(only).Shows("aio.network.up"));
        Assert.All(loaded.Widgets.Where(w=>w.Kind is WidgetKind.Cpu or WidgetKind.Gpu or WidgetKind.Memory),w=>{Assert.True(w.Visible);Assert.Equal(WidgetStyle.Standard,w.Style);});
    }
    [Fact]
    public void Multiple_gpu_fans_are_not_duplicated_by_the_aggregate_fan_reading()
    {
        var (snap,_)=Render.Demo();var gpu=snap.Gpus.First() with {FanRpm=1450};
        var raw=new SensorNode("GpuNvidia",gpu.Name,gpu.Id,new[]{new SensorValue("Fan","GPU1",1450,1,"fan1"),new SensorValue("Fan","GPU2",1420,2,"fan2")},Array.Empty<SensorNode>());
        var rows=AioReadings.For(snap with {Gpus=new[]{gpu},HardwareSensors=new[]{raw},Fans=Array.Empty<FanInfo>(),Cpu=snap.Cpu! with {FanRpm=null}},new Settings());
        Assert.Equal(2,rows.Count(r=>r.Group=="Cooling"));
    }
    [Fact]
    public void A_transient_missing_fan_keeps_its_row_but_an_explicit_switch_hides_it_immediately()=>UiHost.Run(()=>
    {
        var (snap,h)=Render.Demo();var s=Settings.Starter();var slot=s.Widgets.Single(w=>w.Kind==WidgetKind.Overview);slot.Style=WidgetStyle.AioPrecision;AioDesigns.ShowAllParts(s);
        var card=new AioTile(slot.Style,Palette.For(true,false),Metrics.For(Density.Fit),s);card.Own(slot,0);
        const string key="aio.cooling.merged:Test case fan";
        card.Update(snap with {Fans=new[]{new FanInfo("Test case fan",860,null)}},h);
        Assert.Contains(key,SensorDetails.VisibleKeys(card));
        card.Update(snap with {Fans=Array.Empty<FanInfo>()},h);
        Assert.Contains(key,SensorDetails.VisibleKeys(card));
        var field=card.GetLogicalDescendants().OfType<TextBlock>().Single(t=>SensorDetails.KeyOf(t)==key);Assert.Equal("—",field.Text);
        WidgetSensors.Select(s,slot,"aio.cooling",false,snap);card.Update(snap,h);Assert.DoesNotContain(key,SensorDetails.VisibleKeys(card));
        return true;
    });
    [Theory,MemberData(nameof(Styles))]
    public void Aio_uses_a_picked_background_and_hardware_name_switch(WidgetStyle style)=>UiHost.Run(()=>
    {
        var (snap,h)=Render.Demo();var s=Settings.Starter();s.Background="black";
        var own=s.Widgets.Single(w=>w.Kind==WidgetKind.Overview);own.Style=style;own.HardwareName="My workstation";
        var cpu=s.Widgets.Single(w=>w.Kind==WidgetKind.Cpu);cpu.HardwareName="My processor";
        var p=Palette.For(s,true,null);var card=new AioTile(own.Style,p,Metrics.For(Density.Fit),s);card.Own(own,0);card.Update(snap,h);
        Assert.Equal(p.Card,((Avalonia.Media.ISolidColorBrush)card.Background!).Color);
        Assert.Contains(card.GetLogicalDescendants().OfType<TextBlock>(),t=>t.Text=="My processor");
        Assert.Contains(card.GetLogicalDescendants().OfType<TextBlock>(),t=>t.Text?.Contains("My workstation")==true);
        cpu.ShowHardware=false;card.Update(snap,h);Assert.DoesNotContain(card.GetLogicalDescendants().OfType<TextBlock>(),t=>t.Text=="My processor");
        own.ShowHardware=false;card.Update(snap,h);Assert.DoesNotContain(card.GetLogicalDescendants().OfType<TextBlock>(),t=>t.Text?.Contains("My workstation")==true);
        return true;
    });
    [Fact]
    public void Recommended_selection_restores_the_aio_profile_even_without_a_current_snapshot()=>UiHost.Run(()=>
    {
        var s=Settings.Starter();var slot=s.Widgets.Single(w=>w.Kind==WidgetKind.Overview);slot.Visible=true;slot.Style=WidgetStyle.AioTypeBlock;
        WidgetSensors.Select(s,slot,"aio.cpu.clock",true,null);WidgetSensors.Select(s,slot,"aio.network.up",false,null);
        var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        typeof(SettingsWindow).GetField("pageIndex",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!.SetValue(null,0);
        var win=new SettingsWindow(s,Palette.For(true,false),()=>{},()=>new[]{"Demo"},new TestPlatform());
        try
        {
            ((HashSet<WidgetKind>)typeof(SettingsWindow).GetField("expanded",flags)!.GetValue(win)!).Add(WidgetKind.Overview);
            typeof(SettingsWindow).GetMethod("Build",flags)!.Invoke(win,null);
            var button=win.GetLogicalDescendants().OfType<Button>().Single(b=>b.Name=="sensor-recommended-Overview");
            button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Assert.False(s.ForSlot(slot).Shows("aio.cpu.clock"));Assert.True(s.ForSlot(slot).Shows("aio.network.up"));
        }
        finally{win.Close();}
        return true;
    });
    private sealed class TestPlatform:Lumen.App.Platform
    {
        public override bool IsAdministrator=>false;
        public override bool AutostartEnabled=>false;
    }
    [Theory]
    [InlineData(WidgetStyle.AioInstrument)]
    [InlineData(WidgetStyle.AioAuroraFusion)]
    [InlineData(WidgetStyle.AioSignalRack)]
    public void History_switch_removes_history_graphics_without_hiding_sensor_values(WidgetStyle style)=>UiHost.Run(()=>
    {
        var (snap,h)=Render.Demo();var s=Settings.Starter();var slot=s.Widgets.Single(w=>w.Kind==WidgetKind.Overview);slot.Style=style;
        var card=new AioTile(style,Palette.For(true,false),Metrics.For(Density.Fit),s);card.Own(slot,0);card.Update(snap,h);
        Assert.True(card.GetLogicalDescendants().OfType<AreaChart>().Any()||card.GetLogicalDescendants().OfType<AioReferenceHistory>().Any()||card.GetLogicalDescendants().OfType<AioMeter>().Any(m=>m.Historical));
        var keys=SensorDetails.VisibleKeys(card).Order().ToArray();WidgetSensors.Select(s,slot,"aio.graphs",false,snap);card.Update(snap,h);
        Assert.Empty(card.GetLogicalDescendants().OfType<AreaChart>());Assert.DoesNotContain(card.GetLogicalDescendants().OfType<AioMeter>(),m=>m.Historical);
        Assert.Empty(card.GetLogicalDescendants().OfType<AioReferenceHistory>());
        Assert.Equal(keys,SensorDetails.VisibleKeys(card).Order().ToArray());return true;
    });
    [Fact]
    public void Thermal_grid_keeps_the_voltage_rail_beside_the_core_grid_at_normal_size()=>UiHost.Run(()=>
    {
        var (snap,h)=Render.Demo();var s=Settings.Starter();var slot=s.Widgets.Single(w=>w.Kind==WidgetKind.Overview);slot.Style=WidgetStyle.AioThermalGrid;
        var card=new AioTile(slot.Style,Palette.For(true,false),Metrics.For(Density.Fit),s);card.Own(slot,0);card.Update(snap,h);
        var win=new Window{Width=360,Height=1800,Content=new StackPanel{Children={card}}};
        try{win.Show();Dispatcher.UIThread.RunJobs();using var frame=win.CaptureRenderedFrame();var split=card.GetLogicalDescendants().OfType<Grid>().Single(g=>Equals(g.Tag,"aio-thermal-split"));Assert.True(split.Children[1].Bounds.X>split.Bounds.Width*.6);Assert.True(split.Children[1].Bounds.Width<split.Bounds.Width*.4);}
        finally{win.Close();}return true;
    });
    [Theory,MemberData(nameof(Layouts))]
    public void Aio_numbers_stay_inside_card_at_supported_widths_and_sizes(WidgetStyle style,double width,double scale)=>UiHost.Run(()=>
    {
        var (snap,h)=Render.Demo();var s=Settings.Starter();var slot=s.Widgets.Single(w=>w.Kind==WidgetKind.Overview);slot.Style=style;slot.WidgetScale=scale;
        var card=new AioTile(style,Palette.For(true,false),Metrics.For(Density.Fit),s);card.Own(slot,0);
        var win=new Window{Width=width,Height=5000,Content=new StackPanel{Children={card}}};
        try
        {
            win.Show();card.Update(snap,h);Dispatcher.UIThread.RunJobs();using var frame=win.CaptureRenderedFrame();
            foreach(var t in card.GetLogicalDescendants().OfType<TextBlock>().Where(t=>SensorDetails.Visible(t,card)&&SensorDetails.KeyOf(t).Length>0))
            {
                var a=t.TranslatePoint(default,card);var b=t.TranslatePoint(new Point(t.Bounds.Width,t.Bounds.Height),card);
                Assert.True(a.HasValue&&b.HasValue);Assert.True(a.Value.X>=-1.5&&b.Value.X<=card.Bounds.Width+1.5,$"{style}/{width}/{scale}: {t.Text} outside card ({a} – {b})");
            }
            foreach(var graphic in card.GetLogicalDescendants().OfType<Control>().Where(c=>c is AioRing or AioMeter or AioFan or AioDevice or AioReferenceFan or AioReferenceHistory or AioIsometricConnections or AioIsometricArtwork))
            {
                var a=graphic.TranslatePoint(default,card);var b=graphic.TranslatePoint(new Point(graphic.Bounds.Width,graphic.Bounds.Height),card);
                Assert.True(a.HasValue&&b.HasValue&&a.Value.X>=-1.5&&b.Value.X<=card.Bounds.Width+1.5,$"{style}/{width}/{scale}: {graphic.GetType().Name} outside card");
            }
        }
        finally{win.Close();}
        return true;
    });
    [Fact]
    public void Widget_switches_choose_the_parts_of_an_aio_card()=>UiHost.Run(()=>
    {
        var (snap,h)=Render.Demo();var s=Settings.Starter();var slot=s.Widgets.Single(w=>w.Kind==WidgetKind.Overview);slot.Style=WidgetStyle.AioPrecision;
        AioDesigns.ShowAllParts(s);
        var card=new AioTile(slot.Style,Palette.For(true,false),Metrics.For(Density.Fit),s);card.Own(slot,0);card.Update(snap,h);
        Assert.Contains(SensorDetails.VisibleKeys(card),k=>k.StartsWith("aio.cooling."));
        Assert.Contains(SensorDetails.VisibleKeys(card),k=>k.StartsWith("aio.drives."));
        s.Widgets.Single(w=>w.Kind==WidgetKind.Cooling).Visible=false;s.Widgets.Single(w=>w.Kind==WidgetKind.Drives).Visible=false;card.Update(snap,h);
        Assert.DoesNotContain(SensorDetails.VisibleKeys(card),k=>k.StartsWith("aio.cooling."));
        Assert.DoesNotContain(SensorDetails.VisibleKeys(card),k=>k.StartsWith("aio.drives.")||k.StartsWith("aio.system.disk."));
        Assert.Contains(SensorDetails.VisibleKeys(card),k=>k.StartsWith("aio.cpu."));
        return true;
    });
    [Fact]
    public void Existing_aio_settings_keep_every_part_after_the_switch_update()
    {
        var s=Settings.Starter();s.Widgets.Single(w=>w.Kind==WidgetKind.Overview).Style=WidgetStyle.AioTypeBlock;s.AioWidgetSwitches=false;
        s.Widgets.Single(w=>w.Kind==WidgetKind.Cooling).Visible=false;
        var loaded=Settings.Normalize(System.Text.Json.JsonSerializer.Deserialize<Settings>(System.Text.Json.JsonSerializer.Serialize(s))!);
        Assert.True(loaded.AioWidgetSwitches);Assert.True(loaded.Widgets.Single(w=>w.Kind==WidgetKind.Cooling).Visible);
    }
    [Fact]
    public void Sensor_choices_reach_an_aio_card_built_from_widget_styles()=>UiHost.Run(()=>
    {
        // Like a real setup: every widget uses an AIO style, the Overview widget itself is switched off.
        var (snap,h)=Render.Demo();var s=Settings.Starter();
        foreach(var w in s.Widgets)w.Style=WidgetStyle.AioTypeBlock;
        AioDesigns.ShowAllParts(s);
        var overview=s.Widgets.Single(w=>w.Kind==WidgetKind.Overview);overview.Visible=false;
        var fan=AioReadings.Options(snap).First(o=>o.Key.StartsWith("aio.cooling.")&&o.Key!="aio.cooling").Key;
        var junction=AioReadings.Options(snap).First(o=>o.Key.EndsWith(".junction")).Key;
        AioTile Card()
        {
            var slot=s.Layout().SelectMany(c=>c).First(w=>w.Kind==WidgetKind.Overview);
            var card=new AioTile(slot.Style,Palette.For(true,false),Metrics.For(Density.Fit),s);card.Own(slot,0);card.Update(snap,h);return card;
        }
        Assert.Contains(fan,SensorDetails.VisibleKeys(Card()));Assert.Contains(junction,SensorDetails.VisibleKeys(Card()));
        WidgetSensors.Select(s,overview,fan,false,snap);WidgetSensors.Select(s,overview,junction,false,snap);
        Assert.DoesNotContain(fan,SensorDetails.VisibleKeys(Card()));Assert.DoesNotContain(junction,SensorDetails.VisibleKeys(Card()));
        WidgetSensors.Select(s,overview,fan,true,snap);
        Assert.Contains(fan,SensorDetails.VisibleKeys(Card()));
        return true;
    });
    [Fact]
    public void Widget_customise_lists_and_switches_the_aio_values() => UiHost.Run(() =>
    {
        const System.Reflection.BindingFlags Private = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        L.Use("en");
        var (snap, _) = Render.Demo();
        var settings = Settings.Starter();
        foreach (var w in settings.Widgets) w.Style = WidgetStyle.AioTypeBlock;
        AioDesigns.ShowAllParts(settings);
        var overview = settings.Widgets.Single(w => w.Kind == WidgetKind.Overview); overview.Visible = false;
        typeof(SettingsWindow).GetField("pageIndex", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.SetValue(null, 0);
        var window = new SettingsWindow(settings, Palette.For(true, false), () => { }, () => new[] { "Demo" }, new IntegratedGraphicsOptionTests.TestPlatform(), snapshot: () => snap);
        var build = typeof(SettingsWindow).GetMethod("Build", Private)!;
        Control Content() => (Control)typeof(SettingsWindow).GetField("content", Private)!.GetValue(window)!;
        try
        {
            var expanded = (HashSet<WidgetKind>)typeof(SettingsWindow).GetField("expanded", Private)!.GetValue(window)!;
            expanded.Add(WidgetKind.Cooling); expanded.Add(WidgetKind.Gpu);
            build.Invoke(window, null);
            var boxes = Content().GetLogicalDescendants().OfType<CheckBox>().Where(c => c.Tag is string k && k.StartsWith("aio.")).ToArray();
            var fan = boxes.First(c => ((string)c.Tag!).StartsWith("aio.cooling.") && (string)c.Tag! != "aio.cooling");
            var junction = boxes.First(c => ((string)c.Tag!).EndsWith(".junction"));
            Assert.True(fan.IsChecked); Assert.True(junction.IsChecked);
            string fanKey = (string)fan.Tag!, junctionKey = (string)junction.Tag!;
            fan.IsChecked = false;
            Content().GetLogicalDescendants().OfType<CheckBox>().First(c => (string?)c.Tag == junctionKey).IsChecked = false;
            Assert.Contains(fanKey, overview.HiddenSensors); Assert.Contains(junctionKey, overview.HiddenSensors);
            Assert.False(Content().GetLogicalDescendants().OfType<CheckBox>().First(c => (string?)c.Tag == fanKey).IsChecked);
        }
        finally { window.Close(); }
        return true;
    });
    [Fact]
    public void Aio_shows_stopped_fans_but_not_empty_headers()=>UiHost.Run(()=>
    {
        var (snap,h)=Render.Demo();var s=Settings.Starter();var slot=s.Widgets.Single(w=>w.Kind==WidgetKind.Overview);slot.Style=WidgetStyle.AioPrecision;AioDesigns.ShowAllParts(s);
        var board=new SensorNode("Motherboard","Board","/mb",Array.Empty<SensorValue>(),new[]{new SensorNode("SuperIO","Nuvoton","/lpc",new[]
        {
            new SensorValue("Fan","Fan 1",null,0,"/lpc/fan/0"),      // header without a fan: no value
            new SensorValue("Fan","Fan 2",734,1,"/lpc/fan/1"),       // running
            new SensorValue("Fan","Fan 3",0,2,"/lpc/fan/2"),         // fan present but stopped
        },Array.Empty<SensorNode>())});
        snap=snap with {Fans=Array.Empty<FanInfo>(),HardwareSensors=new[]{board}};
        s.KnownFans.Add("/lpc/fan/2");   // turned in an earlier run, stopped now
        var card=new AioTile(slot.Style,Palette.For(true,false),Metrics.For(Density.Fit),s);card.Own(slot,0);card.Update(snap,h);
        var shown=SensorDetails.VisibleKeys(card).ToArray();
        Assert.Contains("aio.cooling./lpc/fan/1",shown);
        Assert.Contains("aio.cooling./lpc/fan/2",shown);
        Assert.DoesNotContain("aio.cooling./lpc/fan/0",shown);
        Assert.DoesNotContain(AioReadings.Options(snap),o=>o.Key=="aio.cooling./lpc/fan/0");
        Assert.Contains("/lpc/fan/1",s.KnownFans);  // a running fan is remembered
        // An empty header that never turns stays hidden.
        var empty=new SensorNode("Motherboard","Board2","/mb2",Array.Empty<SensorValue>(),new[]{new SensorNode("SuperIO","Nuvoton","/lpc2",new[]{new SensorValue("Fan","Fan 9",0,0,"/lpc2/fan/9")},Array.Empty<SensorNode>())});
        card.Update(snap with {HardwareSensors=new[]{board,empty}},h);
        Assert.DoesNotContain("aio.cooling./lpc2/fan/9",SensorDetails.VisibleKeys(card));
        // The same empty header also reported by the merged fan list does not come back through it.
        card.Update(snap with {HardwareSensors=new[]{board,empty},Fans=new[]{new FanInfo("Fan 9",0,null)}},h);
        Assert.DoesNotContain(SensorDetails.VisibleKeys(card),k=>k.Contains("Fan 9")||k.Contains("fan/9"));
        // It is still offered under Customise, and ticking it shows it.
        Assert.Contains(AioReadings.Options(snap with {HardwareSensors=new[]{board,empty}}),o=>o.Key=="aio.cooling./lpc2/fan/9");
        WidgetSensors.Select(s,slot,"aio.cooling./lpc2/fan/9",true,snap);
        card.Update(snap with {HardwareSensors=new[]{board,empty}},h);
        Assert.Contains("aio.cooling./lpc2/fan/9",SensorDetails.VisibleKeys(card));
        return true;
    });
    [Theory]
    [MemberData(nameof(Styles))]
    public void Every_aio_sensor_switches_off_and_on_in_every_design(WidgetStyle style)=>UiHost.Run(()=>
    {
        // The user's setup: every widget uses the AIO style, the Overview widget itself is switched off.
        var (snap,h)=Render.Demo();var s=Settings.Starter();
        foreach(var w in s.Widgets)w.Style=style;
        AioDesigns.ShowAllParts(s);
        var overview=s.Widgets.Single(w=>w.Kind==WidgetKind.Overview);overview.Visible=false;
        HashSet<string> Visible()
        {
            var slot=s.Layout().SelectMany(c=>c).First(w=>w.Kind==WidgetKind.Overview);
            var card=new AioTile(slot.Style,Palette.For(true,false),Metrics.For(Density.Fit),s);card.Own(slot,0);card.Update(snap,h);
            return SensorDetails.VisibleKeys(card).ToHashSet();
        }
        var groups=AioReadings.Groups.Select(g=>g.Key).ToHashSet();
        var values=AioReadings.For(snap,new Settings()).Select(r=>r.Key).ToHashSet();
        var keys=AioReadings.Options(snap).Select(o=>o.Key).Where(k=>!groups.Contains(k)&&values.Contains(k)).ToArray();
        Assert.NotEmpty(keys);
        foreach(var key in keys)
        {
            WidgetSensors.Select(s,overview,key,false,snap);
            Assert.False(Visible().Contains(key),style+": "+key+" still shown after switching it off");
            WidgetSensors.Select(s,overview,key,true,snap);
            Assert.True(Visible().Contains(key),style+": "+key+" not shown after switching it on");
        }
        return true;
    });

    [Theory]
    [MemberData(nameof(Styles))]
    public void Core_load_bars_follow_the_settings_choice_in_every_design(WidgetStyle style)=>UiHost.Run(()=>
    {
        var (snap,h)=Render.Demo();var s=Settings.Starter();
        foreach(var w in s.Widgets)w.Style=style;
        AioDesigns.ShowAllParts(s);
        var overview=s.Widgets.Single(w=>w.Kind==WidgetKind.Overview);overview.Visible=false;
        Assert.True(snap.Cpu!.CoreLoads.Count>1);
        Assert.Contains(AioReadings.Options(snap),o=>o.Key=="aio.cpu.coreloads");
        Control? Bars(double width)
        {
            var slot=s.Layout().SelectMany(c=>c).First(w=>w.Kind==WidgetKind.Overview);
            var card=new AioTile(slot.Style,Palette.For(true,false),Metrics.For(Density.Fit),s);card.Own(slot,0);card.Update(snap,h);
            var window=new Window{Content=card,Width=width,Height=3000};window.Show();
            for(int k=0;k<2;k++){Dispatcher.UIThread.RunJobs();window.CaptureRenderedFrame();}
            var bars=card.GetVisualDescendants().OfType<Control>().FirstOrDefault(c=>c is AioCoreBars or MainboardCoreGrid);
            if(bars!=null)
            {
                // Every core gets a bar inside the card, and new loads never change the height (nothing jumps).
                var right=bars.TranslatePoint(new Point(bars.Bounds.Width,0),card)!.Value.X;
                Assert.True(right<=card.Bounds.Width+.5,style+": core bars run out of the card");
                double before=card.Bounds.Height;
                card.Update(snap with{Cpu=snap.Cpu! with{CoreLoads=snap.Cpu!.CoreLoads.Select((v,i)=>i%2==0?100d:0d).ToArray()}},h);
                for(int k=0;k<2;k++){Dispatcher.UIThread.RunJobs();window.CaptureRenderedFrame();}
                Assert.Equal(before,card.Bounds.Height,1);
                var loads=bars is AioCoreBars standard?standard.Loads:((MainboardCoreGrid)bars).Loads;
                Assert.Equal(snap.Cpu.CoreLoads.Count,loads.Count);
                Assert.Equal(100d,loads[0]);
            }
            window.Close();return bars;
        }
        Assert.NotNull(Bars(260));Assert.NotNull(Bars(400));
        WidgetSensors.Select(s,overview,"aio.cpu.coreloads",false,snap);
        Assert.Null(Bars(260));
        WidgetSensors.Select(s,overview,"aio.cpu.coreloads",true,snap);
        Assert.NotNull(Bars(260));
        return true;
    });

    [Fact]
    public void Only_the_main_graphics_card_is_shown_until_a_further_card_is_ticked()
    {
        var (snap,_)=Render.Demo();var g=snap.Gpus.First(x=>!x.Integrated);
        // The main card is the dedicated one with the most memory, whatever order the cards are reported in.
        snap=snap with{Gpus=new[]{g with{Id="small",Name="Small card",MemoryTotalMb=4096},g}};
        var s=Settings.Starter();foreach(var w in s.Widgets)w.Style=WidgetStyle.AioIsometricCompact;AioDesigns.ShowAllParts(s);
        var overview=s.Widgets.Single(w=>w.Kind==WidgetKind.Overview);var scoped=s.ForWidget(WidgetKind.Overview);
        var rows=AioReadings.For(snap,scoped);
        Assert.Contains(rows,r=>r.Group=="GPU"&&r.Device==g.Id);
        Assert.DoesNotContain(rows,r=>r.Group=="GPU"&&r.Device=="small");
        var option=Assert.Single(AioReadings.Options(snap),o=>o.Key=="aio.gpu.small.extra");
        Assert.Contains("Small card",option.Label);
        (overview.AioEnabledSensors??=new()).Add(option.Key);
        rows=AioReadings.For(snap,s.ForWidget(WidgetKind.Overview));
        Assert.Contains(rows,r=>r.Group=="GPU"&&r.Device=="small");
        Assert.Equal(g.Id,rows.First(r=>r.Group=="GPU").Device);
    }

    [Fact]
    public void Integrated_graphics_without_sensors_show_no_empty_values()
    {
        var (snap,_)=Render.Demo();
        var igpu=snap.Gpus.First(g=>g.Integrated) with{Temperature=null,HotSpot=null,MemoryJunction=null};
        snap=snap with{Gpus=new[]{igpu}};
        var s=Settings.Starter();foreach(var w in s.Widgets)w.Style=WidgetStyle.AioTypeBlock;AioDesigns.ShowAllParts(s);
        var rows=AioReadings.For(snap,s.ForWidget(WidgetKind.Overview)).Where(r=>r.Group=="GPU").ToList();
        Assert.Contains(rows,r=>r.Key.EndsWith(".load"));
        Assert.DoesNotContain(rows,r=>r.Key.EndsWith(".temp")||r.Key.EndsWith(".hotspot")||r.Key.EndsWith(".junction"));
    }
}
