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

/// <summary>
/// One part of the overview: processor, graphics, memory, video memory, network or one drive. Main and Sub
/// are the two values of the part (temperature and load, used and total, download and upload).
/// </summary>
public sealed record OverviewRow(string Key, string Label, string Main, string Sub, double Fraction, double? Second, Color Color, string? Spark = null)
{
	public bool Pair => Key == "overview.network";
}

/// <summary>
/// The overview ("the whole PC on one card") as small versions of the design's own widgets: one mini widget
/// per part in a grid of equal cells. Rings for the ring designs (Messuhr, Doppelring, Regler, Lila, Sci-Fi),
/// a value with its curve for the graph designs, a value with the design's bar for the bar designs and
/// plain value cells for the text designs. Equal cells and values that shrink instead of growing keep
/// everything in place from one update to the next.
/// </summary>
public sealed class OverviewTile : Border, IWidget, INamed
{
	private enum Visual { Spark, Bar, Text, DialRing, DoubleRing, KnobRing, SweepRing, TickRing, Level, Pill }

	private enum Head { Badge, Dot, Chip, Mono, Caps, Purple, SciFi, Capsule, Plain, Status, Apple }

	private static readonly FontFamily Mono = new FontFamily("Cascadia Mono, Consolas, DejaVu Sans Mono, fonts:Inter#Inter, $Default");

	private static readonly FontFamily Digital = new FontFamily("Bahnschrift, DIN Alternate, fonts:Inter#Inter, $Default");

	private const double RingBase = 140;

	private readonly WidgetStyle style;

	private readonly Palette P;

	private readonly Metrics M;

	private readonly Settings S;

	private readonly Visual visual;

	private readonly Head head;

	private readonly FontFamily? font;

	private readonly TextBlock title;

	// Two mini widgets per row. The rows are elastic, so a tall sidebar spreads them over its whole height,
	// and the ring or curve of the first cell in a row grows with it (the second cell follows its row).
	private readonly StackPanel grid = new StackPanel { Spacing = 0, Tag = "elastic" };

	private WidgetSlot? slot;

	private int slotIndex;

	private string shape = "";

	private sealed class Cell
	{
		public required TextBlock Caption;

		public required TextBlock Main;

		public required TextBlock Sub;

		public Control? Shape;

		public AreaChart? Chart;

		public Control? Root;
	}

	private readonly List<Cell> cells = new();

