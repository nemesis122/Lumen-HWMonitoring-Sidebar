using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

/// <summary>
/// A native design owns its focus area. This shared safety net gives every selected scalar reading a
/// visible place, even when that design has fewer native slots. Identity is never inferred from a value:
/// two equal temperatures, zero rates, or a peak equal to load remain distinct sensors.
/// </summary>
public sealed class SensorDetails : StackPanel
{
    private sealed class Binding { public string Key = ""; public double OriginalMin = double.NaN; public bool Inferred; }
    private static readonly ConditionalWeakTable<Control, Binding> bindings = new();
    private static readonly ConditionalWeakTable<Border, SensorDetails> instances = new();
    private static readonly ConditionalWeakTable<Border, ScrollViewer> horizontalViews = new();

    private readonly Dictionary<string, (TextBlock Label, TextBlock Value)> fields = new();
    private readonly Dictionary<string, SensorDetailVisual> visuals = new();
    private sealed class Appearance { public WidgetStyle Style; }
    private static readonly ConditionalWeakTable<Control, Appearance> designs = new();
    public static void Design(Control root, WidgetStyle style) => designs.GetOrCreateValue(root).Style = style;
    public static WidgetStyle StyleOf(Control root, Settings settings) => designs.TryGetValue(root, out var design) ? design.Style : settings.ActiveWidget?.Style ?? WidgetStyle.Standard;
    private string shape = "";
    private StackPanel? coreGroup;
    private Skyline? coreBars;
    private readonly StackPanel readings = new() { Spacing = 2 };
    private readonly DriveTransferPairs transfers = new();
    public static string KeyOf(Control control) => bindings.TryGetValue(control, out var binding) ? binding.Key : "";

    public static T Value<T>(T control, string key) where T : Control
    {
        var binding = bindings.GetOrCreateValue(control);
        binding.Key = key;
        binding.Inferred = false;
        return control;
    }

    public static bool Visible(Control c, Control root)
    {
        for (Control? node = c; node != null; node = node.GetLogicalParent() as Control)
        {
            if (!node.IsVisible || node.Opacity == 0) return false;
            if (node == root) return true;
        }
        return false;
    }

    public static IReadOnlyList<string> VisibleKeys(Control root) => root.GetLogicalDescendants().OfType<Control>()
        .Where(c => Visible(c, root) && HasText(c))
        .SelectMany(c => KeyOf(c).Split('|', StringSplitOptions.RemoveEmptyEntries)).Distinct().ToArray();

    private static bool HasText(Control c) => c is TextBlock t ? !string.IsNullOrWhiteSpace(t.Text) && t.Text != "\u2060"
        : c.GetLogicalDescendants().OfType<TextBlock>().Any(t => !string.IsNullOrWhiteSpace(t.Text) && t.Text != "\u2060");

