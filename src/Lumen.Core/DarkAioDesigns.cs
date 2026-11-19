namespace Lumen.Core;

public static class DarkAioDesigns
{
    public enum Arrangement { Rack, Rail, Airflow }
    public enum Variant { Compact, Original, Gallery, Continuous }
    public sealed record Look(WidgetStyle Style, int Number, string Name, Arrangement Layout,
        string Accent, string Secondary, string? Scene = null, Variant Display = Variant.Compact);

    public static readonly Look[] All =
    {
        new(WidgetStyle.AioTitaniumRack, 62, "Titanium Rack · A", Arrangement.Rack, "#91C8F6", "#91C8F6"),
        new(WidgetStyle.AioNeonBackplate, 66, "Neon Backplate · A", Arrangement.Rail, "#F292E7", "#F292E7", "backplate"),
        new(WidgetStyle.AioAirflowMap, 80, "Airflow Map · A", Arrangement.Airflow, "#67DDEF", "#67DDEF", "airflow"),
        new(WidgetStyle.AioTitaniumRackOriginal, 92, "Titanium Rack · Original", Arrangement.Rack, "#91C8F6", "#91C8F6", null, Variant.Original),
        new(WidgetStyle.AioNeonBackplateOriginal, 93, "Neon Backplate · Original", Arrangement.Rail, "#F292E7", "#F292E7", "backplate", Variant.Original),
        new(WidgetStyle.AioAirflowMapOriginal, 94, "Airflow Map · Original", Arrangement.Airflow, "#67DDEF", "#67DDEF", "airflow", Variant.Original),
        new(WidgetStyle.AioTitaniumRackB, 95, "Titanium Rack · B", Arrangement.Rack, "#91C8F6", "#91C8F6", null, Variant.Gallery),
        new(WidgetStyle.AioNeonBackplateB, 96, "Neon Backplate · B", Arrangement.Rail, "#F292E7", "#F292E7", "backplate", Variant.Gallery),
        new(WidgetStyle.AioAirflowMapB, 97, "Airflow Map · B", Arrangement.Airflow, "#67DDEF", "#67DDEF", "airflow", Variant.Gallery),
        new(WidgetStyle.AioTitaniumRackC, 98, "Titanium Rack · C", Arrangement.Rack, "#91C8F6", "#91C8F6", null, Variant.Continuous),
        new(WidgetStyle.AioNeonBackplateC, 99, "Neon Backplate · C", Arrangement.Rail, "#F292E7", "#F292E7", "backplate", Variant.Continuous),
        new(WidgetStyle.AioAirflowMapC, 100, "Airflow Map · C", Arrangement.Airflow, "#67DDEF", "#67DDEF", "airflow", Variant.Continuous),
    };
    private static readonly Dictionary<WidgetStyle, Look> ByStyle = All.ToDictionary(item => item.Style);
    public static bool Contains(WidgetStyle style) => ByStyle.ContainsKey(style);
    public static Look For(WidgetStyle style) => ByStyle[style];
    // Keep the enum IDs readable in settings saved by earlier local builds.
    public static bool WasRemoved(WidgetStyle style) =>
        style is >= WidgetStyle.AioTitaniumRack and <= WidgetStyle.AioMidnightGrid && !Contains(style);
}