	public OverviewTile(WidgetStyle style, Palette p, Metrics m, Settings s)
	{
		// The graph variants draw the overview like their base design (a bar under each value).
		style = style switch
		{
			WidgetStyle.NeonHudGraph => WidgetStyle.NeonHud,
			WidgetStyle.SegmentGraph => WidgetStyle.Segments,
			WidgetStyle.FillBarsGraph => WidgetStyle.FillBars,
			WidgetStyle.PillRowGraph => WidgetStyle.PillRow,
			_ => style
		};
		this.style = style;
		P = p;
		M = m;
		S = s;
		(head, visual, font) = style switch
		{
			WidgetStyle.Aperture => (Head.Chip, Visual.DoubleRing, (FontFamily?)null),
			WidgetStyle.PixelDeck => (Head.Status, Visual.Bar, null),
			WidgetStyle.HorizonSync => (Head.Plain, Visual.Spark, null),
			WidgetStyle.Panorama => (Head.Plain, Visual.Spark, (FontFamily?)null),
			WidgetStyle.Terminal => (Head.Mono, Visual.Text, null),
			WidgetStyle.Thermal => (Head.Caps, Visual.Level, null),
			WidgetStyle.MosaicPro => (Head.Apple, Visual.Pill, null),
			WidgetStyle.RibbonFlow => (Head.Capsule, Visual.Pill, null),
			WidgetStyle.SplitLens => (Head.Plain, Visual.Spark, null),
			WidgetStyle.Meridian => (Head.Plain, Visual.Spark, (FontFamily?)null),
			WidgetStyle.Strata => (Head.Apple, Visual.Bar, null),
			WidgetStyle.Vector => (Head.Plain, Visual.Level, null),
			WidgetStyle.Orbit => (Head.Chip, Visual.DialRing, null),
			WidgetStyle.Lucent => (Head.Apple, Visual.Pill, null),
			WidgetStyle.Circuit => (Head.Mono, Visual.Bar, null),
			WidgetStyle.CleanDark or WidgetStyle.CompactPro or WidgetStyle.PerformanceView => (Head.Dot, Visual.Spark, (FontFamily?)null),
			WidgetStyle.Dial => (Head.Chip, Visual.DialRing, (FontFamily?)null),
			WidgetStyle.RingPair => (Head.Badge, Visual.DoubleRing, null),
			WidgetStyle.Thermostat => (Head.Caps, Visual.KnobRing, null),
			WidgetStyle.PurpleDash or WidgetStyle.PurpleList => (Head.Purple, Visual.SweepRing, null),
			WidgetStyle.SciFi => (Head.SciFi, Visual.TickRing, Digital),
			WidgetStyle.Segments or WidgetStyle.Duplex or WidgetStyle.SignalRows or WidgetStyle.OpenPanel => (Head.Badge, Visual.Bar, null),
			WidgetStyle.NeonHud => (Head.Mono, Visual.Bar, Mono),
			WidgetStyle.NeonTiles => (Head.Status, Visual.Text, Mono),
			WidgetStyle.SmartPanel => (Head.Caps, Visual.Text, null),
			WidgetStyle.Pill => (Head.Capsule, Visual.Pill, null),
			WidgetStyle.PillRow or WidgetStyle.FillBars => (Head.Badge, Visual.Pill, null),
			WidgetStyle.LevelColumns => (Head.Badge, Visual.Level, null),
			WidgetStyle.TableGraph or WidgetStyle.BadgeListGraph => (Head.Badge, Visual.Bar, null),
			WidgetStyle.Table or WidgetStyle.BadgeList or WidgetStyle.Clarity or WidgetStyle.ControlDeck or WidgetStyle.ClarityColor or WidgetStyle.ClarityRows or WidgetStyle.ClarityTiles => (Head.Badge, Visual.Text, null),
			WidgetStyle.Evolution => (Head.Dot, Visual.Spark, null),
			WidgetStyle.TwinFocus => (Head.Plain, Visual.Spark, null),
			WidgetStyle.Pure or WidgetStyle.PureBar or WidgetStyle.PureNumber or WidgetStyle.PureSegments => (Head.Apple, Visual.Text, null),
			WidgetStyle.PureWidgets or WidgetStyle.PureWidgetsRight => (Head.Apple, Visual.DialRing, null),
			_ => (Head.Badge, Visual.Spark, null)
		};
		Frame();
		title = Ui.Text(L.T("Overview"), 12.5, p.Ink, FontWeight.DemiBold);
		grid.Margin = new Thickness(-4, 0, -4, -6);
		var root = Ui.Stack(head is Head.Capsule ? 8 : 10, Header(), grid);
		if (head == Head.SciFi)
		{
			Child = new ChamferFrame { Fill = p.DesignBack("#0d0e11"), Edge = p.Dark ? Color.Parse("#2c2f37") : p.Stroke, Accent = Accent("#3b82f6"), Padding = new Thickness(16, 12, 16, 16), Child = root };
		}
		else
		{
			Child = root;
		}
	}

	private Color Tint => Accents.For(WidgetKind.Overview, slot ?? S.ActiveWidget, P);

	private Color Accent(string hex) => Accents.Design(hex, P);

	private Color Dark(string hex) => P.DesignBack(hex);

	private Color Edge(string hex) => P.Dark ? Color.Parse(hex) : P.Stroke;

	private Color Track => P.Dark ? Palette.Mix(P.Card, P.Ink, 0.1) : P.Track;

