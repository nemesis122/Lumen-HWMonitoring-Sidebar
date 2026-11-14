namespace Lumen.Core;

/// <summary>Only the eleven main widget switches have a saved selection per design.</summary>
public static class DesignWidgetSelection
{
    public static bool Main(WidgetKind kind)=>AioDesigns.Switches.Contains(kind)||
        kind is WidgetKind.Clock or WidgetKind.WorldClocks or WidgetKind.Calendar or WidgetKind.Weather;
    private static WidgetStyle Effective(Settings s,WidgetSlot widget)
    {
        if(AioDesigns.Switches.Contains(widget.Kind))
        {
            var overview=s.Widgets.FirstOrDefault(w=>w.Kind==WidgetKind.Overview&&w.Visible&&
                s.EffectivePage(w)==s.EffectivePage(widget)&&AioDesigns.Contains(w.Style));
            if(overview!=null)return overview.Style;
        }
        return widget.Style;
    }
    public static void Remember(Settings settings,WidgetSlot widget)
    {
        if(!Main(widget.Kind))return;
        settings.DesignWidgetChoices??=new();
        var design=Effective(settings,widget);
        if(!settings.DesignWidgetChoices.TryGetValue(design,out var choices)||choices==null)
            settings.DesignWidgetChoices[design]=choices=new();
        choices[widget.Kind]=widget.Visible;
    }
    public static void Apply(Settings settings,WidgetStyle design,int register=0,WidgetSlot? only=null)
    {
        var targets=settings.Widgets.Where(w=>only!=null?ReferenceEquals(w,only):
            Settings.HasStyles(w.Kind)&&(register==0||settings.EffectivePage(w)==register)).ToArray();
        var main=only?.Kind==WidgetKind.Overview
            ?settings.Widgets.Where(w=>AioDesigns.Switches.Contains(w.Kind)&&settings.EffectivePage(w)==settings.EffectivePage(only)).ToArray()
            :targets.Where(w=>Main(w.Kind)).ToArray();
        // Capture the active checks before changing styles. Clicking the same design
        // therefore preserves its current edits, and a restart keeps Visible as saved.
        foreach(var widget in main)Remember(settings,widget);
        foreach(var widget in targets)widget.Style=design;
        foreach(var widget in main)
        {
            var effective=Effective(settings,widget);
            bool defaultVisible=widget.Kind is not (WidgetKind.Cooling or WidgetKind.Power)||AioDesigns.Contains(effective);
            widget.Visible=settings.DesignWidgetChoices.TryGetValue(effective,out var choices)&&choices!=null&&choices.TryGetValue(widget.Kind,out var visible)
                ?visible:defaultVisible;
        }
        settings.AioWidgetSwitches=true;
    }
}
