using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Avalonia.Threading;
using Lumen.App;
using Lumen.Core;
using Xunit;

/// <summary>
/// Release check 5.1.0, LUMEN_RELEASE_CHECK=1 for all designs (user request: "Sensor zuordnen, ausschalten, einschalten, Darstellung, History-Chart ab- und
/// einschalten – auf alles"): every design, driven end to end through the real Settings window. Each sensor switch
/// (history charts included) is clicked off and on again in Settings → Widgets; after every click the sidebar is built
/// again the way the app does it (Normalize, new SidebarView, Update). Each appearance control (Settings → Appearance
/// and the per-widget sizes, titles and colours) is changed and set back the same way.
/// </summary>
public class ReleaseSettingsUiTests
{
    private sealed class TestPlatform : Lumen.App.Platform
    {
        public override bool IsAdministrator => false;
        public override bool AutostartEnabled => false;
    }

    /// <summary>Without LUMEN_RELEASE_CHECK=1 (the full check takes hours), a few designs of every kind: a card design,
    /// the Dial, a Cyberpunk, a Neumorph and two art designs.</summary>
    private static readonly WidgetStyle[] Quick = { WidgetStyle.Standard, WidgetStyle.Dial, WidgetStyle.AioCyberArasaka, WidgetStyle.AioNeoHero, WidgetStyle.AioArtThermal, WidgetStyle.AioArtDotMatrix };

    private static IEnumerable<WidgetStyle> Shard()
    {
        var all = Settings.Selectable.Distinct().ToArray();
        if (Environment.GetEnvironmentVariable("LUMEN_STYLES") is { Length: > 0 } names)
            all = all.Where(s => names.Split(',').Contains(s.ToString())).ToArray();
        else if (Environment.GetEnvironmentVariable("LUMEN_RELEASE_CHECK") != "1" && Environment.GetEnvironmentVariable("LUMEN_SHARD") == null)
            all = all.Where(Quick.Contains).ToArray();
        if (Environment.GetEnvironmentVariable("LUMEN_SHARD") is { } shard && shard.Split('/') is [var a, var b] && int.TryParse(a, out int i) && int.TryParse(b, out int n))
            return all.Where((_, k) => k % n == i);
        return all;
    }
    private static string[] HardwareSet() => (Environment.GetEnvironmentVariable("LUMEN_RELEASE_HARDWARE") ?? "demo").Split(',');
    public static IEnumerable<object[]> Designs => Shard().SelectMany(s => HardwareSet().Select(h => new object[] { s, h }));

    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    /// <summary>The app as the user sees it: settings, the Settings window and the sidebar rebuilt after each change.</summary>
    private sealed class Rig : IDisposable
    {
        public readonly Settings S;
        public readonly Snapshot Snap; public readonly History H;
        public readonly SettingsWindow Win;
        public readonly Window Host;
        public int Changes;
        private bool dirty = true;
        private HashSet<(string Card, string Key, char Kind)> shown = new();
        public SidebarView? View;
        private static int tick;

        public Rig(WidgetStyle style, string hardware)
        {
            L.Use("en");
            S = Settings.Starter(); DesignWidgetSelection.Apply(S, style); Settings.Normalize(S);
            (Snap, H) = HardwareVariants.For(hardware);
            Win = new SettingsWindow(S, Palette.For(S, true, null), () => { Settings.Normalize(S); Changes++; dirty = true; }, () => new[] { "Demo" }, new TestPlatform(), snapshot: () => Snap);
            Host = new Window { Width = 340, Height = 3200 };
            Host.Show();
        }

        /// <summary>Only this widget opened in Settings → Widgets (null: all closed).</summary>
        public void Open(WidgetKind? kind)
        {
            var expanded = (HashSet<WidgetKind>)typeof(SettingsWindow).GetField("expanded", Flags)!.GetValue(Win)!;
            expanded.Clear(); if (kind is { } k) expanded.Add(k);
        }
        public WidgetKind[] Kinds => S.Widgets.Where(w => w.Visible).Select(w => w.Kind).Distinct().ToArray();

