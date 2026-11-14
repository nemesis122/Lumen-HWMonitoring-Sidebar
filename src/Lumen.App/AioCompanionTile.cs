using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

/// <summary>Design-matched extras retain their native data, selection and interactive controls.</summary>
public sealed class AioCompanionTile : Border,IWidget,INamed
{
    public WidgetStyle Design {get;}
    public Control NativeContent {get;}
    public AioCompanionTile(WidgetKind kind,WidgetStyle design,Palette palette,Control native)
    {
        Design=design;NativeContent=native;Tag="aio-companion";
        bool violet=kind is WidgetKind.WorldClocks or WidgetKind.Weather or WidgetKind.Media or WidgetKind.Ping or WidgetKind.DriveHealth;
        var accent=palette.Chosen??(design is WidgetStyle.AioChromatic or WidgetStyle.AioCarbonPulse?MainboardMapColors.Violet:
            violet?MainboardMapColors.Violet:MainboardMapColors.Cyan);
        var dark=design switch{WidgetStyle.AioPrecision=>"#0D172B",WidgetStyle.AioChromatic=>"#170D2D",_=>"#0C1017"};
        var back=palette.OwnBackground?palette.Card:palette.Dark?Color.Parse(dark):palette.Card;
        Background=Palette.Brush(Color.FromArgb(palette.Card.A,back.R,back.G,back.B));
        BorderBrush=Palette.Brush(Palette.Mix(palette.Track,accent,.65));
        BorderThickness=design==WidgetStyle.AioMainboardMatrix?new Thickness(0,1,0,0):
            design==WidgetStyle.AioMainboardType?new Thickness(3,0,0,0):new Thickness(1);
        CornerRadius=new CornerRadius(design==WidgetStyle.AioMainboardMatrix?0:design==WidgetStyle.AioMainboardGauges?10:6);
        Padding=new Thickness(4);
        if(native is Border border){border.Background=Brushes.Transparent;border.BorderThickness=new Thickness(0);border.CornerRadius=new CornerRadius(0);}
        bool stripe=design is WidgetStyle.AioMainboardMap or WidgetStyle.AioMainboardDesktop or WidgetStyle.AioSignalRack or WidgetStyle.AioMainboardStrips;
        var content=new StackPanel{Spacing=2};
        if(stripe)content.Children.Add(new Border{Height=2,Background=Palette.Brush(accent),Margin=new Thickness(6,2,6,0)});
        content.Children.Add(native);Child=content;
        SensorDetails.Design(native,ContentStyle(design));
    }
    public static WidgetStyle ContentStyle(WidgetStyle style)=>style switch
    {
        WidgetStyle.AioMainboardGauges or WidgetStyle.AioIsometricRings or WidgetStyle.AioOrbitNetwork=>WidgetStyle.RingPair,
        WidgetStyle.AioMainboardType or WidgetStyle.AioTypeBlock=>WidgetStyle.KeyFigures,
        WidgetStyle.AioMainboardMatrix or WidgetStyle.AioPrecision or WidgetStyle.AioInstrument=>WidgetStyle.Table,
        WidgetStyle.AioThermalGrid or WidgetStyle.AioIsometricTiles=>WidgetStyle.ClarityTiles,
        WidgetStyle.AioChromatic=>WidgetStyle.PurpleList,
        WidgetStyle.AioCarbonPulse=>WidgetStyle.Terminal,
        WidgetStyle.AioPopConsole=>WidgetStyle.MosaicPro,
        WidgetStyle.AioStudioLight=>WidgetStyle.PureWidgets,
        WidgetStyle.AioAuroraFusion=>WidgetStyle.Meridian,
        _=>WidgetStyle.Table
    };
    // Notes and Timer have no individual style picker. They follow the AIO on their register,
    // including settings saved before AIO companions existed, without rewriting saved preferences.
    public static WidgetStyle ResolveStyle(WidgetSlot slot,Settings settings)
    {
        if(Settings.HasStyles(slot.Kind)||slot.Style!=WidgetStyle.Standard)return slot.Style;
        var styles=settings.Widgets.Where(w=>Settings.HasStyles(w.Kind)&&w.Visible&&settings.EffectivePage(w)==settings.EffectivePage(slot))
            .Select(w=>w.Style).Distinct().ToArray();
        return styles.Length==1&&AioDesigns.Contains(styles[0])?styles[0]:slot.Style;
    }
    public void Own(WidgetSlot slot,int index){if(NativeContent is INamed named)named.Own(slot,index);}
    public void Update(Snapshot snapshot,History history)=>((IWidget)NativeContent).Update(snapshot,history);
}