	private void Frame()
	{
		if (LocalDesigns.Contains(style)) { LocalDesigns.Frame(this, style, P, M); return; }
		Background = Palette.Brush(P.Card);
		BorderBrush = Palette.Brush(P.Stroke);
		BorderThickness = Ui.CardEdge;
		CornerRadius = Ui.CardRadius(M);
		Padding = new Thickness(M.Pad, M.Pad - 4, M.Pad, M.Pad - 2);
		switch (style)
		{
		case WidgetStyle.CleanDark:
		case WidgetStyle.CompactPro:
		case WidgetStyle.PerformanceView:
			CornerRadius = new CornerRadius(style == WidgetStyle.CleanDark ? 24 : style == WidgetStyle.CompactPro ? 12 : 14);
			BorderThickness = new Thickness(style == WidgetStyle.PerformanceView ? 1 : 0);
			Padding = new Thickness(M.Pad, style == WidgetStyle.CompactPro ? 10 : 14);
			if (style == WidgetStyle.PerformanceView) Background = Palette.Brush(Dark("#101925"));
			break;
		case WidgetStyle.Pill:
			Background = Brushes.Transparent;
			BorderThickness = new Thickness(0);
			Padding = new Thickness(0);
			break;
		case WidgetStyle.SignalRows:
			BorderThickness = new Thickness(1);
			CornerRadius = new CornerRadius(M.Radius * 0.3);
			break;
		case WidgetStyle.OpenPanel:
			Background = Brushes.Transparent;
			BorderThickness = new Thickness(0);
			CornerRadius = new CornerRadius(0);
			break;
		case WidgetStyle.Dial:
			Background = Palette.Brush(P.Dark ? Color.Parse("#111213") : P.Card);
			Padding = new Thickness(12, 10, 12, 12);
			break;
		case WidgetStyle.NeonHud:
			Background = Palette.Brush(Dark("#0b0f17"));
			BorderBrush = Palette.Brush(Edge("#1c2433"));
			BorderThickness = new Thickness(1);
			CornerRadius = new CornerRadius(10);
			Padding = new Thickness(13, 11, 13, 12);
			break;
		case WidgetStyle.PureBar:
        case WidgetStyle.PureNumber:
        case WidgetStyle.PureSegments:
        case WidgetStyle.Pure:
		case WidgetStyle.PureWidgets:
        case WidgetStyle.PureWidgetsRight:
			Background = Palette.Brush(Dark("#1c1c1e"));
			BorderThickness = new Thickness(0);
			CornerRadius = new CornerRadius(22);
			Padding = new Thickness(18, 14, 18, 16);
			break;
		case WidgetStyle.NeonTiles:
			Background = Palette.Brush(Dark("#090c12"));
			BorderBrush = Palette.Brush(Edge("#1a2130"));
			BorderThickness = new Thickness(1);
			CornerRadius = new CornerRadius(12);
			Padding = new Thickness(13, 12, 13, 12);
			break;
		case WidgetStyle.Thermostat:
			Background = Palette.Brush(Dark("#0c0d10"));
			BorderBrush = Palette.Brush(Edge("#1c1d22"));
			BorderThickness = new Thickness(1);
			CornerRadius = new CornerRadius(14);
			Padding = new Thickness(14, 12, 14, 12);
			break;
		case WidgetStyle.SmartPanel:
			Background = Palette.Brush(Dark("#0e0f12"));
			BorderBrush = Palette.Brush(Edge("#1d1f25"));
			BorderThickness = new Thickness(1);
			CornerRadius = new CornerRadius(14);
			Padding = new Thickness(14, 12, 14, 14);
			break;
		case WidgetStyle.PurpleDash:
		case WidgetStyle.PurpleList:
			Background = Palette.Brush(Dark("#050506"));
			BorderBrush = Palette.Brush(Edge("#2b2b2f"));
			BorderThickness = new Thickness(1);
			CornerRadius = new CornerRadius(10);
			Padding = new Thickness(14, 12, 14, 12);
			break;
		case WidgetStyle.SciFi:
			Background = null;
			BorderThickness = new Thickness(0);
			Padding = new Thickness(0);
			break;
		}
	}

