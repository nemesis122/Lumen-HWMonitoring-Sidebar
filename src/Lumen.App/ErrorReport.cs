using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Lumen.Core;

namespace Lumen.App;

public static class ErrorReport
{
    public static string Create(Settings settings, string sensorReport, IReadOnlyList<string> screens, string? folder = null)
    {
        folder ??= DefaultFolder();
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, $"Lumen-Report-{DateTime.Now:yyyy-MM-dd-HHmmss-fff}-{Guid.NewGuid():N}.zip");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        void Add(string name, string value) {
            using var writer = new StreamWriter(zip.CreateEntry(name, CompressionLevel.Optimal).Open(), new UTF8Encoding(false));
            writer.Write(value);
        }
        Add("system.txt", SystemText(settings, screens));
        Add("sensors.txt", sensorReport);
        Add("settings.json", JsonSerializer.Serialize(Anonymous(settings), Settings.Json));
        var status = new StringBuilder(Diagnostics.Status + "\n\nLog sources:\n");
        var dirs = new[] {
            Path.GetDirectoryName(Platform.ExePath) ?? AppContext.BaseDirectory,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lumen"),
            Diagnostics.DirectoryPath,
            Path.GetDirectoryName(Diagnostics.SettingsFile) ?? ""
        }.Where(d => d.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (string name in new[] { "lumen-crash.log", "lumen-crash.log.1", "lumen-activity.log", "lumen-activity.log.1",
            "lumen-session.json", "lumen-previous-session.json", "lumen-restarts.txt" }) {
            var collected = new List<string>();
            foreach (string dir in dirs) {
                string file = Path.Combine(dir, name);
                try {
                    using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    bool tail = stream.Length > 200_000;
                    if (tail) stream.Seek(-200_000, SeekOrigin.End);
                    using var reader = new StreamReader(stream);
                    collected.Add("--- " + file + "\n" + (tail ? "[last 200 KB]\n" : "") + reader.ReadToEnd());
                    status.AppendLine(file + ": included" + (tail ? " (tail only)" : ""));
                } catch (FileNotFoundException) { status.AppendLine(file + ": missing"); }
                catch (DirectoryNotFoundException) { status.AppendLine(file + ": directory missing"); }
                catch (Exception e) { status.AppendLine(file + ": READ FAILED: " + e.GetType().Name + ": " + e.Message); }
            }
            if (collected.Count > 0) Add(name, string.Join("\n", collected));
            else if (name == "lumen-crash.log") Add(name,
                "No managed exception log was available. This does not rule out a crash, native failure, forced termination or power loss. See report-status.txt.\n");
        }
        Add("report-status.txt", status.ToString());
        Add("session-info.txt", "A running session has CleanExit=false until normal process exit. A previous session without a clean exit is a clue, not proof of a crash.\n" +
            "The report includes the executable path, current layout, available activity/crash logs and restart history. No data is sent automatically.\n");
        return path;
    }


	public static string DefaultFolder()
	{
		string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
		if (folderPath.Length <= 0 || !Directory.Exists(folderPath))
		{
			return Path.GetTempPath();
		}
		return folderPath;
	}

