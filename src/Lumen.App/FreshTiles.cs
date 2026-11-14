using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

// Shared rules of the three newer designs (Graph, Rings, Columns): every value appears once. Readings that
// repeat the big number or the second value are left out, and an extra whose value is already on the card
// is not listed again even under another label.
public abstract class FreshTile : SummaryTile
{
	protected FreshTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s)
		: base(kind, index, p, m, s, half: false)
	{
	}

	protected WidgetSlot? Own => Slot ?? S.ActiveWidget;

	protected Color Tint => Accents.For(Kind, Own, P);

	protected static string Key(string text) => new string(text.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

	protected static string HeroText(Summary sum) => (sum.Value + " " + sum.Unit).Trim();

	// Which drive the big number belongs to ("Hottest · C:"); its row is left out of the list, so the card
	// must name it or that drive would seem to be missing.
	protected string Which(Summary sum) => Kind == WidgetKind.Drives ? sum.HeroLabel : "";

	// The readings of the widget without the ones already in the big number or the second value.
	protected List<Reading> Rest(Summary sum)
	{
		var (snap, history) = Last;
		var list = Readings.For(Kind, Index, snap, history, S, P).Where(r => r.Value.Length > 0 && r.Value != "—").Select(Fan).ToList();
		foreach (string taken in new[] { HeroText(sum), sum.SecondLabel })
		{
			string k = Key(taken);
			if (k.Length == 0)
			{
				continue;
			}
			int i = list.FindIndex(r => Key(r.Value) == k && r.Label != L.T("2-min peak"));
			if (i >= 0)
			{
				list.RemoveAt(i);
			}
		}
		return list;
	}

	// Fan readings come as bare numbers; they get their unit, a stopped fan reads "off".
	protected Reading Fan(Reading r)
	{
		if (Kind != WidgetKind.Cooling || r.Value.Length == 0 || !r.Value.All(ch => char.IsDigit(ch) || ch == '.' || ch == ','))
		{
			return r;
		}
		return r with { Value = r.Value + "\u00A0" + Format.RpmUnit }; // "0 rpm" like every design
	}

	protected List<Reading> All()
	{
		var (snap, history) = Last;
        if (Kind == WidgetKind.Drives && this is NeonTilesTile or ColumnsTile or SmartPanelTile)
            return DriveMetrics();
		return Readings.For(Kind, Index, snap, history, S, P).Where(r => r.Value.Length > 0 && r.Value != "—").Select(Fan).ToList();
	}

    private List<Reading> DriveMetrics() => CompleteReadings.For(Kind, Index, Last.Snap, Last.History, S, P)
        .Where(r => r.Key.StartsWith("drive:") && !r.Key.EndsWith(":model") && !DriveTransferPairs.IsRate(r))
        .Select(r => { double fraction = r.Fraction ?? SegmentScale.Fraction(Kind, r.Label, r.Value, Last.Snap) ?? 0;
            return new Reading(r.Label, r.Value, fraction, r.Key.EndsWith(":temp") ? P.At(fraction) : P.Cool) { Key = r.Key }; }).ToList();

	protected override bool ShowsEmpty => false;

	// Network: download and upload are one pair. Every design shows them the same way (same size, same
	// kind of element), never download as the big number and upload as an ordinary line below it.
	protected (Reading Down, Reading Up)? Rates()
	{
		if (Kind != WidgetKind.Network)
		{
			return null;
		}
		var all = All();
		Reading? down = all.FirstOrDefault(r => r.Label.StartsWith('↓'));
		Reading? up = all.FirstOrDefault(r => r.Label.StartsWith('↑'));
		return down != null && up != null ? (down, up) : null;
	}

	// Drives: every drive the same way, none of them as "the hottest" big number.
	protected bool AllDrives => Kind == WidgetKind.Drives;

	protected List<Reading> DriveList() => DriveRows.Of(Index, Last.Snap, Last.History, S, P);

	protected static bool IsRate(Reading r) => r.Label.StartsWith('↓') || r.Label.StartsWith('↑');

	protected List<Reading> WithoutRates(List<Reading> list) =>
		Kind == WidgetKind.Network && Rates() != null ? list.Where(r => !IsRate(r)).ToList() : list;

	protected override List<(string Label, string Value)> AdjustExtras(List<(string Label, string Value)> items)
	{
		var shown = new HashSet<string>();
		foreach (TextBlock t in this.GetLogicalDescendants().OfType<TextBlock>())
		{
			if (string.IsNullOrWhiteSpace(t.Text) || !t.IsVisible || t.GetLogicalAncestors().Contains(Extras) || t.GetLogicalAncestors().OfType<SensorDetails>().Any())
			{
				continue;
			}
			shown.Add(Key(t.Text));
		}
		foreach (TextBlock t in this.GetLogicalDescendants().OfType<TextBlock>().Where(t => t.IsVisible && !t.GetLogicalAncestors().Contains(Extras) && !t.GetLogicalAncestors().OfType<SensorDetails>().Any()))
		{
			// Value split into number and unit in two text blocks.
			if (t.Parent is StackPanel row && row.Orientation == Orientation.Horizontal)
			{
				shown.Add(Key(string.Concat(row.Children.OfType<TextBlock>().Select(x => x.Text))));
			}
		}
		// A 2-minute peak is its own reading even when it equals the current value right now; dropping it
		// for that moment would make the card shorter and everything below it jump.
		return items.Where(i => !shown.Contains(Key(i.Value)) || i.Label == L.T("2-min peak")).ToList();
	}

	protected static Color Opaque(Color c) => Color.FromArgb(255, c.R, c.G, c.B);
}

