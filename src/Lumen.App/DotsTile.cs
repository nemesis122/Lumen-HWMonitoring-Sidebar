using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed class DotsTile : SummaryTile
{
	private readonly DotText dots;

	private readonly TextBlock label;

	private readonly TextBlock unit;

	private readonly Ellipse alert = new Ellipse
	{
		Width = 7.0,
		Height = 7.0,
		HorizontalAlignment = HorizontalAlignment.Right,
		VerticalAlignment = VerticalAlignment.Center
	};

	public DotsTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s)
		: base(kind, index, p, m, s, half: true)
	{
		label = Ui.Text("", 11.0, p.Soft, FontWeight.DemiBold);
		label.LetterSpacing = 1.0;
		dots = new DotText
		{
			Cell = (m.Subtitles ? 6 : 5),
			Color = p.Ink,
			Off = Palette.Alpha(p.Ink, p.Dark ? 0.06 : 0.08)
		};
		unit = Ui.Text("", 12.0, p.Soft, FontWeight.DemiBold);
		unit.VerticalAlignment = VerticalAlignment.Bottom;
		unit.Margin = new Thickness(5.0, 0.0, 0.0, 0.0);
		Viewbox item = new Viewbox
		{
			Child = Ui.Row(0.0, dots, unit),
			StretchDirection = StretchDirection.DownOnly,
			HorizontalAlignment = HorizontalAlignment.Left,
			Margin = new Thickness(0.0, 8.0, 0.0, 6.0)
		};
		Grid item2 = Ui.Columns("*,Auto", label, alert);
		Grid.SetColumn(alert, 1);
		StackPanel stackPanel = new StackPanel
		{
			Spacing = 2.0,
			Tag = "elastic"
		};
		stackPanel.Children.Add(item2);
		stackPanel.Children.Add(item);
		stackPanel.Children.Add(Caption);
		base.Child = stackPanel;
	}

	protected override void Show(Summary sum)
	{
		label.Text = Naming.Title(base.Slot, Index, sum.Title).ToUpper(L.Culture);
		dots.Text = sum.Value;
		unit.Text = ((sum.Value == "—") ? "" : sum.Unit);
		bool isVisible = sum.Color == P.Ramp[^1] || sum.Color == P.Ramp[2];
		alert.Fill = Palette.Brush((sum.Color == P.Ramp[^1]) ? P.Ramp[^1] : P.Ramp[2]);
		alert.IsVisible = isVisible;
	}
}
