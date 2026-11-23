using Path = Avalonia.Controls.Shapes.Path;
using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Lumen.Core;

namespace Lumen.App;

public static class Ui
{
	private static readonly Lazy<Bitmap> NemesisArtwork = new(() =>
	{
		using var stream = AssetLoader.Open(new Uri("avares://Lumen/Assets/nemesis.png"));
		return new Bitmap(stream);
	});

	public static Image NemesisLogo(double size)
	{
		var logo = new Image
		{
			Source = NemesisArtwork.Value,
			Width = size,
			Height = size,
			Stretch = Stretch.Uniform,
			VerticalAlignment = VerticalAlignment.Center,
			IsHitTestVisible = false
		};
		RenderOptions.SetBitmapInterpolationMode(logo, BitmapInterpolationMode.HighQuality);
		return logo;
	}

	public static readonly FontFamily Inter = new FontFamily("fonts:Inter#Inter, $Default");

	private static readonly FontFeatureCollection Tabular = new FontFeatureCollection
	{
		FontFeature.Parse("tnum"),
		FontFeature.Parse("cv11")
	};

	public static FontFamily Font { get; private set; } = Inter;

	/// <summary>Orbitron, the font of the Panel designs (embedded, OFL).</summary>
	public static readonly FontFamily Orbitron = new FontFamily("avares://Lumen/Assets/Fonts#Orbitron");

	/// <summary>Doto, a dot-matrix face (embedded, OFL): the LED board design.</summary>
	public static readonly FontFamily Doto = new FontFamily("avares://Lumen/Assets/Fonts#Doto");

	/// <summary>Playfair Display, a high-contrast serif (embedded, OFL): Black &amp; Gold and Sumi-e Ink.</summary>
	public static readonly FontFamily Playfair = new FontFamily("avares://Lumen/Assets/Fonts#Playfair Display");

	/// <summary>A design's own font (Orbitron, Mono, Digital …) unless the user chose a typeface other than Inter: then that one, in every design.</summary>
	public static FontFamily DesignFont(FontFamily own) => FontKey == "inter" ? own : Font;

	public static FontStretch FontWidth { get; private set; } = FontStretch.Normal;

	public static string FontKey { get; private set; } = "inter";

	private static string graphKey = "";
    public static string GraphFontKey => graphKey;
    public static void UseGraphFont(string key) => graphKey = FontCatalog.Normalize(key);
    public static FontFamily GraphFont => graphKey.Length > 0 ? FamilyFor(graphKey) : Font;
    public static FontFamily GraphDesignFont(FontFamily own) => graphKey.Length > 0 ? FamilyFor(graphKey) : DesignFont(own);
    public static void UseFonts(Settings settings) { UseFont(settings.Font); graphKey = FontCatalog.Normalize(settings.GraphFont); }

    public static bool Modern { get; set; } = true;

	public static Thickness CardEdge
	{
		get
		{
			if (!Modern)
			{
				return new Thickness(1.0);
			}
			return new Thickness(0.0);
		}
	}

	public static FontFamily FamilyFor(string key)
	{
		var bundled = FontCatalog.Bundled.FirstOrDefault(f => f.Key == key);
        if (bundled != null) return new FontFamily("avares://Lumen/Assets/Fonts#" + bundled.Name);
        return key switch
		{
			"segoe" => new FontFamily("Segoe UI, fonts:Inter#Inter, $Default"),
			"segoe-variable" => new FontFamily("Segoe UI Variable Text, Segoe UI Variable, Segoe UI, fonts:Inter#Inter, $Default"),
			"bahnschrift" => new FontFamily("Bahnschrift, fonts:Inter#Inter, $Default"),
			"bahnschrift-narrow" => new FontFamily("Bahnschrift SemiCondensed, Bahnschrift, fonts:Inter#Inter, $Default"),
			"mono" => new FontFamily("Cascadia Mono, Consolas, fonts:Inter#Inter, $Default"),
			"orbitron" => Orbitron,
			_ => Inter,
		};
	}

	public static CornerRadius CardRadius(Metrics m)
	{
		return new CornerRadius(Modern ? (m.Radius + 6.0) : m.Radius);
	}

	public static Control Badge(string glyph, Palette p, Color tint, double size = 14.0)
	{
		if (!Modern)
		{
			return Glyph(glyph, p.Soft, size);
		}
		double num = Math.Round(size * 1.9);
		return new Border
		{
			Width = num,
			Height = num,
			CornerRadius = new CornerRadius(num / 2.0),
			Background = Palette.Brush(Palette.Alpha(tint, p.Dark ? 0.16 : 0.14)),
			VerticalAlignment = VerticalAlignment.Center,
			Child = new Viewbox
			{
				Width = size,
				Height = size,
				Child = Glyph(glyph, tint, size),
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			}
		};
	}

	public static void UseFont(string key)
	{
		graphKey = "";
		FontKey = (Settings.Fonts.Contains(key) ? key : "inter");
		Font = FamilyFor(FontKey);
		FontWidth = ((FontKey == "bahnschrift-narrow") ? FontStretch.SemiCondensed : FontStretch.Normal);
	}

	public static TextBlock Text(string text, double size, Color color, FontWeight weight = FontWeight.Normal, bool numbers = false)
	{
		return new TextBlock
		{
			Text = text,
			FontFamily = Font,
			FontStretch = FontWidth,
			FontSize = size,
			FontWeight = weight,
			Foreground = Palette.Brush(color),
			FontFeatures = (numbers ? Tabular : null),
			TextTrimming = TextTrimming.CharacterEllipsis,
			VerticalAlignment = VerticalAlignment.Center
		};
	}

	public static PathIcon Icon(string data, Color color, double size = 15.0)
	{
		return new PathIcon
		{
			Data = StreamGeometry.Parse(data),
			Width = size,
			Height = size,
			Foreground = Palette.Brush(color)
		};
	}

	public static Control Glyph(string data, Color color, double size = 15.0)
	{
		return new Path
		{
			Data = StreamGeometry.Parse(data),
			Stroke = Palette.Brush(color),
			StrokeThickness = 1.4,
			StrokeLineCap = PenLineCap.Round,
			StrokeJoin = PenLineJoin.Round,
			Width = size,
			Height = size,
			Stretch = Stretch.Uniform,
			VerticalAlignment = VerticalAlignment.Center
		};
	}

	public static Grid Columns(string definition, params Control[] children)
	{
		Grid grid = new ResponsiveGrid
		{
			ColumnDefinitions = ColumnDefinitions.Parse(definition)
		};
		for (int i = 0; i < children.Length; i++)
		{
			Grid.SetColumn(children[i], i);
			grid.Children.Add(children[i]);
		}
		return grid;
	}

	public static StackPanel Stack(double spacing, params Control[] children)
	{
		StackPanel stackPanel = new StackPanel();
		stackPanel.Spacing = spacing;
		stackPanel.Children.AddRange(children);
		return stackPanel;
	}

	public static StackPanel Row(double spacing, params Control[] children)
	{
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = Orientation.Horizontal;
		stackPanel.Spacing = spacing;
		stackPanel.Children.AddRange(children);
		return stackPanel;
	}
}