	private Control Header()
	{
		Color tint = Tint;
		switch (head)
		{
		case Head.Dot:
		{
			var dot = new Avalonia.Controls.Shapes.Ellipse { Width = 8, Height = 8, Fill = Palette.Brush(tint), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
			return Ui.Row(0, dot, title);
		}
		case Head.Chip:
		{
			var chip = Text("PC", 10, Accent("#29b6d8"), FontWeight.Bold);
			var box = new Border { CornerRadius = new CornerRadius(5), Padding = new Thickness(6, 2), Child = chip, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0), Background = Palette.Brush(P.Dark ? Color.Parse("#0e2f3a") : Palette.Alpha(Accent("#29b6d8"), 0.15)) };
			title.FontSize = 13;
			return Ui.Row(0, box, title);
		}
		case Head.Mono:
			title.FontFamily = Mono;
			title.FontSize = 11;
			title.FontWeight = FontWeight.Bold;
			title.LetterSpacing = 0.6;
			title.Foreground = Palette.Brush(Accent("#22d3ee"));
			return title;
		case Head.Status:
		{
			var dot = new Avalonia.Controls.Shapes.Ellipse { Width = 7, Height = 7, Fill = Palette.Brush(Accent("#22d3ee")), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 7, 0) };
			title.FontFamily = Mono;
			title.FontSize = 9.5;
			title.FontWeight = FontWeight.Bold;
			title.LetterSpacing = 0.8;
			title.Foreground = Palette.Brush(Accent("#22d3ee"));
			return Ui.Row(0, dot, title);
		}
		case Head.Caps:
			title.FontSize = 9.5;
			title.FontWeight = FontWeight.Medium;
			title.LetterSpacing = 1.2;
			title.Foreground = Palette.Brush(P.Soft);
			return title;
		case Head.Purple:
		{
			var icon = new Avalonia.Controls.Shapes.Path { Width = 18, Height = 18, Stretch = Stretch.Uniform, StrokeThickness = 1.4, Stroke = Palette.Brush(P.Ink), Data = Geometry.Parse(Glyphs.Overview), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
			var bold = Ui.Text("PC", 15, P.Ink, FontWeight.Bold);
			title.FontSize = 15;
			title.FontWeight = FontWeight.Normal;
			title.Opacity = 0.85;
			title.Margin = new Thickness(6, 0, 0, 0);
			return Ui.Columns("Auto,Auto,*", icon, bold, title);
		}
		case Head.SciFi:
		{
			var left = new Border { Height = 2, Width = 26, Background = Palette.Brush(Accent("#3b82f6")), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
			var right = new Border { Height = 2, Width = 26, Background = Palette.Brush(Accent("#3b82f6")), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
			title.FontFamily = Digital;
			title.FontSize = 14;
			title.LetterSpacing = 2;
			var row = Ui.Row(0, left, title, right);
			row.HorizontalAlignment = HorizontalAlignment.Center;
			return row;
		}
		case Head.Capsule:
		{
			double h = M.Subtitles ? 52 : 46;
			var icon = new Border { Width = h - 16, Height = h - 16, CornerRadius = new CornerRadius((h - 16) / 2), Background = Palette.Brush(tint), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 12, 0), Child = new Viewbox { Width = 16, Height = 16, Child = Ui.Glyph(Glyphs.Overview, Readings.TextOn(tint, P), 16) } };
			title.FontSize = 13.5;
			title.VerticalAlignment = VerticalAlignment.Center;
			return new Border { Height = h, CornerRadius = new CornerRadius(h / 2), Background = Palette.Brush(P.Card), Child = Ui.Row(0, icon, title) };
		}
		case Head.Apple:
			title.FontSize = 13.5;
			title.Foreground = Palette.Brush(tint);
			return title;
		case Head.Plain:
		{
			title.FontSize = 13;
			title.FontWeight = FontWeight.Bold;
			var glyph = Ui.Glyph(Glyphs.Overview, tint, 18);
			return Ui.Columns("*,Auto", title, glyph);
		}
		default:
			title.Margin = new Thickness(Ui.Modern ? 10 : 8, 0, 0, 0);
			title.VerticalAlignment = VerticalAlignment.Center;
			return Ui.Columns("Auto,*", Ui.Badge(Glyphs.Overview, P, tint), title);
		}
	}


	public void Own(WidgetSlot slot, int index)
	{
		this.slot = slot;
		slotIndex = index;
	}

	public void Update(Snapshot s, History h)
	{
		var snap = WidgetSensors.FilterSnapshot(s, S);
		string name = Naming.Title(slot, slotIndex, L.T("Overview"));
		title.Text = head is Head.Mono or Head.Status or Head.Caps or Head.SciFi ? name.ToUpper(L.Culture) : name;
		var rows = OverviewCard.Rows(snap, S, P);
		string next = string.Join("|", rows.Select(r => r.Key + ":" + r.Label));
		if (next != shape)
		{
			shape = next;
			Build(rows);
		}
		for (int i = 0; i < rows.Count; i++)
		{
			Fill(cells[i], rows[i], i, h);
		}
		WidgetPresentation.Apply(this, S);
	}

	private TextBlock Text(string text, double size, Color color, FontWeight weight = FontWeight.Normal, bool numbers = false)
	{
		if (LocalDesigns.Contains(style)) size = Math.Max(size, numbers ? 14 : 11.5);
		var t = Ui.Text(text, size, color, weight, numbers);
		t.TextWrapping = TextWrapping.NoWrap;
		if (font != null)
		{
			t.FontFamily = font;
		}
		return t;
	}

	private bool Ring => visual is Visual.DialRing or Visual.DoubleRing or Visual.KnobRing or Visual.SweepRing or Visual.TickRing;

	private string Caption(OverviewRow r) => head is Head.Mono or Head.Status or Head.Caps or Head.SciFi || style == WidgetStyle.LevelColumns ? r.Label.ToUpper(L.Culture) : r.Label;

	private static Control Fit(Control c, HorizontalAlignment align) =>
		SteadyHeight.Of(new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = align, Child = c });