// "Graph": the history of the main value fills the card like a task manager graph, the big number sits on
// it, the other readings follow as small label/value pairs.
public sealed class GraphTile : FreshTile
{
    private readonly DesignValueFaces driveFaces = new();
	private readonly AreaChart chart = new AreaChart { Height = 84, FillOpacity = 0.28, LineWidth = 1.8, VerticalAlignment = VerticalAlignment.Bottom };

	private readonly TextBlock value;

	private readonly TextBlock unit;

	private readonly TextBlock second;

	private readonly TextBlock downArrow, upArrow, upValue, upUnit;

	private readonly StackPanel upBig;

	private readonly DriveRows drives;

	private readonly Border driveBox;

	private readonly UniformGrid pairs = new UniformGrid { Columns = 2 };

	private readonly Border graph;

	public GraphTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s)
		: base(kind, index, p, m, s)
	{
		value = Ui.Text("", m.Subtitles ? 30 : 26, p.Ink, FontWeight.DemiBold, numbers: true);
		value.LetterSpacing = -1;
		unit = Ui.Text("", 12, p.Soft, FontWeight.Medium);
		unit.VerticalAlignment = VerticalAlignment.Bottom;
		unit.Margin = new Thickness(3, 0, 0, 5);
		second = Ui.Text("", 11.5, p.Soft, FontWeight.Medium, numbers: true);
		second.HorizontalAlignment = HorizontalAlignment.Right;
		second.VerticalAlignment = VerticalAlignment.Top;
		downArrow = Ui.Text("↓ ", 14, p.Soft, FontWeight.Medium);
		upArrow = Ui.Text("↑ ", 14, p.Soft, FontWeight.Medium);
		upValue = Ui.Text("", m.Subtitles ? 30 : 26, p.Ink, FontWeight.DemiBold, numbers: true);
		upValue.LetterSpacing = -1;
		upUnit = Ui.Text("", 12, p.Soft, FontWeight.Medium);
		foreach (var t in new[] { downArrow, upArrow, upUnit })
		{
			t.VerticalAlignment = VerticalAlignment.Bottom;
			t.Margin = new Thickness(t == upUnit ? 3 : 0, 0, 0, 5);
		}
		downArrow.IsVisible = false;
		var big = Ui.Row(0, downArrow, value, unit);
		big.VerticalAlignment = VerticalAlignment.Top;
		big.Margin = new Thickness(10, 6, 10, 0);
		// Network: upload in the same size at the right, the pair of download and upload.
		upBig = Ui.Row(0, upArrow, upValue, upUnit);
		upBig.VerticalAlignment = VerticalAlignment.Top;
		upBig.HorizontalAlignment = HorizontalAlignment.Right;
		upBig.Margin = new Thickness(10, 6, 10, 0);
		upBig.IsVisible = false;
		second.Margin = new Thickness(10, 10, 10, 0);
		graph = new Border
		{
			CornerRadius = new CornerRadius(8),
			ClipToBounds = true,
			MinHeight = 84,
			Child = new Panel { Children = { chart, big, second, upBig } }
		};
		drives = new DriveRows(8, 11.5, 13, p.Soft, () => new FillBack { Height = 6, Radius = 3, Track = Palette.Mix(p.Card, p.Ink, p.Dark ? 0.12 : 0.1) },
			(c, r) => ((FillBack)c).Animate(r.Fraction, Opaque(r.Color)), r => Opaque(r.Color));
		driveBox = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 9), Child = drives, IsVisible = false };
		var root = Ui.Stack(8, Head, graph, driveBox, driveFaces, pairs, Extras);
		root.Tag = "elastic";
		Child = root;
	}

	protected override void Show(Summary sum)
	{
        driveFaces.IsVisible = false;
        if (AllDrives)
        {
            drives.Show(DriveList()); driveBox.IsVisible = graph.IsVisible = pairs.IsVisible = false;
            pairs.Children.Clear();
            driveFaces.Show(WidgetStyle.HistoryGraph, DesignValueFaces.Readings(Kind, Index, Last.Snap, Last.History, S, P), P, Last.History);
            return;
        }
		Color tint = Kind is WidgetKind.Cpu or WidgetKind.Gpu or WidgetKind.Drives or WidgetKind.Cooling ? Opaque(Shade(sum)) : Tint;
		value.Text = sum.Value;
		BindPrimary(value);
		value.Foreground = Palette.Brush(sum.Value == "—" ? P.Soft : P.Ink);
		unit.Text = sum.Value == "—" ? "" : sum.Unit;
		second.Text = sum.SecondLabel.Length > 0 && sum.Second.HasValue ? sum.SecondLabel + " " + L.T("Load") : Which(sum);
		second.IsVisible = second.Text.Length > 0 && (Kind is WidgetKind.Cpu or WidgetKind.Gpu || Which(sum).Length > 0);
        if (Kind is WidgetKind.Cpu or WidgetKind.Gpu && sum.Second.HasValue) BindSecondary(second);
		// Drives: the panel lists every drive instead of the curve of the hottest one.
		var rates = Rates();
		downArrow.IsVisible = upBig.IsVisible = rates != null;
		if (rates is var (down, up))
		{
			second.IsVisible = false;
			(value.Text, unit.Text) = Format.Split(down.Value);
			(upValue.Text, upUnit.Text) = Format.Split(up.Value);
            BindSecondary(upValue);
			value.Foreground = upValue.Foreground = Palette.Brush(P.Ink);
		}
		bool curve = sum.Series.Length > 1;
		chart.IsVisible = curve;
		graph.Background = Palette.Brush(Palette.Mix(P.Card, tint, P.Dark ? 0.08 : 0.06));
		// Drives: the panel lists every drive instead of the curve of the hottest one.
		graph.IsVisible = sum.Value.Length > 0 && !AllDrives;
		driveBox.IsVisible = AllDrives;
		if (AllDrives)
		{
			driveBox.Background = graph.Background;
			drives.Show(DriveList());
		}
		graph.MinHeight = curve ? 84 : 0;
		graph.Padding = curve ? new Thickness(0) : new Thickness(0, 0, 0, 8);
		if (curve)
		{
			chart.Max = sum.SeriesMax;
			chart.Set(sum.Series, tint);
		}
		pairs.Children.Clear();
		foreach (Reading r in AllDrives ? new List<Reading>() : WithoutRates(Rest(sum)))
		{
			var label = Ui.Text(r.Label.ToUpper(L.Culture), 9, P.Soft, FontWeight.Medium);
			var val = Ui.Text(r.Value, 13, r.Value.EndsWith("°C") || r.Value.EndsWith("°F") ? Opaque(r.Color) : P.Ink, FontWeight.DemiBold, numbers: true);
			pairs.Children.Add(new Border { Padding = new Thickness(2, 0, 8, 4), Child = Ui.Stack(0, label, val) });
		}
		pairs.IsVisible = pairs.Children.Count > 0;
	}
}

