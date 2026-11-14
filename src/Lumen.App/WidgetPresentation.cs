using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Lumen.Core;

namespace Lumen.App;

public static class WidgetPresentation
{
    private sealed class Original
    {
        public string Key = "";
        public double FontSize, Width, Height, MinHeight, LetterSpacing, MinimumColumnWidth, MaxWidth, MaxHeight;
        public bool Captured;
        public bool Steady;
        public Control? Cell;
        public double CellMin;
        public string Row = "";
        public double AppliedGraphics = double.NaN;
    }
    private static readonly ConditionalWeakTable<Control, Original> originals = new();

    private static readonly ConditionalWeakTable<Control, Dictionary<string, double>> widest = new();

    public static T Sensor<T>(T control, string key) where T : Control
    {
        originals.GetOrCreateValue(control).Key = key;
        return control;
    }

    public static string SensorKey(Control control) => originals.TryGetValue(control, out var original) ? original.Key : "";

    /// <summary>Numbers drawn at least this large are the widget's main value.</summary>
    public const double HeroFont = 20;

    public static void Apply(Control root, Settings settings)
    {
        double values = settings.ActiveWidget?.Values ?? 1;
        double graphics = settings.ActiveWidget?.Graphics ?? 1;
        double labels = settings.ActiveWidget?.Labels ?? 1;
        double hero = settings.ActiveWidget?.Hero ?? 1;
        // Rows of the form "name – bar – value": every row of a widget shares one name width and one value
        // width, so all bars start and end at the same place.
        Grid.SetIsSharedSizeScope(root, true);
        foreach (var row in root.GetLogicalDescendants().OfType<Grid>())
        {
            var cols = row.ColumnDefinitions;
            if (cols.Count != 3 || !cols[0].Width.IsAuto || !cols[1].Width.IsStar || !cols[2].Width.IsAuto) continue;
            bool bar = row.Children.Any(c => Grid.GetColumn(c) == 1 && c is FillBack or Meter or GradientBar or SegmentBar or RingBar or LevelBar);
            bool name = row.Children.Any(c => Grid.GetColumn(c) == 0 && c is TextBlock { FontFeatures: null });
            if (!bar || !name) continue;
            cols[0].SharedSizeGroup = "LumenName";
            cols[2].SharedSizeGroup = "LumenValue";
        }
        foreach (var control in root.GetLogicalDescendants().OfType<Control>().Prepend(root))
        {
            var original = originals.GetOrCreateValue(control);
            if (!original.Captured)
            {
                original.Captured = true;
                original.Width = control.Width;
                original.Height = control.Height;
                original.MinHeight = control.MinHeight;
                original.MaxWidth = control.MaxWidth;
                original.MaxHeight = control.MaxHeight;
                if (control is StatPanel panel) original.MinimumColumnWidth = panel.MinimumColumnWidth;
                if (control is TextBlock text) { original.FontSize = text.FontSize; original.LetterSpacing = text.LetterSpacing; }
            }
            if (original.Key.Length > 0 && !settings.Shows(original.Key)) control.IsVisible = false;
            if (control is StatPanel stats) stats.MinimumColumnWidth = original.MinimumColumnWidth * Math.Max(1, Math.Max(values, labels));
            if (root is CpuCard && control is AreaChart && !settings.Shows("cpu.load.history")) control.IsVisible = false;
            if (root is GpuCard && control is AreaChart && !settings.Shows("gpu.load.history")) control.IsVisible = false;
            // English widgets say "Temp" instead of "Temperature" (in every design; the settings keep the
            // full word).
            if (control is TextBlock word && L.Language == "en" && word.Text is string said && said.Contains("emperature", StringComparison.OrdinalIgnoreCase))
            {
                word.Text = said.Replace("Temperature", "Temp").Replace("TEMPERATURE", "TEMP").Replace("temperature", "temp");
            }
            // Short names in every hardware design (CPU, GPU, DL, UP, PSU, Hot, Clk …). Info widgets (clock,
            // calendar, weather, notes) and numbers keep their text.
            if (control is TextBlock { FontFeatures: null, Text: string label } shortened && root is not InfoTile && label.Length > 0)
            {
                var shorter = ShortNames.Apply(label, original.FontSize >= 15);
                if (shorter != label) shortened.Text = shorter;
            }
            // Numbers in a column sized to its content: the column keeps the widest width seen, so the bar or
            // label beside it does not get longer and shorter as the digits change ("0" ↔ "1130 rpm"). The
            // width is remembered per widget and row name, so rows that are built again keep it too.
            if (control is TextBlock { FontFeatures: not null } steady && !original.Steady)
            {
                Control cell = steady;
                while (cell.Parent is Border wrap && wrap.Child == cell) cell = wrap;
                if (cell.Parent is Grid grid && grid.ColumnDefinitions.Count > 1 && Grid.GetColumn(cell) == grid.ColumnDefinitions.Count - 1
                    && grid.ColumnDefinitions[^1].Width.IsAuto
                    && grid.ColumnDefinitions.Any(c => c.Width.IsStar))
                {
                    original.Steady = true;
                    original.Cell = cell;
                    original.CellMin = cell.MinWidth;
                    original.Row = grid.Children.OfType<TextBlock>().FirstOrDefault(x => x.FontFeatures == null && Grid.GetRow(x) == Grid.GetRow(cell))?.Text ?? "#" + Grid.GetRow(cell);
                    if (cell == steady) steady.TextAlignment = Avalonia.Media.TextAlignment.Right;
                }
            }
            if (control is TextBlock number)
            {
                // Big figures (the main value) follow "Main value size", the smaller ones in the list
                // follow "Value size", text follows "Label size".
                double scale = number.FontFeatures == null ? labels : original.FontSize >= HeroFont ? hero : values;
                number.FontSize = original.FontSize * scale;
                // The value column keeps the widest value seen (per widget, row name and size), measured before
                // the layout pass, so it never needs an extra pass.
                if (original.Cell is Control cell2 && cell2.IsVisible)
                {
                    var memory = widest.GetOrCreateValue(root);
                    string key = original.Row + "|" + number.FontSize.ToString("0.0");
                    cell2.MinWidth = original.CellMin;
                    cell2.Measure(Size.Infinity);
                    double width = Math.Max(memory.GetValueOrDefault(key), Math.Ceiling(cell2.DesiredSize.Width));
                    memory[key] = width;
                    cell2.MinWidth = Math.Max(original.CellMin, width);
                }
                number.LetterSpacing = original.LetterSpacing * scale;
                if (double.IsFinite(original.Width)) number.Width = original.Width * scale;
            }
            // Rings and gauges of the newer designs: a Viewbox that scales its drawing up and down (not the
            // ones that only shrink a value to fit), or a panel marked as a graphic. "Graphic size" makes the
            // drawing larger or smaller; the text in it follows the value and label sizes.
            if ((control is Viewbox { StretchDirection: Avalonia.Media.StretchDirection.Both } || object.Equals(control.Tag, "graphic")) && !object.Equals(control.Tag, "grow"))
            {
                if (double.IsFinite(original.Width)) control.Width = original.Width * graphics;
                if (double.IsFinite(original.Height)) control.Height = original.Height * graphics;
                if (double.IsFinite(original.MaxWidth)) control.MaxWidth = original.MaxWidth * graphics;
                if (double.IsFinite(original.MaxHeight)) control.MaxHeight = original.MaxHeight * graphics;
            }
            if (control is GradientBar or SegmentBar or RingBar or LevelBar)
            {
                if (double.IsFinite(original.Height)) control.Height = original.Height * graphics;
                if (double.IsFinite(original.Width)) control.Width = original.Width * graphics;
            }
            // Charts are also grown by the fitting panel to fill the column. Their height is set again only
            // when the graphic size changes; resetting it on every update made the fitting panel grow them
            // again each time, and with display scaling (125 %, 150 %) the result could differ by a pixel,
            // so everything below a chart twitched once a second.
            bool resize = !(control is AreaChart or MirrorChart) || original.AppliedGraphics != graphics;
            if (control is Meter or AreaChart or MirrorChart or Skyline or WaveChart or WeatherIcon or FillBack && resize)
            {
                original.AppliedGraphics = graphics;
                if (double.IsFinite(original.Height)) control.Height = original.Height * graphics;
                if (double.IsFinite(original.Width)) control.Width = original.Width * graphics;
                // Fill backgrounds share their container with values: reserve enough height for both.
                if (control is FillBack && control.Parent is Panel panel && panel.Classes.Contains("pill-surface"))
                {
                    var container = originals.GetOrCreateValue(panel);
                    panel.MinHeight = container.MinHeight * Math.Max(labels, Math.Max(values, graphics));
                }
                else if (control is FillBack && control.Parent is Panel panel2 && double.IsFinite(panel2.Height))
                {
                    var container = originals.GetOrCreateValue(panel2);
                    if (!container.Captured) { container.Captured = true; container.Height = panel2.Height; container.Width = panel2.Width; }
                    panel2.Height = container.Height * Math.Max(labels, Math.Max(values, graphics));
                }
            }
        }
        AlignRegisterColumns(root);
        bool enlarged = Math.Max(Math.Max(values, labels), Math.Max(graphics, hero)) > 1.15;
        foreach (var row in root.GetLogicalDescendants().OfType<ResponsiveGrid>()) row.Adapt = enlarged;
        foreach (var face in root.GetLogicalDescendants().OfType<DesignValueFaces>()) face.Enlarged = enlarged;
    }