	private void Build(List<OverviewRow> rows)
	{
		grid.Children.Clear();
		cells.Clear();
		// Two mini widgets per row in the sidebar. In the horizontal bar the cards are wide and low, so the
		// parts go side by side in two rows instead of one tall column of rings.
		int perRow = S.Horizontal ? Math.Max(2, (int)Math.Ceiling(rows.Count / 2.0)) : 2;
		for (int i = 0; i < rows.Count; i += perRow)
		{
			// Rings in the horizontal bar: the first ring of a row sets the row's height and the others fit into
			// it (a ring taking its height from a wide cell would make the bar row shrink).
			Panel row = Ring && S.Horizontal ? new LeadRow { Columns = perRow } : new Grid { ColumnDefinitions = ColumnDefinitions.Parse(string.Join(",", Enumerable.Repeat("*", perRow))) };
			for (int k = 0; k < perRow && i + k < rows.Count; k++)
			{
				Cell cell = Ring ? RingCell(rows[i + k], k == 0) : FlatCell(rows[i + k], k == 0);
				Grid.SetColumn(cell.Root!, k);
				row.Children.Add(cell.Root!);
				cells.Add(cell);
			}
			grid.Children.Add(row);
		}
		grid.IsVisible = rows.Count > 0;
	}

	// A ring as in the design's own widget, drawn at the design's size and scaled into the cell, with the
	// value in the middle. Network: download and upload one above the other, in the same size.
	private Cell RingCell(OverviewRow r, bool lead)
	{
		var caption = Text("", 10.5, P.Soft, FontWeight.Medium);
		caption.HorizontalAlignment = HorizontalAlignment.Center;
		caption.TextTrimming = TextTrimming.CharacterEllipsis;
		double mainSize = r.Pair ? 21 : 30;
		var main = Text("", mainSize, P.Ink, FontWeight.DemiBold, numbers: true);
		var sub = Text("", r.Pair ? mainSize : 15, r.Pair ? P.Ink : P.Soft, r.Pair ? FontWeight.DemiBold : FontWeight.Medium, numbers: true);
		main.HorizontalAlignment = sub.HorizontalAlignment = HorizontalAlignment.Center;
		Control shape = visual switch
		{
			Visual.DialRing => new DialArc(),
			Visual.DoubleRing => new RingPair(),
			Visual.KnobRing => new KnobRing(),
			Visual.SweepRing => new SweepRing(),
			_ => new TickRing()
		};
		shape.Width = shape.Height = RingBase;
		var center = Ui.Stack(0, Fit(main, HorizontalAlignment.Center), Fit(sub, HorizontalAlignment.Center));
		center.VerticalAlignment = VerticalAlignment.Center;
		center.HorizontalAlignment = HorizontalAlignment.Center;
		center.MaxWidth = RingBase * 0.56;
		center.Margin = new Thickness(0, visual is Visual.DialRing or Visual.KnobRing ? 8 : 0, 0, 0);
		var face = new Panel { Width = RingBase, Height = RingBase, Children = { shape, center } };
		// The first ring of a row has a height the panel can grow; the second one fills the same row.
		var box = new Viewbox { Stretch = Stretch.Uniform, Child = face, HorizontalAlignment = HorizontalAlignment.Center, Tag = "grow" };
		if (lead)
		{
			box.Height = 92 * Math.Clamp(S.ActiveWidget?.Graphics ?? 1, 0.5, 2);
		}
		var root = new DockPanel { LastChildFill = true };
		DockPanel.SetDock(caption, Dock.Top);
		caption.Margin = new Thickness(0, 0, 0, 4);
		root.Children.Add(caption);
		root.Children.Add(box);
		var cellRoot = new Border { Padding = new Thickness(4, 0, 4, 10), Child = root };
		return new Cell { Caption = caption, Main = main, Sub = sub, Shape = shape, Root = cellRoot };
	}

