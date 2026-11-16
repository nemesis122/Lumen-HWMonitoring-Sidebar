namespace Lumen.Core;

/// <summary>The retained full-case overlay; retired enum IDs are preserved for profile migration.</summary>
public static class CaseDesigns
{
    public sealed record Look(WidgetStyle Style, string Name, string Asset, string Accent, int Layout,
        bool Graphs = false, bool LargeValues = false, bool Metal = false, bool Terminal = false);

    public static readonly Look[] All =
    {
        new(WidgetStyle.CaseCyanGlass, "Case · Cyan Glas", "01-Cyan-Glas", "#20E2ED", 0),
    };
    public static bool Contains(WidgetStyle style) => All.Any(l => l.Style == style);
    public static Look For(WidgetStyle style) => All.Single(l => l.Style == style);
}
