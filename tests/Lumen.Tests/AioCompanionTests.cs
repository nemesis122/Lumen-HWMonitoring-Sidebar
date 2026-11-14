using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Lumen.App;
using Lumen.Core;
using Xunit;
using INamed = Lumen.App.INamed;

public class AioCompanionTests
{
    public static IEnumerable<object[]> Styles()=>AioDesigns.Styles.Select(s=>new object[]{s});
    [Theory,MemberData(nameof(Styles))]
    public void Every_extra_uses_the_aio_frame_and_keeps_its_selected_readings(WidgetStyle style)=>UiHost.Run(()=>
    {
        var (snapshot,history)=Render.Demo();
        foreach(var kind in Enum.GetValues<WidgetKind>().Where(k=>!AioDesigns.Hardware(k)))
        {
            var settings=Settings.Starter();var slot=settings.Widgets.Single(w=>w.Kind==kind);slot.Style=style;slot.Visible=true;
            var baseline=Cards.Create(kind,WidgetStyle.Standard,0,Palette.For(true,false),Metrics.For(Density.Fit),settings);
            ((INamed)baseline).Own(slot,0);((IWidget)baseline).Update(snapshot,history);
            var expected=SensorDetails.VisibleKeys(baseline);
            var companion=Assert.IsType<AioCompanionTile>(Cards.Create(kind,style,0,Palette.For(true,false),Metrics.For(Density.Fit),settings));
            companion.Own(slot,0);companion.Update(snapshot,history);
            Assert.Equal(style,companion.Design);
            foreach(var key in expected)Assert.Contains(key,SensorDetails.VisibleKeys(companion));
            var window=new Window{Width=380,Height=1600,Content=new StackPanel{Children={companion}}};
            try
            {
                window.Show();Dispatcher.UIThread.RunJobs();using var frame=window.CaptureRenderedFrame();
                Assert.True(companion.Bounds.Height>20);
                Assert.All(companion.GetLogicalDescendants().OfType<TextBlock>().Where(t=>SensorDetails.KeyOf(t).Length>0),value=>
                {
                    var right=value.TranslatePoint(new Point(value.Bounds.Width,0),companion)!.Value.X;
                    Assert.True(right<=companion.Bounds.Width+1,$"{style}/{kind}: {SensorDetails.KeyOf(value)} overflows.");
                });
            }
            finally{window.Close();}
        }
        return true;
    });

    [Fact]
    public void Info_clock_updates_and_sensor_switches_still_work()=>UiHost.Run(()=>
    {
        var (snapshot,history)=Render.Demo();var settings=Settings.Starter();
        var slot=settings.Widgets.Single(w=>w.Kind==WidgetKind.Clock);slot.Style=WidgetStyle.AioMainboardCores;
        var card=Cards.Create(slot.Kind,slot.Style,0,Palette.For(true,false),Metrics.For(Density.Fit),settings);((INamed)card).Own(slot,0);
        ((IWidget)card).Update(snapshot with{Time=new DateTime(2026,11,14,12,34,0)},history);
        var before=card.GetLogicalDescendants().OfType<TextBlock>().Where(t=>SensorDetails.KeyOf(t).Length>0).Select(t=>t.Text).ToArray();
        ((IWidget)card).Update(snapshot with{Time=new DateTime(2026,11,14,13,45,0)},history);
        var after=card.GetLogicalDescendants().OfType<TextBlock>().Where(t=>SensorDetails.KeyOf(t).Length>0).Select(t=>t.Text).ToArray();
        Assert.False(before.SequenceEqual(after));
        var key=SensorDetails.VisibleKeys(card).First();WidgetSensors.Select(settings,slot,key,false,snapshot);
        ((IWidget)card).Update(snapshot,history);Assert.DoesNotContain(key,SensorDetails.VisibleKeys(card));
        return true;
    });

    [Fact]
    public void Notes_and_timer_inherit_the_register_aio_without_changing_saved_preferences()
    {
        var settings=Settings.Starter();foreach(var widget in settings.Widgets.Where(w=>Settings.HasStyles(w.Kind)))widget.Style=WidgetStyle.AioMainboardMap;
        foreach(var kind in new[]{WidgetKind.Notes,WidgetKind.Timer})
        {
            var slot=settings.Widgets.Single(w=>w.Kind==kind);Assert.Equal(WidgetStyle.Standard,slot.Style);
            Assert.Equal(WidgetStyle.AioMainboardMap,AioCompanionTile.ResolveStyle(slot,settings));
            Assert.Equal(WidgetStyle.Standard,slot.Style);
        }
    }
}
