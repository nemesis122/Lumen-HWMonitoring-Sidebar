using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.LogicalTree;
using Lumen.Core;

namespace Lumen.App;

/// <summary>Design-matched extras retain their native data, selection and interactive controls.</summary>
public sealed class AioCompanionTile : Border,IWidget,INamed
{
    public WidgetStyle Design {get;}
    public Control NativeContent {get;}
    private readonly FontFamily? font;
    public AioCompanionTile(WidgetKind kind,WidgetStyle design,Palette palette,Control native,Settings? settings=null)
    {
        Design=design;NativeContent=native;Tag="aio-companion";
        if(settings!=null&&(AioDesigns.Panel(design)||AioDesigns.Black(design)||AioDesigns.Red(design)))
        {
            // Panel and Black designs: the extras take the design's colour, card and font, so the sidebar looks like one piece.
            if(native is Border inner){inner.Background=Brushes.Transparent;inner.BorderThickness=new Thickness(0);inner.CornerRadius=new CornerRadius(0);}
            Padding=new Thickness(4);Child=native;
            if(AioDesigns.Black(design)) font=BlackFrame(AioDesigns.BlackLookOf(settings,design),palette);
            else if(AioDesigns.Red(design)){PanelFrame(PanelTheme.For("red",palette.Chosen),palette);font=PanelTheme.Font;}
            else PanelFrame(PanelTheme.For(settings.PanelTheme,palette.Chosen),palette);
            SensorDetails.Design(native,ContentStyle(design));
            ApplyFont();
            return;
        }
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
    /// <summary>The palette the extra's content is drawn with: the design's accent for Panel and Black designs.</summary>
    public static Palette Themed(WidgetStyle design,Palette p,Settings s)
    {
        if(AioDesigns.Red(design)&&!AioDesigns.Black(design)){var r=PanelTheme.For("red",p.Chosen);return p.Themed(r.Accent,r.Section);}
        if(AioDesigns.Panel(design)){var t=PanelTheme.For(s.PanelTheme,p.Chosen);return p.Themed(t.Accent,t.Section);}
        if(AioDesigns.Black(design)){var t=BlackTheme.For(AioDesigns.BlackLookOf(s,design),p.Chosen);return p.Themed(t.A,Color.Parse("#0B0B0D"));}
        return p;
    }
    private Color See(Color c,Palette p)=>Color.FromArgb((byte)Math.Round(c.A*p.Card.A/255d),c.R,c.G,c.B);
    private void PanelFrame(PanelTheme t,Palette p)
    {
        var back=p.OwnBackground?p.Card:See(t.Section,p);
        Background=Palette.Brush(back);BorderBrush=Palette.Brush(t.Border);
        BorderThickness=t.Stripe?new Thickness(3,1.5,1.5,1.5):new Thickness(1.5);CornerRadius=new CornerRadius(t.Radius);
        if(t.Stripe)BorderBrush=new LinearGradientBrush{StartPoint=new RelativePoint(0,0,RelativeUnit.Absolute),EndPoint=new RelativePoint(3.01,0,RelativeUnit.Absolute),
            GradientStops={new(t.Accent,0),new(t.Accent,.99),new(t.Border,1)}};
    }
    private FontFamily? BlackFrame(AioDesigns.BlackLook look,Palette p)
    {
        var t=BlackTheme.For(look,p.Chosen);var a=t.A;
        var fill=p.OwnBackground?p.Card:See(Color.Parse("#0B0B0D"),p);
        Background=Palette.Brush(fill);BorderThickness=new Thickness(1);
        switch(look.Cards)
        {
            case "carbon":
                Background=new LinearGradientBrush{StartPoint=new RelativePoint(0,0,RelativeUnit.Absolute),EndPoint=new RelativePoint(6,6,RelativeUnit.Absolute),SpreadMethod=GradientSpreadMethod.Repeat,
                    GradientStops={new(See(Color.Parse("#0D0D0D"),p),0),new(See(Color.Parse("#0D0D0D"),p),.5),new(See(Color.Parse("#141414"),p),.5),new(See(Color.Parse("#141414"),p),1)}};
                BorderBrush=Palette.Brush(Color.Parse("#262626"));CornerRadius=new CornerRadius(10);break;
            case "neon": BorderBrush=Palette.Brush(BlackDraw.A(a,.7));CornerRadius=new CornerRadius(4);BoxShadow=new BoxShadows(new BoxShadow{Blur=14,Color=BlackDraw.A(a,.16)});break;
            case "line": BorderBrush=new LinearGradientBrush{StartPoint=new RelativePoint(0,0,RelativeUnit.Relative),EndPoint=new RelativePoint(0,1,RelativeUnit.Relative),
                    GradientStops={new(a,0),new(a,.02),new(Color.Parse("#1D1D20"),.021),new(Color.Parse("#1D1D20"),1)}};
                BorderThickness=new Thickness(1,2,1,1);CornerRadius=new CornerRadius(12);break;
            case "edge": BorderBrush=new LinearGradientBrush{StartPoint=new RelativePoint(0,0,RelativeUnit.Relative),EndPoint=new RelativePoint(1,1,RelativeUnit.Relative),
                    GradientStops={new(a,0),new(Colors.Transparent,.6),new(BlackDraw.A(a,.4),1)}};
                BorderThickness=new Thickness(1.5);CornerRadius=new CornerRadius(18);break;
            case "glass": Background=new LinearGradientBrush{StartPoint=new RelativePoint(0,0,RelativeUnit.Relative),EndPoint=new RelativePoint(1,1,RelativeUnit.Relative),
                    GradientStops={new(See(Color.FromArgb(46,255,255,255),p),0),new(See(Color.FromArgb(16,255,255,255),p),1)}};
                BorderBrush=Palette.Brush(Color.FromArgb(36,255,255,255));CornerRadius=new CornerRadius(20);break;
            default: BorderBrush=Palette.Brush(BlackDraw.A(a,.2));CornerRadius=new CornerRadius(16);break;
        }
        return look.Font=="inter"?null:t.Font;
    }
    /// <summary>The design's font for the extra's fixed text (title and captions), set once when the card is built.
    /// Live values and rows built later keep the standard font, so their reserved widths and line breaks never change.</summary>
    private void ApplyFont()
    {
        if(font==null)return;
        foreach(var text in NativeContent.GetLogicalDescendants().OfType<TextBlock>().Prepend(NativeContent as TextBlock).OfType<TextBlock>())
            // Live numbers keep their own font: they reserve the room of their widest form, so nothing jumps.
            if(text.FontFeatures==null&&!Equals(text.FontFamily,font))text.FontFamily=font;
    }
    public static WidgetStyle ContentStyle(WidgetStyle style)=>(AioDesigns.Black(style)?AioDesigns.BlackLayout(style):AioDesigns.Base(style)) switch
    {
        WidgetStyle.AioBlackDashboard or WidgetStyle.AioBlackRings=>WidgetStyle.RingPair,
        WidgetStyle.AioBlackTiles=>WidgetStyle.ClarityTiles,
        WidgetStyle.AioMainboardGauges or WidgetStyle.AioIsometricRings or WidgetStyle.AioOrbitNetwork=>WidgetStyle.RingPair,
        WidgetStyle.AioMainboardType or WidgetStyle.AioTypeBlock=>WidgetStyle.KeyFigures,
        WidgetStyle.AioMainboardMatrix or WidgetStyle.AioPrecision or WidgetStyle.AioInstrument=>WidgetStyle.Table,
        WidgetStyle.AioThermalGrid or WidgetStyle.AioIsometricTiles=>WidgetStyle.ClarityTiles,
        WidgetStyle.AioChromatic=>WidgetStyle.PurpleList,
        WidgetStyle.AioCarbonPulse=>WidgetStyle.Terminal,
        WidgetStyle.AioPopConsole=>WidgetStyle.MosaicPro,
        WidgetStyle.AioStudioLight=>WidgetStyle.Pure,
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
