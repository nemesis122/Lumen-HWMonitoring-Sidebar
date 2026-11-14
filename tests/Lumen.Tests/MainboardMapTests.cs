using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Lumen.App;
using Lumen.Core;
using Xunit;

public class MainboardMapTests
{
    [Fact]
    public void Mainboard_cores_has_aligned_rows_and_a_wide_live_core_strip()=>UiHost.Run(()=>
    {
        var (snapshot,history)=Render.Demo();snapshot=snapshot with{Cpu=snapshot.Cpu! with{CoreLoads=Enumerable.Range(1,16).Select(i=>(double)i).ToArray()}};
        var settings=Settings.Starter();AioDesigns.ShowAllParts(settings);
        var slot=settings.Widgets.Single(w=>w.Kind==WidgetKind.Overview);slot.Style=WidgetStyle.AioMainboardCores;
        var card=new AioTile(slot.Style,Palette.For(true,false),Metrics.For(Density.Fit),settings);card.Own(slot,0);card.Update(snapshot,history);
        var expected=AioReadings.For(snapshot,settings.ForSlot(slot)).Where(r=>r.Group!="Cores").Select(r=>r.Key).Order().ToArray();
        var window=new Window{Width=440,Height=2000,Content=new StackPanel{Children={card}}};
        try
        {
            window.Show();
            foreach(double width in new[]{440d,360d,260d,440d})
            {
                window.Width=width;Dispatcher.UIThread.RunJobs();using var frame=window.CaptureRenderedFrame();
                var bars=card.GetLogicalDescendants().OfType<MainboardCoreGrid>().Single();
                Assert.True(bars.PreferSingleRow);Assert.Equal(width>=360?16:8,bars.VisibleColumns);
                Assert.Equal(expected,card.GetLogicalDescendants().OfType<TextBlock>().Select(SensorDetails.KeyOf).Where(k=>k.Length>0).Order());
                Border Module(string key)=>card.GetLogicalDescendants().OfType<TextBlock>().Single(t=>SensorDetails.KeyOf(t)==key)
                    .GetLogicalAncestors().OfType<Border>().First(b=>Equals(b.Tag,"aio-section"));
                var cpu=Module("aio.cpu.temp");var gpu=Module(expected.First(k=>k.StartsWith("aio.gpu.")&&k.EndsWith(".temp")));
                var ram=Module("aio.system.ram");var psu=Module("aio.rails.12");
                double Top(Control c)=>c.TranslatePoint(default,card)!.Value.Y;
                Assert.Equal(Top(cpu),Top(gpu),1);Assert.Equal(cpu.Bounds.Height,gpu.Bounds.Height,1);
                Assert.True(Top(bars)>Top(cpu)+cpu.Bounds.Height);
                Assert.Equal(Top(ram),Top(psu),1);Assert.True(Top(ram)>Top(bars)+bars.Bounds.Height);
                Assert.True(bars.Bounds.Width>card.Bounds.Width*.8);
            }
            card.Update(snapshot with{Cpu=snapshot.Cpu! with{CoreLoads=Enumerable.Repeat(73d,16).ToArray()}},history);
            Assert.All(card.GetLogicalDescendants().OfType<MainboardCoreGrid>().Single().Loads,v=>Assert.Equal(73,v));
            WidgetSensors.Select(settings,slot,"aio.cpu.coreloads",false,snapshot);card.Update(snapshot,history);
            Assert.Empty(card.GetLogicalDescendants().OfType<MainboardCoreGrid>());
            Assert.Contains("aio.cpu.temp",SensorDetails.VisibleKeys(card));
        }
        finally{window.Close();}
        return true;
    });

    [Fact]
    public void Mainboard_rails_never_cross_full_width_fields_even_with_glass()=>UiHost.Run(()=>
    {
        var (snapshot,history)=Render.Demo();
        foreach(bool fans in new[]{true,false})
        foreach(double width in new[]{260d,360d,440d})
        {
            var settings=Settings.Starter();settings.Glass=true;AioDesigns.ShowAllParts(settings);
            settings.Widgets.Single(w=>w.Kind==WidgetKind.Cooling).Visible=fans;
            var slot=settings.Widgets.Single(w=>w.Kind==WidgetKind.Overview);slot.Style=WidgetStyle.AioMainboard;
            var card=new AioTile(slot.Style,Palette.For(true,true),Metrics.For(Density.Fit),settings);card.Own(slot,0);card.Update(snapshot,history);
            var window=new Window{Width=width,Height=2200,Content=new StackPanel{Children={card}}};
            try
            {
                window.Show();Dispatcher.UIThread.RunJobs();using var frame=window.CaptureRenderedFrame();
                var traces=card.GetLogicalDescendants().OfType<MainboardTraces>().Single();
                var footers=card.GetLogicalDescendants().OfType<Border>().Where(b=>Equals(b.Tag,"aio-section")&&b.Bounds.Width>traces.Bounds.Width*.7).ToArray();
                Assert.NotEmpty(footers);Assert.NotEmpty(traces.RailSegments());
                foreach(var field in footers)
                {
                    var point=field.TranslatePoint(default,traces)!.Value;var area=new Rect(point,field.Bounds.Size);
                    foreach(var line in traces.RailSegments())
                    {
                        bool crosses=line.Start.X>area.Left&&line.Start.X<area.Right&&line.End.Y>area.Top&&line.Start.Y<area.Bottom;
                        Assert.False(crosses,$"Rail crosses a full-width field at {width}px, fans={fans}.");
                    }
                }
            }
            finally{window.Close();}
        }
        return true;
    });

