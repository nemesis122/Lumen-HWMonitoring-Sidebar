using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

// Two calm designs in the manner of Apple's system apps. "Pure": a grey card, the name in the widget
// colour, a big number with a short caption, a small ring for the load and a quiet list with hairlines.
// "Widgets": the card holds square tiles like home-screen widgets (a ring with the value, a list, one tile
// per drive, download and upload over a bar history).
public abstract class AppleTile : DashTile
{
	protected AppleTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s)
		: base(kind, index, p, m, s)
	{
		Background = Palette.Brush(Back("#1c1c1e"));
		BorderThickness = new Thickness(0);
		CornerRadius = new CornerRadius(22);
		Padding = new Thickness(18, 14, 18, 16);
	}

	protected double HorizontalGraphicLimit => S.Horizontal ? Math.Clamp(S.BarHeight - 140, 64, 160) : double.PositiveInfinity;

	// A value that keeps the widest width it has shown, so a shrinking text in a fitted cell does not
	// change its scale back and forth.
	private readonly Dictionary<string, double> widths = new();

	protected SteadyText Steady(string text, double size, Color color, string place)
	{
		var model = Ui.Text("", size, color, FontWeight.DemiBold, true);
		return new SteadyText { Text = text, FontFamily = model.FontFamily, FontStretch = model.FontStretch, FontSize = size, FontWeight = FontWeight.DemiBold, Foreground = model.Foreground, FontFeatures = model.FontFeatures, VerticalAlignment = VerticalAlignment.Center }.Remember(widths, place);
	}

	protected Color Hairline => P.Dark ? Palette.Mix(P.DesignBack("#1c1c1e"), P.Ink, 0.1) : P.Stroke;

	protected Color RingTrack => P.Dark ? Palette.Mix(P.DesignBack("#1c1c1e"), P.Ink, 0.12) : P.Track;

	// Name in the widget colour on the left, the hardware name in grey on the right.
	protected (Grid Row, TextBlock Name, TextBlock Hardware) Header()
	{
		var name = Text("", 13.5, Tint, FontWeight.DemiBold);
		name.TextTrimming = TextTrimming.CharacterEllipsis;
		var hw = Text("", 12, Soft);
		hw.TextTrimming = TextTrimming.CharacterEllipsis;
		hw.HorizontalAlignment = HorizontalAlignment.Right;
		hw.Margin = new Thickness(10, 0, 0, 0);
		var row = Ui.Columns("Auto,*", name, hw);
        HardwareWidgetTitle.Register(this, name, hw);
		hw.VerticalAlignment = name.VerticalAlignment = VerticalAlignment.Center;
		return (row, name, hw);
	}

	protected void ShowHeader(TextBlock name, TextBlock hw, Summary sum)
	{
		name.Text = Naming.Title(Own, Index, sum.Title);
		name.Foreground = Palette.Brush(Tint);
		hw.Text = Naming.ShowHardware(Own, S) ? Naming.Hardware(Own, Index, sum.Name) : "";
		hw.IsVisible = hw.Text.Length > 0;
	}

	// A big number with its unit in grey beside it.
	protected (StackPanel Row, TextBlock Number, TextBlock Unit) Big(double size)
	{
		var number = new ReservedNumberText(Text("", size, Ink, FontWeight.DemiBold, null, true), "888");
		number.LetterSpacing = -1;
		var unit = Text("", Math.Round(size * 0.38), Soft, FontWeight.DemiBold);
		unit.VerticalAlignment = VerticalAlignment.Bottom;
		unit.Margin = new Thickness(3, 0, 0, size * 0.12);
		return (Ui.Row(0, number, unit), number, unit);
	}

	protected static void SetBig(TextBlock number, TextBlock unit, string value, string u)
	{
		bool none = value == "—";
		number.Text = value;
		unit.Text = none ? "" : u;
		unit.IsVisible = unit.Text.Length > 0;
	}

	// The load (or the fill) for the small ring: the second value when the widget has one.
	protected static (double Fraction, string Text) RingValue(Summary sum)
	{
		if (sum.Second is double s)
		{
			return (s, Format.Percent(s * 100));
		}
		return (sum.Value == "—" ? 0 : sum.Fraction, sum.Value == "—" ? "" : Format.Percent(sum.Fraction * 100));
	}

	// Label left in grey, value right, a hairline above every row but the first.
	protected Control ListRow(string label, string value, Color ink, bool first, double size = 14)
	{
		var l = Text(label, size, Soft);
		l.TextTrimming = TextTrimming.CharacterEllipsis;
		var v = Text(value, size, ink, FontWeight.Medium, null, true);
		v.TextAlignment = TextAlignment.Right;
		var row = new Border
		{
			BorderBrush = Palette.Brush(Hairline),
			BorderThickness = new Thickness(0, first ? 0 : 1, 0, 0),
			Padding = new Thickness(0, 8, 0, 8),
			Child = Ui.Columns("*,Auto", l, v)
		};
		v.Margin = new Thickness(10, 0, 0, 0);
		return Mark(row, value);
	}

	// Drives: reading, writing and the other drive facts, without the drives themselves (they have lines
	// or tiles of their own).
	protected List<(string Label, string Value)> DriveFacts()
	{
		var letters = DriveList().Select(d => Key(d.Label)).Where(k => k.Length > 0).ToHashSet();
		return Summaries.Extras(Kind, Index, Last.Snap, Last.History, S)
			.Where(e => e.Value.Length > 0 && e.Value != "—" && !letters.Contains(Key(e.Label)) && !letters.Any(l => Key(e.Label).StartsWith(l)))
			.Take(4).ToList();
	}

	// Readings and facts of the widget without the ones in the big number (and without download and
	// upload, which the widget shows as a pair).
	protected List<(string Label, string Value, Color Ink)> Lines(Summary sum, int facts)
	{
		var shown = AllBut(sum);
		var rest = WithoutRates(shown);
		return rest.Select(r => (r.Label, r.Value, Heat(r.Value) ? Opaque(r.Color) : Ink))
			.Concat(Facts(sum, shown, facts).Select(f => (f.Label, f.Value, Ink))).ToList();
	}
}

