using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

// Accent colour per widget for the Evolution design. The user can pick one of the swatches per widget;
// otherwise every kind has its own colour. On a light background the colour is darkened for contrast.
public static class Accents
{
	public static readonly (string Hex, string Name)[] Swatches =
	{
		("#22d3ee", "Cyan"), ("#a78bfa", "Violet"), ("#fbbf24", "Amber"), ("#60a5fa", "Blue"), ("#f472b6", "Pink"),
		("#34d399", "Green"), ("#fb923c", "Orange"), ("#f87171", "Red"), ("#a3e635", "Lime"), ("#818cf8", "Indigo")
	};

	public static string Default(WidgetKind kind) => kind switch
	{
		WidgetKind.Cpu => "#22d3ee",
		WidgetKind.Gpu => "#a78bfa",
		WidgetKind.Memory => "#fbbf24",
		WidgetKind.Network or WidgetKind.DataUsage or WidgetKind.Ping => "#60a5fa",
		WidgetKind.Drives or WidgetKind.Volumes => "#34d399",
		WidgetKind.Cooling or WidgetKind.Power => "#fb923c",
		WidgetKind.Fps => "#a3e635",
		WidgetKind.Battery => "#34d399",
		WidgetKind.Media => "#f472b6",
		_ => "#7dd3fc"
	};

	// Upload always gets the second network colour, so the two directions stay apart.
	public const string Upload = "#f472b6";

	public static Color For(WidgetKind kind, WidgetSlot? slot, Palette p)
	{
		// Without a colour of its own the widget takes the colour scheme's colour when one was picked.
		if ((slot == null || slot.Accent.Length == 0) && p.Chosen is Color chosen) return Readable(chosen, p);
		string hex = slot != null && slot.Accent.Length > 0 ? slot.Accent : Default(kind);
		return Readable(Color.TryParse(hex, out var c) ? c : Color.Parse(Default(kind)), p);
	}

	public static Color Readable(Color c, Palette p) => p.Dark ? c : Palette.Mix(c, Colors.Black, 0.3);

	/// <summary>A design's own accent colour, or the colour scheme's colour when the user picked one.</summary>
	public static Color Design(string hex, Palette p) => Readable(p.Chosen ?? Color.Parse(hex), p);
}
