using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

/// <summary>
/// The widgets without a measured value (clock, world clocks, calendar, weather, now playing) and the top
/// apps, drawn in the frame, header, fonts and colours of every design. Time and date are the big number and
/// never get a bar; the weather shows its temperature big (inside the design's ring for the ring designs);
/// now playing is title and artist as text; the top apps are always the list of apps, each with its CPU bar
/// in the design's style. Rows are built once and keep their place from one update to the next.
/// </summary>
public sealed class InfoTile : Border, IWidget, INamed
{
	private enum Head { Badge, Dot, Chip, Mono, Caps, Purple, SciFi, Capsule, Plain, Status, Apple }

	private static readonly FontFamily Mono = new FontFamily("Cascadia Mono, Consolas, DejaVu Sans Mono, fonts:Inter#Inter, $Default");

	private static readonly FontFamily Digital = new FontFamily("Bahnschrift, DIN Alternate, fonts:Inter#Inter, $Default");

	public const int AppRows = 5;

	private readonly WidgetKind kind;

	private readonly int index;

	private readonly WidgetStyle style;

	private readonly Palette P;

	private readonly Metrics M;

	private readonly Settings S;

	private readonly Head head;

	private readonly FontFamily? font;

	private readonly bool ring;

	private readonly TextBlock title;

	private readonly Control glyphHost;

	private WidgetSlot? slot;

	private int slotIndex;

	// Big number: time, day, temperature, title.
	private TextBlock big = null!;

	private TextBlock bigUnit = null!;

	private TextBlock aside = null!;

	private TextBlock line = null!;

	private TextBlock line2 = null!;

	private Control? ringShape;

	private readonly List<(TextBlock Label, TextBlock Value)> facts = new();

	private readonly List<(Control Root, TextBlock Name, TextBlock Sub, TextBlock Value, Control? Bar, Border? Dot)> rows = new();

	private readonly List<TimeZoneInfo> zones = new();

	private TextBlock status = null!;

	private StackPanel healthList = null!;

	private string drivesShape = "";

	public InfoTile(WidgetKind kind, int index, WidgetStyle style, Palette p, Metrics m, Settings s)
	{
		this.kind = kind;
		this.index = index;
		this.style = style;
		P = p;
		M = m;
		S = s;
		(head, font, ring) = style switch
		{
            WidgetStyle.SplitFlap => (Head.Mono, Digital, false),
            WidgetStyle.MeasureTape or WidgetStyle.HardwareMap => (Head.Plain, null, false),
			WidgetStyle.Aperture => (Head.Chip, (FontFamily?)null, true),
			WidgetStyle.PixelDeck => (Head.Status, null, false),
			WidgetStyle.HorizonSync => (Head.Plain, null, false),
			WidgetStyle.Panorama or WidgetStyle.SplitLens => (Head.Plain, (FontFamily?)null, false),
			WidgetStyle.Terminal => (Head.Mono, null, false),
			WidgetStyle.Thermal => (Head.Caps, null, false),
			WidgetStyle.MosaicPro => (Head.Apple, null, false),
			WidgetStyle.RibbonFlow => (Head.Capsule, null, false),
			WidgetStyle.Meridian or WidgetStyle.Vector => (Head.Plain, (FontFamily?)null, false),
			WidgetStyle.Strata or WidgetStyle.Lucent => (Head.Apple, null, false),
			WidgetStyle.Orbit => (Head.Chip, null, true),
			WidgetStyle.Circuit => (Head.Mono, null, false),
			WidgetStyle.CleanDark or WidgetStyle.CompactPro or WidgetStyle.PerformanceView => (Head.Dot, (FontFamily?)null, false),
			WidgetStyle.Dial => (Head.Chip, (FontFamily?)null, true),
			WidgetStyle.RingPair => (Head.Badge, null, true),
			WidgetStyle.Thermostat => (Head.Caps, null, true),
			WidgetStyle.PurpleDash or WidgetStyle.PurpleList => (Head.Purple, null, true),
			WidgetStyle.SciFi => (Head.SciFi, Digital, true),
			WidgetStyle.NeonHud or WidgetStyle.NeonHudGraph => (Head.Mono, Mono, false),
			WidgetStyle.NeonTiles => (Head.Status, Mono, false),
			WidgetStyle.SmartPanel => (Head.Caps, null, false),
			WidgetStyle.Pill => (Head.Capsule, null, false),
			WidgetStyle.Evolution or WidgetStyle.HistoryGraph or WidgetStyle.LevelColumns => (Head.Dot, null, false),
			WidgetStyle.TwinFocus => (Head.Plain, null, false),
			WidgetStyle.Pure or WidgetStyle.PureBar or WidgetStyle.PureNumber or WidgetStyle.PureSegments => (Head.Apple, null, false),
			WidgetStyle.PureWidgets or WidgetStyle.PureWidgetsRight => (Head.Apple, null, true),
			_ => (Head.Badge, null, false)
		};
		Frame();
		title = Ui.Text("", 12.5, p.Ink, FontWeight.DemiBold);
        HardwareWidgetTitle.Register(this, title);
		title.TextTrimming = TextTrimming.CharacterEllipsis;
		glyphHost = Header();
		if (LocalDesigns.Contains(style)) title.FontSize = 14;
		var body = Body();
		var root = Ui.Stack(head is Head.Capsule ? 8 : 10, glyphHost, body);
		if (head == Head.SciFi)
		{
			// The chamfered frame is drawn by the child, so free height must go inside it (the card's own
			// padding would leave the frame short of the column's bottom).
			root.Tag = "elastic";
			Child = new ChamferFrame { Fill = p.DesignBack("#0d0e11"), Edge = p.Dark ? Color.Parse("#2c2f37") : p.Stroke, Accent = Accent("#3b82f6"), Padding = new Thickness(16, 12, 16, 16), Child = root };
		}
		else
		{
			Child = root;
		}
	}

