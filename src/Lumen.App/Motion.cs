using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Lumen.App;

public static class Motion
{
	private sealed record Anim(long Start, double Ms, Action<double> Step);

	private static readonly Dictionary<object, Anim> active = new Dictionary<object, Anim>(ReferenceEqualityComparer.Instance);

	private static readonly Stopwatch clock = Stopwatch.StartNew();

	private static DispatcherTimer? timer;

	private static readonly Regex Number = new Regex("^(\\D*?)(-?\\d+(?:\\.(\\d+))?)(\\D*)$", RegexOptions.Compiled);

	public static bool Enabled { get; set; } = true;

	public static double Duration { get; set; } = 450.0;

	public static double ScrollDuration { get; set; } = 900.0;

	public static void Run(object key, double ms, Action<double> step)
	{
		if (!Enabled || ms <= 0.0)
		{
			active.Remove(key);
			step(1.0);
			return;
		}
		active[key] = new Anim(clock.ElapsedMilliseconds, ms, step);
		step(0.0);
		if (timer == null)
		{
			timer = new DispatcherTimer(DispatcherPriority.Render)
			{
				Interval = TimeSpan.FromMilliseconds(16.0)
			};
			timer.Tick += delegate
			{
				Tick();
			};
		}
		if (!timer.IsEnabled)
		{
			timer.Start();
		}
	}

	public static void Stop(object key)
	{
		active.Remove(key);
	}

	public static void Pump()
	{
		Tick();
	}

	private static void Tick()
	{
		long elapsedMilliseconds = clock.ElapsedMilliseconds;
		foreach (KeyValuePair<object, Anim> item in active.ToList())
		{
			item.Deconstruct(out var key, out var value);
			object key2 = key;
			Anim anim = value;
			double num = Math.Clamp((double)(elapsedMilliseconds - anim.Start) / anim.Ms, 0.0, 1.0);
			double obj = 1.0 - Math.Pow(1.0 - num, 3.0);
			try
			{
				anim.Step(obj);
			}
			catch
			{
				active.Remove(key2);
				continue;
			}
			if (num >= 1.0)
			{
				active.Remove(key2);
			}
		}
		if (active.Count == 0)
		{
			timer?.Stop();
		}
	}

	public static void Text(TextBlock block, string text)
	{
		string text2 = block.Text ?? "";
		if (text2 == text)
		{
			return;
		}
		Match match = Number.Match(text2);
		Match match2 = Number.Match(text);
		if (!Enabled || !match.Success || !match2.Success || match.Groups[1].Value != match2.Groups[1].Value || match.Groups[4].Value != match2.Groups[4].Value)
		{
			Stop(block);
			block.Text = text;
			return;
		}
		double from = double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
		double to = double.Parse(match2.Groups[2].Value, CultureInfo.InvariantCulture);
		int length = match2.Groups[3].Value.Length;
		string prefix = match2.Groups[1].Value;
		string suffix = match2.Groups[4].Value;
		string format = "F" + length;
		Run(block, Duration, delegate(double t)
		{
			block.Text = ((t >= 1.0) ? text : (prefix + (from + (to - from) * t).ToString(format, CultureInfo.InvariantCulture) + suffix));
		});
	}
}
