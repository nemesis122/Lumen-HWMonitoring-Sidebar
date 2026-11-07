using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Lumen.Core;

namespace Lumen.App;

public sealed class CpuCard : Card
{
	private readonly Hero hero;

	private readonly Hero load;

	private readonly AreaChart? chart;

	private readonly Skyline? cores;

	private readonly Dictionary<string, Stat> stats;

	public CpuCard(Palette p, Metrics m, Settings s)
		: base(p, m, s, "M5,3 H11 A2,2 0 0 1 13,5 V11 A2,2 0 0 1 11,13 H5 A2,2 0 0 1 3,11 V5 A2,2 0 0 1 5,3 Z M6.5,6.5 H9.5 V9.5 H6.5 Z M6,1 V3 M10,1 V3 M6,13 V15 M10,13 V15 M1,6 H3 M1,10 H3 M13,6 H15 M13,10 H15", L.T("Processor"))
	{
		(hero, load, chart) = TwinHeroBlock(L.T("Load"), "cpu.chart");
		if (Shows("cpu.cores"))
		{
			cores = new Skyline
			{
				Height = m.Skyline,
				Palette = p
			};
			Body.Children.Add(cores);
		}
		stats = Stats(("cpu.power", L.T("Power")), ("cpu.voltage", L.T("Voltage")), ("cpu.fan", L.T("Fan")));
        WidgetPresentation.Sensor(hero, "cpu.temp");
        WidgetPresentation.Sensor((Control)load.Parent!, "cpu.load");
	}

	protected override void UpdateReadings(Snapshot s, History h)
	{
		CpuInfo cpu = s.Cpu;
		base.Subtitle = cpu?.Name ?? "";
		base.Aside = Format.Clock(cpu?.ClockMhz);
		hero.Set(Temp(cpu?.Temperature), base.TempUnit, HeatColor(cpu?.Temperature));
		load.Set(Format.Number(cpu?.Load), "%", P.Cool);
		chart?.Set(h.Read("cpu.load"), P.At(Format.Share(cpu?.Load) * 0.9));
		if (cores != null)
		{
			cores.Loads = cpu?.CoreLoads ?? Array.Empty<double>();
			cores.IsVisible = cores.Loads.Count > 0;
			cores.InvalidateVisual();
		}
		Card.Put(stats, "cpu.power", Format.Watts(cpu?.PowerW));
		Card.Put(stats, "cpu.voltage", Format.Volts(cpu?.Voltage));
		Card.Put(stats, "cpu.fan", Format.Rpm(cpu?.FanRpm));
	}
}
