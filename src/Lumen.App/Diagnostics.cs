using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Lumen.Core;

namespace Lumen.App;

public static class Diagnostics
{
    public const string Build = "3.0.0";
    private static readonly object Gate = new();
    private static string directory = "", context = "No layout captured yet.", stage = "startup", failure = "";
    private static readonly string SessionId = Guid.NewGuid().ToString("N");
    private static readonly DateTimeOffset Started = DateTimeOffset.Now;
    private static bool initialized, fatal, ended;
    private static string measure = "No measure captured yet.";
    public static string SettingsFile { get; private set; } = "";
    public static string DirectoryPath => directory;
    public static string Context => $"Build: {Build}\nStage: {stage}\nLast layout measure: {measure}\n{context}";
    public static string Status => $"Log directory: {(directory.Length == 0 ? "unavailable / not initialized" : directory)}\nLast logging failure: {(failure.Length == 0 ? "none" : failure)}\n{Context}";

    public static void Initialize(string? preferredFolder = null)
    {
        lock (Gate)
        {
            if (initialized) return;
            foreach (var candidate in new[] { preferredFolder ?? Path.GetDirectoryName(Platform.ExePath) ?? AppContext.BaseDirectory,
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lumen") }.Distinct())
            {
                try {
                    Directory.CreateDirectory(candidate);
                    File.AppendAllText(Path.Combine(candidate, "lumen-activity.log"), $"\n=== {DateTimeOffset.Now:O} · session {SessionId} · {Build}\n", Encoding.UTF8);
                    directory = candidate;
                    break;
                } catch (Exception e) { failure = e.GetType().Name + ": " + e.Message; }
            }
            initialized = true;
            if (directory.Length == 0) return;
            try {
                var marker = Path.Combine(directory, "lumen-session.json");
                if (File.Exists(marker)) {
                    var previous = File.ReadAllText(marker);
                    File.WriteAllText(Path.Combine(directory, "lumen-previous-session.json"), previous);
                    using var doc = JsonDocument.Parse(previous);
                    if (!doc.RootElement.TryGetProperty("CleanExit", out var clean) || !clean.GetBoolean())
                        Append("previous-session", "No clean exit was recorded. Possible crash, forced termination or power loss; cause unknown.");
                }
            } catch (Exception e) { failure = "Previous session: " + e.Message; }
            Append("startup", "Executable: " + Platform.ExePath + "\nBase directory: " + AppContext.BaseDirectory);
            SaveSession(false);
            AppDomain.CurrentDomain.ProcessExit += (_, _) => Complete();
        }
    }

    public static void SetLayout(Settings s, int page, string reason)
    {
        var next = $"Edge: {s.Edge}; horizontal: {s.Horizontal}; screen: {s.Screen}; rows/columns: {s.Columns}; " +
            $"bar height: {s.BarHeight}; width: {s.Width}; text: {s.TextScale:P0}; density: {s.Density}; register: {page}/{s.Pages}\n" +
            "Visible widgets: " + string.Join(", ", s.Widgets.Where(w => w.Visible).Select(w => $"{w.Kind}/{w.Style}@page{w.Page}"));
        lock (Gate) {
            if (context == next && stage == reason) return;
            context = next; stage = reason;
            Append("layout", Context); SaveSession(false);
        }
    }
    public static void SetSettingsPath(string path) => SettingsFile = path;
    public static void SetStage(string value) => stage = value;
    public static void RecordMeasure(double width, double height, double scale, int cells) =>
        measure = $"available {width:0.##} x {height:0.##}; scale {scale:0.####}; cells {cells}";
    public static void RecordException(Exception e, string kind)
    {
        lock (Gate) {
            if (kind == "fatal") fatal = true;
            Append("exception/" + kind, e + "\n" + Context);
            SaveSession(false);
        }
    }
    public static void Complete()
    {
        lock (Gate) {
            if (!initialized || ended) return;
            ended = true;
            Append(fatal ? "fatal-exit" : "shutdown", Context);
            SaveSession(!fatal);
        }
    }
    private static void Append(string name, string text)
    {
        if (directory.Length == 0) return;
        try {
            var path = Path.Combine(directory, "lumen-activity.log");
            Rotate(path);
            File.AppendAllText(path, $"[{DateTimeOffset.Now:O}] {name}\n{text}\n", Encoding.UTF8);
        } catch (Exception e) { failure = e.GetType().Name + ": " + e.Message; }
    }
    internal static void Rotate(string path)
    {
        if (File.Exists(path) && new FileInfo(path).Length > 1_048_576)
            File.Move(path, path + ".1", true);
    }
    private static void SaveSession(bool clean)
    {
        if (directory.Length == 0) return;
        try {
            var path = Path.Combine(directory, "lumen-session.json");
            var json = JsonSerializer.Serialize(new { SessionId, Started, Updated = DateTimeOffset.Now, CleanExit = clean,
                Fatal = fatal, Executable = Platform.ExePath, Build, Context }, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path + ".tmp", json, Encoding.UTF8);
            File.Move(path + ".tmp", path, true);
        } catch (Exception e) { failure = e.GetType().Name + ": " + e.Message; }
    }
}




