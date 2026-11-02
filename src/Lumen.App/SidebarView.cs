using Path = Avalonia.Controls.Shapes.Path;
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Lumen.Core;

namespace Lumen.App;

public sealed class SidebarView : Border
{
	private readonly Palette p;

	private readonly Settings settings;

	private readonly List<IWidget> widgets = new List<IWidget>();

	private readonly Border hint;

	private readonly TextBlock emptyNote;

	private readonly Ellipse statusDot;

	private readonly TextBlock statusText;

	private readonly List<StackPanel> lists = new List<StackPanel>();

	private readonly FitPanel fit;

	private readonly Grid barRows = new Grid();

	private int gpuCount = 1;

	private Metrics metrics;

	private bool compacted;

	private bool balanced;

	private int rebalanceTries;

	private Dictionary<WidgetKind, double>? measured;

	private readonly Dictionary<WidgetKind, List<Control>> builtCards = new Dictionary<WidgetKind, List<Control>>();

	private Snapshot? last;

	private History? lastHistory;

	private static int page = 1;

	private readonly WrapPanel tabs = new WrapPanel
	{
		Orientation = Orientation.Horizontal,
		Margin = new Thickness(12.0, 0.0, 0.0, 0.0),
		VerticalAlignment = VerticalAlignment.Center
	};

	public event Action? SettingsRequested;

	public event Action? ElevateRequested;