// "Rings": two rings around the big number, the outer one for the main value, the inner one for the
// second value (load), with the remaining readings listed beside them.
public sealed class RingsTile : FreshTile
{
    private readonly DesignValueFaces driveFaces = new();
	private readonly RingPair rings = new RingPair { Width = 96, Height = 96 };

	private readonly TextBlock value;

	private readonly TextBlock sub;

	private readonly TextBlock downText, upText;

	private readonly DriveRows drives;

	private readonly Grid body;

	private readonly StackPanel list = new StackPanel { Spacing = 5, VerticalAlignment = VerticalAlignment.Center };

	// Network: the values under the download and upload lines (link speed, use, address …).
	private readonly StackPanel below = new StackPanel { Spacing = 5, IsVisible = false };

	private readonly List<Reading> lines = new();

	public RingsTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s)
		: base(kind, index, p, m, s)
	{
		value = Ui.Text("", 22, p.Ink, FontWeight.DemiBold, numbers: true);
		value.HorizontalAlignment = HorizontalAlignment.Center;
		sub = Ui.Text("", 10, p.Soft, FontWeight.Medium, numbers: true);
		sub.HorizontalAlignment = HorizontalAlignment.Center;
		upText = Ui.Text("", 13.5, p.Ink, FontWeight.DemiBold, numbers: true);
		upText.HorizontalAlignment = HorizontalAlignment.Center;
		downText = Ui.Text("", 13.5, p.Ink, FontWeight.DemiBold, numbers: true);
		downText.HorizontalAlignment = HorizontalAlignment.Center;
		upText.IsVisible = downText.IsVisible = false;
		var center = Ui.Stack(0, value, downText, upText, sub);
		center.VerticalAlignment = VerticalAlignment.Center;
		center.HorizontalAlignment = HorizontalAlignment.Center;
		var ringBox = new Panel { Width = 96, Height = 96, Children = { rings, center }, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0), Tag = "graphic" };
		body = Ui.Columns("Auto,*", ringBox, list);
		drives = new DriveRows(8, 11.5, 12.5, p.Soft, () => new RingBar { Height = 6 },
			(c, r) => ((RingBar)c).Set(r.Fraction, Opaque(r.Color), Palette.Mix(p.Card, p.Ink, p.Dark ? 0.1 : 0.08)), r => Opaque(r.Color));
		var root = Ui.Stack(8, Head, body, drives, driveFaces, below, Extras);
		root.Tag = "elastic";
		Child = root;
	}

	protected override void Show(Summary sum)
	{
		Color main = Kind is WidgetKind.Cpu or WidgetKind.Gpu or WidgetKind.Drives or WidgetKind.Cooling ? Opaque(Shade(sum)) : Tint;
		// Drives: no ring for the hottest drive; every drive as a line with a round-capped bar.
		// Network: download and upload as lines with the same bar as the drives instead of the rings.
		var rates = Rates();
		bool asLines = AllDrives || (Kind == WidgetKind.Network && rates != null);
		body.IsVisible = !asLines;
		drives.IsVisible = false;
		below.Children.Clear();
		below.IsVisible = false;
		lines.Clear();
        driveFaces.IsVisible = false;
		if (AllDrives)
		{
            drives.Show(DriveList()); drives.IsVisible = false;
            var used = CompleteReadings.For(Kind, Index, Last.Snap, Last.History, S, P).Where(r => r.Key.EndsWith(":used"))
                .Select(r => new Reading(r.Label, r.Value, r.Fraction ?? 0, P.Calm) { Key = r.Key }).ToList();
            driveFaces.Show(WidgetStyle.RingPair, DesignValueFaces.Readings(Kind, Index, Last.Snap, Last.History, S, P), P, secondary: used);
			return;
		}
		if (asLines && rates is var (d, u))
		{
			lines.Add(d with { Color = Tint, Fraction = SegmentScale.Fraction(Kind, d.Label, d.Value, Last.Snap) ?? d.Fraction });
			lines.Add(u with { Color = Opaque(P.Cool), Fraction = SegmentScale.Fraction(Kind, u.Label, u.Value, Last.Snap) ?? u.Fraction });
			drives.Show(lines);
			// Every other reading keeps its line even when its value happens to equal the download (the
			// 2-minute peak right after a new peak); dropping it would make the card jump.
			foreach (Reading r in WithoutRates(All()))
			{
				AddRow(below, r.Label, r.Value, P.Ink);
			}
			below.IsVisible = below.Children.Count > 0;
			return;
		}
		value.Text = sum.Value == "—" ? "—" : sum.Value + (sum.Unit.Length <= 2 ? sum.Unit : "");
		BindPrimary(value);
		value.Foreground = Palette.Brush(sum.Value == "—" ? P.Soft : main);
		bool hasSecond = sum.Second.HasValue && sum.SecondLabel.Length > 0;
		sub.Text = hasSecond ? sum.SecondLabel : Which(sum).Length > 0 ? Which(sum).Split('·')[^1].Trim() : (sum.Unit.Length > 2 ? sum.Unit : "");
        if (hasSecond) BindSecondary(sub);
		sub.IsVisible = sub.Text.Length > 0;
		Color track = Palette.Mix(P.Card, P.Ink, P.Dark ? 0.1 : 0.08);
		rings.Set(sum.Value == "—" ? 0 : sum.Fraction, main, hasSecond ? sum.Second : null, Tint, track);
		upText.IsVisible = downText.IsVisible = rates != null;
		value.IsVisible = rates == null;
		if (rates is var (down, up))
		{
			// Download on the outer ring, upload on the inner ring, both numbers in the same size.
			downText.Text = "↓ " + down.Value;
			upText.Text = "↑ " + up.Value;
			downText.Foreground = Palette.Brush(main);
			upText.Foreground = Palette.Brush(Opaque(P.Cool));
			sub.IsVisible = false;
			rings.Set(down.Fraction, main, up.Fraction, Opaque(P.Cool), track);
		}
		list.Children.Clear();
		foreach (Reading r in WithoutRates(Rest(sum)))
		{
			bool heat = r.Value.EndsWith("°C") || r.Value.EndsWith("°F");
			AddRow(list, r.Label, r.Value, heat ? Opaque(r.Color) : P.Ink);
		}
		list.IsVisible = list.Children.Count > 0;
	}

	// Names whose value once did not fit beside them; they keep the value on a line of its own, so the
	// row does not flip between one and two lines as the value changes.
	private readonly HashSet<string> wrapped = new HashSet<string>();

	private void AddRow(StackPanel target, string text, string reading, Color color)
	{
		var label = Ui.Text(text, 11.5, P.Soft);
		label.TextTrimming = TextTrimming.CharacterEllipsis;
		var val = Ui.Text(reading, 12.5, color, FontWeight.DemiBold, numbers: true);
		target.Children.Add(new NameValueRow(label, val, wrapped, text));
	}

	// All further values (clock, voltage, fan, link …) stand in the list to the right of the rings instead
	// of underneath. Drives keep their layout: drive lines, then reading and writing below.
	protected override List<(string Label, string Value)> AdjustExtras(List<(string Label, string Value)> items)
	{
		var rest = base.AdjustExtras(items);
		if (AllDrives)
		{
			// Reading and writing are lines with the drives (see Show).
			string read = L.T("Read"), write = L.T("Write");
			return rest.Where(i => i.Label != read && i.Label != write).ToList();
		}
		var target = body.IsVisible ? list : below;
		if (!body.IsVisible)
		{
			// Network lines: the further values always keep their line, even when one equals the download.
			var have = below.Children.OfType<NameValueRow>().Select(r => r.Name).ToHashSet();
			rest = Summaries.Extras(Kind, Index, Last.Snap, Last.History, S).Where(i => i.Value.Length > 0 && i.Value != "—" && !have.Contains(i.Label)).ToList();
		}
		foreach (var (text, reading) in rest)
		{
			AddRow(target, text, reading, P.Ink);
		}
		target.IsVisible = target.Children.Count > 0;
		return new List<(string Label, string Value)>();
	}
}

