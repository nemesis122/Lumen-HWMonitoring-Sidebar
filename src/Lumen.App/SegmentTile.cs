using Avalonia;
using Avalonia.LogicalTree;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed class SegmentTile : SummaryTile
{
	private readonly Meter first;

	private readonly Meter second;

	private readonly TextBlock firstValue;

	private readonly TextBlock secondValue;

	private readonly TextBlock secondLabel;

	private readonly TextBlock firstLabel;

	private readonly Grid secondRow;

	private readonly Grid firstRow;

	private readonly DriveRows drives;

	// "Segment graph": every further value (clock, power, fan, free …) gets a segment line too, instead of
	// the plain list of numbers below.
	private readonly bool graph;

	private readonly StackPanel more = new StackPanel { Spacing = 8, IsVisible = false };

	private readonly Palette palette;

	public SegmentTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s, bool graph = false)
		: base(kind, index, p, m, s, half: false)
	{
		this.graph = graph;
		palette = p;
		Grid grid = Line("", out firstLabel, out first, out firstValue);
		firstRow = grid;
		// Drives: every drive as its own segment line.
		drives = new DriveRows(8, 11, 13, p.Soft, () => new Meter { Height = 12, Palette = p, Segments = 20, RampBySegment = true },
			(c, r) => ((Meter)c).Set(r.Fraction, r.Color), r => r.Color);
		// In the graph variant reading and writing follow as lines of their own; the drive lines keep the
		// same spacing as those instead of spreading over the free height.
		if (graph)
		{
			drives.Tag = null;
		}
        WidgetPresentation.Sensor(grid, WidgetSensors.PrimaryKey(kind));
		secondRow = Line(L.T("Load"), out secondLabel, out second, out secondValue);
        WidgetPresentation.Sensor(secondRow, WidgetSensors.SecondaryKey(kind));
		StackPanel item = Ui.Stack(8.0, base.Head, grid, secondRow, drives, more);
		if (graph)
		{
			// One label width for all lines, so every bar starts at the same place.
			firstLabel.Width = secondLabel.Width = LabelWidth;
		}
		StackPanel stackPanel = new StackPanel
		{
			Spacing = 8.0,
			Tag = "elastic"
		};
		stackPanel.Children.Add(item);
		stackPanel.Children.Add(new Border
		{
			Height = 0.0
		});
		stackPanel.Children.Add(Extras);
		base.Child = stackPanel;
		Grid Line(string label, out TextBlock l, out Meter meter, out TextBlock v)
		{
			l = Ui.Text(label, 11.0, p.Soft, FontWeight.Medium);
			l.MinWidth = 46.0;
			l.MaxWidth = 92.0;
			l.VerticalAlignment = VerticalAlignment.Center;
			meter = new Meter
			{
				Height = 12.0,
				Palette = p,
				Segments = 20,
				VerticalAlignment = VerticalAlignment.Center
			};
			v = Ui.Text("", 13.0, p.Ink, FontWeight.DemiBold, numbers: true);
			v.MinWidth = 58.0;
			v.TextAlignment = TextAlignment.Right;
			v.VerticalAlignment = VerticalAlignment.Center;
			Grid result = Ui.Columns("Auto,*,Auto", l, meter, v);
			Share(result);
			meter.Margin = new Thickness(8, 0, 12, 0);
			return result;
		}
	}

	private const double LabelWidth = 84;

	// One name width and one value width for every line of the widget (main lines, further lines and
	// drive lines), so all bars start and end at the same place.
	private static Grid Share(Grid row)
	{
		row.ColumnDefinitions[0].SharedSizeGroup = "LumenName";
		row.ColumnDefinitions[2].SharedSizeGroup = "LumenValue";
		return row;
	}

	protected override List<(string Label, string Value)> AdjustExtras(List<(string Label, string Value)> items)
	{
		if (!graph)
		{
			return items;
		}
		foreach (var (label, value) in items)
		{
			var l = Ui.Text(label, 11.0, palette.Soft, FontWeight.Medium);
			l.Width = Kind == WidgetKind.Drives ? double.NaN : LabelWidth;
			l.MinWidth = 46;
			l.TextTrimming = TextTrimming.CharacterEllipsis;
			l.VerticalAlignment = VerticalAlignment.Center;
			var v = Ui.Text(value, 13.0, palette.Ink, FontWeight.DemiBold, numbers: true);
			v.MinWidth = 58.0;
			v.TextAlignment = TextAlignment.Right;
			v.VerticalAlignment = VerticalAlignment.Center;
			// The same spacing as the main lines and the drive lines, so every bar starts and ends together.
			var meter = new Meter { Height = 12.0, Palette = palette, Segments = 20, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 12, 0) };
			double? fraction = SegmentScale.Fraction(Kind, label, value, Last.Snap);
			if (fraction is double fr)
			{
				bool heat = value.Contains('°');
				meter.RampBySegment = heat;
				meter.Set(fr, heat ? palette.At(fr) : palette.Cool);
				if (heat)
				{
					v.Foreground = Palette.Brush(palette.At(fr));
				}
			}
			else
			{
				// Text without a scale (an address, a model name): no bar.
				meter.IsVisible = false;
			}
			more.Children.Add(Share(Ui.Columns("Auto,*,Auto", l, meter, v)));
		}
		more.IsVisible = more.Children.Count > 0;
		return new List<(string Label, string Value)>();
	}

	// Widest name and value seen so far, per text size, so the columns never shrink and jump back.
	private readonly Dictionary<string, double> widest = new();

	// Every line of the widget (main lines, further lines, drive lines) gets one name width and one value
	// width before it is laid out, so all bars start and end at the same place. A line without a bar
	// (an address) keeps its own widths.
	protected override Size MeasureOverride(Size available)
	{
		var rows = this.GetLogicalDescendants().OfType<Grid>()
			.Where(g => g.ColumnDefinitions.Count == 3 && g.ColumnDefinitions[0].SharedSizeGroup == "LumenName" && g.IsVisible && g.Children.Any(c => Grid.GetColumn(c) == 1 && c.IsVisible) && g.GetLogicalAncestors().OfType<Control>().All(a => a.IsVisible))
			.ToList();
		if (rows.Count > 1)
		{
			double Widest(int column)
			{
				double w = 0;
				string size = "";
				foreach (var row in rows)
				{
					foreach (var cell in row.Children.Where(c => Grid.GetColumn(c) == column && c.IsVisible))
					{
						cell.Measure(Size.Infinity);
						w = Math.Max(w, Math.Ceiling(cell.DesiredSize.Width));
						if (size.Length == 0 && cell is TextBlock tb) size = tb.FontSize.ToString("0.#");
					}
				}
				string key = column + "|" + size;
				w = Math.Max(w, widest.GetValueOrDefault(key));
				widest[key] = w;
				return w;
			}
			double name = Widest(0), value = Widest(2);
			foreach (var row in rows)
			{
				var cols = row.ColumnDefinitions;
				if (cols[0].Width.Value != name || !cols[0].Width.IsAbsolute) cols[0].Width = new GridLength(name);
				if (cols[2].Width.Value != value || !cols[2].Width.IsAbsolute) cols[2].Width = new GridLength(value);
			}
		}
		return base.MeasureOverride(available);
	}

	protected override void Show(Summary sum)
	{
		// The lines of the last update must not count as "already shown" when the values are picked again.
		more.Children.Clear();
		bool all = Kind == WidgetKind.Drives;
		firstRow.IsVisible = !all;
		drives.IsVisible = false;
		if (all)
		{
			secondRow.IsVisible = false;
			drives.Show(DriveRows.Of(Index, Last.Snap, Last.History, S, P));
			return;
		}
        if (Kind == WidgetKind.Network)
        {
            var readings = Readings.For(Kind, Index, Last.Snap, Last.History, S, P);
            var download = readings.FirstOrDefault(r => r.Label.StartsWith("↓"));
            var upload = readings.FirstOrDefault(r => r.Label.StartsWith("↑"));
            firstLabel.Text = "DL";
            firstLabel.IsVisible = true;
            firstValue.Text = download?.Value ?? "—";
            firstValue.Foreground = Palette.Brush(P.Cool);
            first.RampBySegment = false;
            first.Set(download?.Fraction ?? 0, P.Cool);
            secondRow.IsVisible = S.Shows("net.up");
            secondLabel.Text = "UP";
            secondValue.Text = upload?.Value ?? "—";
            secondValue.Foreground = Palette.Brush(P.Cool);
            second.RampBySegment = false;
            second.Set(upload?.Fraction ?? 0, P.Cool);
            return;
        }
		TextBlock textBlock = firstLabel;
		string unit = sum.Unit;
		bool flag = ((unit == "°C" || unit == "°F") ? true : false);
		// The narrow label shows only the sensor's short name ("C:", "Mainboard").
		textBlock.Text = (flag ? ((sum.HeroLabel.Length > 0) ? sum.HeroLabel.Split('·')[^1].Trim() : L.T("Temp")) : "");
		if (graph && firstLabel.Text.Length == 0)
		{
			// Every line has its name here, so the bars line up.
			firstLabel.Text = WidgetSensors.PrimaryLabel(Kind);
		}
		firstLabel.IsVisible = firstLabel.Text.Length > 0;
		firstValue.Text = ((sum.Value == "—") ? sum.Value : (sum.Value + " " + sum.Unit).Trim());
		firstValue.Foreground = Palette.Brush(sum.Color);
		first.RampBySegment = true;
		first.Set(sum.Fraction, Shade(sum));
		secondRow.IsVisible = sum.Second.HasValue;
		double? num = sum.Second;
		if (num.HasValue)
		{
			double valueOrDefault = num.GetValueOrDefault();
			second.RampBySegment = false;
			second.Set(valueOrDefault, P.Cool);
			secondValue.Text = ((sum.SecondLabel.Length > 0) ? sum.SecondLabel : Format.Percent(valueOrDefault * 100.0));
			secondLabel.Text = L.T("Load");
		}
	}
}

