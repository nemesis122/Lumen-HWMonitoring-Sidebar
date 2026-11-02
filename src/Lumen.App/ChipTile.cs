using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed class ChipTile : SummaryTile
{
	private readonly Ellipse dot = new Ellipse
	{
		Width = 9.0,
		Height = 9.0
	};

	private readonly Border halo;

	private readonly TextBlock name;

	private readonly TextBlock value;

	private readonly TextBlock second;

	private readonly Meter meter;

	public ChipTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s)
		: base(kind, index, p, m, s, half: false)
	{
		base.CornerRadius = new CornerRadius(16.0);
		base.Padding = new Thickness(5.0, 4.0, 14.0, 4.0);
		halo = new Border
		{
			Width = 22.0,
			Height = 22.0,
			CornerRadius = new CornerRadius(11.0),
			Child = dot,
			VerticalAlignment = VerticalAlignment.Center
		};
		dot.HorizontalAlignment = HorizontalAlignment.Center;
		dot.VerticalAlignment = VerticalAlignment.Center;
		name = Ui.Text("", 12.5, p.Soft, FontWeight.Medium);
		value = Ui.Text("", 13.0, p.Ink, FontWeight.Bold, numbers: true);
		second = Ui.Text("", 12.5, p.Ink, FontWeight.DemiBold, numbers: true);
		meter = new Meter
		{
			Width = 36.0,
			Height = 4.0,
			Palette = p,
			VerticalAlignment = VerticalAlignment.Center
		};
		TextBlock[] array = new TextBlock[3] { name, value, second };
		for (int i = 0; i < array.Length; i++)
		{
			array[i].VerticalAlignment = VerticalAlignment.Center;
		}
		name.Margin = new Thickness(8.0, 0.0, 8.0, 0.0);
		meter.Margin = new Thickness(8.0, 0.0, 8.0, 0.0);
		base.Child = Ui.Row(0.0, halo, name, value, meter, second);
	}

	protected override void Show(Summary sum)
	{
		Color c = Shade(sum);
		bool flag = sum.Color == P.Ramp[^1];
		base.Background = Palette.Brush(flag ? Palette.Alpha(P.Ramp[^1], P.Dark ? 0.18 : 0.14) : P.Card);
		halo.Background = Palette.Brush(Palette.Alpha(c, 0.22));
		dot.Fill = Palette.Brush(c);
		name.Text = Naming.Title(base.Slot, Index, sum.Title);
		Motion.Text(value, (sum.Value == "—") ? sum.Value : (sum.Value + " " + sum.Unit).Trim());
		value.Foreground = Palette.Brush((sum.Color == P.Ink) ? P.Ink : sum.Color);
		Meter obj = meter;
		bool isVisible = (second.IsVisible = sum.Second.HasValue);
		obj.IsVisible = isVisible;
		double? num = sum.Second;
		if (num.HasValue)
		{
			double valueOrDefault = num.GetValueOrDefault();
			meter.Set(valueOrDefault, P.Cool);
			second.Text = sum.SecondLabel;
		}
	}
}
