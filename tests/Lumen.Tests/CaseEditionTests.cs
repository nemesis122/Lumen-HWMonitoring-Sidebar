using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Lumen.App;
using Lumen.Core;
using Xunit;

public class CaseEditionTests
{
    public static IEnumerable<object[]> Designs()=>CaseEditionDesigns.All.Select(l=>new object[]{l.Style});
    [Theory,MemberData(nameof(Designs))]
    public void Drive_details_are_optional_but_explicit_selection_survives(WidgetStyle design)=>UiHost.Run(()=>
    {
        var s=Settings.Starter();DesignWidgetSelection.Apply(s,design);var slot=s.Widgets.Single(w=>w.Kind==WidgetKind.Overview);
        var(snapshot,history)=Render.Demo();var disk=snapshot.Disks.First(d=>d.Life.HasValue&&d.PowerOnHours.HasValue&&d.TotalActivity.HasValue);
        foreach(var suffix in new[]{"life","hours","activity"})
        {
            string key="aio.drives."+disk.Id+"."+suffix;
            Assert.DoesNotContain(AioReadings.For(snapshot,s.ForSlot(slot)),r=>r.Key==key);
            WidgetSensors.Select(s,slot,key,true,snapshot);Assert.Contains(AioReadings.For(snapshot,s.ForSlot(slot)),r=>r.Key==key);
            var saved=Settings.Normalize(JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(s,Settings.Json),Settings.Json)!);
            Assert.Contains(key,saved.Widgets.Single(w=>w.Kind==WidgetKind.Overview).AioEnabledSensors);
            WidgetSensors.Select(s,slot,key,false,snapshot);Assert.DoesNotContain(AioReadings.For(snapshot,s.ForSlot(slot)),r=>r.Key==key);
        }
        Assert.Contains(AioReadings.For(snapshot,s.ForSlot(slot)),r=>r.Key=="aio.drives."+disk.Id+".used");return true;
    });
    [Theory,MemberData(nameof(Designs))]
    public void Inner_value_tile_backgrounds_follow_glass_opacity_while_values_remain_opaque(WidgetStyle design)=>UiHost.Run(()=>
    {
        var tileColor=Avalonia.Media.Color.Parse("#18232D");
        foreach(var (glass,opacity) in new[]{(true,.2),(true,.7),(false,.2)})
        {
            var s=Settings.Starter();s.Glass=glass;s.Opacity=opacity;DesignWidgetSelection.Apply(s,design);
            var slot=s.Widgets.Single(w=>w.Kind==WidgetKind.Overview);var(snapshot,history)=Render.Demo();
            var palette=Palette.For(s,true,null);var card=new AioTile(design,palette,Metrics.For(Density.Fit),s);
            card.Own(slot,0);card.Update(snapshot,history);
            var tiles=card.GetLogicalDescendants().OfType<Border>().Where(b=>b.Background is Avalonia.Media.ISolidColorBrush ink&&ink.Color.R==tileColor.R&&ink.Color.G==tileColor.G&&ink.Color.B==tileColor.B).ToArray();
            if(design!=WidgetStyle.CaseNvidiaEdition)Assert.NotEmpty(tiles);
            foreach(var tile in tiles)
            {
                Assert.Equal(palette.Card.A,((Avalonia.Media.ISolidColorBrush)tile.Background!).Color.A);
                Assert.Equal(1,tile.Opacity);
                foreach(var value in tile.GetLogicalDescendants().OfType<TextBlock>().Where(t=>SensorDetails.KeyOf(t).Length>0))
                {Assert.Equal(1,value.Opacity);Assert.Equal(255,((Avalonia.Media.ISolidColorBrush)value.Foreground!).Color.A);}
            }
        }
        return true;
    });
    public static IEnumerable<object[]> Layouts()=>CaseEditionDesigns.All.SelectMany(l=>new[]{1,2,3,4,5,6}.SelectMany(c=>new[]{300,440,640}.Select(w=>new object[]{l.Style,c,w})));
    [Fact]
    public void Five_editions_are_selectable_and_preserve_placement()
    {
        Assert.Equal(5,CaseEditionDesigns.All.Length);
        foreach(var look in CaseEditionDesigns.All)
        {
            Assert.Contains(look.Style,Settings.Selectable);Assert.True(AioDesigns.Dashboard(look.Style));
            var s=Settings.Starter();s.Width=640;s.Columns=6;s.Density=Density.Compact;
            DesignWidgetSelection.Apply(s,look.Style);
            var saved=Settings.Normalize(JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(s,Settings.Json),Settings.Json)!);
            Assert.Equal(640,saved.Width);Assert.Equal(6,saved.Columns);Assert.Equal(Density.Compact,saved.Density);
            Assert.Contains(saved.Widgets,w=>w.Style==look.Style);
        }
    }
    [Theory,MemberData(nameof(Designs))]
    public void All_selected_readings_remain_live_and_can_be_hidden(WidgetStyle design)=>UiHost.Run(()=>
    {
        var s=Settings.Starter();DesignWidgetSelection.Apply(s,design);AioDesigns.ShowAllParts(s);
        var slot=s.Widgets.Single(w=>w.Kind==WidgetKind.Overview);var(snapshot,history)=Render.Demo();
        var card=new AioTile(design,Palette.For(true,false),Metrics.For(Density.Fit),s);card.Own(slot,0);card.Update(snapshot,history);
        var expected=AioReadings.For(snapshot,s.ForSlot(slot)).Where(r=>r.Group!="Cores").Select(r=>r.Key).Distinct().Order().ToArray();
        Assert.Equal(expected,SensorDetails.VisibleKeys(card).Distinct().Order().ToArray());
        var values=card.GetLogicalDescendants().OfType<TextBlock>().Where(t=>SensorDetails.KeyOf(t)=="aio.cpu.temp").ToArray();Assert.NotEmpty(values);
        card.Update(snapshot with{Cpu=snapshot.Cpu! with{Temperature=93}},history);Assert.All(values,t=>Assert.Contains("93",t.Text!));
        WidgetSensors.Select(s,slot,"aio.cpu",false,snapshot);card.Update(snapshot,history);
        Assert.DoesNotContain(SensorDetails.VisibleKeys(card),k=>k.StartsWith("aio.cpu."));return true;
    });
    [Theory,MemberData(nameof(Layouts))]
    public void Native_cards_fit_without_overlap_and_background_covers_scene(WidgetStyle design,int columns,int width)=>UiHost.Run(()=>
    {
        var s=Settings.Starter();s.Width=width;s.Columns=columns;s.Density=Density.Fit;
        s.FullScreen=design==WidgetStyle.CaseCommandCenter&&columns==3;
        DesignWidgetSelection.Apply(s,design);var(snapshot,history)=Render.Demo();
        typeof(SidebarView).GetField("page",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!.SetValue(null,1);
        var sidebar=new SidebarView(s,Palette.For(true,false));sidebar.Update(snapshot,history);
        var window=new Window{Content=sidebar,Width=s.FullScreen?1920:SidebarView.WindowWidth(s,1920),Height=1040};
        try
        {
            window.Show();
            foreach(int height in new[]{1040,800,1400})
            {
                window.Height=height;Dispatcher.UIThread.RunJobs();using var image=window.CaptureRenderedFrame();Assert.NotNull(image);
                var scene=Assert.Single(sidebar.GetLogicalDescendants().OfType<CaseEditionPanel>());
                Assert.True(scene.ArtworkBounds.Width>=scene.Bounds.Width-.1);Assert.True(scene.ArtworkBounds.Height>=scene.Bounds.Height-.1);
                foreach(var w in scene.Widgets)
                {
                    Assert.True(w.Control.Bounds.Bottom<=scene.Bounds.Height+1,$"{design}/{columns}: {w.Component} {w.Control.Bounds}, scene {scene.Bounds}");
                    Assert.True(w.Control.Bounds.Right<=scene.Bounds.Width+1);
                    foreach(var other in scene.Widgets.Where(q=>q!=w))
                    {var r=w.Control.Bounds.Intersect(other.Control.Bounds);Assert.True(r.Width<1||r.Height<1,$"{design}/{columns}: {w.Component} overlaps {other.Component}");}
                }
                Assert.DoesNotContain(scene.Connectors,c=>c.Component is "power" or "net");
                string? output=Environment.GetEnvironmentVariable("LUMEN_CASE_EDITION_OUTPUT");
                if(output!=null&&width==640&&height==1040&&(columns==1||design==WidgetStyle.CaseCommandCenter&&columns==3))
                {Directory.CreateDirectory(output);image!.Save(Path.Combine(output,$"{design}-{columns}-columns.png"));}
            }
        }
        finally{window.Close();}
        return true;
    });
    [Theory,MemberData(nameof(Designs))]
    public void Different_gpu_manufacturers_keep_their_detected_names_and_readings(WidgetStyle design)=>UiHost.Run(()=>
    {
        var s=Settings.Starter();DesignWidgetSelection.Apply(s,design);AioDesigns.ShowAllParts(s);
        var slot=s.Widgets.Single(w=>w.Kind==WidgetKind.Overview);var(demo,history)=Render.Demo();
        var amd=new GpuInfo("discrete-one","AMD Radeon RX 9070 XT",60,70,40,2200,180,1100,4000,16000);
        var intel=new GpuInfo("other-two","Intel Arc B580",45,null,8,400,12,null,300,12000);
        var snapshot=demo with{Gpus=new[]{amd,intel},HardwareSensors=Array.Empty<SensorNode>()};
        WidgetSensors.Select(s,slot,GpuGroup.ExtraKey(intel),true,snapshot);
        var card=new AioTile(design,Palette.For(true,false),Metrics.For(Density.Fit),s);card.Own(slot,0);card.Update(snapshot,history);
        var scene=Assert.Single(card.GetLogicalDescendants().OfType<CaseEditionPanel>());var gpu=scene.Widgets.Where(w=>w.Component=="gpu").ToArray();Assert.Equal(2,gpu.Length);
        Assert.Contains(gpu,w=>w.Control.GetLogicalDescendants().OfType<TextBlock>().Any(t=>t.Text==amd.Name));
        Assert.Contains(gpu,w=>w.Control.GetLogicalDescendants().OfType<TextBlock>().Any(t=>t.Text==intel.Name));
        Assert.Contains("aio.gpu.discrete-one.temp",SensorDetails.VisibleKeys(card));Assert.Contains("aio.gpu.other-two.load",SensorDetails.VisibleKeys(card));return true;
    });
}
