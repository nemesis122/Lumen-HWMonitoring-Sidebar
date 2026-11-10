using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed class PillRowTile : SummaryTile
{
    public double CapsuleHeight => M.Subtitles ? 44 : 38;
    public static Panel Bubble(TextBlock caption, Control value, FillBack fill, double height, Thickness margin)
    {
        var fit = SteadyHeight.Of(new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Child = value, HorizontalAlignment = HorizontalAlignment.Center });
        var content = Ui.Stack(0, fit, caption); content.VerticalAlignment = VerticalAlignment.Center; content.Margin = new Thickness(8, 0);
        return new Panel { Height = height, Margin = margin, Children = { fill, content } };
    }
	private readonly Grid row = new Grid
	{
		Margin = new Thickness(0.0, 8.0, 0.0, 0.0)
	};

	private readonly List<(FillBack Back, TextBlock Label, TextBlock Value)> pills = new List<(FillBack, TextBlock, TextBlock)>();

	private readonly DriveRows drives;

	// "Capsule row graph": the further values (clock, voltage, fan, free …) become capsules too.
	private readonly bool graph;

	private readonly Avalonia.Controls.Primitives.UniformGrid more = new Avalonia.Controls.Primitives.UniformGrid { Columns = 3, Margin = new Thickness(-3, 6, -3, 0), IsVisible = false };

	public PillRowTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s, bool graph = false)
		: base(kind, index, p, m, s, half: false)
	{
		this.graph = graph;
		StackPanel stackPanel = new StackPanel
		{
			Spacing = 2.0,
			Tag = "elastic"
		};
		stackPanel.Children.Add(base.Head);
		stackPanel.Children.Add(row);
		// Drives: one line per drive with a capsule-shaped bar instead of a row of capsules.
		drives = new DriveRows(8, 11.5, 13, p.Soft, () => new FillBack { Height = 14, Radius = 7, Track = Palette.Mix(p.Card, p.Ink, p.Dark ? 0.06 : 0.05) },
			(c, r) => ((FillBack)c).Animate(r.Fraction, Palette.Alpha(r.Color, p.Dark ? 0.55 : 0.45)), r => r.Color == p.Cool ? p.Ink : r.Color) { Margin = new Thickness(0, 8, 0, 4) };
		stackPanel.Children.Add(drives);
		stackPanel.Children.Add(more);
		stackPanel.Children.Add(Extras);
		// Graph designs: the free height goes below the lines, not between the title and the first line,
		// so in a bar the lines of all cards start right under their titles.
		if (graph)
		{
			stackPanel.Tag = null;
			drives.Tag = null;
		}
		base.Child = stackPanel;
	}

	protected override List<(string Label, string Value)> AdjustExtras(List<(string Label, string Value)> items)
	{
		if (!graph)
		{
			return items;
		}
		double h = (M.Subtitles ? 44 : 38);
        more.Columns = Kind == WidgetKind.Drives && items.Count > 8 ? 2 : 3;
		foreach (var (label, value) in items)
		{
			double? fraction = ExtraFraction(label, value);
			Color color = value.Contains('°') && fraction is double heat ? P.At(heat) : P.Cool;
			var back = new FillBack { Radius = h / 2.0, Track = Palette.Mix(P.Card, P.Ink, P.Dark ? 0.06 : 0.05) };
			back.Animate(fraction ?? 0, Palette.Alpha(color, P.Dark ? 0.34 : 0.28));
			var v = Ui.Text(value.Replace(" °C", "°").Replace(" °F", "°F"), 13.5, value.Contains('°') ? color : P.Ink, FontWeight.Bold, numbers: true);
			var l = Ui.Text(label.ToUpper(L.Culture), 9.5, P.Soft, FontWeight.DemiBold);
			l.LetterSpacing = 0.5;
			l.HorizontalAlignment = HorizontalAlignment.Center;
			l.TextTrimming = TextTrimming.CharacterEllipsis;
            more.Children.Add(Bubble(l, v, back, h, new Thickness(3, 0, 3, 6)));
		}
		more.IsVisible = more.Children.Count > 0;
		return new List<(string Label, string Value)>();
	}

	protected override void Show(Summary sum)
	{
		// Capsules of the last update must not count as "already shown" when the values are picked again.
		more.Children.Clear();
		more.IsVisible = false;
		row.IsVisible = Kind != WidgetKind.Drives || graph;
		drives.IsVisible = false;
		if (Kind == WidgetKind.Drives && !graph)
		{
			drives.Show(DriveRows.Of(Index, Last.Snap, Last.History, S, P));
			return;
		}
        List<Reading> list = Kind == WidgetKind.Drives
            ? DriveRows.Of(Index, Last.Snap, Last.History, S, P).Select(r => r with { Label = r.Label + " · " + L.T("Temp") }).ToList()
            : Readings.For(Kind, Index, base.Last.Snap, base.Last.History, S, P).Take(4).ToList();
		double num = (M.Subtitles ? 44 : 38);
		if (pills.Count != list.Count)
		{
			row.Children.Clear();
			pills.Clear();
            int columns = Kind == WidgetKind.Drives ? Math.Clamp(list.Count, 1, 2) : Math.Max(1, list.Count);
			row.ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("*", columns)));
            row.RowDefinitions = new RowDefinitions(string.Join(",", Enumerable.Repeat("Auto", Math.Max(1, (int)Math.Ceiling(list.Count / (double)columns)))));
			for (int i = 0; i < list.Count; i++)
			{
				FillBack fillBack = new FillBack
				{
					Radius = num / 2.0,
					Track = Palette.Mix(P.Card, P.Ink, P.Dark ? 0.06 : 0.05)
				};
				TextBlock textBlock = Ui.Text("", 13.5, P.Ink, FontWeight.Bold, numbers: true);
				TextBlock textBlock2 = Ui.Text("", 9.5, P.Soft, FontWeight.DemiBold);
				textBlock2.LetterSpacing = 0.5;
				TextBlock[] array = new TextBlock[2] { textBlock, textBlock2 };
				for (int j = 0; j < array.Length; j++)
				{
					array[j].HorizontalAlignment = HorizontalAlignment.Center;
				}
                Panel panel = Bubble(textBlock2, textBlock, fillBack, num, new Thickness(3, 0, 3, Kind == WidgetKind.Drives ? 6 : 0));
				Grid.SetColumn(panel, i % columns); Grid.SetRow(panel, i / columns);
				row.Children.Add(panel);
				pills.Add((fillBack, textBlock2, textBlock));
			}
		}
		for (int k = 0; k < list.Count; k++)
		{
			Reading reading = list[k];
            BindReading(pills[k].Value, reading);
			pills[k].Back.Animate(reading.Fraction, Palette.Alpha(reading.Color, P.Dark ? 0.34 : 0.28));
			pills[k].Value.Text = reading.Value.Replace(" °C", "°").Replace(" °F", "°F");
			pills[k].Value.Foreground = Palette.Brush((reading.Color == P.Cool) ? P.Ink : reading.Color);
			pills[k].Label.Text = reading.Label.ToUpper(L.Culture);
			pills[k].Label.IsVisible = reading.Label.Length > 0;
		}
	}
}
