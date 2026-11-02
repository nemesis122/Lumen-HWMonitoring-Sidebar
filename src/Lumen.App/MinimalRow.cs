using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed class MinimalRow : Border, IWidget, INamed
{
	private WidgetSlot? slot;

	private readonly WidgetKind kind;

	private readonly int index;

	private readonly Settings s;

	private readonly Palette p;

	private readonly TextBlock title;

	private readonly TextBlock caption;

	private readonly TextBlock name;

	private readonly Hero hero;

	private readonly AreaChart spark;

	private readonly ContentControl glyph = new ContentControl();

	private readonly ExtrasView extras;

	public void Own(WidgetSlot slot, int index)
	{
		this.slot = slot;
	}

	public MinimalRow(WidgetKind kind, int index, Palette p, Metrics m, Settings s)
	{
		this.kind = kind;
		this.index = index;
		this.s = s;
		this.p = p;
		base.BorderBrush = Palette.Brush(p.Stroke);
		base.BorderThickness = new Thickness(0.0, 0.0, 0.0, 1.0);
		base.Padding = new Thickness(m.Pad - 6.0, 2.0, m.Pad - 6.0, m.Gap);
		title = Ui.Text("", 10.0, p.Soft, FontWeight.DemiBold);
		title.LetterSpacing = 0.6;
		title.Margin = new Thickness(6.0, 0.0, 0.0, 0.0);
		caption = Ui.Text("", 11.5, p.Soft, FontWeight.Normal, numbers: true);
		caption.HorizontalAlignment = HorizontalAlignment.Right;
		caption.TextTrimming = TextTrimming.CharacterEllipsis;
		caption.MaxWidth = 100.0;
		caption.Margin = new Thickness(8.0, 0.0, 0.0, 0.0);
		title.TextTrimming = TextTrimming.CharacterEllipsis;
		title.MaxWidth = 110.0;
		hero = new Hero(m.Subtitles ? 28 : 24, p);
		spark = new AreaChart
		{
			Height = (m.Subtitles ? 30 : 24),
			LineWidth = 1.4,
			FillOpacity = 0.22,
			VerticalAlignment = VerticalAlignment.Bottom,
			Margin = new Thickness(12.0, 0.0, 0.0, 4.0),
			Tag = (s.Horizontal ? "fixed" : null)
		};
		name = Ui.Text("", 11.0, p.Soft);
		name.TextTrimming = TextTrimming.CharacterEllipsis;
		name.Margin = new Thickness(8.0, 0.0, 8.0, 0.0);
		extras = new ExtrasView(p, wide: true)
		{
			Margin = new Thickness(0.0, 2.0, 0.0, 0.0)
		};
		base.Child = Ui.Stack(0.0, Ui.Columns("Auto,Auto,*,Auto", glyph, title, name, caption), Ui.Columns("Auto,*", hero, spark), extras);
		Grid.SetColumn(caption, 3);
	}

	public void Update(Snapshot snap, History h)
	{
		Summary summary = Summaries.For(kind, index, snap, h, s, p);
		if (glyph.Tag as string != summary.Glyph)
		{
			glyph.Content = Ui.Glyph(summary.Glyph, p.Soft, 12.0);
			glyph.Tag = summary.Glyph;
		}
		title.Text = Naming.Title(slot, index, summary.Title).ToUpper(L.Culture);
		name.Text = Naming.Hardware(slot, index, summary.Name);
		name.IsVisible = Naming.ShowHardware(slot, s) && name.Text.Length > 0;
		caption.Text = summary.Caption;
		hero.Set(summary.Value, summary.Unit, summary.Color);
		AreaChart areaChart = spark;
		int isVisible;
		if (summary.Series.Length > 1)
		{
			string text = Summaries.ChartKey(kind);
			isVisible = ((text == null || s.Shows(text)) ? 1 : 0);
		}
		else
		{
			isVisible = 0;
		}
		areaChart.IsVisible = (byte)isVisible != 0;
		extras.Set(Summaries.Extras(kind, index, snap, h, s));
		spark.Max = summary.SeriesMax;
		spark.Set(summary.Series, (summary.Color == p.Ink) ? p.Cool : summary.Color);
	}
}