	/// <summary>The kinds this tile draws.</summary>
	public static bool Handles(WidgetKind kind) => kind is WidgetKind.Clock or WidgetKind.WorldClocks or WidgetKind.Calendar or WidgetKind.Weather or WidgetKind.Media or WidgetKind.Processes or WidgetKind.DataUsage or WidgetKind.DriveHealth or WidgetKind.System;

	private Color Tint => Accents.For(kind, slot ?? S.ActiveWidget, P);

	private Color Accent(string hex) => Accents.Design(hex, P);

	private Color Dark(string hex) => P.DesignBack(hex);

	private Color Edge(string hex) => P.Dark ? Color.Parse(hex) : P.Stroke;

	private Color Track => P.Dark ? Palette.Mix(P.Card, P.Ink, 0.1) : P.Track;

	private bool Upper => head is Head.Mono or Head.Status or Head.Caps or Head.SciFi || style == WidgetStyle.LevelColumns;

	private string Glyph => kind switch
	{
		WidgetKind.Clock => "M8,1.5 A6.5,6.5 0 1 1 7.99,1.5 Z M8,4.5 V8 L10.5,9.5",
		WidgetKind.WorldClocks => "M8,1.5 A6.5,6.5 0 1 1 7.99,1.5 Z M1.5,8 H14.5 M8,1.5 C5.5,4 5.5,12 8,14.5 M8,1.5 C10.5,4 10.5,12 8,14.5",
		WidgetKind.Calendar => "M2,3.5 H14 V14 H2 Z M2,6.5 H14 M5,2 V5 M11,2 V5",
		WidgetKind.Weather => "M4.5,12.5 H11.5 A3,3 0 0 0 11.7,6.5 A4,4 0 0 0 4.1,7.6 A2.5,2.5 0 0 0 4.5,12.5 Z",
		WidgetKind.Media => "M6,12 V3 L13,1.8 V10.5 M6,12 A2,2 0 1 1 5.99,12 Z M13,10.5 A2,2 0 1 1 12.99,10.5 Z",
		WidgetKind.DataUsage => "M8,2 V10 M5,7 L8,10 L11,7 M3,13.5 H13",
		WidgetKind.DriveHealth => Glyphs.Health,
		WidgetKind.System => "M1.5,3 H14.5 V11 H1.5 Z M6,14 H10 M8,11 V14",
		_ => "M2,2.5 H6.5 V7 H2 Z M9.5,2.5 H14 V7 H9.5 Z M2,9 H6.5 V13.5 H2 Z M9.5,9 H14 V13.5 H9.5 Z"
	};