	public static Settings Anonymous(Settings s)
	{
		Settings settings = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(s, Settings.Json), Settings.Json);
		settings.Notes = ((settings.Notes.Length > 0) ? "(removed)" : "");
		settings.WeatherPlace = ((settings.WeatherPlace.Length > 0) ? "(removed)" : "");
		settings.WeatherLatitude = ((!settings.WeatherLatitude.HasValue) ? ((double?)null) : new double?(0.0));
		settings.WeatherLongitude = ((!settings.WeatherLongitude.HasValue) ? ((double?)null) : new double?(0.0));
		settings.PingTarget = "(removed)";
		settings.Labels = settings.Labels.ToDictionary(x => x.Key, x => x.Value == "-" ? "-" : "(own label)");
		foreach (WidgetSlot widget in settings.Widgets)
		{
			if (widget.Name.Length > 0)
			{
				widget.Name = "(own name)";
			}
			if (widget.HardwareName.Length > 0)
			{
				widget.HardwareName = "(own name)";
			}
		}
		return settings;
	}

	private static string SystemText(Settings s, IReadOnlyList<string> screens)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("Executable: " + Platform.ExePath);
		stringBuilder.AppendLine("Executable directory: " + Path.GetDirectoryName(Platform.ExePath));
		stringBuilder.AppendLine("Build: " + Diagnostics.Build);
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder3 = stringBuilder2;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(19, 2, stringBuilder2);
		handler.AppendLiteral("Lumen ");
		handler.AppendFormatted(typeof(ErrorReport).Assembly.GetName().Version?.ToString(3));
		handler.AppendLiteral(" · report of ");
		handler.AppendFormatted(DateTime.Now, "yyyy-MM-dd HH:mm:ss");
		stringBuilder3.AppendLine(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder4 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(12, 2, stringBuilder2);
		handler.AppendLiteral("Windows: ");
		handler.AppendFormatted(RuntimeInformation.OSDescription);
		handler.AppendLiteral(" (");
		handler.AppendFormatted(RuntimeInformation.OSArchitecture);
		handler.AppendLiteral(")");
		stringBuilder4.AppendLine(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder5 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(6, 1, stringBuilder2);
		handler.AppendLiteral(".NET: ");
		handler.AppendFormatted(RuntimeInformation.FrameworkDescription);
		stringBuilder5.AppendLine(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder6 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(20, 1, stringBuilder2);
		handler.AppendLiteral("Processors: ");
		handler.AppendFormatted(Environment.ProcessorCount);
		handler.AppendLiteral(" logical");
		stringBuilder6.AppendLine(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder7 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(15, 1, stringBuilder2);
		handler.AppendLiteral("Administrator: ");
		handler.AppendFormatted((OperatingSystem.IsWindows() && new Platform().IsAdministrator) ? "yes" : "no");
		stringBuilder7.AppendLine(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder8 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(18, 2, stringBuilder2);
		handler.AppendLiteral("Sensor source: ");
		handler.AppendFormatted(s.SensorSource);
		handler.AppendLiteral(" · ");
		handler.AppendFormatted(ExternalSensors.Status);
		stringBuilder8.AppendLine(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder9 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(48, 5, stringBuilder2);
		handler.AppendLiteral("Layout: edge ");
		handler.AppendFormatted(s.Edge);
		handler.AppendLiteral(", ");
		handler.AppendFormatted(s.Horizontal ? $"{s.Rows} rows of {s.BarHeight:0} px" : $"{s.Columns} column(s), width {s.Width:0}");
		handler.AppendLiteral(", ");
		handler.AppendFormatted(s.Pages);
		handler.AppendLiteral(" register(s), density ");
		handler.AppendFormatted(s.Density);
		handler.AppendLiteral(", text ");
		handler.AppendFormatted(s.TextScale * 100.0, "0");
		handler.AppendLiteral(" %");
		stringBuilder9.AppendLine(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder10 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(50, 5, stringBuilder2);
		handler.AppendLiteral("Look: theme ");
		handler.AppendFormatted(s.Theme);
		handler.AppendLiteral(", colours ");
		handler.AppendFormatted(s.Colors);
		handler.AppendLiteral(", background ");
		handler.AppendFormatted(s.Background);
		handler.AppendLiteral(", glass ");
		handler.AppendFormatted(s.Glass);
		handler.AppendLiteral(", font ");
		handler.AppendFormatted(s.Font);
		stringBuilder10.AppendLine(ref handler);
		stringBuilder.AppendLine("Screens: " + ((screens.Count == 0) ? "—" : string.Join(" | ", screens)));
		stringBuilder.AppendLine("Widgets: " + string.Join(", ", from w in s.Widgets
			where w.Visible
			select $"{w.Kind}/{w.Style}"));
		try
		{
			using Process process = Process.GetCurrentProcess();
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder11 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(85, 6, stringBuilder2);
			handler.AppendLiteral("Lumen process: up ");
			handler.AppendFormatted((DateTime.Now - process.StartTime).TotalMinutes, "0");
			handler.AppendLiteral(" min, CPU time ");
			handler.AppendFormatted(process.TotalProcessorTime.TotalSeconds, "0");
			handler.AppendLiteral(" s, working set ");
			handler.AppendFormatted(process.WorkingSet64 / 1048576);
			handler.AppendLiteral(" MB, private ");
			handler.AppendFormatted(process.PrivateMemorySize64 / 1048576);
			handler.AppendLiteral(" MB, ");
			handler.AppendFormatted(process.Threads.Count);
			handler.AppendLiteral(" threads, ");
			handler.AppendFormatted(process.HandleCount);
			handler.AppendLiteral(" handles");
			stringBuilder11.AppendLine(ref handler);
		}
		catch
		{
		}
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder12 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(33, 4, stringBuilder2);
		handler.AppendLiteral("Managed heap: ");
		handler.AppendFormatted((double)GC.GetTotalMemory(forceFullCollection: false) / 1048576.0, "0.0");
		handler.AppendLiteral(" MB, collections ");
		handler.AppendFormatted(GC.CollectionCount(0));
		handler.AppendLiteral("/");
		handler.AppendFormatted(GC.CollectionCount(1));
		handler.AppendLiteral("/");
		handler.AppendFormatted(GC.CollectionCount(2));
		stringBuilder12.AppendLine(ref handler);
		return stringBuilder.ToString();
	}

	public static void Reveal(string path)
	{
		if (!OperatingSystem.IsWindows())
		{
			return;
		}
		try
		{
			Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + path + "\"")
			{
				UseShellExecute = true
			});
		}
		catch
		{
		}
	}
}

