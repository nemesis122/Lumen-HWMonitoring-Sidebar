using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lumen.Core;

public sealed class ThemeFile
{
	public const string Extension = ".lumentheme";

	public const string Kind = "lumen-theme";

	public static readonly (string Key, string Name, ThemeFile Theme)[] BuiltIn = new(string, string, ThemeFile)[8]
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
		}),
        ("nord", "Nord", new ThemeFile
        {
            Type = Kind, Name = "Nord", Background = "nord",
            Glass = false, HighContrast = false, Font = "inter"
        }),
        ("emerald", "Emerald", new ThemeFile
        {
            Type = Kind, Name = "Emerald", Background = "emerald",
            Glass = false, HighContrast = false, Font = "inter"
        }),
        ("copper", "Copper", new ThemeFile
        {
            Type = Kind, Name = "Copper", Background = "copper",
            Glass = false, HighContrast = false, Font = "inter"
        }),
        ("plum", "Plum", new ThemeFile
        {
            Type = Kind, Name = "Plum", Background = "plum",
            Glass = false, HighContrast = false, Font = "inter"
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
    // Empty means the base/design font, preserving existing profiles.
    public string TitleFont { get; set; } = "";
    public string LabelFont { get; set; } = "";
    public string ValueFont { get; set; } = "";
    public string GraphFont { get; set; } = "";
    public string? TitleColor { get; set; }
    public string? LabelColor { get; set; }
    public string? ValueColor { get; set; }
    public string? GraphColor { get; set; }
    public bool? AutomaticWarningColors { get; set; }
    public List<WidgetColourTheme>? WidgetColourOverrides { get; set; }

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
			Font = s.Font, TitleFont = s.TitleFont, LabelFont = s.LabelFont, ValueFont = s.ValueFont, GraphFont = s.GraphFont,
            TitleColor=s.TitleColor,LabelColor=s.LabelColor,ValueColor=s.ValueColor,GraphColor=s.GraphColor,AutomaticWarningColors=s.AutomaticWarningColors,
            WidgetColourOverrides=s.Widgets.Select((w,index)=>new WidgetColourTheme { Kind=w.Kind,Index=s.Widgets.Take(index).Count(p=>p.Kind==w.Kind),Colours=w.Colours.Copy(),Sensors=w.SensorColours.ToDictionary(p=>p.Key,p=>p.Value.Copy()) }).Where(w=>!w.Colours.InheritGlobal || !w.Colours.Empty || w.Sensors.Count>0).ToList(),
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
		s.Font = Font ?? "inter"; s.TitleFont = TitleFont; s.LabelFont = LabelFont; s.ValueFont = ValueFont; s.GraphFont = GraphFont;
        if(TitleColor!=null)s.TitleColor=TitleColor;if(LabelColor!=null)s.LabelColor=LabelColor;if(ValueColor!=null)s.ValueColor=ValueColor;if(GraphColor!=null)s.GraphColor=GraphColor;
        if(AutomaticWarningColors.HasValue)s.AutomaticWarningColors=AutomaticWarningColors.Value;
        if(WidgetColourOverrides!=null)
            foreach(var widget in s.Widgets){widget.Colours=new();widget.SensorColours=new();}
        foreach(var entry in WidgetColourOverrides??new())
            if(entry!=null && s.Widgets.Where(w=>w.Kind==entry.Kind).ElementAtOrDefault(Math.Max(0,entry.Index)) is {} target)
            {
                target.Colours=(entry.Colours??new()).Copy();
                target.SensorColours=(entry.Sensors??new()).Where(p=>p.Value!=null).ToDictionary(p=>p.Key,p=>p.Value.Copy());
            }
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
		if (string.IsNullOrWhiteSpace(json) || json.Length > 1048576)
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