    [Fact]
    public void Network_values_stay_inside_their_own_cells_after_unit_changes()=>UiHost.Run(()=>
    {
        var (snap,history)=Render.Demo();var settings=Settings.Starter();AioDesigns.ShowAllParts(settings);
        var slot=settings.Widgets.Single(w=>w.Kind==WidgetKind.Overview);slot.Style=WidgetStyle.AioMainboard;
        var card=new AioTile(slot.Style,Palette.For(true,false),Metrics.For(Density.Fit),settings);card.Own(slot,0);
        var window=new Window{Width=380,Height=2600,Content=new StackPanel{Children={card}}};
        try
        {
            window.Show();
            foreach(var width in new[]{260d,380d,440d})
            foreach(var bits in new[]{32_000d,999_999d,108_000_000_000d,32_000d})
            {
                window.Width=width;card.Update(snap with{Network=snap.Network! with{UploadBytesPerSecond=bits/8}},history);
                Dispatcher.UIThread.RunJobs();using var frame=window.CaptureRenderedFrame();
                var cells=card.GetLogicalDescendants().OfType<Border>().Where(b=>Equals(b.Tag,"mainboard-network-cell")).ToArray();Assert.Equal(3,cells.Length);
                foreach(var cell in cells)
                {
                    var value=cell.GetLogicalDescendants().OfType<TextBlock>().Single(t=>SensorDetails.KeyOf(t).Length>0);
                    var left=value.TranslatePoint(default,cell)!.Value.X;
                    var right=value.TranslatePoint(new Point(value.Bounds.Width,0),cell)!.Value.X;
                    // Viewbox layout rounds physical pixels; retain at least two pixels of inset.
                    Assert.True(left>=2&&right<=cell.Bounds.Width-2,$"Network {SensorDetails.KeyOf(value)} escaped its cell at {width}px: {left:F2}..{right:F2} of {cell.Bounds.Width:F2}.");
                }
            }
        }
        finally{window.Close();}
        return true;
    });

    [Fact]
    public void Desktop_fits_real_monitor_sizes_with_readable_values()=>UiHost.Run(()=>
    {
        var (snap,history)=Render.Demo();
        foreach(var size in new[]{new Size(1920,1080),new Size(2560,1440)})
        {
            var settings=Settings.Starter();settings.FullScreen=true;settings.Columns=1;AioDesigns.ShowAllParts(settings);
            foreach(var slot in settings.Widgets.Where(w=>AioDesigns.Hardware(w.Kind)))slot.Style=WidgetStyle.AioMainboardDesktop;
            var view=new SidebarView(settings,Palette.For(true,false));view.Update(snap,history);
            var window=new Window{Width=size.Width,Height=size.Height,Content=view};
            try
            {
                window.Show();Dispatcher.UIThread.RunJobs();using var frame=window.CaptureRenderedFrame();
                var layout=view.GetLogicalDescendants().OfType<MainboardMapLayout>().Single();
                Assert.InRange(layout.Bounds.Width,780,1400);
                var board=layout.GetLogicalDescendants().OfType<Border>().Single(b=>Equals(b.Tag,"mainboard-panel"));
                Assert.InRange(board.Bounds.Height,200,600);
                var values=view.GetLogicalDescendants().OfType<TextBlock>().Where(t=>SensorDetails.KeyOf(t).Length>0).ToArray();Assert.NotEmpty(values);
                foreach(var value in values)
                {
                    var a=value.TranslatePoint(default,view)!.Value;
                    var b=value.TranslatePoint(new Point(0,value.FontSize),view)!.Value;
                    Assert.True(b.Y-a.Y>=9,$"{SensorDetails.KeyOf(value)} shrank to {b.Y-a.Y} pixels.");
                    var bottom=value.TranslatePoint(new Point(0,value.Bounds.Height),view)!.Value.Y;
                    Assert.True(bottom<=size.Height+1,$"{SensorDetails.KeyOf(value)} below screen.");
                }
            }
            finally{window.Close();}
        }
        return true;
    });

