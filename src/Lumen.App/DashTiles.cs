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

// Six designs after dashboard pictures the user supplied: Neon HUD, Neon tiles, Thermostat, Smart panel,
// Purple dashboard and Sci-fi monitor. They keep Lumen's rules: every value once, hidden readings stay
// hidden, accent colour per widget, readable in the light theme.
public abstract class DashTile : FreshTile
{
	protected static readonly FontFamily Mono = new FontFamily("Cascadia Mono, Consolas, DejaVu Sans Mono, fonts:Inter#Inter, $Default");

	protected static readonly FontFamily Digital = new FontFamily("Bahnschrift, DIN Alternate, fonts:Inter#Inter, $Default");

	protected DashTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s)
		: base(kind, index, p, m, s)
	{
	}

	protected Color Back(string dark) => P.DesignBack(dark);

	protected Color Line(string dark) => P.Dark ? Color.Parse(dark) : P.Stroke;

	protected Color Ink => P.Ink;

	protected Color Soft => P.Soft;

	protected Color Accent(string hex) => Accents.Design(hex, P);

	protected static bool Heat(string value) => value.EndsWith("°C") || value.EndsWith("°F");

	protected string Short(WidgetKind kind) => kind switch
	{
		WidgetKind.Cpu => "CPU",
		WidgetKind.Gpu => "GPU",
		WidgetKind.Memory => "RAM",
		WidgetKind.Drives => "SSD",
		WidgetKind.Network => "NET",
		WidgetKind.Cooling => "FAN",
		WidgetKind.Power => "PWR",
		WidgetKind.Fps => "FPS",
		WidgetKind.Ping => "PING",
		WidgetKind.Battery => "BAT",
		WidgetKind.Volumes => "DISK",
		WidgetKind.Processes => "APPS",
		_ => Cards.Name(kind).ToUpper(L.Culture)
	};

	// All readings without the one in the big number.
	protected List<Reading> AllBut(Summary sum)
	{
		var list = All();
		string k = Key(HeroText(sum));
		int i = k.Length == 0 ? -1 : list.FindIndex(r => Key(r.Value) == k);
		if (i >= 0)
		{
			list.RemoveAt(i);
		}
		return list;
	}

	// Further facts of the widget (clock speed, voltage, …) that no reading on the card shows yet.
	// Value of a fact that repeats a value already shown (the 2-minute peak equal to the current value).
	// The row keeps its place but stays empty, so the card does not grow and shrink with every update.
	protected const string Ghost = "\u2060";

	protected List<(string Label, string Value)> Facts(Summary sum, IEnumerable<Reading> shown, int max = 4)
	{
		var (snap, history) = Last;
		var taken = new HashSet<string>(shown.SelectMany(r => new[] { "l:" + Key(r.Label), "v:" + Key(r.Value) }));
		taken.Add("v:" + Key(HeroText(sum)));
		taken.Add("v:" + Key(sum.SecondLabel));
		return Summaries.Extras(Kind, Index, snap, history, S)
			.Where(e => e.Value.Length > 0 && e.Value != "—" && !taken.Contains("l:" + Key(e.Label)))
			.Select(e => (e.Label, taken.Contains("v:" + Key(e.Value)) ? Ghost : e.Value))
			.Take(max).ToList();
	}

	/// <summary>Hides a row whose value is <see cref="Ghost"/> without giving up its space.</summary>
	protected static T Mark<T>(T row, string value) where T : Control
	{
		if (value == Ghost)
		{
			row.Opacity = 0;
			row.IsHitTestVisible = false;
		}
		return row;
	}

	// Network: download and upload as a pair in the same size, side by side or one above the other.
	protected Control RatePair(Reading down, Reading up, double size, FontFamily? font, Color downColor, Color upColor, bool stacked, bool center = false)
	{
		Control Cell(Reading r, Color c)
		{
			var (num, u) = Format.Split(r.Value);
			var number = Ui.Row(0, Text(num, size, c, FontWeight.DemiBold, font, true), Text(u, Math.Max(9, size * 0.4), Soft, FontWeight.Medium, font));
			var unitBlock = (Control)number.Children[1];
			unitBlock.VerticalAlignment = VerticalAlignment.Bottom;
			unitBlock.Margin = new Thickness(3, 0, 0, size * 0.14);
			var cap = Text(r.Label, 10, Soft, FontWeight.Medium, font);
			var align = center ? HorizontalAlignment.Center : HorizontalAlignment.Left;
			cap.HorizontalAlignment = align;
			return Ui.Stack(1, cap, Fit(number, align));
		}
		if (stacked)
		{
			var both = Ui.Stack(6, Cell(down, downColor), Cell(up, upColor));
			both.HorizontalAlignment = center ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
			return both;
		}
		var grid = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("*,*") };
		var a = Cell(down, downColor);
		var b = Cell(up, upColor);
		b.Margin = new Thickness(10, 0, 0, 0);
		Grid.SetColumn(b, 1);
		grid.Children.Add(a);
		grid.Children.Add(b);
		return grid;
	}

	protected TextBlock Text(string text, double size, Color color, FontWeight weight = FontWeight.Normal, FontFamily? family = null, bool numbers = false)
	{
		var t = Ui.Text(text, size, color, weight, numbers);
		if (family != null)
		{
			t.FontFamily = family;
		}
		return t;
	}

	protected static IBrush Gradient(Color a, Color b, bool vertical = false) => new LinearGradientBrush
	{
		StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
		EndPoint = vertical ? new RelativePoint(0, 1, RelativeUnit.Relative) : new RelativePoint(1, 0, RelativeUnit.Relative),
		GradientStops = { new GradientStop(a, 0), new GradientStop(b, 1) }
	};

	protected static Control Fit(Control c, HorizontalAlignment align = HorizontalAlignment.Left) =>
		SteadyHeight.Of(new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = align, Child = c });
}

// 1 · Neon HUD: dark glass panel, monospace, uppercase title in the widget colour with a badge for the
// main value, one line per reading with a cyan-to-magenta bar.
public sealed class NeonHudTile : DashTile
{
	private readonly TextBlock title;

	private readonly TextBlock badge;

	private readonly Border badgeBox;

	private readonly StackPanel rows = new StackPanel { Spacing = 9 };

	private readonly UniformGrid boxes = new UniformGrid { Columns = 2 };

	// "Neon HUD graph": every further value as a row with its neon bar too, not only three plain lines.
	private readonly bool graph;

