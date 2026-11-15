using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Lumen.App;
using Lumen.Core;
using Xunit;

public class TextColourTests
{
    public static IEnumerable<object[]> Styles() => AioDesigns.Styles.Select(s => new object[] { s });

    [Fact]
    public void Invalid_colours_fall_back_to_the_design()
    {
        var s = Settings.Starter(); s.ValueColor = "orange"; s.LabelColor = "#06b6d4";
        Settings.Normalize(s);
        Assert.Equal("", s.ValueColor); Assert.Equal("#06B6D4", s.LabelColor);
    }

    [Theory, MemberData(nameof(Styles))]
    public void Values_and_labels_take_the_chosen_colours_and_keep_them_after_updates(WidgetStyle style) => UiHost.Run(() =>
    {
        var (snap, h) = Render.Demo(); var s = Settings.Starter(); var slot = s.Widgets.Single(w => w.Kind == WidgetKind.Overview); slot.Style = style;
        s.ValueColor = "#F97316"; s.LabelColor = "#06B6D4";
        var card = new AioTile(style, Palette.For(true, false), Metrics.For(Density.Fit), s); card.Own(slot, 0); card.Update(snap, h);
        TextColours.Apply(card, s); card.Update(snap with { Cpu = snap.Cpu! with { Temperature = 61 } }, h); TextColours.Apply(card, s);
        var values = card.ValueTexts;
        Color Of(TextBlock t) => ((ISolidColorBrush)t.Foreground!).Color;
        bool Is(TextBlock t, string hex) { var c = Of(t); var o = Color.Parse(hex); return c.R == o.R && c.G == o.G && c.B == o.B; }
        var texts = card.GetLogicalDescendants().OfType<TextBlock>().Where(t => t.IsVisible && !string.IsNullOrWhiteSpace(t.Text) && t.Foreground is ISolidColorBrush).ToList();
        // No plain white or grey text is left: each one took the value or the label colour.
        Assert.DoesNotContain(texts, t => TextColours.Neutral(Of(t)));
        Assert.Contains(texts, t => values.Contains(t) && Is(t, "#F97316"));
        Assert.Contains(texts, t => !values.Contains(t) && Is(t, "#06B6D4"));
        return true;
    });
}
