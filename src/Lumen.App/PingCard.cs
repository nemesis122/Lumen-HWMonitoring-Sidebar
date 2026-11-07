using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Lumen.Core;

namespace Lumen.App;

public sealed class PingCard : Card
{
	private readonly Hero hero;

	private readonly TextBlock router;

	private readonly AreaChart? chart;

	private readonly Dictionary<string, Stat> stats;

	public PingCard(Palette p, Metrics m, Settings s)
		: base(p, m, s, "M2,13 L5,13 L7,4 L9.5,12 L11,8 L14,8", "Ping")
	{
		(hero, router, chart) = HeroBlock(L.T("Router"), "ping.chart");
		if (chart != null)
		{
			chart.Max = null;
			chart.Height = m.Chart * 0.8;
		}
		stats = Stats(("ping.jitter", L.T("Jitter")), ("ping.loss", L.T("Loss")), ("ping.target", L.T("Target")));
        WidgetPresentation.Sensor(hero, "ping.latency");
        WidgetPresentation.Sensor((Control)router.Parent!, "ping.router");
	}

	protected override void UpdateReadings(Snapshot s, History h)
	{
		PingInfo ping = s.Ping;
		var (value, unitText) = Format.Split(Format.Milliseconds(ping?.Ms));
		hero.Set(value, unitText, Summaries.PingColor(ping?.Ms, P));
		Motion.Text(router, Format.Milliseconds(ping?.GatewayMs));
		if (chart != null)
		{
			double[] array = h.Read("ping");
			chart.Max = Math.Max(40.0, array.Where(double.IsFinite).DefaultIfEmpty(0.0).Max() * 1.2);
			chart.Set(array, Summaries.PingColor(ping?.Ms, P));
		}
		Card.Put(stats, "ping.jitter", Format.Milliseconds(ping?.JitterMs));
		Dictionary<string, Stat> dictionary = stats;
		object value3;
		if ((object)ping != null)
		{
			double? value2 = ping.LossPercent;
			double lossPercent = ping.LossPercent;
			value3 = Format.Number(value2, (lossPercent > 0.0 && lossPercent < 10.0) ? "0.0" : "0") + " %";
		}
		else
		{
			value3 = "—";
		}
		Card.Put(dictionary, "ping.loss", (string)value3, ((object)ping != null && ping.LossPercent > 0.0) ? P.Ramp[2] : P.Ink);
		Card.Put(stats, "ping.target", ping?.Target ?? S.PingTarget);
	}
}
