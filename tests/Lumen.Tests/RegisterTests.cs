using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Lumen.App;
using Lumen.Core;
using Xunit;

public class RegisterTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Private)!.GetValue(owner)!;
    static T Call<T>(object owner, string name, params object[] args) => (T)owner.GetType().GetMethod(name, Private)!.Invoke(owner, args)!;
    static IEnumerable<T> Within<T>(Control root) where T : class => root.GetLogicalDescendants().OfType<T>();
    static string[] Names(Control group) => Within<CheckBox>(group).Where(c => c.Name?.StartsWith("widget-") == true).Select(c => (string)c.Content!).ToArray();
    static string[] Text(Control root) => Within<TextBlock>(root).Select(t => t.Text ?? "").ToArray();
    static Control[] Groups(SettingsWindow window) => Call<Control[]>(window, "WidgetsPage").Skip(2).ToArray();
    static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    static SettingsWindow Editor(Settings s, Action? changed = null)
    {
        L.Use("en");
        typeof(SettingsWindow).GetField("pageIndex", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, 0);
        return new SettingsWindow(s, Palette.For(true, false), changed ?? (() => {}), () => new[] { "Demo display" }, new TestPlatform());
    }
    sealed class TestPlatform : Lumen.App.Platform
    {
        public override bool IsAdministrator => false;
        public override bool AutostartEnabled => false;
    }

    [Theory]
    [InlineData(Edge.Left)] [InlineData(Edge.Right)] [InlineData(Edge.Top)] [InlineData(Edge.Bottom)]
    public void EveryWidgetAppearsOnExactlyItsAssignedRegister(Edge edge)
    {
        var s = new Settings { Edge = edge, Pages = 6, Columns = 3 };
        int i = 0;
        foreach (var slot in s.Widgets) { slot.Visible = true; slot.Page = ++i % 6 + 1; slot.Column = i % 4; }
        foreach (var slot in s.Widgets)
        {
            for (int page = 1; page <= 6; page++)
                Assert.Equal(slot.Page == page, s.Layout(page: page).SelectMany(c => c).Contains(slot));
            slot.Visible = false;
            Assert.DoesNotContain(slot, s.Layout(page: slot.Page).SelectMany(c => c));
            slot.Visible = true;
        }
    }

    [Fact]
    public void ReducingRegistersKeepsAssignmentsAndRestoresThemAfterSaving()
    {
        var s = new Settings { Pages = 6, PageNames = new() { "HW", "Info", "3", "4", "5", "Extras" } };
        foreach (var slot in s.Widgets) { slot.Visible = true; slot.Page = 6; }
        s.Pages = 2;
        s = Settings.Normalize(JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(s, Settings.Json), Settings.Json)!);
        Assert.Empty(s.WidgetsOnPage(1));
        Assert.Equal(s.Widgets.Count, s.Layout(page: 2).SelectMany(c => c).Count());
        Assert.All(s.Widgets, w => Assert.Equal(6, w.Page));
        s.Pages = 6;
        Assert.Empty(s.WidgetsOnPage(2));
        Assert.Equal(s.Widgets.Count, s.WidgetsOnPage(6).Count());
        Assert.Equal("Extras", s.PageName(6));
    }

    [Fact]
    public void SettingsGroupsMatchRegistersRegardlessOfWidgetCategory() => UiHost.Run(() =>
    {
        var s = new Settings { Pages = 2, PageNames = new() { "HW", "INFO" }, Widgets = new()
        {
            new() { Kind = WidgetKind.Cpu, Page = 1 },
            new() { Kind = WidgetKind.Weather, Page = 2 },
            new() { Kind = WidgetKind.Cooling, Page = 2 },
            new() { Kind = WidgetKind.Calendar, Page = 1 },
            new() { Kind = WidgetKind.Memory, Page = 2, Visible = false }
        }};
        var window = Editor(s);
        try
        {
            var groups = Groups(window);
            Assert.Equal(new[] { "Processor", "Calendar" }, Names(groups[0]));
            Assert.Equal(new[] { "Weather", "Cooling and mainboard" }, Names(groups[1]));
            Assert.Contains("REGISTER 2: INFO", Text(groups[1]));
            Assert.Contains("2 shown", Text(groups[1]));
            Assert.Equal(new[] { "Memory" }, Names(groups[2]));
            s.Pages = 1;
            groups = Groups(window);
            Assert.Equal(new[] { "Processor", "Weather", "Cooling and mainboard", "Calendar" }, Names(groups[0]));
            Assert.Contains("WIDGETS", Text(groups[0]));
            s.Pages = 3;
            groups = Groups(window);
            Assert.Contains("0 shown", Text(groups[2]));
        }
        finally { window.Close(); }
        return true;
    });

    [Fact]
    public void RegisterSelectionRefreshesGroupsBadgeAndSidebarImmediately() => UiHost.Run(() =>
    {
        var s = new Settings { Pages = 2, PageNames = new() { "Same", "Same" }, Widgets = new()
        { new() { Kind = WidgetKind.Cooling, Page = 1 } } };
        Settings.Normalize(s);
        foreach (var slot in s.Widgets) slot.Visible = slot.Kind == WidgetKind.Cooling;
        var window = Editor(s, () => Settings.Normalize(s));
        try
        {
            Field<HashSet<WidgetKind>>(window, "expanded").Add(WidgetKind.Cooling);
            Call<object?>(window, "Build");
            var content = Field<StackPanel>(window, "content");
            var choice = Within<ComboBox>(content).Single();
            Assert.Equal(new[] { "Register 1: Same", "Register 2: Same" }, choice.ItemsSource!.Cast<string>());
            choice.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(2, s.Widgets.Single(w => w.Kind == WidgetKind.Cooling).Page);
            var groups = content.Children.Skip(2).ToArray();
            Assert.Empty(Names(groups[0]));
            Assert.Equal(new[] { "Cooling and mainboard" }, Names(groups[1]));
            Assert.Contains(Text(groups[1]), t => t.StartsWith("Register 2: Same ·"));

            s.PageNames[1] = "Information";
            Call<object?>(window, "Build");
            Assert.Contains("REGISTER 2: INFORMATION", Text(content));
            var view = new SidebarView(s, Palette.For(true, false));
            typeof(SidebarView).GetMethod("Show", Private)!.Invoke(view, new object[] { 1 });
            Assert.Empty(Field<Dictionary<WidgetKind, List<Control>>>(view, "builtCards"));
            typeof(SidebarView).GetMethod("Show", Private)!.Invoke(view, new object[] { 2 });
            Assert.Contains(WidgetKind.Cooling, Field<Dictionary<WidgetKind, List<Control>>>(view, "builtCards").Keys);
        }
        finally { window.Close(); }
        return true;
    });

    [Fact]
    public void ReorderAndHideUseTheActualRegisterGroup() => UiHost.Run(() =>
    {
        var s = new Settings { Pages = 2, PageNames = new() { "HW", "Info" }, Widgets = new()
        {
            new() { Kind = WidgetKind.Cooling, Page = 2 },
            new() { Kind = WidgetKind.Cpu, Page = 1 },
            new() { Kind = WidgetKind.Weather, Page = 2 }
        }};
        var window = Editor(s);
        try
        {
            var group = Groups(window)[1];
            var up = Within<Button>(group).First(b => Equals(ToolTip.GetTip(b), "Move up"));
            Assert.False(up.IsEnabled);
            var down = Within<Button>(group).First(b => Equals(ToolTip.GetTip(b), "Move down"));
            Click(down);
            Assert.Equal(new[] { WidgetKind.Weather, WidgetKind.Cooling }, s.WidgetsOnPage(2).Select(w => w.Kind));
            Assert.Single(s.WidgetsOnPage(1));
            group = Groups(window)[1];
            Within<CheckBox>(group).Single(c => Equals(c.Content, "Cooling and mainboard")).IsChecked = false;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(new[] { WidgetKind.Weather }, s.WidgetsOnPage(2).Select(w => w.Kind));
            var hidden = Groups(window).Last();
            Within<CheckBox>(hidden).Single().IsChecked = true;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(2, s.Widgets.Single(w => w.Kind == WidgetKind.Cooling).Page);
            Assert.Equal(new[] { "Weather", "Cooling and mainboard" }, Names(Groups(window)[1]));

            if (Environment.GetEnvironmentVariable("LUMEN_REGISTER_SCREENSHOT") is string path)
            {
                var stack = new StackPanel { Margin = new Thickness(24), Spacing = 20 };
                foreach (var item in Groups(window)) stack.Children.Add(item);
                var shot = new Window { Width = 880, Height = 280, RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark, Background = Palette.Brush(Palette.For(true,false).Surface), Content = stack };
                try { shot.Show(); Dispatcher.UIThread.RunJobs(); using var frame = shot.CaptureRenderedFrame()!; frame.Save(path); }
                finally { shot.Close(); }
            }
        }
        finally { window.Close(); }
        return true;
    });
}
