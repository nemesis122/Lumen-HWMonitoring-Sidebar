using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Lumen.Core;

namespace Lumen.App;

/// <summary>
/// A living background behind the cards. "Ocean": the water level follows the CPU temperature, the waves, their speed and
/// the rising bubbles follow the load (CPU or graphics, whichever is higher), and the water turns from blue over turquoise
/// to orange as it gets hot. Every change glides, so the picture never jumps. Draws nothing the cards need; it only paints
/// under them and never takes clicks.
/// </summary>
public sealed class LiveBackdrop : Control
{
	private sealed class Bubble
	{
		public double X, Y, Speed, Radius;
	}

	public const int FramesPerSecond = 30;

	private readonly string kind;

	private readonly DispatcherTimer timer;

	private readonly Bubble[] bubbles;

	private readonly Random random = new Random(7);

	private DateTime lastTick = DateTime.MinValue;

	private double level = 0.3, levelTarget = 0.3, energy, energyTarget, heat, heatTarget, phase;

	public LiveBackdrop(string kind)
	{
		this.kind = kind;
		IsHitTestVisible = false;
		ClipToBounds = true;
		bubbles = Enumerable.Range(0, 60).Select(_ => new Bubble { X = random.NextDouble(), Y = random.NextDouble(), Speed = 0.5 + random.NextDouble(), Radius = 1.0 + random.NextDouble() * 2.6 }).ToArray();
		timer = new DispatcherTimer(TimeSpan.FromMilliseconds(1000.0 / FramesPerSecond), DispatcherPriority.Render, delegate
		{
			DateTime now = DateTime.UtcNow;
			double dt = lastTick == DateTime.MinValue ? 0.0 : (now - lastTick).TotalSeconds;
			lastTick = now;
			if (IsEffectivelyVisible) Advance(Math.Min(dt, 0.25));
		});
		AttachedToVisualTree += delegate { lastTick = DateTime.MinValue; timer.Start(); };
		DetachedFromVisualTree += delegate { timer.Stop(); };
	}

	public string Kind => kind;

	/// <summary>Current water level (0..1 of the height), for tests.</summary>
	public double Level => level;

	/// <summary>Current wave energy (0..1), for tests.</summary>
	public double Energy => energy;

	/// <summary>New readings: where the water and the waves are heading.</summary>
	public void Feed(Snapshot s)
	{
		double? temperature = s.Cpu?.Temperature ?? s.Gpus.Select(g => g.Temperature).Where(t => t.HasValue).DefaultIfEmpty().Max();
		double cpuLoad = s.Cpu?.Load ?? 0.0;
		double gpuLoad = s.Gpus.Select(g => g.Load ?? 0.0).DefaultIfEmpty(0.0).Max();
		heatTarget = temperature is double t && double.IsFinite(t) ? Math.Clamp((t - 35.0) / 45.0, 0.0, 1.0) : 0.2;
		double load = Math.Max(cpuLoad, gpuLoad);
		energyTarget = double.IsFinite(load) ? Math.Clamp(load / 100.0, 0.0, 1.0) : 0.0;
		levelTarget = 0.22 + 0.5 * heatTarget;
	}

	/// <summary>Moves the picture on by <paramref name="dt"/> seconds (the timer calls it; tests call it directly).</summary>
	public void Advance(double dt)
	{
		if (!(dt > 0.0)) { InvalidateVisual(); return; }
		double ease = Math.Min(1.0, dt * 1.4);
		level += (levelTarget - level) * ease;
		energy += (energyTarget - energy) * ease;
		heat += (heatTarget - heat) * ease;
		phase += dt * (0.8 + 3.2 * energy);
		double height = Math.Max(1.0, Bounds.Height);
		double surface = 1.0 - level;
		foreach (Bubble b in bubbles)
		{
			b.Y -= dt * b.Speed * (15.0 + 90.0 * energy) / height;
			if (b.Y < surface)
			{
				b.Y = 1.0 + random.NextDouble() * 0.05;
				b.X = random.NextDouble();
			}
		}
		InvalidateVisual();
	}

	/// <summary>Blue when cool, turquoise when warm, orange when hot.</summary>
	public static Color Water(double heat)
	{
		Color cool = Color.Parse("#1E6FDC"), warm = Color.Parse("#14DCE6"), hot = Color.Parse("#FF7A3D");
		heat = Math.Clamp(heat, 0.0, 1.0);
		return heat < 0.5 ? Palette.Mix(cool, warm, heat / 0.5) : Palette.Mix(warm, hot, (heat - 0.5) / 0.5);
	}

	public override void Render(DrawingContext context)
	{
		double w = Bounds.Width, h = Bounds.Height;
		if (w <= 0.0 || h <= 0.0) return;
		context.FillRectangle(new LinearGradientBrush
		{
			StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
			EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
			GradientStops = { new GradientStop(Color.Parse("#020610"), 0.0), new GradientStop(Color.Parse("#03101C"), 1.0) }
		}, new Rect(0, 0, w, h));
		Color water = Water(heat);
		double top = h - level * h;
		// A soft glow just under the surface.
		context.FillRectangle(new LinearGradientBrush
		{
			StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
			EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
			GradientStops = { new GradientStop(Color.FromArgb(0, water.R, water.G, water.B), 0.0), new GradientStop(Color.FromArgb((byte)(55 + 55 * energy), water.R, water.G, water.B), 0.35), new GradientStop(Color.FromArgb(0, water.R, water.G, water.B), 1.0) }
		}, new Rect(0, top - 60, w, 160));
		for (int layer = 0; layer < 3; layer++)
		{
			double amplitude = (5.0 + 16.0 * energy) * (1.0 - layer * 0.25);
			StreamGeometry wave = new StreamGeometry();
			using (StreamGeometryContext g = wave.Open())
			{
				g.BeginFigure(new Point(0, h), isFilled: true);
				for (double x = 0; x <= w + 6; x += 6)
				{
					double y = top + layer * 18 + Math.Sin(x / 38.0 + phase * (1.0 + layer * 0.3) + layer) * amplitude + Math.Sin(x / 17.0 - phase * 1.7) * amplitude * 0.3;
					g.LineTo(new Point(Math.Min(x, w), y));
				}
				g.LineTo(new Point(w, h));
				g.EndFigure(isClosed: true);
			}
			context.DrawGeometry(new SolidColorBrush(water, 0.14 + 0.06 * layer), layer == 0 ? new Pen(new SolidColorBrush(water, 0.55), 1.2) : null, wave);
		}
		int shown = (int)Math.Round(15 + 45 * energy);
		Pen ring = new Pen(new SolidColorBrush(water, 0.5), 1.0);
		for (int i = 0; i < shown && i < bubbles.Length; i++)
		{
			Bubble b = bubbles[i];
			double y = b.Y * h;
			if (y < top + 6) continue;
			context.DrawEllipse(null, ring, new Point(b.X * w + Math.Sin(y / 20.0) * 3.0, y), b.Radius, b.Radius);
		}
	}
}
