using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lumen.App;
using Lumen.Core;
using Xunit;

public class ColumnHeightTests
{
    public static IEnumerable<object[]> Dashboards() => AioDesigns.DashboardStyles.SelectMany(s => new[] { 2, 3, 4, 5, 6 }.Select(c => new object[] { s, c }));
    public static IEnumerable<object[]> AllDesigns() => Settings.Selectable.SelectMany(s => new[] { 2, 3, 4, 5, 6 }.Select(c => new object[] { s, c }));

    public static void Audit(WidgetStyle design, int columns, bool dashboard,double width=300,double maximumWidth=double.PositiveInfinity)
    {
        int warningCount = UiHost.LayoutWarnings.Count;
        Ui.UseFont("inter");
        var settings = Settings.Starter(); settings.Width = width; settings.Columns = columns; settings.Density = Density.Fit;
        settings.PageNames = new() { "HW", "INFO" };
        DesignWidgetSelection.Apply(settings, design);
        var (snapshot, history) = Render.Demo();
        typeof(SidebarView).GetField("page", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.SetValue(null, 1);
        var sidebar = new SidebarView(settings, Palette.For(true, false)); sidebar.Update(snapshot, history);
        var window = new Window { Content = sidebar, Width = SidebarView.WindowWidth(settings,maximumWidth), Height = 1040 };
        try
        {
            window.Show();
            for (int pass = 0; pass < 3; pass++)
            {
                window.Height = pass == 1 ? 1400 : pass == 2 ? 900 : 1040;
                Dispatcher.UIThread.RunJobs(); using var image = window.CaptureRenderedFrame();
                var fit = sidebar.GetLogicalDescendants().OfType<FitPanel>().First();
                foreach (var column in fit.Children.OfType<StackPanel>().Where(s => s.Children.Any(c => c.IsVisible)))
                {
                    var bottom = column.TranslatePoint(new Point(0, column.Bounds.Height), fit)!.Value.Y;
                    Assert.True(Math.Abs(bottom - fit.Bounds.Height) <= 3,
                        $"{design}/{columns}/pass {pass}: bottom {bottom}, fit {fit.Bounds.Height}, column {column.Bounds.Height}, desired {column.DesiredSize.Height}; " +
                        string.Join(';', column.Children.OfType<AioTile>().Select(d => $"card {d.Bounds.Height} desired {d.DesiredSize.Height} growth {d.DashboardHeightGrowth}")));
                }
                if (dashboard)
                {
                    var card = Assert.Single(sidebar.GetLogicalDescendants().OfType<AioTile>());
                    Assert.True(card.Padding.Bottom < 24, "Empty padding must not masquerade as filled sensor panels");
                    var flows = card.GetLogicalDescendants().OfType<DashboardFlow>().ToArray();
                    // As many columns as asked for, or one per part when there are fewer parts (since 5.7.0 the demo PC,
                    // which has no voltage rails, has no power card).
                    if (columns >= 3) Assert.Contains(flows, f => f.VisibleColumns == Math.Min(columns, f.Items));
                    foreach (var flow in flows)
                        foreach (var col in flow.Children.OfType<DashboardVertical>())
                        {
                            var last = col.Children.LastOrDefault(c => c.IsVisible); if (last == null) continue;
                            double bottom = last.TranslatePoint(new Point(0, last.Bounds.Height), flow)!.Value.Y;
                            Assert.InRange(bottom - flow.Bounds.Height, -3, 3);
                        }
                    foreach (var pair in card.GetLogicalDescendants().OfType<DashboardColumns>())
                        foreach (var col in pair.Children.OfType<DashboardVertical>())
                        {
                            var last = col.Children.LastOrDefault(c => c.IsVisible); if (last == null) continue;
                            double bottom = last.TranslatePoint(new Point(0, last.Bounds.Height), col)!.Value.Y;
                            Assert.InRange(bottom - col.Bounds.Height, -3, 3);
                        }
                    string? output = Environment.GetEnvironmentVariable("LUMEN_HEIGHT_OUTPUT");
                    if (pass == 2 && output != null) { Directory.CreateDirectory(output); image!.Save(System.IO.Path.Combine(output, $"{design}-{columns}-columns.png")); }
                }
                sidebar.Update(snapshot with { Cpu = snapshot.Cpu! with { Temperature = 50 + pass * 7 } }, history);
            }
            Assert.Equal(columns, settings.Columns); Assert.Equal(width, settings.Width); Assert.Equal(Density.Fit, settings.Density);
            Assert.Equal(new[] { "HW", "INFO" }, settings.PageNames);
            if (dashboard) Assert.DoesNotContain(UiHost.LayoutWarnings.Skip(warningCount), message => message.Contains("cycle", StringComparison.OrdinalIgnoreCase));
        }
        finally { window.Close(); }
    }
    [Theory, MemberData(nameof(Dashboards))]
    public void New_layouts_use_requested_columns_and_fill_to_work_area_bottom(WidgetStyle design, int columns) => UiHost.Run(() => { Audit(design, columns, true); return true; });
    [Theory, MemberData(nameof(AllDesigns))]
    public void Every_design_fills_the_available_height_in_automatic_mode(WidgetStyle design, int columns) => UiHost.Run(() => { Audit(design, columns, false); return true; });
}
