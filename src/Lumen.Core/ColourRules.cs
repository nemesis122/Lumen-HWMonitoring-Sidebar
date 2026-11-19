namespace Lumen.Core;

public enum ColourRole { Title, Label, Value, Graph }

/// <summary>An empty colour inherits its parent. Null warning behaviour also inherits.</summary>
public class SensorColours
{
    public string TitleColor { get; set; } = "";
    public string LabelColor { get; set; } = "";
    public string ValueColor { get; set; } = "";
    public string GraphColor { get; set; } = "";
    public bool? AutomaticWarningColors { get; set; }
    public string For(ColourRole role) => role switch { ColourRole.Title => TitleColor, ColourRole.Label => LabelColor, ColourRole.Graph => GraphColor, _ => ValueColor };
    [System.Text.Json.Serialization.JsonIgnore] public bool Empty => TitleColor.Length == 0 && LabelColor.Length == 0 && ValueColor.Length == 0 && GraphColor.Length == 0 && AutomaticWarningColors == null;
    public void Normalize()
    {
        TitleColor = ColourRules.Hex(TitleColor); LabelColor = ColourRules.Hex(LabelColor);
        ValueColor = ColourRules.Hex(ValueColor); GraphColor = ColourRules.Hex(GraphColor);
    }
    public SensorColours Copy() => new() { TitleColor=TitleColor, LabelColor=LabelColor, ValueColor=ValueColor, GraphColor=GraphColor, AutomaticWarningColors=AutomaticWarningColors };
}

public sealed class WidgetColours : SensorColours
{
    public bool InheritGlobal { get; set; } = true;
    public new WidgetColours Copy() => new() { InheritGlobal=InheritGlobal, TitleColor=TitleColor, LabelColor=LabelColor, ValueColor=ValueColor, GraphColor=GraphColor, AutomaticWarningColors=AutomaticWarningColors };
}

public sealed class WidgetColourTheme
{
    public WidgetKind Kind { get; set; }
    public int Index { get; set; }
    public WidgetColours Colours { get; set; } = new();
    public Dictionary<string, SensorColours> Sensors { get; set; } = new();
}

public readonly record struct EffectiveColour(string Hex, bool Explicit, bool AutomaticWarnings);

public static class ColourRules
{
    private static IEnumerable<string> Candidates(string key,ColourRole role)
    {
        if(role==ColourRole.Graph)
        {
            var native=Keys(key).LastOrDefault()??key;
            string group=native is "cpu.temp" or "cpu.load" ? "cpu.chart" : native is "gpu.temp" or "gpu.load" ? "gpu.chart"
                : native=="mem.load" ? "mem.bar" : native is "net.down" or "net.up" ? "net.chart"
                : native=="drives.used" ? "drives.bar" : "";
            if(group.Length>0)yield return group;
            foreach(var candidate in Keys(key).Reverse())yield return candidate+".history";
        }
        foreach(var candidate in Keys(key).Reverse())yield return candidate;
    }
    public static string Hex(string? text) => text?.Trim() is { } value && Settings.IsHexColor(value) ? value.ToUpperInvariant() : "";
    public static bool Active(Settings s) => !s.AutomaticWarningColors || s.TitleColor.Length > 0 || s.GraphColor.Length > 0
        || s.Widgets.Any(w => !w.Colours.InheritGlobal && !w.Colours.Empty || w.SensorColours.Values.Any(c => !c.Empty));

    public static WidgetKind? Kind(string key) => key.StartsWith("aio.performance.") ? WidgetKind.Fps
        : key.StartsWith("aio.") ? AioDesigns.SwitchFor(key)
        : key.StartsWith("cpu.") ? WidgetKind.Cpu : key.StartsWith("gpu.") ? WidgetKind.Gpu
        : key.StartsWith("mem.") ? WidgetKind.Memory : key.StartsWith("net.") ? WidgetKind.Network
        : key.StartsWith("drives.") || key.StartsWith("drive:") ? WidgetKind.Drives
        : key.StartsWith("cooling.") || key.StartsWith("fan:") || key.StartsWith("temp:") ? WidgetKind.Cooling
        : key.StartsWith("power.") ? WidgetKind.Power : null;

