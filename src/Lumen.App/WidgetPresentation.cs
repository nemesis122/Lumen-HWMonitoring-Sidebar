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
    }
    private static readonly ConditionalWeakTable<Control, Original> originals = new();

    public static T Sensor<T>(T control, string key) where T : Control
    {
        originals.GetOrCreateValue(control).Key = key;
        return control;
    }

    /// <summary>Numbers drawn at least this large are the widget's main value.</summary>
    public const double HeroFont = 20;

    public static void Apply(Control root, Settings settings)
    {
        double values = settings.ActiveWidget?.Values ?? 1;
        double graphics = settings.ActiveWidget?.Graphics ?? 1;
        double labels = settings.ActiveWidget?.Labels ?? 1;
        double hero = settings.ActiveWidget?.Hero ?? 1;
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
            if (control is TextBlock number)
            {
                // Big figures (the main value) follow "Main value size", the smaller ones in the list
                // follow "Value size", text follows "Label size".
                double scale = number.FontFeatures == null ? labels : original.FontSize >= HeroFont ? hero : values;
                number.FontSize = original.FontSize * scale;
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
            if (control is Meter or AreaChart or MirrorChart or Skyline or WaveChart or WeatherIcon or FillBack)
            {
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
    }
}
