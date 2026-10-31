using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Lumen.Core;
using static Lumen.App.L;
using Ellipse = Avalonia.Controls.Shapes.Ellipse;

namespace Lumen.App;

// The whole sidebar surface: a slim top bar, an optional access hint, then the cards.
// It knows nothing about windows, so the same view renders on the desktop and in screenshots.
public sealed class SidebarView : Border
{
    public event Action? SettingsRequested;
    public event Action? ElevateRequested;

    private readonly Palette p;
    private readonly Settings settings;
    private readonly List<IWidget> widgets = new();
    private readonly Border hint;
    private readonly Ellipse statusDot;
    private readonly TextBlock statusText;
    private readonly List<StackPanel> lists = new();
    private readonly FitPanel fit;
    private int gpuCount = 1;
    private Metrics metrics;
    private bool compacted;
    private Snapshot? last; private History? lastHistory;

    public SidebarView(Settings settings, Palette palette)
    {
        p = palette;
        this.settings = settings;
        metrics = Metrics.For(settings.Density);
        var m = metrics;
        Background = Palette.Brush(p.Surface);
        BorderBrush = Palette.Brush(p.Stroke);
        BorderThickness = settings.Edge == Edge.Right ? new Thickness(1, 0, 0, 0) : new Thickness(0, 0, 1, 0);

        // Top bar: wordmark, live state, settings.
        var mark = new Avalonia.Controls.Shapes.Path
        {
            Data = StreamGeometry.Parse("M3.2,12.8 A6.5,6.5 0 1 1 12.8,12.8"),
            Stroke = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(p.Ramp[0], 0), new GradientStop(p.Ramp[1], .55), new GradientStop(p.Ramp[2], 1) }
            },
            StrokeThickness = 2.6, StrokeLineCap = PenLineCap.Round, Width = 16, Height = 16, Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Center
        };
        var word = Ui.Text("lumen", 15, p.Ink, FontWeight.SemiBold);
        word.LetterSpacing = .2;
        statusDot = new Ellipse { Width = 6, Height = 6, VerticalAlignment = VerticalAlignment.Center };
        statusText = Ui.Text("", 10.5, p.Soft, FontWeight.SemiBold);
        var status = new Border
        {
            Background = Palette.Brush(p.Track),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(7, 2, 8, 2),
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = Ui.Row(5, statusDot, statusText)
        };
        var gear = new Button
        {
            Content = Ui.Glyph(Glyphs.Gear, p.Soft, 15),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(6),
            CornerRadius = new CornerRadius(8),
            Cursor = new Cursor(StandardCursorType.Hand),
            VerticalAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(gear, T("Settings"));
        gear.Click += (_, _) => SettingsRequested?.Invoke();
        var bar = Ui.Columns("Auto,Auto,Auto,*,Auto", mark, word, status, new Control(), gear);
        word.Margin = new Thickness(7, 0, 0, 1);

        // Shown only when temperatures are out of reach.
        var hintText = Ui.Text(T("Temperatures need administrator rights and the PawnIO driver."), 11.5, p.Ink);
        hintText.TextWrapping = TextWrapping.Wrap;
        hintText.TextTrimming = TextTrimming.None;
        var elevate = new Button
        {
            Content = T("Restart as administrator"), FontSize = 11.5, FontWeight = FontWeight.SemiBold,
            Padding = new Thickness(10, 4), CornerRadius = new CornerRadius(7), Margin = new Thickness(0, 8, 0, 0),
            Background = Palette.Brush(Palette.Alpha(p.Ramp[2], .28)), Foreground = Palette.Brush(p.Ink)
        };
        elevate.Click += (_, _) => ElevateRequested?.Invoke();
        var shield = Ui.Glyph(Glyphs.Shield, p.Ramp[2], 16);
        shield.VerticalAlignment = VerticalAlignment.Top;
        var hintBody = Ui.Stack(0, hintText, elevate);
        hintBody.Margin = new Thickness(10, 0, 0, 0);
        hint = new Border
        {
            Background = Palette.Brush(Palette.Alpha(p.Ramp[2], p.Dark ? .14 : .16)),
            BorderBrush = Palette.Brush(Palette.Alpha(p.Ramp[2], .35)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(m.Radius - 4),
            Padding = new Thickness(12, 10),
            IsVisible = false,
            Child = Ui.Columns("Auto,*", shield, hintBody)
        };

        for (int i = 0; i < settings.Columns; i++) lists.Add(new StackPanel());
        // "Automatic" fills the height exactly; the other densities scroll when there is too much.
        bool fill = settings.Density == Density.Fit;
        fit = new FitPanel(lists, settings.TextScale, fill, m.Gap);
        BuildCards();
        Control body = fill ? fit : new ScrollViewer
        {
            Content = fit,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden
        };
        var root = new DockPanel { Margin = new Thickness(m.Gap, m.Gap + 2, m.Gap, m.Gap) };
        DockPanel.SetDock(bar, Dock.Top);
        bar.Margin = new Thickness(6, 0, 0, m.Gap + 2);
        root.Children.Add(bar);
        root.Children.Add(body);
        Child = root;

        // With many widgets, comfortable sizes squeezed too far read worse than compact ones.
        if (fill) fit.Fitted += scale =>
        {
            if (scale >= .8 || compacted) return;
            compacted = true;
            Dispatcher.UIThread.Post(() => { metrics = Metrics.For(Density.Compact); BuildCards(); if (last != null) Update(last, lastHistory!); });
        };
    }

    public static double WindowWidth(Settings s) => s.Width * s.Columns + (s.Columns - 1) * Metrics.For(s.Density).Gap;

    private void BuildCards()
    {
        widgets.Clear();
        var columns = settings.Layout();
        for (int c = 0; c < lists.Count; c++)
        {
            var list = lists[c];
            list.Children.Clear();
            list.Spacing = metrics.Gap;
            if (c == 0) list.Children.Add(hint);
            // Each graphics adapter is its own widget, so ring tiles can pair up with anything.
            var items = new List<(WidgetSlot slot, int index)>();
            foreach (var slot in c < columns.Count ? columns[c] : new())
                for (int i = 0; i < (slot.Kind == WidgetKind.Gpu ? gpuCount : 1); i++) items.Add((slot, i));
            for (int i = 0; i < items.Count; i++)
            {
                var (slot, index) = items[i];
                var card = Cards.Create(slot.Kind, slot.Style, index, p, metrics, settings);
                widgets.Add((IWidget)card);
                // Two ring tiles in a row share one line.
                if (slot.Style == WidgetStyle.Ring && card is RingTile && i + 1 < items.Count && items[i + 1].slot.Style == WidgetStyle.Ring && Settings.HasStyles(items[i + 1].slot.Kind))
                {
                    var next = Cards.Create(items[i + 1].slot.Kind, WidgetStyle.Ring, items[i + 1].index, p, metrics, settings);
                    widgets.Add((IWidget)next);
                    var pair = new Grid { ColumnDefinitions = new ColumnDefinitions($"*,{metrics.Gap},*") };
                    Grid.SetColumn(next, 2);
                    pair.Children.Add(card);
                    pair.Children.Add(next);
                    list.Children.Add(pair);
                    i++;
                }
                else list.Children.Add(card);
            }
        }
        fit?.SetGap(metrics.Gap);
    }

    public void Update(Snapshot s, History h)
    {
        last = s; lastHistory = h;
        (Color c, string text) = s.Access switch
        {
            SensorAccess.Full => (p.Calm, T("LIVE")),
            SensorAccess.Limited => (p.Ramp[2], T("LIMITED")),
            _ => (p.Cool, T("DEMO"))
        };
        statusDot.Fill = Palette.Brush(c);
        statusText.Text = text;
        hint.IsVisible = s.Access == SensorAccess.Limited;
        int gpus = Math.Max(1, GpuGroup.Visible(s, settings).Count);
        if (gpus != gpuCount) { gpuCount = gpus; BuildCards(); }
        foreach (var w in widgets) w.Update(s, h);
    }
}

