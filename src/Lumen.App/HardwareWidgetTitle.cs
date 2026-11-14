using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public static class HardwareWidgetTitle
{
    private sealed record Parts(TextBlock Title, TextBlock? Hardware);
    private static readonly ConditionalWeakTable<Control, Parts> parts = new();
    public static TextBlock? TitleOf(Control root) => root is AioCompanionTile companion ? TitleOf(companion.NativeContent)
        : parts.TryGetValue(root, out var entry) ? entry.Title : null;
    public static void Register(Control root, TextBlock title, TextBlock? hardware = null)
    {
        parts.Remove(root); parts.Add(root, new(title, hardware));
        title.TextWrapping = TextWrapping.Wrap; title.TextTrimming = TextTrimming.None; title.MaxLines = 0;
    }
    public static void Apply(Border root, WidgetKind kind, int index, Snapshot snapshot, History history, Settings settings, Palette palette)
    {
        if (!parts.TryGetValue(root, out var entry)) return;
        // A fitted card changes its logical width during arrange. Feeding that width back into a
        // title's measure constraint creates a wrap/scale/resize loop. Use the configured column
        // width instead; the native parent still constrains narrower layouts during measure.
        entry.Title.MaxWidth = Math.Max(100, settings.Width - root.Padding.Left - root.Padding.Right - 56);
        var summary = Summaries.For(kind, index, snapshot, history, settings, palette);
        var slot = settings.ActiveWidget;
        string title = Naming.Title(slot, index, summary.Title);
        string hardware = Naming.ShowHardware(slot, settings) ? Naming.Hardware(slot, index, summary.Name) : "";
        string fullHardware = hardware;
        string keys = "";
        if (kind == WidgetKind.Drives)
        {
            var models = CompleteReadings.For(kind, index, snapshot, history, settings, palette).Where(r => r.Key.EndsWith(":model")).ToArray();
            var shortNames = models.Select(r => r.Label.Split(" · ")[0] + " " + Names.CompactDrive(r.Value)).ToArray();
            hardware = string.Join("\n", shortNames);
            fullHardware = string.Join("\n", models.Select(r => r.Label.Split(" · ")[0] + " " + r.Value));
            keys = string.Join('|', models.Select(r => r.Key));
            if (slot is { HardwareName.Length: > 0 } && Naming.ShowHardware(slot, settings))
            {
                hardware = slot.HardwareName.Trim() + (hardware.Length > 0 ? "\n" + hardware : "");
                fullHardware = slot.HardwareName.Trim() + (fullHardware.Length > 0 ? "\n" + fullHardware : "");
            }
        }
        string combined = title.Length > 0 && hardware.Length > 0 ? title + (kind == WidgetKind.Drives ? "\n" : " · ") + hardware : title + hardware;
        bool caps = entry.Title.Text is { Length: > 0 } previous && previous == previous.ToUpper(L.Culture) && previous.Any(char.IsLetter);
        entry.Title.Text = caps ? combined.ToUpper(L.Culture) : combined;
        entry.Title.IsVisible = combined.Length > 0;
        entry.Title.TextWrapping = TextWrapping.Wrap; entry.Title.TextTrimming = TextTrimming.None; entry.Title.MaxLines = 0;
        if (kind == WidgetKind.Drives) { entry.Title.TextWrapping = TextWrapping.NoWrap; entry.Title.TextTrimming = TextTrimming.CharacterEllipsis; }
        if (entry.Title.HorizontalAlignment == HorizontalAlignment.Stretch) entry.Title.HorizontalAlignment = HorizontalAlignment.Left;
        if (entry.Hardware != null) entry.Hardware.IsVisible = false;
        if (kind == WidgetKind.Drives) SensorDetails.Value(entry.Title, keys);
        ToolTip.SetTip(entry.Title, string.Join("\n", new[] { title, fullHardware }.Where(s => s.Length > 0)));
        if (entry.Title.Parent is Grid grid)
        {
            int col = Grid.GetColumn(entry.Title);
            if (col < grid.ColumnDefinitions.Count) grid.ColumnDefinitions[col].Width = new GridLength(1, GridUnitType.Star);
            if (entry.Hardware != null && entry.Hardware.Parent == grid)
                grid.ColumnDefinitions[Grid.GetColumn(entry.Hardware)].Width = GridLength.Auto;
        }
    }
}