public sealed class PureTile : AppleTile
{
    private readonly DesignValueFaces valueFaces = new();
    private readonly WidgetStyle variant;
	private readonly TextBlock name, hardware, number, unit, caption, ringText;

	private readonly AppleRing ring = new AppleRing { Width = 60, Height = 60 };

	private readonly RingBar level = new RingBar { Height = 8, IsVisible = false };

	private Control ringBox = null!;

	private readonly PureLoadIndicator? loadIndicator;

	private readonly Grid hero;

	private readonly RatePairView pair;

	private readonly DriveRows drives;

	private readonly StackPanel list = new StackPanel();

	// Once the widget had a load (ring) it keeps the ring; a reading that drops out for a moment must not
	// swap the ring for the bar and back.
	private bool hadSecond;

	public PureTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s, WidgetStyle style = WidgetStyle.Pure)
		: base(kind, index, p, m, s)
	{
        variant = style;
		(var head, name, hardware) = Header();
		(var big, number, unit) = Big(46);
		caption = Text("", 12.5, Soft);
		caption.TextTrimming = TextTrimming.CharacterEllipsis;
		var left = Ui.Stack(2, big, caption);
		left.VerticalAlignment = VerticalAlignment.Center;
		ringText = Text("", 11.5, Ink, FontWeight.DemiBold, null, true);
		ringText.HorizontalAlignment = HorizontalAlignment.Center;
		ringText.VerticalAlignment = VerticalAlignment.Center;
		ringBox = new Panel { Width = 60, Height = 60, Children = { ring, ringText }, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0), Tag = "graphic" };
		if (PureLoadIndicator.Styles.Contains(style))
		{
			loadIndicator = new PureLoadIndicator(style, p);
			ringBox = loadIndicator;
			ringBox.Margin = new Thickness(12, 0, 0, 0);
			ringBox.VerticalAlignment = VerticalAlignment.Center;
		}
		hero = Ui.Columns("*,Auto", left, ringBox);
		drives = new DriveRows(10, 13, 14, p.Soft, () => new RingBar { Height = 6 },
			(c, r) => ((RingBar)c).Set(r.Fraction, Opaque(r.Color), RingTrack), r => Opaque(r.Color));
		pair = new RatePairView(p, 34);
		var root = Ui.Stack(14, head, hero, level, pair, valueFaces, drives, list);
		root.Tag = "elastic";
		Child = root;
	}

	protected override void Show(Summary sum)
	{
		ShowHeader(name, hardware, sum);
        if (AllDrives)
        {
            drives.Show(DriveList()); drives.IsVisible = hero.IsVisible = level.IsVisible = pair.IsVisible = list.IsVisible = false;
            list.Children.Clear();
            valueFaces.Show(variant, DesignValueFaces.Readings(Kind, Index, Last.Snap, Last.History, S, P), P);
            return;
        }
        valueFaces.IsVisible = false;
		var rates = Rates();
		hero.IsVisible = rates == null && !AllDrives;
		pair.IsVisible = rates != null;
		if (rates is var (down, up))
		{
			pair.Set(down, up);
		}
		drives.IsVisible = false;
		level.IsVisible = false;
		string ringShown = "";
		if (AllDrives)
		{
			drives.Show(DriveList());
		}
		else
		{
			SetBig(number, unit, sum.Value, sum.Unit); BindPrimary(number);
			string which = Which(sum);
			caption.Text = which.Length > 0 ? which : sum.HeroLabel.Length > 0 ? sum.HeroLabel : WidgetSensors.PrimaryLabel(Kind);
			// The ring shows the load beside the temperature; a fill level (memory, battery) gets a
			// capsule bar instead, so the same percentage does not stand twice.
			hadSecond |= sum.Second != null;
			ringBox.IsVisible = hadSecond;
			level.IsVisible = hero.IsVisible && !hadSecond && Kind is WidgetKind.Memory or WidgetKind.Battery or WidgetKind.Volumes;
			var (fraction, text) = RingValue(sum);
			ring.Set(fraction, Tint, RingTrack);
			level.Set(sum.Value == "—" ? 0 : sum.Fraction, Tint, RingTrack);
			ringText.Text = text; if (sum.Second.HasValue) BindSecondary(ringText);
			loadIndicator?.Set(sum.Second, Tint, RingTrack);
			ringShown = ringBox.IsVisible ? Key(text) : "";
		}
		list.Children.Clear();
		var lines = (AllDrives ? DriveExtras() : Lines(sum, 3)).Where(l => ringShown.Length == 0 || Key(l.Value) != ringShown).ToList();
		for (int i = 0; i < lines.Count; i++)
		{
			list.Children.Add(ListRow(lines[i].Label, lines[i].Value, lines[i].Ink, i == 0));
		}
		list.IsVisible = list.Children.Count > 0;
	}

	// Drives: reading, writing and the other drive facts below the drive lines.
	private List<(string Label, string Value, Color Ink)> DriveExtras() => DriveFacts().Select(e => (e.Label, e.Value, Ink)).ToList();
}

