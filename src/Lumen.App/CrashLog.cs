using System;
using System.IO;

namespace Lumen.App;

public static class CrashLog
{
    private static readonly object Gate = new();
    public static string? LastPath { get; private set; }
    public static void Write(Exception? e, string kind)
    {
        if (e == null) return;
        try { Diagnostics.RecordException(e, kind); } catch { }
        var contents = $"=== {DateTimeOffset.Now:O} · Lumen {typeof(CrashLog).Assembly.GetName().Version?.ToString(3)} · {kind}\n" +
            $"Executable: {Platform.ExePath}\n{Diagnostics.Context}\n{e}\n\n";
        lock (Gate) {
            foreach (var folder in new[] { Path.GetDirectoryName(Platform.ExePath) ?? AppContext.BaseDirectory,
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lumen") }) {
                try {
                    Directory.CreateDirectory(folder);
                    var path = Path.Combine(folder, "lumen-crash.log");
                    Diagnostics.Rotate(path);
                    File.AppendAllText(path, contents);
                    LastPath = path;
                    break;
                } catch { }
            }
        }
    }
}
