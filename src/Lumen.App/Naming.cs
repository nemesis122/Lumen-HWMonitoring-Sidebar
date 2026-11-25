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

	/// <summary>
	/// A built-in CPU/GPU name without the maker and filler words, so the part that matters fits:
	/// "AMD Ryzen 7 9800X3D 8-Core Processor" → "Ryzen 7 9800X3D", "AMD Radeon RX 9070 XT" → "RX 9070 XT",
	/// "NVIDIA GeForce RTX 4090" → "RTX 4090", "13th Gen Intel(R) Core(TM) i7-13700K" → "Core i7-13700K".
	/// </summary>
	public static string Short(string name)
	{
		if (string.IsNullOrWhiteSpace(name)) return name ?? "";
		string n = System.Text.RegularExpressions.Regex.Replace(name, @"\((R|TM|tm|r)\)|®|™", "");
		n = System.Text.RegularExpressions.Regex.Replace(n, @"\b\d+(th|st|nd|rd) Gen\b", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
		n = System.Text.RegularExpressions.Regex.Replace(n, @"\b(\d+|Six|Eight|Twelve|Sixteen)-Core( Processor)?\b", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
		n = System.Text.RegularExpressions.Regex.Replace(n, @"\b(Processor|CPU|Graphics Card|with Radeon Graphics|w/ Radeon \w+ Graphics)\b", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
		n = System.Text.RegularExpressions.Regex.Replace(n, @"^\s*(AMD|Intel|NVIDIA)\s+", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
		n = System.Text.RegularExpressions.Regex.Replace(n, @"^\s*(Radeon|GeForce)\s+(?=(RX|RTX|GTX|GT|Pro|VII)\b)", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
		n = System.Text.RegularExpressions.Regex.Replace(n, @"\s*@\s*[\d.]+\s*GHz", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
		// Drives (5.7.0, names were cut off in narrow designs): "Samsung SSD 970 EVO Plus 2TB" reads "970 EVO Plus 2TB".
		n = System.Text.RegularExpressions.Regex.Replace(n, @"^\s*(Samsung|WDC?|Western Digital|Crucial|Kingston|Seagate|SK ?hynix|Sabrent|Corsair|ADATA|Lexar|Team ?Group|Micron|Toshiba|KIOXIA|SanDisk|Solidigm|Phison|Patriot|PNY|Gigabyte|Lenovo)\s+", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
		n = System.Text.RegularExpressions.Regex.Replace(n, @"^\s*(SSD|NVMe)\s+", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
		n = System.Text.RegularExpressions.Regex.Replace(n, @"\s{2,}", " ").Trim(' ', ',', '-');
		return n.Length == 0 ? name.Trim() : n;
	}

	/// <summary>The hardware name for AIO designs: an own name as typed, a built-in name shortened (see <see cref="Short"/>).</summary>
	public static string HardwareShort(WidgetSlot? slot, int index, string builtIn)
	{
		string name = Hardware(slot, index, builtIn);
		return name == builtIn ? Short(builtIn) : name;
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
