using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Lumen.App;
using Lumen.Core;
using Xunit;

public class HorizontalReadabilityTests
{
    public static IEnumerable<object[]> Designs() => FavoriteTile.Styles.Concat(PureLoadIndicator.Styles)
        .Concat(new[] { WidgetStyle.PureWidgets, WidgetStyle.PureWidgetsRight }).Select(s => new object[] { s });

    [Theory]
    [MemberData(nameof(Designs))]
    public void Wide_flat_bar_keeps_readings_legible_instead_of_shrinking_around_squares(WidgetStyle style) => UiHost.Run(() =>
    {
        L.Use("de");
        // Other register tests may leave the app-wide selection on INFO. Measure HW explicitly.
        typeof(SidebarView).GetField("page", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.SetValue(null, 1);
        var (snap, history) = Render.Demo();
        var problems = new List<string>();
        foreach (var edge in new[] { Edge.Top, Edge.Bottom })
        foreach (var size in new[] { new Size(1280, 240), new Size(1920, 320), new Size(2560, 376) })
        {
            var settings = Settings.Starter(); settings.Edge = edge; settings.BarHeight = size.Height; settings.BarFullWidth = true;
            foreach (var slot in settings.Widgets) if (Settings.HasStyles(slot.Kind)) slot.Style = style;
            var view = new SidebarView(settings, Palette.For(true, false));
            var window = new Window { Content = view, Width = size.Width, Height = size.Height };
            window.Show(); view.Update(snap, history);
            for (int i = 0; i < 5; i++) { Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame(); }
            var cards = view.GetLogicalDescendants().OfType<SummaryTile>().ToArray();
            Assert.NotEmpty(cards);
            for (int cardIndex = 0; cardIndex < cards.Length; cardIndex++)
            {
                var card = cards[cardIndex];
                var numbers = card.GetLogicalDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && t.FontFeatures != null && !string.IsNullOrWhiteSpace(t.Text)
                    && t.GetLogicalAncestors().OfType<Control>().All(c => c.Opacity > 0)).ToArray();
                double Effective(TextBlock t)
                {
                    var a = t.TranslatePoint(default, view)!.Value; var b = t.TranslatePoint(new Point(1, 0), view)!.Value;
                    return t.FontSize * Math.Abs(b.X - a.X);
                }
                double minimum = numbers.Select(Effective).DefaultIfEmpty(99).Min();
                double biggest = numbers.Select(Effective).DefaultIfEmpty(0).Max();
                if (minimum < 6.9) problems.Add($"{edge} {size}: {card.GetType().Name}: smallest reading {minimum:0.0}px");
                if (size.Width >= 1920 && cardIndex < 2 && biggest < 20) problems.Add($"{edge} {size}: {card.GetType().Name}: CPU/GPU main reading only {biggest:0.0}px");
            }
            window.Close();
        }
        Assert.True(problems.Count == 0, style + "\n" + string.Join("\n", problems));
        return true;
    });
}
