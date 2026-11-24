using System.Reflection;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Lumen.App;
using Lumen.Core;
using Xunit;

public class Width640Tests
{
    [Theory]
    [InlineData(440,440)][InlineData(520,520)][InlineData(640,640)][InlineData(9000,640)][InlineData(239,240)][InlineData(double.NaN,300)]
    public void Normalization_accepts_extended_width_and_preserves_old_values(double width,double expected)
        =>Assert.Equal(expected,Settings.Normalize(new Settings{Width=width}).Width);
    [Theory]
    [InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)][InlineData(6)]
    public void Extended_width_round_trips_and_window_stays_within_work_area(int columns)
    {
        var settings=Settings.Starter();settings.Width=640;settings.Columns=columns;
        var saved=Settings.Normalize(JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(settings,Settings.Json),Settings.Json)!);
        Assert.Equal(640,saved.Width);Assert.Equal(columns,saved.Columns);Assert.Equal(2,saved.Pages);
        Assert.Equal(Math.Min(1920,SidebarView.WindowWidth(saved)),SidebarView.WindowWidth(saved,1920));
        Assert.InRange(SidebarView.WindowWidth(saved,1280),1,1280);
    }
    private sealed class TestPlatform:Lumen.App.Platform{public override bool IsAdministrator=>false;public override bool AutostartEnabled=>false;}
    [Fact]
    public void Placement_slider_reaches_640_and_updates_the_saved_setting()=>UiHost.Run(()=>
    {
        var settings=Settings.Starter();var window=new SettingsWindow(settings,Palette.For(true,false),()=>Settings.Normalize(settings),()=>new[]{"Demo"},new TestPlatform());
        try
        {
            var sections=(Control[])typeof(SettingsWindow).GetMethod("PlacementPage",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(window,null)!;
            var slider=Assert.Single(sections.SelectMany(c=>c.GetLogicalDescendants().OfType<Slider>()),s=>s.Minimum==240&&s.Maximum==640);
            slider.Value=640;Assert.Equal(640,settings.Width);
        }
        finally{window.Close();Avalonia.Threading.Dispatcher.UIThread.RunJobs();}
        return true;
    });
    public static IEnumerable<object[]> Designs=>Settings.Selectable.Select(s=>new object[]{s});
    [Theory]
    [InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)][InlineData(6)]
    public void Wide_case_columns_fit_a_1920_work_area(int columns)=>UiHost.Run(()=>
    {ColumnHeightTests.Audit(WidgetStyle.CaseCyanGlass,columns,false,640,1920);return true;});
    [Theory,MemberData(nameof(Designs))]
    public void Every_design_renders_at_640_and_fills_available_height(WidgetStyle style)=>UiHost.Run(()=>
    {ColumnHeightTests.Audit(style,1,false,640);return true;});
}
