using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Lumen.Core;

namespace Lumen.App;

public sealed class Controller
{
	private readonly IClassicDesktopStyleApplicationLifetime desktop;

	private readonly Platform platform = new Platform();

	private readonly Settings settings;

	private readonly History history = new History();

	private readonly SensorHold hold = new SensorHold();

	private readonly ISnapshotSource source;

	private readonly bool demo;

	private readonly DispatcherTimer timer = new DispatcherTimer();

	private readonly DispatcherTimer weatherTimer = new DispatcherTimer
	{
		Interval = TimeSpan.FromMinutes(20.0)
	};

	private readonly WarningTracker warnings = new WarningTracker();

	private readonly UsageMeter usage;

	private readonly IMediaSource? media;

	private PingMonitor? ping;

	private FpsMonitor? fps;

	private SidebarWindow? window;

	private SettingsWindow? settingsWindow;

	private TrayIcon? tray;

	private Snapshot? last;

	private WeatherInfo? weather;

	private bool reading;

	private string trayLanguage = "";

	private FreezeWatch? freezeWatch;

	private const int HiddenIntervalMs = 5000;

	public Controller(IClassicDesktopStyleApplicationLifetime desktop, bool demo)
	{
		this.desktop = desktop;
		this.demo = demo || !OperatingSystem.IsWindows();
		settings = Settings.Load(platform.SettingsPath);
		Diagnostics.SetSettingsPath(platform.SettingsPath);
		L.Use(settings.Language);
		ISnapshotSource snapshotSource2;
		if (!this.demo)
		{
			ISnapshotSource snapshotSource = new WindowsSource(platform.IsAdministrator, () => settings.SensorSource);
			snapshotSource2 = snapshotSource;
		}
		else
		{
			ISnapshotSource snapshotSource = new DemoSource();
			snapshotSource2 = snapshotSource;
		}
		source = snapshotSource2;
		usage = new UsageMeter(this.demo ? null : Path.Combine(Path.GetDirectoryName(platform.SettingsPath) ?? ".", "lumen-usage.json"));
		if (!this.demo)
		{
			media = new WindowsMedia();
		}
		NotesCard.Changed += delegate
		{
			Save();
		};
		MediaCard.Command += delegate(string c)
		{
			media?.Send(c);
		};
	}

	public void Start()
	{
		CreateTray();
		Rebuild();
		Application current = Application.Current;
		if (current != null)
		{
			current.ActualThemeVariantChanged += delegate
			{
				if (settings.Theme == ThemeMode.System)
				{
					Rebuild();
				}
			};
			IPlatformSettings platformSettings = current.PlatformSettings;
			if (platformSettings != null)
			{
				platformSettings.ColorValuesChanged += delegate
				{
					if (settings.Colors == "windows")
					{
						Rebuild();
					}
				};
			}
		}
		freezeWatch = new FreezeWatch(FreezeWatch.RestartNow, Path.Combine(Path.GetDirectoryName(platform.SettingsPath) ?? ".", "lumen-restarts.txt"));
		freezeWatch.Start();
		timer.Tick += delegate
		{
			Tick();
		};
		timer.Interval = TimeSpan.FromMilliseconds(settings.IntervalMs);
		timer.Start();
		weatherTimer.Tick += delegate
		{
			RefreshWeather();
		};
		weatherTimer.Start();
		DispatcherTimer.RunOnce(Compact, TimeSpan.FromSeconds(45.0));
		RefreshWeather();
		Tick();
	}

	private Palette CurrentPalette()
	{
		bool dark = settings.Theme switch
		{
			ThemeMode.Dark => true, 
			ThemeMode.Light => false, 
			_ => Application.Current?.ActualThemeVariant != ThemeVariant.Light, 
		};
		return Palette.For(settings, dark, Application.Current?.PlatformSettings?.GetColorValues().AccentColor1);
	}

