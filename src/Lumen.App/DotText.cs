using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Lumen.App;

public sealed class DotText : Control
{
	private static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
	{
		['0'] = new string[7] { ".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###." },
		['1'] = new string[7] { "..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###." },
		['2'] = new string[7] { ".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####" },
		['3'] = new string[7] { "####.", "....#", "....#", ".###.", "....#", "....#", "####." },
		['4'] = new string[7] { "...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#." },
		['5'] = new string[7] { "#####", "#....", "####.", "....#", "....#", "#...#", ".###." },
		['6'] = new string[7] { "..##.", ".#...", "#....", "####.", "#...#", "#...#", ".###." },
		['7'] = new string[7] { "#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#..." },
		['8'] = new string[7] { ".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###." },
		['9'] = new string[7] { ".###.", "#...#", "#...#", ".####", "....#", "...#.", ".##.." },
		['.'] = new string[7] { ".", ".", ".", ".", ".", ".", "#" },
		[','] = new string[7] { ".", ".", ".", ".", ".", "#", "#" },
		[':'] = new string[7] { ".", ".", "#", ".", "#", ".", "." },
		['-'] = new string[7] { "...", "...", "...", "###", "...", "...", "..." },
		['—'] = new string[7] { "....", "....", "....", "####", "....", "....", "...." },
		['−'] = new string[7] { "...", "...", "...", "###", "...", "...", "..." },
		['°'] = new string[7] { "###", "#.#", "###", "...", "...", "...", "..." },
		['%'] = new string[7] { "##..#", "##.#.", "..#..", ".#...", "#..##", "...##", "....." },
		[' '] = new string[7] { "..", "..", "..", "..", "..", "..", ".." }
	};

	private string text = "";

	public string Text
	{
		get
		{
			return text;
		}
		set
		{
			if (!(text == value))
			{
				text = value ?? "";
				InvalidateMeasure();
				InvalidateVisual();
			}
		}
	}

	public Color Color { get; set; } = Colors.White;

	public Color Off { get; set; } = Colors.Transparent;

	public double Cell { get; set; } = 6.0;

	private IEnumerable<string[]> Shapes()
	{
		string[] value;
		return text.Select((char c) => (!Glyphs.TryGetValue(c, out value)) ? Glyphs[' '] : value);
	}

	protected override Size MeasureOverride(Size available)
	{
		int val = Shapes().Sum((string[] g) => g[0].Length + 1) - 1;
		return new Size((double)Math.Max(0, val) * Cell, 7.0 * Cell);
	}

	public override void Render(DrawingContext ctx)
	{
		SolidColorBrush brush = new SolidColorBrush(Color);
		SolidColorBrush solidColorBrush = ((Off.A > 0) ? new SolidColorBrush(Off) : null);
		double num = Cell * 0.38;
		double num2 = 0.0;
		foreach (string[] item in Shapes())
		{
			for (int i = 0; i < 7; i++)
			{
				for (int j = 0; j < item[i].Length; j++)
				{
					Point center = new Point(num2 + (double)j * Cell + Cell / 2.0, (double)i * Cell + Cell / 2.0);
					if (item[i][j] == '#')
					{
						ctx.DrawEllipse(brush, null, center, num, num);
					}
					else if (solidColorBrush != null)
					{
						ctx.DrawEllipse(solidColorBrush, null, center, num, num);
					}
				}
			}
			num2 += (double)(item[0].Length + 1) * Cell;
		}
	}
}
