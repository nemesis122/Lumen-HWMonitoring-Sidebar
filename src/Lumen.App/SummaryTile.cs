using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public abstract class SummaryTile : Border, IWidget, INamed
{
	private WidgetSlot? slot;

	protected readonly WidgetKind Kind;

	protected readonly int Index;

	protected readonly Settings S;

	protected readonly Palette P;

	protected readonly Metrics M;

	private readonly TextBlock title;

	private readonly TextBlock name;

	protected readonly TextBlock Caption;

	protected readonly ExtrasView Extras;

	private readonly ContentControl glyph = new ContentControl();

	protected Control Head { get; }

	protected WidgetSlot? Slot => slot;

	protected (Snapshot Snap, History History) Last { get; private set; }

	public void Own(WidgetSlot slot, int index)
	{
		this.slot = slot;
	}

	protected SummaryTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s, bool half)
	{
		Kind = kind;
		Index = index;
		S = s;
		P = p;
		M = m;
		base.Background = Palette.Brush(p.Card);
		base.BorderBrush = Palette.Brush(p.Stroke);
		base.BorderThickness = Ui.CardEdge;
		base.CornerRadius = Ui.CardRadius(m);
		base.Padding = (half ? new Thickness(12.0, m.Pad - 6.0, 12.0, m.Pad - 4.0) : new Thickness(m.Pad, m.Pad - 4.0, m.Pad, m.Pad - 2.0));
		Extras = new ExtrasView(p, !half)
		{
			Margin = new Thickness(0.0, half ? 2 : 4, 0.0, 0.0)
		};
		title = Ui.Text("", half ? 11.5 : 12.5, p.Ink, FontWeight.DemiBold);
		title.Margin = new Thickness(7.0, 0.0, 0.0, 0.0);
		name = Ui.Text("", 10.5, p.Soft);
		Caption = Ui.Text("", 11.5, p.Soft, FontWeight.Normal, numbers: true);
		Caption.HorizontalAlignment = (half ? HorizontalAlignment.Left : HorizontalAlignment.Right);
		Caption.VerticalAlignment = VerticalAlignment.Center;
		Head = (half ? Ui.Stack(0.0, Ui.Columns("Auto,*", glyph, title), name) : Ui.Stack(0.0, Ui.Columns("Auto,Auto,*", glyph, title, Caption), name));
		if (!half)
		{
			Grid.SetColumn(Caption, 2);
		}
	}

	public void Update(Snapshot snap, History h)
	{
		Last = (Snap: snap, History: h);
		Summary summary = Summaries.For(Kind, Index, snap, h, S, P);
		Color tint = Shade(summary);
		string text = summary.Glyph + "|" + tint;
		if (glyph.Tag as string != text)
		{
			glyph.Content = Ui.Badge(summary.Glyph, P, tint, 13.0);
			glyph.Tag = text;
		}
		title.Text = Naming.Title(slot, Index, summary.Title);
		name.Text = Naming.Hardware(slot, Index, summary.Name);
		name.IsVisible = Naming.ShowHardware(slot, S) && name.Text.Length > 0;
		Caption.Text = summary.Caption;
		Extras.Set(Summaries.Extras(Kind, Index, snap, h, S));
		Show(summary);
	}

	protected abstract void Show(Summary sum);

	protected Color Shade(Summary sum)
	{
		if (!(sum.Color == P.Ink))
		{
			return sum.Color;
		}
		return P.Cool;
	}
}
