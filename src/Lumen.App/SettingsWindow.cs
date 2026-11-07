using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Lumen.Core;

namespace Lumen.App;

public sealed class SettingsWindow : Window
{
	private readonly Settings s;

	private readonly Action changed;

	private readonly Action weatherChanged;

	private readonly Func<IReadOnlyList<string>> screens;

	private readonly Platform platform;

	private readonly Palette p;

	private readonly StackPanel content = new StackPanel
	{
		Spacing = 22.0,
		Margin = new Thickness(28.0, 20.0, 32.0, 28.0)
	};

	private readonly StackPanel nav = new StackPanel
	{
		Spacing = 2.0,
		Margin = new Thickness(14.0, 4.0, 10.0, 14.0)
	};

	private readonly ScrollViewer scroller = new ScrollViewer();

	private static int pageIndex;

	// Which register the "Style for all widgets" buttons apply to: 0 = every register.
	private static int styleRegister;

	private readonly TextBlock title;

	private readonly TextBlock lead;

	private readonly Button reset;

	private readonly Button resetPage;

	private readonly Button done;

	private readonly TextBox search;

	private readonly HashSet<WidgetKind> expanded = new HashSet<WidgetKind>();

	private string weatherStatus = "";

	private bool confirmReset;

	private int building;

	private readonly Dictionary<int, List<string>> pageProps = new Dictionary<int, List<string>>();

	private readonly List<(int Page, string Label, string Help)> searchIndex = new List<(int, string, string)>();

	private static readonly Settings Defaults = Settings.Starter();


	private readonly Func<string>? report;

	private readonly Func<Snapshot?> snapshot;

	private WidgetSlot? dragging;

	private double dragStart;


	private string reportMessage = "";


	public static readonly string[] Zones = new string[22]
	{
		"America/Los_Angeles", "America/Denver", "America/Chicago", "America/New_York", "America/Toronto", "America/Mexico_City", "America/Sao_Paulo", "Europe/London", "Europe/Zurich", "Europe/Istanbul",
		"Europe/Moscow", "Africa/Johannesburg", "Asia/Dubai", "Asia/Kolkata", "Asia/Bangkok", "Asia/Singapore", "Asia/Hong_Kong", "Asia/Shanghai", "Asia/Seoul", "Asia/Tokyo",
		"Australia/Sydney", "Pacific/Auckland"
	};

