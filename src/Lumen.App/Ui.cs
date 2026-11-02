using Path = Avalonia.Controls.Shapes.Path;
using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public static class Ui
{
	public static readonly FontFamily Inter = new FontFamily("fonts:Inter#Inter, $Default");

	private static readonly FontFeatureCollection Tabular = new FontFeatureCollection
	{
		FontFeature.Parse("tnum"),
		FontFeature.Parse("cv11")
	};

	public static FontFamily Font { get; private set; } = Inter;

	public static FontStretch FontWidth { get; private set; } = FontStretch.Normal;

	public static string FontKey { get; private set; } = "inter";

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
		return key switch
		{
			"segoe" => new FontFamily("Segoe UI, fonts:Inter#Inter, $Default"), 
			"segoe-variable" => new FontFamily("Segoe UI Variable Text, Segoe UI Variable, Segoe UI, fonts:Inter#Inter, $Default"), 
			"bahnschrift" => new FontFamily("Bahnschrift, fonts:Inter#Inter, $Default"), 
			"bahnschrift-narrow" => new FontFamily("Bahnschrift SemiCondensed, Bahnschrift, fonts:Inter#Inter, $Default"), 
			"mono" => new FontFamily("Cascadia Mono, Consolas, fonts:Inter#Inter, $Default"), 
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
		Grid grid = new Grid
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

