using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using Lumen.Core;

namespace Lumen.App;

public sealed class SidebarWindow : Window
{
	private bool reserved;

	private Settings? settings;

	private bool addedLayered;

	private PixelPoint target;

	public SidebarView? View { get; private set; }

	public SidebarWindow()
	{
		base.Title = "Lumen";
		base.SystemDecorations = SystemDecorations.None;
		base.CanResize = false;
		base.ShowInTaskbar = false;
		base.ShowActivated = false;
		base.Background = Brushes.Transparent;
		base.Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://Lumen/Assets/lumen.ico")));
		base.ScalingChanged += delegate
		{
			if (settings != null)
			{
				Place(settings);
			}
		};
		base.Resized += delegate
		{
			Dispatcher.UIThread.Post(delegate
			{
				if (settings != null && base.Position != target)
				{
					base.Position = target;
				}
			}, DispatcherPriority.Background);
		};
		base.Opened += delegate
		{
			if (settings != null)
			{
				Place(settings);
			}
		};
	}

	public void Apply(Settings s, SidebarView view)
	{
		settings = s;
		View = view;
		base.Content = view;
		base.Topmost = s.Topmost;
		base.Background = s.Floating ? Brushes.Transparent : null;
		base.TransparencyLevelHint = (s.Floating ? new WindowTransparencyLevel[1] { WindowTransparencyLevel.Transparent } : (!s.Glass) ? new WindowTransparencyLevel[1] { WindowTransparencyLevel.None } : new WindowTransparencyLevel[3]
		{
			WindowTransparencyLevel.AcrylicBlur,
			WindowTransparencyLevel.Blur,
			WindowTransparencyLevel.Transparent
		});
		Place(s);
		ClickThrough(s.ClickThrough);
		Gaps(s.Floating && !s.ClickThrough);
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct CursorPoint
	{
		public int X, Y;
	}

	[DllImport("user32.dll")]
	private static extern bool GetCursorPos(out CursorPoint point);

	private DispatcherTimer? gapTimer;

	private bool passing;

	// Floating: the window lets the mouse through wherever there is no card under it. Windows cannot tell
	// that from the transparent pixels, so the cursor is checked a few times per second and the window
	// switches between catching and passing clicks.
	private void Gaps(bool on)
	{
		if (!OperatingSystem.IsWindows())
		{
			return;
		}
		if (!on)
		{
			gapTimer?.Stop();
			if (passing)
			{
				Pass(false);
			}
			return;
		}
		if (gapTimer == null)
		{
			gapTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(60), DispatcherPriority.Input, delegate
			{
				CheckGaps();
			});
		}
		gapTimer.Start();
	}

	private void CheckGaps()
	{
		if (View == null || !base.IsVisible || !GetCursorPos(out CursorPoint c))
		{
			return;
		}
		double scaling = base.RenderScaling;
		Point point = new Point((c.X - base.Position.X) / scaling, (c.Y - base.Position.Y) / scaling);
		bool inside = point.X >= 0 && point.Y >= 0 && point.X < base.Bounds.Width && point.Y < base.Bounds.Height;
		bool pass = !inside || !View.IsOverCard(point);
		if (pass != passing)
		{
			Pass(pass);
		}
	}

	private void Pass(bool on)
	{
		nint handle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
		if (handle == IntPtr.Zero)
		{
			return;
		}
		int style = GetWindowLong(handle, -20);
		int next = on ? (style | 0x20 | 0x80000) : (style & ~0x20);
		if (next != style)
		{
			SetWindowLong(handle, -20, next);
			if (on && (style & 0x80000) == 0)
			{
				SetLayeredWindowAttributes(handle, 0u, byte.MaxValue, 2u);
			}
		}
		passing = on;
	}

	[DllImport("user32.dll")]
	private static extern int GetWindowLong(nint hWnd, int index);

	[DllImport("user32.dll")]
	private static extern int SetWindowLong(nint hWnd, int index, int value);