	public NeonHudTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s, bool graph = false)
		: base(kind, index, p, m, s)
	{
		this.graph = graph;
		Background = Palette.Brush(Back("#0b0f17"));
		BorderBrush = Palette.Brush(Line("#1c2433"));
		BorderThickness = new Thickness(1);
		CornerRadius = new CornerRadius(10);
		Padding = new Thickness(13, 11, 13, 12);
		title = Text("", 11, Ink, FontWeight.Bold, Mono);
		title.LetterSpacing = 0.6;
		title.TextTrimming = TextTrimming.CharacterEllipsis;
		title.VerticalAlignment = VerticalAlignment.Center;
		badge = Text("", 10.5, Ink, FontWeight.Bold, Mono, numbers: true);
		badgeBox = new Border { CornerRadius = new CornerRadius(5), BorderThickness = new Thickness(1), Padding = new Thickness(7, 2), Child = badge, VerticalAlignment = VerticalAlignment.Center };
		boxes.Margin = new Thickness(-3, 0, -3, 0);
		var root = Ui.Stack(11, Ui.Columns("*,Auto", title, badgeBox), rows, boxes);
		// The free height goes below the lines, not between the title and the first line, so in a bar
		// the lines of all cards start right under their titles (with and without "graph").
		root.Tag = null;
		Child = root;
	}

	private Color Main => Kind switch
	{
		WidgetKind.Gpu or WidgetKind.Drives or WidgetKind.Volumes => Accent("#ec4899"),
		WidgetKind.Cooling or WidgetKind.Memory or WidgetKind.Power => Accent("#f59e0b"),
		_ => Accent("#22d3ee")
	};

	protected override void Show(Summary sum)
	{
		Color main = Own?.Accent.Length > 0 ? Tint : Main;
		string name = Naming.Title(Own, Index, sum.Title);
		string device = Naming.Hardware(Own, Index, sum.Name);
		title.Text = (Naming.ShowHardware(Own, S) && device.Length > 0 ? name + " · " + device : name).ToUpper(L.Culture);
		title.Foreground = Palette.Brush(main);
		badge.Text = sum.Value == "—" ? "" : (Which(sum).Length > 0 ? Which(sum).Split('·')[^1].Trim() + "  " : "") + HeroText(sum);
		badge.Foreground = Palette.Brush(main);
		badgeBox.BorderBrush = Palette.Brush(Color.FromArgb(150, main.R, main.G, main.B));
		badgeBox.Background = Palette.Brush(Color.FromArgb(28, main.R, main.G, main.B));
		// Network: no badge; download and upload both as rows of the same kind.
		bool rates = Rates() != null;
		// Drives: no badge for the hottest drive either; every drive is a row.
		badgeBox.IsVisible = badge.Text.Length > 0 && !rates && !AllDrives;
		var readings = rates || AllDrives ? All() : AllBut(sum);
		rows.Children.Clear();
		boxes.Children.Clear();
		Color cyan = Accent("#22d3ee");
		Color magenta = Accent("#ec4899");
		Color track = P.Dark ? Color.Parse("#1a2230") : P.Track;
		bool fans = Kind == WidgetKind.Cooling;
		// Every reading (all drives, all fans); only the additional facts are limited.
		foreach (Reading r in readings)
		{
			if (fans && !Heat(r.Value))
			{
				// Fans as small boxes with the speed large, as in a HUD.
				var (num, unit) = Format.Split(r.Value);
				var value = Ui.Row(0, Text(num, 17, Ink, FontWeight.Bold, Mono, true), Text(" " + unit, 9.5, Soft, FontWeight.Medium, Mono));
				((Control)value.Children[1]).VerticalAlignment = VerticalAlignment.Bottom;
				((Control)value.Children[1]).Margin = new Thickness(0, 0, 0, 3);
				boxes.Children.Add(new Border
				{
					Margin = new Thickness(3, 0, 3, 6),
					CornerRadius = new CornerRadius(7),
					BorderThickness = new Thickness(1),
					BorderBrush = Palette.Brush(Line("#1f2a3a")),
					Background = Palette.Brush(P.Dark && !P.OwnBackground ? Color.Parse("#0e131d") : Palette.Mix(P.Card, P.Ink, 0.03)),
					Padding = new Thickness(9, 7),
					Child = Ui.Stack(3, Text(r.Label.ToUpper(L.Culture), 9, Soft, FontWeight.Medium, Mono), Fit(value))
				});
				continue;
			}
			bool high = r.Fraction >= 0.7 || (Heat(r.Value) && r.Fraction >= 0.6);
			var label = Text(r.Label, 11, Ink, FontWeight.Normal, Mono);
			label.Opacity = 0.85;
			label.TextTrimming = TextTrimming.CharacterEllipsis;
			var val = Text(r.Value, 11, high ? magenta : cyan, FontWeight.Bold, Mono, true);
			var bar = new GradientBar { Height = 4 };
			bar.Set(r.Fraction, cyan, magenta, track);
			rows.Children.Add(Ui.Stack(5, Ui.Columns("*,Auto", label, val), bar));
		}
		foreach (var (label, value) in Facts(sum, readings, graph ? 12 : 3))
		{
			var l = Text(label, 11, Ink, FontWeight.Normal, Mono);
			l.Opacity = 0.85;
			var line = Ui.Columns("*,Auto", l, Text(value, 11, Ink, FontWeight.Bold, Mono, true));
			// A repeated value (kept as an empty placeholder) keeps its bar too, so the row height stays.
			double? scale = value == Ghost ? 0 : SegmentScale.Fraction(Kind, label, value, Last.Snap);
			if (graph && scale is double fraction)
			{
				var bar = new GradientBar { Height = 4 };
				bar.Set(fraction, cyan, magenta, track);
				rows.Children.Add(Mark(Ui.Stack(5, line, bar), value));
				continue;
			}
			rows.Children.Add(Mark(line, value));
		}
		rows.IsVisible = rows.Children.Count > 0;
		boxes.IsVisible = boxes.Children.Count > 0;
	}
}

// 2 · Neon tiles: status line with a glowing dot, the hardware name large, then the readings in framed
// tiles with a coloured caption each.
public sealed class NeonTilesTile : DashTile
{
	private readonly Avalonia.Controls.Shapes.Ellipse dot = new Avalonia.Controls.Shapes.Ellipse { Width = 7, Height = 7, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 7, 0) };

	private readonly TextBlock status;

	private readonly TextBlock headline;

	private readonly UniformGrid tiles = new UniformGrid { Columns = 3 };

	public NeonTilesTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s)
		: base(kind, index, p, m, s)
	{
		Background = Palette.Brush(Back("#090c12"));
		BorderBrush = Palette.Brush(Line("#1a2130"));
		BorderThickness = new Thickness(1);
		CornerRadius = new CornerRadius(12);
		Padding = new Thickness(13, 12, 13, 12);
		status = Text("", 9.5, Ink, FontWeight.Bold, Mono);
		status.LetterSpacing = 0.8;
		headline = Text("", 17, Ink, FontWeight.Bold);
		headline.TextWrapping = TextWrapping.Wrap;
		headline.TextTrimming = TextTrimming.None;
		tiles.Margin = new Thickness(-4, 0, -4, 0);
		var root = Ui.Stack(10, Ui.Row(0, dot, status), headline, tiles);
		root.Tag = "elastic";
		Child = root;
	}

	protected override void Show(Summary sum)
	{
		Color cyan = Accent("#22d3ee");
		Color magenta = Accent("#ec4899");
		Color amber = Accent("#f59e0b");
		Color main = Own?.Accent.Length > 0 ? Tint : cyan;
		dot.Fill = Palette.Brush(main);
		status.Text = Naming.Title(Own, Index, sum.Title).ToUpper(L.Culture);
		status.Foreground = Palette.Brush(main);
		string device = Naming.Hardware(Own, Index, sum.Name);
		headline.Text = (Naming.ShowHardware(Own, S) && device.Length > 0 ? device : Naming.Title(Own, Index, sum.Title)).ToUpper(L.Culture);
		// Without a device name the headline would repeat the title above it.
		headline.IsVisible = headline.Text != status.Text;
		Color[] captions = { cyan, magenta, amber };
		var readings = All();
		var items = readings.Select(r => (r.Label, r.Value)).Concat(Facts(sum, readings, 3)).ToList();
		tiles.Children.Clear();
		for (int i = 0; i < items.Count; i++)
		{
			var (label, value) = items[i];
			var cap = Text(label.ToUpper(L.Culture), 8.5, captions[i % 3], FontWeight.Bold, Mono);
			cap.HorizontalAlignment = HorizontalAlignment.Center;
			cap.TextTrimming = TextTrimming.CharacterEllipsis;
			var val = Text(value, 16, Ink, FontWeight.Bold, null, true);
			tiles.Children.Add(Mark(new Border
			{
				Margin = new Thickness(4, 0, 4, 8),
				MinHeight = 62,
				CornerRadius = new CornerRadius(10),
				BorderThickness = new Thickness(1),
				BorderBrush = Palette.Brush(Line("#1e2636")),
				Background = Palette.Brush(P.Dark && !P.OwnBackground ? Color.Parse("#111723") : Palette.Mix(P.Card, P.Ink, 0.03)),
				Padding = new Thickness(6, 8),
				Child = Ui.Stack(6, cap, Fit(val, HorizontalAlignment.Center))
			}, value));
		}
		tiles.Columns = Math.Clamp(items.Count, 1, 3);
		tiles.IsVisible = items.Count > 0;
	}
}

