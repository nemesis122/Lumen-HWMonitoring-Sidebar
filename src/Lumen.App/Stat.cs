using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Lumen.App;

public sealed class Stat : StackPanel
{
	private readonly TextBlock value;

	public string Value
	{
		set
		{
			Motion.Text(this.value, value);
		}
	}

	public Color Color
	{
		set
		{
			this.value.Foreground = Palette.Brush(value);
		}
	}

	public Stat(string label, Palette p)
	{
		base.Spacing = 1.0;
		base.Margin = new Thickness(0.0, 0.0, 6.0, 0.0);
		TextBlock textBlock = (Ui.Modern ? Ui.Text(label, 10.5, p.Faint, FontWeight.Medium) : Ui.Text(label.ToUpperInvariant(), 10.0, p.Faint, FontWeight.DemiBold));
		if (!Ui.Modern)
		{
			textBlock.LetterSpacing = 0.6;
		}
		value = Ui.Text("—", Ui.Modern ? 13.0 : 12.5, p.Ink, Ui.Modern ? FontWeight.DemiBold : FontWeight.Medium, numbers: true);
		base.Children.Add(textBlock);
		base.Children.Add(value);
	}
}