	private void ClickThrough(bool on)
	{
		if (!OperatingSystem.IsWindows())
		{
			return;
		}
		nint num = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
		if (num == IntPtr.Zero)
		{
			if (on)
			{
				base.Opened += delegate
				{
					ClickThrough(on: true);
				};
			}
			return;
		}
		int windowLong = GetWindowLong(num, -20);
		int num2 = (on ? (windowLong | 0x20 | 0x80000) : (windowLong & -33 & ~(addedLayered ? 524288 : 0)));
		if (num2 != windowLong)
		{
			if (on && (windowLong & 0x80000) == 0)
			{
				addedLayered = true;
			}
			SetWindowLong(num, -20, num2);
			if (on && (num2 & 0x80000) != 0)
			{
				SetLayeredWindowAttributes(num, 0u, byte.MaxValue, 2u);
			}
			if (!on)
			{
				addedLayered = false;
			}
		}
	}

	[DllImport("user32.dll")]
	private static extern bool SetLayeredWindowAttributes(nint hWnd, uint key, byte alpha, uint flags);

	[StructLayout(LayoutKind.Sequential)]
	private struct Rect32
	{
		public int Left, Top, Right, Bottom;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct MonitorInfo
	{
		public int Size;
		public Rect32 Monitor;
		public Rect32 Work;
		public uint Flags;
	}

	[DllImport("user32.dll")]
	private static extern nint GetForegroundWindow();

	[DllImport("user32.dll")]
	private static extern bool GetWindowRect(nint hWnd, out Rect32 rect);

	[DllImport("user32.dll")]
	private static extern nint MonitorFromWindow(nint hWnd, uint flags);

	[DllImport("user32.dll")]
	private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern int GetClassName(nint hWnd, System.Text.StringBuilder name, int max);

	[DllImport("user32.dll")]
	private static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);

	[DllImport("user32.dll")]
	private static extern bool IsZoomed(nint hWnd);

	[DllImport("user32.dll")]
	private static extern bool ShowWindow(nint hWnd, int command);

	private bool covered;

	// Hides the sidebar while another program fills the monitor the sidebar is on (a game or a video in full
	// screen). A program in full screen on the other monitor leaves the sidebar alone. The window is hidden
	// and shown again without taking the focus away from the game.
	public void CheckFullscreen(bool enabled)
	{
		if (!OperatingSystem.IsWindows())
		{
			return;
		}
		nint own = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
		if (own == IntPtr.Zero || !base.IsVisible)
		{
			return;
		}
		bool cover = false;
		try
		{
			cover = enabled && FullscreenOnSameMonitor(own);
		}
		catch
		{
		}
		if (cover != covered)
		{
			covered = cover;
			ShowWindow(own, cover ? 0 : 4);
		}
	}

	private static bool FullscreenOnSameMonitor(nint own)
	{
		nint fg = GetForegroundWindow();
		if (fg == IntPtr.Zero || fg == own)
		{
			return false;
		}
		GetWindowThreadProcessId(fg, out uint pid);
		if (pid == (uint)Environment.ProcessId)
		{
			return false;
		}
		System.Text.StringBuilder name = new System.Text.StringBuilder(64);
		GetClassName(fg, name, name.Capacity);
		switch (name.ToString())
		{
		case "Progman":
		case "WorkerW":
		case "Shell_TrayWnd":
		case "Shell_SecondaryTrayWnd":
			return false;
		}
		nint monitor = MonitorFromWindow(fg, 2u);
		if (monitor == IntPtr.Zero || monitor != MonitorFromWindow(own, 2u))
		{
			return false;
		}
		MonitorInfo info = new MonitorInfo
		{
			Size = Marshal.SizeOf<MonitorInfo>()
		};
		if (!GetMonitorInfo(monitor, ref info) || !GetWindowRect(fg, out Rect32 r))
		{
			return false;
		}
		bool fills = r.Left <= info.Monitor.Left && r.Top <= info.Monitor.Top && r.Right >= info.Monitor.Right && r.Bottom >= info.Monitor.Bottom;
		// A normal maximised window with a title bar is not full screen, even with an auto-hiding taskbar.
		bool caption = (GetWindowLong(fg, -16) & 0x00C00000) == 0x00C00000;
		return fills && !(caption && IsZoomed(fg));
	}