// 3 · Thermostat: a large ring with a warm-to-cool gradient and a glowing knob at the value, the number in
// the middle, further readings in a line at the bottom.
public sealed class ThermostatTile : DashTile
{
	private readonly TextBlock head;

	private readonly TextBlock device;

	// Drawn larger than the text inside needs, so the knob never touches the numbers.
	private readonly KnobRing ring = new KnobRing { Width = 250, Height = 250 };

	private readonly TextBlock caption;

	private readonly TextBlock value;

	private readonly TextBlock unit;

	private readonly TextBlock sub;

	private readonly Viewbox face;

	private readonly Border pairHost = new Border { IsVisible = false };

	private readonly StackPanel number;

	private readonly DriveRows drives;

	private readonly Grid footer = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("*,Auto") };

	// The values beside the dial (everything but drives, which keep their footer below the lines).
	private readonly StackPanel side = new StackPanel { Spacing = 7, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };

	private readonly Grid body = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("1*,1*") };

	private readonly HashSet<string> wrapped = new HashSet<string>();

	public ThermostatTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s)
		: base(kind, index, p, m, s)
	{
		Background = Palette.Brush(Back("#0c0d10"));
		BorderBrush = Palette.Brush(Line("#1c1d22"));
		BorderThickness = new Thickness(1);
		CornerRadius = new CornerRadius(14);
		Padding = new Thickness(14, 12, 14, 12);
		head = Text("", 9.5, Soft, FontWeight.Medium);
		head.LetterSpacing = 1.2;
		device = Text("", 9.5, Soft, FontWeight.Medium);
		device.LetterSpacing = 1.2;
		device.Opacity = 0.7;
		device.HorizontalAlignment = HorizontalAlignment.Right;
		device.TextTrimming = TextTrimming.CharacterEllipsis;
		device.MaxWidth = 150;
		caption = Text("", 10, Soft, FontWeight.DemiBold);
		caption.LetterSpacing = 1;
		caption.HorizontalAlignment = HorizontalAlignment.Center;
		value = Text("", 46, Ink, FontWeight.DemiBold, null, true);
		value.LetterSpacing = -1.5;
		unit = Text("", 16, Ink, FontWeight.Medium);
		unit.Margin = new Thickness(2, 6, 0, 0);
		number = Ui.Row(0, value, unit);
		number.HorizontalAlignment = HorizontalAlignment.Center;
		sub = Text("", 10.5, Soft);
		sub.HorizontalAlignment = HorizontalAlignment.Center;
		var center = Ui.Stack(0, caption, number, sub, pairHost);
		center.VerticalAlignment = VerticalAlignment.Center;
		center.HorizontalAlignment = HorizontalAlignment.Center;
		face = new Viewbox { Stretch = Stretch.Uniform, MaxWidth = 262, HorizontalAlignment = HorizontalAlignment.Center, Child = new Panel { Width = 250, Height = 250, Children = { ring, center } } };
		// Drives: every drive as a line with the warm-to-cool bar instead of the dial for the hottest one.
		drives = new DriveRows(9, 10.5, 12.5, Soft, () => new GradientBar { Height = 5 },
			(c, r) => ((GradientBar)c).Set(r.Fraction, Accent("#7cc4f5"), Accent("#f6b25a"), P.Dark ? Color.Parse("#23262e") : P.Track), r => Ink);
		body.Children.Add(face);
		Grid.SetColumn(side, 1);
		body.Children.Add(side);
		var root = Ui.Stack(10, Ui.Columns("*,Auto", head, device), body, drives, footer);
		root.Tag = "elastic";
		Child = root;
	}

	protected override void Show(Summary sum)
	{
		head.Text = Naming.Title(Own, Index, sum.Title).ToUpper(L.Culture);
		string name = Naming.Hardware(Own, Index, sum.Name);
		device.Text = Naming.ShowHardware(Own, S) ? name.ToUpper(L.Culture) : "";
		bool none = sum.Value == "—";
		caption.Text = (sum.HeroLabel.Length > 0 ? sum.HeroLabel : WidgetSensors.PrimaryLabel(Kind)).ToUpper(L.Culture);
		caption.IsVisible = caption.Text.Length > 0;
		value.Text = sum.Value;
		unit.Text = none ? "" : sum.Unit.Replace("°C", "°").Replace("°F", "°");
		sub.Text = sum.SecondLabel.Length > 0 ? sum.SecondLabel + " " + WidgetSensors.SecondaryLabel(Kind) : "";
		sub.IsVisible = sub.Text.Length > 0;
		ring.Set(none ? 0 : sum.Fraction, Accent("#f6b25a"), Accent("#7cc4f5"), P.Dark ? Color.Parse("#23262e") : P.Track);
		var rates = Rates();
		pairHost.IsVisible = rates != null;
		if (rates is var (down, up))
		{
			// Download and upload one above the other in the middle of the dial, in the same size.
			caption.IsVisible = number.IsVisible = sub.IsVisible = false;
			pairHost.Child = RatePair(down, up, 28, null, Ink, Ink, stacked: true, center: true);
		}
		else
		{
			number.IsVisible = true;
			pairHost.Child = null;
		}
		// Drives and network as lines, each with the warm-to-cool bar: every drive, then read and write; or
		// download and upload. The other values follow below.
		bool asLines = AllDrives || (Kind == WidgetKind.Network && rates != null);
		face.IsVisible = !asLines;
		body.IsVisible = !asLines;
		drives.IsVisible = false;
		var shown = AllDrives ? All() : Rest(sum);
		var rest = AllDrives ? new List<Reading>() : WithoutRates(shown);
		var items = rest.Select(r => (r.Label, r.Value)).Concat(Facts(sum, shown, Kind is WidgetKind.Power or WidgetKind.DataUsage or WidgetKind.Ping or WidgetKind.Fps ? 4 : 2)).ToList();
		if (asLines)
		{
			var snap = Last.Snap;
			Reading Line(string label, string value, double fallback) =>
				new Reading(label, value, SegmentScale.Fraction(Kind, label, value, snap) ?? fallback, Ink);
			var rows = new List<Reading>();
			if (AllDrives)
			{
				rows.AddRange(DriveList());
				string read = L.T("Read"), write = L.T("Write");
				foreach (var (l, v) in items.Where(i => (i.Label == read || i.Label == write) && i.Value != Ghost).ToList())
				{
					rows.Add(Line(l, v, 0));
					items.RemoveAll(i => i.Label == l);
				}
			}
			else if (rates is var (d, u))
			{
				rows.Add(Line(d.Label, d.Value, d.Fraction));
				rows.Add(Line(u.Label, u.Value, u.Fraction));
			}
			drives.Show(rows);
		}
		footer.Children.Clear();
		side.Children.Clear();
		if (!asLines)
		{
			// Beside the dial: every value under its name, always (no switching between one and two lines when
			// sizes or values change). A value too wide for the column shrinks instead of wrapping.
			foreach (var (l, v) in items)
			{
				var nameText = Text(l, 10.5, Soft);
				nameText.TextTrimming = TextTrimming.CharacterEllipsis;
				var valueText = Text(v, 13, Ink, FontWeight.DemiBold, null, true);
				valueText.TextTrimming = TextTrimming.None;
				side.Children.Add(Mark(Ui.Stack(1, nameText, Fit(valueText, HorizontalAlignment.Left)), v));
			}
			// "Graphic size" makes the dial take a larger or smaller share of the width; the values keep the
			// rest, so nothing runs out of the card.
			double share = Math.Clamp(Own?.Graphics ?? 1, 0.5, 2);
			body.ColumnDefinitions[0].Width = new GridLength(share, GridUnitType.Star);
			side.IsVisible = side.Children.Count > 0;
			footer.IsVisible = false;
			return;
		}
		side.IsVisible = false;
		if (items.Count > 0)
		{
			var first = Mark(Text("● " + items[0].Label + " " + items[0].Value, 10.5, Soft, FontWeight.Medium, null, true), items[0].Value);
			first.TextTrimming = TextTrimming.CharacterEllipsis;
			var others = Text(string.Join("   ", items.Skip(1).Where(x => x.Value != Ghost).Select(x => x.Label + " " + x.Value)), 10.5, Soft, FontWeight.Medium, null, true);
			others.HorizontalAlignment = HorizontalAlignment.Right;
			Grid.SetColumn(others, 1);
			footer.Children.Add(first);
			footer.Children.Add(others);
			if (items.Count > 2)
			{
				// Too long for one line next to each other: one per line.
				footer.Children.Clear();
				var lines = new StackPanel { Spacing = 3 };
				foreach (var (l, v) in items)
				{
					lines.Children.Add(Mark(Ui.Columns("*,Auto", Text(l, 10.5, Soft), Text(v, 10.5, Ink, FontWeight.DemiBold, null, true)), v));
				}
				Grid.SetColumnSpan(lines, 2);
				footer.Children.Add(lines);
			}
		}
		footer.IsVisible = items.Count > 0;
	}
}

