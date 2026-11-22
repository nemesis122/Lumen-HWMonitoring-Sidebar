namespace Lumen.Core;

/// <summary>
/// The Cyberpunk and Neumorph designs (5.1.0, chosen by the user from the style drafts): one AIO card each, every value
/// bound to its sensor like in all AIO designs. Each design has a fixed look; a colour chosen in Appearance replaces the
/// accents.
/// </summary>
public static class NeoDesigns
{
    public enum Family { Cyber, Neumorph }
    /// <summary>Cyber: Stack (one panel per part) or Split (processor and graphics side by side).
    /// Neumorph: Dials (2 × 2 rings), Hero (one large processor ring) or Bars (one bar per part).</summary>
    public enum Arrangement { Stack, Split, Dials, Hero, Bars }
    /// <param name="Accents">Four accents (processor, graphics, memory, drive); Cyber designs use the first two.</param>
    /// <param name="Surface">Card colour; Neumorph also draws its light and dark shadows from it.</param>
    public sealed record Look(WidgetStyle Style, string Name, Family Kind, Arrangement Layout, string[] Accents, string Surface,
        bool Parts = false, bool RedParts = false);

    public static readonly Look[] All =
    {
        new(WidgetStyle.AioCyberArasaka, "Cyber Arasaka", Family.Cyber, Arrangement.Split, new[] { "#FF2238", "#F2F2F2" }, "#070203"),
        new(WidgetStyle.AioCyberNetrunner, "Cyber Netrunner", Family.Cyber, Arrangement.Stack, new[] { "#FF2BD6", "#00F0FF" }, "#0D0612"),
        new(WidgetStyle.AioCyberMilitech, "Cyber Militech", Family.Cyber, Arrangement.Split, new[] { "#39FF14", "#C8FFB8" }, "#030803"),
        new(WidgetStyle.AioCyberAfterlife, "Cyber Afterlife", Family.Cyber, Arrangement.Stack, new[] { "#FCEE0A", "#00F0FF" }, "#0A0A0F", Parts: true),
        new(WidgetStyle.AioCyberRedline, "Cyber Redline", Family.Cyber, Arrangement.Split, new[] { "#FF2238", "#FFFFFF" }, "#080203", Parts: true, RedParts: true),
        new(WidgetStyle.AioCyberBlackIce, "Cyber Black Ice", Family.Cyber, Arrangement.Stack, new[] { "#5CE1FF", "#E6F7FF" }, "#04090D"),
        new(WidgetStyle.AioNeoBlackRed, "Neumorph Black Red", Family.Neumorph, Arrangement.Dials, new[] { "#FF2238", "#FF2238", "#FF2238", "#FF2238" }, "#121316"),
        new(WidgetStyle.AioNeoHero, "Neumorph Hero", Family.Neumorph, Arrangement.Hero, new[] { "#FF7A59", "#59A8FF", "#59FFB2", "#C08CFF" }, "#22252B"),
        new(WidgetStyle.AioNeoParts, "Neumorph Parts", Family.Neumorph, Arrangement.Bars, new[] { "#FF7A59", "#59A8FF", "#59FFB2", "#C08CFF" }, "#22252B", Parts: true),
        new(WidgetStyle.AioNeoRedParts, "Neumorph Red Parts", Family.Neumorph, Arrangement.Bars, new[] { "#FF2238", "#FF2238", "#FF2238", "#FF2238" }, "#1C1416", Parts: true, RedParts: true),
        new(WidgetStyle.AioNeoCyanHero, "Neumorph Cyan", Family.Neumorph, Arrangement.Hero, new[] { "#22D3EE", "#22D3EE", "#22D3EE", "#22D3EE" }, "#121316"),
        new(WidgetStyle.AioNeoMono, "Neumorph Mono", Family.Neumorph, Arrangement.Bars, new[] { "#F2F2F2", "#F2F2F2", "#F2F2F2", "#F2F2F2" }, "#121316"),
    };
    private static readonly Dictionary<WidgetStyle, Look> ByStyle = All.ToDictionary(l => l.Style);
    public static bool Contains(WidgetStyle style) => ByStyle.ContainsKey(style);
    public static Look For(WidgetStyle style) => ByStyle[style];
}