/// <summary>Name left, value right; when both do not fit on one line, the value moves below the name.</summary>
public sealed class NameValueRow : Panel
{
	private readonly TextBlock name;

	private readonly TextBlock value;

	private readonly HashSet<string> wrapped;

	private readonly string key;

	private const double Gap = 8;

	public string Name => key;

	public NameValueRow(TextBlock name, TextBlock value, HashSet<string> wrapped, string key)
	{
		this.name = name;
		this.value = value;
		this.wrapped = wrapped;
		this.key = key;
		Children.Add(name);
		Children.Add(value);
	}

	protected override Size MeasureOverride(Size available)
	{
		name.Measure(Size.Infinity);
		value.Measure(Size.Infinity);
		double w = double.IsFinite(available.Width) ? available.Width : name.DesiredSize.Width + Gap + value.DesiredSize.Width;
		if (name.DesiredSize.Width + Gap + value.DesiredSize.Width > w)
		{
			wrapped.Add(key);
		}
		if (wrapped.Contains(key))
		{
			name.Measure(new Size(w, double.PositiveInfinity));
			value.Measure(new Size(w, double.PositiveInfinity));
			return new Size(w, name.DesiredSize.Height + value.DesiredSize.Height);
		}
		return new Size(w, Math.Max(name.DesiredSize.Height, value.DesiredSize.Height));
	}