// 4 · Smart panel: small title with an icon, the main value large in the top right, below a row of
// readings each with a coloured mark, a big number and a small unit.
public sealed class SmartPanelTile : DashTile
{
	private readonly TextBlock head;

	private readonly TextBlock heroLabel;

	private readonly TextBlock heroValue;

	private readonly TextBlock heroUnit;

	private readonly FillBack level = new FillBack { Height = 8, Radius = 4 };

	private readonly UniformGrid stats = new UniformGrid { Columns = 3 };

	private readonly Border pairHost = new Border { IsVisible = false };

	private readonly StackPanel right;

	public SmartPanelTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s)
		: base(kind, index, p, m, s)
	{
		Background = Palette.Brush(Back("#0e0f12"));
		BorderBrush = Palette.Brush(Line("#1d1f25"));
		BorderThickness = new Thickness(1);
		CornerRadius = new CornerRadius(14);
		Padding = new Thickness(14, 12, 14, 14);
		head = Text("", 9.5, Soft, FontWeight.Medium);
		head.LetterSpacing = 1.2;
		head.TextTrimming = TextTrimming.CharacterEllipsis;
		heroLabel = Text("", 10, Soft);
		heroLabel.HorizontalAlignment = HorizontalAlignment.Right;
		heroValue = Text("", 30, Ink, FontWeight.DemiBold, null, true);
		heroValue.LetterSpacing = -1;
		heroUnit = Text("", 11, Soft, FontWeight.Medium);
		heroUnit.VerticalAlignment = VerticalAlignment.Bottom;
		heroUnit.Margin = new Thickness(3, 0, 0, 5);
		var heroRow = Ui.Row(0, heroValue, heroUnit);
		heroRow.HorizontalAlignment = HorizontalAlignment.Right;
		right = Ui.Stack(0, heroLabel, heroRow);
		var top = Ui.Columns("*,Auto", head, right);
		head.VerticalAlignment = VerticalAlignment.Top;
		level.Track = P.Dark ? Color.Parse("#22252c") : P.Track;
		var root = Ui.Stack(12, top, pairHost, level, stats);
		root.Tag = "elastic";
		Child = root;
	}

	protected override void Show(Summary sum)
	{
		head.Text = ("◆  " + Naming.Title(Own, Index, sum.Title)).ToUpper(L.Culture);
		bool none = sum.Value == "—";
		heroLabel.Text = sum.HeroLabel.Length > 0 ? sum.HeroLabel : WidgetSensors.PrimaryLabel(Kind);
		heroValue.Text = sum.Value;
		heroUnit.Text = none ? "" : sum.Unit;
		Color green = Accent("#4ade80");
		level.IsVisible = !none && Kind is WidgetKind.Memory or WidgetKind.Battery or WidgetKind.Volumes;
		if (level.IsVisible)
		{
			level.Animate(sum.Fraction, green);
		}
		Color[] marks = { Accent("#f59e0b"), Accent("#f472b6"), Accent("#60a5fa") };
		var rates = Rates();
		right.IsVisible = rates == null;
		pairHost.IsVisible = rates != null;
		// Network: download and upload both large, side by side, instead of one big value.
		pairHost.Child = rates is var (down, up) ? RatePair(down, up, 30, null, Ink, Ink, stacked: false) : null;
		var shown = AllBut(sum);
		var rest = WithoutRates(shown);
		var items = rest.Select(r => (r.Label, r.Value, Heat: Heat(r.Value) ? (Color?)Opaque(r.Color) : null))
			.Concat(Facts(sum, shown, 3).Select(f => (f.Label, f.Value, Heat: (Color?)null))).ToList();
		stats.Children.Clear();
		for (int i = 0; i < items.Count; i++)
		{
			var (label, value, heat) = items[i];
			var mark = new Border { Width = 7, Height = 7, CornerRadius = new CornerRadius(2), Background = Palette.Brush(marks[i % 3]), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 5, 0) };
			var cap = Text(label, 10, Soft);
			cap.TextTrimming = TextTrimming.CharacterEllipsis;
			var (num, u) = Format.Split(value);
			var big = Ui.Row(0, Text(num, 19, heat ?? Ink, FontWeight.DemiBold, null, true), Text(u.Length > 0 ? " " + u : "", 10, Soft, FontWeight.Medium));
			((Control)big.Children[1]).VerticalAlignment = VerticalAlignment.Bottom;
			((Control)big.Children[1]).Margin = new Thickness(0, 0, 0, 3);
			stats.Children.Add(Mark(new Border { Padding = new Thickness(0, 0, 8, 8), Child = Ui.Stack(3, Ui.Columns("Auto,*", mark, cap), Fit(big)) }, value));
		}
		stats.Columns = Math.Clamp(items.Count, 1, 3);
		stats.IsVisible = items.Count > 0;
	}
}