	private Cell FlatCell(OverviewRow r, bool lead)
	{
		bool centered = style is WidgetStyle.NeonTiles or WidgetStyle.PillRow or WidgetStyle.FillBars;
		var align = centered ? HorizontalAlignment.Center : HorizontalAlignment.Left;
		var caption = Text("", style is WidgetStyle.NeonTiles ? 8.5 : 10.5, P.Soft, style is WidgetStyle.NeonTiles ? FontWeight.Bold : FontWeight.Medium);
		caption.TextTrimming = TextTrimming.CharacterEllipsis;
		caption.HorizontalAlignment = align;
		double size = style switch
		{
			WidgetStyle.Split or WidgetStyle.TwinFocus or WidgetStyle.KeyFigures => 22,
			WidgetStyle.NeonTiles => 15,
			WidgetStyle.Clarity or WidgetStyle.ClarityColor or WidgetStyle.ClarityRows or WidgetStyle.ControlDeck or WidgetStyle.Table => 17,
			_ => 19
		};
		if (r.Pair)
		{
			size *= 0.72;
		}
		var main = Text("", size, P.Ink, FontWeight.DemiBold, numbers: true);
		var sub = Text("", r.Pair ? size : 10.5, r.Pair ? P.Ink : P.Soft, r.Pair ? FontWeight.DemiBold : FontWeight.Medium, numbers: true);
		var parts = new List<Control> { caption, Fit(main, align), Fit(sub, align) };
		Control? shape = null;
		AreaChart? chart = null;
		switch (visual)
		{
		case Visual.Spark when r.Spark != null:
			chart = new AreaChart { Margin = new Thickness(0, 4, 0, 0), MinHeight = 26 };
			if (lead)
			{
				chart.Height = 26;
			}
			parts.Add(chart);
			break;
		case Visual.Spark:
		case Visual.Bar:
			shape = style switch
			{
				WidgetStyle.Segments => new Meter { Height = 9, Palette = P, Segments = 10, RampBySegment = true },
				WidgetStyle.NeonHud => new GradientBar { Height = 4 },
				WidgetStyle.SignalRows or WidgetStyle.OpenPanel or WidgetStyle.TableGraph => new FillBack { Height = 2, Radius = 1, Track = Track },
				_ => new FillBack { Height = 5, Radius = 2.5, Track = Track }
			};
			shape.Margin = new Thickness(0, 6, 0, 0);
			parts.Add(shape);
			if (visual == Visual.Spark)
			{
				// Same height as a curve, so the cells line up.
				shape.Margin = new Thickness(0, 4 + 21, 0, 0);
			}
			break;
		}
		// Name and values at the top; a curve takes the rest of the cell, so it grows with the row.
		var dock = new DockPanel { LastChildFill = chart != null };
		foreach (Control part in parts)
		{
			if (part != chart)
			{
				DockPanel.SetDock(part, Dock.Top);
			}
			dock.Children.Add(part);
		}
		Control content = dock;
		Control root;
		switch (visual)
		{
		case Visual.Pill:
		{
			var back = new FillBack { Radius = style == WidgetStyle.FillBars ? 8 : 18, Track = Palette.Mix(P.Card, P.Ink, P.Dark ? 0.06 : 0.05) };
			shape = back;
			content.Margin = new Thickness(12, 8, 12, 9);
			root = new Panel { Margin = new Thickness(4, 0, 4, 8), Children = { back, content } };
			break;
		}
		case Visual.Level:
		{
			var level = new LevelBar { Width = 12, Height = 58, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
			shape = level;
			root = new Border { Padding = new Thickness(4, 0, 4, 10), Child = Ui.Columns("Auto,*", level, content) };
			break;
		}
		default:
			root = style switch
			{
				WidgetStyle.NeonTiles => new Border { Margin = new Thickness(4, 0, 4, 8), CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1), BorderBrush = Palette.Brush(Edge("#1e2636")), Background = Palette.Brush(P.Dark ? Color.Parse("#111723") : Palette.Mix(P.Card, P.Ink, 0.03)), Padding = new Thickness(6, 8), Child = content },
				WidgetStyle.ClarityTiles or WidgetStyle.ClarityRows => new Border { BorderBrush = Palette.Brush(Tint), BorderThickness = new Thickness(2, 0, 0, 0), Background = Palette.Brush(Palette.Alpha(Tint, 0.06 * P.Card.A / 255d)), CornerRadius = new CornerRadius(3), Padding = new Thickness(M.Gap * 0.6, M.Gap * 0.3), Margin = new Thickness(4, 0, 4, 8), Child = content },
				WidgetStyle.ClarityColor => new Border { BorderBrush = Palette.Brush(Tint), BorderThickness = new Thickness(2, 0, 0, 0), Padding = new Thickness(M.Gap * 0.5, 0, 4, 0), Margin = new Thickness(4, 0, 4, 10), Child = content },
				WidgetStyle.Table or WidgetStyle.TableGraph => new Border { BorderBrush = Palette.Brush(P.Track), BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(0, 0, 0, 6), Margin = new Thickness(4, 0, 4, 8), Child = content },
				WidgetStyle.BadgeList or WidgetStyle.BadgeListGraph => new Border { CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 6), Margin = new Thickness(4, 0, 4, 8), Child = content, Tag = "badge" },
				WidgetStyle.SmartPanel => new Border { Padding = new Thickness(4, 0, 8, 10), Child = content },
				WidgetStyle.HistoryGraph => new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(9, 7), Margin = new Thickness(4, 0, 4, 8), Background = Palette.Brush(Palette.Mix(P.Card, Tint, P.Dark ? 0.08 : 0.06)), Child = content },
				_ => new Border { Padding = new Thickness(4, 0, 4, 10), Child = content }
			};
			break;
		}
		return new Cell { Caption = caption, Main = main, Sub = sub, Shape = shape, Chart = chart, Root = root };
	}

	private void Fill(Cell cell, OverviewRow r, int index, History h)
	{
		cell.Caption.Text = Caption(r);
		cell.Main.Text = r.Main;
		cell.Sub.Text = r.Sub;
		cell.Sub.IsVisible = r.Sub.Length > 0;
		bool heat = r.Key is "overview.cpu" or "overview.gpu" or "overview.drive";
		Color ink = heat && style is WidgetStyle.Dial or WidgetStyle.Segments or WidgetStyle.Evolution or WidgetStyle.SciFi or WidgetStyle.ClarityColor or WidgetStyle.HistoryGraph ? r.Color : P.Ink;
		cell.Main.Foreground = Palette.Brush(ink);
		if (r.Pair)
		{
			cell.Sub.Foreground = Palette.Brush(P.Ink);
		}
		switch (cell.Shape)
		{
		case DialArc arc:
			arc.Set(r.Fraction, r.Color, P.Dark ? Color.Parse("#1a1b1d") : P.Track);
			break;
		case RingPair rings:
			rings.Set(r.Fraction, r.Color, r.Second, Opaque(P.Cool), Track);
			break;
		case KnobRing knob:
			knob.Set(r.Fraction, Accent("#f6b25a"), Accent("#7cc4f5"), P.Dark ? Color.Parse("#23262e") : P.Track);
			break;
		case SweepRing sweep:
			sweep.Set(r.Fraction, Accent("#8b2cf5"), Accent("#e64bd3"));
			break;
		case TickRing ticks:
			ticks.Set(r.Fraction, Accent("#ec4899"), Accent("#3b82f6"), P.Dark ? Color.Parse("#25272e") : P.Track);
			break;
		case Meter meter:
			meter.Set(r.Fraction, r.Color);
			break;
		case GradientBar g:
			g.Set(r.Fraction, Accent("#22d3ee"), Accent("#ec4899"), P.Dark ? Color.Parse("#1a2230") : P.Track);
			break;
		case LevelBar level:
			level.Set(r.Fraction, r.Color, Palette.Mix(P.Card, P.Ink, P.Dark ? 0.09 : 0.07));
			break;
		case FillBack back when visual == Visual.Pill:
			back.Animate(r.Fraction, Palette.Alpha(r.Color, P.Dark ? 0.34 : 0.28));
			break;
		case FillBack bar:
			bar.Animate(r.Fraction, r.Color);
			break;
		}
		if (cell.Chart is AreaChart chart && r.Spark != null)
		{
			double[] values = h.Read(r.Spark);
			chart.Set(values.Length > 0 ? values : new[] { 0.0 }, Opaque(r.Color));
		}
		if (style == WidgetStyle.NeonTiles)
		{
			Color[] captions = { Accent("#22d3ee"), Accent("#ec4899"), Accent("#f59e0b") };
			cell.Caption.Foreground = Palette.Brush(captions[index % 3]);
		}
		if (style is WidgetStyle.BadgeList or WidgetStyle.BadgeListGraph && cell.Root is Border badge)
		{
			badge.Background = Palette.Brush(Palette.Alpha(r.Color, P.Dark ? 0.16 : 0.12));
		}
		if (style == WidgetStyle.SmartPanel)
		{
			Color[] marks = { Accent("#f59e0b"), Accent("#f472b6"), Accent("#60a5fa") };
			cell.Caption.Foreground = Palette.Brush(marks[index % 3]);
		}
	}

	private Color Opaque(Color c) => c.A == 255 ? c : Palette.Mix(P.Card, Color.FromRgb(c.R, c.G, c.B), c.A / 255d);
}
