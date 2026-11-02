using System.Collections.Generic;
using System.Runtime.InteropServices;
using Lumen.Core;

namespace Lumen.App;

public static class AppBar
{
	private struct Rect
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;
	}

	private struct Data
	{
		public int cbSize;

		public nint hWnd;

		public uint uCallbackMessage;

		public uint uEdge;

		public Rect rc;

		public nint lParam;
	}

	private const uint New = 0u;

	private const uint Remove = 1u;

	private const uint QueryPos = 2u;

	private const uint SetPos = 3u;

	private static readonly HashSet<nint> registered = new HashSet<nint>();

	[DllImport("shell32.dll")]
	private static extern nuint SHAppBarMessage(uint msg, ref Data data);

	[DllImport("user32.dll")]
	private static extern uint RegisterWindowMessage(string name);

	public static (int X, int Y, int W, int H) Reserve(nint hwnd, Edge edge, int left, int top, int rightEdge, int bottom, int size)
	{
		uint side = edge switch
		{
			Edge.Left => 0u, 
			Edge.Top => 1u, 
			Edge.Right => 2u, 
			_ => 3u, 
		};
		Data d = new Data
		{
			cbSize = Marshal.SizeOf<Data>(),
			hWnd = hwnd,
			uEdge = side
		};
		if (registered.Add(hwnd))
		{
			d.uCallbackMessage = RegisterWindowMessage("LumenAppBar");
			SHAppBarMessage(0u, ref d);
		}
		d.rc = new Rect
		{
			Left = left,
			Top = top,
			Right = rightEdge,
			Bottom = bottom
		};
		Fit();
		SHAppBarMessage(2u, ref d);
		Fit();
		SHAppBarMessage(3u, ref d);
		return (X: d.rc.Left, Y: d.rc.Top, W: d.rc.Right - d.rc.Left, H: d.rc.Bottom - d.rc.Top);
		void Fit()
		{
			switch (side)
			{
			case 0u:
				d.rc.Right = d.rc.Left + size;
				break;
			case 1u:
				d.rc.Bottom = d.rc.Top + size;
				break;
			case 2u:
				d.rc.Left = d.rc.Right - size;
				break;
			default:
				d.rc.Top = d.rc.Bottom - size;
				break;
			}
		}
	}

	public static void Release(nint hwnd)
	{
		if (registered.Remove(hwnd))
		{
			Data data = new Data
			{
				cbSize = Marshal.SizeOf<Data>(),
				hWnd = hwnd
			};
			SHAppBarMessage(1u, ref data);
		}
	}
}