// 5 · Purple dashboard: icon, bold short name and the hardware name, a half ring from purple to pink with
// the main value, thick purple bars with name and value on the right.
public sealed class PurpleTile : DashTile
{
	private readonly Avalonia.Controls.Shapes.Path icon = new Avalonia.Controls.Shapes.Path { Width = 18, Height = 18, Stretch = Stretch.Uniform, StrokeThickness = 1.4, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };

	private readonly TextBlock bold;

	private readonly TextBlock name;

	private readonly SweepRing ring = new SweepRing { Width = 176, Height = 176 };

	private readonly TextBlock value;

	private readonly TextBlock unit;

	private readonly TextBlock label;

	private readonly Viewbox gauge;

	private readonly Border pairHost = new Border { IsVisible = false };

	private readonly StackPanel numberRow;

	private readonly StackPanel bars = new StackPanel { Spacing = 7, VerticalAlignment = VerticalAlignment.Center };

	// "Lila-Liste": the same card without the half ring; the main value leads the list of bars.
	private readonly bool list;

	public PurpleTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s, bool list = false)
		: base(kind, index, p, m, s)
	{
		this.list = list;
		Background = Palette.Brush(Back("#050506"));
		BorderBrush = Palette.Brush(Line("#2b2b2f"));
		BorderThickness = new Thickness(1);
		CornerRadius = new CornerRadius(10);
		Padding = new Thickness(14, 12, 14, 12);
		icon.Stroke = Palette.Brush(Ink);
		bold = Text("", 15, Ink, FontWeight.Bold);
		name = Text("", 15, Ink, FontWeight.Normal);
		name.Opacity = 0.85;
		name.TextTrimming = TextTrimming.CharacterEllipsis;
		name.Margin = new Thickness(6, 0, 0, 0);
		var header = Ui.Columns("Auto,Auto,*", icon, bold, name);
		value = Text("", 38, Ink, FontWeight.Bold, null, true);
		unit = Text("", 16, Ink, FontWeight.Medium);
		unit.Margin = new Thickness(4, 0, 0, 6);
		unit.VerticalAlignment = VerticalAlignment.Bottom;
		var number = Ui.Row(0, value, unit);
		number.HorizontalAlignment = HorizontalAlignment.Center;
		label = Text("", 14, Ink, FontWeight.Normal);
		label.Opacity = 0.85;
		label.HorizontalAlignment = HorizontalAlignment.Center;
		label.Margin = new Thickness(0, 0, 0, 0);
		numberRow = number;
		var center = Ui.Stack(4, number, label, pairHost);
		center.HorizontalAlignment = HorizontalAlignment.Center;
		center.VerticalAlignment = VerticalAlignment.Center;
		center.Margin = new Thickness(0, 18, 0, 0);
		// The ring takes almost half the width (it was two fifths) and is drawn larger than the text in it.
		gauge = new Viewbox { Stretch = Stretch.Uniform, MaxWidth = 200, VerticalAlignment = VerticalAlignment.Center, Child = new Panel { Width = 176, Height = 176, Children = { ring, center } }, Margin = new Thickness(0, 0, 12, 0) };
		Control body;
		if (list)
		{
			bars.Tag = "elastic";
			body = bars;
		}
		else
		{
			// "Graphic size" also widens the ring's share of the card.
			double g = Math.Clamp(s.ActiveWidget?.Graphics ?? 1, 0.5, 2);
			var grid = new Grid { ColumnDefinitions = new ColumnDefinitions { new ColumnDefinition(5 * g, GridUnitType.Star), new ColumnDefinition(6, GridUnitType.Star) } };
			grid.Children.Add(gauge);
			// Drives: no ring for the hottest drive, the bars of all drives take the full width.
			if (kind == WidgetKind.Drives)
			{
				gauge.IsVisible = false;
				Grid.SetColumnSpan(bars, 2);
				bars.Tag = "elastic";
			}
			Grid.SetColumn(bars, kind == WidgetKind.Drives ? 0 : 1);
			grid.Children.Add(bars);
			body = grid;
		}
		var root = Ui.Stack(12, header, body);
		root.Tag = "elastic";
		Child = root;
	}

	protected override void Show(Summary sum)
	{
		icon.Data = Geometry.Parse(sum.Glyph);
		bold.Text = Short(Kind);
		string device = Naming.Hardware(Own, Index, sum.Name);
		name.Text = Naming.ShowHardware(Own, S) && device.Length > 0 ? device : Naming.Title(Own, Index, sum.Title);
		// "FPS FPS", "PING Ping": the short name is left out when the title already says it.
		bold.IsVisible = !string.Equals(bold.Text, name.Text, StringComparison.OrdinalIgnoreCase);
		bool none = sum.Value == "—";
		value.Text = sum.Value;
		unit.Text = none ? "" : sum.Unit;
		label.Text = sum.HeroLabel.Length > 0 ? sum.HeroLabel : WidgetSensors.PrimaryLabel(Kind);
		Color purple = Own?.Accent.Length > 0 ? Tint : Accent("#8b2cf5");
		Color pink = Accent("#e64bd3");
		ring.Set(none ? 0 : sum.Fraction, purple, pink);
		gauge.IsVisible = sum.Value.Length > 0 && !AllDrives;
		Color track = P.Dark ? Color.Parse("#3a3a3e") : P.Track;
		var rates = Rates();
		var shown = rates != null || AllDrives ? All() : AllBut(sum);
		var rest = WithoutRates(shown);
		// Network in the ring: download and upload one above the other in the same size.
		pairHost.IsVisible = !list && rates != null;
		numberRow.IsVisible = label.IsVisible = list || rates == null;
		pairHost.Child = !list && rates is var (pd, pu) ? RatePair(pd, pu, 28, null, Ink, Ink, stacked: true, center: true) : null;
		((Control)pairHost.Parent!).Margin = new Thickness(0, pairHost.IsVisible ? 6 : 18, 0, 0);
		bars.Children.Clear();
		void Add(string text, string reading, double fraction, Color fill, bool lead)
		{
			var bar = new FillBack { Height = lead ? 14 : 11, Radius = 0, Track = track };
			bar.Animate(fraction, fill);
			var (num, u) = Format.Split(reading);
			var right = Ui.Row(0, Text(num, lead ? 18 : 13, Ink, FontWeight.Bold, null, true), Text(u, lead ? 11 : 9.5, Ink, FontWeight.Normal));
			((Control)right.Children[1]).VerticalAlignment = VerticalAlignment.Bottom;
			((Control)right.Children[1]).Margin = new Thickness(lead ? 2 : 1, 0, 0, lead ? 3 : 2);
			var l = Text(text, lead ? 13 : 12, Ink, lead ? FontWeight.SemiBold : FontWeight.Normal);
			l.Opacity = lead ? 0.9 : 0.75;
			l.TextTrimming = TextTrimming.CharacterEllipsis;
			l.VerticalAlignment = VerticalAlignment.Bottom;
			bars.Children.Add(Ui.Stack(3, bar, Ui.Columns("*,Auto", l, right)));
		}
		if (list && rates is var (ld, lu))
		{
			// Network: download and upload are both lead bars.
			Add(ld.Label, ld.Value, ld.Fraction, pink, true);
			Add(lu.Label, lu.Value, lu.Fraction, pink, true);
		}
		else if (list && sum.Value.Length > 0 && !AllDrives)
		{
			// The value the ring carried, as the first and slightly larger bar.
			Add(label.Text, none ? "—" : (sum.Value + " " + sum.Unit).Trim(), none ? 0 : sum.Fraction, pink, true);
		}
		foreach (Reading r in rest)
		{
			Add(r.Label, r.Value, r.Fraction, purple, false);
		}
		foreach (var (l, v) in Facts(sum, shown, Math.Max(0, (list ? 6 : 5) - bars.Children.Count)))
		{
			var lt = Text(l, 12, Ink);
			lt.Opacity = 0.75;
			bars.Children.Add(Mark(Ui.Columns("*,Auto", lt, Text(v, 13, Ink, FontWeight.Bold, null, true)), v));
		}
		bars.IsVisible = bars.Children.Count > 0;
	}
}

