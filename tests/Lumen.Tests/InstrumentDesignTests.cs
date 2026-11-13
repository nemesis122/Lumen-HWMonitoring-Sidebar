using Avalonia.Controls;
using Avalonia.LogicalTree;
using Lumen.App;
using Lumen.Core;
using Xunit;

public class InstrumentDesignTests
{
    public static IEnumerable<object[]> Styles() => InstrumentDesigns.Styles.Select(s => new object[] {s});

    [Theory, MemberData(nameof(Styles))]
    public void Gpu_history_uses_the_selected_device_and_each_history_switch(WidgetStyle style) => UiHost.Run(() =>
    {
        var (snapshot,history) = Render.Demo(); var s = Settings.Starter();
        var slot = s.Widgets.Single(w => w.Kind == WidgetKind.Gpu);
        var card = Cards.Create(WidgetKind.Gpu,style,0,Palette.For(true,false),Metrics.For(Density.Fit),s);
        var gpu = GpuGroup.Visible(snapshot,s).First();
        ((IWidget)card).Update(snapshot,history);
        var charts = card.GetLogicalDescendants().OfType<AreaChart>().ToArray();
        Assert.Equal(2,charts.Length);
        Assert.Equal(history.Read("gpu."+gpu.Id+".temp"),charts[0].Values);
        Assert.Equal(history.Read("gpu."+gpu.Id+".load"),charts[1].Values);
        Assert.NotEmpty(charts[0].Values);
        WidgetSensors.Select(s,slot,"gpu.temp.history",false,snapshot); ((IWidget)card).Update(snapshot,history);
        Assert.Equal(history.Read("gpu."+gpu.Id+".load"),Assert.Single(card.GetLogicalDescendants().OfType<AreaChart>()).Values);
        WidgetSensors.Select(s,slot,"gpu.chart",false,snapshot); ((IWidget)card).Update(snapshot,history);
        Assert.Empty(card.GetLogicalDescendants().OfType<AreaChart>());
        return true;
    });

    [Theory, MemberData(nameof(Styles))]
    public void Memory_bar_switch_changes_the_graphic_without_losing_the_values(WidgetStyle style) => UiHost.Run(() =>
    {
        var (snapshot,history) = Render.Demo(); var s = Settings.Starter();
        var slot = s.Widgets.Single(w => w.Kind == WidgetKind.Memory);
        var card = Cards.Create(WidgetKind.Memory,style,0,Palette.For(true,false),Metrics.For(Density.Fit),s);
        ((IWidget)card).Update(snapshot,history);
        Assert.Single(card.GetLogicalDescendants().OfType<InstrumentScale>());
        var before = SensorDetails.VisibleKeys(card);
        WidgetSensors.Select(s,slot,"mem.bar",false,snapshot); ((IWidget)card).Update(snapshot,history);
        Assert.Empty(card.GetLogicalDescendants().OfType<InstrumentScale>());
        Assert.Equal(before.Order(),SensorDetails.VisibleKeys(card).Order());
        return true;
    });
}
