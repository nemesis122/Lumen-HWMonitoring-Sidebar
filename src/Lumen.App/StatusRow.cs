using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed class StatusRow : Border, IWidget, INamed
{
	private WidgetSlot? slot;

	private readonly WidgetKind kind;

	private readonly int index;

	private readonly Settings s;

	private readonly Palette p;

	private readonly Ellipse dot = new Ellipse
	{
		Width = 7.0,
		Height = 7.0,
		VerticalAlignment = VerticalAlignment.Center
	};

	private readonly TextBlock title;

	private readonly TextBlock value;

	private readonly TextBlock unit;

	private readonly TextBlock caption;

	private readonly Meter meter;

	public void Own(WidgetSlot slot, int index)
	{
		this.slot = slot;
	}

	public StatusRow(WidgetKind kind, int index, Palette p, Metrics m, Settings s)
	{
		this.kind = kind;
		this.index = index;
		this.s = s;
		this.p = p;
		base.Background = Palette.Brush(p.Card);
		base.BorderBrush = Palette.Brush(p.Stroke);
		base.BorderThickness = Ui.CardEdge;
		base.CornerRadius = new CornerRadius(Ui.Modern ? 16.0 : Math.Min(m.Radius, 10.0));
		base.Padding = new Thickness(10.0, 6.0, 12.0, 6.0);
		title = Ui.Text("", 12.0, p.Soft, FontWeight.DemiBold);
		value = Ui.Text("", 14.0, p.Ink, FontWeight.DemiBold, numbers: true);
		unit = Ui.Text("", 11.0, p.Soft, FontWeight.Medium);
		unit.VerticalAlignment = VerticalAlignment.Bottom;
		unit.Margin = new Thickness(2.0, 0.0, 0.0, 1.0);
		caption = Ui.Text("", 11.5, p.Soft, FontWeight.Normal, numbers: true);
		caption.TextTrimming = TextTrimming.CharacterEllipsis;
		meter = new Meter
		{
			Width = 44.0,
			Height = 4.0,
			Palette = p,
			VerticalAlignment = VerticalAlignment.Center
		};
		Control[] array = new Control[3] { title, value, caption };
		for (int i = 0; i < array.Length; i++)
		{
			array[i].VerticalAlignment = VerticalAlignment.Center;
		}
		Grid child = Ui.Columns("Auto,Auto,Auto,Auto,Auto,*", dot, title, Ui.Row(0.0, value, unit), meter, new Control(), caption);
		title.Margin = new Thickness(7.0, 0.0, 8.0, 0.0);
		meter.Margin = new Thickness(8.0, 0.0, 8.0, 0.0);
		Grid.SetColumn(caption, 5);
		base.Child = child;
	}

	protected override void OnSizeChanged(SizeChangedEventArgs e)
	{
		base.OnSizeChanged(e);
		double width = e.NewSize.Width;
		if (width > 0.0)
		{
			caption.Opacity = (((caption.Opacity > 0.0) ? (width >= 235.0) : (width >= 255.0)) ? 1 : 0);
		}
	}

	public void Update(Snapshot snap, History h)
	{
		Summary summary = Summaries.For(kind, index, snap, h, s, p);
		title.Text = Naming.Title(slot, index, summary.Title);
		Motion.Text(value, summary.Value);
		value.Foreground = Palette.Brush(summary.Color);
		unit.Text = ((summary.Value == "—") ? "" : summary.Unit);
		dot.Fill = Palette.Brush((summary.Color == p.Ink) ? p.Cool : summary.Color);
		meter.Set(summary.Second ?? summary.Fraction, summary.Second.HasValue ? p.Cool : ((summary.Color == p.Ink) ? p.Cool : summary.Color));
		caption.Text = ((summary.Second.HasValue && summary.SecondLabel.Length > 0) ? summary.SecondLabel : summary.Caption);
	}
}
