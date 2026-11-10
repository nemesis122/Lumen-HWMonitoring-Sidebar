using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

// The model register belongs directly to the native title, before any measurement or graph.
public sealed class DriveHardwareHeader : StackPanel
{
    private string shape = "";
    private readonly Dictionary<string, TextBlock> names = new();

    public void Update(CompleteReading[] rows, WidgetStyle style, Palette palette)
    {
        IsVisible = rows.Length > 0;
        string next = string.Join('|', rows.Select(r => r.Key + r.Label));
        if (next != shape)
        {
            shape = next; Children.Clear(); names.Clear(); Spacing = 2;
            bool neon = style is WidgetStyle.NeonHud or WidgetStyle.NeonHudGraph or WidgetStyle.NeonTiles;
            foreach (var row in rows)
            {
                var label = Ui.Text("", neon ? 10 : 11, palette.Soft);
                var value = SensorDetails.Value(Ui.Text("", neon ? 14 : 11.5,
                    neon ? palette.Ink : palette.Soft, neon ? FontWeight.Bold : FontWeight.Normal), row.Key);
                var line = Naming.DriveHardwareRow(label, value, row.Label, style, palette);
                if (neon) { value.FontWeight = FontWeight.Bold; value.Foreground = Palette.Brush(palette.Ink); }
                if (style is WidgetStyle.NeonHud or WidgetStyle.NeonHudGraph or WidgetStyle.Terminal)
                    label.FontFamily = value.FontFamily = Ui.FamilyFor("mono");
                names[row.Key] = value; Children.Add(line);
            }
        }
        foreach (var row in rows) { names[row.Key].Text = row.Value; ToolTip.SetTip(names[row.Key], row.Value); }
    }
}
