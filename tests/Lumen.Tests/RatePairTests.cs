using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lumen.App;
using Lumen.Core;
using Xunit;

// Network widget: in every design download and upload are shown the same way, in the same font size.
public class RatePairTests
{
    public static IEnumerable<object[]> Styles() => Settings.Selectable.Select(s => new object[] { s });

    [Theory]
    [MemberData(nameof(Styles))]
    public void Download_and_upload_have_the_same_size(WidgetStyle style)
    {
        var (down, up, texts) = UiHost.Run(() =>
        {
            L.Use("de");
            var source = new DemoSource();
            var history = new History();
            var settings = new Settings { Language = "de", Width = 330 };
            foreach (var w in settings.Widgets) { w.Visible = w.Kind == WidgetKind.Network; if (Settings.HasStyles(w.Kind)) w.Style = style; }
            var view = new SidebarView(settings, Palette.For(true, false));
            var window = new Window { Content = view, Width = 330, Height = 900 };
            window.Show();
            Snapshot snap = source.Read();
            for (int i = 0; i < 20; i++) { snap = source.Read(); history.Record(snap); }
            // Values far apart, so neither number can be part of the other or of another reading.
            snap = snap with { Network = snap.Network! with { DownloadBytesPerSecond = 47.3e6, UploadBytesPerSecond = 6.18e6 } };
            view.Update(snap, history);
            for (int k = 0; k < 3; k++) { Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame(); }
            var blocks = view.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && t.Opacity > 0 && !string.IsNullOrEmpty(t.Text)).ToList();
            double Size(string number) => blocks.Where(t => System.Text.RegularExpressions.Regex.IsMatch(t.Text!, @"(^|[^\d.])" + System.Text.RegularExpressions.Regex.Escape(number) + @"($|[^\d.])"))
                .Select(t => t.FontSize).DefaultIfEmpty(0).Max();
            string Number(double v) => Format.Split(Format.Rate(v)).Item1;
            var result = (Size(Number(47.3e6)), Size(Number(6.18e6)), string.Join(" | ", blocks.Select(t => $"{t.Text}@{t.FontSize:0.#}")));
            window.Close();
            return result;
        });
        Assert.True(down > 0 && up > 0, $"{style}: download or upload missing\n{texts}");
        Assert.True(Math.Abs(down - up) < 0.01, $"{style}: download {down:0.#} px, upload {up:0.#} px\n{texts}");
    }
}
