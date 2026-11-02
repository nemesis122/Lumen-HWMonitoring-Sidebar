using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed class GpuCard : Card
{
	private readonly int index;

	private readonly Hero hero;

	private readonly TextBlock load;

	private readonly AreaChart? chart;

	private readonly TextBlock? vram;

	private readonly Meter? vramBar;

	private readonly Dictionary<string, Stat> stats;

	public GpuCard(Palette p, Metrics m, Settings s, int index)
		: base(p, m, s, "M1.5,4 H14.5 V12 H1.5 Z M6,8 m-2.2,0 a2.2,2.2 0 1 0 4.4,0 a2.2,2.2 0 1 0 -4.4,0 M10,6.5 H12.5 M10,9.5 H12.5 M3,12 V14", L.T("Graphics"))
	{
		this.index = index;
		(hero, load, chart) = HeroBlock(L.T("Load"), "gpu.chart");
		if (Shows("gpu.vram"))
		{
			vram = Ui.Text("—", 11.5, p.Soft, FontWeight.Medium, numbers: true);
			vram.HorizontalAlignment = HorizontalAlignment.Right;
			vramBar = new Meter
			{
				Height = 5.0,
				Palette = p
			};
			Body.Children.Add(Ui.Stack(5.0, Ui.Columns("*,Auto", Card.Label("VRAM", p), vram), vramBar));
		}
		stats = Stats(("gpu.power", L.T("Power")), ("gpu.hotspot", L.T("Hot spot")), ("gpu.fan", L.T("Fan")));
	}

	public override void Update(Snapshot s, History h)
	{
		Show(GpuGroup.Visible(s, S).ElementAtOrDefault(index), h);
	}

	private void Show(GpuInfo? g, History h)
	{
		base.Title = (((object)g != null && g.Integrated) ? (L.T("Graphics") + " · " + L.T("Integrated")) : L.T("Graphics"));
		base.Subtitle = g?.Name ?? "";
		base.Aside = Format.Clock(g?.ClockMhz);
		hero.Set(Temp(g?.Temperature), base.TempUnit, HeatColor(g?.Temperature));
		Motion.Text(load, Format.Percent(g?.Load));
		chart?.Set(((object)g == null) ? Array.Empty<double>() : h.Read("gpu." + g.Id + ".load"), P.At(Format.Share(g?.Load) * 0.9));
		if (vram != null && vramBar != null)
		{
			double? num = g?.MemoryUsedMb;
			if (num.HasValue)
			{
				double valueOrDefault = num.GetValueOrDefault();
				num = g.MemoryTotalMb;
				if (num.HasValue)
				{
					double valueOrDefault2 = num.GetValueOrDefault();
					if (valueOrDefault2 > 0.0)
					{
						Motion.Text(vram, Format.Number(valueOrDefault / 1024.0, "0.0") + " / " + Format.Gigabytes(valueOrDefault2 / 1024.0));
						vramBar.Set(valueOrDefault / valueOrDefault2, P.At(valueOrDefault / valueOrDefault2 * 0.85));
						goto IL_0243;
					}
				}
			}
			vram.Text = "—";
			vramBar.Set(0.0, P.Cool);
		}
		goto IL_0243;
		IL_0243:
		Card.Put(stats, "gpu.power", Format.Watts(g?.PowerW));
		Card.Put(stats, "gpu.hotspot", TempWithUnit(g?.HotSpot), (!(g?.HotSpot).HasValue) ? P.Ink : HeatColor(g.HotSpot - 10.0));
		Card.Put(stats, "gpu.fan", Format.Rpm(g?.FanRpm));
	}
}
