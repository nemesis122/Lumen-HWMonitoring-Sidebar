using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Headless;
using Avalonia.Media;
using Lumen.App;
using Lumen.Core;
using Xunit;

// Checks for the fixes made after 3.0.1-local-automatik-fix.
public class FitFixTests
{
    private static int Remembered(FitPanel fit)
    {
        int count = 0;
        foreach (var name in new[] { "baseHeight", "baseSpacing", "baseRows" })
        {
            var field = typeof(FitPanel).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!;
            count += ((System.Collections.ICollection)field.GetValue(fit)!).Count;
        }
        return count;
    }

    private static Control Card(int rows) =>
        new StackPanel
        {
            Tag = "elastic",
            Children =
            {
                new StatPanel(Array.Empty<double>(), Enumerable.Range(0, rows).Select(i => (Control)new TextBlock { Text = "Reading " + i }).ToArray()) { Tag = "elastic", MaxColumns = 2 },
                new AreaChart { Height = 30 },
            }
        };

    [Fact]
    public void Removed_cards_are_not_kept_alive()
    {
        int after = UiHost.Run(() =>
        {
            var column = new StackPanel();
            var fit = new FitPanel(new() { column }, 1, true, 10);
            var window = new Window { Width = 300, Height = 900, Content = fit };
            window.Show();
            for (int round = 0; round < 25; round++)
            {
                column.Children.Clear();
                column.Children.Add(Card(4));
                column.Children.Add(Card(6));
                Dispatcher.UIThread.RunJobs();
                window.CaptureRenderedFrame();
            }
            int n = Remembered(fit);
            window.Close();
            return n;
        });
        // Two cards with one elastic stack, one elastic row panel and one chart each.
        Assert.True(after <= 6, $"FitPanel still remembers {after} elements after 25 rebuilds");
    }

    [Fact]
    public void Reset_forgets_everything()
    {
        int after = UiHost.Run(() =>
        {
            var column = new StackPanel { Children = { Card(4) } };
            var fit = new FitPanel(new() { column }, 1, true, 10);
            fit.Measure(new Size(300, 900));
            fit.Reset();
            return Remembered(fit);
        });
        Assert.Equal(0, after);
    }

    [Fact]
    public void Widgets_without_elastic_rows_do_not_drift_apart()
    {
        double spacing = UiHost.Run(() =>
        {
            var column = new StackPanel { Children = { new Border { Height = 40 }, new Border { Height = 40 }, new Border { Height = 40 } } };
            var fit = new FitPanel(new() { column }, 1, true, 10);
            fit.Measure(new Size(300, 900));
            fit.Arrange(new Rect(0, 0, 300, 900));
            return column.Spacing;
        });
        // Normal gap 10, at most 1.5 gaps extra: the rest stays below the last widget.
        Assert.InRange(spacing, 10, 25.01);
    }

    [Fact]
    public void Same_height_twice_keeps_the_same_scale()
    {
        var (first, second) = UiHost.Run(() =>
        {
            var column = new StackPanel { Children = { Card(8), Card(8), Card(8) } };
            var fit = new FitPanel(new() { column }, 1, true, 10);
            var window = new Window { Width = 220, Height = 260, Content = fit };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            window.CaptureRenderedFrame();
            double a = ((ScaleTransform)column.RenderTransform!).ScaleX;
            fit.InvalidateMeasure();
            Dispatcher.UIThread.RunJobs();
            window.CaptureRenderedFrame();
            double b = ((ScaleTransform)column.RenderTransform!).ScaleX;
            window.Close();
            return (a, b);
        });
        Assert.Equal(first, second, 6);
    }

    [Fact]
    public void Damaged_settings_are_kept_as_a_copy()
    {
        string dir = Path.Combine(Path.GetTempPath(), "lumen-broken-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, "lumen-settings.json");
            File.WriteAllText(path, "{ \"Width\": 333, \"Widgets\": [ broken");
            var loaded = Settings.Load(path);
            Assert.Equal(new Settings().Width, loaded.Width);
            var copies = Directory.GetFiles(dir, "lumen-settings.broken-*.json");
            Assert.Single(copies);
            Assert.Contains("333", File.ReadAllText(copies[0]));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Missing_settings_leave_no_copy()
    {
        string dir = Path.Combine(Path.GetTempPath(), "lumen-missing-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            Settings.Load(Path.Combine(dir, "lumen-settings.json"));
            Assert.Empty(Directory.GetFiles(dir));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Compact_cards_return_when_there_is_room_again()
    {
        var (squeezed, restored) = UiHost.Run(() =>
        {
            var settings = Render.Side(WidgetStyle.Standard);
            settings.Density = Density.Fit;
            foreach (var slot in settings.Widgets) slot.Visible = slot.Kind is WidgetKind.Cpu or WidgetKind.Memory;
            var (w, view) = Render.Show(settings, 300, 150);
            var (snap, hist) = Render.Demo();
            view.Update(snap, hist);
            var field = typeof(SidebarView).GetField("compacted", BindingFlags.NonPublic | BindingFlags.Instance)!;
            for (int i = 0; i < 4; i++) { Dispatcher.UIThread.RunJobs(); w.CaptureRenderedFrame(); }
            bool a = (bool)field.GetValue(view)!;
            w.Height = 1400;
            for (int i = 0; i < 6; i++) { Dispatcher.UIThread.RunJobs(); w.CaptureRenderedFrame(); }
            bool b = (bool)field.GetValue(view)!;
            w.Close();
            return (a, b);
        });
        Assert.True(squeezed, "a very low sidebar switches to compact cards");
        Assert.False(restored, "a much taller sidebar brings the normal cards back");
    }
}