	protected override Size ArrangeOverride(Size final)
	{
		double vw = Math.Min(final.Width, value.DesiredSize.Width);
		if (wrapped.Contains(key))
		{
			name.Arrange(new Rect(0, 0, final.Width, name.DesiredSize.Height));
			value.Arrange(new Rect(final.Width - vw, name.DesiredSize.Height, vw, value.DesiredSize.Height));
			return final;
		}
		double h = final.Height;
		name.Arrange(new Rect(0, (h - name.DesiredSize.Height) / 2, Math.Max(0, final.Width - vw - Gap), name.DesiredSize.Height));
		value.Arrange(new Rect(final.Width - vw, (h - value.DesiredSize.Height) / 2, vw, value.DesiredSize.Height));
		return final;
	}
}

// "Columns": every reading as an upright bar with its value above and its name below, like a level meter.
public sealed class ColumnsTile : FreshTile
{
	private readonly UniformGrid columns = new UniformGrid { Rows = 1 };

	public ColumnsTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s)
		: base(kind, index, p, m, s)
	{
		var root = Ui.Stack(10, Head, columns, Extras);
		root.Tag = "elastic";
		Child = root;
	}

	protected override void Show(Summary sum)
	{
		// Every reading is a column, the main value first; there is no separate big number.
		var rest = All();
		columns.Children.Clear();
		Color track = Palette.Mix(P.Card, P.Ink, P.Dark ? 0.09 : 0.07);
		foreach (Reading r in rest)
		{
			bool heat = r.Value.EndsWith("°C") || r.Value.EndsWith("°F");
			Color c = heat ? Opaque(r.Color) : Tint;
			var val = Ui.Text(r.Value, 11, P.Ink, FontWeight.DemiBold, numbers: true);
            BindReading(val, r);
			val.HorizontalAlignment = HorizontalAlignment.Center;
			var bar = new LevelBar { Height = 64, Width = 14, HorizontalAlignment = HorizontalAlignment.Center };
			bar.Set(r.Fraction, c, track);
			var label = Ui.Text(r.Label, 9.5, P.Soft, FontWeight.Medium);
			label.HorizontalAlignment = HorizontalAlignment.Center;
			label.TextTrimming = TextTrimming.CharacterEllipsis;
			// Six columns are narrow: a long value ("2975 U/min") shrinks instead of being cut off.
			var fit = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Child = val, Height = 15, HorizontalAlignment = HorizontalAlignment.Center };
			columns.Children.Add(new Border { Padding = new Thickness(2, 0), Child = Ui.Stack(4, fit, bar, label) });
		}
        columns.Rows = Kind == WidgetKind.Drives ? Math.Max(1, (int)Math.Ceiling(rest.Count / 4d)) : 1;
		columns.Columns = Kind == WidgetKind.Drives ? Math.Clamp(rest.Count, 1, 4) : Math.Max(1, rest.Count);
		columns.IsVisible = rest.Count > 0;
	}
}