// Lays the card columns out at a chosen text scale. With Fill on, it shrinks the scale until the
// tallest column fits the available height, then spreads spare height into each column's gaps,
// so every column reaches the bottom and all text is the same size.
public sealed class FitPanel : Panel
{
    private readonly List<StackPanel> lists;
    private readonly double scale;
    private readonly bool fill;
    private double gap, applied = 1;

    public event Action<double>? Fitted;

    public FitPanel(List<StackPanel> lists, double scale, bool fill, double gap)
    {
        this.lists = lists; this.scale = scale; this.fill = fill; this.gap = gap;
        ClipToBounds = fill;
        foreach (var l in lists) Children.Add(l);
    }

    public void SetGap(double value) { gap = value; InvalidateMeasure(); }

    private double ColumnWidth(double w) => (w - gap * (lists.Count - 1)) / lists.Count;

    private static int Gaps(StackPanel l) => Math.Max(1, l.Children.Count(c => c.IsVisible) - 1);

    // Height without any spare spacing added, so repeated passes land on the same answer.
    private double Natural(StackPanel l) => l.DesiredSize.Height - Gaps(l) * (l.Spacing - gap);

    private double MeasureAll(double colWidth, double s)
    {
        foreach (var l in lists) l.Measure(new Size(colWidth / s, double.PositiveInfinity));
        return lists.Max(Natural);
    }

    protected override Size MeasureOverride(Size available)
    {
        double w = double.IsFinite(available.Width) ? available.Width : 300, h = available.Height, s = scale;
        double col = ColumnWidth(w);
        double tallest = MeasureAll(col, s);
        bool fit = fill && double.IsFinite(h) && h > 0;
        if (fit)
        {
            // Narrower text wraps differently at a new scale, so settle in a few steps.
            for (int i = 0; i < 4 && tallest * s > h + .5; i++) { s = h / tallest; tallest = MeasureAll(col, s); }
            if (tallest * s > h) s = h / Math.Max(1, tallest);
        }
        foreach (var l in lists)
        {
            double target = fit ? gap + Math.Min(Math.Max(0, h / s - Natural(l)) / Gaps(l), gap * 2.5) : gap;
            if (Math.Abs(l.Spacing - target) > .5) { l.Spacing = target; l.Measure(new Size(col / s, double.PositiveInfinity)); }
        }
        if (Math.Abs(s - applied) > .001) { applied = s; Fitted?.Invoke(s); }
        return fit ? new Size(w, h) : new Size(w, lists.Max(l => l.DesiredSize.Height) * s);
    }

    protected override Size ArrangeOverride(Size final)
    {
        double col = ColumnWidth(final.Width);
        for (int i = 0; i < lists.Count; i++)
        {
            var l = lists[i];
            l.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Absolute);
            l.RenderTransform = new ScaleTransform(applied, applied);
            l.Arrange(new Rect(i * (col + gap), 0, col / applied, l.DesiredSize.Height));
        }
        return final;
    }
}
