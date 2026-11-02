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
		base.TransparencyLevelHint = ((!s.Glass) ? new WindowTransparencyLevel[1] { WindowTransparencyLevel.None } : new WindowTransparencyLevel[3]
		{
			WindowTransparencyLevel.AcrylicBlur,
			WindowTransparencyLevel.Blur,
			WindowTransparencyLevel.Transparent
		});
		Place(s);
		ClickThrough(s.ClickThrough);
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
			if (s.ReserveSpace && (!s.FullScreen || s.Horizontal))
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
