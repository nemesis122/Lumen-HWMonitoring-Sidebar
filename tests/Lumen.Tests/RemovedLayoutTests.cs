using System.Text.Json;
using Lumen.Core;
using Xunit;

public class RemovedLayoutTests
{
    public static IEnumerable<object[]> Removed() => Settings.RemovedLayouts.Append(WidgetStyle.AioMinMax).Select(s=>new object[]{s});
    [Fact]
    public void Exactly_the_thirteen_requested_styles_are_removed_and_cyan_is_kept()
    {
        var expected=new[]{WidgetStyle.CaseAmberTechnical,WidgetStyle.CaseWhiteMinimal,WidgetStyle.CaseGreenTerminal,
            WidgetStyle.CaseBlueModules,WidgetStyle.CaseRedPerformance,WidgetStyle.CaseVioletOrbit,
            WidgetStyle.CaseIndustrialSteel,WidgetStyle.CaseCompactSensors,WidgetStyle.CaseLargeValues,
            WidgetStyle.AioSensorTable,WidgetStyle.AioTwoHalves,WidgetStyle.AioSummaryDetails,WidgetStyle.AioCasePlan};
        Assert.Equal(expected.Order(),Settings.RemovedLayouts.Order());
        foreach(var style in expected)
        {
            Assert.DoesNotContain(style,Settings.Selectable);Assert.DoesNotContain(style,AioDesigns.Styles);
            Assert.DoesNotContain(style,AioDesigns.DashboardStyles);Assert.Contains(style,Settings.Retired);
        }
        Assert.Contains(WidgetStyle.CaseCyanGlass,Settings.Selectable);
        foreach(var style in new[]{WidgetStyle.AioBoardCard,WidgetStyle.AioDoubleTable,WidgetStyle.AioQuadrants,WidgetStyle.AioNetworkCenter})
            Assert.Contains(style,Settings.Selectable);
    }
    [Theory,MemberData(nameof(Removed))]
    public void Saved_profiles_migrate_to_cyan_and_keep_sensor_fonts_and_placement(WidgetStyle removed)
    {
        var settings=Settings.Starter();settings.Width=420;settings.Columns=3;settings.Density=Density.Compact;
        settings.Pages=2;settings.PageNames=new(){"HW","INFO"};settings.TitleFont="permanent-marker";settings.ValueFont="jetbrains-mono";
        var slot=settings.Widgets.Single(w=>w.Kind==WidgetKind.Overview);slot.Style=removed;
        slot.Name="My sensors";slot.HiddenSensors.Add("aio.cpu.clock");slot.ShowHardware=false;
        var normalized=Settings.Normalize(JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(settings,Settings.Json),Settings.Json)!);
        var saved=normalized.Widgets.Single(w=>w.Kind==WidgetKind.Overview);
        Assert.Equal(WidgetStyle.CaseCyanGlass,saved.Style);Assert.Equal("My sensors",saved.Name);
        Assert.Contains("aio.cpu.clock",saved.HiddenSensors);Assert.False(saved.ShowHardware);
        Assert.Equal(420,normalized.Width);Assert.Equal(3,normalized.Columns);Assert.Equal(Density.Compact,normalized.Density);
        Assert.Equal(new[]{"HW","INFO"},normalized.PageNames.Take(2));Assert.Equal("permanent-marker",normalized.TitleFont);Assert.Equal("jetbrains-mono",normalized.ValueFont);
    }
}
