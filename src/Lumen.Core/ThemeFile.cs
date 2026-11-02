using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lumen.Core;

public sealed class ThemeFile
{
	public const string Extension = ".lumentheme";

	public const string Kind = "lumen-theme";

	public static readonly (string Key, string Name, ThemeFile Theme)[] BuiltIn = new(string, string, ThemeFile)[4]
	{
		("lumen", "Lumen", new ThemeFile
		{
			Type = "lumen-theme",
			Name = "Lumen"
		}),
		("retro", "Retro LCD", new ThemeFile
		{
			Type = "lumen-theme",
			Name = "Retro LCD",
			Background = "retro",
			Glass = false,
			HighContrast = false,
			Font = "mono"
		}),
		("neon", "Neon", new ThemeFile
		{
			Type = "lumen-theme",
			Name = "Neon",
			Background = "neon",
			Glass = false,
			HighContrast = true,
			Font = "inter"
		}),
		("mono", "Mono", new ThemeFile
		{
			Type = "lumen-theme",
			Name = "Mono",
			Colors = "mono",
			Background = "black",
			Glass = false,
			HighContrast = true,
			Font = "inter"
		})
	};

	[JsonPropertyName("type")]
	public string Type { get; set; } = "";

	public int Version { get; set; } = 1;

	public string Name { get; set; } = "";

	public ThemeMode Theme { get; set; } = ThemeMode.Dark;

	public string Colors { get; set; } = "state";

	public string Background { get; set; } = "graphite";

	public bool Glass { get; set; } = true;

	public double Opacity { get; set; } = 0.55;

	public bool HighContrast { get; set; } = true;

	public double TextScale { get; set; } = 1.0;

	public string Font { get; set; } = "inter";

	public bool SmoothTransitions { get; set; }

	public bool Modern { get; set; } = true;

	public static ThemeFile From(Settings s, string name = "")
	{
		return new ThemeFile
		{
			Type = "lumen-theme",
			Name = name,
			Theme = s.Theme,
			Colors = s.Colors,
			Background = s.Background,
			Glass = s.Glass,
			Opacity = s.Opacity,
			HighContrast = s.HighContrast,
			TextScale = s.TextScale,
			Font = s.Font,
			SmoothTransitions = s.SmoothTransitions,
			Modern = s.Modern
		};
	}

	public void ApplyTo(Settings s)
	{
		s.Theme = ((!Enum.IsDefined(Theme)) ? ThemeMode.Dark : Theme);
		s.Colors = Colors ?? "state";
		s.Background = Background ?? "graphite";
		s.Glass = Glass;
		s.Opacity = Opacity;
		s.HighContrast = HighContrast;
		s.TextScale = TextScale;
		s.Font = Font ?? "inter";
		s.SmoothTransitions = SmoothTransitions;
		s.Modern = Modern;
		Settings.Normalize(s);
	}

	public string ToJson()
	{
		return JsonSerializer.Serialize(this, Settings.Json);
	}

	public static ThemeFile? Parse(string json)
	{
		if (string.IsNullOrWhiteSpace(json) || json.Length > 65536)
		{
			return null;
		}
		try
		{
			ThemeFile themeFile = JsonSerializer.Deserialize<ThemeFile>(json, Settings.Json);
			if (themeFile == null || themeFile.Type != "lumen-theme" || themeFile.Version < 1)
			{
				return null;
			}
			themeFile.Name = (themeFile.Name ?? "").Trim();
			if (themeFile.Name.Length > 40)
			{
				themeFile.Name = themeFile.Name.Substring(0, 40);
			}
			return themeFile;
		}
		catch (JsonException)
		{
			return null;
		}
	}
}