public sealed class WidgetsTile : AppleTile
{
	private readonly TextBlock name, hardware;

	private readonly UniformGrid tiles = new UniformGrid { Columns = 2 };

	private readonly StackPanel wide = new StackPanel { Spacing = 10 };

	private readonly RatePairView pair;

	private readonly HistoryBars bars = new HistoryBars { Height = 44, Margin = new Thickness(0, 4, 0, 0) };

	private Control? pairTile;

	public WidgetsTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s)
		: base(kind, index, p, m, s)
	{
		Padding = new Thickness(12, 12, 12, 12);
		(var head, name, hardware) = Header();
		head.Margin = new Thickness(6, 2, 6, 0);
		tiles.Margin = new Thickness(-5);
		pair = new RatePairView(p, 30);
		var root = Ui.Stack(10, head, tiles, wide);
		root.Tag = "elastic";
		Child = root;
	}

	private Color TileBack => P.DesignTile("#2a2a2d");

	private Border Tile(Control content, bool square) => new Border
	{
		Background = Palette.Brush(TileBack),
		CornerRadius = new CornerRadius(18),
		Padding = new Thickness(14, 12, 14, 12),
		Margin = square ? new Thickness(5) : new Thickness(0),
		Child = square ? new Square { Child = content, MaxSide = HorizontalGraphicLimit } : content
	};

	protected override void Show(Summary sum)
	{
		ShowHeader(name, hardware, sum);
		tiles.Children.Clear();
		wide.Children.Clear();
		var rates = Rates();
		if (AllDrives)
		{
			foreach (Reading d in DriveList())
			{
				tiles.Children.Add(Tile(DriveTile(d), square: true));
			}
			var io = DriveFacts();
			if (io.Count > 0)
			{
				wide.Children.Add(Tile(Figures(io.Select(e => (e.Label, e.Value, Ink)).ToList(), 2), square: false));
			}
		}
		else if (rates is var (down, up))
		{
			pair.Set(down, up);
			bars.Set(sum.Series, sum.SeriesMax, Tint, Palette.Mix(TileBack, Tint, 0.35));
			pairTile ??= Tile(Ui.Stack(8, pair, bars), square: false);
			wide.Children.Add(pairTile);
			var rest = Lines(sum, 3);
			if (rest.Count > 0)
			{
				wide.Children.Add(Tile(Figures(rest, 3), square: false));
			}
		}
		else if (sum.Second == null && Kind is WidgetKind.Memory or WidgetKind.Battery or WidgetKind.Volumes)
		{
			// A fill level: one wide tile with the number and a capsule bar, the rest as figures.
			var (row, number, unit) = Big(38);
			SetBig(number, unit, sum.Value, sum.Unit); BindPrimary(number);
			var bar = new RingBar { Height = 10 };
			bar.Set(sum.Value == "—" ? 0 : sum.Fraction, Tint, RingTrack);
			var cap = Text(sum.HeroLabel.Length > 0 ? sum.HeroLabel : WidgetSensors.PrimaryLabel(Kind), 12, Soft);
			wide.Children.Add(Tile(Ui.Stack(10, row, bar, cap), square: false));
			var rest = Lines(sum, 3);
			if (rest.Count > 0)
			{
				wide.Children.Add(Tile(Figures(rest, 3), square: false));
			}
		}
		else
		{
			// A ring tile with the main value and the load, beside it a tile with the next figures.
			tiles.Children.Add(Tile(RingTile(sum), square: true));
			// The load stands in the ring already.
			string second = Key(sum.SecondLabel);
			var rest = Lines(sum, 4).Where(l => second.Length == 0 || Key(l.Value) != second).ToList();
			tiles.Children.Add(Tile(ListTile(rest.Take(3).ToList()), square: true));
			if (rest.Count > 3)
			{
				wide.Children.Add(Tile(Figures(rest.Skip(3).ToList(), 3), square: false));
			}
		}
		tiles.IsVisible = tiles.Children.Count > 0;
		wide.IsVisible = wide.Children.Count > 0;
	}

	private Control RingTile(Summary sum)
	{
		var ring = new AppleRing { Thickness = 0.11 };
		ring.Set(sum.Value == "—" ? 0 : sum.Fraction, Opaque(Shade(sum)), RingTrack);
		string u = sum.Unit is "°C" or "°F" ? "°" : sum.Unit.Length <= 2 ? sum.Unit : "";
		var value = Text(sum.Value == "—" ? "—" : sum.Value + u, 28, Ink, FontWeight.DemiBold, null, true);
        BindPrimary(value);
		value.HorizontalAlignment = HorizontalAlignment.Center;
		var sub = Text(sum.Second != null ? sum.SecondLabel : sum.HeroLabel, 11, Soft);
        if (sum.Second != null) BindSecondary(sub);
		sub.HorizontalAlignment = HorizontalAlignment.Center;
		sub.TextTrimming = TextTrimming.CharacterEllipsis;
		sub.IsVisible = sub.Text.Length > 0;
		var center = Ui.Stack(0, Fit(value, HorizontalAlignment.Center), sub);
		center.VerticalAlignment = VerticalAlignment.Center;
		center.HorizontalAlignment = HorizontalAlignment.Center;
		center.Margin = new Thickness(sum.Second != null ? 30 : 18);
		if (sum.Second is double s)
		{
			// The load as a thinner inner ring.
			var inner = new AppleRing { Thickness = 0.11, Margin = new Thickness(16) };
			inner.Set(s, Tint, RingTrack);
			return new Panel { Children = { ring, inner, center }, Tag = "graphic" };
		}
		return new Panel { Children = { ring, center }, Tag = "graphic" };
	}

	private Control DriveTile(Reading d)
	{
		var label = Text(d.Label, 13, Tint, FontWeight.DemiBold);
		var (row, number, unit) = Big(36);
		var (num, u) = Format.Split(d.Value);
		SetBig(number, unit, num, u);
        BindReading(number, d);
		number.Foreground = Palette.Brush(Opaque(d.Color));
		var bar = new RingBar { Height = 8 };
		bar.Set(d.Fraction, Opaque(d.Color), RingTrack);
		var top = Ui.Stack(4, label, Fit(row));
		var grid = new Grid { RowDefinitions = RowDefinitions.Parse("Auto,*,Auto") };
		Grid.SetRow(bar, 2);
		grid.Children.Add(top);
		grid.Children.Add(bar);
		return grid;
	}

	private Control ListTile(List<(string Label, string Value, Color Ink)> items)
	{
		var stack = new StackPanel { Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
		foreach (var (label, value, ink) in items)
		{
			var l = Text(label, 11, Soft);
			l.TextTrimming = TextTrimming.CharacterEllipsis;
			var v = Steady(value, 17, ink, "list|" + label);
			stack.Children.Add(Mark(Ui.Stack(0, l, Fit(v)), value));
		}
		return stack;
	}

	private Control Figures(List<(string Label, string Value, Color Ink)> items, int columns)
	{
		// A fixed number of columns: a figure that drops out for a moment must not widen the others.
		var grid = new UniformGrid { Columns = columns };
		foreach (var (label, value, ink) in items)
		{
			var l = Text(label, 11, Soft);
			l.TextTrimming = TextTrimming.CharacterEllipsis;
			var v = Steady(value, 15, ink, "figure|" + label);
			grid.Children.Add(Mark(new Border { Padding = new Thickness(0, 2, 8, 2), Child = Ui.Stack(1, l, Fit(v)) }, value));
		}
		return grid;
	}
}