	private void Frame()
	{
        if (InstrumentDesigns.Contains(style)) { InstrumentDesigns.Frame(this, style, P, M); return; }
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
		case WidgetStyle.NeonHudGraph:
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
		string glyph = Glyph;
		switch (head)
		{
		case Head.Dot:
		{
			var dot = new Avalonia.Controls.Shapes.Ellipse { Width = 8, Height = 8, Fill = Palette.Brush(tint), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
			return Ui.Columns("Auto,*", dot, title);
		}
		case Head.Chip:
		{
			// The dial's chip carries the widget's symbol (no English word in other languages).
			var chip = Ui.Glyph(glyph, Accent("#29b6d8"), 12);
			var box = new Border { CornerRadius = new CornerRadius(5), Padding = new Thickness(6, 3), Child = chip, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0), Background = Palette.Brush(P.Dark ? Color.Parse("#0e2f3a") : Palette.Alpha(Accent("#29b6d8"), 0.15)) };
			title.FontSize = 13;
			return Ui.Columns("Auto,*", box, title);
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
			return Ui.Columns("Auto,*", dot, title);
		}
		case Head.Caps:
			title.FontSize = 9.5;
			title.FontWeight = FontWeight.Medium;
			title.LetterSpacing = 1.2;
			title.Foreground = Palette.Brush(P.Soft);
			return title;
		case Head.Purple:
		{
			var icon = new Avalonia.Controls.Shapes.Path { Width = 18, Height = 18, Stretch = Stretch.Uniform, StrokeThickness = 1.4, Stroke = Palette.Brush(P.Ink), Data = Geometry.Parse(glyph), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
			title.FontSize = 15;
			title.FontWeight = FontWeight.Normal;
			title.Opacity = 0.9;
			return Ui.Columns("Auto,*", icon, title);
		}
		case Head.SciFi:
		{
			var left = new Border { Height = 2, Width = 26, Background = Palette.Brush(Accent("#3b82f6")), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
			var right = new Border { Height = 2, Width = 26, Background = Palette.Brush(Accent("#3b82f6")), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
			title.FontFamily = Digital;
			title.FontSize = 14;
			title.LetterSpacing = 2;
			title.MaxWidth = 230;
			var row = Ui.Row(0, left, title, right);
			row.HorizontalAlignment = HorizontalAlignment.Center;
			return row;
		}
		case Head.Capsule:
		{
			double h = M.Subtitles ? 52 : 46;
			var icon = new Border { Width = h - 16, Height = h - 16, CornerRadius = new CornerRadius((h - 16) / 2), Background = Palette.Brush(tint), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 12, 0), Child = new Viewbox { Width = 16, Height = 16, Child = Ui.Glyph(glyph, Readings.TextOn(tint, P), 16) } };
			title.FontSize = 13.5;
			title.VerticalAlignment = VerticalAlignment.Center;
			return new Border { Height = h, CornerRadius = new CornerRadius(h / 2), Background = Palette.Brush(P.Card), Child = Ui.Columns("Auto,*", icon, title) };
		}
		case Head.Apple:
			title.FontSize = 13.5;
			title.Foreground = Palette.Brush(tint);
			return title;
		case Head.Plain:
		{
			title.FontSize = 13;
			title.FontWeight = FontWeight.Bold;
			return Ui.Columns("*,Auto", title, Ui.Glyph(glyph, tint, 18));
		}
		default:
			title.Margin = new Thickness(Ui.Modern ? 10 : 8, 0, 0, 0);
			title.VerticalAlignment = VerticalAlignment.Center;
			return Ui.Columns("Auto,*", Ui.Badge(glyph, P, tint), title);
		}
	}


	private TextBlock Text(string text, double size, Color color, FontWeight weight = FontWeight.Normal, bool numbers = false)
	{
		if (LocalDesigns.Contains(style)) size = Math.Max(size, numbers ? 14 : 11.5);
		var t = Ui.Text(text, size, color, weight, numbers);
		t.TextWrapping = TextWrapping.NoWrap;
		t.TextTrimming = TextTrimming.CharacterEllipsis;
		if (font != null)
		{
			t.FontFamily = font;
		}
		return t;
	}

	private string Caption(string label) => Upper ? label.ToUpper(L.Culture) : label;

	private Color HeroInk => style switch
	{
		WidgetStyle.NeonHud or WidgetStyle.NeonHudGraph => Accent("#22d3ee"),
		WidgetStyle.Dial => Accent("#29b6d8"),
		WidgetStyle.Evolution or WidgetStyle.HistoryGraph or WidgetStyle.LevelColumns or WidgetStyle.ClarityColor or WidgetStyle.Duplex or WidgetStyle.OpenPanel or WidgetStyle.SignalRows => Tint,
		_ => P.Ink
	};

	private double HeroSize => style switch
	{
		WidgetStyle.Table or WidgetStyle.TableGraph or WidgetStyle.Clarity or WidgetStyle.ClarityColor or WidgetStyle.ClarityRows or WidgetStyle.ClarityTiles or WidgetStyle.ControlDeck or WidgetStyle.BadgeList or WidgetStyle.BadgeListGraph => M.Subtitles ? 34 : 28,
		_ => M.Subtitles ? 40 : 32
	};

	// ---------------------------------------------------------------- layout

	private Control Body()
	{
		switch (kind)
		{
		case WidgetKind.WorldClocks:
		{
			var list = new StackPanel { Spacing = 6, Tag = "elastic" };
			foreach (string id in S.WorldClocks)
			{
				if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out TimeZoneInfo? zone))
				{
					zones.Add(zone);
					list.Children.Add(Row(WorldClocksCard.City(id), clock: true));
				}
			}
			if (zones.Count == 0)
			{
				list.Children.Add(Text(L.T("Choose cities in the settings."), 12, P.Soft));
			}
			return list;
		}
		case WidgetKind.Processes:
		{
			var list = new StackPanel { Spacing = style is WidgetStyle.Pill or WidgetStyle.PillRow or WidgetStyle.PillRowGraph or WidgetStyle.FillBars or WidgetStyle.FillBarsGraph ? 5 : 6, Tag = "elastic" };
			for (int i = 0; i < AppRows; i++)
			{
				list.Children.Add(Row("", clock: false));
			}
			return list;
		}
		case WidgetKind.DriveHealth:
		{
			// Verdict for all drives on top, then one line per drive: name and life left, the life-left bar
			// in the design's style, and the verdict, hours, starts and temperature under it.
			status = Text("", 12, P.Calm, FontWeight.DemiBold);
			healthList = new StackPanel { Spacing = 8 };
			return Ui.Stack(6, status, healthList);
		}
		case WidgetKind.Media:
		{
			big = Text("", M.Subtitles ? 19 : 16.5, P.Ink, FontWeight.DemiBold);
			WidgetPresentation.Sensor(big, "media.title");
			line = Text("", 12.5, style is WidgetStyle.Evolution or WidgetStyle.HistoryGraph ? Tint : P.Soft, FontWeight.Medium);
			WidgetPresentation.Sensor(line, "media.artist");
			var stack = Ui.Stack(3, big, line);
			var info = Facts(2);
			info.Margin = new Thickness(0, 3, 0, 0);
			stack.Children.Add(info);
			WidgetPresentation.Sensor(Cell(0), "media.source");
			WidgetPresentation.Sensor(Cell(1), "media.progress");
			return stack;
		}
		default:
			return Hero();
		}
	}

