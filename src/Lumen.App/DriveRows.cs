using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

/// <summary>
/// The drives of the drives widget, one row each: letter, bar, temperature. Every drive is shown the same
/// way and none of them is singled out as "the hottest". Each design passes its own bar, fonts and
/// colours, so the arrangement is shared while the look stays the design's own.
/// </summary>
public sealed class DriveRows : StackPanel
{
	private readonly double labelSize;

	private readonly double valueSize;

	private readonly Color labelColor;

	private readonly Func<Control> makeBar;

	private readonly Action<Control, Reading> setBar;

	private readonly Func<Reading, Color> valueColor;

	private readonly FontFamily? font;

	private readonly FontWeight weight;

	private readonly List<(TextBlock Label, Control Bar, TextBlock Value)> rows = new();

	public DriveRows(double spacing, double labelSize, double valueSize, Color labelColor, Func<Control> makeBar, Action<Control, Reading> setBar,
		Func<Reading, Color> valueColor, FontFamily? font = null, FontWeight weight = FontWeight.DemiBold)
	{
		Spacing = spacing;
		Tag = "elastic";
		IsVisible = false;
		this.labelSize = labelSize;
		this.valueSize = valueSize;
		this.labelColor = labelColor;
		this.makeBar = makeBar;
		this.setBar = setBar;
		this.valueColor = valueColor;
		this.font = font;
		this.weight = weight;
	}

	/// <summary>The drive readings (letter, temperature, heat) of the drives the user did not hide.</summary>
	public static List<Reading> Of(int index, Snapshot s, History h, Settings set, Palette p) =>
		Readings.For(WidgetKind.Drives, index, s, h, set, p).Where(r => r.Label.Length > 0 && r.Value.Length > 0).ToList();

    // Supplemental metrics use the exact factory and painting delegate of the drive rows above.
    // This also covers design-specific gradients, segmented bars, tracks and value colours.
    public (Control Root, Control Bar, Action<Reading> Update) Detail(TextBlock label, TextBlock value)
    {
        label.FontSize = labelSize; label.Foreground = Palette.Brush(labelColor);
        label.FontWeight = FontWeight.Medium; label.MaxWidth = labelSize * 9;
        label.TextWrapping = TextWrapping.Wrap; label.VerticalAlignment = VerticalAlignment.Center;
        value.FontSize = valueSize; value.FontWeight = weight;
        value.MinWidth = valueSize * 6.6; value.TextWrapping = TextWrapping.NoWrap;
        value.TextAlignment = TextAlignment.Right; value.VerticalAlignment = VerticalAlignment.Center;
        if (font != null) label.FontFamily = value.FontFamily = font;
        var bar = makeBar(); bar.Margin = new Thickness(8, 0, 12, 0);
        bar.VerticalAlignment = VerticalAlignment.Center;
        var line = AlignedLine(label, bar, value);
        return (line, bar, reading => { setBar(bar, reading); value.Foreground = Palette.Brush(valueColor(reading)); });
    }

    private static Grid AlignedLine(TextBlock label, Control bar, TextBlock value)
    {
        label.HorizontalAlignment = HorizontalAlignment.Left;
        var line = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Auto) { SharedSizeGroup = "LumenName" }, new ColumnDefinition(1, GridUnitType.Star), new ColumnDefinition(GridLength.Auto) { SharedSizeGroup = "LumenValue" } } };
        Grid.SetColumn(bar, 1); Grid.SetColumn(value, 2);
        line.Children.Add(label); line.Children.Add(bar); line.Children.Add(value);
        return line;
    }

	public void Show(IReadOnlyList<Reading> drives)
	{
		if (rows.Count != drives.Count)
		{
			Children.Clear();
			rows.Clear();
			foreach (Reading _ in drives)
			{
				var label = Ui.Text("", labelSize, labelColor, FontWeight.Medium);
				label.MinWidth = labelSize * 2.6;
				label.VerticalAlignment = VerticalAlignment.Center;
				label.TextTrimming = TextTrimming.CharacterEllipsis;
				label.MaxWidth = labelSize * 6;
				var value = Ui.Text("", valueSize, labelColor, weight, numbers: true);
				value.MinWidth = valueSize * 4.4;
				value.TextAlignment = TextAlignment.Right;
				value.VerticalAlignment = VerticalAlignment.Center;
				if (font != null)
				{
					label.FontFamily = value.FontFamily = font;
				}
				Control bar = makeBar();
				bar.VerticalAlignment = VerticalAlignment.Center;
				bar.Margin = new Thickness(8, 0, 12, 0);
				// Names and values share one width in every row, so all bars start and end at the same place.
				var row = AlignedLine(label, bar, value);
				Children.Add(row);
				rows.Add((label, bar, value));
			}
		}
		for (int i = 0; i < drives.Count; i++)
		{
			var (label, bar, value) = rows[i];
			Reading r = drives[i];
			label.Text = r.Label;
			value.Text = r.Value;
            SensorDetails.Value(value, r.Key);
			value.Foreground = Palette.Brush(valueColor(r));
			// The value column keeps the widest value seen, so the bars do not get longer and shorter as the
			// digits change ("9.6 MB/s" ↔ "61 MB/s").
			value.Measure(Size.Infinity);
			value.MinWidth = Math.Max(value.MinWidth, Math.Ceiling(value.DesiredSize.Width));
			setBar(bar, r);
		}
		IsVisible = drives.Count > 0;
	}
}
