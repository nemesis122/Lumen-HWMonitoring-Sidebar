using System.Reflection;
using System.Runtime.CompilerServices;
using Avalonia.Threading;
using Lumen.App;
using Lumen.Core;
using Xunit;

/// <summary>
/// Found by the 5.1.0 release check: every change in Settings builds all pages again. The network row of a page that was
/// not on screen subscribed to the static ExternalIp.Changed event and was never detached, so each change kept a full
/// copy of the old window (about 3 MB), and a closed Settings window was never freed.
/// </summary>
public class SettingsWindowMemoryTests
{
    private sealed class TestPlatform : Lumen.App.Platform
    {
        public override bool IsAdministrator => false;
        public override bool AutostartEnabled => false;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference OpenChangeAndClose(int page, bool show)
    {
        var s = Settings.Starter();
        var window = new SettingsWindow(s, Palette.For(true, false), () => Settings.Normalize(s), () => new[] { "Demo" }, new TestPlatform(), snapshot: () => new DemoSource().Read());
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        ((HashSet<WidgetKind>)typeof(SettingsWindow).GetField("expanded", flags)!.GetValue(window)!).Add(WidgetKind.Network);
        typeof(SettingsWindow).GetField("pageIndex", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, page);
        if (show) window.Show();
        var build = typeof(SettingsWindow).GetMethod("Build", flags)!;
        for (int i = 0; i < 3; i++) { build.Invoke(window, null); Dispatcher.UIThread.RunJobs(); }
        window.Close(); Dispatcher.UIThread.RunJobs();
        return new WeakReference(window);
    }

    [Theory, InlineData(0, true), InlineData(1, true), InlineData(1, false)]
    public void A_closed_settings_window_is_freed(int page, bool show) => UiHost.Run(() =>
    {
        var weak = OpenChangeAndClose(page, show);
        for (int i = 0; i < 4 && weak.IsAlive; i++) { Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick(2); Dispatcher.UIThread.RunJobs(); GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
        if (weak.IsAlive && Environment.GetEnvironmentVariable("LUMEN_DUMP") is { } dump)
            System.Diagnostics.Process.Start("/root/.dotnet/tools/dotnet-dump", $"collect -p {Environment.ProcessId} -o {dump} --type Heap")!.WaitForExit();
        Assert.False(weak.IsAlive, "the closed Settings window is still held in memory");
        return true;
    });
}