// A straight bar with round caps, drawn like a piece of the rings (same thickness feel and colours).
public sealed class RingBar : Control
{
    internal void CopyAppearanceTo(RingBar target, double value) => target.Set(value, fill, track);
	private double fraction;

	private Color fill = Colors.DeepSkyBlue;

	private Color track = Colors.DimGray;

	public void Set(double value, Color color, Color back)
	{
		fraction = Math.Clamp(double.IsFinite(value) ? value : 0, 0, 1);
		fill = color;
		track = back;
		InvalidateVisual();
	}

	public override void Render(DrawingContext ctx)
	{
		double h = Bounds.Height, w = Bounds.Width;
		if (w < h * 2)
		{
			return;
		}
		double y = h / 2;
		ctx.DrawLine(new Pen(Palette.Brush(track), h, lineCap: PenLineCap.Round), new Point(h / 2, y), new Point(w - h / 2, y));
		if (fraction > 0.004)
		{
			ctx.DrawLine(new Pen(Palette.Brush(fill), h, lineCap: PenLineCap.Round), new Point(h / 2, y), new Point(h / 2 + (w - h) * fraction, y));
		}
	}
}

// Two concentric rings, open at the bottom: outer for the main value, inner for the second value.
public sealed class RingPair : Control
{
	private double outer;

