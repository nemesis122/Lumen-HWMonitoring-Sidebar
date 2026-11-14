using System.Text.Json;
using Lumen.Core;
using Xunit;

public class DesignWidgetSelectionTests
{
    static WidgetKind[] Selected(Settings settings)=>settings.Widgets.Where(w=>DesignWidgetSelection.Main(w.Kind)&&w.Visible).Select(w=>w.Kind).Order().ToArray();
    static readonly WidgetKind[] Normal={WidgetKind.Cpu,WidgetKind.Gpu,WidgetKind.Memory,WidgetKind.Drives,WidgetKind.Network,
        WidgetKind.Clock,WidgetKind.WorldClocks,WidgetKind.Calendar,WidgetKind.Weather};
    [Fact]
    public void Every_new_design_starts_with_the_correct_main_widgets()
    {
        var s=Settings.Starter();
        foreach(var design in new[]{WidgetStyle.MeasureTape,WidgetStyle.AioMainboardCores,WidgetStyle.Table,WidgetStyle.AioPrecision})
        {
            DesignWidgetSelection.Apply(s,design);
            var expected=AioDesigns.Contains(design)?Normal.Concat(new[]{WidgetKind.Cooling,WidgetKind.Power}):Normal;
            Assert.Equal(expected.Order(),Selected(s));
        }
    }
    [Fact]
    public void A_design_restores_its_own_checks_after_other_designs_and_a_restart()
    {
        var s=Settings.Starter();DesignWidgetSelection.Apply(s,WidgetStyle.AioMainboardCores);
        s.Widgets.Single(w=>w.Kind==WidgetKind.Cooling).Visible=false;
        s.Widgets.Single(w=>w.Kind==WidgetKind.Network).Visible=false;
        var coreChoice=Selected(s);
        DesignWidgetSelection.Apply(s,WidgetStyle.MeasureTape);Assert.Equal(Normal.Order(),Selected(s));
        s.Widgets.Single(w=>w.Kind==WidgetKind.Clock).Visible=false;var tapeChoice=Selected(s);
        s=Settings.Normalize(JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(s,Settings.Json),Settings.Json)!);
        Assert.Equal(tapeChoice,Selected(s));
        DesignWidgetSelection.Apply(s,WidgetStyle.AioMainboardCores);Assert.Equal(coreChoice,Selected(s));
        DesignWidgetSelection.Apply(s,WidgetStyle.Table);Assert.Equal(Normal.Order(),Selected(s));
        DesignWidgetSelection.Apply(s,WidgetStyle.MeasureTape);Assert.Equal(tapeChoice,Selected(s));
    }
    [Fact]
    public void Same_design_preserves_active_edits_and_extras_and_subsensors_are_not_reset()
    {
        var s=Settings.Starter();DesignWidgetSelection.Apply(s,WidgetStyle.AioMainboardCores);
        var cpu=s.Widgets.Single(w=>w.Kind==WidgetKind.Cpu);cpu.Visible=false;cpu.HiddenSensors.Add("cpu.clock");
        var fps=s.Widgets.Single(w=>w.Kind==WidgetKind.Fps);fps.Visible=true;
        DesignWidgetSelection.Apply(s,WidgetStyle.AioMainboardCores);Assert.False(cpu.Visible);
        DesignWidgetSelection.Apply(s,WidgetStyle.Table);Assert.True(cpu.Visible);Assert.True(fps.Visible);Assert.Contains("cpu.clock",cpu.HiddenSensors);
        Assert.All(s.DesignWidgetChoices.Values,choices=>Assert.All(choices.Keys,k=>Assert.True(DesignWidgetSelection.Main(k))));
    }
    [Fact]
    public void Register_only_selection_keeps_the_other_register_and_widget_order()
    {
        var s=Settings.Starter();var before=s.Widgets.Where(w=>w.Page==2).Select(w=>(w.Kind,w.Visible,w.Style)).ToArray();
        var order=s.Widgets.Select(w=>w.Kind).ToArray();
        DesignWidgetSelection.Apply(s,WidgetStyle.AioMainboardCores,1);
        Assert.Equal(7,s.Widgets.Count(w=>w.Page==1&&w.Visible));
        Assert.Equal(before,s.Widgets.Where(w=>w.Page==2).Select(w=>(w.Kind,w.Visible,w.Style)));
        Assert.Equal(order,s.Widgets.Select(w=>w.Kind));
    }
    [Fact]
    public void Explicit_overview_design_remembers_the_hardware_it_controls()
    {
        var s=Settings.Starter();var overview=s.Widgets.Single(w=>w.Kind==WidgetKind.Overview);overview.Visible=true;
        DesignWidgetSelection.Apply(s,WidgetStyle.AioMainboardCores,only:overview);
        Assert.All(s.Widgets.Where(w=>AioDesigns.Switches.Contains(w.Kind)),w=>Assert.True(w.Visible));
        var cooling=s.Widgets.Single(w=>w.Kind==WidgetKind.Cooling);cooling.Visible=false;DesignWidgetSelection.Remember(s,cooling);
        DesignWidgetSelection.Apply(s,WidgetStyle.Table,only:overview);
        DesignWidgetSelection.Apply(s,WidgetStyle.AioMainboardCores,only:overview);Assert.False(cooling.Visible);
    }
}
