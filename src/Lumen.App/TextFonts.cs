using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public enum TextRole { Label, Title, Value, Graph }

/// <summary>Explicit semantic roles; model names containing digits remain labels.</summary>
public static class TextFonts
{
    public static readonly AttachedProperty<TextRole> RoleProperty =
        AvaloniaProperty.RegisterAttached<Control, TextRole>("Role", typeof(TextFonts), TextRole.Label);

    public static T Mark<T>(T control, TextRole role) where T : Control
    {
        control.SetValue(RoleProperty, role);
        if (control is not TextBlock)
            foreach (var text in control.GetLogicalDescendants().OfType<TextBlock>()) text.SetValue(RoleProperty, role);
        return control;
    }

    public static bool Active(Settings s) => s.TitleFont.Length > 0 || s.LabelFont.Length > 0 || s.ValueFont.Length > 0 || s.GraphFont.Length > 0;

    public static TextRole RoleOf(TextBlock text)
    {
        var role = text.GetValue(RoleProperty);
        if (text.IsSet(RoleProperty)) return role;
        return text is AioValueText || text.FontFeatures != null || SensorDetails.KeyOf(text).Length > 0 ? TextRole.Value : TextRole.Label;
    }

    public static string Key(Settings s, TextRole role) => role switch
    {
        TextRole.Title => s.TitleFont, TextRole.Value => s.ValueFont,
        TextRole.Graph => s.GraphFont, _ => s.LabelFont
    };

    public static void Apply(Control root, Settings s)
    {
        if (!Active(s)) return;
        foreach (var text in root.GetLogicalDescendants().OfType<TextBlock>().Prepend(root as TextBlock))
        {
            if (text == null) continue;
            var key = Key(s, RoleOf(text));
            if (key.Length == 0) continue;
            var family = Ui.FamilyFor(key);
            if (!Equals(text.FontFamily, family)) text.FontFamily = family;
            text.FontStretch = key == "bahnschrift-narrow" ? FontStretch.SemiCondensed : FontStretch.Normal;
        }
    }
}