/// <summary>Download and upload side by side in the same size; each number keeps the widest width it
/// has shown (right-aligned), so the unit beside it stays in place while the rate changes.</summary>
public sealed class RatePairView : ResponsiveGrid
{
	private readonly (TextBlock Caption, SteadyText Number, TextBlock Unit)[] cells = new (TextBlock, SteadyText, TextBlock)[2];

	public RatePairView(Palette p, double size)
	{
		ColumnDefinitions = ColumnDefinitions.Parse("*,*");
		for (int i = 0; i < 2; i++)
		{
			var cap = Ui.Text("", 10.5, p.Soft, FontWeight.Medium);
			var number = new SteadyText { Text = "", FontFamily = Ui.Font, FontStretch = Ui.FontWidth, FontSize = size, FontWeight = FontWeight.DemiBold, Foreground = Palette.Brush(p.Ink), FontFeatures = Ui.Text("", 1, p.Ink, FontWeight.Normal, true).FontFeatures, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom };
			number.LetterSpacing = -1;
			var unit = Ui.Text("", Math.Round(size * 0.4), p.Soft, FontWeight.DemiBold);
			unit.VerticalAlignment = VerticalAlignment.Bottom;
			unit.Margin = new Thickness(4, 0, 0, size * 0.12);
			var row = Ui.Row(0, number, unit);
			var cell = Ui.Stack(1, cap, row);
			if (i == 1)
			{
				cell.Margin = new Thickness(10, 0, 0, 0);
			}
			Grid.SetColumn(cell, i);
			Children.Add(cell);
			cells[i] = (cap, number, unit);
		}
	}