        public void Page(int page)
        {
            typeof(SettingsWindow).GetField("pageIndex", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, page);
            typeof(SettingsWindow).GetMethod("Build", Flags)!.Invoke(Win, null);
        }
        public StackPanel Content => (StackPanel)typeof(SettingsWindow).GetField("content", Flags)!.GetValue(Win)!;
        public CheckBox? Check(string key) => Content.GetLogicalDescendants().OfType<CheckBox>().FirstOrDefault(c => Equals(c.Tag, key));
        public void Touch() => dirty = true;

        /// <summary>What the rebuilt sidebar shows: per card, sensor key and kind of control.</summary>
        public HashSet<(string Card, string Key, char Kind)> Shown()
        {
            if (!dirty) return shown;
            Ui.UseFonts(S); Motion.Enabled = false; HistorySwitchEffectTests.ClearDayStats();
            View = new SidebarView(S, Palette.For(S, true, null));
            Host.Width = Math.Max(120, SidebarView.WindowWidth(S, 1200));
            Host.Content = View;
            Dispatcher.UIThread.RunJobs();
            // Readings at later and later times, as in the app: designs that collect their own short curves get some.
            for (int i = 0; i < 4; i++) { View.Update(Snap with { Time = Snap.Time.AddSeconds(++tick) }, H); Dispatcher.UIThread.RunJobs(); }
            var cards = (Dictionary<WidgetKind, List<Control>>)typeof(SidebarView).GetField("builtCards", Flags)!.GetValue(View)!;
            shown = new();
            foreach (var (kind, list) in cards)
                for (int i = 0; i < list.Count; i++)
                    foreach (var (key, k) in AioSensorSelectionTests.Shown(list[i])) shown.Add((kind + "#" + i, key, k));
            dirty = false;
            return shown;
        }

        /// <summary>Text with a sensor key that sticks out of its card (horizontally) or has no size.</summary>
        public List<string> Outside()
        {
            Shown();
            var result = new List<string>();
            var cards = (Dictionary<WidgetKind, List<Control>>)typeof(SidebarView).GetField("builtCards", Flags)!.GetValue(View!)!;
            foreach (var (kind, list) in cards)
                foreach (var card in list)
                {
                    if (!card.IsEffectivelyVisible || card.Bounds.Width < 1) continue;
                    foreach (var t in card.GetLogicalDescendants().OfType<TextBlock>())
                    {
                        if (!SensorDetails.Visible(t, card) || SensorDetails.KeyOf(t).Length == 0 || string.IsNullOrWhiteSpace(t.Text)) continue;
                        if (t.GetLogicalAncestors().TakeWhile(a => a != card).OfType<ScrollViewer>().Any()) continue;
                        // The ink of the text (alignment and scaling included), in card coordinates.
                        var layout = t.TextLayout;
                        if (layout is null || layout.TextLines.Count == 0) continue;
                        double left = t.Padding.Left + layout.TextLines.Min(l => l.Start);
                        double right = t.Padding.Left + layout.TextLines.Max(l => l.Start + l.Width);
                        if (t.TranslatePoint(new Point(left, 0), card) is not { } a || t.TranslatePoint(new Point(right, t.Bounds.Height), card) is not { } b) continue;
                        double x0 = Math.Min(a.X, b.X), x1 = Math.Max(a.X, b.X);
                        // Within a clipping parent the text may only go as far as that parent shows.
                        double min = 0, max = card.Bounds.Width;
                        foreach (var parent in t.GetVisualAncestors().OfType<Visual>().TakeWhile(v => v != card))
                            if (parent.ClipToBounds && parent.TranslatePoint(default, card) is { } c && parent.TranslatePoint(new Point(parent.Bounds.Width, 0), card) is { } d)
                            { min = Math.Max(min, Math.Min(c.X, d.X)); max = Math.Min(max, Math.Max(c.X, d.X)); }
                        if (x0 < min - 1.5 || x1 > max + 1.5)
                            result.Add($"{kind} '{t.Text}' ({SensorDetails.KeyOf(t)}) ink {x0:0}–{x1:0}, visible {min:0}–{max:0}");
                    }
                }
            return result;
        }

