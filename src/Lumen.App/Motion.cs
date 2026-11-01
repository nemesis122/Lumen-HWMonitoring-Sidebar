using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Lumen.App;

// Smooth transitions: numbers count to their new value, bars and rings glide, charts scroll.
// One shared 60 fps timer runs only while something is moving, so an idle sidebar costs nothing.
public static class Motion
{
    public static bool Enabled { get; set; } = true;

    // How long a change takes; charts use most of the update interval so they scroll continuously.
    public static double Duration { get; set; } = 450;
    public static double ScrollDuration { get; set; } = 900;

    private sealed record Anim(long Start, double Ms, Action<double> Step);

    private static readonly Dictionary<object, Anim> active = new(ReferenceEqualityComparer.Instance);
    private static readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
    private static DispatcherTimer? timer;

    // Runs step(0..1, eased) for the given key; a new run for the same key replaces the old one.
    public static void Run(object key, double ms, Action<double> step)
    {
        if (!Enabled || ms <= 0) { active.Remove(key); step(1); return; }
        active[key] = new Anim(clock.ElapsedMilliseconds, ms, step);
        step(0);
        if (timer == null)
        {
            timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
            timer.Tick += (_, _) => Tick();
        }
        if (!timer.IsEnabled) timer.Start();
    }

    public static void Stop(object key) => active.Remove(key);

    // Advances running transitions by hand (headless rendering has no live timer).
    public static void Pump() => Tick();

    private static void Tick()
    {
        long now = clock.ElapsedMilliseconds;
        foreach (var (key, a) in active.ToList())
        {
            double t = Math.Clamp((now - a.Start) / a.Ms, 0, 1);
            double eased = 1 - Math.Pow(1 - t, 3);
            try { a.Step(eased); } catch { active.Remove(key); continue; }
            if (t >= 1) active.Remove(key);
        }
        if (active.Count == 0) timer?.Stop();
    }

    // "57 °C" → "61 °C" counts up through 58, 59, 60; anything that is not one number with the
    // same text around it (times, addresses, "—") is simply replaced.
    private static readonly Regex Number = new(@"^(\D*?)(-?\d+(?:\.(\d+))?)(\D*)$", RegexOptions.Compiled);

    public static void Text(TextBlock block, string text)
    {
        string old = block.Text ?? "";
        if (old == text) return;
        var a = Number.Match(old);
        var b = Number.Match(text);
        if (!Enabled || !a.Success || !b.Success || a.Groups[1].Value != b.Groups[1].Value || a.Groups[4].Value != b.Groups[4].Value)
        {
            Stop(block);
            block.Text = text;
            return;
        }
        double from = double.Parse(a.Groups[2].Value, CultureInfo.InvariantCulture);
        double to = double.Parse(b.Groups[2].Value, CultureInfo.InvariantCulture);
        int decimals = b.Groups[3].Value.Length;
        string prefix = b.Groups[1].Value, suffix = b.Groups[4].Value, format = "F" + decimals;
        Run(block, Duration, t => block.Text = t >= 1 ? text : prefix + (from + (to - from) * t).ToString(format, CultureInfo.InvariantCulture) + suffix);
    }
}