	public void Set(Reading down, Reading up)
	{
		foreach (var (cell, r) in new[] { (cells[0], down), (cells[1], up) })
		{
			var (num, u) = Format.Split(r.Value);
			cell.Caption.Text = r.Label;
			cell.Number.Text = num;
			cell.Unit.Text = u;
		}
	}
}

/// <summary>A number that never gets narrower than the widest it has been at its text size.</summary>
public sealed class SteadyText : TextBlock
{
	private Dictionary<string, double> widest = new();

	private string key = "";

	protected override Type StyleKeyOverride => typeof(TextBlock);

	/// <summary>Shares the memory with the text that stood in the same place before (the card rebuilds
	/// its lines on every update).</summary>
	public SteadyText Remember(Dictionary<string, double> memory, string place)
	{
		widest = memory;
		key = place;
		return this;
	}

	protected override Size MeasureOverride(Size available)
	{
		var size = base.MeasureOverride(available);
		string k = key + "|" + FontSize;
		double w = Math.Max(size.Width, widest.GetValueOrDefault(k));
		widest[k] = w;
		return new Size(w, size.Height);
	}
}

/// <summary>A full ring that fills clockwise from the top, with round ends.</summary>
public sealed class AppleRing : Control
{
	private double fraction;

	private Color fill = Colors.DeepSkyBlue;