    [Fact]
    public void Vram_stays_in_the_same_row_as_its_label()=>UiHost.Run(()=>
    {
        var (snap,history)=Render.Demo();var settings=Settings.Starter();AioDesigns.ShowAllParts(settings);
        var slot=settings.Widgets.Single(w=>w.Kind==WidgetKind.Overview);slot.Style=WidgetStyle.AioMainboardMap;
        var card=new AioTile(slot.Style,Palette.For(true,false),Metrics.For(Density.Fit),settings);card.Own(slot,0);card.Update(snap,history);
        var window=new Window{Width=440,Height=2500,Content=new StackPanel{Children={card}}};
        try
        {
            window.Show();
            foreach(double width in new[]{440d,380d})
            {
                window.Width=width;Dispatcher.UIThread.RunJobs();using var frame=window.CaptureRenderedFrame();
                var value=card.GetLogicalDescendants().OfType<TextBlock>().Single(t=>SensorDetails.KeyOf(t).EndsWith(".vram"));
                var label=card.GetLogicalDescendants().OfType<TextBlock>().Single(t=>Equals(t.Tag,"map-label:"+SensorDetails.KeyOf(value)));
                var v=value.TranslatePoint(new Point(0,value.Bounds.Height/2),card)!.Value;
                var l=label.TranslatePoint(new Point(label.Bounds.Width,label.Bounds.Height/2),card)!.Value;
                Assert.True(v.X>=l.X,"VRAM value belongs to the right of its label.");
                Assert.True(Math.Abs(v.Y-l.Y)<2,"VRAM must not appear below its label.");
            }
        }
        finally{window.Close();}
        return true;
    });

    [Fact]
    public void Contour_connections_follow_components_without_crossing_sensor_text()=>UiHost.Run(()=>
    {
        var (snap,history)=Render.Demo();var settings=Settings.Starter();AioDesigns.ShowAllParts(settings);
        var slot=settings.Widgets.Single(w=>w.Kind==WidgetKind.Overview);slot.Style=WidgetStyle.AioMainboardMap;
        var card=new AioTile(slot.Style,Palette.For(true,false),Metrics.For(Density.Fit),settings);card.Own(slot,0);card.Update(snap,history);
        var window=new Window{Width=440,Height=2500,Content=new StackPanel{Children={card}}};
        try
        {
            window.Show();
            foreach(double width in new[]{440d,380d,260d,1160d})
            {
                window.Width=width;Dispatcher.UIThread.RunJobs();using var frame=window.CaptureRenderedFrame();
                var overlay=card.GetLogicalDescendants().OfType<MapConnections>().Single();
                var paths=overlay.GetPaths();
                var hardware=paths.Where(p=>p.Component.Width>0).ToArray();
                Assert.Equal(new[]{"cpu","gpu","psu","ram"},hardware.Select(p=>p.Part).Order());
                var textRects=card.GetLogicalDescendants().OfType<TextBlock>().Where(t=>SensorDetails.KeyOf(t).Length>0).Select(t=>
                {
                    var a=t.TranslatePoint(default,overlay)!.Value;var b=t.TranslatePoint(new Point(t.Bounds.Width,t.Bounds.Height),overlay)!.Value;
                    return new Rect(a,b);
                }).ToArray();
                foreach(var path in paths)
                {
                    if(path.Component.Width>0)Assert.True(path.Component.Contains(path.Points[^1]),"Connection must end on its own component.");
                    for(int i=1;i<path.Points.Count;i++)
                    {
                        var a=path.Points[i-1];var b=path.Points[i];bool vertical=Math.Abs(a.X-b.X)<.01;
                        Assert.True(vertical||Math.Abs(a.Y-b.Y)<.01,"Variant 1 uses orthogonal routes.");
                        foreach(var text in textRects)
                        {
                            bool crosses=vertical?a.X>text.Left+.5&&a.X<text.Right-.5&&Math.Max(a.Y,b.Y)>text.Top+.5&&Math.Min(a.Y,b.Y)<text.Bottom-.5:
                                a.Y>text.Top+.5&&a.Y<text.Bottom-.5&&Math.Max(a.X,b.X)>text.Left+.5&&Math.Min(a.X,b.X)<text.Right-.5;
                            Assert.False(crosses,$"{path.Part} line crosses a sensor value at width {width}.");
                        }
                    }
                }
            }
        }
        finally{window.Close();}
        return true;
    });