    public static IEnumerable<string> Keys(string key)
    {
        yield return key;
        if (key == "aio.cpu.coreloads") yield return "cpu.cores";
        if (key.StartsWith("aio.cpu.")) yield return key[4..];
        else if (key.StartsWith("aio.gpu.")) yield return "gpu." + key[(key.LastIndexOf('.') + 1)..];
        else if (key.StartsWith("aio.network.")) yield return "net." + key[12..];
        else if (key == "aio.performance.fps") yield return "fps.value";
        else if (key == "aio.system.ram") yield return "mem.load";
        else if (key.StartsWith("aio.system.ram.")) yield return "mem." + (key[15..] == "free" ? "free" : key[15..]);
        else if (key.StartsWith("aio.system.disk.")) { yield return "drive:" + key[16..] + ":temp"; yield return "drives.temp"; }
        else if (key.StartsWith("aio.drives."))
        {
            int split = key.LastIndexOf('.'); yield return "drive:" + key[11..split] + ":" + key[(split + 1)..]; yield return "drives." + key[(split + 1)..];
        }
        else if (key.StartsWith("drive:")) yield return "drives." + key[(key.LastIndexOf(':') + 1)..];
        else if(key.StartsWith("volume:")&&key.LastIndexOf(':')>6)
        { yield return key[..key.LastIndexOf(':')];yield return "volumes."+key[(key.LastIndexOf(':')+1)..]; }
        else if (key.StartsWith("aio.cooling.")) yield return "fan:" + key[12..];
    }

    public static EffectiveColour Resolve(Settings s, WidgetSlot? root, string key, ColourRole role, WidgetKind? component = null)
    {
        var kind = component ?? Kind(key);
        var owner = root?.Kind == WidgetKind.Overview && kind.HasValue ? s.Widgets.FirstOrDefault(w => w.Kind == kind.Value) ?? root : root;
        string global = role switch { ColourRole.Title => s.TitleColor, ColourRole.Label => s.LabelColor, ColourRole.Graph => s.GraphColor, _ => s.ValueColor };
        string colour = global; bool explicitColour = false; bool warnings = s.AutomaticWarningColors;
        foreach (var widget in new[] { root, owner }.Where(w => w != null).Distinct())
        {
            if (widget!.Colours.InheritGlobal) continue;
            if (widget.Colours.For(role).Length > 0) { colour = widget.Colours.For(role); explicitColour = true; }
            warnings = widget.Colours.AutomaticWarningColors ?? warnings;
        }
        // AIO exact identities take precedence over the equivalent standalone sensor setting.
        foreach (var widget in new[] { owner, root }.Where(w => w != null).Distinct())
            foreach (string candidate in Candidates(key,role))
                if (widget!.SensorColours.TryGetValue(candidate, out var sensor))
                {
                    if (sensor.For(role).Length > 0) { colour = sensor.For(role); explicitColour = true; }
                    warnings = sensor.AutomaticWarningColors ?? warnings;
                }
        return new(colour, explicitColour, warnings);
    }

    public static void Normalize(Settings s)
    {
        s.TitleColor = Hex(s.TitleColor); s.GraphColor = Hex(s.GraphColor);
        foreach (var widget in s.Widgets)
        {
            widget.Colours ??= new(); widget.Colours.Normalize(); widget.SensorColours ??= new();
            widget.SensorColours = widget.SensorColours.Where(p => !string.IsNullOrWhiteSpace(p.Key) && p.Value != null)
                .ToDictionary(p => p.Key, p => { p.Value.Normalize(); return p.Value; });
            foreach (var key in widget.SensorColours.Where(p => p.Value.Empty).Select(p => p.Key).ToArray()) widget.SensorColours.Remove(key);
        }
    }
}
