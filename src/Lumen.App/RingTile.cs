using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed class RingTile : Border, IWidget, INamed
{
	private WidgetSlot? slot;

	private readonly WidgetKind kind;

	private readonly int index;

	private readonly Settings s;

	private readonly Palette p;

	private readonly RingGauge ring;

	private readonly TextBlock title;

	private readonly TextBlock value;

	private readonly TextBlock unit;

	private readonly TextBlock caption;

	private readonly TextBlock name;

	private readonly Control glyphHost;

	private readonly ExtrasView extras;

	public void Own(WidgetSlot slot, int index)
	{
		this.slot = slot;
	}

	public RingTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s)
	{
		this.kind = kind;
		this.index = index;
		this.s = s;
		this.p = p;
		base.Background = Palette.Brush(p.Card);
		base.BorderBrush = Palette.Brush(p.Stroke);
		base.BorderThickness = Ui.CardEdge;
		base.CornerRadius = Ui.CardRadius(m);
		base.Padding = new Thickness(10.0, m.Pad - 6.0, 10.0, m.Pad - 4.0);
		title = Ui.Text("", 11.5, p.Ink, FontWeight.DemiBold);
		title.Margin = new Thickness(6.0, 0.0, 0.0, 0.0);
		glyphHost = new ContentControl();
		Grid grid = Ui.Columns("Auto,*", glyphHost, title);
		name = Ui.Text("", 10.5, p.Soft);
		name.Margin = new Thickness(0.0, 1.0, 0.0, 0.0);
		double num = (m.Subtitles ? 88 : 72);
		ring = new RingGauge
		{
			Width = num,
			Height = num,
			Palette = p,
			Thickness = (m.Subtitles ? 7 : 6)
		};
		value = Ui.Text("", num * 0.3, p.Ink, FontWeight.DemiBold, numbers: true);
		value.LetterSpacing = -0.5;
		unit = Ui.Text("", 10.5, p.Soft, FontWeight.Medium);
		unit.Margin = new Thickness(1.0, 0.0, 0.0, num * 0.05);
		unit.VerticalAlignment = VerticalAlignment.Bottom;
		StackPanel child = Ui.Row(0.0, value, unit);
		Viewbox item = new Viewbox
		{
			Child = child,
			StretchDirection = StretchDirection.DownOnly,
			MaxWidth = num * 0.72,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		Panel panel = new Panel
		{
			Width = num,
			Height = num,
			HorizontalAlignment = HorizontalAlignment.Center,
			Children = 
			{
				(Control)ring,
				(Control)item
			}
		};
		caption = Ui.Text("", 11.5, p.Soft, FontWeight.Normal, numbers: true);
		caption.HorizontalAlignment = HorizontalAlignment.Center;
		caption.Margin = new Thickness(0.0, -6.0, 0.0, 0.0);
		extras = new ExtrasView(p, wide: false)
		{
			Margin = new Thickness(0.0, 2.0, 0.0, 0.0)
		};
		base.Child = Ui.Stack(4.0, Ui.Stack(0.0, grid, name), panel, caption, extras);
	}

	public void Update(Snapshot snap, History h)
	{
		Summary summary = Summaries.For(kind, index, snap, h, s, p);
		Color tint = ((summary.Color == p.Ink) ? p.Cool : summary.Color);
		if (glyphHost is ContentControl contentControl && contentControl.Tag as string != summary.Glyph + tint)
		{
			contentControl.Content = Ui.Badge(summary.Glyph, p, tint, 13.0);
			contentControl.Tag = summary.Glyph + tint;
		}
		title.Text = Naming.Title(slot, index, summary.Title);
		name.Text = Naming.Hardware(slot, index, summary.Name);
		name.IsVisible = Naming.ShowHardware(slot, s) && name.Text.Length > 0;
		Motion.Text(value, summary.Value);
		value.Foreground = Palette.Brush((summary.Color == p.Ink) ? p.Ink : summary.Color);
		unit.Text = ((summary.Value == "—") ? "" : summary.Unit);
		ring.Animate(summary.Fraction, summary.Color);
		caption.Text = summary.Caption;
		extras.Set(Summaries.Extras(kind, index, snap, h, s));
	}
}
