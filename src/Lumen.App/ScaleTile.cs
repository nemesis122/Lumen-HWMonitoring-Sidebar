using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed class ScaleTile : SummaryTile
{
	private readonly Hero hero;

	private readonly LinearScale scale;

	private readonly Meter load;

	private readonly TextBlock loadText;

	private readonly Grid loadRow;

	public ScaleTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s)
		: base(kind, index, p, m, s, half: false)
	{
		hero = new Hero(m.Subtitles ? 30 : 26, p)
		{
			Margin = new Thickness(0.0, 4.0, 0.0, 0.0)
		};
		scale = new LinearScale
		{
			Height = 18.0,
			Palette = p,
			Margin = new Thickness(-2.0, 6.0, -2.0, 0.0)
		};
		load = new Meter
		{
			Height = 4.0,
			Palette = p,
			VerticalAlignment = VerticalAlignment.Center
		};
		loadText = Ui.Text("", 11.0, p.Soft, FontWeight.Medium, numbers: true);
		loadText.Margin = new Thickness(10.0, 0.0, 0.0, 0.0);
		loadRow = Ui.Columns("*,Auto", load, loadText);
		Grid.SetColumn(loadText, 1);
		loadRow.Margin = new Thickness(0.0, 4.0, 0.0, 0.0);
		StackPanel stackPanel = new StackPanel
		{
			Spacing = 0.0,
			Tag = "elastic"
		};
		stackPanel.Children.Add(base.Head);
		stackPanel.Children.Add(hero);
		stackPanel.Children.Add(scale);
		stackPanel.Children.Add(loadRow);
		stackPanel.Children.Add(Extras);
		base.Child = stackPanel;
	}

	protected override void Show(Summary sum)
	{
		hero.Set(sum.Value, sum.Unit, sum.Color);
		LinearScale linearScale = scale;
		string unit = sum.Unit;
		bool flag = ((unit == "°C" || unit == "°F") ? true : false);
		linearScale.RedFrom = (flag ? Format.Heat(S.TemperatureWarning) : 0.9);
		scale.Animate(sum.Fraction);
		loadRow.IsVisible = sum.Second.HasValue;
		double? second = sum.Second;
		if (second.HasValue)
		{
			double valueOrDefault = second.GetValueOrDefault();
			load.Set(valueOrDefault, P.Cool);
			loadText.Text = L.T("Load") + " " + sum.SecondLabel;
		}
	}
}
