using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Lumen.Core;

namespace Lumen.App;

// Floating sidebar: the window covers the whole monitor and is transparent; the cards sit on it. Without a
// saved position the cards line up along the chosen screen edge (and wrap into a second column or row when
// they do not fit). With Ctrl held, a card can be dragged anywhere; its position is kept per monitor.
public sealed class FloatPanel : Panel
{
	private readonly Settings settings;

	private readonly double gap;

	private readonly Dictionary<Control, string> keys = new();

	private Control? dragged;

	private Point grab;

	private Point dragPosition;

	public event Action? PositionsChanged;

	public double CardWidth { get; set; } = 300;

	public bool Dragging => dragged != null;

	public FloatPanel(Settings settings, double gap)
	{
		this.settings = settings;
		this.gap = gap;
		Background = null;
		AddHandler(PointerPressedEvent, Pressed, RoutingStrategies.Tunnel);
		AddHandler(PointerMovedEvent, Moved, RoutingStrategies.Tunnel);
		AddHandler(PointerReleasedEvent, Released, RoutingStrategies.Tunnel);
		AddHandler(PointerCaptureLostEvent, (_, _) => dragged = null, RoutingStrategies.Bubble);
	}

	public void Add(Control card, string key)
	{
		keys[card] = key;
		Children.Add(card);
	}

	public void Register(Control card, string key) => keys[card] = key;

	public void ClearCards(params Control[] keep)
	{
		foreach (Control c in Children.ToList())
		{
			if (!keep.Contains(c))
			{
				Children.Remove(c);
				keys.Remove(c);
			}
		}
	}

	private string Store(string key) => settings.Screen + ":" + key;

	private Point? Saved(Control c)
	{
		if (c == dragged)
		{
			return dragPosition;
		}
		if (keys.TryGetValue(c, out string? key) && settings.FloatPositions.TryGetValue(Store(key), out double[]? xy) && xy.Length == 2)
		{
			return new Point(xy[0], xy[1]);
		}
		return null;
	}

	// The card under a point of the window, for letting clicks through the gaps.
	public bool IsOverCard(Point p) => Dragging || Children.Any(c => c.IsVisible && c.Bounds.Contains(p));

	protected override Size MeasureOverride(Size available)
	{
		foreach (Control c in Children)
		{
			c.Measure(new Size(CardWidth, double.PositiveInfinity));
		}
		return new Size(double.IsFinite(available.Width) ? available.Width : CardWidth, double.IsFinite(available.Height) ? available.Height : 900);
	}

	protected override Size ArrangeOverride(Size size)
	{
		double m = 12;
		Edge edge = settings.Edge;
		bool across = edge is Edge.Top or Edge.Bottom;
		// Flow of the cards without a saved position.
		double x = edge == Edge.Right ? size.Width - m - CardWidth : m;
		double y = edge == Edge.Bottom ? size.Height - m : m;
		double lane = 0;
		foreach (Control c in Children.Where(c => c.IsVisible))
		{
			Size want = new Size(CardWidth, c.DesiredSize.Height);
			if (Saved(c) is Point saved)
			{
				double px = Math.Clamp(saved.X, 0, Math.Max(0, size.Width - want.Width));
				double py = Math.Clamp(saved.Y, 0, Math.Max(0, size.Height - Math.Min(want.Height, size.Height)));
				c.Arrange(new Rect(new Point(px, py), want));
				continue;
			}
			if (!across)
			{
				if (y > m && y + want.Height > size.Height - m)
				{
					// Next column, further away from the edge.
					x += edge == Edge.Right ? -(CardWidth + gap) : CardWidth + gap;
					y = m;
				}
				c.Arrange(new Rect(new Point(x, y), want));
				y += want.Height + gap;
			}
			else
			{
				if (x > m && x + CardWidth > size.Width - m)
				{
					x = m;
					y += edge == Edge.Bottom ? -(lane + gap) : lane + gap;
					lane = 0;
				}
				double top = edge == Edge.Bottom ? y - want.Height : y;
				c.Arrange(new Rect(new Point(x, top), want));
				x += CardWidth + gap;
				lane = Math.Max(lane, want.Height);
			}
		}
		return size;
	}

	private void Pressed(object? sender, PointerPressedEventArgs e)
	{
		if ((e.KeyModifiers & KeyModifiers.Control) == 0 || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
		{
			return;
		}
		Point p = e.GetPosition(this);
		Control? card = Children.LastOrDefault(c => c.IsVisible && c.Bounds.Contains(p));
		if (card == null)
		{
			return;
		}
		dragged = card;
		grab = p - card.Bounds.Position;
		dragPosition = card.Bounds.Position;
		card.ZIndex = 10;
		card.Opacity = 0.85;
		e.Pointer.Capture(this);
		e.Handled = true;
	}

	private void Moved(object? sender, PointerEventArgs e)
	{
		if (dragged == null)
		{
			return;
		}
		dragPosition = e.GetPosition(this) - grab;
		InvalidateArrange();
		e.Handled = true;
	}

	private void Released(object? sender, PointerReleasedEventArgs e)
	{
		if (dragged == null)
		{
			return;
		}
		Control card = dragged;
		dragged = null;
		card.ZIndex = 0;
		card.Opacity = 1;
		e.Pointer.Capture(null);
		if (keys.TryGetValue(card, out string? key))
		{
			settings.FloatPositions[Store(key)] = new[] { Math.Round(dragPosition.X), Math.Round(dragPosition.Y) };
			PositionsChanged?.Invoke();
		}
		InvalidateArrange();
		e.Handled = true;
	}
}
