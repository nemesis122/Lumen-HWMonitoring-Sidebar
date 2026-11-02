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

public sealed class HeatmapTile : SummaryTile
{
	private readonly UniformGrid grid = new UniformGrid
	{
		Margin = new Thickness(0.0, 6.0, 0.0, 0.0)
	};

	private readonly List<(Border Cell, TextBlock Text)> cells = new List<(Border, TextBlock)>();

	private readonly Hero hero;

	private readonly TextBlock foot;

	public HeatmapTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s)
		: base(kind, index, p, m, s, half: false)
	{
		hero = new Hero(m.Subtitles ? 22 : 20, p)
		{
			HorizontalAlignment = HorizontalAlignment.Right
		};
		foot = Ui.Text("", 11.0, p.Soft, FontWeight.Normal, numbers: true);
		foot.Margin = new Thickness(0.0, 4.0, 0.0, 0.0);
		Caption.IsVisible = false;
		Grid item = Ui.Columns("*,Auto", base.Head, hero);
		Grid.SetColumn(hero, 1);
		StackPanel stackPanel = new StackPanel
		{
			Spacing = 2.0,
			Tag = "elastic"
		};
		stackPanel.Children.Add(item);
		stackPanel.Children.Add(grid);
		stackPanel.Children.Add(foot);
		stackPanel.Children.Add(Extras);
		base.Child = stackPanel;
	}

	private void Shape(int n, bool labelled)
	{
		if (cells.Count != n)
		{
			grid.Children.Clear();
			cells.Clear();
			grid.Columns = ((n <= 6) ? Math.Max(1, n) : ((n <= 12) ? 6 : 8));
			for (int i = 0; i < n; i++)
			{
				TextBlock textBlock = Ui.Text("", labelled ? 10.5 : 10.5, P.Ink, FontWeight.DemiBold, numbers: true);
				textBlock.HorizontalAlignment = HorizontalAlignment.Center;
				Border border = new Border
				{
					CornerRadius = new CornerRadius(5.0),
					Margin = new Thickness(2.0),
					Height = (M.Subtitles ? 24 : 20),
					Child = textBlock
				};
				grid.Children.Add(border);
				cells.Add((border, textBlock));
			}
		}
	}

	protected override void Show(Summary sum)
	{
		Snapshot item = base.Last.Snap;
		List<(double, Color, string)> list = new List<(double, Color, string)>();
		string text = sum.Caption;
		if (Kind == WidgetKind.Cpu)
		{
			CpuInfo cpu = item.Cpu;
			if ((object)cpu != null)
			{
				IReadOnlyList<double> coreLoads = cpu.CoreLoads;
				if (coreLoads != null && coreLoads.Count > 0)
				{
					foreach (double coreLoad in cpu.CoreLoads)
					{
						list.Add((coreLoad / 100.0, P.At(Math.Clamp(coreLoad / 100.0, 0.0, 1.0)), Format.Number(coreLoad)));
					}
					int item2 = cpu.CoreLoads.Select((double l, int i) => (l: l, i: i)).MaxBy(((double l, int i) x) => x.l).i;
					text = $"{cpu.CoreLoads.Count} {L.T("Threads")} · {L.T("Busiest")} #{item2 + 1} · {sum.Caption}";
					hero.Set(sum.Value, sum.Unit, sum.Color);
					goto IL_0360;
				}
			}
		}
		if (sum.Series.Length > 1)
		{
			double[] array = sum.Series.TakeLast(24).ToArray();
			double num = sum.SeriesMax ?? Math.Max(1E-09, array.Max());
			Color b = Shade(sum);
			double[] array2 = array;
			for (int num2 = 0; num2 < array2.Length; num2++)
			{
				double num3 = Math.Clamp(array2[num2] / num, 0.0, 1.0);
				list.Add((1.0, Palette.Mix(P.Track, b, 0.2 + 0.8 * num3), ""));
			}
			hero.Set(sum.Value, sum.Unit, sum.Color);
		}
		else
		{
			foreach (Reading item5 in Readings.For(Kind, Index, item, base.Last.History, S, P))
			{
				list.Add((item5.Fraction, item5.Color, item5.Value));
			}
			hero.Set(sum.Value, sum.Unit, sum.Color);
		}
		goto IL_0360;
		IL_0360:
		Shape(list.Count, list.Any<(double, Color, string)>(((double Fraction, Color Color, string Text) v) => v.Text.Length > 0));
		for (int num4 = 0; num4 < list.Count; num4++)
		{
			(Border Cell, TextBlock Text) tuple = cells[num4];
			Border item3 = tuple.Cell;
			TextBlock item4 = tuple.Text;
			Color c = ((list[num4].Item1 < 0.25) ? Palette.Mix(P.Track, list[num4].Item2, 0.35 + list[num4].Item1) : list[num4].Item2);
			item3.Background = Palette.Brush(Color.FromArgb(byte.MaxValue, c.R, c.G, c.B));
			item4.Text = list[num4].Item3;
			item4.Foreground = Palette.Brush(Readings.TextOn(c, P));
		}
		foot.Text = text;
	}
}