	private double? inner;

	private Color outerColor = Colors.DeepSkyBlue;

	private Color innerColor = Colors.MediumPurple;

	private Color track = Colors.DimGray;

	public void Set(double main, Color mainColor, double? second, Color secondColor, Color back)
	{
		outer = Math.Clamp(main, 0, 1);
		inner = second is double s ? Math.Clamp(s, 0, 1) : null;
		outerColor = mainColor;
		innerColor = secondColor;
		track = back;
		InvalidateVisual();
	}

	public override void Render(DrawingContext ctx)
	{
		double size = Math.Min(Bounds.Width, Bounds.Height);
		if (size < 20)
		{
			return;
		}
		var c = new Point(Bounds.Width / 2, Bounds.Height / 2);
		double thick = size * 0.075;
		Ring(ctx, c, size / 2 - thick / 2 - 1, thick, outer, outerColor);
		if (inner is double v)
		{
			Ring(ctx, c, size / 2 - thick * 2.1, thick * 0.8, v, innerColor);
		}
	}

	private void Ring(DrawingContext ctx, Point c, double r, double thick, double fraction, Color color)
	{
		ctx.DrawGeometry(null, new Pen(Palette.Brush(track), thick, lineCap: PenLineCap.Round), Arc(c, r, 135, 270));
		if (fraction > 0.004)
		{
			ctx.DrawGeometry(null, new Pen(Palette.Brush(color), thick, lineCap: PenLineCap.Round), Arc(c, r, 135, 270 * fraction));
		}
	}

	private static Geometry Arc(Point c, double r, double start, double sweep)
	{
		var g = new StreamGeometry();
		using (var x = g.Open())
		{
			Point P(double deg) => new Point(c.X + r * Math.Cos(deg * Math.PI / 180), c.Y + r * Math.Sin(deg * Math.PI / 180));
			x.BeginFigure(P(start), false);
			x.ArcTo(P(start + sweep), new Size(r, r), 0, sweep > 180, SweepDirection.Clockwise);
			x.EndFigure(false);
		}
		return g;
	}
}

// An upright bar filled from the bottom.
public sealed class LevelBar : Control
{
    internal void CopyAppearanceTo(LevelBar target, double value) => target.Set(value, color, track);
	private double fraction;

	private Color color = Colors.DeepSkyBlue;

	private Color track = Colors.DimGray;

	public void Set(double value, Color fill, Color back)
	{
		fraction = double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0;
		color = fill;
		track = back;
		InvalidateVisual();
	}

	public override void Render(DrawingContext ctx)
	{
		var b = new Rect(Bounds.Size);
		double r = Math.Min(b.Width / 2, 5);
		ctx.DrawRectangle(Palette.Brush(track), null, b, r, r);
		double h = Math.Max(fraction > 0 ? 3 : 0, b.Height * fraction);
		if (h > 0)
		{
			ctx.DrawRectangle(Palette.Brush(color), null, new Rect(0, b.Height - h, b.Width, h), r, r);
		}
	}
}
