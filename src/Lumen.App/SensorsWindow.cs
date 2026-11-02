using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace Lumen.App;

public sealed class SensorsWindow : Window
{
	public SensorsWindow(Palette p, Func<string> report, string saveFolder)
	{
		SensorsWindow sensorsWindow = this;
		base.Title = "Lumen – " + L.T("Sensors");
		base.Width = 760.0;
		base.Height = 720.0;
		base.MinWidth = 520.0;
		base.MinHeight = 400.0;
		base.FontFamily = Ui.Inter;
		base.Background = Palette.Brush(p.Dark ? Color.Parse("#14181F") : Color.Parse("#F3F5F8"));
		base.RequestedThemeVariant = (p.Dark ? ThemeVariant.Dark : ThemeVariant.Light);
		base.WindowStartupLocation = WindowStartupLocation.CenterScreen;
		TextBox text = new TextBox
		{
			IsReadOnly = true,
			AcceptsReturn = true,
			TextWrapping = TextWrapping.NoWrap,
			FontFamily = new FontFamily("Cascadia Mono, Consolas, Courier New, monospace"),
			FontSize = 12.0,
			Text = report(),
			CornerRadius = new CornerRadius(8.0)
		};
		ScrollViewer.SetHorizontalScrollBarVisibility(text, ScrollBarVisibility.Auto);
		TextBlock status = Ui.Text("", 12.0, p.Soft);
		Button button = Make(L.T("Refresh"), delegate
		{
			text.Text = report();
			status.Text = "";
		});
		Button button2 = Make(L.T("Copy"), async delegate
		{
			if (sensorsWindow.Clipboard != null)
			{
				await sensorsWindow.Clipboard.SetTextAsync(text.Text ?? "");
				status.Text = L.T("Copied.");
			}
		});
		Button button3 = Make(L.T("Save as file"), delegate
		{
			try
			{
				string text2 = Path.Combine(saveFolder, "lumen-sensors.txt");
				File.WriteAllText(text2, text.Text ?? "");
				status.Text = L.F("Saved: {0}", text2);
			}
			catch (Exception ex)
			{
				status.Text = ex.Message;
			}
		});
		TextBlock textBlock = Ui.Text(L.T("Everything LibreHardwareMonitor and Open Hardware Monitor found on this PC. If a value is missing in the sidebar, send this list."), 12.5, p.Soft);
		textBlock.TextWrapping = TextWrapping.Wrap;
		textBlock.TextTrimming = TextTrimming.None;
		StackPanel stackPanel = Ui.Stack(4.0, Ui.Text(L.T("Sensors"), 20.0, p.Ink, FontWeight.DemiBold), textBlock);
		stackPanel.Margin = new Thickness(20.0, 18.0, 20.0, 10.0);
		StackPanel stackPanel2 = Ui.Row(0.0, button, button2, button3, status);
		status.VerticalAlignment = VerticalAlignment.Center;
		stackPanel2.Margin = new Thickness(20.0, 10.0, 20.0, 16.0);
		DockPanel dockPanel = new DockPanel();
		DockPanel.SetDock(stackPanel, Dock.Top);
		DockPanel.SetDock(stackPanel2, Dock.Bottom);
		dockPanel.Children.Add(stackPanel);
		dockPanel.Children.Add(stackPanel2);
		text.Margin = new Thickness(20.0, 0.0, 20.0, 0.0);
		dockPanel.Children.Add(text);
		base.Content = dockPanel;
		static Button Make(string label, Action click)
		{
			Button button4 = new Button();
			button4.Content = label;
			button4.Padding = new Thickness(14.0, 7.0);
			button4.CornerRadius = new CornerRadius(8.0);
			button4.Margin = new Thickness(0.0, 0.0, 8.0, 0.0);
			button4.Click += delegate
			{
				click();
			};
			return button4;
		}
	}
}