// 6 · Sci-fi monitor: chamfered metal frame, centred title between two coloured lines, a ring of ticks
// from pink to blue, a large digital number with the load below and two readings at the foot.
public sealed class SciFiTile : DashTile
{
	private readonly Border lineLeft = new Border { Height = 2, Width = 26, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };

	private readonly Border lineRight = new Border { Height = 2, Width = 26, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };

	private readonly TextBlock title;

	private readonly TickRing ring = new TickRing { Width = 200, Height = 200, TickLength = 0.06 };

	// Network: upload on a second, smaller ring inside the download ring.
	private readonly TickRing upRing = new TickRing { Width = 158, Height = 158, TickLength = 0.06, IsVisible = false, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

	private readonly TextBlock value;

	private readonly TextBlock unit;

	private readonly TextBlock load;

	private readonly Border divider = new Border { Height = 2, Width = 120, Margin = new Thickness(0, 5, 0, 5) };

	private readonly TextBlock foot;

	private readonly Viewbox face;

	private readonly Border pairHost = new Border { IsVisible = false };

	private readonly StackPanel number;

	private readonly SegmentBar segments = new SegmentBar { Height = 12 };

	private readonly DriveRows drives;

	private readonly StackPanel rows = new StackPanel { Spacing = 5 };

	public SciFiTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s)
		: base(kind, index, p, m, s)
	{
		Background = null;
		BorderThickness = new Thickness(0);
		Padding = new Thickness(0);
		title = Text("", 15, Ink, FontWeight.DemiBold, Digital);
		title.LetterSpacing = 2;
		var header = Ui.Row(0, lineLeft, title, lineRight);
		header.HorizontalAlignment = HorizontalAlignment.Center;
		value = Text("", 66, Palette.Mix(Ink, Soft, 0.15), FontWeight.DemiBold, Digital, true);
		unit = Text("", 18, Palette.Mix(Ink, Soft, 0.15), FontWeight.Medium, Digital);
		unit.VerticalAlignment = VerticalAlignment.Bottom;
		unit.Margin = new Thickness(3, 0, 0, 12);
		number = Ui.Row(0, value, unit);
		number.HorizontalAlignment = HorizontalAlignment.Center;
		load = Text("", 19.5, Ink, FontWeight.DemiBold, Digital, true);
		load.HorizontalAlignment = HorizontalAlignment.Center;
		foot = Text("", 15, Ink, FontWeight.Medium, Digital, true);
		foot.HorizontalAlignment = HorizontalAlignment.Center;
		foot.Opacity = 0.85;
		var footFit = Fit(foot, HorizontalAlignment.Center);
		footFit.MaxWidth = 100;
		// Load and the line under it (watts) stand one below the other without a dividing line, the same in
		// every widget (a line only where a second line exists looked uneven).
		footFit.Margin = new Thickness(0, 4, 0, 0);
		var center = Ui.Stack(0, number, pairHost, load, footFit);
		center.HorizontalAlignment = HorizontalAlignment.Center;
		center.VerticalAlignment = VerticalAlignment.Center;
		// Slightly lower than before: thinner ticks, larger numbers, more room for the list below.
		face = new Viewbox { Stretch = Stretch.Uniform, MaxWidth = 210, HorizontalAlignment = HorizontalAlignment.Center, Child = new Panel { Width = 200, Height = 200, Children = { ring, upRing, center } } };
		// Drives: every drive as a segmented line instead of the ring for the hottest one.
		drives = new DriveRows(8, 11, 13, Soft, () => new SegmentBar { Height = 10 },
			(c, r) => ((SegmentBar)c).Set(r.Fraction, Accent("#ec4899"), Accent("#3b82f6"), P.Dark ? Color.Parse("#25272e") : P.Track), r => Heat(r.Value) ? Opaque(r.Color) : Ink, Digital);
		var inner = Ui.Stack(10, header, face, drives, segments, rows);
		inner.Tag = "elastic";
		Child = new ChamferFrame
		{
			Fill = P.DesignBack("#0d0e11"),
			Edge = P.Dark ? Color.Parse("#2c2f37") : P.Stroke,
			Padding = new Thickness(16, 12, 16, 16),
			Child = inner
		};
	}

	private Color Main => Own?.Accent.Length > 0 ? Tint : Kind switch
	{
		WidgetKind.Cpu or WidgetKind.Memory => Accent("#ec4899"),
		WidgetKind.Gpu => Accent("#a855f7"),
		_ => Accent("#3b82f6")
	};

