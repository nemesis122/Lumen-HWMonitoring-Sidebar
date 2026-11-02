using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed class FpsCard : Card
{
	private readonly Hero hero;

	private readonly TextBlock low;

	private readonly AreaChart? chart;

	private readonly Dictionary<string, Stat> stats;

	private readonly TextBlock empty;

	private readonly Control[] live;

	public FpsCard(Palette p, Metrics m, Settings s)
		: base(p, m, s, "M2,12 A6,6 0 0 1 14,12 M8,12 L11,7.5 M4.2,8.6 L5,9.3 M11.8,8.6 L11,9.3 M8,6 V7", "FPS")
	{
		(hero, low, chart) = HeroBlock(L.T("1 % low"), "fps.chart");
		if (chart != null)
		{
			chart.Max = null;
		}
		stats = Stats(("fps.frametime", L.T("Frame time")));
		empty = Ui.Text(L.T("No game in the foreground."), 12.0, p.Soft);
		empty.TextWrapping = TextWrapping.Wrap;
		empty.TextTrimming = TextTrimming.None;
		Body.Children.Add(empty);
		live = (from c in Body.Children.Skip(1)
			where c != empty
			select c).ToArray();
	}

	public static Color Color(double? fps, Palette p)
	{
		if (fps.HasValue)
		{
			double valueOrDefault = fps.GetValueOrDefault();
			if (!(valueOrDefault >= 60.0))
			{
				if (!(valueOrDefault >= 30.0))
				{
					return p.Ramp[^1];
				}
				return p.Ramp[2];
			}
			return p.Calm;
		}
		return p.Faint;
	}

	public override void Update(Snapshot s, History h)
	{
		FpsInfo fps = s.Fps;
		Control[] array = live;
		for (int i = 0; i < array.Length; i++)
		{
			array[i].IsVisible = fps != null;
		}
		empty.IsVisible = fps == null;
		base.Aside = fps?.App ?? "";
		hero.Set(Format.Number(fps?.Fps), "fps", Color(fps?.Fps, P));
		TextBlock block = low;
		double? num = fps?.Low1;
		object text;
		if (num.HasValue)
		{
			double valueOrDefault = num.GetValueOrDefault();
			text = Format.Number(valueOrDefault);
		}
		else
		{
			text = "—";
		}
		Motion.Text(block, (string)text);
		if (chart != null)
		{
			double[] array2 = h.Read("fps");
			chart.Max = Math.Max(60.0, array2.Where(double.IsFinite).DefaultIfEmpty(0.0).Max() * 1.15);
			chart.Set(array2, Color(fps?.Fps, P));
		}
		Card.Put(stats, "fps.frametime", Format.Milliseconds(fps?.FrameTimeMs));
	}
}