    private static void AlignRegisterColumns(Control root)
    {
        if (root is not (TableTile or BadgeListTile) && !root.GetLogicalDescendants().OfType<DriveRows>().Any()) return;
        var rows = root.GetLogicalDescendants().OfType<Grid>().Where(g => g.ColumnDefinitions.Count == 3 && g.ColumnDefinitions[1].Width.IsStar
            && g.Children.Any(c => Grid.GetColumn(c) == 1 && c is Meter or FillBack or GradientBar or SegmentBar or RingBar)
            && g.IsVisible && g.GetLogicalAncestors().OfType<Control>().All(c => c.IsVisible)).ToArray();
        if (rows.Length < 2) return;
        double Measure(int column)
        {
            double width = 0, font = 0;
            foreach (var row in rows)
            foreach (var child in row.Children.Where(c => Grid.GetColumn(c) == column && c.IsVisible))
            {
                child.Measure(Size.Infinity); width = Math.Max(width, Math.Ceiling(child.DesiredSize.Width));
                foreach (var text in child.GetLogicalDescendants().OfType<TextBlock>().Prepend(child as TextBlock).OfType<TextBlock>()) font = Math.Max(font, text.FontSize);
            }
            var memory = widest.GetOrCreateValue(root); string key = "register-column|" + column + "|" + font.ToString("0.0");
            memory[key] = Math.Max(memory.GetValueOrDefault(key), width); return memory[key];
        }
        double name = Measure(0), value = Measure(2);
        foreach (var row in rows)
        {
            row.ColumnDefinitions[0].Width = new GridLength(name);
            row.ColumnDefinitions[2].Width = new GridLength(value);
        }
    }
}
