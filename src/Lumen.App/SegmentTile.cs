using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed class SegmentTile : SummaryTile
{
	private readonly Meter first;

	private readonly Meter second;

	private readonly TextBlock firstValue;

	private readonly TextBlock secondValue;

	private readonly TextBlock secondLabel;

	private readonly TextBlock firstLabel;

	private readonly Grid secondRow;

	public SegmentTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s)
		: base(kind, index, p, m, s, half: false)
	{
		Grid grid = Line("", out firstLabel, out first, out firstValue);
		secondRow = Line(L.T("Load"), out secondLabel, out second, out secondValue);
		StackPanel item = Ui.Stack(8.0, base.Head, grid, secondRow);
		StackPanel stackPanel = new StackPanel
		{
			Spacing = 8.0,
			Tag = "elastic"
		};
		stackPanel.Children.Add(item);
		stackPanel.Children.Add(new Border
		{
			Height = 0.0
		});
		stackPanel.Children.Add(Extras);
		base.Child = stackPanel;
		Grid Line(string label, out TextBlock l, out Meter meter, out TextBlock v)
		{
			l = Ui.Text(label, 11.0, p.Soft, FontWeight.Medium);
			l.Width = 46.0;
			l.VerticalAlignment = VerticalAlignment.Center;
			meter = new Meter
			{
				Height = 12.0,
				Palette = p,
				Segments = 20,
				VerticalAlignment = VerticalAlignment.Center
			};
			v = Ui.Text("", 13.0, p.Ink, FontWeight.DemiBold, numbers: true);
			v.MinWidth = 58.0;
			v.TextAlignment = TextAlignment.Right;
			v.VerticalAlignment = VerticalAlignment.Center;
			Grid result = Ui.Columns("Auto,*,Auto", l, meter, v);
			meter.Margin = new Thickness(0.0, 0.0, 10.0, 0.0);
			return result;
		}
	}

	protected override void Show(Summary sum)
	{
		TextBlock textBlock = firstLabel;
		string unit = sum.Unit;
		bool flag = ((unit == "°C" || unit == "°F") ? true : false);
		textBlock.Text = (flag ? L.T("Temp") : "");
		firstLabel.IsVisible = firstLabel.Text.Length > 0;
		firstValue.Text = ((sum.Value == "—") ? sum.Value : (sum.Value + " " + sum.Unit).Trim());
		firstValue.Foreground = Palette.Brush(sum.Color);
		first.RampBySegment = true;
		first.Set(sum.Fraction, Shade(sum));
		secondRow.IsVisible = sum.Second.HasValue;
		double? num = sum.Second;
		if (num.HasValue)
		{
			double valueOrDefault = num.GetValueOrDefault();
			second.RampBySegment = false;
			second.Set(valueOrDefault, P.Cool);
			secondValue.Text = ((sum.SecondLabel.Length > 0) ? sum.SecondLabel : Format.Percent(valueOrDefault * 100.0));
			secondLabel.Text = L.T("Load");
		}
	}
}