	private void Place(Settings s)
	{
		IReadOnlyList<Screen> all = base.Screens.All;
		if (all.Count == 0)
		{
			base.Width = SidebarView.WindowWidth(s);
			base.Height = 900.0;
			return;
		}
		Screen screen = all[Math.Clamp(s.Screen, 0, all.Count - 1)];
		double scaling = screen.Scaling;
		PixelRect workingArea = screen.WorkingArea;
		nint num = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
		double num3;
		int num4;
		int x;
		int y;
		int num2;
		if (s.Horizontal)
		{
			num2 = (s.BarFullWidth ? workingArea.Width : Math.Min(workingArea.Width, (int)Math.Round(SidebarView.WindowWidth(s) * scaling)));
			num3 = (double)num2 / scaling;
			num4 = Math.Min(workingArea.Height, (int)Math.Round((s.BarHeight * (double)s.Rows + (double)(s.Rows - 1) * Metrics.For(s.Density).Gap) * scaling));
			x = workingArea.X + (workingArea.Width - num2) / 2;
			y = ((s.Edge == Edge.Top) ? workingArea.Y : (workingArea.Bottom - num4));
		}
		else
		{
			num3 = (s.FullScreen ? ((double)workingArea.Width / scaling) : SidebarView.WindowWidth(s));
			num2 = (s.FullScreen ? workingArea.Width : ((int)Math.Round(num3 * scaling)));
			x = (s.FullScreen ? workingArea.X : ((s.Edge == Edge.Right) ? (workingArea.Right - num2) : workingArea.X));
			y = workingArea.Y;
			num4 = workingArea.Height;
		}
		if (OperatingSystem.IsWindows() && num != IntPtr.Zero)
		{
			if (s.ReserveSpace && !s.Floating && (!s.FullScreen || s.Horizontal))
			{
				PixelRect bounds = screen.Bounds;
				if (!s.Horizontal)
				{
					(x, y, _, num4) = AppBar.Reserve(num, s.Edge, bounds.X, workingArea.Y, bounds.Right, workingArea.Bottom, num2);
				}
				else
				{
					int num5;
					int num6;
					(num5, y, num6, num4) = AppBar.Reserve(num, s.Edge, bounds.X, bounds.Y, bounds.Right, bounds.Bottom, num4);
					if (s.BarFullWidth)
					{
						x = num5;
						num2 = num6;
						num3 = (double)num2 / scaling;
					}
				}
				reserved = true;
			}
			else if (reserved)
			{
				AppBar.Release(num);
				reserved = false;
			}
		}
		if (s.Floating)
		{
			// Floating: the window covers the whole working area; the cards are placed on it and clicks
			// between them go to the windows behind.
			x = workingArea.X;
			y = workingArea.Y;
			num2 = workingArea.Width;
			num4 = workingArea.Height;
			num3 = (double)num2 / scaling;
		}
		base.Width = num3;
		base.Height = (double)num4 / scaling;
		base.Position = new PixelPoint(x, y);
		target = new PixelPoint(x, y);
		Dispatcher.UIThread.Post(delegate
		{
			if (base.Position != target)
			{
				base.Position = target;
			}
		}, DispatcherPriority.Background);
	}

	public void Release()
	{
		nint num = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
		if (reserved && num != IntPtr.Zero && OperatingSystem.IsWindows())
		{
			AppBar.Release(num);
		}
		reserved = false;
	}

	protected override void OnClosed(EventArgs e)
	{
		Release();
		base.OnClosed(e);
	}
}
