using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed class PillTile : SummaryTile
{
	private readonly FillBack fill;

	private readonly Border icon;

	private readonly TextBlock name;

	private readonly TextBlock sub;

	private readonly TextBlock value;

	private readonly TextBlock unit;

	private string iconKey = "";

	public PillTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s)
		: base(kind, index, p, m, s, half: false)
	{
		double num = (m.Subtitles ? 60 : 52);
		base.Background = Brushes.Transparent;
		base.Padding = new Thickness(0.0);
		base.CornerRadius = new CornerRadius(num / 2.0);
		base.BorderThickness = new Thickness(0.0);
		fill = new FillBack
		{
			Radius = num / 2.0,
			Track = p.Card
		};
		icon = new Border
		{
			Width = num - 18.0,
			Height = num - 18.0,
			CornerRadius = new CornerRadius((num - 18.0) / 2.0),
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(9.0, 0.0, 12.0, 0.0)
		};
		name = Ui.Text("", 13.5, p.Ink, FontWeight.DemiBold);
		sub = Ui.Text("", 11.5, p.Soft, FontWeight.Normal, numbers: true);
		value = Ui.Text("", m.Subtitles ? 24 : 21, p.Ink, FontWeight.Bold, numbers: true);
		value.LetterSpacing = -0.5;
		unit = Ui.Text("", 12.5, p.Soft, FontWeight.DemiBold);
		unit.VerticalAlignment = VerticalAlignment.Bottom;
		unit.Margin = new Thickness(1.0, 0.0, 0.0, 3.0);
		StackPanel stackPanel = Ui.Stack(0.0, name, sub);
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel2 = Ui.Row(0.0, value, unit);
		stackPanel2.VerticalAlignment = VerticalAlignment.Center;
		stackPanel2.Margin = new Thickness(8.0, 0.0, 18.0, 0.0);
		Grid item = Ui.Columns("Auto,*,Auto", icon, stackPanel, stackPanel2);
		Grid.SetColumn(stackPanel2, 2);
		base.Child = new Panel
		{
			Height = num,
			Children = 
			{
				(Control)fill,
				(Control)item
			}
		};
	}

	protected override void Show(Summary sum)
	{
		Color c = Shade(sum);
		fill.Animate(sum.Second ?? sum.Fraction, Palette.Alpha(c, P.Dark ? 0.24 : 0.2));
		string text = sum.Glyph + c;
		if (iconKey != text)
		{
			iconKey = text;
			icon.Background = Palette.Brush(c);
			icon.Child = new Viewbox
			{
				Width = 17.0,
				Height = 17.0,
				Child = Ui.Glyph(sum.Glyph, Readings.TextOn(c, P), 16.0)
			};
		}
		name.Text = Naming.Title(base.Slot, Index, sum.Title);
		sub.Text = ((sum.Second.HasValue && sum.SecondLabel.Length > 0) ? (L.T("Load") + " " + sum.SecondLabel) : sum.Caption);
		Motion.Text(value, sum.Value);
		unit.Text = ((sum.Value == "—") ? "" : sum.Unit);
	}
}