    // Used only by legacy layout adapters: a label resolves to a stable ID, never by comparing numbers.
    public static string LabelKey(WidgetKind kind, string label, IReadOnlyList<CompleteReading> rows, string unitHint = "")
    {
        // A Celsius unit is not the drive letter C: when legacy rows reuse their controls.
        if (label.Trim() is "°C" or "°F") return "";
        string Norm(string x) => new(x.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        var aliases = new Dictionary<string, string>();
        void Alias(string english, string key) { aliases[Norm(L.T(english))] = key; }
        string prefix = kind == WidgetKind.Cpu ? "cpu." : "gpu.";
        if (kind is WidgetKind.Cpu or WidgetKind.Gpu)
        {
            Alias("Temp", prefix + "temp"); Alias("Temperature", prefix + "temp"); Alias("Load", prefix + "load");
            Alias("Clock speed", prefix + "clock"); Alias("Clock", prefix + "clock");
            Alias("Power", prefix + "power"); Alias("Fan", prefix + "fan"); Alias("Voltage", prefix + "voltage");
            Alias("Hot spot", "gpu.hotspot"); Alias("Memory junction", "gpu.junction");
            Alias("Average clock", prefix + "clock"); Alias("Core voltage", prefix + "voltage");
            Alias("Core clock", prefix + "clock");
        }
        if (kind == WidgetKind.Memory) { Alias("Used", "mem.load"); Alias("2-min peak", "mem.peak"); }
        if (kind == WidgetKind.Memory && Norm(label) == Norm(L.T("Used")) && unitHint.Contains("GB")) return "mem.used";
        if (kind == WidgetKind.Cooling)
        {
            var sensors = rows.Where(r => (r.Key.StartsWith("temp:") || r.Key.StartsWith("fan:")) && Norm(r.Label) == Norm(label)).ToArray();
            if (sensors.Length == 1) return sensors[0].Key;
            if (sensors.Length > 1 && unitHint.Length > 0)
                return sensors.FirstOrDefault(r => r.Key.StartsWith(unitHint.Contains('°') ? "temp:" : "fan:"))?.Key ?? "";
        }
        if (kind == WidgetKind.Gpu && Norm(label) == "vram")
            return string.Join('|', rows.Where(r => r.Key.StartsWith("gpu.vram.")).Select(r => r.Key));
        if (kind == WidgetKind.Network)
        {
            Alias("Down", "net.down"); Alias("Download", "net.down"); Alias("Up", "net.up"); Alias("Upload", "net.up");
            Alias("Address", "net.address"); Alias("2-min peak", "net.peak");
        }
        if (aliases.TryGetValue(Norm(label), out var alias) && rows.Any(r => r.Key == alias)) return alias;
        if (kind == WidgetKind.Drives)
        {
            var byDrive = rows.Where(r => r.Key.EndsWith(":temp") && Norm(r.Label.Split(" · ")[0]) == Norm(label)).ToArray();
            if (byDrive.Length == 1) return byDrive[0].Key;
        }
        var exact = rows.Where(r => Norm(r.Label) == Norm(label)).ToArray();
        return exact.Length == 1 ? exact[0].Key : "";
    }

    public static IReadOnlyList<CompleteReading> Apply(Border root, WidgetKind kind, int index, Snapshot snap, History history, Settings settings, Palette palette, bool collectOnly = false, Control? excluded = null)
    {
        if (kind is WidgetKind.Overview or WidgetKind.Notes or WidgetKind.Timer) return Array.Empty<CompleteReading>();
        var rows = CompleteReadings.For(kind, index, snap, history, settings, palette);
        HardwareWidgetTitle.Apply(root, kind, index, snap, history, settings, palette);
        // Register numeric native controls already carrying the presentation's stable sensor identity.
        foreach (var c in root.GetLogicalDescendants().OfType<Control>().Where(c => !c.GetLogicalAncestors().OfType<SensorDetails>().Any()))
        {
            string key = WidgetPresentation.SensorKey(c);
            if (!bindings.TryGetValue(c, out _) && key.Length > 0 && (c is TextBlock or Hero or BoldValue or FocusReadout || c is Panel && HasText(c)))
                Value(c, key);
            if (c.Tag is string tag && (tag.StartsWith("local-value:") || tag.StartsWith("favorite-value:") || tag.StartsWith("insight-value:")))
                Value(c, tag[(tag.IndexOf(':') + 1)..]);
        }
        var detail = instances.GetOrCreateValue(root);
        // Old adapters still draw label/value rows. Bind their actual value control to the source ID.
        foreach (var label in root.GetLogicalDescendants().OfType<TextBlock>().ToArray())
        {
            if (!Visible(label, root) || label.GetLogicalAncestors().OfType<SensorDetails>().Any()) continue;
            string key = LabelKey(kind, label.Text ?? "", rows);
            Control? parent = label.GetLogicalParent() as Control;
            if (parent is not Panel) continue;
            var children = parent.GetLogicalChildren().OfType<Control>().ToArray();
            if (children.Any(c => c is ExtrasView or DriveRows)) continue;
            // Table layouts share one grid across all readings; only inspect the label's own row.
            IEnumerable<Control> candidates = parent is Grid table
                ? children.Where(c => Grid.GetRow(c) == Grid.GetRow(label)) : children;
            if (candidates.Count() > 4) continue;
            var texts = candidates.SelectMany(c => c.GetLogicalDescendants().OfType<TextBlock>().Prepend(c as TextBlock).OfType<TextBlock>())
                .Where(t => t != label && t.FontFeatures != null && Visible(t, root) && !string.IsNullOrWhiteSpace(t.Text) && t.Text != "\u2060"
                    && (t.Text.Any(char.IsDigit) || t.Text.StartsWith("—") || t.Text == L.T("Off") || t.Text == L.T("Stopped"))).ToArray();
            if (texts.Length == 1) key = LabelKey(kind, label.Text ?? "", rows, texts[0].Text ?? "");
            if (key.Length == 0) continue;
            if (texts.Length == 1 && (!bindings.TryGetValue(texts[0], out var oldBinding) || oldBinding.Inferred))
            {
                Value(texts[0], key);
                bindings.GetOrCreateValue(texts[0]).Inferred = true;
            }
        }
        StabilizeDriveValues(root, kind);
        // Previous fallback rows must not count as native coverage, or they would alternate each tick.
        var native = root.GetLogicalDescendants().OfType<Control>()
            .Where(c => c != excluded && (excluded == null || !c.GetLogicalAncestors().Contains(excluded)) && !c.GetLogicalAncestors().OfType<SensorDetails>().Any() && Visible(c, root) && HasText(c))
            .SelectMany(c => KeyOf(c).Split('|', StringSplitOptions.RemoveEmptyEntries)).ToHashSet();
        var missing = rows.Where(r => !native.Contains(r.Key)).ToArray();
        if (collectOnly) return missing;
        bool coreFallback = kind == WidgetKind.Cpu && settings.Shows("cpu.cores") && !root.GetLogicalDescendants().OfType<Control>()
            .Any(c => !c.GetLogicalAncestors().OfType<SensorDetails>().Any() && Visible(c, root) && (c is Skyline or CoreBars || c.GetType().Name == "ThreadBars"));
        if ((missing.Length > 0 || coreFallback) && detail.GetLogicalParent() == null)
        {
            // Preserve native frame types (SciFi, for example, updates its ChamferFrame each tick).
            Control? target = root.Child;
            while (target is Decorator || target is ScrollViewer)
                target = target is Decorator frame ? frame.Child : ((ScrollViewer)target).Content as Control;
            if (target is StackPanel stack) stack.Children.Add(detail);
            else
            {
                var original = root.Child;
                root.Child = null;
                var content = Ui.Stack(6); content.Tag = "elastic";
                if (original != null) content.Children.Add(original);
                content.Children.Add(detail); root.Child = content;
            }
        }
        if (kind == WidgetKind.Drives && root.GetLogicalDescendants().OfType<DriveRows>().FirstOrDefault(r => Visible(r, root)) is { } driveRows)
        {
            Grid.SetIsSharedSizeScope(root, true);
            // Match native insets too (capsules have a row margin, history cards a padded box).
            double left = 0, right = 0;
            for (Control? node = driveRows; node != null && node != root; node = node.GetLogicalParent() as Control)
            {
                left += node.Margin.Left; right += node.Margin.Right;
                if (node is Border border) { left += border.Padding.Left + border.BorderThickness.Left; right += border.Padding.Right + border.BorderThickness.Right; }
            }
            for (Control? node = detail.GetLogicalParent() as Control; node != null && node != root; node = node.GetLogicalParent() as Control)
            {
                left -= node.Margin.Left; right -= node.Margin.Right;
                if (node is Border border) { left -= border.Padding.Left + border.BorderThickness.Left; right -= border.Padding.Right + border.BorderThickness.Right; }
            }
            detail.Margin = new Thickness(Math.Max(0, left), 0, Math.Max(0, right), 0);
        }
        detail.Set(missing, palette, settings, kind, designs.TryGetValue(root, out var appearance) ? appearance.Style : settings.ActiveWidget?.Style ?? WidgetStyle.Standard, snap, root);
        StabilizeDriveValues(detail, kind);
        if (kind == WidgetKind.Drives)
            foreach (var number in root.GetLogicalDescendants().OfType<TextBlock>())
                if (KeyOf(number).EndsWith(":read") || KeyOf(number).EndsWith(":write"))
                    number.Foreground = Palette.Brush(palette.Ink);
        if (coreFallback)
        {
            if (detail.coreGroup == null)
            {
                detail.coreBars = new Skyline { Height = 16, Palette = palette };
                detail.coreGroup = Ui.Stack(2, Ui.Text(L.T("Core bars"), 10.5, palette.Soft), detail.coreBars);
            }
            if (!detail.Children.Contains(detail.coreGroup)) detail.Children.Add(detail.coreGroup);
            detail.coreGroup.IsVisible = true;
            detail.coreBars!.Loads = snap.Cpu?.CoreLoads ?? Array.Empty<double>();
            detail.coreBars.InvalidateVisual();
            detail.IsVisible = true;
        }
        else if (detail.coreGroup != null) detail.coreGroup.IsVisible = false;
        ConstrainHorizontal(root, settings);
        return missing;
    }

    private static void StabilizeDriveValues(Control root, WidgetKind kind)
    {
        if (kind != WidgetKind.Drives) return;
        foreach (var text in root.GetLogicalDescendants().OfType<TextBlock>().Where(t => t.FontFeatures != null))
        {
            string key = KeyOf(text);
            if (key.Length == 0) key = text.GetLogicalAncestors().OfType<Control>().Select(KeyOf).FirstOrDefault(k => k.StartsWith("drive:")) ?? "";
            if (!key.StartsWith("drive:") || key.EndsWith(":model") || !(text.Text ?? "").Any(char.IsDigit)) continue;
            string shown = text.Text ?? "";
            string sample = key.EndsWith(":read") || key.EndsWith(":write") ? "888.8 GB/s"
                : key.EndsWith(":temp") ? shown.Contains('°') ? "888 °C" : "888"
                : key.EndsWith(":hours") ? "888888 h"
                : key.EndsWith(":powercount") ? "888888"
                : key.EndsWith(":free") || key.EndsWith(":total") ? "888.8 TB"
                : shown.Contains('%') ? "100 %" : "100";
            var probe = new TextBlock { Text = sample, FontSize = text.FontSize, FontFamily = text.FontFamily,
                FontWeight = text.FontWeight, FontFeatures = text.FontFeatures, LetterSpacing = text.LetterSpacing };
            probe.Measure(Size.Infinity);
            var binding = bindings.GetOrCreateValue(text);
            if (double.IsNaN(binding.OriginalMin)) binding.OriginalMin = text.MinWidth;
            text.MinWidth = Math.Max(binding.OriginalMin, Math.Ceiling(probe.DesiredSize.Width));
        }
    }

    // A flat bar has a finite height. Scroll overflowing content at its chosen font size instead of
    // making every card in the row unreadably small because one card has many selected sensors.
    public static void ConstrainHorizontal(Border root, Settings settings)
    {
        if (!settings.Horizontal) return;
        if (!horizontalViews.TryGetValue(root, out var scroll))
        {
            scroll = new ScrollViewer { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
            if (root.Child is ChamferFrame frame)
            {
                var original = frame.Child; frame.Child = null; scroll.Content = original; frame.Child = scroll;
            }
            else { var original = root.Child; root.Child = null; scroll.Content = original; root.Child = scroll; }
            horizontalViews.Add(root, scroll);
        }
        scroll.MaxHeight = Math.Max(64, (settings.BarHeight / Math.Max(1, settings.Rows) - 76) / Math.Max(1, settings.TextScale));
    }

    private void Set(CompleteReading[] rows, Palette p, Settings settings, WidgetKind kind, WidgetStyle style, Snapshot snapshot, Control nativeRoot)
    {
        IsVisible = rows.Length > 0;
        if (kind == WidgetKind.Drives && DriveTransferPairs.Applies(style))
        {
            if (!Children.Contains(transfers)) Children.Add(transfers);
            transfers.Update(rows.Where(DriveTransferPairs.IsRate).ToArray(), style, p, snapshot, nativeRoot is PillRowTile pill ? pill.CapsuleHeight : 44);
            rows = rows.Where(r => !DriveTransferPairs.IsRate(r)).ToArray();
        }
        string next = string.Join("\n", rows.Select(r => r.Key + "|" + r.Label));
        if (next != shape)
        {
            shape = next; readings.Children.Clear(); fields.Clear(); visuals.Clear(); Spacing = 2;
            // A single outer viewport handles overflow. Keeping the register in the same shared-size
            // scope as the native rows also aligns their name, graph and value columns.
            if (!Children.Contains(readings)) Children.Insert(0, readings);
            foreach (var row in rows)
            {
                var label = Ui.Text(row.Label, 10.5, p.Soft);
                label.TextWrapping = TextWrapping.Wrap; label.TextTrimming = TextTrimming.None;
                var value = Value(Ui.Text("", 12, p.Ink, FontWeight.SemiBold, numbers: true), row.Key);
                value.TextWrapping = TextWrapping.Wrap; value.TextTrimming = TextTrimming.None;
                var visual = new SensorDetailVisual(label, value, row, style, kind, settings, p, nativeRoot);
                visuals.Add(row.Key, visual);
                fields.Add(row.Key, (label, value));
                readings.Children.Add(visual.Root);
            }
        }
        foreach (var row in rows)
        {
            var field = fields[row.Key];
            field.Label.Text = kind == WidgetKind.Drives && row.Key.EndsWith(":model") ? row.Label.Split(" · ")[0] : row.Label;
            field.Value.Text = row.Value;
            visuals[row.Key].Update(row, snapshot);
            ToolTip.SetTip(field.Value, row.Label + ": " + (row.Value.StartsWith("—") ? L.T("Not available") : row.Value));
        }
    }
}
