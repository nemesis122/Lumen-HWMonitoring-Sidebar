namespace Lumen.Core;

/// <summary>Brand-colour case editions; hardware identities always come from the sensor source.</summary>
public static class CaseEditionDesigns
{
    public enum Arrangement { Amd, Nvidia, Intel, Studio, Command }
    public sealed record Look(WidgetStyle Style,string Name,string Asset,string Accent,Arrangement Layout)
    {
        public string WideAsset=>Layout==Arrangement.Command?Asset:Asset+"-wide";
    }
    public static readonly Look[] All=
    {
        new(WidgetStyle.CaseAmdEdition,"Case · AMD Edition","Case-AMD","#F04444",Arrangement.Amd),
        new(WidgetStyle.CaseNvidiaEdition,"Case · NVIDIA Edition","Case-NVIDIA","#76D900",Arrangement.Nvidia),
        new(WidgetStyle.CaseIntelEdition,"Case · Intel Edition","Case-Intel","#12B9F3",Arrangement.Intel),
        new(WidgetStyle.CaseStudio,"Case · Studio","Case-Studio","#FFB94D",Arrangement.Studio),
        new(WidgetStyle.CaseCommandCenter,"Case · Command Center","Case-Command","#20E2ED",Arrangement.Command)
    };
    public static bool Contains(WidgetStyle style)=>All.Any(l=>l.Style==style);
    public static Look For(WidgetStyle style)=>All.Single(l=>l.Style==style);
}
