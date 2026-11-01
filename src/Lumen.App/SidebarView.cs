using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.LogicalTree;
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
    // Shown on a register that has no widgets yet.
    private readonly TextBlock emptyNote;
    private readonly Ellipse statusDot;
    private readonly TextBlock statusText;
    private readonly List<StackPanel> lists = new();
    private readonly FitPanel fit;
    // The bar at the top or bottom: one fitted strip of cards per row.
    private readonly Grid barRows = new();
    private int gpuCount = 1;
    private Metrics metrics;
    private bool compacted, balanced;
    private int rebalanceTries;
    private Dictionary<WidgetKind, double>? measured;
    private readonly Dictionary<WidgetKind, List<Control>> builtCards = new();
    private Snapshot? last; private History? lastHistory;
    // The register shown, kept across rebuilds (settings changes, theme switches).
    private static int page = 1;
    private readonly WrapPanel tabs = new() { Orientation = Avalonia.Layout.Orientation.Horizontal, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };

    public SidebarView(Settings settings, Palette palette)
    {
        p = palette;
        this.settings = settings;
        metrics = Metrics.For(settings.Density);
        var m = metrics;
        Background = Palette.Brush(p.Surface);
        BorderBrush = Palette.Brush(p.Stroke);
        BorderThickness = settings.Edge switch
        {
            _ when settings.FullScreen && !settings.Horizontal => new Thickness(0),
            Edge.Right => new Thickness(1, 0, 0, 0),
            Edge.Top => new Thickness(0, 0, 0, 1),
            Edge.Bottom => new Thickness(0, 1, 0, 0),
            _ => new Thickness(0, 0, 1, 0)
        };

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
        // Numbered registers fit in the top bar; named ones get a row of their own below it.
        bool namedTabs = settings.Pages > 1 && settings.HasPageNames;
        var bar = namedTabs ? Ui.Columns("Auto,Auto,Auto,*,Auto", mark, word, status, new Control(), gear) : Ui.Columns("Auto,Auto,Auto,*,Auto", mark, word, status, tabs, gear);
        if (namedTabs) tabs.Margin = new Thickness(2, 0, 0, 0);
        // Registers: click a number, or turn the mouse wheel anywhere on the top bar.
        page = Math.Clamp(page, 1, settings.Pages);
        bar.PointerWheelChanged += (_, e) =>
        {
            if (settings.Pages < 2) return;
            Show(page + (e.Delta.Y < 0 ? 1 : -1));
            e.Handled = true;
        };
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
        emptyNote = Ui.Text(T("Nothing on this register yet – choose its widgets in the settings under Widgets → Customise → Register."), 12.5, p.Soft);
        emptyNote.TextWrapping = TextWrapping.Wrap;
        emptyNote.TextTrimming = TextTrimming.None;
        emptyNote.Margin = new Thickness(6, 4, 6, 0);
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

        for (int i = 0; i < settings.ColumnCount; i++) lists.Add(new StackPanel());
        // "Automatic" fills the height exactly; the other densities scroll when there is too much.
        // A bar always fits its height.
        bool fill = settings.Density == Density.Fit || settings.Horizontal;
        fit = new FitPanel(lists, settings.TextScale, fill, m.Gap);
        BuildCards();
        Control body = settings.Horizontal ? barRows : fill ? fit : new ScrollViewer
        {
            Content = fit,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden
        };
        var root = new DockPanel { Margin = new Thickness(m.Gap, m.Gap + 2, m.Gap, m.Gap) };
        DockPanel.SetDock(bar, Dock.Top);
        bar.Margin = new Thickness(6, 0, 0, m.Gap + 2);
        root.Children.Add(bar);
        if (namedTabs)
        {
            DockPanel.SetDock(tabs, Dock.Top);
            tabs.Margin = new Thickness(4, -m.Gap * .5, 0, m.Gap + 2);
            tabs.PointerWheelChanged += (_, e) => { Show(page + (e.Delta.Y < 0 ? 1 : -1)); e.Handled = true; };
            root.Children.Add(tabs);
        }
        root.Children.Add(body);
        Child = root;

        if (fill) fit.Fitted += Squeezed;
    }

    // With many widgets, comfortable sizes squeezed too far read worse than compact ones.
    private void Squeezed(double scale)
    {
        if (scale >= .8 || compacted) return;
        compacted = true;
        Dispatcher.UIThread.Post(() => { metrics = Metrics.For(Density.Compact); BuildCards(); if (last != null) Update(last, lastHistory!); });
    }

    // Larger text widens each column by the same factor, so cards keep their layout instead of cutting labels off.
    public static double WindowWidth(Settings s)
    {
        int n = s.ColumnCount;
        double grow = Math.Max(1, s.TextScale);
        return (s.Width * n + (n - 1) * Metrics.For(s.Density).Gap) * grow;
    }

    private void BuildCards()
    {
        widgets.Clear();
        builtCards.Clear();
        BuildTabs();
        if (settings.Horizontal) { BuildBar(); return; }
        var columns = measured is null ? settings.Layout(page: page) : settings.Layout(w => measured.TryGetValue(w.Kind, out var hgt) ? hgt : Settings.Weight(w) * 150, page);
        for (int c = 0; c < lists.Count; c++)
        {
            var list = lists[c];
            list.Children.Clear();
            list.Spacing = metrics.Gap;
            if (c == 0) { list.Children.Add(hint); (emptyNote.Parent as Panel)?.Children.Remove(emptyNote); list.Children.Add(emptyNote); emptyNote.IsVisible = columns.All(col => col.Count == 0); }
            // Each graphics adapter is its own widget, so ring tiles can pair up with anything.
            var items = new List<(WidgetSlot slot, int index)>();
            foreach (var slot in c < columns.Count ? columns[c] : new())
                for (int i = 0; i < (slot.Kind == WidgetKind.Gpu ? gpuCount : 1); i++) items.Add((slot, i));
            for (int i = 0; i < items.Count; i++)
            {
                var (slot, index) = items[i];
                var card = Cards.Create(slot.Kind, slot.Style, index, p, metrics, settings);
                widgets.Add((IWidget)card);
                Remember(slot.Kind, card);
                // Two half-width tiles (rings, number tiles) in a row share one line.
                static bool Half(WidgetSlot w) => Settings.IsHalf(w);
                if (Half(slot) && card is RingTile or NumberTile && i + 1 < items.Count && Half(items[i + 1].slot))
                {
                    var next = Cards.Create(items[i + 1].slot.Kind, items[i + 1].slot.Style, items[i + 1].index, p, metrics, settings);
                    widgets.Add((IWidget)next);
                    Remember(items[i + 1].slot.Kind, next);
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

    // Each bar row is its own strip: the cards stand side by side and fit the row's height.
    private void BuildBar()
    {
        foreach (var old in barRows.Children.OfType<FitPanel>()) old.Fitted -= Squeezed;
        barRows.Children.Clear();
        barRows.RowDefinitions.Clear();
        var rows = settings.Layout(page: page);
        var allCells = new List<List<StackPanel>>();
        for (int r = 0; r < rows.Count; r++)
        {
            var items = new List<(WidgetSlot slot, int index)>();
            foreach (var slot in rows[r])
                for (int i = 0; i < (slot.Kind == WidgetKind.Gpu ? gpuCount : 1); i++) items.Add((slot, i));
            var cells = new List<StackPanel>();
            if (r == 0)
            {
                (hint.Parent as Panel)?.Children.Remove(hint);
                (emptyNote.Parent as Panel)?.Children.Remove(emptyNote);
                emptyNote.IsVisible = rows.All(row => row.Count == 0);
                cells.Add(new StackPanel { Children = { hint, emptyNote } });
            }
            for (int i = 0; i < items.Count; i++)
            {
                var (slot, index) = items[i];
                var card = Cards.Create(slot.Kind, slot.Style, index, p, metrics, settings);
                widgets.Add((IWidget)card);
                Remember(slot.Kind, card);
                Control cell = card;
                if (Settings.IsHalf(slot) && card is RingTile or NumberTile && i + 1 < items.Count && Settings.IsHalf(items[i + 1].slot))
                {
                    var next = Cards.Create(items[i + 1].slot.Kind, items[i + 1].slot.Style, items[i + 1].index, p, metrics, settings);
                    widgets.Add((IWidget)next);
                    Remember(items[i + 1].slot.Kind, next);
                    var pair = new Grid { ColumnDefinitions = new ColumnDefinitions($"*,{metrics.Gap},*") };
                    Grid.SetColumn(next, 2);
                    pair.Children.Add(card);
                    pair.Children.Add(next);
                    cell = pair;
                    i++;
                }
                cells.Add(new StackPanel { Spacing = metrics.Gap, Children = { cell } });
            }
            allCells.Add(cells);
        }
        // Rows with fewer cards keep the same card width as the fullest row, so all line up.
        int Filled(List<StackPanel> cells) => cells.Count(c => c.Children.Any(x => x != hint && x != emptyNote));
        int width = allCells.Select(Filled).DefaultIfEmpty(1).Max();
        // Rows without any widget (fewer widgets on this register than rows) are left out;
        // the remaining rows share the bar's height.
        allCells = allCells.Where((c, i) => i == 0 || Filled(c) > 0).ToList();
        for (int r = 0; r < allCells.Count; r++)
        {
            var cells = allCells[r];
            for (int k = Filled(cells); k < width; k++) cells.Add(new StackPanel { Children = { new Border { Height = 1, Opacity = 0 } } });
            if (r > 0) barRows.RowDefinitions.Add(new RowDefinition(metrics.Gap, GridUnitType.Pixel));
            barRows.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
            var strip = new FitPanel(cells, settings.TextScale, true, metrics.Gap);
            strip.Fitted += Squeezed;
            Grid.SetRow(strip, r * 2);
            barRows.Children.Add(strip);
        }
    }



    private void BuildTabs()
    {
        tabs.Children.Clear();
        if (settings.Pages < 2) return;
        for (int i = 1; i <= settings.Pages; i++)
        {
            int n = i;
            bool on = n == page;
            var b = new Button
            {
                Content = Ui.Text(settings.PageName(n), 11.5, on ? p.Ink : p.Soft, on ? FontWeight.Bold : FontWeight.Medium),
                Margin = new Thickness(0, 0, 2, 2),
                Background = on ? Palette.Brush(p.Track) : Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(settings.HasPageNames ? 10 : 8, settings.HasPageNames ? 3 : 2),
                MinWidth = 24,
                CornerRadius = new CornerRadius(8),
                Cursor = new Cursor(StandardCursorType.Hand),
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            b.Click += (_, _) => Show(n);
            tabs.Children.Add(b);
        }
    }

    private void Show(int target)
    {
        target = Math.Clamp(target, 1, settings.Pages);
        if (target == page) return;
        page = target;
        balanced = false;
        rebalanceTries = 0;
        try
        {
            BuildCards();
            if (last != null) Update(last, lastHistory!);
        }
        catch (Exception e) { CrashLog.Write(e, $"register {target}, {(settings.Horizontal ? "bar" : "side")}"); }
    }

    private void Remember(WidgetKind kind, Control card)
    {
        if (!builtCards.TryGetValue(kind, out var list)) builtCards[kind] = list = new();
        list.Add(card);
    }

    private void Rebalance()
    {
        // Natural heights, without the chart growth and extra spacing the fill has added.
        var heights = builtCards.ToDictionary(kv => kv.Key, kv => kv.Value.Sum(c => fit.NaturalHeight(c) + metrics.Gap));
        // Not laid out yet: try again shortly.
        if (heights.Values.Any(v => v <= metrics.Gap))
        {
            if (++rebalanceTries < 20) Dispatcher.UIThread.Post(Rebalance, DispatcherPriority.Background);
            return;
        }
        if (measured != null) foreach (var kv in measured) heights.TryAdd(kv.Key, kv.Value);
        var before = settings.Layout(measured is null ? null : w => measured.GetValueOrDefault(w.Kind, 150), page).Select(c => string.Join(",", c.Select(x => x.Kind))).ToList();
        var after = settings.Layout(w => heights.GetValueOrDefault(w.Kind, 150), page).Select(c => string.Join(",", c.Select(x => x.Kind))).ToList();
        if (Environment.GetEnvironmentVariable("LUMEN_DEBUG") == "1")
            Console.WriteLine("rebalance " + string.Join(" ", heights.Select(kv => $"{kv.Key}={kv.Value:0}")) + " | " + string.Join(" / ", before) + " -> " + string.Join(" / ", after));
        measured = heights;
        if (before.SequenceEqual(after)) return;
        BuildCards();
        if (last != null) foreach (var w in widgets) w.Update(last, lastHistory!);
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
        // With two columns, place automatic widgets once more by their real heights, so both
        // columns come out about equally tall and nothing has to be stretched to fill.
        if (settings.ColumnsInUse >= 2 && !settings.Horizontal && !balanced)
        {
            balanced = true;
            Dispatcher.UIThread.Post(Rebalance, DispatcherPriority.Background);
        }
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

    // Columns with something in them; a bar's register with fewer widgets leaves the rest empty.
    private List<StackPanel> Active()
    {
        var a = lists.Where(l => l.Children.Any(c => c.IsVisible)).ToList();
        return a.Count > 0 ? a : lists.Take(1).ToList();
    }

    private double ColumnWidth(double w) { int n = Math.Max(1, Active().Count); return (w - gap * (n - 1)) / n; }

    private static int Gaps(StackPanel l) => Math.Max(1, l.Children.Count(c => c.IsVisible) - 1);

    // Charts are the elastic part of a column: they can grow to soak up spare height.
    private readonly Dictionary<Control, double> baseHeight = new();

    private List<Control> Growable(StackPanel l)
    {
        var list = l.GetLogicalDescendants().OfType<Control>()
            .Where(c => c is AreaChart or MirrorChart && c.IsVisible && c.GetLogicalAncestors().OfType<Control>().All(a => a.IsVisible)).ToList();
        foreach (var c in list) if (!baseHeight.ContainsKey(c) && double.IsFinite(c.Height)) baseHeight[c] = c.Height;
        return list.Where(baseHeight.ContainsKey).ToList();
    }

    // Inside each card, the space between its rows can open up a little too.
    private readonly Dictionary<StackPanel, double> baseSpacing = new();

    private List<StackPanel> Airy(StackPanel l)
    {
        var list = l.GetLogicalDescendants().OfType<StackPanel>()
            .Where(c => Equals(c.Tag, "elastic") && c.IsVisible && c.Children.Count(x => x.IsVisible) > 1).ToList();
        foreach (var c in list) if (!baseSpacing.ContainsKey(c)) baseSpacing[c] = c.Spacing;
        return list;
    }

    private static int InnerGaps(StackPanel s) => s.Children.Count(x => x.IsVisible) - 1;

    public double NaturalHeight(Control card)
    {
        double h = card.DesiredSize.Height;
        foreach (var c in card.GetLogicalDescendants().OfType<Control>().Where(x => x is AreaChart or MirrorChart && x.IsVisible))
            if (baseHeight.TryGetValue(c, out var b)) h -= c.Height - b;
        foreach (var s in card.GetLogicalDescendants().OfType<StackPanel>().Where(x => Equals(x.Tag, "elastic")).Concat(card is StackPanel sp && Equals(sp.Tag, "elastic") ? new[] { sp } : Array.Empty<StackPanel>()))
            if (baseSpacing.TryGetValue(s, out var b) && s.IsVisible) h -= (s.Spacing - b) * Math.Max(0, InnerGaps(s));
        return h;
    }

    private double Grown(StackPanel l) => Growable(l).Sum(c => c.Height - baseHeight[c])
        + Airy(l).Sum(s => (s.Spacing - baseSpacing[s]) * InnerGaps(s));

    // Height without any spare spacing or chart growth added, so repeated passes land on the same answer.
    private double Natural(StackPanel l) => l.DesiredSize.Height - Gaps(l) * (l.Spacing - gap) - Grown(l);

    private double MeasureAll(double colWidth, double s)
    {
        foreach (var l in lists) l.Measure(new Size(colWidth / s, double.PositiveInfinity));
        // An empty strip (a bar row or register with nothing in it) has no height of its own.
        return lists.Count == 0 ? 0 : lists.Max(Natural);
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
            // Few widgets: grow everything a little (up to 8 %, beyond that labels get cut off)
            // before the charts and gaps take up the rest.
            double most = scale * 1.08;
            for (int i = 0; i < 4 && tallest * s < h - .5 && s < most - .001; i++) { s = Math.Min(most, h / tallest); tallest = MeasureAll(col, s); }
            if (tallest * s > h) s = h / Math.Max(1, tallest);
        }
        foreach (var l in lists)
        {
            var charts = Growable(l);
            double spacing = gap, grow = 0;
            bool airyChanged = false;
            if (fit)
            {
                // Spare height goes first into slightly wider gaps, then into taller charts,
                // and whatever is left into the gaps again, so every column ends at the bottom.
                double spare = Math.Max(0, h / s - Natural(l));
                double toGaps = Math.Min(spare, Gaps(l) * gap * .6);
                spare -= toGaps;
                if (charts.Count > 0)
                {
                    double room = charts.Sum(c => baseHeight[c] * 2.5);
                    grow = Math.Min(spare, room);
                    spare -= grow;
                }
                // Then the rows inside the cards breathe. Whatever is still left goes there too,
                // so the cards themselves fill the column instead of empty gaps between them.
                var airy = Airy(l);
                int inner = airy.Sum(InnerGaps);
                double perInner = inner > 0 ? spare / inner : 0;
                spare -= perInner * inner;
                foreach (var a in airy)
                {
                    double target = baseSpacing[a] + perInner;
                    if (Math.Abs(a.Spacing - target) > .5) { a.Spacing = target; airyChanged = true; }
                }
                spacing = gap + (toGaps + spare) / Gaps(l);
            }
            if (!fit) foreach (var a in Airy(l)) if (a.Spacing != baseSpacing[a]) { a.Spacing = baseSpacing[a]; airyChanged = true; }
            bool changed = Math.Abs(l.Spacing - spacing) > .5 || airyChanged;
            if (changed) l.Spacing = spacing;
            double total = charts.Sum(c => baseHeight[c]);
            foreach (var c in charts)
            {
                double target = baseHeight[c] + (total > 0 ? grow * baseHeight[c] / total : 0);
                if (Math.Abs(c.Height - target) > .5) { c.Height = target; changed = true; }
            }
            if (changed) l.Measure(new Size(col / s, double.PositiveInfinity));
        }
        if (Math.Abs(s - applied) > .001) { applied = s; Fitted?.Invoke(s); }
        return fit ? new Size(w, h) : new Size(w, lists.Select(l => l.DesiredSize.Height).DefaultIfEmpty(0).Max() * s);
    }

    protected override Size ArrangeOverride(Size final)
    {
        double col = ColumnWidth(final.Width);
        var active = Active();
        foreach (var l in lists.Except(active)) l.Arrange(new Rect(0, 0, 0, 0));
        for (int i = 0; i < active.Count; i++)
        {
            var l = active[i];
            l.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Absolute);
            l.RenderTransform = new ScaleTransform(applied, applied);
            l.Arrange(new Rect(i * (col + gap), 0, col / applied, l.DesiredSize.Height));
        }
        return final;
    }
}
