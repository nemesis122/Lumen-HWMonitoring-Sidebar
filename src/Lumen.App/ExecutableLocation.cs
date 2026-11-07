using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Lumen.App;

public static class ExecutableLocation
{
    public static Control Create(Palette palette, bool showButton)
    {
        var path = Platform.ExePath;
        var text = Ui.Text(path, 11.5, palette.Soft);
        text.TextTrimming = TextTrimming.CharacterEllipsis;
        text.MaxLines = 1;
        ToolTip.SetTip(text, path);
        var open = new MenuItem { Header = L.T("Open folder") };
        open.Click += (_, _) => ErrorReport.Reveal(path);
        var copy = new MenuItem { Header = L.T("Copy path") };
        copy.Click += async (_, _) => {
            try {
                var clipboard = TopLevel.GetTopLevel(text)?.Clipboard;
                if (clipboard != null) await clipboard.SetTextAsync(path);
            } catch (Exception e) { CrashLog.Write(e, "copy executable path"); }
        };
        text.ContextMenu = new ContextMenu { ItemsSource = new[] { open, copy } };
        var content = Ui.Stack(4, Ui.Text(L.T("Executable"), 11.5, palette.Soft, FontWeight.DemiBold), text);
        if (showButton) {
            var button = new Button { Content = L.T("Open folder"), Padding = new Thickness(12,6),
                CornerRadius = new CornerRadius(8), HorizontalAlignment = HorizontalAlignment.Left };
            button.Click += (_, _) => ErrorReport.Reveal(path);
            content.Children.Add(button);
        }
        return content;
    }
}