    [Fact]
    public void Separate_window_updates_live_and_keeps_sensor_switches()=>UiHost.Run(()=>
    {
        var (snap,history)=Render.Demo();var settings=Settings.Starter();AioDesigns.ShowAllParts(settings);
        var window=new MainboardMapWindow(settings,Palette.For(true,false));
        try
        {
            window.Show();window.Update(snap,history);Dispatcher.UIThread.RunJobs();using var frame=window.CaptureRenderedFrame();
            var card=window.GetLogicalDescendants().OfType<AioTile>().Single();
            Assert.Contains("aio.cpu.temp",SensorDetails.VisibleKeys(card));
            Assert.Contains("aio.network.link",SensorDetails.VisibleKeys(card));
            window.Update(snap with{Cpu=snap.Cpu! with{Temperature=91}},history);
            Assert.Contains(card.GetLogicalDescendants().OfType<TextBlock>(),t=>SensorDetails.KeyOf(t)=="aio.cpu.temp"&&t.Text!.Contains("91"));
            settings.Widgets.Single(w=>w.Kind==WidgetKind.Network).Visible=false;
            window.Update(snap,history);
            Assert.DoesNotContain(SensorDetails.VisibleKeys(card),k=>k.StartsWith("aio.network."));
        }
        finally{window.Close();}
        return true;
    });

    [Fact]
    public void Map_reflows_between_sidebar_and_window_without_losing_values()=>UiHost.Run(()=>
    {
        var (snap,history)=Render.Demo();var settings=Settings.Starter();AioDesigns.ShowAllParts(settings);
        var slot=settings.Widgets.Single(w=>w.Kind==WidgetKind.Overview);slot.Style=WidgetStyle.AioMainboardMap;
        var card=new AioTile(slot.Style,Palette.For(true,false),Metrics.For(Density.Fit),settings);card.Own(slot,0);card.Update(snap,history);
        var expected=AioReadings.For(snap,settings.ForSlot(slot)).Where(r=>r.Group!="Cores").Select(r=>r.Key).Order().ToArray();
        var window=new Window{Width=1160,Height=2000,Content=new StackPanel{Children={card}}};
        try
        {
            window.Show();
            foreach(double width in new[]{1160d,440d,380d,260d,1160d})
            {
                window.Width=width;Dispatcher.UIThread.RunJobs();using var frame=window.CaptureRenderedFrame();
                var map=card.GetLogicalDescendants().OfType<MainboardMapLayout>().Single();
                Assert.Equal(width>=780,map.Wide);
                if(!map.Wide)
                {
                    var board=map.GetLogicalDescendants().OfType<Border>().Single(b=>Equals(b.Tag,"mainboard-panel"));
                    Assert.Equal(map.Bounds.Width,board.Bounds.Width,1);
                    Assert.True(board.Bounds.Height<=330,"The artwork row must leave room for readable hardware labels.");
                    Border Module(string key)
                    {
                        Control c=card.GetLogicalDescendants().OfType<TextBlock>().Single(t=>SensorDetails.KeyOf(t)==key);
                        while(c is not Border{Tag:string tag}||!tag.StartsWith("map-")) c=(Control)c.Parent!;
                        return (Border)c;
                    }
                    var cpu=Module("aio.cpu.temp");var gpu=Module(expected.Single(k=>k.StartsWith("aio.gpu.")&&k.EndsWith(".temp")));
                    var ram=Module("aio.system.ram");var psu=Module("aio.rails.12");
                    Point Position(Control c)=>c.TranslatePoint(default,map)!.Value;
                    Assert.True(Position(cpu).Y>=board.Bounds.Bottom);
                    Assert.Equal(Position(cpu).Y,Position(gpu).Y,1);
                    Assert.Equal(Position(ram).Y,Position(psu).Y,1);
                    Assert.True(Position(ram).Y>=Position(cpu).Y+cpu.Bounds.Height);
                    Assert.Equal(Position(cpu).X,Position(ram).X,1);
                    Assert.Equal(Position(gpu).X,Position(psu).X,1);
                    Assert.Equal(cpu.Bounds.Width,gpu.Bounds.Width,1);
                }
                Assert.Equal(expected,SensorDetails.VisibleKeys(card).Distinct().Order());
                Assert.All(card.GetLogicalDescendants().OfType<TextBlock>().Where(t=>SensorDetails.KeyOf(t).Length>0),t=>
                {
                    var right=t.TranslatePoint(new Point(t.Bounds.Width,0),card);
                    Assert.True(right.HasValue&&right.Value.X<=card.Bounds.Width+1);
                });
            }
        }
        finally{window.Close();}
        return true;
    });
}
