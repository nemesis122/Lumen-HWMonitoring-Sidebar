using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed class BatteryCard : Card
{
	private readonly Hero hero;

	private readonly TextBlock state;

	private readonly Meter bar;

	private readonly Dictionary<string, Stat> stats;

	public BatteryCard(Palette p, Metrics m, Settings s)
		: base(p, m, s, "M2,4.5 H12.5 A1,1 0 0 1 13.5,5.5 V10.5 A1,1 0 0 1 12.5,11.5 H2 A1,1 0 0 1 1,10.5 V5.5 A1,1 0 0 1 2,4.5 Z M15,6.8 V9.2 M3.5,7 V9 M6,7 V9", L.T("Battery"))
	{
		hero = new Hero(M.Hero * 0.9, p);
		state = Ui.Text("", 12.5, p.Soft, FontWeight.Medium);
		state.VerticalAlignment = VerticalAlignment.Bottom;
		state.Margin = new Thickness(0.0, 0.0, 0.0, M.Hero * 0.14);
		Body.Children.Add(Ui.Columns("*,Auto", hero, state));
		bar = new Meter
		{
			Height = 8.0,
			Palette = p,
			Segments = 20
		};
		Body.Children.Add(bar);
		stats = Stats(("battery.time", L.T("Time left")), ("battery.rate", L.T("Rate")), ("battery.health", L.T("Health")));
        WidgetPresentation.Sensor(hero, "battery.level");
        WidgetPresentation.Sensor(bar, "battery.level");
        WidgetPresentation.Sensor(state, "battery.state");
	}

	protected override void UpdateReadings(Snapshot s, History h)
	{
		BatteryInfo battery = s.Battery;
		base.IsVisible = battery != null;
		if (battery == null)
		{
			return;
		}
		Color color = ((battery.Charging || battery.OnAc) ? P.Calm : P.At(Math.Clamp((60.0 - battery.Percent) / 50.0, 0.0, 1.0) * 0.5 + ((battery.Percent < 15.0) ? 0.5 : 0.0)));
		hero.Set(Format.Number(battery.Percent), "%", color);
		state.Text = (battery.Charging ? L.T("Charging") : (battery.OnAc ? L.T("Plugged in") : L.T("On battery")));
		bar.Set(battery.Percent / 100.0, color);
		Stat stat;
		object value2;
		double? minutesLeft;
		if (stats.TryGetValue("battery.time", out Stat value))
		{
			stat = value;
			minutesLeft = battery.MinutesLeft;
			if (minutesLeft.HasValue)
			{
				double valueOrDefault = minutesLeft.GetValueOrDefault();
				if (valueOrDefault > 0.0)
				{
					value2 = $"{(int)(valueOrDefault / 60.0)} h {(int)(valueOrDefault % 60.0):00} m";
					goto IL_01c2;
				}
			}
			value2 = "—";
			goto IL_01c2;
		}
		goto IL_0205;
		IL_0205:
		Dictionary<string, Stat> dictionary = stats;
		minutesLeft = battery.PowerW;
		object value3;
		if (minutesLeft.HasValue)
		{
			double valueOrDefault2 = minutesLeft.GetValueOrDefault();
			value3 = ((valueOrDefault2 > 0.0) ? "+" : "") + Format.Watts(valueOrDefault2);
		}
		else
		{
			value3 = "—";
		}
		Card.Put(dictionary, "battery.rate", (string)value3);
		Dictionary<string, Stat> dictionary2 = stats;
		minutesLeft = battery.WearPercent;
		object value4;
		if (minutesLeft.HasValue)
		{
			double valueOrDefault3 = minutesLeft.GetValueOrDefault();
			value4 = Format.Percent(100.0 - valueOrDefault3);
		}
		else
		{
			value4 = "—";
		}
		Card.Put(dictionary2, "battery.health", (string)value4);
		return;
		IL_01c2:
		stat.Value = (string)value2;
		((TextBlock)value.Children[0]).Text = (battery.Charging ? L.T("Until full") : L.T("Time left")).ToUpper(L.Culture);
		goto IL_0205;
	}
}