        /// <summary>The pixels of the cards of one widget, as the user sees them.</summary>
        public string Picture(WidgetKind kind)
        {
            Shown();
            using var frame = Host.CaptureRenderedFrame()!;
            var cards = (Dictionary<WidgetKind, List<Control>>)typeof(SidebarView).GetField("builtCards", Flags)!.GetValue(View!)!;
            if (!cards.TryGetValue(kind, out var list)) return "";
            using var buffer = frame.Lock();
            var hash = new System.Text.StringBuilder();
            foreach (var card in list)
            {
                if (card.TranslatePoint(default, Host) is not { } at) continue;
                int x0 = Math.Max(0, (int)at.X), y0 = Math.Max(0, (int)at.Y);
                int x1 = Math.Min(buffer.Size.Width, (int)(at.X + card.Bounds.Width)), y1 = Math.Min(buffer.Size.Height, (int)(at.Y + card.Bounds.Height));
                var row = new byte[Math.Max(0, x1 - x0) * 4];
                using var sha = System.Security.Cryptography.SHA1.Create();
                for (int y = y0; y < y1; y++)
                {
                    System.Runtime.InteropServices.Marshal.Copy(buffer.Address + y * buffer.RowBytes + x0 * 4, row, 0, row.Length);
                    sha.TransformBlock(row, 0, row.Length, null, 0);
                }
                sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                hash.Append(Convert.ToHexString(sha.Hash!)).Append($"@{x0},{y0},{x1},{y1};").Append(HistorySwitchEffectTests.Charts(card));
            }
            return hash.ToString();
        }

        public string Json() => Canon(Raw());
        public string Raw() => JsonSerializer.Serialize(S, Settings.Json);

        private static readonly HashSet<string> SetNames = new() { "HiddenSensors", "AioEnabledSensors", "HiddenValues" };
        /// <summary>Settings with the order of switched-off lists left out (it does not matter).</summary>
        private static string Canon(string json)
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(json)!;
            void Walk(System.Text.Json.Nodes.JsonNode? n)
            {
                if (n is System.Text.Json.Nodes.JsonArray a) foreach (var e in a) Walk(e);
                if (n is not System.Text.Json.Nodes.JsonObject o) return;
                foreach (var (name, value) in o.ToList())
                {
                    if (SetNames.Contains(name) && value is System.Text.Json.Nodes.JsonArray set)
                    {
                        var sorted = set.Select(e => e!.ToJsonString()).OrderBy(t => t, StringComparer.Ordinal).ToList();
                        set.Clear(); foreach (var t in sorted) set.Add(System.Text.Json.Nodes.JsonNode.Parse(t));
                    }
                    else Walk(value);
                }
            }
            Walk(node);
            return node.ToJsonString();
        }

