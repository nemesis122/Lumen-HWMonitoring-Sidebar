using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;

namespace Lumen.App;

public sealed class Toast : Window
{
	public Toast(Palette p, string titleText, string message)
	{
		base.SystemDecorations = SystemDecorations.None;
		base.CanResize = false;
		base.ShowInTaskbar = false;
		base.ShowActivated = false;
		base.Topmost = true;
		base.Width = 340.0;
		base.SizeToContent = SizeToContent.Height;
		base.Background = Brushes.Transparent;
		base.TransparencyLevelHint = new WindowTransparencyLevel[2]
		{
			WindowTransparencyLevel.AcrylicBlur,
			WindowTransparencyLevel.Transparent
		};
		Color color = p.Ramp[^1];
		TextBlock textBlock = Ui.Text(titleText, 13.5, p.Ink, FontWeight.DemiBold);
		TextBlock textBlock2 = Ui.Text(message, 12.5, p.Soft);
		textBlock2.TextWrapping = TextWrapping.Wrap;
		textBlock2.TextTrimming = TextTrimming.None;
		Control control = Ui.Glyph("M8,1.5 L13.5,3.5 V7.5 C13.5,11 11,13.5 8,14.5 C5,13.5 2.5,11 2.5,7.5 V3.5 Z M8,5 V8.5 M8,10.5 V11", color, 20.0);
		control.VerticalAlignment = VerticalAlignment.Top;
		StackPanel stackPanel = Ui.Stack(3.0, textBlock, textBlock2);
		stackPanel.Margin = new Thickness(12.0, 0.0, 0.0, 0.0);
		base.Content = new Border
		{
			Background = Palette.Brush(Palette.Mix(p.Dark ? Color.Parse("#171C25") : Colors.White, color, 0.08)),
			BorderBrush = Palette.Brush(Palette.Alpha(color, 0.55)),
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(12.0),
			Padding = new Thickness(16.0, 14.0),
			Child = Ui.Columns("Auto,*", control, stackPanel)
		};
		base.PointerPressed += delegate
		{
			Close();
		};
		DispatcherTimer.RunOnce(base.Close, TimeSpan.FromSeconds(9.0));
	}

	public void ShowBeside(Window sidebar, bool right)
	{
		Screen screen = sidebar.Screens.ScreenFromWindow(sidebar) ?? sidebar.Screens.Primary;
		Show();
		if ((object)screen != null)
		{
			double scaling = screen.Scaling;
			PixelRect workingArea = screen.WorkingArea;
			int num = (int)(base.Width * scaling);
			int num2 = (int)(base.Bounds.Height * scaling);
			int num3 = (int)(16.0 * scaling);
			int num4 = (int)(sidebar.Bounds.Width * scaling);
			int x = (right ? (workingArea.Right - num4 - num - num3) : (workingArea.X + num4 + num3));
			base.Position = new PixelPoint(x, workingArea.Bottom - num2 - num3);
		}
	}
}