	private void Rebuild()
	{
		Diagnostics.SetLayout(settings, 1, "rebuild sidebar");
		Palette palette = CurrentPalette();
		Ui.UseFont(settings.Font);
		Motion.Enabled = settings.SmoothTransitions;
		Motion.ScrollDuration = Math.Min(900.0, (double)settings.IntervalMs * 0.9);
		Motion.Duration = Math.Min(450.0, (double)settings.IntervalMs * 0.6);
		Application current = Application.Current;
		if (current != null)
		{
			Application application = current;
			application.RequestedThemeVariant = settings.Theme switch
			{
				ThemeMode.Dark => ThemeVariant.Dark, 
				ThemeMode.Light => ThemeVariant.Light, 
				_ => ThemeVariant.Default, 
			};
		}
		if (window == null)
		{
			window = new SidebarWindow();
		}
		SidebarView sidebarView = new SidebarView(settings, palette);
		sidebarView.SettingsRequested += OpenSettings;
		sidebarView.ElevateRequested += platform.RestartElevated;
		sidebarView.LayoutChanged += Save;
		window.Apply(settings, sidebarView);
		if (!window.IsVisible)
		{
			window.Show();
		}
		if (last != null)
		{
			sidebarView.Update(last, history);
		}
		timer.Interval = TimeSpan.FromMilliseconds(settings.IntervalMs);
		if (trayLanguage != L.Language)
		{
			CreateTray();
		}
	}

	private async void Tick()
	{
		if (reading)
		{
			return;
		}
		reading = true;
		try
		{
			window?.CheckFullscreen(settings.HideInFullscreen);
			bool shown = window?.IsVisible ?? false;
			TimeSpan timeSpan = TimeSpan.FromMilliseconds(shown ? settings.IntervalMs : Math.Max(settings.IntervalMs, 5000));
			if (timer.Interval != timeSpan)
			{
				timer.Interval = timeSpan;
			}
			if (source is WindowsSource windowsSource)
			{
				windowsSource.WantsProcesses = shown && settings.Widgets.Any((WidgetSlot w) => w.Visible && w.Kind == WidgetKind.Processes);
				windowsSource.WantsSystem = shown && settings.Widgets.Any((WidgetSlot w) => w.Visible && w.Kind == WidgetKind.System);
			}
			Diagnostics.SetStage("read sensors");
			Snapshot snapshot = await Task.Run((Func<Snapshot>)source.Read);
			// A sensor that reports nothing for a moment keeps its last value, so no line or tile vanishes
			// for a second and the widgets below it do not jump.
			snapshot = hold.Apply(snapshot);
			Diagnostics.SetStage("update widgets");
			if (!demo || weather != null)
			{
				snapshot = snapshot with
				{
					Weather = weather
				};
			}
			if (!demo)
			{
				bool num = settings.Widgets.Any((WidgetSlot w) => w.Visible && w.Kind == WidgetKind.Ping);
				if (num && ping == null)
				{
					ping = new PingMonitor(settings.PingTarget);
				}
				if (!num && ping != null)
				{
					ping.Dispose();
					ping = null;
				}
				if (ping != null && ping.Target != settings.PingTarget)
				{
					ping.Target = settings.PingTarget;
				}
				bool num2 = settings.Widgets.Any((WidgetSlot w) => w.Visible && w.Kind == WidgetKind.Fps) && platform.IsAdministrator;
				if (num2 && fps == null)
				{
					fps = new FpsMonitor();
					fps.Start();
				}
				if (!num2 && fps != null)
				{
					fps.Dispose();
					fps = null;
				}
				snapshot = snapshot with
				{
					Ping = ping?.Read(),
					Usage = usage.Record(snapshot, settings.BaseWatts),
					Media = media?.Read(),
					Fps = fps?.Read()
				};
			}
			history.Record(snapshot);
			last = snapshot;
			if (shown)
			{
				window?.View?.Update(snapshot, history);
			}
			if (!settings.WarningPopup)
			{
				return;
			}
			SidebarWindow sidebarWindow = window;
			if (sidebarWindow == null || !sidebarWindow.IsVisible)
			{
				return;
			}
			foreach (Warning item in warnings.Check(snapshot, settings.TemperatureWarning))
			{
				new Toast(CurrentPalette(), L.T("Temperature warning"), L.F("{0} is at {1} – above your limit of {2}.", item.Name, Format.Temperature(item.Celsius, settings.Fahrenheit) + " " + Format.TemperatureUnit(settings.Fahrenheit), Format.Temperature(settings.TemperatureWarning, settings.Fahrenheit) + " " + Format.TemperatureUnit(settings.Fahrenheit))).ShowBeside(window, settings.Edge == Edge.Right);
			}
		}
		catch (Exception e)
		{
			CrashLog.Write(e, "sensor/widget update");
		}
		finally
		{
			Diagnostics.SetStage("idle");
			reading = false;
		}
	}