/// <summary>
/// How full a segment line is for a value that has no fraction of its own: percentages as they are,
/// temperatures by heat, clocks, power, voltage and fan speed against a typical maximum, sizes against
/// the total, rates on a logarithmic scale (1 KB/s … 1 GB/s). Text without a number has no line.
/// </summary>
public static class SegmentScale
{
	private static readonly System.Text.RegularExpressions.Regex Number = new System.Text.RegularExpressions.Regex(@"-?\d+(?:[.,]\d+)?");

	private static double Parse(string s) => double.Parse(s.Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture);

	public static double? Fraction(WidgetKind kind, string label, string value, Snapshot snap)
	{
		var numbers = Number.Matches(value).Select(m => Parse(m.Value)).ToList();
		if (numbers.Count == 0 || value.Count(char.IsDigit) > 0 && value.Count(c => c == '.') >= 3)
		{
			return null; // no number, or an IP address
		}
		double n = numbers[0];
		string v = value.Replace("\u2060", "");
		double Clamp(double x) => double.IsFinite(x) ? Math.Clamp(x, 0, 1) : 0;
		double Log(double x, double lo, double hi) => x <= lo ? 0 : Clamp(Math.Log10(x / lo) / Math.Log10(hi / lo));
		if (v.Contains('%')) return Clamp(n / 100);
		if (v.Contains("°F")) return Format.Heat((n - 32) / 1.8);
		if (v.Contains('°')) return Format.Heat(n);
		if (numbers.Count >= 2 && v.Contains('/') && (v.Contains("GB") || v.Contains("TB") || v.Contains("MB"))) return Clamp(numbers[0] / numbers[1]);
		if (v.EndsWith("GHz")) return Clamp(n / (kind == WidgetKind.Gpu ? 3.0 : 6.0));
		if (v.EndsWith("MHz")) return Clamp(n / (kind == WidgetKind.Gpu ? 3000.0 : 6000.0));
		if (v.EndsWith(" V")) return Clamp(n / 1.6);
		if (v.EndsWith(" W")) return Clamp(n / (kind == WidgetKind.Gpu ? 450.0 : 250.0));
		if (v.Contains("min") || v.Contains("rpm")) return Clamp(n / 3000.0);
		if (v.EndsWith("bit/s"))
		{
			double bits = n * (v.EndsWith("Gbit/s") ? 1e9 : v.EndsWith("Mbit/s") ? 1e6 : v.EndsWith("kbit/s") ? 1e3 : 1);
			return Log(bits, 1e3, 1e10);
		}
		if (v.EndsWith("B/s"))
		{
			double bytes = n * (v.EndsWith("GB/s") ? 1 << 30 : v.EndsWith("MB/s") ? 1 << 20 : v.EndsWith("KB/s") ? 1 << 10 : 1);
			return Log(bytes, 1 << 10, 1 << 30);
		}
		if (v.EndsWith("GB") || v.EndsWith("TB"))
		{
			double gb = n * (v.EndsWith("TB") ? 1024 : 1);
			double? total = kind == WidgetKind.Memory ? snap.Memory?.TotalGb : null;
			return total is double t && t > 0 ? Clamp(gb / t) : 1;
		}
		if (v.EndsWith("ms")) return Log(n, 1, 1000);
		return null;
	}
}