	// Big number (time, day or temperature) with its unit, a short note on the right and a line under it.
	private Control Hero()
	{
		double size = HeroSize;
		big = Text("", size, HeroInk, FontWeight.DemiBold, numbers: true);
		big.LetterSpacing = -1.2;
		big.VerticalAlignment = VerticalAlignment.Bottom;
		big.TextTrimming = TextTrimming.None;
		bigUnit = Text("", size * 0.38, P.Soft, FontWeight.Medium, numbers: true);
		bigUnit.VerticalAlignment = VerticalAlignment.Bottom;
		bigUnit.Margin = new Thickness(4, 0, 0, size * 0.12);
		bigUnit.TextTrimming = TextTrimming.None;
		aside = Text("", M.Subtitles ? 20 : 17, style is WidgetStyle.NeonHud or WidgetStyle.NeonHudGraph ? Accent("#ec4899") : Tint, FontWeight.DemiBold, numbers: true);
		aside.VerticalAlignment = VerticalAlignment.Bottom;
		aside.HorizontalAlignment = HorizontalAlignment.Right;
		aside.Margin = new Thickness(8, 0, 0, size * 0.1);
		line = Text("", 12, P.Soft, FontWeight.Medium);
		line2 = Text("", 11, P.Soft);
		var number = Ui.Row(0, big, bigUnit);
		number.VerticalAlignment = VerticalAlignment.Bottom;
		var parts = new List<Control>();
		switch (kind)
		{
		case WidgetKind.Clock:
			WidgetPresentation.Sensor(big, "clock.time");
			WidgetPresentation.Sensor(bigUnit, "clock.seconds");
			WidgetPresentation.Sensor(aside, "clock.week");
			WidgetPresentation.Sensor(line, "clock.date");
			WidgetPresentation.Sensor(line2, "clock.day");
			break;
		case WidgetKind.Calendar:
			WidgetPresentation.Sensor(big, "calendar.date");
			WidgetPresentation.Sensor(bigUnit, "calendar.date");
			WidgetPresentation.Sensor(aside, "calendar.week");
			break;
		case WidgetKind.System:
			// Uptime big, the operating system under it, then started, processes and threads.
			WidgetPresentation.Sensor(big, "system.uptime");
			WidgetPresentation.Sensor(bigUnit, "system.uptime");
			WidgetPresentation.Sensor(line, "system.os");
			aside.IsVisible = false;
			line2.IsVisible = false;
			break;
		case WidgetKind.DataUsage:
			WidgetPresentation.Sensor(big, "data.down");
			WidgetPresentation.Sensor(bigUnit, "data.down");
			WidgetPresentation.Sensor(aside, "data.up");
			line.IsVisible = false;
			line2.IsVisible = false;
			break;
		case WidgetKind.Weather:
			WidgetPresentation.Sensor(big, "weather.temp");
			WidgetPresentation.Sensor(bigUnit, "weather.temp");
			WidgetPresentation.Sensor(line, "weather.sky");
			aside.IsVisible = false;
			line2.IsVisible = false;
			break;
		}
		if (kind == WidgetKind.Weather && ring)
		{
			// The temperature inside the design's ring, the condition next to it.
			ringShape = style switch
			{
				WidgetStyle.Dial => new DialArc(),
				WidgetStyle.RingPair => new RingPair(),
				WidgetStyle.Thermostat => new KnobRing(),
				WidgetStyle.PurpleDash or WidgetStyle.PurpleList => new SweepRing(),
				_ => new TickRing()
			};
			const double face = 140;
			ringShape.Width = ringShape.Height = face;
			big.FontSize = 34;
			bigUnit.FontSize = 14;
			bigUnit.Margin = new Thickness(3, 0, 0, 5);
			var center = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Child = number, MaxWidth = face * 0.6, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
			var dial = new Panel { Width = face, Height = face, Children = { ringShape, center } };
			var box = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.Both, Width = 96, Height = 96, Child = dial };
			WidgetPresentation.Sensor(box, "weather.temp");
			line.TextWrapping = TextWrapping.Wrap;
			line.MaxLines = 2;
			line.FontSize = 13;
			line.Foreground = Palette.Brush(P.Ink);
			line.VerticalAlignment = VerticalAlignment.Center;
			line.Margin = new Thickness(12, 0, 0, 0);
			parts.Add(Ui.Columns("Auto,*", box, line));
		}
		else
		{
			var top = Ui.Columns("Auto,*", number, aside);
			parts.Add(top);
			parts.Add(line);
			if (kind == WidgetKind.Clock)
			{
				parts.Add(line2);
			}
			else if (kind == WidgetKind.Calendar)
			{
				parts.Add(line2);
				WidgetPresentation.Sensor(line2, "calendar.date");
			}
		}
		if (kind == WidgetKind.System)
		{
			var stats = Facts(3);
			stats.Margin = new Thickness(0, 4, 0, 0);
			parts.Add(stats);
		}
		if (kind == WidgetKind.DataUsage)
		{
			// This week and this month, download and upload: two small values per line.
			var week = Facts(2);
			week.Margin = new Thickness(0, 4, 0, 0);
			var month = Facts(2);
			month.Margin = new Thickness(0, 4, 0, 0);
			parts.Add(week);
			parts.Add(month);
		}
		if (kind == WidgetKind.Weather)
		{
			var stats = Facts(3);
			stats.Margin = new Thickness(0, 4, 0, 0);
			WidgetPresentation.Sensor(Cell(0), "weather.feels");
			WidgetPresentation.Sensor(Cell(1), "weather.wind");
			WidgetPresentation.Sensor(Cell(2), "weather.humidity");
			parts.Add(stats);
			var days = Facts(3);
			days.Margin = new Thickness(0, 4, 0, 0);
			WidgetPresentation.Sensor(days, "weather.forecast");
			parts.Add(days);
		}
		return Ui.Stack(2, parts.ToArray());
	}

	private Control Cell(int i) => (Control)facts[i].Value.Parent!.Parent!;

	// Two or three small values side by side, each with its name above it, in the design's own look.
	private Grid Facts(int count)
	{
		var grid = new Grid { ColumnDefinitions = ColumnDefinitions.Parse(string.Join(",", Enumerable.Repeat("*", count))), ColumnSpacing = 6 };
		bool caps = style is WidgetStyle.NeonTiles;
		for (int i = 0; i < count; i++)
		{
			var label = Text("", caps ? 8.5 : 10, style is WidgetStyle.NeonTiles ? new[] { Accent("#22d3ee"), Accent("#ec4899"), Accent("#f59e0b") }[i % 3] : P.Soft, caps ? FontWeight.Bold : FontWeight.Normal);
			var value = Text("", 12.5, P.Ink, FontWeight.DemiBold, numbers: true);
			bool centered = style is WidgetStyle.NeonTiles or WidgetStyle.PillRow or WidgetStyle.PillRowGraph or WidgetStyle.FillBars or WidgetStyle.FillBarsGraph or WidgetStyle.Pill;
			if (centered)
			{
				label.HorizontalAlignment = value.HorizontalAlignment = HorizontalAlignment.Center;
			}
			var stack = Ui.Stack(1, label, value);
			Control cell = style switch
			{
				WidgetStyle.NeonTiles => new Border { CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), BorderBrush = Palette.Brush(Edge("#1e2636")), Background = Palette.Brush(P.Dark ? Color.Parse("#111723") : Palette.Mix(P.Card, P.Ink, 0.03)), Padding = new Thickness(4, 5), Child = stack },
				WidgetStyle.Pill or WidgetStyle.PillRow or WidgetStyle.PillRowGraph => new Border { CornerRadius = new CornerRadius(14), Background = Palette.Brush(Palette.Mix(P.Card, P.Ink, P.Dark ? 0.06 : 0.05)), Padding = new Thickness(4, 4), Child = stack },
				WidgetStyle.FillBars or WidgetStyle.FillBarsGraph => new Border { CornerRadius = new CornerRadius(7), Background = Palette.Brush(Palette.Mix(P.Card, P.Ink, P.Dark ? 0.06 : 0.05)), Padding = new Thickness(4, 4), Child = stack },
				WidgetStyle.BadgeList or WidgetStyle.BadgeListGraph => new Border { CornerRadius = new CornerRadius(8), Background = Palette.Brush(Palette.Alpha(Tint, P.Dark ? 0.14 : 0.1)), Padding = new Thickness(7, 4), Child = stack },
				WidgetStyle.ClarityRows or WidgetStyle.ClarityTiles or WidgetStyle.ClarityColor => new Border { BorderBrush = Palette.Brush(Tint), BorderThickness = new Thickness(2, 0, 0, 0), Padding = new Thickness(6, 0, 0, 0), Child = stack },
				WidgetStyle.Table or WidgetStyle.TableGraph => new Border { BorderBrush = Palette.Brush(P.Track), BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 4, 0, 0), Child = stack },
				_ => new Border { Child = stack }
			};
			Grid.SetColumn(cell, i);
			grid.Children.Add(cell);
			facts.Add((label, value));
		}
		return grid;
	}

	// One line of a list: name (with a note under it for the world clocks), value, and for the top apps the
	// CPU bar in the design's style.
	private Control Row(string name, bool clock, bool health = false)
	{
		var nameText = Text(name, clock ? 13 : 12, P.Ink, FontWeight.Medium);
		var sub = Text("", 10.5, P.Soft, FontWeight.Normal, numbers: clock);
		var value = Text("", clock ? (M.Subtitles ? 21 : 19) : 12, clock ? HeroInk : P.Ink, FontWeight.DemiBold, numbers: true);
		value.TextTrimming = TextTrimming.None;
		value.HorizontalAlignment = HorizontalAlignment.Right;
		value.VerticalAlignment = VerticalAlignment.Center;
		Border? dot = null;
		Control? bar = null;
		Control content;
		bool pill = style is WidgetStyle.Pill or WidgetStyle.PillRow or WidgetStyle.PillRowGraph or WidgetStyle.FillBars or WidgetStyle.FillBarsGraph;
		if (clock)
		{
			WidgetPresentation.Sensor(value, "world.time");
			WidgetPresentation.Sensor(sub, "world.offset");
			dot = new Border { Width = 7, Height = 7, CornerRadius = new CornerRadius(4), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
			content = Ui.Columns("Auto,*,Auto", dot, Ui.Stack(0, nameText, sub), value);
		}
		else
		{
			sub.IsVisible = health;
			WidgetPresentation.Sensor(value, health ? "health.life" : "proc.cpu");
			if (!pill)
			{
				bar = style switch
				{
					WidgetStyle.Segments or WidgetStyle.SegmentGraph => new Meter { Height = 6, Palette = P, Segments = 16, RampBySegment = true },
					WidgetStyle.NeonHud or WidgetStyle.NeonHudGraph or WidgetStyle.PurpleDash or WidgetStyle.PurpleList or WidgetStyle.SciFi or WidgetStyle.Thermostat => new GradientBar { Height = 4 },
					WidgetStyle.SignalRows or WidgetStyle.OpenPanel or WidgetStyle.Table or WidgetStyle.TableGraph => new FillBack { Height = 2, Radius = 1, Track = Track },
					WidgetStyle.LevelColumns => new FillBack { Height = 6, Radius = 1.5, Track = Track },
					_ => new FillBack { Height = 4, Radius = 2, Track = Track }
				};
				bar.Margin = new Thickness(0, 3, 0, 0);
				WidgetPresentation.Sensor(bar, health ? "health.life" : "proc.cpu");
				content = health ? Ui.Stack(0, Ui.Columns("*,Auto", nameText, value), bar, sub) : Ui.Stack(0, Ui.Columns("*,Auto", nameText, value), bar);
			}
			else
			{
				content = health ? Ui.Stack(1, Ui.Columns("*,Auto", nameText, value), sub) : Ui.Columns("*,Auto", nameText, value);
			}
			sub.Margin = new Thickness(0, 3, 0, 0);
		}
		Control root;
		if (pill && clock)
		{
			// A capsule surface for a time (no fill: a time is not a share of anything).
			content.Margin = new Thickness(12, 6, 12, 6);
			root = new Border { CornerRadius = new CornerRadius(style is WidgetStyle.FillBars or WidgetStyle.FillBarsGraph ? 7 : 14), Background = Palette.Brush(Palette.Mix(P.Card, P.Ink, P.Dark ? 0.06 : 0.05)), Child = content };
		}
		else if (pill)
		{
			var back = new FillBack { Radius = style is WidgetStyle.FillBars or WidgetStyle.FillBarsGraph ? 7 : 14, Track = Palette.Mix(P.Card, P.Ink, P.Dark ? 0.06 : 0.05) };
			if (!clock)
			{
				bar = back;
			}
			content.Margin = new Thickness(12, 6, 12, 6);
			root = new Panel { Children = { back, content } };
		}
		else
		{
			root = style switch
			{
				WidgetStyle.NeonTiles => new Border { CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), BorderBrush = Palette.Brush(Edge("#1e2636")), Background = Palette.Brush(P.Dark ? Color.Parse("#111723") : Palette.Mix(P.Card, P.Ink, 0.03)), Padding = new Thickness(8, 5), Child = content },
				WidgetStyle.ClarityTiles or WidgetStyle.ClarityRows => new Border { BorderBrush = Palette.Brush(Tint), BorderThickness = new Thickness(2, 0, 0, 0), Background = Palette.Brush(Palette.Alpha(Tint, 0.06 * P.Card.A / 255d)), CornerRadius = new CornerRadius(3), Padding = new Thickness(8, 4), Child = content },
				WidgetStyle.ClarityColor => new Border { BorderBrush = Palette.Brush(Tint), BorderThickness = new Thickness(2, 0, 0, 0), Padding = new Thickness(8, 0, 0, 0), Child = content },
				// The apps' bars already underline each line; the clocks get the table's divider.
				WidgetStyle.Table or WidgetStyle.TableGraph when clock => new Border { BorderBrush = Palette.Brush(P.Track), BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(0, 0, 0, 5), Child = content },
				WidgetStyle.BadgeList or WidgetStyle.BadgeListGraph => new Border { CornerRadius = new CornerRadius(9), Padding = new Thickness(10, 5), Background = Palette.Brush(Palette.Alpha(Tint, P.Dark ? 0.14 : 0.1)), Child = content },
				WidgetStyle.HistoryGraph => new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(9, 5), Background = Palette.Brush(Palette.Mix(P.Card, Tint, P.Dark ? 0.08 : 0.06)), Child = content },
				_ => new Border { Child = content }
			};
		}
		rows.Add((root, nameText, sub, value, bar, dot));
		return root;
	}

	// ---------------------------------------------------------------- update

	public void Own(WidgetSlot slot, int index)
	{
		this.slot = slot;
		slotIndex = index;
	}

	public void Update(Snapshot s, History h)
	{
		Summary sum = Summaries.For(kind, index, s, h, S, P);
		string builtIn = kind switch
		{
			WidgetKind.Clock => L.T("Clock and date"),
			WidgetKind.WorldClocks => L.T("World clocks"),
			WidgetKind.Calendar => L.T("Calendar"),
			WidgetKind.Weather => S.Shows("weather.place") && s.Weather != null ? s.Weather.Place : L.T("Weather"),
			WidgetKind.Media => L.T("Now playing"),
			WidgetKind.DataUsage => L.T("Data today"),
			WidgetKind.DriveHealth => L.T("Drive health"),
			WidgetKind.System => L.T("Uptime"),
			_ => L.T("Top apps")
		};
		string name = Naming.Title(slot, slotIndex, builtIn);
		title.Text = Upper ? name.ToUpper(L.Culture) : name;
		switch (kind)
		{
		case WidgetKind.Clock:
			Clock(s.Time);
			break;
		case WidgetKind.Calendar:
			Calendar(s.Time);
			break;
		case WidgetKind.Weather:
			Weather(s, sum);
			break;
		case WidgetKind.WorldClocks:
			WorldClocks(s.Time);
			break;
		case WidgetKind.Media:
			Media(s.Media);
			break;
		case WidgetKind.Processes:
			Apps(s.Processes);
			break;
		case WidgetKind.DriveHealth:
			Health(s.Disks);
			break;
		case WidgetKind.System:
		{
			big.Text = sum.Value.Length > 0 ? sum.Value : "—";
			bigUnit.Text = sum.Unit;
			line.Text = sum.Caption;
			var items = Summaries.Extras(kind, index, s, h, S);
			for (int i = 0; i < facts.Count; i++)
			{
				bool has = i < items.Count;
				facts[i].Label.Text = has ? Caption(items[i].Label) : "\u2060";
				facts[i].Value.Text = has ? items[i].Value : "\u2060";
				Cell(i).Opacity = has ? 1 : 0;
			}
			((Control)Cell(0).Parent!).IsVisible = items.Count > 0;
			break;
		}
		case WidgetKind.DataUsage:
			big.Text = sum.Value.Length > 0 ? sum.Value : "—";
			bigUnit.Text = sum.Unit;
			aside.Text = sum.Caption;
			aside.Foreground = Palette.Brush(style is WidgetStyle.NeonHud or WidgetStyle.NeonHudGraph ? Accent("#ec4899") : P.Calm);
			var extras = Summaries.Extras(kind, index, s, h, S);
			for (int i = 0; i < facts.Count; i++)
			{
				bool has = i < extras.Count;
				facts[i].Label.Text = has ? Caption(extras[i].Label) : "\u2060";
				facts[i].Value.Text = has ? extras[i].Value : "\u2060";
				Cell(i).Opacity = has ? 1 : 0;
			}
			// A line whose values are switched off in the settings takes no room.
			for (int i = 0; i < facts.Count; i += 2)
			{
				((Control)Cell(i).Parent!).IsVisible = i < extras.Count;
			}
			break;
		}
		WidgetPresentation.Apply(this, S);

        SensorDetails.Apply(this, kind, index, s, h, S, P);
        WidgetPresentation.Apply(this, S);
	}

	private static string Capital(string text) => text.Length == 0 ? text : char.ToUpper(text[0], L.Culture) + text[1..];

	private void Clock(DateTime time)
	{
		big.Text = time.ToString("HH:mm", L.Culture);
		bigUnit.Text = time.ToString("ss", L.Culture);
		aside.Text = L.T("Week") + " " + ISOWeek.GetWeekOfYear(time);
		string pattern = L.Culture.DateTimeFormat.LongDatePattern;
		line.Text = Capital(time.ToString(pattern.Contains("dddd") ? pattern : "dddd, " + pattern, L.Culture));
		double day = time.TimeOfDay.TotalHours / 24.0;
		TimeSpan left = TimeSpan.FromHours(24.0) - time.TimeOfDay;
		line2.Text = L.T("Day") + " " + Format.Number(day * 100.0) + " %  ·  " + L.F("{0} h {1:00} m left", (int)left.TotalHours, left.Minutes);
	}

	private void Calendar(DateTime time)
	{
		big.Text = time.Day.ToString(L.Culture);
		bigUnit.Text = time.ToString("MMMM", L.Culture);
		aside.Text = L.T("Week") + " " + ISOWeek.GetWeekOfYear(time);
		line.Text = Capital(time.ToString("dddd", L.Culture));
		line2.Text = L.T("Day") + " " + time.DayOfYear + " / " + (DateTime.IsLeapYear(time.Year) ? 366 : 365) + "  ·  " + time.ToString("yyyy", L.Culture);
	}

	private string Degrees(double? celsius)
	{
		if (!celsius.HasValue)
		{
			return "—";
		}
		double v = S.Fahrenheit ? celsius.Value * 9.0 / 5.0 + 32.0 : celsius.Value;
		return Format.Number(v) + "°";
	}

	private void Weather(Snapshot s, Summary sum)
	{
		WeatherInfo? w = s.Weather;
		big.Text = sum.Value.Length > 0 ? sum.Value : "—";
		bigUnit.Text = sum.Unit;
		line.Text = sum.Caption;
		facts[0].Label.Text = Caption(L.T("Feels like"));
		facts[0].Value.Text = w?.FeelsLikeC is double f ? Format.Temperature(f, S.Fahrenheit) + " " + Format.TemperatureUnit(S.Fahrenheit) : "—";
		facts[1].Label.Text = Caption(L.T("Wind"));
		facts[1].Value.Text = w?.WindKmh is double k ? Format.Number(k) + " km/h" : "—";
		facts[2].Label.Text = Caption(L.T("Humidity"));
		facts[2].Value.Text = Format.Percent(w?.Humidity);
		var days = w == null ? new List<WeatherDay>() : w.Days.Where(d => d.Date.Date > w.Updated.Date).Take(3).ToList();
		for (int i = 0; i < 3; i++)
		{
			var (label, value) = facts[3 + i];
			label.Text = i < days.Count ? Caption(days[i].Date.ToString("ddd", L.Culture)) : "⁠";
			value.Text = i < days.Count ? Degrees(days[i].MinC) + " / " + Degrees(days[i].MaxC) : "⁠";
            SensorDetails.Value(value, i < days.Count ? "weather:" + days[i].Date.ToString("yyyy-MM-dd") : "");
		}
		double fraction = Math.Clamp(sum.Fraction, 0, 1);
		Color color = sum.Color;
		switch (ringShape)
		{
		case DialArc arc:
			arc.Set(fraction, color, P.Dark ? Color.Parse("#1a1b1d") : P.Track);
			break;
		case RingPair pair:
			pair.Set(fraction, color, null, P.Cool, Track);
			break;
		case KnobRing knob:
			knob.Set(fraction, Accent("#f6b25a"), Accent("#7cc4f5"), P.Dark ? Color.Parse("#23262e") : P.Track);
			break;
		case SweepRing sweep:
			sweep.Set(fraction, Accent("#8b2cf5"), Accent("#e64bd3"));
			break;
		case TickRing ticks:
			ticks.Set(fraction, Accent("#ec4899"), Accent("#3b82f6"), P.Dark ? Color.Parse("#25272e") : P.Track);
			break;
		}
	}

	private void WorldClocks(DateTime now)
	{
		TimeSpan local = TimeZoneInfo.Local.GetUtcOffset(now);
		DateTime utc = DateTime.SpecifyKind(now - local, DateTimeKind.Utc);
		for (int i = 0; i < zones.Count; i++)
		{
			var row = rows[i];
			DateTime there = TimeZoneInfo.ConvertTimeFromUtc(utc, zones[i]);
			row.Value.Text = there.ToString("HH:mm", L.Culture);
			double hours = (zones[i].GetUtcOffset(utc) - local).TotalHours;
			string offset = hours == 0 ? "±0 h" : (hours > 0 ? "+" : "−") + Math.Abs(hours).ToString("0.#", CultureInfo.InvariantCulture) + " h";
			string day = there.Date > now.Date ? L.T("tomorrow") : there.Date < now.Date ? L.T("yesterday") : L.T("today");
			row.Sub.Text = day + " · " + offset;
			if (row.Dot != null)
			{
				row.Dot.Background = Palette.Brush(there.Hour is >= 7 and < 19 ? P.Ramp[2] : P.Cool);
			}
		}
	}

	private void Media(MediaInfo? media)
	{
		facts[0].Label.Text = Caption(L.T("Application"));
		facts[1].Label.Text = Caption(L.T("Progress"));
		if (media == null)
		{
			big.Text = L.T("Nothing is playing.");
			big.Foreground = Palette.Brush(P.Soft);
			line.Text = "⁠";
			facts[0].Value.Text = "—";
			facts[1].Value.Text = "—";
			return;
		}
		big.Foreground = Palette.Brush(P.Ink);
		big.Text = media.Title.Length > 0 ? media.Title : "—";
		line.Text = media.Artist.Length > 0 ? media.Artist : "⁠";
		facts[0].Value.Text = media.App.Length > 0 ? media.App : "—";
		static string Clock(double seconds) => TimeSpan.FromSeconds(seconds).ToString(seconds >= 3600 ? "h\\:mm\\:ss" : "m\\:ss");
		facts[1].Value.Text = media.PositionSeconds is double at
			? Clock(at) + (media.DurationSeconds is double all && all > 0 ? " / " + Clock(all) : "")
			: "—";
	}

	private (string Verdict, Color Color) Verdict(DiskInfo d)
	{
		if (d.Life is < 5 || d.Temperature is >= 80)
		{
			return (L.T("Critical"), Palette.Alpha(P.Ramp[^1], 1));
		}
		if (d.Life is < 20 || d.Temperature is >= 70)
		{
			return (L.T("Check"), Palette.Alpha(P.Ramp[2], 1));
		}
		return (L.T("Good"), P.Calm);
	}

	private void Health(IReadOnlyList<DiskInfo> disks)
	{
		string next = string.Join("|", disks.Select(d => d.Id));
		if (next != drivesShape)
		{
			drivesShape = next;
			healthList.Children.Clear();
			rows.Clear();
			foreach (DiskInfo _ in disks)
			{
				healthList.Children.Add(Row("", clock: false, health: true));
			}
			if (disks.Count == 0)
			{
				healthList.Children.Add(Text(L.T("No drives found."), 11.5, P.Soft));
			}
		}
		int warnings = 0;
		for (int i = 0; i < disks.Count && i < rows.Count; i++)
		{
			DiskInfo d = disks[i];
			var row = rows[i];
			var (verdict, color) = Verdict(d);
			if (color != P.Calm)
			{
				warnings++;
			}
			row.Name.Text = (d.Letters.Length > 0 ? d.Letters + "  " : "") + d.Name;
			row.Value.Text = d.Life is double life ? Format.Number(life, "0") + " %" : "—";
            SensorDetails.Value(row.Value, "drive:" + d.Id + ":life");
			row.Value.Foreground = Palette.Brush(color == P.Calm ? P.Ink : color);
			var facts = new List<string> { verdict };
			if (S.Shows("health.hours") && d.PowerOnHours is double hours) facts.Add(Format.Number(hours, "0") + " h");
			if (S.Shows("health.count") && d.PowerOnCount is double count) facts.Add(Format.Number(count, "0") + "× " + L.T("started"));
			if (S.Shows("health.temp") && d.Temperature is double t) facts.Add(Format.Temperature(t, S.Fahrenheit) + " " + Format.TemperatureUnit(S.Fahrenheit));
			row.Sub.Text = string.Join("  ·  ", facts);
            var healthKeys = new List<string>();
            if (S.Shows("health.hours") && d.PowerOnHours.HasValue) healthKeys.Add("drive:" + d.Id + ":hours");
            if (S.Shows("health.count") && d.PowerOnCount.HasValue) healthKeys.Add("drive:" + d.Id + ":powercount");
            if (S.Shows("health.temp") && d.Temperature.HasValue) healthKeys.Add("drive:" + d.Id + ":temp");
            SensorDetails.Value(row.Sub, string.Join('|', healthKeys));
			row.Sub.Foreground = Palette.Brush(color == P.Calm ? P.Soft : color);
			double fraction = Math.Clamp((d.Life ?? 0) / 100.0, 0, 1);
			Color fill = d.Life is < 10 ? Palette.Alpha(P.Ramp[^1], 1) : d.Life is < 30 ? Palette.Alpha(P.Ramp[2], 1) : P.Calm;
			if (row.Bar != null)
			{
				row.Bar.Opacity = d.Life.HasValue ? 1 : 0;
			}
			switch (row.Bar)
			{
			case Meter meter:
				meter.Set(fraction, fill);
				break;
			case GradientBar g:
				(Color a, Color b) = style switch
				{
					WidgetStyle.PurpleDash or WidgetStyle.PurpleList => (Accent("#8b2cf5"), Accent("#e64bd3")),
					WidgetStyle.SciFi => (Accent("#ec4899"), Accent("#3b82f6")),
					WidgetStyle.Thermostat => (Accent("#7cc4f5"), Accent("#f6b25a")),
					_ => (Accent("#22d3ee"), Accent("#ec4899"))
				};
				g.Set(fraction, a, b, P.Dark ? Color.Parse("#1a2230") : P.Track);
				break;
			case FillBack back when style is WidgetStyle.Pill or WidgetStyle.PillRow or WidgetStyle.PillRowGraph or WidgetStyle.FillBars or WidgetStyle.FillBarsGraph:
				back.Opacity = 1;
				back.Animate(fraction, Palette.Alpha(fill, P.Dark ? 0.34 : 0.28));
				break;
			case FillBack bar:
				bar.Animate(fraction, fill);
				break;
			}
		}
		status.Text = Caption(disks.Count == 0 ? "" : warnings == 0 ? L.T("All good") : L.F("{0} to check", warnings));
		status.Foreground = Palette.Brush(warnings == 0 ? P.Calm : Palette.Alpha(P.Ramp[2], 1));
		status.IsVisible = disks.Count > 0;
	}

	private void Apps(IReadOnlyList<ProcessInfo> processes)
	{
		var top = processes.OrderByDescending(x => x.CpuPercent).Take(AppRows).ToList();
		double scale = Math.Max(25.0, top.Select(x => x.CpuPercent).DefaultIfEmpty(0).Max());
		bool memory = S.Shows("proc.memory");
		bool cpu = S.Shows("proc.cpu");
		for (int i = 0; i < rows.Count; i++)
		{
			var row = rows[i];
			if (i >= top.Count)
			{
				// Keep the line (and its height) so the list never jumps; show nothing in it.
				row.Root.Opacity = 0;
				row.Name.Text = "⁠";
				row.Value.Text = "⁠";
				continue;
			}
			row.Root.Opacity = 1;
			ProcessInfo app = top[i];
			row.Name.Text = app.Name;
			string ram = app.MemoryMb >= 1024.0 ? Format.Number(app.MemoryMb / 1024.0, "0.0") + " GB" : Format.Number(app.MemoryMb) + " MB";
			string load = Format.Number(app.CpuPercent, app.CpuPercent < 10.0 ? "0.0" : "0") + " %";
			row.Value.Text = cpu && memory ? load + " · " + ram : cpu ? load : memory ? ram : "";
			row.Value.IsVisible = cpu || memory;
			Color color = P.At(Math.Clamp(app.CpuPercent / 100.0 * 0.9 + 0.1, 0, 1));
			double fraction = Math.Clamp(app.CpuPercent / scale, 0, 1);
			switch (row.Bar)
			{
			case Meter meter:
				meter.Set(fraction, color);
				break;
			case GradientBar g:
				(Color a, Color b) = style switch
				{
					WidgetStyle.PurpleDash or WidgetStyle.PurpleList => (Accent("#8b2cf5"), Accent("#e64bd3")),
					WidgetStyle.SciFi => (Accent("#ec4899"), Accent("#3b82f6")),
					WidgetStyle.Thermostat => (Accent("#7cc4f5"), Accent("#f6b25a")),
					_ => (Accent("#22d3ee"), Accent("#ec4899"))
				};
				g.Set(fraction, a, b, P.Dark ? Color.Parse("#1a2230") : P.Track);
				break;
			case FillBack back when style is WidgetStyle.Pill or WidgetStyle.PillRow or WidgetStyle.PillRowGraph or WidgetStyle.FillBars or WidgetStyle.FillBarsGraph:
				back.Animate(fraction, Palette.Alpha(color, P.Dark ? 0.34 : 0.28));
				break;
			case FillBack bar:
				bar.Animate(fraction, style is WidgetStyle.Dial or WidgetStyle.RingPair ? Tint : color);
				break;
			}
		}
	}
}
