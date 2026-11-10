using System.Text.RegularExpressions;

namespace Lumen.Core;

public static class DriveOrdering
{
    public static readonly string[] Modes = { "system", "letters", "detected" };
    public static string Mode(Settings settings) => settings.ActiveWidget?.DriveOrder ?? settings.Widgets.FirstOrDefault(w => w.Kind == WidgetKind.Drives)?.DriveOrder ?? "system";
    public static DiskInfo[] Order(IEnumerable<DiskInfo> drives, string? mode = "system")
    {
        if (mode == "detected") return drives.ToArray();
        static string Letters(DiskInfo disk) => string.Concat(Regex.Matches(disk.Letters ?? "", @"(?<![A-Za-z0-9])([A-Za-z])\s*:")
            .Select(m => m.Groups[1].Value.ToUpperInvariant()).OrderBy(s => s, StringComparer.Ordinal));
        // LINQ's stable sort retains source order for unmounted disks and tied letters.
        return drives.Select(d => (Disk: d, Letters: Letters(d)))
            .OrderBy(d => mode != "letters" && d.Letters.Contains('C') ? 0 : 1)
            .ThenBy(d => d.Letters.Length > 0 ? d.Letters[..1] : "~", StringComparer.Ordinal)
            .Select(d => d.Disk).ToArray();
    }
}
