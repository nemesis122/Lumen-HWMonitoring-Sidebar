using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lumen.App;
using Lumen.Core;
using Xunit;

public class CaseOverlayTests
{
    [Theory]
    [InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)][InlineData(6)]
    public void Case_card_backgrounds_follow_glass_opacity_without_fading_text(int columns)=>UiHost.Run(()=>
    {
        foreach(var (glass,opacity) in new[]{(true,.2),(true,.7),(false,.2)})
        {
            var s=Settings.Starter();s.Glass=glass;s.Opacity=opacity;s.Columns=columns;
            DesignWidgetSelection.Apply(s,WidgetStyle.CaseCyanGlass);var slot=s.Widgets.Single(w=>w.Kind==WidgetKind.Overview);
            var(snapshot,history)=Render.Demo();var palette=Palette.For(s,true,null);
            var card=new AioTile(WidgetStyle.CaseCyanGlass,palette,Metrics.For(Density.Fit),s);card.Own(slot,0);card.Update(snapshot,history);
            var scene=Assert.Single(card.GetLogicalDescendants().OfType<CaseOverlayPanel>());Assert.NotEmpty(scene.Widgets);
            byte expected=(byte)Math.Round((columns<=2?250:248)*palette.Card.A/255d);
            foreach(var widget in scene.Widgets)
            {
                var panel=Assert.IsType<Border>(widget.Control);
                Assert.Equal(expected,Assert.IsAssignableFrom<Avalonia.Media.ISolidColorBrush>(panel.Background).Color.A);
                Assert.Equal(1,panel.Opacity);
                foreach(var value in panel.GetLogicalDescendants().OfType<TextBlock>().Where(t=>SensorDetails.KeyOf(t).Length>0))
                {Assert.Equal(1,value.Opacity);Assert.Equal(255,Assert.IsAssignableFrom<Avalonia.Media.ISolidColorBrush>(value.Foreground).Color.A);}
            }
        }
        return true;
    });
    public static IEnumerable<object[]> Designs() => CaseDesigns.All.Select(l => new object[] { l.Style });
    public static IEnumerable<object[]> Layouts() => CaseDesigns.All.SelectMany(l => new[] {1,2,3,4,5,6}.Select(c => new object[]{l.Style,c}));
    [Fact]
    public void Retained_case_is_selectable_and_round_trips_without_touching_placement()
    {
        Assert.Single(CaseDesigns.All);
        Assert.Equal(WidgetStyle.CaseCyanGlass, CaseDesigns.All[0].Style);
        foreach (var look in CaseDesigns.All)
        {
            Assert.True((int)look.Style > (int)WidgetStyle.AioNetworkCenter);
            Assert.Contains(look.Style, Settings.Selectable); Assert.True(AioDesigns.Contains(look.Style)); Assert.True(AioDesigns.Dashboard(look.Style));
            var settings=Settings.Starter();settings.Width=440;settings.Columns=4;settings.Pages=2;settings.PageNames=new(){"HW","INFO"};settings.Density=Density.Compact;
            DesignWidgetSelection.Apply(settings,look.Style);
            var saved=Settings.Normalize(JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(settings,Settings.Json),Settings.Json)!);
            Assert.Contains(saved.Widgets,w=>w.Style==look.Style);Assert.Equal(4,saved.Columns);Assert.Equal(440,saved.Width);Assert.Equal(Density.Compact,saved.Density);Assert.Equal(new[]{"HW","INFO"},saved.PageNames.Take(2));
        }
    }
    [Theory,MemberData(nameof(Designs))]
    public void Every_selected_reading_has_one_native_binding_and_live_updates(WidgetStyle design) => UiHost.Run(()=>
    {
        var s=Settings.Starter();DesignWidgetSelection.Apply(s,design);AioDesigns.ShowAllParts(s);
        var slot=s.Widgets.Single(w=>w.Kind==WidgetKind.Overview);var (snapshot,history)=Render.Demo();
        var card=new AioTile(design,Palette.For(true,false),Metrics.For(Density.Fit),s);card.Own(slot,0);card.Update(snapshot,history);
        var expected=AioReadings.For(snapshot,s.ForSlot(slot)).Where(r=>r.Group!="Cores").Select(r=>r.Key).Order().ToArray();
        Assert.Equal(expected,SensorDetails.VisibleKeys(card).Order().ToArray());
        var values=card.GetLogicalDescendants().OfType<TextBlock>().Where(t=>SensorDetails.KeyOf(t).Length>0).ToArray();
        Assert.Equal(values.Length,values.Select(SensorDetails.KeyOf).Distinct().Count());
        var temp=values.Single(t=>SensorDetails.KeyOf(t)=="aio.cpu.temp");
        card.Update(snapshot with{Cpu=snapshot.Cpu! with{Temperature=93}},history);Assert.Contains("93",temp.Text!);
        Assert.Same(temp,card.GetLogicalDescendants().OfType<TextBlock>().Single(t=>SensorDetails.KeyOf(t)=="aio.cpu.temp"));
        WidgetSensors.Select(s,slot,"aio.cpu",false,snapshot);card.Update(snapshot,history);
        Assert.DoesNotContain(SensorDetails.VisibleKeys(card),k=>k.StartsWith("aio.cpu."));
        Assert.Contains("aio.system.ram",SensorDetails.VisibleKeys(card));
        return true;
    });
    [Theory,MemberData(nameof(Layouts))]
    public void Case_scene_fills_height_and_widgets_do_not_overlap_at_all_column_counts(WidgetStyle design,int columns) => UiHost.Run(()=>
    {
        int warnings=UiHost.LayoutWarnings.Count;
        var settings=Settings.Starter();settings.Width=440;settings.Columns=columns;settings.Density=Density.Fit;settings.PageNames=new(){"HW","INFO"};
        DesignWidgetSelection.Apply(settings,design);var (snapshot,history)=Render.Demo();
        typeof(SidebarView).GetField("page",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!.SetValue(null,1);
        var sidebar=new SidebarView(settings,Palette.For(true,false));sidebar.Update(snapshot,history);
        var window=new Window{Content=sidebar,Width=SidebarView.WindowWidth(settings),Height=1040};
        try
        {
            window.Show();
            foreach(int height in new[]{1040,1400,800})
            {
                window.Height=height;Dispatcher.UIThread.RunJobs();using var frame=window.CaptureRenderedFrame();Assert.NotNull(frame);
                var card=Assert.Single(sidebar.GetLogicalDescendants().OfType<AioTile>());
                var scene=Assert.Single(card.GetLogicalDescendants().OfType<CaseOverlayPanel>());Assert.Equal(columns,scene.Columns);
                if(scene.Refined)
                {
                    Assert.True(scene.ArtworkBounds.Width>=scene.Bounds.Width-1);
                    Assert.True(scene.ArtworkBounds.Height>=scene.Bounds.Height-1);
                    if(columns==2)Assert.All(scene.Widgets,w=>Assert.True(w.Control.Bounds.Width<=241));
                }
                else Assert.InRange(scene.ArtworkBounds.Height-scene.Bounds.Height,-1,1);
                var widgets=scene.Widgets;Assert.NotEmpty(widgets);
                foreach(var widget in widgets)
                {
                    Assert.True(widget.Control.Bounds.Width>40);Assert.True(widget.Control.Bounds.Height>0);
                    Assert.True(widget.Control.Bounds.Bottom<=scene.Bounds.Height+1,$"{design}/{columns}/height {height}: {widget.Component} {widget.Control.Bounds}, desired {widget.Control.DesiredSize}, scene {scene.Bounds}, desired {scene.DesiredSize}, growth {card.DashboardHeightGrowth}");
                    foreach(var other in widgets.Where(w=>!ReferenceEquals(w,widget)))
                    {
                        var intersection=widget.Control.Bounds.Intersect(other.Control.Bounds);
                        Assert.True(intersection.Width<1||intersection.Height<1,$"{design}/{columns}: {widget.Component} overlaps {other.Component}");
                    }
                }
                var fit=sidebar.GetLogicalDescendants().OfType<FitPanel>().First();
                var bottom=card.TranslatePoint(new Point(0,card.Bounds.Height),fit)!.Value.Y;
                Assert.InRange(bottom-fit.Bounds.Height,-3,3);
                foreach(var line in scene.Connectors.Where(c=>c.Points.Count>0)) Assert.Equal(scene.ComponentPoint(line.Component,line.Instance),line.Points[^1]);
                foreach(var component in widgets.Select(w=>w.Component).Distinct().Where(id=>!scene.Refined||id is not ("net" or "power" or "other")))
                    Assert.Contains(scene.Connectors,c=>c.Component==component&&c.Points.Count>=2);
                if(scene.Refined)
                {
                    foreach(var widget in widgets.Where(w=>w.Component is not ("net" or "power" or "other")))
                    {
                        var line=Assert.Single(scene.Connectors,c=>c.Component==widget.Component&&c.Instance==widget.Instance);
                        Assert.True(line.Points.Count>=2,$"Missing {widget.Component}/{widget.Instance} connector");
                        double length=Enumerable.Range(1,line.Points.Count-1).Sum(i=>Math.Abs(line.Points[i].X-line.Points[i-1].X)+Math.Abs(line.Points[i].Y-line.Points[i-1].Y));
                        Assert.True(length<scene.Bounds.Width*.8,$"Long connector {widget.Component}: {length}");
                    }
                    Assert.DoesNotContain(scene.Connectors,c=>c.Component is "net" or "power" or "other");
                }
                string? output=Environment.GetEnvironmentVariable("LUMEN_CASE_OUTPUT");
                if(output!=null&&height==1040){Directory.CreateDirectory(output);frame!.Save(System.IO.Path.Combine(output,$"{design}-{columns}cols.png"));}
                sidebar.Update(snapshot with{Cpu=snapshot.Cpu! with{Temperature=51}},history);
            }
            Assert.DoesNotContain(UiHost.LayoutWarnings.Skip(warnings),s=>s.Contains("cycle",StringComparison.OrdinalIgnoreCase));
        }
        finally{window.Close();}
        return true;
    });
    [Fact]
    public void Connector_routes_around_an_intervening_sensor_widget()
    {
        var route=CaseConnectorRouter.Route(new Point(20,100),new Point(280,100),new[]{new Rect(100,50,100,100)},new Rect(0,0,300,220));
        Assert.True(route.Count>=4);Assert.Equal(new Point(280,100),route[^1]);
        Assert.Contains(route,p=>p.Y<50||p.Y>150);
    }

    public static IEnumerable<object[]> ScrollLayouts() => CaseDesigns.All.Select(l=>l.Style).Concat(CaseEditionDesigns.All.Select(l=>l.Style)).SelectMany(style=>new[]{Density.Comfortable,Density.Compact}.Select(d=>new object[]{style,d}));
    [Theory,MemberData(nameof(ScrollLayouts))]
    public void Natural_density_keeps_scrolling_instead_of_forcing_automatic_fit(WidgetStyle design,Density density) => UiHost.Run(()=>
    {
        var s=Settings.Starter();s.Width=440;s.Columns=2;s.Density=density;DesignWidgetSelection.Apply(s,design);
        var (snapshot,history)=Render.Demo();var sidebar=new SidebarView(s,Palette.For(true,false));sidebar.Update(snapshot,history);
        var window=new Window{Content=sidebar,Width=SidebarView.WindowWidth(s),Height=800};
        try
        {
            window.Show();Dispatcher.UIThread.RunJobs();using var image=window.CaptureRenderedFrame();Assert.NotNull(image);
            var scroll=Assert.Single(sidebar.GetLogicalDescendants().OfType<ScrollViewer>(),v=>v.Content is FitPanel);
            Assert.Equal(0,((FitPanel)scroll.Content!).LastHeight);Assert.Equal(density,s.Density);
            if(CaseEditionDesigns.Contains(design))Assert.Equal(2,Assert.Single(sidebar.GetLogicalDescendants().OfType<CaseEditionPanel>()).Columns);
            else Assert.Equal(2,Assert.Single(sidebar.GetLogicalDescendants().OfType<CaseOverlayPanel>()).Columns);
        }
        finally{window.Close();}
        return true;
    });

    [Theory,MemberData(nameof(Designs))]
    public void Different_gpu_models_keep_separate_names_and_selected_values(WidgetStyle design) => UiHost.Run(()=>
    {
        var s=Settings.Starter();DesignWidgetSelection.Apply(s,design);AioDesigns.ShowAllParts(s);
        var slot=s.Widgets.Single(w=>w.Kind==WidgetKind.Overview);var (demo,history)=Render.Demo();
        var a=new GpuInfo("discrete-one","AMD Radeon RX 9070 XT",60,70,40,2200,180,1100,4000,16000);
        var b=new GpuInfo("other-two","Intel Arc B580",45,null,8,400,12,null,300,12000);
        var snapshot=demo with{Gpus=new[]{a,b},HardwareSensors=Array.Empty<SensorNode>()};
        WidgetSensors.Select(s,slot,GpuGroup.ExtraKey(b),true,snapshot);
        var card=new AioTile(design,Palette.For(true,false),Metrics.For(Density.Fit),s);card.Own(slot,0);card.Update(snapshot,history);
        var scene=Assert.Single(card.GetLogicalDescendants().OfType<CaseOverlayPanel>());
        var gpu=scene.Widgets.Where(w=>w.Component=="gpu").ToArray();Assert.Equal(2,gpu.Length);
        Assert.Contains(gpu,w=>w.Control.GetLogicalDescendants().OfType<TextBlock>().Any(t=>t.Text==a.Name));
        Assert.Contains(gpu,w=>w.Control.GetLogicalDescendants().OfType<TextBlock>().Any(t=>t.Text==b.Name));
        Assert.Contains("aio.gpu.discrete-one.temp",SensorDetails.VisibleKeys(card));Assert.Contains("aio.gpu.other-two.load",SensorDetails.VisibleKeys(card));
        return true;
    });
}