	protected override void Show(Summary sum)
	{
		Color main = Main;
		Color pink = Accent("#ec4899");
		Color blue = Accent("#3b82f6");
		((ChamferFrame)Child!).Accent = main;
		lineLeft.Background = lineRight.Background = divider.Background = Palette.Brush(main);
		title.Text = Short(Kind);
		bool none = sum.Value == "—";
		bool level = Kind is WidgetKind.Memory or WidgetKind.Volumes or WidgetKind.Battery;
		value.Text = sum.Value;
		value.FontSize = 66;
		unit.Text = none ? "" : sum.Unit;
		bool second = sum.Second.HasValue && sum.SecondLabel.Length > 0;
		load.Text = second ? sum.SecondLabel + " " + WidgetSensors.SecondaryLabel(Kind).ToUpper(L.Culture) : Which(sum).ToUpper(L.Culture);
		load.Foreground = Palette.Brush(main);
		var rates = Rates();
		// Network: download and upload as two digital numbers of the same size inside the ring.
		number.IsVisible = rates == null;
		pairHost.IsVisible = rates != null;
		Color cyan = Accent("#22d3ee");
		// Download on the outer ring (cyan), upload on the inner ring (pink); the numbers in the same colours.
		pairHost.Child = rates is var (down, up) ? RatePair(down, up, 26, Digital, cyan, pink, stacked: true, center: true) : null;
		upRing.IsVisible = rates != null;
		if (rates != null)
		{
			load.Text = "";
		}
		load.IsVisible = load.Text.Length > 0;
		var shown = AllDrives ? All() : Rest(sum);
		var rest = AllDrives ? new List<Reading>() : WithoutRates(shown);
		string peakLabel = L.T("2-min peak");
		string installed = L.T("Installed");
		var (snap, history) = Last;
		// Memory: the 2-minute peak is a mark on the ring, not a second "39 %"; under the load stands used of
		// installed instead (so "Installed" leaves the list).
		bool memoryFoot = Kind == WidgetKind.Memory && snap.Memory != null && S.Shows("mem.used");
		rest = rest.Where(r => !(Kind == WidgetKind.Memory && r.Label == peakLabel)).ToList();
		var feet = memoryFoot ? new List<Reading>() : rest.Where(r => !Heat(r.Value)).Take(2).ToList();
		// Every line in the ring says what it is ("POWER 81 W").
		foot.Text = memoryFoot
			? Format.Number(snap.Memory!.UsedGb, "0.0") + " / " + Format.Gigabytes(snap.Memory.TotalGb) + " " + L.T("Used").ToUpper(L.Culture)
			: string.Join("  |  ", feet.Select(r => Short(r.Label).ToUpper(L.Culture) + " " + r.Value));
		foot.IsVisible = foot.Text.Length > 0;
		divider.IsVisible = false;
		var track = P.Dark ? Color.Parse("#2a2c33") : P.Track;
		if (rates is var (d, u))
		{
			ring.Set(SegmentScale.Fraction(Kind, d.Label, d.Value, snap) ?? sum.Fraction, Accent("#0ea5e9"), cyan, track);
			upRing.Set(SegmentScale.Fraction(Kind, u.Label, u.Value, snap) ?? 0, Accent("#a855f7"), pink, track);
			ring.SetPeak(null, Ink);
		}
		else
		{
			ring.Set(none ? 0 : sum.Fraction, pink, blue, track);
			ring.SetPeak(none ? null : PeakFraction(history), Ink);
		}
		// The big number turns amber, then red when it is hot or nearly full.
		value.Foreground = Palette.Brush(none ? Palette.Mix(Ink, Soft, 0.15) : State(sum));
		face.IsVisible = sum.Value.Length > 0 && !AllDrives;
		drives.IsVisible = false;
		if (AllDrives)
		{
			drives.Show(DriveList());
		}
		segments.IsVisible = level && !none;
		if (segments.IsVisible)
		{
			segments.Set(sum.Fraction, pink, blue, P.Dark ? Color.Parse("#25272e") : P.Track);
		}
		rows.Children.Clear();
		var others = rest.Except(feet).Select(r => (r.Label, r.Value, Heat(r.Value) ? (Color?)Opaque(r.Color) : null))
			.Concat(Facts(sum, shown, 3).Select(f => (f.Label, f.Value, (Color?)null)))
			.Where(o => !(Kind == WidgetKind.Memory && (o.Label == peakLabel || (memoryFoot && o.Label == installed))));
		foreach (var (l, v, c) in others)
		{
			rows.Children.Add(Mark(Ui.Columns("*,Auto", Text(l.ToUpper(L.Culture), 12.5, Soft, FontWeight.Medium, Digital), Text(v, 15, c ?? Ink, FontWeight.DemiBold, Digital, true)), v));
		}
		rows.IsVisible = rows.Children.Count > 0;
	}

	private static string Short(string label) => label.Length > 10 ? label[..10] : label;

	// Where the ring was at its highest in the recorded minutes, on the ring's own scale.
	private double? PeakFraction(History history)
	{
		double? Top(string key)
		{
			double[] values = history.Read(key).Where(double.IsFinite).ToArray();
			return values.Length > 0 ? values.Max() : null;
		}
		return Kind switch
		{
			WidgetKind.Cpu => Top("cpu.temp") is double c ? Format.Heat(c) : null,
			WidgetKind.Gpu when Index == 0 => Top("gpu.temp") is double g ? Format.Heat(g) : null,
			WidgetKind.Memory when S.Shows("mem.peak") => Top("mem.load") is double m ? m / 100.0 : null,
			WidgetKind.Power => Top("power") is double w ? Math.Clamp(w / 600.0, 0, 1) : null,
			WidgetKind.Ping => Top("ping") is double ms ? Math.Clamp(ms / 150.0, 0, 1) : null,
			_ => null
		};
	}

	// Normal: white; amber from 80 °C or 85 % full, red from 90 °C or 95 % full.
	private Color State(Summary sum)
	{
		Color normal = Palette.Mix(Ink, Soft, 0.15);
		Color amber = Opaque(P.Ramp[2]);
		Color red = Opaque(P.Ramp[^1]);
		if (!double.TryParse(sum.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double v))
		{
			return normal;
		}
		if (sum.Unit is "°C" or "°F")
		{
			double celsius = sum.Unit == "°F" ? (v - 32) / 1.8 : v;
			return celsius >= 90 ? red : celsius >= 80 ? amber : normal;
		}
		if (sum.Unit == "%" && Kind is WidgetKind.Memory or WidgetKind.Volumes)
		{
			return v >= 95 ? red : v >= 85 ? amber : normal;
		}
		return normal;
	}
}

// A thin bar with a colour gradient over the filled part.
public sealed class GradientBar : Control
{
	private double fraction;

	private Color from = Colors.Cyan;

	private Color to = Colors.Magenta;

	private Color track = Colors.DimGray;

	public void Set(double value, Color a, Color b, Color back)
	{
		fraction = double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0;
		from = a;
		to = b;
		track = back;
		InvalidateVisual();
	}

	public override void Render(DrawingContext ctx)
	{
		var r = new Rect(Bounds.Size);
		double radius = r.Height / 2;
		ctx.DrawRectangle(Palette.Brush(track), null, r, radius, radius);
		if (fraction > 0.003)
		{
			var fill = new Rect(0, 0, Math.Max(r.Height, r.Width * fraction), r.Height);
			ctx.DrawRectangle(new LinearGradientBrush
			{
				StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
				EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
				GradientStops = { new GradientStop(from, 0), new GradientStop(to, 1) }
			}, null, fill, radius, radius);
		}
	}
}

// Thermostat ring: 270°, warm-to-cool gradient up to the value and a glowing knob there.
public sealed class KnobRing : Control
{
	private double fraction;

	private Color warm = Colors.Orange;

	private Color cool = Colors.LightBlue;

	private Color track = Colors.DimGray;

	public void Set(double value, Color a, Color b, Color back)
	{
		fraction = double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0;
		warm = a;
		cool = b;
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
		double thick = size * 0.09;
		double r = size / 2 - thick * 1.6;
		ctx.DrawGeometry(null, new Pen(Palette.Brush(track), thick, lineCap: PenLineCap.Round), Rings.Arc(c, r, 135, 270));
		if (fraction > 0.004)
		{
			var brush = new LinearGradientBrush
			{
				StartPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
				EndPoint = new RelativePoint(0.3, 0, RelativeUnit.Relative),
				GradientStops = { new GradientStop(cool, 0), new GradientStop(warm, 1) }
			};
			ctx.DrawGeometry(null, new Pen(brush, thick, lineCap: PenLineCap.Round), Rings.Arc(c, r, 135, 270 * fraction));
		}
		double angle = (135 + 270 * fraction) * Math.PI / 180;
		var knob = new Point(c.X + r * Math.Cos(angle), c.Y + r * Math.Sin(angle));
		double kr = thick * 0.95;
		ctx.DrawEllipse(Palette.Brush(Color.FromArgb(70, warm.R, warm.G, warm.B)), null, knob, kr * 1.6, kr * 1.6);
		ctx.DrawEllipse(Palette.Brush(warm), new Pen(Palette.Brush(Colors.White), 2), knob, kr, kr);
		ctx.DrawEllipse(Palette.Brush(Colors.White), null, new Point(knob.X + kr * 0.35, knob.Y - kr * 0.1), kr * 0.22, kr * 0.22);
	}
}