	private async void RefreshWeather()
	{
		double? weatherLatitude = settings.WeatherLatitude;
		if (weatherLatitude.HasValue)
		{
			double valueOrDefault = weatherLatitude.GetValueOrDefault();
			weatherLatitude = settings.WeatherLongitude;
			if (weatherLatitude.HasValue)
			{
				double valueOrDefault2 = weatherLatitude.GetValueOrDefault();
				try
				{
					weather = (await WeatherClient.ReadAsync(settings.WeatherPlace, valueOrDefault, valueOrDefault2)) ?? weather;
					return;
				}
				catch
				{
					return;
				}
			}
		}
		weather = null;
	}

	private void Changed()
	{
		Settings.Normalize(settings);
		L.Use(settings.Language);
		Save();
		Rebuild();
	}

	private void Save()
	{
		try
		{
			settings.Save(platform.SettingsPath);
		}
		catch
		{
		}
	}

	private void OpenSettings()
	{
		SettingsWindow settingsWindow = this.settingsWindow;
		if (settingsWindow != null && settingsWindow.IsVisible)
		{
			this.settingsWindow.Activate();
			return;
		}
		this.settingsWindow = new SettingsWindow(settings, CurrentPalette(), Changed, ScreenNames, platform, RefreshWeather, SensorReport, () => last);
		this.settingsWindow.Closed += delegate
		{
			this.settingsWindow = null;
		};
		this.settingsWindow.Show();
	}

	private string SensorReport()
	{
		string text = ((source is ISensorReport sensorReport) ? sensorReport.Report() : "Demo data – no sensors are read.\n");
		return text + "\n=== " + L.T("Shown in the sidebar") + " ===\n" + (((object)last == null) ? "—" : SnapshotText.Describe(last));
	}

	private IReadOnlyList<string> ScreenNames()
	{
		return (window?.Screens.All ?? Array.Empty<Screen>()).Select((Screen s, int i) => $"{L.F("Screen {0}", i + 1)} · {s.Bounds.Width}×{s.Bounds.Height}{(s.IsPrimary ? (" (" + L.T("main") + ")") : "")}").ToList();
	}

	private void CreateTray()
	{
		trayLanguage = L.Language;
		NativeMenu nativeMenu = new NativeMenu();
		NativeMenuItem nativeMenuItem = new NativeMenuItem(L.T("Show or hide"));
		nativeMenuItem.Click += delegate
		{
			if (window != null)
			{
				if (window.IsVisible)
				{
					window.Hide();
				}
				else
				{
					window.Show();
					Rebuild();
				}
			}
		};
		NativeMenuItem nativeMenuItem2 = new NativeMenuItem(L.T("Settings"));
		nativeMenuItem2.Click += delegate
		{
			OpenSettings();
		};
		NativeMenuItem nativeMenuItem3 = new NativeMenuItem(L.T("Create error report"));
		nativeMenuItem3.Click += delegate
		{
			CreateReport();
		};
		NativeMenuItem nativeMenuItem4 = new NativeMenuItem(L.T("Quit"));
		nativeMenuItem4.Click += delegate
		{
			Quit();
		};
		nativeMenu.Items.Add(nativeMenuItem);
		nativeMenu.Items.Add(nativeMenuItem2);
		nativeMenu.Items.Add(nativeMenuItem3);
		nativeMenu.Items.Add(new NativeMenuItemSeparator());
		nativeMenu.Items.Add(nativeMenuItem4);
		tray?.Dispose();
		tray = new TrayIcon
		{
			Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://Lumen/Assets/lumen.ico"))),
			ToolTipText = "Lumen",
			Menu = nativeMenu
		};
		tray.Clicked += delegate
		{
			OpenSettings();
		};
		TrayIcon.SetIcons(Application.Current, new TrayIcons { tray });
	}

	private static void Compact()
	{
		GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
		GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
	}

	private void CreateReport()
	{
		try
		{
			ErrorReport.Reveal(ErrorReport.Create(settings, SensorReport(), ScreenNames()));
		}
		catch (Exception e)
		{
			CrashLog.Write(e, "report");
		}
	}

	private void Quit()
	{
		freezeWatch?.Dispose();
		timer.Stop();
		weatherTimer.Stop();
		ping?.Dispose();
		fps?.Dispose();
		if (last != null)
		{
			usage.Save(last.Time);
		}
		window?.Release();
		tray?.Dispose();
		source.Dispose();
		desktop.Shutdown();
	}
}

