using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed class MemoryCard : Card
{
	private readonly Hero hero;

	private readonly TextBlock load;

	private readonly Meter? bar;

	private readonly Dictionary<string, Stat> stats;

	public MemoryCard(Palette p, Metrics m, Settings s)
		: base(p, m, s, "M1.5,5 H14.5 V11 H1.5 Z M4,7 V9 M6.5,7 V9 M9,7 V9 M11.5,7 V9 M3,11 V13 M6,11 V13 M10,11 V13 M13,11 V13", L.T("Memory"))
	{
		hero = new Hero(m.Hero * 0.8, p);
		load = Ui.Text("—", Math.Round(m.Hero * 0.5), p.Ink, FontWeight.Medium, numbers: true);
		load.VerticalAlignment = VerticalAlignment.Bottom;
		load.Margin = new Thickness(0.0, 0.0, 0.0, m.Hero * 0.1);
		Body.Children.Add(Ui.Columns("*,Auto", hero, load));
		if (Shows("mem.bar"))
		{
			bar = new Meter
			{
				Height = 8.0,
				Palette = p,
				Segments = 24,
				RampBySegment = true
			};
			Body.Children.Add(bar);
		}
		stats = Stats(("mem.free", L.T("Available")), ("mem.total", L.T("Installed")), ("mem.peak", L.T("2-min peak")));
	}

	public override void Update(Snapshot s, History h)
	{
		MemoryInfo memory = s.Memory;
		var (value, text) = Format.Split(Format.Gigabytes(memory?.UsedGb));
		hero.Set(value, text + " " + L.T("used"), P.Ink);
		Motion.Text(load, Format.Percent(memory?.Load));
		load.Foreground = Palette.Brush(P.At(Format.Share(memory?.Load) * 0.9));
		bar?.Set(Format.Share(memory?.Load), P.Cool);
		Card.Put(stats, "mem.free", Format.Gigabytes(memory?.AvailableGb));
		Card.Put(stats, "mem.total", Format.Gigabytes(memory?.TotalGb));
		double[] array = h.Read("mem.load").Where(double.IsFinite).ToArray();
		Card.Put(stats, "mem.peak", (array.Length == 0) ? "—" : Format.Percent(array.Max()));
	}
}
