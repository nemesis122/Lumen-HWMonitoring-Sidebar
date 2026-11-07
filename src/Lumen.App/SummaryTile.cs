using Avalonia;
using Avalonia.Controls;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Layout;
using Avalonia.LogicalTree;
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
        Caption.IsVisible = false;
		Head = Ui.Stack(0.0, Ui.Columns("Auto,*", glyph, title), name);
	}

	public void Update(Snapshot snap, History h)
	{
		snap = WidgetSensors.FilterSnapshot(snap, S);
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
        Caption.IsVisible = false;
		Show(summary);
		Extras.Set(AdjustExtras(WithoutShown(Summaries.Extras(Kind, Index, snap, h, S)).Where(i => ShowsEmpty || i.Value != "—").ToList()));
        WidgetPresentation.Apply(this, S);
	}

	// Designs that list only readings the hardware actually reports leave out values shown as a dash.
	protected virtual bool ShowsEmpty => true;

	protected virtual List<(string Label, string Value)> AdjustExtras(List<(string Label, string Value)> items) => items;

	protected abstract void Show(Summary sum);

	// A reading that the upper part of the card already shows (bar, big number, second value) is not
	// repeated in the extras below. Readings are matched by their label, so a value that is still
	// counting up or formatted differently does not make it appear twice for a moment.
	private List<(string Label, string Value)> WithoutShown(List<(string Label, string Value)> items)
	{
		if (items.Count == 0)
		{
			return items;
		}
		HashSet<string> shown = new HashSet<string>();
		foreach (TextBlock text in this.GetLogicalDescendants().OfType<TextBlock>())
		{
			if (string.IsNullOrWhiteSpace(text.Text) || !Visible(text))
			{
				continue;
			}
			List<ILogical> ancestors = text.GetLogicalAncestors().ToList();
			if (ancestors.Contains(Extras) || ancestors.Contains(Head))
			{
				continue;
			}
			shown.Add(Key(text.Text));
			foreach (string part in text.Text.Split('·'))
			{
				shown.Add(Key(part));
			}
		}
		return items.Where(((string Label, string Value) i) => !shown.Contains(Key(i.Label))).ToList();
	}

	private bool Visible(Control control)
	{
		for (Control? c = control; c != null && c != this; c = c.GetLogicalParent() as Control)
		{
			if (!c.IsVisible)
			{
				return false;
			}
		}
		return true;
	}

	private static string Key(string text)
	{
		return new string(text.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
	}

	protected Color Shade(Summary sum)
	{
		if (!(sum.Color == P.Ink))
		{
			return sum.Color;
		}
		return P.Cool;
	}
}