// Purple half ring: 240° from purple to pink up to the value, two small dots at its start.
public sealed class SweepRing : Control
{
	private double fraction;

	private Color from = Colors.Purple;

	private Color to = Colors.HotPink;

	public void Set(double value, Color a, Color b)
	{
		fraction = double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0;
		from = a;
		to = b;
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
		double thick = size * 0.1;
		double r = size / 2 - thick;
		double sweep = Math.Max(0.04, fraction) * 240;
		var brush = new LinearGradientBrush
		{
			StartPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
			EndPoint = new RelativePoint(1, 0.3, RelativeUnit.Relative),
			GradientStops = { new GradientStop(from, 0), new GradientStop(to, 1) }
		};
		ctx.DrawGeometry(null, new Pen(brush, thick, lineCap: PenLineCap.Round), Rings.Arc(c, r, 150, sweep));
		// Two small dots trail the start of the arc, getting smaller.
		for (int i = 1; i <= 2; i++)
		{
			double ang = (150 - i * 13) * Math.PI / 180;
			var dotPoint = new Point(c.X + r * Math.Cos(ang), c.Y + r * Math.Sin(ang));
			double dr = thick * (0.42 - i * 0.1);
			ctx.DrawEllipse(Palette.Brush(from), null, dotPoint, dr, dr);
		}
	}
}

// Sci-fi ring: short radial ticks over 270°, the filled ones from pink to blue, and a thin outer circle.
public sealed class TickRing : Control
{
	private double fraction;

	private Color from = Colors.HotPink;

	private Color to = Colors.DodgerBlue;

	private Color off = Colors.DimGray;

	private double? peak;

	private Color mark = Colors.White;

	/// <summary>Length of the ticks as a share of the ring's size.</summary>
	public double TickLength { get; set; } = 0.075;

	public void Set(double value, Color a, Color b, Color back)
	{
		fraction = double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0;
		from = a;
		to = b;
		off = back;
		InvalidateVisual();
	}

	/// <summary>A bright, longer tick where the highest value of the last minutes was (null: none).</summary>
	public void SetPeak(double? value, Color color)
	{
		peak = value is double v && double.IsFinite(v) ? Math.Clamp(v, 0, 1) : null;
		mark = color;
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
		double outer = size / 2 - 2;
		ctx.DrawGeometry(null, new Pen(Palette.Brush(Color.FromArgb(90, off.R, off.G, off.B)), 1.2), Rings.Arc(c, outer, 120, 300));
		const int ticks = 56;
		double r1 = outer - 8;
		double r2 = outer - 8 - size * TickLength;
		if (peak is double top)
		{
			double a = (135 + 270 * top) * Math.PI / 180;
			var pen = new Pen(Palette.Brush(mark), size * 0.022, lineCap: PenLineCap.Round);
			ctx.DrawLine(pen, new Point(c.X + (outer - 2) * Math.Cos(a), c.Y + (outer - 2) * Math.Sin(a)), new Point(c.X + (r2 - 4) * Math.Cos(a), c.Y + (r2 - 4) * Math.Sin(a)));
		}
		int lit = (int)Math.Round(fraction * ticks);
		for (int i = 0; i < ticks; i++)
		{
			double t = i / (double)(ticks - 1);
			double angle = (135 + 270 * t) * Math.PI / 180;
			Color color = i < lit ? Palette.Mix(from, to, t) : off;
			var pen = new Pen(Palette.Brush(color), size * 0.018, lineCap: PenLineCap.Round);
			ctx.DrawLine(pen, new Point(c.X + r1 * Math.Cos(angle), c.Y + r1 * Math.Sin(angle)), new Point(c.X + r2 * Math.Cos(angle), c.Y + r2 * Math.Sin(angle)));
		}
	}
}

// A level made of small segments that fade from pink to blue.
public sealed class SegmentBar : Control
{
	private double fraction;

	private Color from = Colors.HotPink;

	private Color to = Colors.DodgerBlue;

	private Color off = Colors.DimGray;

	public void Set(double value, Color a, Color b, Color back)
	{
		fraction = double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0;
		from = a;
		to = b;
		off = back;
		InvalidateVisual();
	}

	public override void Render(DrawingContext ctx)
	{
		double w = Bounds.Width;
		if (w < 20)
		{
			return;
		}
		const double seg = 3, gap = 2;
		int count = (int)((w + gap) / (seg + gap));
		int lit = (int)Math.Round(fraction * count);
		for (int i = 0; i < count; i++)
		{
			Color color = i < lit ? Palette.Mix(from, to, i / (double)Math.Max(1, count - 1)) : off;
			ctx.DrawRectangle(Palette.Brush(color), null, new Rect(i * (seg + gap), 0, seg, Bounds.Height), 1, 1);
		}
	}
}

// Panel with cut corners, a metal edge and short accent marks at the top and bottom.
public sealed class ChamferFrame : Decorator
{
	public Color Fill { get; set; } = Color.Parse("#0d0e11");

	public Color Edge { get; set; } = Color.Parse("#2c2f37");

	public Color Accent { get; set; } = Colors.HotPink;

	public override void Render(DrawingContext ctx)
	{
		double w = Bounds.Width, h = Bounds.Height, cut = 12;
		if (w < 2 * cut || h < 2 * cut)
		{
			return;
		}
		var g = new StreamGeometry();
		using (var x = g.Open())
		{
			x.BeginFigure(new Point(cut, 0.5), true);
			x.LineTo(new Point(w - cut, 0.5));
			x.LineTo(new Point(w - 0.5, cut));
			x.LineTo(new Point(w - 0.5, h - cut));
			x.LineTo(new Point(w - cut, h - 0.5));
			x.LineTo(new Point(cut, h - 0.5));
			x.LineTo(new Point(0.5, h - cut));
			x.LineTo(new Point(0.5, cut));
			x.EndFigure(true);
		}
		var fill = new LinearGradientBrush
		{
			StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
			EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
			GradientStops = { new GradientStop(Palette.Mix(Fill, Edge, 0.18), 0), new GradientStop(Fill, 0.35), new GradientStop(Fill, 1) }
		};
		ctx.DrawGeometry(fill, new Pen(Palette.Brush(Edge), 1.5), g);
		var mark = new Pen(Palette.Brush(Accent), 2.5, lineCap: PenLineCap.Round);
		ctx.DrawLine(mark, new Point(w / 2 - 28, h - 4), new Point(w / 2 + 28, h - 4));
		ctx.DrawLine(new Pen(Palette.Brush(Color.FromArgb(160, Accent.R, Accent.G, Accent.B)), 2, lineCap: PenLineCap.Round), new Point(cut + 10, 4), new Point(cut + 30, 4));
		ctx.DrawLine(new Pen(Palette.Brush(Edge), 1), new Point(w - cut - 40, 4), new Point(w - cut - 10, 4));
	}
}

// Arc geometry shared by the rings above.
internal static class Rings
{
	public static Geometry Arc(Point c, double r, double start, double sweep)
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
