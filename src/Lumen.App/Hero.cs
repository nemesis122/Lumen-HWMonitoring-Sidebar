using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Lumen.App;

public sealed class Hero : StackPanel
{
	private readonly TextBlock number;

	private readonly TextBlock unit;

	public Hero(double size, Palette p)
	{
		base.Orientation = Orientation.Horizontal;
		base.Spacing = 3.0;
		number = Ui.Text("—", size, p.Ink, Ui.Modern ? FontWeight.Bold : FontWeight.DemiBold, numbers: true);
		number.LetterSpacing = (0.0 - size) * (Ui.Modern ? 0.045 : 0.03);
		number.VerticalAlignment = VerticalAlignment.Bottom;
		unit = Ui.Text("", Math.Max(10.5, Math.Round(size * 0.4)), p.Soft, FontWeight.Medium, numbers: true);
		unit.VerticalAlignment = VerticalAlignment.Bottom;
		unit.Margin = new Thickness(0.0, 0.0, 0.0, size * 0.16);
		base.Children.Add(number);
		base.Children.Add(unit);
	}

	public void Set(string value, string unitText, Color color)
	{
		Motion.Text(number, value);
		number.Foreground = Palette.Brush(color);
		unit.Text = ((value == "—") ? "" : unitText);
	}
}