	private Color track = Colors.DimGray;

	/// <summary>Thickness of the ring as a share of its size.</summary>
	public double Thickness { get; set; } = 0.12;

	public void Set(double value, Color color, Color back)
	{
		fraction = double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0;
		fill = color;
		track = back;
		InvalidateVisual();
	}

	public override void Render(DrawingContext ctx)
	{
		double size = Math.Min(Bounds.Width, Bounds.Height);
		if (size < 12)
		{
			return;
		}
		double thick = size * Thickness;
		double r = size / 2 - thick / 2;
		var c = new Point(Bounds.Width / 2, Bounds.Height / 2);
		ctx.DrawEllipse(null, new Pen(Palette.Brush(track), thick), c, r, r);
		if (fraction > 0.004)
		{
			var pen = new Pen(Palette.Brush(fill), thick, lineCap: PenLineCap.Round);
			if (fraction >= 0.999)
			{
				ctx.DrawEllipse(null, pen, c, r, r);
				return;
			}
			var g = new StreamGeometry();
			using (var x = g.Open())
			{
				Point At(double deg) => new Point(c.X + r * Math.Cos(deg * Math.PI / 180), c.Y + r * Math.Sin(deg * Math.PI / 180));
				double sweep = 360 * fraction;
				x.BeginFigure(At(-90), false);
				x.ArcTo(At(-90 + sweep), new Size(r, r), 0, sweep > 180, SweepDirection.Clockwise);
				x.EndFigure(false);
			}
			ctx.DrawGeometry(null, pen, g);
		}
	}
}

/// <summary>The recent history as rounded upright bars; the newest bar in full colour.</summary>
public sealed class HistoryBars : Control
{
	private double[] values = Array.Empty<double>();

	private double max = 1;

	private Color now = Colors.DeepSkyBlue;

	private Color past = Colors.SteelBlue;

	private const int Count = 40;

	public void Set(double[] series, double? top, Color current, Color older)
	{
		values = series.Length > Count ? series[^Count..] : series;
		double m = top ?? (values.Length > 0 ? values.Max() : 1);
		max = m > 0 && double.IsFinite(m) ? m : 1;
		now = current;
		past = older;
		InvalidateVisual();
	}

	public override void Render(DrawingContext ctx)
	{
		double w = Bounds.Width, h = Bounds.Height;
		if (w < 20 || h < 6)
		{
			return;
		}
		double gap = 3, bw = (w - gap * (Count - 1)) / Count;
		if (bw <= 0)
		{
			return;
		}
		int offset = Count - values.Length;
		for (int i = 0; i < values.Length; i++)
		{
			double v = double.IsFinite(values[i]) ? Math.Clamp(values[i] / max, 0, 1) : 0;
			double bh = Math.Max(2, h * v);
			double x = (offset + i) * (bw + gap);
			ctx.DrawRectangle(Palette.Brush(i == values.Length - 1 ? now : past), null, new Rect(x, h - bh, bw, bh), Math.Min(2, bw / 2), Math.Min(2, bw / 2));
		}
	}
}

/// <summary>Lays its child out as a square as wide as the space it gets.</summary>
public sealed class Square : Decorator
{
	public double MaxSide { get; init; } = double.PositiveInfinity;
	protected override Size MeasureOverride(Size available)
	{
		double w = Math.Min(MaxSide, double.IsFinite(available.Width) ? available.Width : 140);
		Child?.Measure(new Size(w, w));
		return new Size(w, w);
	}

	protected override Size ArrangeOverride(Size final)
	{
		if (double.IsFinite(MaxSide))
		{
			double side = Math.Min(MaxSide, Math.Min(final.Width, final.Height));
			Child?.Arrange(new Rect((final.Width - side) / 2, (final.Height - side) / 2, side, side));
		}
		else Child?.Arrange(new Rect(final));
		return final;
	}
}