	public SettingsWindow(Settings settings, Palette palette, Action onChanged, Func<IReadOnlyList<string>> screenNames, Platform platform, Action? onWeather = null, Func<string>? report = null, Func<Snapshot?>? snapshot = null)
	{
		this.report = report;
		this.snapshot = snapshot ?? ((Func<Snapshot>)(() => (Snapshot?)null));
		s = settings;
		p = palette;
		changed = onChanged;
		screens = screenNames;
		this.platform = platform;
		weatherChanged = onWeather ?? ((Action)delegate
		{
		});
		base.Width = 940.0;
		base.Height = 780.0;
		base.MinWidth = 760.0;
		base.MinHeight = 520.0;
		base.FontFamily = Ui.Inter;
		base.Background = Palette.Brush(p.Dark ? Color.Parse("#14181F") : Color.Parse("#F3F5F8"));
		base.RequestedThemeVariant = (p.Dark ? ThemeVariant.Dark : ThemeVariant.Light);
		base.WindowStartupLocation = WindowStartupLocation.CenterScreen;
		base.Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://Lumen/Assets/lumen.ico")));
		reset = new Button
		{
			Padding = new Thickness(14.0, 7.0),
			CornerRadius = new CornerRadius(8.0),
			Background = Brushes.Transparent,
			BorderThickness = new Thickness(1.0),
			BorderBrush = Palette.Brush(Color.Parse("#B3FF5D5D")),
			Foreground = Palette.Brush(p.Dark ? Color.Parse("#FF8A8A") : Color.Parse("#C62828"))
		};
		reset.Click += delegate
		{
			if (!confirmReset)
			{
				confirmReset = true;
				reset.Content = L.T("Sure? Click again");
				DispatcherTimer.RunOnce(delegate
				{
					confirmReset = false;
					reset.Content = L.T("Reset all …");
				}, TimeSpan.FromSeconds(4.0));
			}
			else
			{
				confirmReset = false;
				Reset();
			}
		};
		resetPage = new Button
		{
			Padding = new Thickness(14.0, 7.0),
			CornerRadius = new CornerRadius(8.0),
			Margin = new Thickness(10.0, 0.0, 0.0, 0.0)
		};
		resetPage.Click += delegate
		{
			ResetPage(pageIndex);
		};
		done = new Button
		{
			Padding = new Thickness(22.0, 7.0),
			CornerRadius = new CornerRadius(8.0),
			Classes = { "accent" }
		};
		done.Click += delegate
		{
			Close();
		};
		Border border = new Border
		{
			BorderBrush = Palette.Brush(p.Stroke),
			BorderThickness = new Thickness(0.0, 1.0, 0.0, 0.0),
			Padding = new Thickness(28.0, 14.0),
			Child = Ui.Columns("Auto,Auto,*,Auto", reset, resetPage, new Control(), done)
		};
		title = Ui.Text("", 22.0, p.Ink, FontWeight.DemiBold);
		lead = Ui.Text("", 12.5, p.Soft);
		search = new TextBox
		{
			CornerRadius = new CornerRadius(8.0),
			MinWidth = 0.0,
			Margin = new Thickness(0.0, 14.0, 0.0, 0.0),
			InnerLeftContent = Ui.Glyph("M7,2.5 A4.5,4.5 0 1 1 6.99,2.5 Z M10.3,10.3 L14,14", p.Soft, 13.0)
		};
		if (search.InnerLeftContent is Control control)
		{
			control.Margin = new Thickness(10.0, 0.0, 2.0, 0.0);
		}
		search.TextChanged += delegate
		{
			Build();
		};
		StackPanel stackPanel = Ui.Stack(2.0, title, lead, search);
		stackPanel.Margin = new Thickness(26.0, 24.0, 16.0, 12.0);
		DockPanel dockPanel = new DockPanel
		{
			Width = 220.0
		};
		DockPanel.SetDock(stackPanel, Dock.Top);
		dockPanel.Children.Add(stackPanel);
		dockPanel.Children.Add(new ScrollViewer
		{
			Content = nav
		});
		Border border2 = new Border
		{
			Background = Palette.Brush(p.Dark ? Color.Parse("#10141A") : Color.Parse("#E9EDF2")),
			BorderBrush = Palette.Brush(p.Stroke),
			BorderThickness = new Thickness(0.0, 0.0, 1.0, 0.0),
			Child = dockPanel
		};
		scroller.Content = content;
		DockPanel dockPanel2 = new DockPanel();
		DockPanel.SetDock(border2, Dock.Left);
		DockPanel.SetDock(border, Dock.Bottom);
		dockPanel2.Children.Add(border2);
		dockPanel2.Children.Add(border);
		dockPanel2.Children.Add(scroller);
		base.Content = dockPanel2;
		KeyDown += delegate(object? _, KeyEventArgs e)
		{
			if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Control))
			{
				search.Focus();
				search.SelectAll();
				e.Handled = true;
			}
			else if (e.Key == Key.Escape)
			{
				if ((search.Text ?? "").Length > 0)
				{
					search.Text = "";
				}
				else
				{
					Close();
				}
				e.Handled = true;
			}
		};
		Build();
	}

	private void Reset()
	{
		// "Reset everything" returns to the layout of the first start.
		Settings obj = Settings.Starter();
		foreach (PropertyInfo item in from x in typeof(Settings).GetProperties()
			where x.CanWrite
			select x)
		{
			item.SetValue(s, item.GetValue(obj));
		}
		changed();
		weatherChanged();
		Build();
	}

	private static PropertyInfo? Prop(string name)
	{
		return typeof(Settings).GetProperty(name);
	}

	private bool IsChanged(string name)
	{
		PropertyInfo propertyInfo = Prop(name);
		if ((object)propertyInfo != null)
		{
			return JsonSerializer.Serialize(propertyInfo.GetValue(s), Settings.Json) != JsonSerializer.Serialize(propertyInfo.GetValue(Defaults), Settings.Json);
		}
		return false;
	}

	private void Restore(IEnumerable<string> names)
	{
		Settings obj = Settings.Starter();
		foreach (string name in names)
		{
			PropertyInfo propertyInfo = Prop(name);
			if ((object)propertyInfo != null && propertyInfo.CanWrite)
			{
				propertyInfo.SetValue(s, propertyInfo.GetValue(obj));
			}
		}
		changed();
		weatherChanged();
		Build();
	}

	private void ResetPage(int page)
	{
		Restore(pageProps.TryGetValue(page, out List<string> value) ? value : new List<string>());
	}

	private void Owns(params string[] names)
	{
		if (!pageProps.TryGetValue(building, out List<string> value))
		{
			value = (pageProps[building] = new List<string>());
		}
		foreach (string item in names)
		{
			if (!value.Contains(item))
			{
				value.Add(item);
			}
		}
	}

	private Control? ChangedDot(string? prop)
	{
		if (prop == null)
		{
			return null;
		}
		Owns(prop);
		if (!IsChanged(prop))
		{
			return null;
		}
		Button obj = new Button
		{
			Width = 18.0,
			Height = 18.0,
			Padding = new Thickness(0.0),
			Background = Brushes.Transparent,
			BorderThickness = new Thickness(0.0),
			Margin = new Thickness(6.0, 0.0, 0.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center,
			Content = new Ellipse
			{
				Width = 8.0,
				Height = 8.0,
				Fill = Palette.Brush(p.Cool)
			},
			Cursor = new Cursor(StandardCursorType.Hand)
		};
		ToolTip.SetTip(obj, L.T("Changed – click to reset"));
		obj.Click += delegate
		{
			Restore(new string[1] { prop });
		};
		return obj;
	}

	private void Build()
	{
		string fontKey = Ui.FontKey;
		Ui.UseFont("inter");
		try
		{
			BuildPages();
		}
		finally
		{
			Ui.UseFont(fontKey);
		}
	}

	private void BuildPages()
	{
		base.Title = "Lumen – " + L.T("Settings");
		title.Text = L.T("Settings");
		lead.Text = L.T("Changes apply immediately.");
		search.Watermark = L.T("Search settings…");
		if (!confirmReset)
		{
			reset.Content = L.T("Reset all …");
		}
		resetPage.Content = L.T("Reset this page");
		done.Content = L.T("Done");
		content.Children.Clear();
		pageProps.Clear();
		searchIndex.Clear();
		List<(string, string, Control[])> list = new List<(string, string, Control[])>();
		building = 0;
		Owns("Widgets", "HiddenValues");
		list.Add((L.T("Widgets"), "M2,2.5 H6.5 V7 H2 Z M9.5,2.5 H14 V7 H9.5 Z M2,9 H6.5 V13.5 H2 Z M9.5,9 H14 V13.5 H9.5 Z", WidgetsPage()));
		building = 1;
		list.Add((L.T("Appearance"), "M8,5.5 A2.5,2.5 0 1 1 7.99,5.5 Z M8,1 V3 M8,13 V15 M1,8 H3 M13,8 H15 M3.05,3.05 L4.46,4.46 M11.54,11.54 L12.95,12.95 M3.05,12.95 L4.46,11.54 M11.54,4.46 L12.95,3.05", AppearancePage()));
		building = 2;
		list.Add((L.T("Placement"), "M2,2.5 H6.5 V13.5 H2 Z M9.5,2.5 H14 V13.5 H9.5 Z", PlacementPage()));
		building = 3;
		list.Add((L.T("Sensors"), "M5,3 H11 A2,2 0 0 1 13,5 V11 A2,2 0 0 1 11,13 H5 A2,2 0 0 1 3,11 V5 A2,2 0 0 1 5,3 Z M6.5,6.5 H9.5 V9.5 H6.5 Z M6,1 V3 M10,1 V3 M6,13 V15 M10,13 V15 M1,6 H3 M1,10 H3 M13,6 H15 M13,10 H15", new Control[1] { Section(L.T("Sensors"), Row(L.T("Sensor source"), Choice(new string[4]
		{
			L.T("Built-in"),
			L.T("Automatic"),
			"AIDA64",
			"HWiNFO"
		}, (int)s.SensorSource, delegate(int i)
		{
			s.SensorSource = (SensorSource)i;
		}, rebuild: true), SourceHelp(s.SensorSource), "SensorSource"), Row(L.T("Update interval"), Slide(0.5, 5.0, 0.5, (double)s.IntervalMs / 1000.0, delegate(double v)
		{
			s.IntervalMs = (int)Math.Round(v * 1000.0);
		}, (double v) => v.ToString("0.0", L.Culture) + " s"), null, "IntervalMs"), Row(L.T("Temperature"), Choice(new string[2]
		{
			L.T("Celsius (°C)"),
			L.T("Fahrenheit (°F)")
		}, s.Fahrenheit ? 1 : 0, delegate(int i)
		{
			s.Fahrenheit = i == 1;
		}, rebuild: true), null, "Fahrenheit"), Row(L.T("Warn from"), Slide(50.0, 110.0, 1.0, s.TemperatureWarning, delegate(double v)
		{
			s.TemperatureWarning = v;
		}, (double v) => $"{v:0} °C"), L.T("Above this the temperature turns red."), "TemperatureWarning"), Row(L.T("Warning pop-up"), Toggle(s.WarningPopup, delegate(bool v)
		{
			s.WarningPopup = v;
		}, notify: true, rebuild: true), L.T("Shows a notice when a temperature crosses the limit."), "WarningPopup"), Row(L.T("Integrated graphics"), Toggle(s.ShowIntegratedGpu, delegate(bool v)
		{
			s.ShowIntegratedGpu = v;
		}, notify: true, rebuild: true), L.T("Also show the graphics built into the processor."), "ShowIntegratedGpu")) }));
		building = 4;
		Owns("WeatherPlace", "WeatherLatitude", "WeatherLongitude", "WorldClocks");
		NumericUpDown price = new NumericUpDown
		{
			Value = (decimal)s.PricePerKwh,
			Minimum = 0m,
			Maximum = 5m,
			Increment = 0.01m,
			FormatString = "0.00",
			Width = 130.0,
			CornerRadius = new CornerRadius(8.0)
		};
		price.ValueChanged += delegate
		{
			s.PricePerKwh = (double)price.Value.GetValueOrDefault();
			changed();
		};
		TextBox currency = new TextBox
		{
			Text = s.Currency,
			Width = 70.0,
			CornerRadius = new CornerRadius(8.0),
			MaxLength = 5
		};
		currency.LostFocus += delegate
		{
			s.Currency = currency.Text ?? "CHF";
			changed();
		};
		TextBox target = new TextBox
		{
			Text = s.PingTarget,
			Width = 170.0,
			CornerRadius = new CornerRadius(8.0)
		};
		target.LostFocus += delegate
		{
			s.PingTarget = target.Text ?? "1.1.1.1";
			changed();
		};
		Index(L.T("Place"), L.T("Weather"));
		Index(L.T("World clocks"), L.T("Up to four cities."));
		list.Add((L.T("Extras"), "M4.5,12.5 H11.5 A3,3 0 0 0 11.7,6.5 A4,4 0 0 0 4.1,7.6 A2.5,2.5 0 0 0 4.5,12.5 Z", new Control[4]
		{
			Section(L.T("Weather"), WeatherEditor()),
			Section(L.T("World clocks"), WorldClockPicker()),
			Section(L.T("Power and cost"), Row(L.T("Price per kWh"), Ui.Row(8.0, price, currency), null, "PricePerKwh"), Row(L.T("Rest of the PC"), Slide(0.0, 300.0, 5.0, s.BaseWatts, delegate(double v)
			{
				s.BaseWatts = v;
			}, (double v) => $"{v:0} W"), L.T("Screen, board, drives and fans are not measured; this estimate is added."), "BaseWatts")),
			Section("Ping", Row(L.T("Target"), target, L.T("A server on the internet, e.g. 1.1.1.1 or google.com."), "PingTarget"))
		}));
		Owns("Currency");
		building = 5;
		List<string> list2 = new List<string> { L.T("Automatic (Windows)") };
		list2.AddRange(L.Choices.Select(((string Code, string Name) c) => c.Name));
		int selected = ((!(s.Language == "auto")) ? (Array.FindIndex(L.Choices, ((string Code, string Name) c) => c.Code == s.Language) + 1) : 0);
		bool isAdministrator = platform.IsAdministrator;
		TextBlock textBlock = Ui.Text(isAdministrator ? L.T("Running with administrator rights – all sensors are reachable.") : L.T("Without administrator rights the CPU temperatures are missing."), 12.0, isAdministrator ? p.Calm : p.Soft);
		textBlock.TextWrapping = TextWrapping.Wrap;
		textBlock.TextTrimming = TextTrimming.None;
		Button button = new Button
		{
			Content = L.T("Restart as administrator"),
			Padding = new Thickness(12.0, 6.0),
			CornerRadius = new CornerRadius(8.0),
			IsVisible = !isAdministrator,
			Margin = new Thickness(0.0, 8.0, 0.0, 0.0)
		};
		button.Click += delegate
		{
			platform.RestartElevated();
		};
		Control editor = Toggle(platform.AutostartEnabled, delegate(bool v)
		{
			if (!platform.SetAutostart(v))
			{
				Build();
			}
		}, notify: false);
		Button button2 = new Button
		{
			Content = L.T("Show all sensors"),
			Padding = new Thickness(12.0, 6.0),
			CornerRadius = new CornerRadius(8.0)
		};
		button2.Click += delegate
		{
			new SensorsWindow(p, report ?? ((Func<string>)(() => "")), System.IO.Path.GetDirectoryName(platform.SettingsPath) ?? ".").Show();
		};
		Button button3 = new Button
		{
			Content = L.T("Create error report"),
			Padding = new Thickness(12.0, 6.0),
			CornerRadius = new CornerRadius(8.0)
		};
		button3.Click += delegate
		{
			try
			{
				string path = ErrorReport.Create(s, report?.Invoke() ?? "", screens());
				reportMessage = L.F("Saved on the desktop: {0}", System.IO.Path.GetFileName(path));
				ErrorReport.Reveal(path);
			}
			catch (Exception ex)
			{
				CrashLog.Write(ex, "create error report");
				reportMessage = L.T("The file could not be read or written.") + " " + ex.Message;
			}
			Build();
		};
		list.Add((L.T("System"), "M8,1.5 L13.5,3.5 V7.5 C13.5,11 11,13.5 8,14.5 C5,13.5 2.5,11 2.5,7.5 V3.5 Z M8,5 V8.5 M8,10.5 V11", new Control[2]
		{
			Section(L.T("System"), Row(L.T("Language"), Choice(list2, selected, delegate(int i)
			{
				s.Language = ((i == 0) ? "auto" : L.Choices[i - 1].Code);
			}, rebuild: true), null, "Language"), Row(L.T("Start with Windows"), editor, L.T("Starts with administrator rights, without a prompt.")), Ui.Stack(0.0, textBlock, button), ExecutableLocation.Create(p, true), Row(L.T("Sensors"), button2, L.T("Lists everything LibreHardwareMonitor and Open Hardware Monitor found.")), Row(L.T("Error report"), button3, (reportMessage.Length > 0) ? reportMessage : L.T("One ZIP file with error log, sensor lists and settings – without notes, places or own names. Nothing is sent."))),
			Ui.Text("Lumen " + typeof(SettingsWindow).Assembly.GetName().Version?.ToString(3) + " · " + L.F("Settings stored in {0}", System.IO.Path.GetFileName(platform.SettingsPath)), 11.0, p.Faint)
		}));
		pageIndex = Math.Clamp(pageIndex, 0, list.Count - 1);
		string text = (search.Text ?? "").Trim();
		nav.Children.Clear();
		for (int num = 0; num < list.Count; num++)
		{
			int n = num;
			bool flag = n == pageIndex && text.Length == 0;
			TextBlock item = Ui.Text(list[n].Item1, 13.5, flag ? p.Ink : p.Soft, flag ? FontWeight.DemiBold : FontWeight.Medium);
			DockPanel dockPanel = new DockPanel();
			Control control = Ui.Glyph(list[n].Item2, flag ? p.Cool : p.Soft);
			control.Margin = new Thickness(0.0, 0.0, 11.0, 0.0);
			DockPanel.SetDock(control, Dock.Left);
			dockPanel.Children.Add(control);
			Control control2 = NavBadge(n);
			if (control2 != null)
			{
				DockPanel.SetDock(control2, Dock.Right);
				dockPanel.Children.Add(control2);
			}
			dockPanel.Children.Add(item);
			Button obj = new Button
			{
				Content = dockPanel
			};
			IBrush background;
			if (!flag)
			{
				IBrush transparent = Brushes.Transparent;
				background = transparent;
			}
			else
			{
				background = Palette.Brush(p.Dark ? Color.Parse("#222A35") : Colors.White);
			}
			obj.Background = background;
			obj.BorderThickness = new Thickness(0.0);
			obj.HorizontalAlignment = HorizontalAlignment.Stretch;
			obj.HorizontalContentAlignment = HorizontalAlignment.Stretch;
			obj.Padding = new Thickness(12.0, 9.0);
			obj.CornerRadius = new CornerRadius(8.0);
			obj.Cursor = new Cursor(StandardCursorType.Hand);
			Button button4 = obj;
			button4.Click += delegate
			{
				pageIndex = n;
				scroller.Offset = default(Vector);
				if ((search.Text ?? "").Length > 0)
				{
					search.Text = "";
				}
				else
				{
					Build();
				}
			};
			nav.Children.Add(button4);
		}
		resetPage.IsEnabled = text.Length == 0 && pageProps.TryGetValue(pageIndex, out List<string> value) && value.Any(IsChanged);
		if (text.Length > 0)
		{
			content.Children.Add(SearchResults(text, list.Select<(string, string, Control[]), string>(((string Name, string Glyph, Control[] Body) x) => x.Name).ToList()));
			return;
		}
		content.Children.Add(PageHeading(list[pageIndex].Item1, pageIndex));
		Control[] item2 = list[pageIndex].Item3;
		foreach (Control item3 in item2)
		{
			content.Children.Add(item3);
		}
	}

	private static readonly string[] PageLeads =
	{
		"Choose what the sidebar shows, in which design and in which order.",
		"Colours, effects, text and themes.",
		"Screen edge, monitor, columns and registers.",
		"Where the readings come from and how often they update.",
		"Weather, world clocks, power costs and ping.",
		"Language, autostart, warnings and diagnostics."
	};

	// Large title of the open page, so it is always clear where one is.
	private Control PageHeading(string name, int page)
	{
		TextBlock heading = Ui.Text(name, 24.0, p.Ink, FontWeight.Bold);
		TextBlock sub = Ui.Text(page < PageLeads.Length ? L.T(PageLeads[page]) : "", 12.5, p.Soft);
		sub.TextWrapping = TextWrapping.Wrap;
		sub.TextTrimming = TextTrimming.None;
		TextBlock keys = Ui.Text(L.T("Ctrl+F searches, Esc closes."), 11.0, p.Faint);
		keys.VerticalAlignment = VerticalAlignment.Bottom;
		Grid grid = Ui.Columns("*,Auto", Ui.Stack(2.0, heading, sub), keys);
		grid.Margin = new Thickness(0.0, 0.0, 0.0, 4.0);
		return grid;
	}

	private Control? NavBadge(int page)
	{
		if (page == 3 && s.SensorSource != SensorSource.Builtin)
		{
			string status = ExternalSensors.Status;
			bool num = status.StartsWith("connected:");
			string text = ((!num) ? "—" : (status.EndsWith("Aida64") ? "AIDA64" : "HWiNFO"));
			Color color = (num ? p.Calm : p.Ramp[2]);
			return Ui.Row(5.0, new Ellipse
			{
				Width = 7.0,
				Height = 7.0,
				Fill = Palette.Brush(color),
				VerticalAlignment = VerticalAlignment.Center
			}, Ui.Text(text, 11.0, color, FontWeight.Medium));
		}
		if (pageProps.TryGetValue(page, out List<string> value) && value.Any(IsChanged))
		{
			Ellipse obj = new Ellipse
			{
				Width = 7.0,
				Height = 7.0,
				Fill = Palette.Brush(p.Cool),
				VerticalAlignment = VerticalAlignment.Center
			};
			ToolTip.SetTip(obj, L.T("Changed from the default"));
			return obj;
		}
		return null;
	}

	private void Index(string label, string? help)
	{
		searchIndex.Add((building, label, help ?? ""));
	}

	private Control SearchResults(string query, IReadOnlyList<string> pageNames)
	{
		List<(int, string, string)> list = (from x in searchIndex
			where x.Label.Contains(query, StringComparison.CurrentCultureIgnoreCase) || x.Help.Contains(query, StringComparison.CurrentCultureIgnoreCase)
			group x by (Page: x.Page, Label: x.Label) into g
			select g.First()).ToList();
		StackPanel stackPanel = new StackPanel
		{
			Spacing = 6.0
		};
		foreach (var item in list)
		{
			Button button = new Button();
			button.HorizontalAlignment = HorizontalAlignment.Stretch;
			button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
			button.Padding = new Thickness(14.0, 10.0);
			button.CornerRadius = new CornerRadius(10.0);
			button.Background = Palette.Brush(p.Dark ? Color.Parse("#191E26") : Colors.White);
			button.BorderBrush = Palette.Brush(p.Stroke);
			button.BorderThickness = new Thickness(1.0);
			button.Content = Ui.Columns("*,Auto", Ui.Stack(2.0, Ui.Text(item.Item2, 13.5, p.Ink, FontWeight.Medium), Ui.Text(item.Item3, 11.5, p.Soft)), Ui.Text(pageNames[item.Item1] + "  ›", 12.0, p.Cool, FontWeight.Medium));
			button.Cursor = new Cursor(StandardCursorType.Hand);
			Button button2 = button;
			var (page, _, _) = item;
			button2.Click += delegate
			{
				pageIndex = page;
				scroller.Offset = default(Vector);
				search.Text = "";
			};
			stackPanel.Children.Add(button2);
		}
		if (list.Count == 0)
		{
			stackPanel.Children.Add(Ui.Text(L.T("No setting found."), 13.0, p.Soft));
		}
		return Ui.Stack(10.0, SectionTitle(L.F("Results for “{0}”", query)), stackPanel);
	}

	private Control[] WidgetsPage()
	{
		TextBlock textBlock = Ui.Text(L.T("Tick what the sidebar shows; drag by the handle or use the arrows to sort. “Customise” opens style, column, register and single values."), 12.0, p.Soft);
		textBlock.TextWrapping = TextWrapping.Wrap;
		textBlock.TextTrimming = TextTrimming.None;
		Index(L.T("Style for all widgets"), L.T("One click sets every widget with styles; each one can still be changed below."));
		UniformGrid uniformGrid = new UniformGrid
		{
			Columns = 6
		};
		if (styleRegister > s.Pages)
		{
			styleRegister = 0;
		}
		// The widgets the buttons set: all of them, or only those of the chosen register (shown or not).
		bool Target(WidgetSlot w) => Settings.HasStyles(w.Kind) && (styleRegister == 0 || s.EffectivePage(w) == styleRegister);
		foreach (WidgetStyle style in Settings.Selectable)
		{
			List<WidgetSlot> list = s.Widgets.Where(Target).ToList();
			bool flag = list.Count > 0 && list.All((WidgetSlot w) => w.Style == style);
			Button button = new Button();
			button.Content = Ui.Stack(6.0, StyleThumb(style), Ui.Text(StyleName(style), 12.0, flag ? p.Ink : p.Soft, flag ? FontWeight.DemiBold : FontWeight.Medium));
			button.HorizontalAlignment = HorizontalAlignment.Stretch;
			button.HorizontalContentAlignment = HorizontalAlignment.Center;
			button.Padding = new Thickness(6.0, 10.0, 6.0, 8.0);
			button.Margin = new Thickness(0.0, 0.0, 8.0, 8.0);
			button.CornerRadius = new CornerRadius(10.0);
			button.Background = Palette.Brush(p.Dark ? Color.Parse("#191E26") : Colors.White);
			button.BorderBrush = Palette.Brush(flag ? p.Cool : p.Stroke);
			button.BorderThickness = new Thickness((!flag) ? 1 : 2);
			button.Cursor = new Cursor(StandardCursorType.Hand);
			ToolTip.SetTip(button, StyleName(style));
			if (button.Content is StackPanel stackPanel)
			{
				foreach (Control child in stackPanel.Children)
				{
					child.HorizontalAlignment = HorizontalAlignment.Center;
				}
			}
			button.Click += delegate
			{
				foreach (WidgetSlot item in s.Widgets.Where(Target))
				{
					item.Style = style;
				}
				changed();
				Build();
			};
			uniformGrid.Children.Add(button);
		}
		List<WidgetSlot> list4 = s.Widgets.Where((WidgetSlot w) => !w.Visible).ToList();
		foreach (WidgetSlot widget in s.Widgets)
		{
			Index(Cards.Name(widget.Kind), L.T("Widgets"));
		}
		List<Control> list5 = new List<Control>();
		list5.Add(textBlock);
		if (s.Pages > 1)
		{
			// A design per register: choose the register, then click the design.
			Index(L.T("Style per register"), L.T("Sets the design only for the widgets of the chosen register."));
			var registers = new List<string> { L.T("All registers") };
			registers.AddRange(Enumerable.Range(1, s.Pages).Select(RegisterLabel));
			var pick = Choice(registers, styleRegister, delegate(int i)
			{
				styleRegister = i;
			}, rebuild: true);
			var note = Ui.Text(L.T("Sets the design only for the widgets of the chosen register."), 12.0, p.Soft);
			note.TextWrapping = TextWrapping.Wrap;
			note.IsVisible = styleRegister > 0;
			string title = styleRegister > 0 ? L.T("Style per register") + " · " + RegisterLabel(styleRegister) : L.T("Style for all widgets");
			list5.Add(Ui.Stack(10.0, SectionTitle(title), Ui.Row(8.0, Ui.Text(L.T("Register"), 12.5, p.Soft), pick), note, uniformGrid));
		}
		else
		{
			list5.Add(Ui.Stack(10.0, SectionTitle(L.T("Style for all widgets")), uniformGrid));
		}
		List<Control> list6 = list5;
		for (int register = 1; register <= s.Pages; register++)
		{
			List<WidgetSlot> slots = s.WidgetsOnPage(register).ToList();
			string heading = s.Pages > 1 ? RegisterLabel(register) : L.T("Widgets");
			list6.Add(Group(heading, L.F("{0} shown", slots.Count), slots));
		}
		if (list4.Count > 0)
		{
			WrapPanel wrapPanel = new WrapPanel();
			foreach (WidgetSlot slot in list4)
			{
				CheckBox c = new CheckBox
				{
					Name = "widget-" + slot.Kind,
					Content = Cards.Name(slot.Kind),
					IsChecked = false,
					FontSize = 13.0
				};
				c.IsCheckedChanged += delegate
				{
					slot.Visible = c.IsChecked == true;
					changed();
					Dispatcher.UIThread.Post(Build);
				};
				wrapPanel.Children.Add(new Border
				{
					Child = c,
					Padding = new Thickness(10.0, 0.0, 12.0, 0.0),
					Margin = new Thickness(0.0, 0.0, 8.0, 8.0),
					CornerRadius = new CornerRadius(9.0),
					Background = Palette.Brush(p.Dark ? Color.Parse("#151A21") : Color.Parse("#F7F9FB")),
					BorderBrush = Palette.Brush(p.Dark ? Color.Parse("#2E3644") : Color.Parse("#CDD5DF")),
					BorderThickness = new Thickness(1.0)
				});
			}
			list6.Add(Ui.Stack(10.0, Ui.Columns("*,Auto", SectionTitle(L.T("Hidden")), Ui.Text(L.T("Tick to show"), 12.0, p.Soft)), wrapPanel));
		}
		return list6.ToArray();
	}

	private Control Group(string name, string count, List<WidgetSlot> slots)
	{
		StackPanel stackPanel = new StackPanel
		{
			Spacing = 6.0
		};
		for (int i = 0; i < slots.Count; i++)
		{
			stackPanel.Children.Add(WidgetRow(slots, i, stackPanel));
		}
		if (slots.Count == 0)
		{
			var hint = Ui.Text(L.T("Nothing on this register yet – choose its widgets in the settings under Widgets → Customise → Register."), 12.0, p.Soft);
			hint.TextWrapping = TextWrapping.Wrap;
			stackPanel.Children.Add(hint);
		}
		return Ui.Stack(10.0, Ui.Columns("*,Auto", SectionTitle(name), Ui.Text(count, 12.0, p.Soft)), stackPanel);
	}

	private string RegisterLabel(int page) => s.PageName(page) == page.ToString()
		? L.F("Register {0}", page)
		: L.F("Register {0}: {1}", page, s.PageName(page));

	// Ten swatches plus the colour of the widget kind; only the Evolution design uses it.
	private Control AccentRow(WidgetSlot slot)
	{
		WrapPanel swatches = new WrapPanel();
		void Add(string hex, string tip)
		{
			bool chosen = slot.Accent == hex;
			Color color = Accents.Readable(Color.Parse(hex.Length > 0 ? hex : Accents.Default(slot.Kind)), p);
			Button b = new Button
			{
				Name = "accent-" + (hex.Length > 0 ? hex.TrimStart('#') : "default"),
				Width = 26.0,
				Height = 26.0,
				Padding = new Thickness(0),
				Margin = new Thickness(0, 0, 6, 6),
				CornerRadius = new CornerRadius(13),
				Background = Palette.Brush(color),
				BorderBrush = Palette.Brush(chosen ? p.Ink : Colors.Transparent),
				BorderThickness = new Thickness(chosen ? 2.5 : 0),
				Content = hex.Length == 0 ? Ui.Text("A", 11, Readings.TextOn(color, p), FontWeight.Bold) : null,
				HorizontalContentAlignment = HorizontalAlignment.Center,
				VerticalContentAlignment = VerticalAlignment.Center
			};
			ToolTip.SetTip(b, tip);
			b.Click += delegate
			{
				slot.Accent = hex;
				changed();
				Build();
			};
			swatches.Children.Add(b);
		}
		Add("", L.T("Automatic"));
		foreach (var (hex, name) in Accents.Swatches)
		{
			Add(hex, L.T(name));
		}
		Grid row = Ui.Columns("Auto,*", Ui.Text(L.T("Accent colour"), 12.5, p.Soft), swatches);
		row.Children[0].Margin = new Thickness(0, 5, 10, 0);
		row.Children[0].VerticalAlignment = VerticalAlignment.Top;
		row.Margin = new Thickness(28, 4, 0, 0);
		return row;
	}

	private TextBlock SectionTitle(string text)
	{
		TextBlock textBlock = Ui.Text(text.ToUpper(L.Culture), 11.0, p.Soft, FontWeight.DemiBold);
		textBlock.LetterSpacing = 1.0;
		textBlock.VerticalAlignment = VerticalAlignment.Bottom;
		return textBlock;
	}

	// Readable English names for the designs whose translation key is their code name.
	private static string English(WidgetStyle style) => style switch
	{
		WidgetStyle.Pill => "Capsule",
		WidgetStyle.FillBars => "Fill bars",
		WidgetStyle.PillRow => "Capsule row",
		WidgetStyle.KeyFigures => "Key figures",
		WidgetStyle.BadgeList => "List",
		WidgetStyle.TwinFocus => "Twin Focus",
		WidgetStyle.SignalRows => "Signal rows",
		WidgetStyle.OpenPanel => "Open panel",
		WidgetStyle.ControlDeck => "Control deck",
		WidgetStyle.ClarityColor => "Clarity colour",
		WidgetStyle.ClarityRows => "Clarity rows",
		WidgetStyle.ClarityTiles => "Clarity tiles",
		_ => style.ToString()
	};

	public static string StyleName(WidgetStyle style)
	{
		return style switch
		{
			WidgetStyle.HistoryGraph => L.T("History graph"),
			WidgetStyle.RingPair => L.T("Double ring"),
			WidgetStyle.LevelColumns => L.T("Level columns"),
			WidgetStyle.NeonHud => "Neon HUD",
			WidgetStyle.NeonTiles => L.T("Neon tiles"),
			WidgetStyle.Thermostat => L.T("Thermostat"),
			WidgetStyle.SmartPanel => "Smart Panel",
			WidgetStyle.PurpleDash => L.T("Purple dashboard"),
			WidgetStyle.PurpleList => L.T("Purple list"),
			WidgetStyle.SegmentGraph => L.T("Segment graph"),
			WidgetStyle.TableGraph => L.T("Table graph"),
			WidgetStyle.FillBarsGraph => L.T("Fill bars graph"),
			WidgetStyle.PillRowGraph => L.T("Capsule row graph"),
			WidgetStyle.BadgeListGraph => L.T("List graph"),
			WidgetStyle.NeonHudGraph => L.T("Neon HUD graph"),
			WidgetStyle.SciFi => "Sci-Fi",
			_ => L.T(style.ToString()) is var name && name != style.ToString() ? name : English(style)
		};
	}

	private Control StyleThumb(WidgetStyle style)
	{
		Canvas c = new Canvas
		{
			Width = 64.0,
			Height = 34.0
		};
		IBrush brush = Palette.Brush(p.Cool);
		IBrush brush2 = Palette.Brush(p.Calm);
		IBrush brush3 = Palette.Brush(p.Track);
		IBrush fill = Palette.Brush(p.Dark ? Color.Parse("#262E3B") : Color.Parse("#E4E9F0"));
		IBrush fill2 = Palette.Brush(p.Soft);
		switch (style)
		{
        case WidgetStyle.HistoryGraph:
        {
            var a=Palette.Brush(Accents.Readable(Color.Parse("#22d3ee"),p));
            Rect(2,2,62,22,fill2,3);Rect(5,5,14,6,fill,1.5);
            var line=new Avalonia.Controls.Shapes.Polyline{Points={new Point(3,22),new Point(12,17),new Point(20,19),new Point(30,10),new Point(40,14),new Point(50,7),new Point(63,12)},Stroke=a,StrokeThickness=1.6};
            c.Children.Add(line);
            Rect(2,28,18,3,fill,1);Rect(24,28,18,3,fill,1);Rect(46,28,18,3,fill,1);
            break;
        }
        case WidgetStyle.RingPair:
        {
            var a=Palette.Brush(Accents.Readable(Color.Parse("#34d399"),p));var b=Palette.Brush(Accents.Readable(Color.Parse("#a78bfa"),p));
            foreach(var (sz,off,br,sw) in new[]{(26.0,2.0,a,200.0),(16.0,7.0,b,120.0)}){var t=new Avalonia.Controls.Shapes.Arc{Width=sz,Height=sz,StartAngle=135,SweepAngle=270,Stroke=brush3,StrokeThickness=3,StrokeLineCap=PenLineCap.Round};var v=new Avalonia.Controls.Shapes.Arc{Width=sz,Height=sz,StartAngle=135,SweepAngle=sw,Stroke=br,StrokeThickness=3,StrokeLineCap=PenLineCap.Round};Canvas.SetLeft(t,off+2);Canvas.SetTop(t,off+4);Canvas.SetLeft(v,off+2);Canvas.SetTop(v,off+4);c.Children.Add(t);c.Children.Add(v);}
            Rect(36,8,26,3,fill,1);Rect(36,15,22,3,fill,1);Rect(36,22,26,3,fill,1);
            break;
        }
        case WidgetStyle.LevelColumns:
        {
            var a=Palette.Brush(Accents.Readable(Color.Parse("#60a5fa"),p));
            double[] hs={18,10,24,14,20};
            for(int i=0;i<5;i++){Rect(6+i*12,6,6,26,fill2,2);Rect(6+i*12,32-hs[i],6,hs[i],a,2);}
            break;
        }
        case WidgetStyle.NeonHud:
        {
            var cy=Palette.Brush(Accents.Readable(Color.Parse("#22d3ee"),p));var mg=Palette.Brush(Accents.Readable(Color.Parse("#ec4899"),p));
            Rect(3,3,26,3,cy,1);Rect(50,2,14,6,fill2,2);
            for(int i=0;i<3;i++){Rect(3,12+i*8,18,2,fill,1);Rect(54,12+i*8,10,2,i==1?mg:cy,1);Rect(3,16+i*8,61,2,fill2,1);Rect(3,16+i*8,20+i*12,2,i==1?mg:cy,1);}
            break;
        }
        case WidgetStyle.NeonTiles:
        {
            var cy=Palette.Brush(Accents.Readable(Color.Parse("#22d3ee"),p));var mg=Palette.Brush(Accents.Readable(Color.Parse("#ec4899"),p));var am=Palette.Brush(Accents.Readable(Color.Parse("#f59e0b"),p));
            Rect(3,3,3,3,cy,1.5);Rect(8,3,22,3,cy,1);Rect(3,9,40,5,fill,1.5);
            Rect(3,18,19,15,fill2,3);Rect(24,18,19,15,fill2,3);Rect(45,18,19,15,fill2,3);
            Rect(7,21,11,2,cy,1);Rect(28,21,11,2,mg,1);Rect(49,21,11,2,am,1);
            Rect(7,26,11,4,fill,1);Rect(28,26,11,4,fill,1);Rect(49,26,11,4,fill,1);
            break;
        }
        case WidgetStyle.Thermostat:
        {
            var am=Palette.Brush(Accents.Readable(Color.Parse("#f6b25a"),p));var bl=Palette.Brush(Accents.Readable(Color.Parse("#7cc4f5"),p));
            var t=new Avalonia.Controls.Shapes.Arc{Width=28,Height=28,StartAngle=135,SweepAngle=270,Stroke=brush3,StrokeThickness=3.5,StrokeLineCap=PenLineCap.Round};
            var v=new Avalonia.Controls.Shapes.Arc{Width=28,Height=28,StartAngle=135,SweepAngle=150,Stroke=am,StrokeThickness=3.5,StrokeLineCap=PenLineCap.Round};
            Canvas.SetLeft(t,19);Canvas.SetTop(t,2);Canvas.SetLeft(v,19);Canvas.SetTop(v,2);c.Children.Add(t);c.Children.Add(v);
            Rect(27,13,12,5,fill,1.5);Rect(3,32,20,2,bl,1);Rect(44,32,20,2,fill2,1);
            break;
        }
        case WidgetStyle.SmartPanel:
        {
            var am=Palette.Brush(Accents.Readable(Color.Parse("#f59e0b"),p));var pk=Palette.Brush(Accents.Readable(Color.Parse("#f472b6"),p));var bl=Palette.Brush(Accents.Readable(Color.Parse("#60a5fa"),p));
            Rect(3,3,20,2,fill2,1);Rect(46,2,18,8,fill,1.5);
            Rect(3,20,3,3,am,1);Rect(25,20,3,3,pk,1);Rect(47,20,3,3,bl,1);
            Rect(3,25,16,6,fill,1.5);Rect(25,25,16,6,fill,1.5);Rect(47,25,16,6,fill,1.5);
            break;
        }
        case WidgetStyle.PurpleDash:
        {
            var pu=Palette.Brush(Accents.Readable(Color.Parse("#8b2cf5"),p));var pk=Palette.Brush(Accents.Readable(Color.Parse("#e64bd3"),p));
            Rect(3,2,10,4,fill,1);Rect(15,2,22,4,fill2,1);
            var v=new Avalonia.Controls.Shapes.Arc{Width=24,Height=24,StartAngle=150,SweepAngle=200,Stroke=pk,StrokeThickness=3.5,StrokeLineCap=PenLineCap.Round};
            Canvas.SetLeft(v,3);Canvas.SetTop(v,10);c.Children.Add(v);Rect(11,19,8,5,fill,1);
            for(int i=0;i<3;i++){Rect(32,11+i*8,32,3,fill2,0);Rect(32,11+i*8,8+i*8,3,pu,0);}
            break;
        }
        case WidgetStyle.PurpleList:
        {
            var pu=Palette.Brush(Accents.Readable(Color.Parse("#8b2cf5"),p));var pk=Palette.Brush(Accents.Readable(Color.Parse("#e64bd3"),p));
            Rect(3,2,10,4,fill,1);Rect(15,2,22,4,fill2,1);
            Rect(3,10,60,4,fill2,0);Rect(3,10,34,4,pk,0);
            for(int i=0;i<3;i++){Rect(3,18+i*6,60,3,fill2,0);Rect(3,18+i*6,14+i*12,3,pu,0);}
            break;
        }
        case WidgetStyle.SciFi:
        {
            var pk=Palette.Brush(Accents.Readable(Color.Parse("#ec4899"),p));var bl=Palette.Brush(Accents.Readable(Color.Parse("#3b82f6"),p));
            Rect(2,2,62,32,fill2,2);Rect(22,4,6,1.5,pk,0);Rect(38,4,6,1.5,pk,0);Rect(29,3,8,3,fill,1);
            for(int i=0;i<12;i++){double a=(135+270*i/11.0)*Math.PI/180;double x=33+11*Math.Cos(a),y=21+11*Math.Sin(a);Rect(x-1,y-1,2,2,i<7?(i<4?pk:bl):brush3,0.5);}
            Rect(27,18,12,6,fill,1);
            break;
        }
        case WidgetStyle.Dial:
        {
            var green=Palette.Brush(Accents.Readable(Color.Parse("#34d399"),p));
            var ring=new Avalonia.Controls.Shapes.Arc{Width=26,Height=26,StartAngle=135,SweepAngle=270,Stroke=brush3,StrokeThickness=3.5,StrokeLineCap=PenLineCap.Round};
            var val=new Avalonia.Controls.Shapes.Arc{Width=26,Height=26,StartAngle=135,SweepAngle=170,Stroke=green,StrokeThickness=3.5,StrokeLineCap=PenLineCap.Round};
            Canvas.SetLeft(ring,2);Canvas.SetTop(ring,4);Canvas.SetLeft(val,2);Canvas.SetTop(val,4);c.Children.Add(ring);c.Children.Add(val);
            Rect(9,15,12,4,green,1.5);
            Rect(33,4,14,12,fill,2);Rect(49,4,14,12,fill,2);Rect(33,19,14,12,fill,2);Rect(49,19,14,12,fill,2);
            Rect(35,10,9,2,fill2,1);Rect(51,10,9,2,fill2,1);Rect(35,25,9,2,fill2,1);Rect(51,25,9,2,fill2,1);
            break;
        }
        case WidgetStyle.Evolution:
        {
            var cyan=Palette.Brush(Accents.Readable(Color.Parse("#22d3ee"),p));var pink=Palette.Brush(Accents.Readable(Color.Parse("#f472b6"),p));var amber=Palette.Brush(Accents.Readable(Color.Parse("#fbbf24"),p));
            Rect(2,2,4,4,cyan,1.5);Rect(9,2.5,16,3,fill2);Rect(2,10,20,11,cyan,2);Rect(2,24,12,2,fill2);
            for(int i=0;i<8;i++){double hh=3+((i*5)%9);Rect(36+i*3.3,21-hh,2.4,hh,i==4?amber:cyan,1);}
            Rect(2,30,40,3,brush3,1.5);Rect(2,30,26,3,pink,1.5);Rect(46,29,16,5,fill2,1);
            break;
        }
        case WidgetStyle.ClarityColor:
            for(int row=0;row<3;row++)for(int col=0;col<2;col++){var tone=Palette.Brush(p.Ramp[(row+col)%3]);Rect(2+col*33,3+row*10,2,5,tone);Rect(7+col*33,4+row*10,10,2,fill2);Rect(20+col*33,3+row*10,10,4,tone);}break;
        case WidgetStyle.ClarityRows:
            for(int row=0;row<3;row++){var tone=Palette.Brush(p.Ramp[row]);Rect(2,2+row*11,60,8,brush3,2);Rect(2,2+row*11,2,8,tone);Rect(8,5+row*11,19,2,fill2);Rect(47,4+row*11,11,4,tone);}break;
        case WidgetStyle.ClarityTiles:
            for(int row=0;row<2;row++)for(int col=0;col<3;col++){var tone=Palette.Brush(p.Ramp[(row+col)%3]);Rect(2+col*21,2+row*17,18,14,brush3,2);Rect(2+col*21,2+row*17,2,14,tone);Rect(7+col*21,5+row*17,9,2,fill2);Rect(8+col*21,10+row*17,9,4,tone);}break;
        case WidgetStyle.Clarity:
            for(int row=0;row<3;row++){Rect(2,3+row*8,14,2,fill2);Rect(21,3+row*8,9,3,brush);Rect(35,3+row*8,12,2,fill2);Rect(53,3+row*8,9,3,brush2);}c.Children.Add(Line("M2,32 L12,29 L22,31 L32,28 L42,31 L52,29 L62,30",brush));break;
        case WidgetStyle.ControlDeck:
            Rect(2,2,23,9,brush);Rect(35,2,27,9,brush2);c.Children.Add(Line("M2,18 L12,14 L22,19 L32,15 L42,18 L52,14 L62,17",brush));for(int row=0;row<2;row++){Rect(2,24+row*6,16,2,fill2);Rect(24,24+row*6,8,2,brush);Rect(38,24+row*6,12,2,fill2);Rect(55,24+row*6,7,2,brush2);}break;
        case WidgetStyle.Duplex:
            for(int col=0;col<2;col++){Rect(2+col*33,2,25,3,fill2);Rect(2+col*33,10,27,8,col==0?brush:brush2);c.Children.Add(Line(col==0?"M2,28 L10,24 L18,29 L27,23":"M35,28 L43,24 L51,29 L62,23",col==0?brush:brush2));}break;
        case WidgetStyle.SignalRows:
            for(int row=0;row<2;row++){Rect(2,5+row*18,10,3,fill2);Rect(48,3+row*18,14,7,row==0?brush:brush2);c.Children.Add(Line(row==0?"M16,10 L22,4 L29,12 L36,6 L43,9":"M16,28 L22,22 L29,30 L36,24 L43,27",row==0?brush:brush2));}break;
        case WidgetStyle.OpenPanel:
            Rect(2,2,24,9,brush);Rect(36,2,25,9,brush2);Rect(2,17,60,1,fill2);for(int row=0;row<2;row++){Rect(2,22+row*8,18,3,fill2);Rect(41,22+row*8,21,3,brush);}break;
		case WidgetStyle.Standard:
			Rect(2.0, 3.0, 22.0, 9.0, brush2);
			Rect(2.0, 17.0, 60.0, 3.0, brush3, 1.5);
			Rect(2.0, 17.0, 20.0, 3.0, brush, 1.5);
			c.Children.Add(Line("M2,31 L12,28 L22,30 L32,25 L42,29 L52,26 L62,28", brush));
			break;
		case WidgetStyle.SegmentGraph:
		{
			for (int row = 0; row < 4; row++)
			{
				for (int num3 = 0; num3 < 10; num3++)
				{
					int on = new[] { 6, 3, 8, 5 }[row];
					Rect(2.0 + (double)num3 * 6.2, 4.0 + row * 8.0, 5.0, 5.0, num3 < on ? (row == 0 ? brush2 : brush) : brush3, 1.0);
				}
			}
			break;
		}
		case WidgetStyle.Segments:
		{
			for (int num3 = 0; num3 < 10; num3++)
			{
				Rect(2.0 + (double)num3 * 6.2, 8.0, 5.0, 7.0, (num3 < 6) ? ((num3 < 5) ? brush2 : Palette.Brush(p.Ramp[2])) : brush3, 1.0);
				Rect(2.0 + (double)num3 * 6.2, 20.0, 5.0, 7.0, (num3 < 3) ? brush : brush3, 1.0);
			}
			break;
		}
		case WidgetStyle.Pill:
		{
			Rect(0.0, 8.0, 64.0, 20.0, fill, 10.0);
			Rect(0.0, 8.0, 30.0, 20.0, Palette.Brush(Palette.Alpha(p.Calm, 0.35)), 10.0);
			Ellipse ellipse9 = new Ellipse
			{
				Width = 14.0,
				Height = 14.0,
				Fill = brush2
			};
			Canvas.SetLeft(ellipse9, 3.0);
			Canvas.SetTop(ellipse9, 11.0);
			c.Children.Add(ellipse9);
			Rect(21.0, 14.0, 14.0, 4.0, fill2, 1.0);
			Rect(46.0, 13.0, 13.0, 6.0, Palette.Brush(p.Ink), 1.0);
			break;
		}
		case WidgetStyle.FillBars:
			Rect(2.0, 5.0, 60.0, 8.0, brush3, 4.0);
			Rect(2.0, 5.0, 30.0, 8.0, brush2, 4.0);
			Rect(2.0, 18.0, 60.0, 8.0, brush3, 4.0);
			Rect(2.0, 18.0, 52.0, 8.0, Palette.Brush(p.Ramp[3]), 4.0);
			break;
		case WidgetStyle.Split:
			Rect(2.0, 10.0, 14.0, 10.0, brush2);
			Rect(21.0, 6.0, 1.0, 18.0, brush3, 0.0);
			Rect(26.0, 10.0, 14.0, 10.0, Palette.Brush(p.Ink));
			Rect(45.0, 6.0, 1.0, 18.0, brush3, 0.0);
			Rect(50.0, 10.0, 12.0, 10.0, Palette.Brush(p.Ink));
			Rect(2.0, 23.0, 10.0, 2.0, fill2, 1.0);
			Rect(26.0, 23.0, 10.0, 2.0, fill2, 1.0);
			Rect(50.0, 23.0, 10.0, 2.0, fill2, 1.0);
			break;
		case WidgetStyle.PillRow:
		{
			Rect(2.0, 3.0, 16.0, 4.0, fill2, 1.0);
			for (int l = 0; l < 3; l++)
			{
				Rect(2 + l * 21, 13.0, 19.0, 16.0, fill, 8.0);
				Rect(2 + l * 21, 13.0, 8 + l * 4, 16.0, (l == 2) ? Palette.Brush(Palette.Alpha(p.Ramp[2], 0.5)) : Palette.Brush(Palette.Alpha(p.Calm, 0.45)), 8.0);
			}
			break;
		}
		case WidgetStyle.KeyFigures:
		{
			for (int k = 0; k < 3; k++)
			{
				Rect(2 + k * 22, 7.0, 14.0, 9.0, (k == 0) ? brush2 : Palette.Brush(p.Ink));
				Rect(2 + k * 22, 19.0, 9.0, 2.0, fill2, 1.0);
				Rect(2 + k * 22, 25.0, 18.0, 3.0, brush3, 1.5);
				Rect(2 + k * 22, 25.0, 6 + k * 5, 3.0, (k == 0) ? brush2 : brush, 1.5);
			}
			Rect(21.0, 6.0, 1.0, 22.0, brush3, 0.0);
			Rect(43.0, 6.0, 1.0, 22.0, brush3, 0.0);
			break;
		}
        case WidgetStyle.TwinFocus:
            Rect(3,3,22,4,fill2);Rect(3,11,23,15,brush2);Rect(36,3,22,4,fill2);Rect(36,11,23,15,brush);
            Rect(31,3,1,28,brush3);break;
		case WidgetStyle.BadgeList:
		{
			for (int j = 0; j < 3; j++)
			{
				Rect(2.0, 5 + j * 10, 18.0, 4.0, fill2, 1.0);
				Rect(42.0, 3 + j * 10, 20.0, 8.0, (j == 2) ? Palette.Brush(Palette.Alpha(p.Ramp[3], 0.45)) : Palette.Brush(Palette.Alpha(p.Calm, 0.4)), 4.0);
				if (j < 2)
				{
					Rect(2.0, 12 + j * 10, 60.0, 1.0, brush3, 0.0);
				}
			}
			break;
		}
		case WidgetStyle.PillRowGraph:
		{
			for (int i = 0; i < 6; i++)
			{
				double x = (i % 3) * 21.5, y = 4 + (i / 3) * 15;
				Rect(x, y, 19.0, 11.0, brush3, 5.5);
				Rect(x, y, 6 + i * 2, 11.0, (i == 0) ? Palette.Brush(p.Ramp[2]) : brush, 5.5);
			}
			break;
		}
		case WidgetStyle.BadgeListGraph:
		{
			for (int i = 0; i < 4; i++)
			{
				Rect(2.0, 3 + i * 9, 12.0, 3.0, fill2, 1.0);
				Rect(18.0, 3.5 + i * 9, 26.0, 2.0, brush3, 1.0);
				Rect(18.0, 3.5 + i * 9, 6 + i * 5, 2.0, brush, 1.0);
				Rect(48.0, 1.5 + i * 9, 16.0, 6.0, (i == 0) ? Palette.Brush(p.Ramp[2]) : brush2, 3.0);
			}
			break;
		}
		case WidgetStyle.NeonHudGraph:
		{
			var cyan = Palette.Brush(Accents.Readable(Color.Parse("#22d3ee"), p));
			var pink = Palette.Brush(Accents.Readable(Color.Parse("#ec4899"), p));
			for (int i = 0; i < 4; i++)
			{
				Rect(2.0, 2 + i * 9, 14.0, 2.5, fill2, 1.0);
				Rect(52.0, 2 + i * 9, 10.0, 2.5, cyan, 1.0);
				Rect(2.0, 6 + i * 9, 60.0, 1.5, brush3, 0.5);
				Rect(2.0, 6 + i * 9, 14 + i * 9, 1.5, i % 2 == 0 ? cyan : pink, 0.5);
			}
			break;
		}
		case WidgetStyle.FillBarsGraph:
		{
			int[] fills = { 30, 18, 44, 26 };
			for (int i = 0; i < 4; i++)
			{
				Rect(0.0, 1 + i * 9, 64.0, 7.0, brush3, 3.0);
				Rect(0.0, 1 + i * 9, fills[i], 7.0, (i == 0) ? Palette.Brush(p.Ramp[2]) : brush, 3.0);
			}
			break;
		}
		case WidgetStyle.TableGraph:
		{
			for (int i = 0; i < 5; i++)
			{
				Rect(2.0, 3 + i * 7, 12.0, 3.0, fill2, 1.0);
				Rect(18.0, 3.5 + i * 7, 28.0, 2.0, brush3, 1.0);
				Rect(18.0, 3.5 + i * 7, 6 + i * 5, 2.0, (i == 0) ? Palette.Brush(p.Ramp[2]) : brush, 1.0);
				Rect(50.0, 3 + i * 7, 12.0, 3.0, brush2, 1.0);
			}
			break;
		}
		case WidgetStyle.Table:
		{
			for (int i = 0; i < 3; i++)
			{
				Rect(2.0, 5 + i * 10, 12.0, 4.0, fill2, 1.0);
				Rect(18.0, 6 + i * 10, 28.0, 2.5, brush3, 1.0);
				Rect(18.0, 6 + i * 10, 8 + i * 7, 2.5, (i == 2) ? Palette.Brush(p.Ramp[2]) : brush, 1.0);
				Rect(50.0, 5 + i * 10, 12.0, 4.0, brush2, 1.0);
			}
			break;
		}
		}
		return c;
		static Control Line(string data, IBrush stroke, IBrush? fill3 = null)
		{
			return new Avalonia.Controls.Shapes.Path
			{
				Data = Geometry.Parse(data),
				Stroke = stroke,
				StrokeThickness = 1.6,
				Fill = fill3,
				StrokeLineCap = PenLineCap.Round
			};
		}
		void Rect(double x, double y, double w, double h, IBrush fill3, double r = 2.0)
		{
			Rectangle rectangle = new Rectangle
			{
				Width = w,
				Height = h,
				Fill = fill3,
				RadiusX = r,
				RadiusY = r
			};
			Canvas.SetLeft(rectangle, x);
			Canvas.SetTop(rectangle, y);
			c.Children.Add(rectangle);
		}
	}

	private Control WidgetRow(List<WidgetSlot> group, int pos, StackPanel container)
	{
		WidgetSlot slot = group[pos];
		string text = ((slot.Name.Length > 0) ? (slot.Name + "  (" + Cards.Name(slot.Kind) + ")") : Cards.Name(slot.Kind));
		CheckBox check = new CheckBox
		{
			Name = "widget-" + slot.Kind,
			Content = text,
			IsChecked = slot.Visible,
			FontSize = 13.5
		};
		check.IsCheckedChanged += delegate
		{
			slot.Visible = check.IsChecked == true;
			changed();
			Dispatcher.UIThread.Post(Build);
		};
		bool flag = expanded.Contains(slot.Kind);
		Button button = Small(Ui.Text(L.T("Customise") + (flag ? "  ▴" : "  ▾"), 12.0, p.Ink, FontWeight.Medium), L.T("Customise"), enabled: true, delegate
		{
			if (!expanded.Remove(slot.Kind))
			{
				expanded.Add(slot.Kind);
			}
			Build();
		});
		List<string> list = new List<string>();
		if (s.Pages > 1)
		{
			list.Add(RegisterLabel(s.EffectivePage(slot)));
		}
		if (s.ColumnsInUse > 1 && slot.Column > 0)
		{
			list.Add(s.Horizontal ? L.F("Row {0}", Math.Min(slot.Column, s.ColumnsInUse)) : L.F("Column {0}", Math.Min(slot.Column, s.ColumnsInUse)));
		}
		if (Settings.HasStyles(slot.Kind))
		{
			list.Add(StyleName(slot.Style));
		}
		Border border = new Border
		{
			Child = Ui.Text(string.Join(" · ", list), 11.5, p.Soft),
			Background = Palette.Brush(p.Dark ? Color.Parse("#262E3B") : Color.Parse("#EEF2F6")),
			CornerRadius = new CornerRadius(6.0),
			Padding = new Thickness(8.0, 3.0),
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(0.0, 0.0, 6.0, 0.0),
			IsVisible = (list.Count > 0)
		};
		Border handle = new Border
		{
			Child = Ui.Glyph("M6,3.5 h0.01 M10,3.5 h0.01 M6,8 h0.01 M10,8 h0.01 M6,12.5 h0.01 M10,12.5 h0.01", p.Soft, 14.0),
			Background = Brushes.Transparent,
			Padding = new Thickness(4.0, 6.0),
			Margin = new Thickness(0.0, 0.0, 8.0, 0.0),
			Cursor = new Cursor(StandardCursorType.SizeNorthSouth),
			VerticalAlignment = VerticalAlignment.Center
		};
		if (handle.Child is Shape shape)
		{
			shape.StrokeThickness = 3.0;
			shape.StrokeLineCap = PenLineCap.Round;
		}
		ToolTip.SetTip(handle, L.T("Drag to sort"));
		Grid grid = Ui.Columns("Auto,*,Auto,Auto,Auto,Auto", handle, check, border, button, Small(Ui.Glyph("M3,10 L8,5 L13,10", p.Soft, 12.0), L.T("Move up"), pos > 0, delegate
		{
			MoveTo(group[pos - 1]);
		}), Small(Ui.Glyph("M3,6 L8,11 L13,6", p.Soft, 12.0), L.T("Move down"), pos < group.Count - 1, delegate
		{
			MoveTo(group[pos + 1]);
		}));
		StackPanel stackPanel = Ui.Stack(8.0, grid);
		Border card = new Border
		{
			Background = Palette.Brush(p.Dark ? Color.Parse("#1C222B") : Colors.White),
			BorderBrush = Palette.Brush(flag ? Color.FromArgb(115, p.Cool.R, p.Cool.G, p.Cool.B) : p.Stroke),
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(10.0),
			Padding = new Thickness(8.0, 6.0, 8.0, 6.0),
			Child = stackPanel
		};
		handle.PointerPressed += delegate(object? _, PointerPressedEventArgs e)
		{
			if (e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed)
			{
				dragging = slot;
				dragStart = e.GetPosition(container).Y;
				e.Pointer.Capture(handle);
				card.Opacity = 0.75;
				card.ZIndex = 10;
				e.Handled = true;
			}
		};
		handle.PointerMoved += delegate(object? _, PointerEventArgs e)
		{
			if (dragging == slot)
			{
				card.RenderTransform = new TranslateTransform(0.0, e.GetPosition(container).Y - dragStart);
			}
		};
		handle.PointerReleased += delegate(object? _, PointerReleasedEventArgs e)
		{
			if (dragging == slot)
			{
				dragging = null;
				e.Pointer.Capture(null);
				double y = e.GetPosition(container).Y;
				int num = group.Count - 1;
				for (int i = 0; i < container.Children.Count; i++)
				{
					if (y < container.Children[i].Bounds.Bottom)
					{
						num = i;
						break;
					}
				}
				if (y < 0.0)
				{
					num = 0;
				}
				if (num != pos)
				{
					MoveTo(group[num]);
				}
				else
				{
					card.RenderTransform = null;
					card.Opacity = 1.0;
					card.ZIndex = 0;
				}
			}
		};
		TextBlock value;
		Button fetch;
		if (flag)
		{
			string text2 = HardwareNameNow(slot.Kind);
			StackPanel stackPanel2 = Ui.Stack(6.0, NameRow(L.T("Title"), NameBox(slot.Name, Cards.Name(slot.Kind), delegate(string v)
			{
				slot.Name = v;
			}), ShowBox(slot.ShowTitle, delegate(bool v)
			{
				slot.ShowTitle = v;
			})), NameRow(L.T("Hardware name"), NameBox(slot.HardwareName, (text2.Length > 0) ? text2 : L.T("None"), delegate(string v)
			{
				slot.HardwareName = v;
			}), ShowBox(slot.ShowHardware, delegate(bool v)
			{
				slot.ShowHardware = v;
			})));
			stackPanel2.Margin = new Thickness(28.0, 0.0, 0.0, 6.0);
			stackPanel.Children.Add(stackPanel2);
			WrapPanel wrapPanel = new WrapPanel
			{
				Margin = new Thickness(28.0, 0.0, 0.0, 0.0)
			};
			if (Settings.HasStyles(slot.Kind))
			{
				WrapPanel wrapPanel2 = new WrapPanel();
				foreach (WidgetStyle style in Settings.Selectable)
				{
					bool flag2 = slot.Style == style;
					Button button2 = new Button();
					button2.Content = Ui.Row(6.0, new Viewbox
					{
						Width = 32.0,
						Height = 17.0,
						Child = StyleThumb(style)
					}, Ui.Text(StyleName(style), 12.0, flag2 ? Colors.White : p.Ink, flag2 ? FontWeight.DemiBold : FontWeight.Normal));
					button2.Padding = new Thickness(8.0, 4.0, 10.0, 4.0);
					button2.Margin = new Thickness(0.0, 0.0, 6.0, 6.0);
					button2.CornerRadius = new CornerRadius(7.0);
					button2.Classes.Add(flag2 ? "accent" : "plain");
					Button button3 = button2;
					button3.Click += delegate
					{
						slot.Style = style;
						changed();
						Build();
					};
					wrapPanel2.Children.Add(button3);
				}
				Grid grid2 = Ui.Columns("Auto,*", Ui.Text(L.T("Style"), 12.5, p.Soft), wrapPanel2);
				Control control = grid2.Children[0];
				if (control != null)
				{
					control.Margin = new Thickness(0.0, 6.0, 10.0, 0.0);
					control.VerticalAlignment = VerticalAlignment.Top;
				}
				grid2.Margin = new Thickness(28.0, 0.0, 0.0, 0.0);
				stackPanel.Children.Add(grid2);
				if (slot.Style == WidgetStyle.Evolution)
				{
					stackPanel.Children.Add(AccentRow(slot));
				}
			}
			if (s.ColumnsInUse > 1)
			{
				wrapPanel.Children.Add(Ui.Row(8.0, Ui.Text(s.Horizontal ? L.T("Row") : L.T("Column"), 12.5, p.Soft), Choice(new string[1] { L.T("Automatic") }.Concat(from n in Enumerable.Range(1, s.ColumnsInUse)
					select n.ToString()).ToList(), Math.Min(slot.Column, s.ColumnsInUse), delegate(int i)
				{
					slot.Column = i;
				}, rebuild: true, small: true)));
			}
			if (s.Pages > 1)
			{
				wrapPanel.Children.Add(Ui.Row(8.0, Ui.Text(L.T("Register"), 12.5, p.Soft), Choice(Enumerable.Range(1, s.Pages).Select(RegisterLabel).ToList(), s.EffectivePage(slot) - 1, delegate(int i)
				{
					slot.Page = i + 1;
				}, rebuild: true, small: true)));
			}
			foreach (Control child in wrapPanel.Children)
			{
				child.Margin = new Thickness(0.0, 0.0, 18.0, 6.0);
			}
			if (wrapPanel.Children.Count > 0)
			{
				stackPanel.Children.Add(wrapPanel);
			}
            var sizing = Ui.Stack(8,
                Ui.Columns("140,*", Ui.Text(L.T("Widget size"), 12.5, p.Soft), Slide(0.5, 2.0, 0.05, slot.WidgetScale, v => slot.WidgetScale = v, v => $"{v * 100:0} %")),
                Ui.Columns("140,*", Ui.Text(L.T("Graphic size"), 12.5, p.Soft), Slide(0.5, 2.0, 0.05, slot.GraphicScale, v => slot.GraphicScale = v, v => $"{v * 100:0} %")),
                Ui.Columns("140,*", Ui.Text(L.T("Main value size"), 12.5, p.Soft), Slide(0.5, 2.0, 0.05, slot.HeroScale, v => slot.HeroScale = v, v => $"{v * 100:0} %")),
                Ui.Columns("140,*", Ui.Text(L.T("Value size"), 12.5, p.Soft), Slide(0.5, 2.0, 0.05, slot.ValueScale, v => slot.ValueScale = v, v => $"{v * 100:0} %")),
                Ui.Columns("140,*", Ui.Text(L.T("Label size"), 12.5, p.Soft), Slide(0.5, 2.0, 0.05, slot.LabelScale, v => slot.LabelScale = v, v => $"{v * 100:0} %")));
            sizing.Margin = new Thickness(28, 8, 0, 8);
            stackPanel.Children.Add(sizing);
            var sensors = new StackPanel { Spacing = 2, Margin = new Thickness(28, 4, 0, 8) };
            sensors.Children.Add(SectionTitle(L.T("Sensor values")));
            foreach (var option in WidgetSensors.Options(slot.Kind, this.snapshot()))
            {
                if (slot.Kind == WidgetKind.Cpu && (ComparisonTile.Styles.Contains(slot.Style) || InsightTile.Styles.Contains(slot.Style)) && option.Key == "cpu.cores") continue;
                string sensorKey = option.Key;
                var sensorCheck = new CheckBox
                {
                    Name = "sensor-" + sensorKey.Replace(':', '-').Replace('.', '-'),
                    Content = option.Label,
                    IsChecked = s.Shows(sensorKey) && !slot.HiddenSensors.Contains(sensorKey),
                    FontSize = 12.5,
                    Margin = new Thickness(0, 1),
                    Tag = sensorKey
                };
                sensorCheck.IsCheckedChanged += (_, _) =>
                {
                    if (sensorCheck.IsChecked == true)
                    {
                        slot.HiddenSensors.Remove(sensorKey);
                        s.HiddenValues.Remove(sensorKey);
                    }
                    else if (!slot.HiddenSensors.Contains(sensorKey)) slot.HiddenSensors.Add(sensorKey);
                    changed();
                };
                sensors.Children.Add(sensorCheck);
            }
            if (sensors.Children.Count == 1) sensors.Children.Add(Ui.Text(L.T("No sensor values for this widget."), 12, p.Soft));
            stackPanel.Children.Add(sensors);

			if (slot.Kind == WidgetKind.Network)
			{
				value = Ui.Text("", 12.5, p.Ink, FontWeight.Medium, numbers: true);
				value.VerticalAlignment = VerticalAlignment.Center;
				fetch = new Button
				{
					Content = L.T("Get"),
					Padding = new Thickness(14.0, 5.0),
					CornerRadius = new CornerRadius(8.0),
					Margin = new Thickness(12.0, 0.0, 0.0, 0.0)
				};
				ToolTip.SetTip(fetch, L.T("Asks api.ipify.org once for the public address of this connection."));
				ExternalIp.Changed += OnChanged;
				fetch.DetachedFromVisualTree += delegate
				{
					ExternalIp.Changed -= OnChanged;
				};
				fetch.Click += async delegate
				{
					await ExternalIp.FetchAsync();
				};
				Show();
				TextBlock textBlock2 = Ui.Text(L.T("External IP"), 12.5, p.Soft);
				textBlock2.Width = 120.0;
				textBlock2.VerticalAlignment = VerticalAlignment.Center;
				Grid grid3 = Ui.Columns("Auto,*,Auto", textBlock2, value, fetch);
				grid3.Margin = new Thickness(28.0, 6.0, 0.0, 4.0);
				stackPanel.Children.Add(grid3);
			}
		}
		return card;
		void MoveTo(WidgetSlot target)
		{
			if (target != slot)
			{
				int index = s.Widgets.IndexOf(target);
				int index2 = s.Widgets.IndexOf(slot);
				s.Widgets.RemoveAt(index2);
				s.Widgets.Insert(index, slot);
				changed();
				Build();
			}
		}
		TextBox NameBox(string text3, string watermark, Action<string> set)
		{
			TextBox box = new TextBox
			{
				Text = text3,
				Watermark = watermark,
				MinWidth = 0.0,
				CornerRadius = new CornerRadius(8.0),
				MaxLength = 60
			};
			box.LostFocus += delegate
			{
				string obj = (box.Text ?? "").Trim();
				set(obj);
				changed();
			};
			return box;
		}
		Control NameRow(string label, TextBox box, CheckBox show)
		{
			TextBlock textBlock3 = Ui.Text(label, 12.5, p.Soft);
			textBlock3.Width = 120.0;
			textBlock3.VerticalAlignment = VerticalAlignment.Center;
			return Ui.Columns("Auto,*,Auto", textBlock3, box, show);
		}
		void OnChanged()
		{
			Dispatcher.UIThread.Post(Show);
		}
		void Show()
		{
			TextBlock textBlock3 = value;
			object text3;
			if (!ExternalIp.Busy)
			{
				string address = ExternalIp.Address;
				text3 = ((address != null) ? $"{address}  ·  {ExternalIp.At:HH:mm}" : (ExternalIp.Failed ? L.T("Not reachable") : L.T("Not fetched yet")));
			}
			else
			{
				text3 = L.T("Asking…");
			}
			textBlock3.Text = (string?)text3;
			value.Foreground = Palette.Brush((ExternalIp.Address == null) ? p.Soft : p.Ink);
			fetch.IsEnabled = !ExternalIp.Busy;
		}
		CheckBox ShowBox(bool on, Action<bool> set)
		{
			CheckBox c3 = new CheckBox
			{
				Content = L.T("Show"),
				IsChecked = on,
				FontSize = 12.5,
				Margin = new Thickness(12.0, 0.0, 0.0, 0.0)
			};
			c3.IsCheckedChanged += delegate
			{
				set(c3.IsChecked == true);
				changed();
			};
			return c3;
		}
		static Button Small(Control body, string tip, bool enabled, Action click)
		{
			Button obj = new Button
			{
				Content = body,
				Padding = new Thickness(8.0, 6.0),
				CornerRadius = new CornerRadius(7.0),
				IsEnabled = enabled,
				Margin = new Thickness(6.0, 0.0, 0.0, 0.0)
			};
			ToolTip.SetTip(obj, tip);
			obj.Click += delegate
			{
				click();
			};
			return obj;
		}
	}

	private Control[] AppearancePage()
	{
		(ThemeMode, string)[] obj = new(ThemeMode, string)[3]
		{
			(ThemeMode.Dark, L.T("Dark")),
			(ThemeMode.Light, L.T("Light")),
			(ThemeMode.System, L.T("Like Windows"))
		};
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Spacing = 2.0
		};
		(ThemeMode, string)[] array = obj;
		for (int i = 0; i < array.Length; i++)
		{
			(ThemeMode, string) tuple = array[i];
			ThemeMode mode = tuple.Item1;
			string item = tuple.Item2;
			bool flag = s.Theme == mode;
			Button obj2 = new Button
			{
				Content = item,
				Padding = new Thickness(14.0, 5.0),
				CornerRadius = new CornerRadius(7.0),
				BorderThickness = new Thickness(0.0),
				FontSize = 13.0
			};
			IBrush background;
			if (!flag)
			{
				IBrush transparent = Brushes.Transparent;
				background = transparent;
			}
			else
			{
				background = Palette.Brush(p.Dark ? Color.Parse("#2A3242") : Colors.White);
			}
			obj2.Background = background;
			obj2.FontWeight = (flag ? FontWeight.DemiBold : FontWeight.Normal);
			Button button = obj2;
			button.Click += delegate
			{
				s.Theme = mode;
				changed();
				Build();
			};
			stackPanel.Children.Add(button);
		}
		Border editor = new Border
		{
			Child = stackPanel,
			Padding = new Thickness(3.0),
			CornerRadius = new CornerRadius(9.0),
			Background = Palette.Brush(p.Dark ? Color.Parse("#0F131A") : Color.Parse("#E4E9F0")),
			BorderBrush = Palette.Brush(p.Stroke),
			BorderThickness = new Thickness(1.0)
		};
		WrapPanel wrapPanel = new WrapPanel
		{
			MaxWidth = 330.0,
			HorizontalAlignment = HorizontalAlignment.Right
		};
		LinearGradientBrush linearGradientBrush = new LinearGradientBrush
		{
			StartPoint = new RelativePoint(0.0, 0.0, RelativeUnit.Relative),
			EndPoint = new RelativePoint(1.0, 0.0, RelativeUnit.Relative)
		};
		linearGradientBrush.GradientStops.Add(new GradientStop(p.Ramp[0], 0.0));
		linearGradientBrush.GradientStops.Add(new GradientStop(p.Ramp[1], 0.45));
		linearGradientBrush.GradientStops.Add(new GradientStop(p.Ramp[2], 0.75));
		linearGradientBrush.GradientStops.Add(new GradientStop(p.Ramp[3], 1.0));
		wrapPanel.Children.Add(Swatch(linearGradientBrush, s.Colors == "state", L.T("By state (blue to red)"), delegate
		{
			s.Colors = "state";
		}, 50.0));
		Color c = Application.Current?.PlatformSettings?.GetColorValues().AccentColor1 ?? Color.Parse("#0078D4");
		wrapPanel.Children.Add(Swatch(Palette.Brush(c), s.Colors == "windows", L.T("Windows accent colour"), delegate
		{
			s.Colors = "windows";
		}));
		LinearGradientBrush linearGradientBrush2 = new LinearGradientBrush
		{
			StartPoint = new RelativePoint(0.0, 0.0, RelativeUnit.Relative),
			EndPoint = new RelativePoint(1.0, 0.0, RelativeUnit.Relative)
		};
		linearGradientBrush2.GradientStops.Add(new GradientStop(Color.Parse("#9A9A9A"), 0.0));
		linearGradientBrush2.GradientStops.Add(new GradientStop(Color.Parse("#D6D6D6"), 0.7));
		linearGradientBrush2.GradientStops.Add(new GradientStop(p.Ramp[3], 1.0));
		wrapPanel.Children.Add(Swatch(linearGradientBrush2, s.Colors == "mono", L.T("Grey – only warnings in colour"), delegate
		{
			s.Colors = "mono";
		}, 50.0));
		(string, string)[] presets = Palette.Presets;
		for (int i = 0; i < presets.Length; i++)
		{
			var (hex, key) = presets[i];
			wrapPanel.Children.Add(Swatch(Palette.Brush(Color.Parse(hex)), s.Colors.Equals(hex, StringComparison.OrdinalIgnoreCase), L.T(key), delegate
			{
				s.Colors = hex;
			}));
		}
		bool flag2 = s.Theme switch
		{
			ThemeMode.Dark => true, 
			ThemeMode.Light => false, 
			_ => Application.Current?.ActualThemeVariant != ThemeVariant.Light, 
		};
		WrapPanel wrapPanel2 = new WrapPanel
		{
			MaxWidth = 330.0,
			HorizontalAlignment = HorizontalAlignment.Right
		};
		(string, string, string, string, string, string)[] backgrounds = Palette.Backgrounds;
		for (int i = 0; i < backgrounds.Length; i++)
		{
			(string Key, string Name, string DarkSurface, string DarkCard, string LightSurface, string LightCard) bg = backgrounds[i];
			wrapPanel2.Children.Add(Swatch(Palette.Brush(Color.Parse(flag2 ? bg.DarkCard : bg.LightCard)), s.Background == bg.Key, L.T(bg.Name), delegate
			{
				s.Background = bg.Key;
			}, 24.0, round: true));
		}
		string help = L.T(Palette.Backgrounds.FirstOrDefault(((string Key, string Name, string DarkSurface, string DarkCard, string LightSurface, string LightCard) b) => b.Key == s.Background).Name ?? Palette.Backgrounds[0].Name);
		string text = ((s.Colors == "state") ? L.T("By state (blue to red)") : ((s.Colors == "windows") ? L.T("Windows accent colour") : ((s.Colors == "mono") ? L.T("Grey – only warnings in colour") : L.T(Palette.Presets.FirstOrDefault(((string Hex, string Name) x) => x.Hex.Equals(s.Colors, StringComparison.OrdinalIgnoreCase)).Name ?? ""))));
		Control control = null;
		if (s.Glass)
		{
			control = Row(L.T("Opacity"), Slide(0.2, 1.0, 0.05, s.Opacity, delegate(double v)
			{
				s.Opacity = v;
			}, (double v) => $"{v * 100.0:0} %"), L.T("Only with the glass effect."), "Opacity");
			control.Margin = new Thickness(16.0, 0.0, 0.0, 0.0);
		}
		else
		{
			Owns("Opacity");
		}
		return new Control[5]
		{
			Section(L.T("Colours"), Row(L.T("Theme"), editor, null, "Theme"), Row(L.T("Colour scheme"), wrapPanel, text + ((s.Colors == "state") ? "" : (" · " + L.T("Hot readings still turn amber and red."))), "Colors"), Row(L.T("Background"), wrapPanel2, help, "Background")),
			Section(L.T("Effects"), Row(L.T("Glass effect"), Toggle(s.Glass, delegate(bool v)
			{
				s.Glass = v;
			}, notify: true, rebuild: true), L.T("Translucent background with blur"), "Glass"), control, Row(L.T("Modern look"), Toggle(s.Modern, delegate(bool v)
			{
				s.Modern = v;
			}, notify: true, rebuild: true), L.T("Cards without borders, rounder corners, icons in tinted circles, bolder numbers."), "Modern"), Row(L.T("Smooth transitions"), Toggle(s.SmoothTransitions, delegate(bool v)
			{
				s.SmoothTransitions = v;
			}, notify: true, rebuild: true), L.T("Numbers count, bars glide and charts scroll smoothly."), "SmoothTransitions")),
			Section(L.T("Text"), Row(L.T("Text size"), Slide(0.5, 2.0, 0.05, s.TextScale, delegate(double v)
			{
				s.TextScale = v;
			}, (double v) => $"{v * 100.0:0} %"), L.T("Above 100 % the sidebar grows wider by the same amount."), "TextScale"), Row(L.T("Typeface"), Choice(FontNames(), Math.Max(0, Array.IndexOf(Settings.Fonts, s.Font)), delegate(int num)
			{
				s.Font = Settings.Fonts[num];
			}, rebuild: true), L.T("Narrow fonts fit more values into the bar."), "Font"), Row(L.T("High contrast"), Toggle(s.HighContrast, delegate(bool v)
			{
				s.HighContrast = v;
			}, notify: true, rebuild: true), L.T("All labels in full brightness."), "HighContrast"), Row(L.T("Hardware names"), Toggle(s.ShowNames, delegate(bool v)
			{
				s.ShowNames = v;
			}, notify: true, rebuild: true), L.T("Model names under the titles, e.g. Ryzen 9 5900X."), "ShowNames")),
			AllSizesSection(),
			ThemeSection()
		};
		Button Swatch(IBrush fill, bool on, string tip, Action pick, double w = 24.0, bool round = false)
		{
			Button obj3 = new Button
			{
				Width = w + 4.0,
				Height = 28.0,
				Padding = new Thickness(0.0),
				Margin = new Thickness(0.0, 0.0, 6.0, 6.0),
				CornerRadius = new CornerRadius(round ? 14 : 7),
				Background = Brushes.Transparent,
				BorderThickness = new Thickness(2.0)
			};
			IBrush borderBrush;
			if (!on)
			{
				IBrush transparent2 = Brushes.Transparent;
				borderBrush = transparent2;
			}
			else
			{
				borderBrush = Palette.Brush(p.Ink);
			}
			obj3.BorderBrush = borderBrush;
			obj3.Content = new Border
			{
				Width = w,
				Height = 24.0,
				CornerRadius = new CornerRadius(round ? 12 : 5),
				Background = fill,
				BorderBrush = Palette.Brush((!round) ? p.Stroke : (p.Dark ? Color.Parse("#4A5568") : Color.Parse("#AEB8C6"))),
				BorderThickness = new Thickness(1.0)
			};
			obj3.Cursor = new Cursor(StandardCursorType.Hand);
			ToolTip.SetTip(obj3, tip);
			obj3.Click += delegate
			{
				pick();
				changed();
				Build();
			};
			return obj3;
		}
	}

	private static string[] FontNames()
	{
		return new string[6]
		{
			L.T("Inter (built in)"),
			"Segoe UI",
			"Segoe UI Variable",
			"Bahnschrift",
			L.T("Bahnschrift narrow"),
			L.T("Monospace (Cascadia, Consolas)")
		};
	}

    private Control ThemeSection()
    {
        var choices = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, MaxWidth = 360 };
        foreach (var preset in ThemeFile.BuiltIn)
        {
            var theme = preset.Theme;
            var button = new Button
            {
                Content = L.T(preset.Name), Padding = new Thickness(12, 6),
                CornerRadius = new CornerRadius(8), Margin = new Thickness(8, 0, 0, 6)
            };
            button.Click += delegate
            {
                double textScale = s.TextScale;
                theme.ApplyTo(s);
                s.TextScale = textScale;
                changed();
                Build();
            };
            choices.Children.Add(button);
        }
        return Section(L.T("Themes"), Row(L.T("Built-in themes"), choices,
            L.T("Eight themes with coordinated colours, backgrounds and fonts.")));
    }

	private Control[] PlacementPage()
	{
		IReadOnlyList<string> readOnlyList = screens();
		Control control = null;
		if (s.Horizontal)
		{
			TextBlock textBlock = Ui.Text(L.T("Bar at the top or bottom: the widgets sit side by side."), 12.0, p.Soft);
			textBlock.TextWrapping = TextWrapping.Wrap;
			textBlock.TextTrimming = TextTrimming.None;
			control = textBlock;
		}
		Owns("Width");
		if (!s.Horizontal)
		{
			Owns("BarHeight", "BarFullWidth");
		}
		else
		{
			Owns("FullScreen");
		}
		if (s.Pages <= 1)
		{
			Owns("PageNames");
		}
		Control[] array = new Control[2];
		string titleText = L.T("Position");
		Control[] obj = new Control[8]
		{
			Row(L.T("Screen edge"), Choice(new string[4]
			{
				L.T("Right"),
				L.T("Left"),
				L.T("Top (bar)"),
				L.T("Bottom (bar)")
			}, (int)s.Edge, delegate(int i)
			{
				if (i >= 2 != s.Horizontal)
				{
					s.Columns = 1;
				}
				s.Edge = (Edge)i;
			}, rebuild: true), s.Horizontal ? L.T("A bar across the whole width; the widgets sit side by side.") : null, "Edge"),
			null,
			null,
			null,
			null,
			null,
			null,
			null
		};
		string label = L.T("Screen");
		IReadOnlyList<string> items;
		if (readOnlyList.Count <= 0)
		{
			IReadOnlyList<string> readOnlyList2 = new string[1] { L.T("Main screen") };
			items = readOnlyList2;
		}
		else
		{
			items = readOnlyList;
		}
		obj[1] = Row(label, Choice(items, Math.Min(s.Screen, Math.Max(0, readOnlyList.Count - 1)), delegate(int i)
		{
			s.Screen = i;
		}, rebuild: true), null, "Screen");
		obj[2] = Row(L.T("Always on top"), Toggle(s.Topmost, delegate(bool v)
		{
			s.Topmost = v;
		}, notify: true, rebuild: true), null, "Topmost");
		obj[3] = Row(L.T("Reserve space"), Toggle(s.ReserveSpace, delegate(bool v)
		{
			s.ReserveSpace = v;
		}, notify: true, rebuild: true), L.T("Maximised windows stop before the sidebar."), "ReserveSpace");
		obj[4] = Row(L.T("Click-through"), Toggle(s.ClickThrough, delegate(bool v)
		{
			s.ClickThrough = v;
		}, notify: true, rebuild: true), L.T("Clicks go to the windows behind the sidebar; open the settings from the tray icon."), "ClickThrough");
		obj[5] = Row(L.T("Floating"), Toggle(s.Floating, delegate(bool v)
		{
			s.Floating = v;
		}, notify: true, rebuild: true), L.T("Single cards with a gap and a shadow instead of a continuous bar; no space is reserved."), "Floating");
		if (s.Floating)
		{
			Button reset = new Button
			{
				Content = L.T("Reset positions"),
				Padding = new Thickness(14.0, 5.0),
				CornerRadius = new CornerRadius(8.0)
			};
			reset.Click += delegate
			{
				string prefix = s.Screen + ":";
				foreach (string key in s.FloatPositions.Keys.Where((string k) => k.StartsWith(prefix)).ToList())
				{
					s.FloatPositions.Remove(key);
				}
				changed();
			};
			obj[7] = Row(L.T("Card positions"), reset, L.T("Hold Ctrl and drag a card to place it anywhere on the monitor. Clicks between the cards go to the windows behind."), "FloatPositions");
		}
		obj[6] = Row(L.T("Hide in full screen"), Toggle(s.HideInFullscreen, delegate(bool v)
		{
			s.HideInFullscreen = v;
		}, notify: true), L.T("Hides the sidebar while a game or video fills the monitor it is on."), "HideInFullscreen");
		array[0] = Section(titleText, obj);
		array[1] = Section(L.T("Layout"), control, Row(s.Horizontal ? L.T("Rows") : L.T("Columns"), Choice(new string[6] { "1", "2", "3", "4", "5", "6" }, s.Columns - 1, delegate(int i)
		{
			s.Columns = i + 1;
		}, rebuild: true), s.Horizontal ? L.T("Rows one below the other, like columns at the side; each is as tall as the row height.") : L.T("Several columns side by side; each is as wide as set below, everything stays large."), "Columns"), Row(L.T("Registers"), Choice(new string[6] { "1", "2", "3", "4", "5", "6" }, s.Pages - 1, delegate(int i)
		{
			s.Pages = i + 1;
		}, rebuild: true), L.T("Pages at the top; each widget goes on one of them. Switch by click or mouse wheel."), "Pages"), (s.Pages > 1) ? WideRow(L.T("Register names"), PageNameEditor(), L.T("Shown at the top instead of 1, 2, 3; leave empty for the number."), "PageNames") : null, s.Horizontal ? Row(L.T("Whole width"), Toggle(s.BarFullWidth, delegate(bool v)
		{
			s.BarFullWidth = v;
		}, notify: true, rebuild: true), L.T("Off: the bar is only as wide as its widgets, centred."), "BarFullWidth") : Row(L.T("Whole screen"), Toggle(s.FullScreen, delegate(bool v)
		{
			s.FullScreen = v;
		}, notify: true, rebuild: true), L.T("Covers the whole monitor up to the taskbar; the columns share its width."), "FullScreen"), s.Horizontal ? Row(L.T("Row height"), Slide(40.0, 700.0, 4.0, s.BarHeight, delegate(double v)
		{
			s.BarHeight = v;
		}, (double v) => $"{v:0} px"), L.T("Increase the row height for larger values and graphics."), "BarHeight") : null, Row(L.T("Density"), Choice(new string[3]
		{
			L.T("Automatic"),
			L.T("Comfortable"),
			L.T("Compact")
		}, (int)s.Density, delegate(int i)
		{
			s.Density = (Density)i;
		}, rebuild: true), s.Horizontal ? L.T("In the bar, Compact makes the widgets smaller and tighter.") : L.T("Automatic fills the height up to the taskbar, without scrolling."), "Density"), (s.Horizontal && s.BarFullWidth) ? null : Row(s.Horizontal ? L.T("Widget width") : L.T("Width"), Slide(240.0, 440.0, 10.0, s.Width, delegate(double v)
		{
			s.Width = v;
		}, (double v) => $"{v:0} px"), null, "Width"));
		return array;
	}

	private Control WeatherEditor()
	{
		TextBox box = new TextBox
		{
			Text = s.WeatherPlace,
			Watermark = "Zürich",
			CornerRadius = new CornerRadius(8.0),
			MinWidth = 0.0
		};
		Button search = new Button
		{
			Content = L.T("Search"),
			Padding = new Thickness(14.0, 6.0),
			CornerRadius = new CornerRadius(8.0),
			Margin = new Thickness(8.0, 0.0, 0.0, 0.0)
		};
		TextBlock status = Ui.Text(weatherStatus, 12.0, p.Soft);
		status.TextWrapping = TextWrapping.Wrap;
		status.TextTrimming = TextTrimming.None;
		status.IsVisible = weatherStatus.Length > 0;
		search.Click += delegate
		{
			Find();
		};
		box.KeyDown += delegate(object? _, KeyEventArgs e)
		{
			if (e.Key == Key.Return)
			{
				Find();
			}
		};
		TextBlock textBlock = Ui.Text(L.T("Weather data from Open-Meteo; only the place's coordinates are sent."), 11.5, p.Soft);
		textBlock.TextWrapping = TextWrapping.Wrap;
		textBlock.TextTrimming = TextTrimming.None;
		TextBlock textBlock2 = Ui.Text(L.T("Place"), 13.5, p.Ink, FontWeight.Medium);
		textBlock2.Margin = new Thickness(0.0, 0.0, 16.0, 0.0);
		return Ui.Stack(8.0, Ui.Columns("Auto,*,Auto", textBlock2, box, search), status, textBlock);
		async void Find()
		{
			string text = box.Text ?? "";
			if (string.IsNullOrWhiteSpace(text))
			{
				s.WeatherPlace = "";
				Settings settings = s;
				double? weatherLatitude = (s.WeatherLongitude = null);
				settings.WeatherLatitude = weatherLatitude;
				weatherStatus = "";
				changed();
				weatherChanged();
			}
			else
			{
				search.IsEnabled = false;
				status.Text = L.T("Searching…");
				status.IsVisible = true;
				try
				{
					WeatherClient.Place place = await WeatherClient.FindAsync(text, L.Language);
					if ((object)place == null)
					{
						weatherStatus = L.T("No place found.");
					}
					else
					{
						s.WeatherPlace = place.Name;
						s.WeatherLatitude = place.Latitude;
						s.WeatherLongitude = place.Longitude;
						weatherStatus = L.F("Found: {0}", (place.Region.Length > 0) ? (place.Name + ", " + place.Region) : place.Name);
						s.Widgets.First((WidgetSlot w) => w.Kind == WidgetKind.Weather).Visible = true;
						changed();
						weatherChanged();
					}
				}
				catch
				{
					weatherStatus = L.T("No connection to the weather service.");
				}
				Build();
			}
		}
	}

	private Control WorldClockPicker()
	{
		WrapPanel wrapPanel = new WrapPanel();
		string[] zones = Zones;
		foreach (string zone in zones)
		{
			CheckBox c = new CheckBox
			{
				Content = WorldClocksCard.City(zone),
				IsChecked = s.WorldClocks.Contains(zone),
				FontSize = 12.5,
				Margin = new Thickness(0.0, 0.0, 10.0, 2.0),
				MinWidth = 128.0
			};
			c.IsCheckedChanged += delegate
			{
				if (c.IsChecked == true && !s.WorldClocks.Contains(zone))
				{
					if (s.WorldClocks.Count >= 4)
					{
						c.IsChecked = false;
						return;
					}
					s.WorldClocks.Add(zone);
				}
				else if (c.IsChecked != true)
				{
					s.WorldClocks.Remove(zone);
				}
				changed();
			};
			wrapPanel.Children.Add(c);
		}
		TextBlock textBlock = Ui.Text(L.T("Up to four cities."), 11.5, p.Soft);
		return Ui.Stack(8.0, wrapPanel, textBlock);
	}

	private Control Section(string titleText, params Control?[] given)
	{
		Control[] array = given.Where((Control r) => r != null).Cast<Control>().ToArray();
		TextBlock textBlock = Ui.Text(titleText.ToUpper(L.Culture), 11.0, p.Soft, FontWeight.DemiBold);
		textBlock.LetterSpacing = 1.0;
		StackPanel stackPanel = new StackPanel
		{
			Spacing = 14.0
		};
		stackPanel.Children.AddRange(array);
		bool flag = array.Length == 1 && array[0] is StackPanel stackPanel2 && stackPanel2.Spacing == 6.0;
		Border border = new Border();
		IBrush background;
		if (!flag)
		{
			background = Palette.Brush(p.Dark ? Color.Parse("#191E26") : Colors.White);
		}
		else
		{
			IBrush transparent = Brushes.Transparent;
			background = transparent;
		}
		border.Background = background;
		border.BorderBrush = Palette.Brush(p.Stroke);
		border.BorderThickness = new Thickness((!flag) ? 1 : 0);
		border.CornerRadius = new CornerRadius(12.0);
		border.Padding = (flag ? new Thickness(0.0) : new Thickness(18.0, 16.0));
		border.Child = stackPanel;
		Border border2 = border;
		return Ui.Stack(10.0, textBlock, border2);
	}

	// The same five sizes as under Widgets → Customize, for every widget at once. The slider starts at the
	// value most widgets have; moving it sets that size on all widgets.
	private Control AllSizesSection()
	{
		Control All(string label, Func<WidgetSlot, double> get, Action<WidgetSlot, double> set, string? help = null)
		{
			double start = s.Widgets.GroupBy(w => Math.Round(get(w), 2)).OrderByDescending(g => g.Count()).Select(g => g.Key).DefaultIfEmpty(1.0).First();
			return Row(label, Slide(0.5, 2.0, 0.05, start, v =>
			{
				foreach (WidgetSlot w in s.Widgets)
				{
					set(w, v);
				}
			}, v => $"{v * 100:0} %"), help);
		}
		return Section(L.T("Sizes for all widgets"),
			All(L.T("Widget size"), w => w.WidgetScale, (w, v) => w.WidgetScale = v, L.T("Sets every widget at once; each one can still be changed under Widgets → Customise.")),
			All(L.T("Graphic size"), w => w.GraphicScale, (w, v) => w.GraphicScale = v),
			All(L.T("Main value size"), w => w.HeroScale, (w, v) => w.HeroScale = v),
			All(L.T("Value size"), w => w.ValueScale, (w, v) => w.ValueScale = v),
			All(L.T("Label size"), w => w.LabelScale, (w, v) => w.LabelScale = v));
	}

	private Control Row(string label, Control editor, string? help = null, string? prop = null)
	{
		Index(label, help);
		TextBlock textBlock = Ui.Text(label, 13.5, p.Ink, FontWeight.Medium);
		StackPanel stackPanel = new StackPanel
		{
			Spacing = 2.0,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(0.0, 0.0, 16.0, 0.0)
		};
		Controls children = stackPanel.Children;
		Control control = ChangedDot(prop);
		children.Add((control != null) ? ((Control)Ui.Row(0.0, textBlock, control)) : ((Control)textBlock));
		if (help != null)
		{
			TextBlock textBlock2 = Ui.Text(help, 11.5, p.Soft);
			textBlock2.TextWrapping = TextWrapping.Wrap;
			textBlock2.TextTrimming = TextTrimming.None;
			stackPanel.Children.Add(textBlock2);
		}
		editor.VerticalAlignment = VerticalAlignment.Center;
		return Ui.Columns("*,Auto", stackPanel, editor);
	}

	private Control WideRow(string label, Control editor, string? help = null, string? prop = null)
	{
		Index(label, help);
		StackPanel stackPanel = new StackPanel
		{
			Spacing = 2.0
		};
		TextBlock textBlock = Ui.Text(label, 13.5, p.Ink, FontWeight.Medium);
		Controls children = stackPanel.Children;
		Control control = ChangedDot(prop);
		children.Add((control != null) ? ((Control)Ui.Row(0.0, textBlock, control)) : ((Control)textBlock));
		if (help != null)
		{
			TextBlock textBlock2 = Ui.Text(help, 11.5, p.Soft);
			textBlock2.TextWrapping = TextWrapping.Wrap;
			textBlock2.TextTrimming = TextTrimming.None;
			stackPanel.Children.Add(textBlock2);
		}
		editor.Margin = new Thickness(0.0, 8.0, 0.0, 0.0);
		stackPanel.Children.Add(editor);
		return stackPanel;
	}

	private Control Toggle(bool value, Action<bool> set, bool notify = true, bool rebuild = false)
	{
		ToggleSwitch t = new ToggleSwitch
		{
			IsChecked = value,
			OnContent = "",
			OffContent = "",
			MinWidth = 0.0,
			Margin = new Thickness(0.0, 0.0, -8.0, 0.0)
		};
		t.IsCheckedChanged += delegate
		{
			set(t.IsChecked == true);
			if (notify)
			{
				changed();
			}
			if (rebuild)
			{
				Dispatcher.UIThread.Post(Build);
			}
		};
		return t;
	}

	private Control PageNameEditor()
	{
		UniformGrid uniformGrid = new UniformGrid
		{
			Columns = Math.Min(3, s.Pages)
		};
		for (int i = 0; i < s.Pages; i++)
		{
			int n = i;
			TextBox box = new TextBox
			{
				Text = (s.PageNames.ElementAtOrDefault(n) ?? ""),
				Watermark = $"{L.T("Register")} {n + 1}",
				MaxLength = 16,
				CornerRadius = new CornerRadius(8.0),
				Margin = new Thickness(0.0, 0.0, 6.0, 6.0),
				MinWidth = 0.0
			};
			box.LostFocus += delegate
			{
				while (s.PageNames.Count < 6)
				{
					s.PageNames.Add("");
				}
				string text = (box.Text ?? "").Trim();
				if (!(s.PageNames[n] == text))
				{
					s.PageNames[n] = text;
					changed();
				}
			};
			uniformGrid.Children.Add(box);
		}
		return uniformGrid;
	}

	private string HardwareNameNow(WidgetKind kind)
	{
		Snapshot snapshot = this.snapshot();
		if ((object)snapshot == null)
		{
			return "";
		}
		switch (kind)
		{
		case WidgetKind.Cpu:
			return Names.Clean(snapshot.Cpu?.Name);
		case WidgetKind.Gpu:
			return Names.Clean(snapshot.Gpus.FirstOrDefault()?.Name);
		case WidgetKind.Cooling:
			return Names.Clean(snapshot.BoardName);
		case WidgetKind.Network:
			return Names.Clean(snapshot.Network?.Name);
		case WidgetKind.System:
		{
			SystemInfo system = snapshot.System;
			return ((object)system != null) ? (system.Os + " · " + system.Build) : "";
		}
		default:
			return "";
		}
	}

	private static string SourceHelp(SensorSource source)
	{
		if (source == SensorSource.Builtin)
		{
			return L.T("LibreHardwareMonitor and Open Hardware Monitor, built into Lumen.");
		}
		string status = ExternalSensors.Status;
		string text = source switch
		{
			SensorSource.Aida64 => L.T("In AIDA64: Preferences → Hardware Monitoring → External Applications → Enable shared memory."), 
			SensorSource.HwInfo => L.T("In HWiNFO: Settings → Shared Memory Support (the free version turns it off after 12 hours)."), 
			_ => L.T("Uses AIDA64 if it is running, otherwise HWiNFO, otherwise the built-in sensors."), 
		};
		if (status.StartsWith("connected:"))
		{
			string text2 = (status.EndsWith("Aida64") ? "AIDA64" : "HWiNFO");
			return L.F("Connected to {0}. Readings it does not have come from the built-in sensors.", text2) + " " + text;
		}
		if (status.StartsWith("missing:"))
		{
			return L.T("Not reachable – the program is not running or its shared memory is off. The built-in sensors are shown meanwhile.") + " " + text;
		}
		return L.T("Readings it does not have come from the built-in sensors.") + " " + text;
	}

	private Control Choice(IReadOnlyList<string> items, int selected, Action<int> set, bool rebuild = false, bool small = false)
	{
		ComboBox c = new ComboBox
		{
			ItemsSource = items,
			SelectedIndex = Math.Clamp(selected, 0, items.Count - 1),
			MinWidth = (small ? 110 : 190),
			MaxWidth = 290.0,
			CornerRadius = new CornerRadius(8.0)
		};
		c.SelectionChanged += delegate
		{
			if (c.SelectedIndex >= 0)
			{
				set(c.SelectedIndex);
				changed();
				if (rebuild)
				{
					Dispatcher.UIThread.Post(Build);
				}
			}
		};
		return c;
	}

	private Control Slide(double min, double max, double step, double value, Action<double> set, Func<double, string> show)
	{
		TextBlock text = Ui.Text(show(value), 12.5, p.Soft, FontWeight.Medium, numbers: true);
		text.Width = 52.0;
		text.TextAlignment = TextAlignment.Right;
		Slider slider = new Slider
		{
			Minimum = min,
			Maximum = max,
			Value = value,
			TickFrequency = step,
			IsSnapToTickEnabled = true,
			Width = 150.0
		};
		slider.ValueChanged += delegate(object? _, RangeBaseValueChangedEventArgs e)
		{
			text.Text = show(e.NewValue);
			set(e.NewValue);
			changed();
		};
		return Ui.Row(10.0, slider, text);
	}
}