        /// <summary>Back to saved settings, the way a restart with that settings file would be.</summary>
        public void Restore(string json)
        {
            var clone = JsonSerializer.Deserialize<Settings>(json, Settings.Json)!;
            foreach (var p in typeof(Settings).GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0))
                p.SetValue(S, p.GetValue(clone));
            dirty = true;
        }

        public void Dispose() { Win.Close(); Host.Close(); }
    }

    private static bool Belongs(WidgetKind widget, string option, (string Card, string Key, char Kind) item)
    {
        if (item.Key.StartsWith("aio.") || option.StartsWith("aio.")) return AioSensorSelectionTests.Belongs(option, (item.Key, item.Kind));
        // A switch of one widget only touches that widget's cards.
        if (!item.Card.StartsWith(widget + "#")) return false;
        // The bar of a drive is its "used" bar.
        if (option == "drives.bar" && item.Kind == 'g' && item.Key.EndsWith(":used")) return true;
        return WidgetSensorSelectionTests.Belongs(option, (item.Key, item.Kind));
    }

    private static string Diff(string a, string b)
    {
        var x = JsonDocument.Parse(a).RootElement; var y = JsonDocument.Parse(b).RootElement;
        var props = x.EnumerateObject().Select(p => p.Name).Union(y.EnumerateObject().Select(p => p.Name));
        string Raw(JsonElement e, string p) => e.TryGetProperty(p, out var v) ? v.GetRawText() : "";
        var parts = new List<string>();
        foreach (var p in props.Where(p => Raw(x, p) != Raw(y, p)).Take(4))
        {
            if (p == "Widgets" && x.GetProperty(p).GetArrayLength() == y.GetProperty(p).GetArrayLength())
            {
                var xa = x.GetProperty(p).EnumerateArray().ToArray(); var ya = y.GetProperty(p).EnumerateArray().ToArray();
                for (int i = 0; i < xa.Length; i++)
                    foreach (var q in xa[i].EnumerateObject().Select(o => o.Name).Union(ya[i].EnumerateObject().Select(o => o.Name)))
                        if (Raw(xa[i], q) != Raw(ya[i], q)) parts.Add($"{Raw(xa[i], "Kind")}.{q}: {Cut(Raw(xa[i], q))} → {Cut(Raw(ya[i], q))}");
            }
            else parts.Add($"{p}: {Cut(Raw(x, p))} → {Cut(Raw(y, p))}");
        }
        return string.Join("; ", parts.Take(4));
        static string Cut(string t) => t.Length > 160 ? t[..160] + "…" : t;
    }

    private static string Card(System.Text.RegularExpressions.Match m) => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
    private static string Short(IEnumerable<(string Card, string Key, char Kind)> items) => string.Join(", ", items.Take(5).Select(i => $"{i.Card}:{i.Key}/{i.Kind}"));

    internal static List<string> SensorProblems(WidgetStyle style, string hardware, List<string> notes)
    {
        var problems = new List<string>();
        using var rig = new Rig(style, hardware);
        var baseline = rig.Shown().ToHashSet();
        string json0 = rig.Json(), raw0 = rig.Raw();
        var outside0 = rig.Outside();
        foreach (var o in outside0.Take(3)) problems.Add("start: outside its card: " + o);
        int total = 0;
        foreach (var kind in rig.Kinds)
        {
        rig.Open(kind); rig.Page(0);
        var keys = rig.Content.GetLogicalDescendants().OfType<CheckBox>().Select(c => c.Tag as string).OfType<string>().Distinct().ToList();
        total += keys.Count;
        foreach (var key in keys)
        {
            var box = rig.Check(key);
            if (box is null) { problems.Add($"'{key}': switch is gone from Settings"); continue; }
            if (!box.IsEnabled || box.IsChecked is null) continue;
            bool was = box.IsChecked == true;
            bool historySwitch = key.EndsWith(".history") || key.EndsWith(".chart");
            // Every AIO switch must change something on this PC (5.7.0, user request: no switch without effect).
            bool aio = key.StartsWith("aio.");
            string pictureOn = was || historySwitch || aio ? rig.Picture(kind) : "";
            int before = rig.Changes;
            box.IsChecked = !was;
            if (rig.Changes == before) { problems.Add($"'{key}': clicking the switch applies nothing"); continue; }
            var changed = rig.Shown().ToHashSet();
            var again = rig.Check(key);
            if (again is null) { problems.Add($"'{key}': switch is gone after switching it {(was ? "off" : "on")}"); rig.Restore(raw0); continue; }
            if (again.IsChecked != !was) problems.Add($"'{key}': switched {(was ? "off" : "on")}, Settings shows it {(again.IsChecked == true ? "on" : again.IsChecked == false ? "off" : "half")}");
            var gone = baseline.Except(changed).ToList(); var added = changed.Except(baseline).ToList();
            if (was)
            {
                var foreign = gone.Where(i => !Belongs(kind, key, i)
                    // Its graphic may change while the value itself stays (a combined bar becomes a single one).
                    && !(i.Kind is 'g' or 'h' && changed.Any(c => c.Key == i.Key))).ToList();
                // A design may draw another graphic of the same part, or fill a freed place with another value of the same widget.
                added = added.Where(i => !(i.Kind is 'g' or 'h' && (i.Key.StartsWith("aio.") ? AioSensorSelectionTests.SamePart(key, i.Key) : WidgetSensorSelectionTests.Head(i.Key) == WidgetSensorSelectionTests.Head(key)))
                    && !(!i.Key.StartsWith("aio.") && WidgetSensorSelectionTests.Head(i.Key) == WidgetSensorSelectionTests.Head(key))
                    && i.Key != WidgetSensors.PrimaryKey(Enum.Parse<WidgetKind>(i.Card.Split('#')[0]))).ToList();
                if (foreign.Count > 0) problems.Add($"'{key}' off also removes: {Short(foreign)}");
                if (added.Count > 0) problems.Add($"'{key}' off adds: {Short(added)}");
                if (baseline.Any(i => Belongs(kind, key, i)) && gone.Count == 0 && added.Count == 0 && pictureOn == rig.Picture(kind)) problems.Add($"'{key}' off: the sidebar still shows it ({Short(baseline.Where(i => Belongs(kind, key, i)))})");
                else if (aio && gone.Count == 0 && added.Count == 0 && pictureOn == rig.Picture(kind)) problems.Add($"'{key}' off: the sidebar does not change (a switch without effect)");
            }
            else
            {
                var foreign = added.Where(i => !Belongs(kind, key, i)).ToList();
                if (foreign.Count > 0) problems.Add($"'{key}' on also adds: {Short(foreign)}");
                // A fan of the merged list named "CPU …", ticked, is the processor fan: it is shown under its own name instead of
                // the plain "CPU" fan (one fan, listed once).
                if (key.StartsWith("aio.cooling.merged:") && key.Contains("CPU", StringComparison.OrdinalIgnoreCase))
                    gone = gone.Where(i => i.Key != "aio.cooling.cpu").ToList();
                if (gone.Count > 0) problems.Add($"'{key}' on removes: {Short(gone)}");
                // A history chart switched on must show something (the demo has history for every reading).
                if ((historySwitch || aio) && added.Count == 0 && gone.Count == 0 && pictureOn == rig.Picture(kind)) problems.Add($"'{key}' on: the sidebar does not change (a switch without effect)");
            }
            foreach (var o in rig.Outside().Except(outside0).Take(2)) problems.Add($"'{key}' {(was ? "off" : "on")}: outside its card: {o}");
            // And back, by clicking the same switch again.
            again.IsChecked = was;
            var back = rig.Shown();
            bool sameSidebar = back.SetEquals(baseline), opened = false;
            if (!sameSidebar)
            {
                var extra = back.Except(baseline).ToList(); var missing = baseline.Except(back).ToList();
                // A value of a graphics card that is switched off, ticked, brings that card onto the sidebar (by design).
                var gpu = System.Text.RegularExpressions.Regex.Match(key, @"^aio\.(?:gpu\.([^.]+)\.|cooling\.gpu:(.+)$)");
                // Ticked and unticked again, the card stays with its other values and its fan (the card has its own switch).
                bool opensCard = gpu.Success && missing.Count == 0 && extra.All(i => i.Key.StartsWith("aio.gpu." + Card(gpu) + ".") || i.Key == "aio.cooling.gpu:" + Card(gpu))
                    // The instrument designs leave out voltage rails that report nothing (the demo has none) until one is ticked by hand.
                    || key.StartsWith("aio.rails.") && missing.Count == 0 && extra.All(i => i.Key.StartsWith("aio.rails."));
                opened = opensCard;
                (opensCard ? notes : problems).Add($"'{key}': switched back {(was ? "on" : "off")}, the sidebar differs (missing {Short(missing)}; extra {Short(extra)})");
            }
            string json = rig.Json();
            // Different settings with the same sidebar: only the record of what was ticked by hand (kept as a note).
            if (json != json0) { (sameSidebar || opened ? notes : problems).Add($"'{key}': switched back, settings differ ({Diff(json0, json)})"); rig.Restore(raw0); rig.Page(0); }
        }
        }
        if (total == 0) problems.Add("no sensor switches in Settings → Widgets");
        return problems;
    }

    /// <summary>For the audit: every switch in Settings flipped once; those after which neither the values shown nor the
    /// pixels of the widget change ("on"/"off" = the state the switch was in).</summary>
    internal static List<string> SwitchesWithoutEffect(WidgetStyle style, string hardware)
    {
        var result = new List<string>();
        using var rig = new Rig(style, hardware);
        string raw0 = rig.Raw(), json0 = rig.Json();
        foreach (var kind in rig.Kinds)
        {
            rig.Open(kind); rig.Page(0);
            var keys = rig.Content.GetLogicalDescendants().OfType<CheckBox>().Select(c => c.Tag as string).OfType<string>().Distinct().ToList();
            foreach (var key in keys)
            {
                var box = rig.Check(key);
                if (box is null || !box.IsEnabled || box.IsChecked is null) continue;
                bool was = box.IsChecked == true;
                var before = rig.Shown().ToHashSet(); string picture = rig.Picture(kind);
                box.IsChecked = !was;
                var after = rig.Shown().ToHashSet();
                if (after.SetEquals(before) && rig.Picture(kind) == picture) result.Add($"{kind}	{key}	{(was ? "on" : "off")}");
                if (rig.Check(key) is { } again) again.IsChecked = was;
                if (rig.Json() != json0) { rig.Restore(raw0); rig.Page(0); }
            }
        }
        return result;
    }

    [Theory, MemberData(nameof(Designs))]
    public void Every_sensor_and_history_switch_off_and_on_through_settings(WidgetStyle style, string hardware) => UiHost.Run(() =>
    {
        var notes = new List<string>();
        var problems = SensorProblems(style, hardware, notes);
        Report("sensors", style, hardware, problems.Concat(notes.Select(n => "NOTE " + n)).ToList());
        Assert.True(problems.Count == 0, string.Join("\n", problems.Take(30)));
        return true;
    }, 1800);

    private static void Report(string what, WidgetStyle style, string hardware, List<string> problems)
    {
        if (Environment.GetEnvironmentVariable("LUMEN_RELEASE_REPORT") is { Length: > 0 } dir)
        { Directory.CreateDirectory(dir); File.WriteAllLines(Path.Combine(dir, $"{what}-{style}-{hardware}.txt"), problems); }
    }

    /// <summary>One change per appearance control, and the way back.</summary>
    private sealed record Step(string Name, Func<Control, bool> Apply, Func<Control, bool>? Back);

    private static IEnumerable<(int Page, int Index, Control Control)> AppearanceControls(Rig rig, int page)
    {
        rig.Page(page);
        var list = rig.Content.GetLogicalDescendants().OfType<Control>().Where(c => c.IsEffectivelyEnabled && c switch
        {
            CheckBox cb => cb.Tag is null && cb.Name?.StartsWith("widget-") != true,
            ToggleSwitch => true,
            ComboBox => true,
            Slider => true,
            Button b => page == 1 && b.Name is not { Length: > 0 } && b is not ToggleButton,
            _ => false
        }).ToList();
        for (int i = 0; i < list.Count; i++) yield return (page, i, list[i]);
    }

    private static string Describe(Control c)
    {
        string label = c.GetLogicalAncestors().OfType<Control>().Select(a => a.GetLogicalChildren().OfType<TextBlock>().FirstOrDefault()?.Text).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t)) ?? "";
        string own = c is ContentControl cc ? cc.Content as string ?? (cc.Content as TextBlock)?.Text ?? "" : "";
        return $"{c.GetType().Name}{(c.Name is { Length: > 0 } ? "#" + c.Name : "")} '{(own.Length > 0 ? own : label)}'";
    }

    internal static List<string> AppearanceProblems(WidgetStyle style, string hardware, List<string> notes)
    {
        var problems = new List<string>();
        using var rig = new Rig(style, hardware);
        var baseline = rig.Shown().ToHashSet();
        var keys0 = baseline.Select(i => i.Key).ToHashSet();
        string json0 = rig.Json(), raw0 = rig.Raw();
        var outside0 = rig.Outside();
        foreach (var (page, open) in rig.Kinds.Select(k => (0, (WidgetKind?)k)).Append((1, null)))
        {
            rig.Open(open);
            int count = AppearanceControls(rig, page).Count();
            for (int index = 0; index < count; index++)
            {
                var variants = new List<string> { "a" };
                var probe = AppearanceControls(rig, page).ElementAtOrDefault(index).Control;
                if (probe is null) { problems.Add($"{open} page {page} #{index}: control is gone"); break; }
                if (probe is Slider) variants = new() { "max", "min" };
                if (probe is ComboBox pc && pc.ItemCount > 2) variants = new() { "last", "next" };
                foreach (var variant in variants)
                {
                    var c = AppearanceControls(rig, page).ElementAtOrDefault(index).Control;
                    if (c is null) break;
                    string name = $"{(open is { } ok ? "Widgets/" + ok : "Appearance")} {Describe(c)} {variant}";
                    int before = rig.Changes;
                    object? old = null;
                    try
                    {
                        switch (c)
                        {
                            case ToggleButton t: old = t.IsChecked; t.IsChecked = t.IsChecked != true; break;
                            case ComboBox cb:
                                old = cb.SelectedIndex;
                                if (cb.ItemCount < 2) continue;
                                cb.SelectedIndex = variant == "next" ? (cb.SelectedIndex + 1) % cb.ItemCount : (cb.SelectedIndex == cb.ItemCount - 1 ? 0 : cb.ItemCount - 1);
                                break;
                            case Slider sl:
                                old = sl.Value;
                                double target = variant == "max" ? sl.Maximum : sl.Minimum;
                                if (Math.Abs(sl.Value - target) < 1e-9) continue;
                                sl.Value = target; break;
                            case Button b: b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); break;
                        }
                    }
                    catch (Exception e) { problems.Add($"{name}: throws {e.GetType().Name}: {e.Message}"); rig.Restore(raw0); continue; }
                    string changedJson = rig.Json();
                    if (rig.Changes == before && changedJson == json0) continue; // nothing to change in this state (e.g. already chosen)
                    HashSet<(string Card, string Key, char Kind)> shown;
                    try { shown = rig.Shown().ToHashSet(); }
                    catch (Exception e) { problems.Add($"{name}: sidebar cannot be built ({e.GetType().Name}: {e.Message})"); rig.Restore(raw0); continue; }
                    // Hardware names may be switched off; moving a widget to another register takes it off this one.
                    var lost = keys0.Except(shown.Select(i => i.Key)).Where(k => !k.EndsWith("model") && !k.EndsWith("name") && !name.Contains("'Register'")).ToList();
                    if (lost.Count > 0) problems.Add($"{name}: values disappear: {string.Join(", ", lost.Take(5))}");
                    foreach (var o in rig.Outside().Except(outside0).Take(2)) problems.Add($"{name}: outside its card: {o}");
                    // Back: the same control set back where the control allows it, otherwise the saved settings.
                    var again = AppearanceControls(rig, page).ElementAtOrDefault(index).Control;
                    bool viaUi = false;
                    if (again is not null && again.GetType() == c.GetType())
                    {
                        switch (again)
                        {
                            case ToggleButton t when old is bool ob: t.IsChecked = ob; viaUi = true; break;
                            case ComboBox cb when old is int oi && oi >= 0: cb.SelectedIndex = oi; viaUi = true; break;
                            case Slider sl when old is double od: sl.Value = od; viaUi = true; break;
                        }
                    }
                    string backJson = rig.Json();
                    if (viaUi && backJson != json0)
                        notes.Add($"{name}: set back in Settings, settings differ ({Diff(json0, backJson)})");
                    if (backJson != json0) rig.Restore(raw0);
                    else rig.Touch();
                    if (viaUi && backJson == json0 && !rig.Shown().SetEquals(baseline))
                        problems.Add($"{name}: set back, the sidebar differs");
                }
            }
        }
        return problems;
    }

    [Theory, MemberData(nameof(Designs))]
    public void Every_appearance_control_changes_and_returns_without_losing_values(WidgetStyle style, string hardware) => UiHost.Run(() =>
    {
        var notes = new List<string>();
        var problems = AppearanceProblems(style, hardware, notes);
        Report("appearance", style, hardware, problems.Concat(notes.Select(n => "NOTE " + n)).ToList());
        Assert.True(problems.Count == 0, string.Join("\n", problems.Take(30)));
        return true;
    }, 1800);

    /// <summary>Pictures for a closer look: LUMEN_PROBE="Style:Property,…" writes the sidebar with that size at 200 %.</summary>
    [Fact]
    public void Writes_size_pictures_on_request() => UiHost.Run(() =>
    {
        if (Environment.GetEnvironmentVariable("LUMEN_PROBE") is not { Length: > 0 } list) return true;
        string dir = Environment.GetEnvironmentVariable("LUMEN_PROBE_DIR") ?? "/tmp/rel/probe";
        Directory.CreateDirectory(dir);
        var lines = new List<string>();
        foreach (var item in list.Split(','))
        {
            var (styleName, prop) = (item.Split(':')[0], item.Split(':')[1]);
            using var rig = new Rig(Enum.Parse<WidgetStyle>(styleName), "demo");
            if (prop != "none") foreach (var w in rig.S.Widgets) typeof(WidgetSlot).GetProperty(prop)!.SetValue(w, 2.0);
            rig.Touch();
            foreach (var o in rig.Outside()) lines.Add($"{styleName} {prop}: {o}");
            rig.Shown();
            using var frame = rig.Host.CaptureRenderedFrame()!;
            frame.Save(Path.Combine(dir, $"{styleName}-{prop}.png"));
        }
        File.WriteAllLines(Path.Combine(dir, "outside.txt"), lines);
        return true;
    }, 900);
}
