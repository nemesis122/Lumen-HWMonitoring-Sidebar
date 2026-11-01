using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using static Lumen.App.L;

namespace Lumen.App;

// Every sensor both libraries found, with values, so missing readings can be tracked down.
public sealed class SensorsWindow : Window
{
    public SensorsWindow(Palette p, Func<string> report, string saveFolder)
    {
        Title = "Lumen – " + T("Sensors");
        Width = 760; Height = 720; MinWidth = 520; MinHeight = 400;
        FontFamily = Ui.Inter;
        Background = Palette.Brush(p.Dark ? Color.Parse("#14181F") : Color.Parse("#F3F5F8"));
        RequestedThemeVariant = p.Dark ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var text = new TextBox
        {
            IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap,
            FontFamily = new FontFamily("Cascadia Mono, Consolas, Courier New, monospace"), FontSize = 12,
            Text = report(), CornerRadius = new CornerRadius(8)
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(text, Avalonia.Controls.Primitives.ScrollBarVisibility.Auto);
        var status = Ui.Text("", 12, p.Soft);
        Button Make(string label, Action click)
        {
            var b = new Button { Content = label, Padding = new Thickness(14, 7), CornerRadius = new CornerRadius(8), Margin = new Thickness(0, 0, 8, 0) };
            b.Click += (_, _) => click();
            return b;
        }
        var refresh = Make(T("Refresh"), () => { text.Text = report(); status.Text = ""; });
        var copy = Make(T("Copy"), async () =>
        {
            if (Clipboard != null) { await Clipboard.SetTextAsync(text.Text ?? ""); status.Text = T("Copied."); }
        });
        var save = Make(T("Save as file"), () =>
        {
            try
            {
                string path = Path.Combine(saveFolder, "lumen-sensors.txt");
                File.WriteAllText(path, text.Text ?? "");
                status.Text = F("Saved: {0}", path);
            }
            catch (Exception e) { status.Text = e.Message; }
        });
        var lead = Ui.Text(T("Everything LibreHardwareMonitor and Open Hardware Monitor found on this PC. If a value is missing in the sidebar, send this list."), 12.5, p.Soft);
        lead.TextWrapping = TextWrapping.Wrap;
        lead.TextTrimming = TextTrimming.None;
        var head = Ui.Stack(4, Ui.Text(T("Sensors"), 20, p.Ink, FontWeight.SemiBold), lead);
        head.Margin = new Thickness(20, 18, 20, 10);
        var buttons = Ui.Row(0, refresh, copy, save, status);
        status.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        buttons.Margin = new Thickness(20, 10, 20, 16);
        var dock = new DockPanel();
        DockPanel.SetDock(head, Dock.Top);
        DockPanel.SetDock(buttons, Dock.Bottom);
        dock.Children.Add(head);
        dock.Children.Add(buttons);
        text.Margin = new Thickness(20, 0, 20, 0);
        dock.Children.Add(text);
        Content = dock;
    }
}
