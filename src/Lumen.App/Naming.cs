using Lumen.Core;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Lumen.App;

public static class Naming
{
    // These designs place the hardware name on the right of their native header.
    // All other designs use a name on the left, below or beside the title.
    public static TextAlignment HardwareAlignment(WidgetStyle style) => Settings.Successor(style) switch
    {
        WidgetStyle.Evolution or WidgetStyle.Thermostat or WidgetStyle.Pure or WidgetStyle.PureBar
            or WidgetStyle.PureNumber or WidgetStyle.PureSegments or WidgetStyle.PureWidgets
            or WidgetStyle.PureWidgetsRight => TextAlignment.Right,
        _ => TextAlignment.Left
    };

    public static Control DriveHardwareRow(TextBlock label, TextBlock name, string caption, WidgetStyle style, Palette palette)
    {
        label.Text = caption.Split(" · ")[0];
        label.Margin = new Thickness(0, 0, 6, 0);
        label.VerticalAlignment = VerticalAlignment.Center;
        name.FontFeatures = null; // A hardware model is text, not a numeric register value.
        name.FontWeight = FontWeight.Normal;
        name.Foreground = Palette.Brush(palette.Soft);
        name.TextAlignment = HardwareAlignment(style);
        name.HorizontalAlignment = HorizontalAlignment.Stretch;
        name.TextWrapping = TextWrapping.Wrap;
        name.TextTrimming = TextTrimming.None;
        name.MinWidth = 0;
        return Ui.Columns("Auto,*", label, name);
    }

	public static string Title(WidgetSlot? slot, int index, string builtIn)
	{
		if (slot == null || slot.ShowTitle)
		{
			if (index != 0 || string.IsNullOrWhiteSpace(slot?.Name))
			{
				return builtIn;
			}
			return slot.Name.Trim();
		}
		return "";
	}

	public static string Hardware(WidgetSlot? slot, int index, string builtIn)
	{
		if (index != 0 || string.IsNullOrWhiteSpace(slot?.HardwareName))
		{
			return builtIn;
		}
		return slot.HardwareName.Trim();
	}

	public static bool ShowHardware(WidgetSlot? slot, Settings s)
	{
		if (s.ShowNames)
		{
			return slot?.ShowHardware ?? true;
		}
		return false;
	}
}
