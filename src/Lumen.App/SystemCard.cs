using System.Collections.Generic;
using Avalonia.Controls;
using Lumen.Core;

namespace Lumen.App;

public sealed class SystemCard : Card
{
	private readonly Hero hero;

	private readonly Dictionary<string, Stat> stats;

	public SystemCard(Palette p, Metrics m, Settings s)
		: base(p, m, s, "M1.5,3 H14.5 V11 H1.5 Z M6,14 H10 M8,11 V14", L.T("System"))
	{
		hero = new Hero(M.Hero * 0.8, p);
		TextBlock textBlock = Card.Label(L.T("Uptime"), p);
		Body.Children.Add(Ui.Stack(0.0, textBlock, hero));
		stats = Stats(("system.boot", L.T("Started")), ("system.processes", L.T("Processes")), ("system.threads", L.T("Threads")));
		Body.Children.Add(ExecutableLocation.Create(p, false));
	}

	public override void Update(Snapshot s, History h)
	{
		SystemInfo system = s.System;
		base.Subtitle = (((object)system == null) ? "" : (system.Os + " · " + system.Build));
		string text = (((object)system == null) ? "—" : Format.Duration(system.Uptime));
		int num = text.IndexOf(' ');
		Hero obj = hero;
		string value = ((num < 0) ? text : text.Substring(0, num));
		object unitText;
		if (num >= 0)
		{
			string text2 = text;
			int num2 = num + 1;
			unitText = text2.Substring(num2, text2.Length - num2);
		}
		else
		{
			unitText = "";
		}
		obj.Set(value, (string)unitText, P.Ink);
		if (!(system == null))
		{
			Card.Put(stats, "system.boot", system.BootTime.ToString((system.Uptime.TotalDays >= 1.0) ? "ddd HH:mm" : "HH:mm", L.Culture));
			Card.Put(stats, "system.processes", Format.Number(system.Processes));
			Card.Put(stats, "system.threads", Format.Number(system.Threads));
		}
	}
}