	public SidebarView(Settings settings, Palette palette)
	{
		SidebarView sidebarView = this;
		p = palette;
		this.settings = settings;
		this.metrics = Metrics.For(settings.Density);
		Ui.Modern = settings.Modern;
		Metrics metrics = this.metrics;
		base.Background = Palette.Brush(p.Surface);
		base.BorderBrush = Palette.Brush(p.Stroke);
		Edge edge = settings.Edge;
		base.BorderThickness = ((settings.FullScreen && !settings.Horizontal) ? new Thickness(0.0) : (edge switch
		{
			Edge.Right => new Thickness(1.0, 0.0, 0.0, 0.0), 
			Edge.Top => new Thickness(0.0, 0.0, 0.0, 1.0), 
			Edge.Bottom => new Thickness(0.0, 1.0, 0.0, 0.0), 
			_ => new Thickness(0.0, 0.0, 1.0, 0.0), 
		}));
		Path path = new Path
		{
			Data = StreamGeometry.Parse("M3.2,12.8 A6.5,6.5 0 1 1 12.8,12.8"),
			Stroke = new LinearGradientBrush
			{
				StartPoint = new RelativePoint(0.0, 1.0, RelativeUnit.Relative),
				EndPoint = new RelativePoint(1.0, 1.0, RelativeUnit.Relative),
				GradientStops = 
				{
					new GradientStop(p.Ramp[0], 0.0),
					new GradientStop(p.Ramp[1], 0.55),
					new GradientStop(p.Ramp[2], 1.0)
				}
			},
			StrokeThickness = 2.6,
			StrokeLineCap = PenLineCap.Round,
			Width = 16.0,
			Height = 16.0,
			Stretch = Stretch.Uniform,
			VerticalAlignment = VerticalAlignment.Center
		};
		TextBlock textBlock = Ui.Text("lumen", 15.0, p.Ink, FontWeight.DemiBold);
		textBlock.LetterSpacing = 0.2;
		statusDot = new Ellipse
		{
			Width = 6.0,
			Height = 6.0,
			VerticalAlignment = VerticalAlignment.Center
		};
		statusText = Ui.Text("", 10.5, p.Soft, FontWeight.DemiBold);
		Border border = new Border
		{
			Background = Palette.Brush(p.Track),
			CornerRadius = new CornerRadius(9.0),
			Padding = new Thickness(7.0, 2.0, 8.0, 2.0),
			Margin = new Thickness(10.0, 0.0, 0.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center,
			Child = Ui.Row(5.0, statusDot, statusText)
		};
		Button button = new Button
		{
			Content = Ui.Glyph("M8,5.5 A2.5,2.5 0 1 1 7.99,5.5 Z M8,1 V3 M8,13 V15 M1,8 H3 M13,8 H15 M3.05,3.05 L4.46,4.46 M11.54,11.54 L12.95,12.95 M3.05,12.95 L4.46,11.54 M11.54,4.46 L12.95,3.05", p.Soft),
			Background = Brushes.Transparent,
			BorderThickness = new Thickness(0.0),
			Padding = new Thickness(6.0),
			CornerRadius = new CornerRadius(8.0),
			Cursor = new Cursor(StandardCursorType.Hand),
			VerticalAlignment = VerticalAlignment.Center
		};
		ToolTip.SetTip(button, L.T("Settings"));
		button.Click += delegate
		{
			sidebarView.SettingsRequested?.Invoke();
		};
		bool flag = settings.Pages > 1 && settings.HasPageNames;
		Grid grid = (flag ? Ui.Columns("Auto,Auto,Auto,*,Auto", path, textBlock, border, new Control(), button) : Ui.Columns("Auto,Auto,Auto,*,Auto", path, textBlock, border, tabs, button));
		if (flag)
		{
			tabs.Margin = new Thickness(2.0, 0.0, 0.0, 0.0);
		}
		page = Math.Clamp(page, 1, settings.Pages);
		grid.PointerWheelChanged += delegate(object? _, PointerWheelEventArgs e)
		{
			if (settings.Pages >= 2)
			{
				sidebarView.Show(page + ((e.Delta.Y < 0.0) ? 1 : (-1)));
				e.Handled = true;
			}
		};
		textBlock.Margin = new Thickness(7.0, 0.0, 0.0, 1.0);
		TextBlock textBlock2 = Ui.Text(L.T("Temperatures need administrator rights and the PawnIO driver."), 11.5, p.Ink);
		textBlock2.TextWrapping = TextWrapping.Wrap;
		textBlock2.TextTrimming = TextTrimming.None;
		Button button2 = new Button
		{
			Content = L.T("Restart as administrator"),
			FontSize = 11.5,
			FontWeight = FontWeight.DemiBold,
			Padding = new Thickness(10.0, 4.0),
			CornerRadius = new CornerRadius(7.0),
			Margin = new Thickness(0.0, 8.0, 0.0, 0.0),
			Background = Palette.Brush(Palette.Alpha(p.Ramp[2], 0.28)),
			Foreground = Palette.Brush(p.Ink)
		};
		button2.Click += delegate
		{
			sidebarView.ElevateRequested?.Invoke();
		};
		Control control = Ui.Glyph("M8,1.5 L13.5,3.5 V7.5 C13.5,11 11,13.5 8,14.5 C5,13.5 2.5,11 2.5,7.5 V3.5 Z M8,5 V8.5 M8,10.5 V11", p.Ramp[2], 16.0);
		control.VerticalAlignment = VerticalAlignment.Top;
		StackPanel stackPanel = Ui.Stack(0.0, textBlock2, button2);
		stackPanel.Margin = new Thickness(10.0, 0.0, 0.0, 0.0);
		emptyNote = Ui.Text(L.T("Nothing on this register yet – choose its widgets in the settings under Widgets → Customise → Register."), 12.5, p.Soft);
		emptyNote.TextWrapping = TextWrapping.Wrap;
		emptyNote.TextTrimming = TextTrimming.None;
		emptyNote.Margin = new Thickness(6.0, 4.0, 6.0, 0.0);
		hint = new Border
		{
			Background = Palette.Brush(Palette.Alpha(p.Ramp[2], p.Dark ? 0.14 : 0.16)),
			BorderBrush = Palette.Brush(Palette.Alpha(p.Ramp[2], 0.35)),
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(metrics.Radius - 4.0),
			Padding = new Thickness(12.0, 10.0),
			IsVisible = false,
			Child = Ui.Columns("Auto,*", control, stackPanel)
		};
		for (int num = 0; num < settings.ColumnCount; num++)
		{
			lists.Add(new StackPanel());
		}
		bool flag2 = settings.Density == Density.Fit || settings.Horizontal;
		fit = new FitPanel(lists, settings.TextScale, flag2, metrics.Gap);
		BuildCards();
		Control item = (settings.Horizontal ? barRows : (flag2 ? ((Control)fit) : ((Control)new ScrollViewer
		{
			Content = fit,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
			VerticalScrollBarVisibility = ScrollBarVisibility.Hidden
		})));
		DockPanel dockPanel = new DockPanel
		{
			Margin = new Thickness(metrics.Gap, metrics.Gap + 2.0, metrics.Gap, metrics.Gap)
		};
		DockPanel.SetDock(grid, Dock.Top);
		grid.Margin = new Thickness(6.0, 0.0, 0.0, metrics.Gap + 2.0);
		dockPanel.Children.Add(grid);
		if (flag)
		{
			DockPanel.SetDock(tabs, Dock.Top);
			tabs.Margin = new Thickness(4.0, (0.0 - metrics.Gap) * 0.5, 0.0, metrics.Gap + 2.0);
			tabs.PointerWheelChanged += delegate(object? _, PointerWheelEventArgs e)
			{
				sidebarView.Show(page + ((e.Delta.Y < 0.0) ? 1 : (-1)));
				e.Handled = true;
			};
			dockPanel.Children.Add(tabs);
		}
		dockPanel.Children.Add(item);
		base.Child = dockPanel;
		if (flag2)
		{
			fit.Fitted += Squeezed;
		}
	}

	private void Squeezed(double scale)
	{
		if (scale >= 0.8 || compacted)
		{
			return;
		}
		compacted = true;
		Dispatcher.UIThread.Post(delegate
		{
			metrics = Metrics.For(Density.Compact);
			BuildCards();
			if (last != null)
			{
				Update(last, lastHistory);
			}
		});
	}

	public static double WindowWidth(Settings s)
	{
		int columnCount = s.ColumnCount;
		double num = Math.Max(1.0, s.TextScale);
		return (s.Width * (double)columnCount + (double)(columnCount - 1) * Metrics.For(s.Density).Gap) * num;
	}

	private void BuildCards()
	{
		Diagnostics.SetLayout(settings, page, $"build widgets; compacted={compacted}");
		widgets.Clear();
		builtCards.Clear();
		BuildTabs();
		if (settings.Horizontal)
		{
			BuildBar();
			return;
		}
		double value;
		List<List<WidgetSlot>> list = ((measured == null) ? settings.Layout(null, page) : settings.Layout((WidgetSlot w) => (!measured.TryGetValue(w.Kind, out value)) ? (Settings.Weight(w) * 150.0) : value, page));
		for (int num = 0; num < lists.Count; num++)
		{
			StackPanel stackPanel = lists[num];
			stackPanel.Children.Clear();
			stackPanel.Spacing = metrics.Gap;
			if (num == 0)
			{
				stackPanel.Children.Add(hint);
				(emptyNote.Parent as Panel)?.Children.Remove(emptyNote);
				stackPanel.Children.Add(emptyNote);
				emptyNote.IsVisible = list.All((List<WidgetSlot> col) => col.Count == 0);
			}
			List<(WidgetSlot, int)> list2 = new List<(WidgetSlot, int)>();
			foreach (WidgetSlot item3 in (num < list.Count) ? list[num] : new List<WidgetSlot>())
			{
				for (int num2 = 0; num2 < ((item3.Kind != WidgetKind.Gpu) ? 1 : gpuCount); num2++)
				{
					list2.Add((item3, num2));
				}
			}
			for (int num3 = 0; num3 < list2.Count; num3++)
			{
				(WidgetSlot, int) tuple = list2[num3];
				WidgetSlot item = tuple.Item1;
				int item2 = tuple.Item2;
				Control control = Make(item, item2);
				widgets.Add((IWidget)control);
				Remember(item.Kind, control);
				bool flag = Half(item);
				if (flag)
				{
					bool flag2 = ((control is RingTile || control is NumberTile || control is DualRingTile || control is BigNumberTile || control is DotsTile || control is BentoTile || control is MiniArcTile) ? true : false);
					flag = flag2;
				}
				if (flag && num3 + 1 < list2.Count && Half(list2[num3 + 1].Item1))
				{
					Control control2 = Make(list2[num3 + 1].Item1, list2[num3 + 1].Item2);
					widgets.Add((IWidget)control2);
					Remember(list2[num3 + 1].Item1.Kind, control2);
					Grid grid = new Grid
					{
						ColumnDefinitions = new ColumnDefinitions($"*,{metrics.Gap},*")
					};
					Grid.SetColumn(control2, 2);
					grid.Children.Add(control);
					grid.Children.Add(control2);
					stackPanel.Children.Add(grid);
					num3++;
				}
				else
				{
					stackPanel.Children.Add(control);
				}
			}
		}
		fit?.SetGap(metrics.Gap);
		static bool Half(WidgetSlot w)
		{
			return Settings.IsHalf(w);
		}
	}

	private void BuildBar()
	{
		foreach (FitPanel item4 in barRows.Children.OfType<FitPanel>())
		{
			item4.Fitted -= Squeezed;
		}
		barRows.Children.Clear();
		barRows.RowDefinitions.Clear();
		List<List<WidgetSlot>> list = settings.Layout(null, page, (WidgetSlot w) => (w.Kind != WidgetKind.Gpu) ? 1 : Math.Max(1, gpuCount));
		List<List<StackPanel>> list2 = new List<List<StackPanel>>();
		for (int num = 0; num < list.Count; num++)
		{
			List<(WidgetSlot, int)> list3 = new List<(WidgetSlot, int)>();
			foreach (WidgetSlot item5 in list[num])
			{
				for (int num2 = 0; num2 < ((item5.Kind != WidgetKind.Gpu) ? 1 : gpuCount); num2++)
				{
					list3.Add((item5, num2));
				}
			}
			List<StackPanel> list4 = new List<StackPanel>();
			if (num == 0)
			{
				(hint.Parent as Panel)?.Children.Remove(hint);
				(emptyNote.Parent as Panel)?.Children.Remove(emptyNote);
				emptyNote.IsVisible = list.All((List<WidgetSlot> row) => row.Count == 0);
				list4.Add(new StackPanel
				{
					Children = 
					{
						(Control)hint,
						(Control)emptyNote
					}
				});
			}
			for (int num3 = 0; num3 < list3.Count; num3++)
			{
				(WidgetSlot, int) tuple = list3[num3];
				WidgetSlot item = tuple.Item1;
				int item2 = tuple.Item2;
				Control control = Make(item, item2);
				widgets.Add((IWidget)control);
				Remember(item.Kind, control);
				Control item3 = control;
				bool flag = Settings.IsHalf(item);
				if (flag)
				{
					bool flag2 = ((control is RingTile || control is NumberTile || control is DualRingTile || control is BigNumberTile || control is DotsTile || control is BentoTile || control is MiniArcTile) ? true : false);
					flag = flag2;
				}
				if (flag && num3 + 1 < list3.Count && Settings.IsHalf(list3[num3 + 1].Item1))
				{
					Control control2 = Make(list3[num3 + 1].Item1, list3[num3 + 1].Item2);
					widgets.Add((IWidget)control2);
					Remember(list3[num3 + 1].Item1.Kind, control2);
					Grid obj = new Grid
					{
						ColumnDefinitions = new ColumnDefinitions($"*,{metrics.Gap},*")
					};
					Grid.SetColumn(control2, 2);
					obj.Children.Add(control);
					obj.Children.Add(control2);
					item3 = obj;
					num3++;
				}
				else
				{
					flag = Settings.IsHalf(item);
					if (flag)
					{
						bool flag2 = ((control is RingTile || control is NumberTile || control is DualRingTile || control is BigNumberTile || control is DotsTile || control is BentoTile || control is MiniArcTile) ? true : false);
						flag = flag2;
					}
					if (flag)
					{
						item3 = new Grid
						{
							ColumnDefinitions = new ColumnDefinitions($"*,{metrics.Gap},*"),
							Children = { control }
						};
					}
				}
				list4.Add(new StackPanel
				{
					Spacing = metrics.Gap,
					Children = { item3 }
				});
			}
			list2.Add(list4);
		}
		int num4 = list2.Select(Filled).DefaultIfEmpty(1).Max();
		list2 = list2.Where((List<StackPanel> c, int i) => i == 0 || Filled(c) > 0).ToList();
		for (int num5 = 0; num5 < list2.Count; num5++)
		{
			List<StackPanel> list5 = list2[num5];
			for (int num6 = Filled(list5); num6 < num4; num6++)
			{
				list5.Add(new StackPanel
				{
					Children = { (Control)new Border
					{
						Height = 1.0,
						Opacity = 0.0
					} }
				});
			}
			if (num5 > 0)
			{
				barRows.RowDefinitions.Add(new RowDefinition(metrics.Gap, GridUnitType.Pixel));
			}
			barRows.RowDefinitions.Add(new RowDefinition(1.0, GridUnitType.Star));
			FitPanel fitPanel = new FitPanel(list5, settings.TextScale, fill: true, metrics.Gap);
			fitPanel.Fitted += Squeezed;
			Grid.SetRow(fitPanel, num5 * 2);
			barRows.Children.Add(fitPanel);
		}
		int Filled(List<StackPanel> cells)
		{
			return cells.Count((StackPanel c) => c.Children.Any((Control x) => x != hint && x != emptyNote));
		}
	}

	private Control Make(WidgetSlot slot, int index)
	{
		Control control = Cards.Create(slot.Kind, slot.Style, index, p, metrics, settings);
		INamed obj = control as INamed;
		if (obj != null)
		{
			obj.Own(slot, index);
			return control;
		}
		return control;
	}

	private void BuildTabs()
	{
		tabs.Children.Clear();
		if (settings.Pages < 2)
		{
			return;
		}
		for (int i = 1; i <= settings.Pages; i++)
		{
			int n = i;
			bool flag = n == page;
			Button obj = new Button
			{
				Content = Ui.Text(settings.PageName(n), 11.5, flag ? p.Ink : p.Soft, flag ? FontWeight.Bold : FontWeight.Medium),
				Margin = new Thickness(0.0, 0.0, 2.0, 2.0)
			};
			IBrush background;
			if (!flag)
			{
				IBrush transparent = Brushes.Transparent;
				background = transparent;
			}
			else
			{
				background = Palette.Brush(p.Track);
			}
			obj.Background = background;
			obj.BorderThickness = new Thickness(0.0);
			obj.Padding = new Thickness(settings.HasPageNames ? 10 : 8, settings.HasPageNames ? 3 : 2);
			obj.MinWidth = 24.0;
			obj.CornerRadius = new CornerRadius(8.0);
			obj.Cursor = new Cursor(StandardCursorType.Hand);
			obj.HorizontalContentAlignment = HorizontalAlignment.Center;
			Button button = obj;
			button.Click += delegate
			{
				Show(n);
			};
			tabs.Children.Add(button);
		}
	}

	private void Show(int target)
	{
		target = Math.Clamp(target, 1, settings.Pages);
		if (target == page)
		{
			return;
		}
		page = target;
		balanced = false;
		rebalanceTries = 0;
		try
		{
			BuildCards();
			if (last != null)
			{
				Update(last, lastHistory);
			}
		}
		catch (Exception e)
		{
			CrashLog.Write(e, $"register {target}, {(settings.Horizontal ? "bar" : "side")}");
		}
	}

	private void Remember(WidgetKind kind, Control card)
	{
		if (!builtCards.TryGetValue(kind, out List<Control> value))
		{
			value = (builtCards[kind] = new List<Control>());
		}
		value.Add(card);
	}

	private void Rebalance()
	{
		Dictionary<WidgetKind, double> heights = builtCards.ToDictionary<KeyValuePair<WidgetKind, List<Control>>, WidgetKind, double>((KeyValuePair<WidgetKind, List<Control>> kv) => kv.Key, (KeyValuePair<WidgetKind, List<Control>> kv) => kv.Value.Sum((Control c) => fit.NaturalHeight(c) + metrics.Gap));
		if (heights.Values.Any((double v) => v <= metrics.Gap))
		{
			if (++rebalanceTries < 20)
			{
				Dispatcher.UIThread.Post(Rebalance, DispatcherPriority.Background);
			}
			return;
		}
		if (measured != null)
		{
			foreach (KeyValuePair<WidgetKind, double> item in measured)
			{
				heights.TryAdd(item.Key, item.Value);
			}
		}
		List<string> list = (from c in settings.Layout((measured == null) ? null : ((Func<WidgetSlot, double>)((WidgetSlot w) => measured.GetValueOrDefault(w.Kind, 150.0))), page)
			select string.Join(",", c.Select((WidgetSlot x) => x.Kind))).ToList();
		List<string> list2 = (from c in settings.Layout((WidgetSlot w) => heights.GetValueOrDefault(w.Kind, 150.0), page)
			select string.Join(",", c.Select((WidgetSlot x) => x.Kind))).ToList();
		if (Environment.GetEnvironmentVariable("LUMEN_DEBUG") == "1")
		{
			Console.WriteLine("rebalance " + string.Join(" ", heights.Select((KeyValuePair<WidgetKind, double> kv) => $"{kv.Key}={kv.Value:0}")) + " | " + string.Join(" / ", list) + " -> " + string.Join(" / ", list2));
		}
		measured = heights;
		if (list.SequenceEqual(list2))
		{
			return;
		}
		BuildCards();
		if (!(last != null))
		{
			return;
		}
		foreach (IWidget widget in widgets)
		{
			widget.Update(last, lastHistory);
		}
	}

	public void Update(Snapshot s, History h)
	{
		last = s;
		lastHistory = h;
		var (c, text) = s.Access switch
		{
			SensorAccess.Full => (p.Calm, L.T("LIVE")), 
			SensorAccess.Limited => (p.Ramp[2], L.T("LIMITED")), 
			_ => (p.Cool, L.T("DEMO")), 
		};
		statusDot.Fill = Palette.Brush(c);
		statusText.Text = text;
		hint.IsVisible = s.Access == SensorAccess.Limited;
		int num = Math.Max(1, GpuGroup.Visible(s, settings).Count);
		if (num != gpuCount)
		{
			gpuCount = num;
			BuildCards();
		}
		foreach (IWidget widget in widgets)
		{
			widget.Update(s, h);
		}
		if (settings.ColumnsInUse >= 2 && !settings.Horizontal && !balanced)
		{
			balanced = true;
			Dispatcher.UIThread.Post(Rebalance, DispatcherPriority.Background);
		}
	}
}


