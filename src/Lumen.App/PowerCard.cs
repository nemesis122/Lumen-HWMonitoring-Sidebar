using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Lumen.Core;

namespace Lumen.App;

public sealed class PowerCard : Card
{
	private readonly Hero hero;

	private readonly TextBlock today;

	private readonly AreaChart? chart;

	private readonly Dictionary<string, Stat> stats;

	private readonly TextBlock note;

	public PowerCard(Palette p, Metrics m, Settings s)
		: base(p, m, s, "M9,1.5 L3.5,9 H8 L7,14.5 L12.5,7 H8 Z", L.T("Power"))
	{
		(hero, today, chart) = HeroBlock(L.T("Today"), "power.chart");
		if (chart != null)
		{
			chart.Max = null;
			chart.Height = m.Chart * 0.8;
		}
		stats = Stats((!Shows("power.cost")) ? Array.Empty<(string, string)>() : new(string, string)[3]
		{
			("power.today", L.T("Today")),
			("power.week", L.T("This week")),
			("power.month", L.T("This month"))
		});
		note = Ui.Text(L.F("CPU + GPU + {0} W for the rest (estimate)", Format.Number(s.BaseWatts)), 11.0, p.Faint);
		note.IsVisible = Shows("power.note");
		Body.Children.Add(note);
	}

	public override void Update(Snapshot s, History h)
	{
		UsageInfo usage = s.Usage;
		hero.Set(Format.Number(usage?.WattsNow), "W", P.At(Math.Clamp((usage?.WattsNow ?? 0.0) / 600.0, 0.0, 1.0)));
		Motion.Text(today, ((object)usage == null) ? "—" : (Format.Number(usage.KwhToday, "0.00") + " kWh"));
		chart?.Set(h.Read("power"), P.At(Math.Clamp((usage?.WattsNow ?? 0.0) / 600.0, 0.0, 1.0)));
		if (!(usage == null))
		{
			Card.Put(stats, "power.today", Format.Money(usage.KwhToday * S.PricePerKwh, S.Currency));
			Card.Put(stats, "power.week", Format.Money(usage.KwhWeek * S.PricePerKwh, S.Currency));
			Card.Put(stats, "power.month", Format.Money(usage.KwhMonth * S.PricePerKwh, S.Currency));
		}
	}
}
