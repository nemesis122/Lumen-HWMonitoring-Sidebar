using System;
using System.Linq;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed class Palette
{
	public static readonly (string Key, string Name, string DarkSurface, string DarkCard, string LightSurface, string LightCard)[] Backgrounds = new(string, string, string, string, string, string)[15]
	{
		("graphite", "Graphite", "#0B0E14", "#171C25", "#ECEFF4", "#FFFFFF"),
		("black", "Black (OLED)", "#000000", "#0E0F12", "#E9E9EB", "#FFFFFF"),
		("midnight", "Midnight blue", "#060E26", "#10203F", "#E6ECF7", "#FBFCFF"),
		("ocean", "Deep sea", "#031A22", "#0B2A35", "#E3F0F2", "#FBFEFF"),
		("forest", "Forest", "#06180E", "#10261A", "#E6F0EA", "#FBFEFC"),
		("aubergine", "Aubergine", "#170A24", "#251536", "#EEE8F4", "#FEFCFF"),
		("wine", "Burgundy", "#1C070D", "#2E111A", "#F3E8EA", "#FFFCFC"),
		("mocha", "Mocha", "#1A120A", "#2A1F14", "#F1ECE6", "#FFFDFA"),
		("slate", "Slate", "#151A21", "#202833", "#E4E8EE", "#F8FAFC"),
		("retro", "Retro LCD", "#0A0906", "#15110A", "#F3EBDD", "#FFF9EE"),
		("neon", "Neon", "#05040A", "#0C0A16", "#EEE9F7", "#FFFFFF"),
        ("nord", "Nord", "#0C1420", "#172434", "#E7EEF4", "#FAFCFF"),
        ("emerald", "Emerald", "#071B16", "#102B23", "#E6F0EA", "#FCFEFD"),
        ("copper", "Copper", "#1D130F", "#302019", "#F5EBE3", "#FFFCF9"),
        ("plum", "Plum", "#1B1223", "#2C1C39", "#EEE8F4", "#FEFCFF")
	};

	public static readonly (string Hex, string Name)[] Presets = new(string, string)[14]
	{
		("#3B82F6", "Blue"),
		("#06B6D4", "Cyan"),
		("#14B8A6", "Teal"),
		("#10B981", "Green"),
		("#84CC16", "Lime"),
		("#EAB308", "Gold"),
		("#F97316", "Orange"),
		("#EF4444", "Red"),
		("#F43F5E", "Rose"),
		("#EC4899", "Pink"),
		("#D946EF", "Magenta"),
		("#8B5CF6", "Violet"),
		("#6366F1", "Indigo"),
		("#94A3B8", "Silver")
	};

	public required bool Dark { get; init; }

	public required Color Surface { get; init; }

	public required Color Card { get; init; }

	public required Color Stroke { get; init; }

	public required Color Ink { get; init; }

	public required Color Soft { get; init; }

	public required Color Faint { get; init; }

	public required Color Track { get; init; }

	public required Color[] Ramp { get; init; }

	public Color Cool => Ramp[0];

	/// <summary>The user picked a background other than the default: designs with their own card colour use
	/// the picked one instead.</summary>
	public bool OwnBackground { get; private set; }

	/// <summary>The colour scheme's single colour (null for "by state", mono and themes): designs with their
	/// own accent colours use it instead.</summary>
	public Color? Chosen { get; private set; }

	/// <summary>A design's own card colour, or the user's background when one was picked.</summary>
	public Color DesignBack(string hex) => Dark && !OwnBackground ? Color.Parse(hex) : Card;

	/// <summary>A design's own tile colour inside a card, or a shade of the user's background.</summary>
	public Color DesignTile(string hex) => Dark && !OwnBackground ? Color.Parse(hex) : Mix(Card, Ink, Dark ? 0.06 : 0.04);

	public Color Calm => Ramp[1];

	public static Palette For(bool dark, bool glass)
	{
		return For(dark, glass, 0.55, null);
	}

	public static Palette For(bool dark, bool glass, double opacity, Color? accent)
	{
		return For(dark, glass, opacity, accent, highContrast: false);
	}

	public static Palette For(bool dark, bool glass, double opacity, Color? accent, bool highContrast)
	{
		return For(dark, glass, opacity, accent, highContrast, "graphite");
	}

	public static Palette For(bool dark, bool glass, double opacity, Color? accent, bool highContrast, string background)
	{
		return For(dark, glass, opacity, accent, highContrast, background, mono: false);
	}

	public static Palette For(Settings s, bool dark, Color? windowsAccent)
	{
		string colors = s.Colors;
		Color? color;
		switch (colors)
		{
		case "state":
		case "mono":
			color = null;
			break;
		case "windows":
			color = windowsAccent ?? Color.Parse("#0078D4");
			break;
		default:
		{
			color = (Color.TryParse(colors, out var color2) ? new Color?(color2) : ((Color?)null));
			break;
		}
		}
		Color? accent = color;
		var palette = For(dark, s.Glass, s.Opacity, accent, s.HighContrast, s.Background, s.Colors == "mono");
		palette.OwnBackground = !string.IsNullOrEmpty(s.Background) && s.Background != "graphite";
		palette.Chosen = s.Colors == "mono" ? null : accent;
		return palette;
	}

	public static Palette For(bool dark, bool glass, double opacity, Color? accent, bool highContrast, string background, bool mono)
	{
		(string, string, string, string, string, string) tuple = Backgrounds.FirstOrDefault(((string Key, string Name, string DarkSurface, string DarkCard, string LightSurface, string LightCard) b) => b.Key == background);
		if (tuple.Item1 == null)
		{
			tuple = Backgrounds[0];
		}
		Color[] array = ((!dark) ? new Color[4]
		{
			Color.Parse("#1F7AE0"),
			Color.Parse("#129C76"),
			Color.Parse("#C98200"),
			Color.Parse("#D93B3B")
		} : new Color[4]
		{
			Color.Parse("#4FB3FF"),
			Color.Parse("#45D3A8"),
			Color.Parse("#F5B544"),
			Color.Parse("#FF5D5D")
		});
		if (accent.HasValue)
		{
			Color valueOrDefault = accent.GetValueOrDefault();
			Color color = (dark ? Mix(valueOrDefault, Colors.White, 0.18) : Mix(valueOrDefault, Colors.Black, 0.08));
			array[0] = (dark ? Mix(color, Colors.White, 0.35) : Mix(color, Colors.White, 0.25));
			array[1] = color;
		}
        string[]? themeRamp = tuple.Item1 switch
        {
            "nord" => dark ? new[]{"#91BED6","#6EAAC8","#E4B566","#F08282"} : new[]{"#275C79","#285F78","#875C11","#AF363D"},
            "emerald" => dark ? new[]{"#78DBB5","#45C294","#E5BD65","#EE8282"} : new[]{"#1F6A50","#247353","#875C11","#AF363D"},
            "copper" => dark ? new[]{"#E6B18B","#D29369","#E3C46F","#F1847B"} : new[]{"#854B2C","#8A4A27","#875C11","#AF363D"},
            "plum" => dark ? new[]{"#CFB1E5","#B895D3","#E8BF73","#EF858D"} : new[]{"#6D408E","#754395","#875C11","#AF363D"},
            _ => null
        };
        if (!accent.HasValue && themeRamp != null) array = themeRamp.Select(Color.Parse).ToArray();
		if (mono)
		{
			array[0] = Color.Parse(dark ? "#9A9A9A" : "#6B6B6B");
			array[1] = Color.Parse(dark ? "#D6D6D6" : "#2E2E2E");
		}
		if (dark && tuple.Item1 == "retro")
		{
			Palette palette = new Palette {
			Dark = true,
			Surface = Base(tuple.Item3, opacity),
			Card = Base(tuple.Item4, opacity * 0.98),
			Stroke = Color.FromArgb(68, byte.MaxValue, 181, 71),
			Ink = Color.Parse("#FFB547"),
			Soft = Color.Parse(highContrast ? "#F0AE48" : "#D9A04A"),
			Faint = Color.Parse("#B0843E"),
			Track = Color.FromArgb(48, byte.MaxValue, 181, 71),
			Ramp = new Color[4]
			{
				Color.Parse("#FFC970"),
				Color.Parse("#FFB547"),
				Color.Parse("#FF8A3D"),
				Color.Parse("#FF4D4D")
			},
			};
			return palette;
		}
		if (dark && tuple.Item1 == "neon")
		{
			Palette palette = new Palette {
			Dark = true,
			Surface = Base(tuple.Item3, opacity),
			Card = Base(tuple.Item4, opacity * 0.98),
			Stroke = Color.FromArgb(176, 217, 70, 239),
			Ink = Color.Parse("#F4F0FF"),
			Soft = Color.Parse(highContrast ? "#DDD3FF" : "#C4B5FD"),
			Faint = Color.Parse("#A99BD6"),
			Track = Color.FromArgb(38, 217, 70, 239),
			Ramp = (mono ? array : (accent.HasValue ? array : new Color[4]
			{
				Color.Parse("#22D3EE"),
				Color.Parse("#E879F9"),
				Color.Parse("#F5B544"),
				Color.Parse("#FF5D5D")
			})),
			};
			return palette;
		}
		if (!dark)
		{
			return new Palette
			{
				Dark = false,
				Surface = Base(tuple.Item5, opacity),
				Card = Base(tuple.Item6, opacity + 0.22),
				Stroke = Color.FromArgb(22, 16, 24, 40),
				Ink = Color.Parse("#12161C"),
				Soft = Color.Parse(highContrast ? "#1E252E" : "#46505E"),
				Faint = Color.Parse(highContrast ? "#2E3640" : "#5F6B7A"),
				Track = Color.FromArgb(18, 16, 24, 40),
				Ramp = array
			};
		}
		return new Palette
		{
			Dark = true,
			Surface = Base(tuple.Item3, opacity),
			Card = Base(tuple.Item4, opacity * 0.98),
			Stroke = Color.FromArgb(28, byte.MaxValue, byte.MaxValue, byte.MaxValue),
			Ink = Color.Parse("#EEF2F7"),
			Soft = Color.Parse(highContrast ? "#E1E7EF" : "#BAC4D1"),
			Faint = Color.Parse(highContrast ? "#C9D2DE" : "#919DAD"),
			Track = Color.FromArgb(22, byte.MaxValue, byte.MaxValue, byte.MaxValue),
			Ramp = array
		};
		Color Base(string hex, double a)
		{
			Color color2 = Color.Parse(hex);
			return Color.FromArgb(glass ? ((byte)Math.Round(Math.Clamp(a, 0.0, 1.0) * 255.0)) : byte.MaxValue, color2.R, color2.G, color2.B);
		}
	}

	public Color At(double t)
	{
		t = (double.IsFinite(t) ? Math.Clamp(t, 0.0, 1.0) : 0.0);
		double[] array = new double[4] { 0.0, 0.5, 0.75, 1.0 };
		for (int i = 1; i < array.Length; i++)
		{
			if (t <= array[i])
			{
				return Mix(Ramp[i - 1], Ramp[i], (t - array[i - 1]) / (array[i] - array[i - 1]));
			}
		}
		return Ramp[^1];
	}

	public static Color Mix(Color a, Color b, double t)
	{
		return Color.FromArgb((byte)((double)(int)a.A + (double)(b.A - a.A) * t), (byte)((double)(int)a.R + (double)(b.R - a.R) * t), (byte)((double)(int)a.G + (double)(b.G - a.G) * t), (byte)((double)(int)a.B + (double)(b.B - a.B) * t));
	}

	public static Color Alpha(Color c, double a)
	{
		return Color.FromArgb((byte)Math.Clamp(a * 255.0, 0.0, 255.0), c.R, c.G, c.B);
	}

	public static IBrush Brush(Color c)
	{
		return new SolidColorBrush(c);
	}
}


