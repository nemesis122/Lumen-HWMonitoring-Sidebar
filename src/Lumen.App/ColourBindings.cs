using System.Reflection;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

/// <summary>A data graphic, including renderers whose colours live in a theme rather than Color fields.</summary>
public interface IColourGraph { }

public static partial class TextColours
{
    private sealed class Owner { public WidgetSlot? Slot; public bool Dark = true; public Snapshot? Snapshot; public int Index; public bool Overridden; }
    private static readonly ConditionalWeakTable<Control, Owner> owners = new();
    public static readonly AttachedProperty<string> SensorProperty = AvaloniaProperty.RegisterAttached<Control, string>("ColourSensor", typeof(TextColours), "");
    public static readonly AttachedProperty<string> SecondSensorProperty = AvaloniaProperty.RegisterAttached<Control, string>("ColourSecondSensor", typeof(TextColours), "");
    public static readonly AttachedProperty<bool> GraphSurfaceProperty = AvaloniaProperty.RegisterAttached<Control, bool>("ColourGraphSurface", typeof(TextColours), false);
    /// <summary>A decorative outline in the design's accent (voltage boxes, drive letters): it takes the graph colour.</summary>
    public static readonly AttachedProperty<bool> FrameProperty = AvaloniaProperty.RegisterAttached<Control, bool>("ColourFrame", typeof(TextColours), false);
    public static void BindHeat(Border cell,string key) { Bind(cell,key);cell.SetValue(GraphSurfaceProperty,true); }
    public static T Frame<T>(T border, string key) where T : Border { Bind(border, key); border.SetValue(FrameProperty, true); return border; }

    /// <summary>
    /// Readings of the drive, network and voltage parts of an AIO card are drawn in the design's accent, but they are
    /// values like every other: the value colour chosen in Appearance replaces that accent (user request 4.5.5).
    /// </summary>
    internal static bool Follows(TextBlock text, Control root)
    {
        if (root is not AioTile) return false;
        for (Control? node = text; node != null && node != root; node = node.Parent as Control)
        {
            string key = DirectKey(node);
            if (key.Length == 0) continue;
            return key.StartsWith("aio.drives.") || key.StartsWith("aio.system.disk.") || key.StartsWith("aio.network.") || key.StartsWith("aio.rails.");
        }
        return false;
    }

    private sealed class FrameState { public IBrush? Original, Applied; }
    private static readonly ConditionalWeakTable<Border, FrameState> frames = new();
    private static void ApplyFrame(Border frame, Color? colour)
    {
        if (!colour.HasValue && !frames.TryGetValue(frame, out _)) return;
        var state = frames.GetOrCreateValue(frame);
        if (!ReferenceEquals(frame.BorderBrush, state.Applied)) state.Original = frame.BorderBrush;
        state.Applied = colour is { } tint
            ? Palette.Brush(Color.FromArgb(state.Original is ISolidColorBrush b ? b.Color.A : (byte)255, tint.R, tint.G, tint.B))
            : state.Original;
        frame.BorderBrush = state.Applied;
    }
    public static void Own(Control root, WidgetSlot? slot, bool? dark = null, int? index = null)
    {
        var owner = owners.GetOrCreateValue(root); owner.Slot = slot; if (dark.HasValue) owner.Dark = dark.Value;
        if(index.HasValue) owner.Index=index.Value;
    }
    public static void Snapshot(Control root, Snapshot snapshot, int? index = null)
    { var owner = owners.GetOrCreateValue(root); owner.Snapshot = snapshot; if(index.HasValue)owner.Index = index.Value; }
    public static T Bind<T>(T control, string key) where T : Control { control.SetValue(SensorProperty, key); return control; }
    public static T Bind<T>(T control, string key, string? second) where T : Control
    { Bind(control,key); control.SetValue(SecondSensorProperty,second ?? ""); return control; }

    private static string DirectKey(Control c)
    {
        string key = c.GetValue(SensorProperty);
        if (key.Length == 0) key = SensorDetails.KeyOf(c);
        if (key.Length == 0) key = WidgetPresentation.SensorKey(c);
        return key.Split('|', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
    }
    private static string Key(Control c, Control root, bool label)
    {
        for (Control? node = c; node != null; node = node.Parent as Control)
        {
            string key = DirectKey(node); if (key.Length > 0) return key;
            if (node == root) break;
        }
        if (!label) return "";
        // Labels share their row with a bound number; never guess a sensor from its displayed value.
        Control child = c;
        for (int depth = 0; depth < 4 && child.Parent is Control parent && parent != root; depth++, child = parent)
        {
            var siblings = parent is Grid grid ? grid.Children.Where(n => Grid.GetRow(n) == Grid.GetRow(child))
                : parent.GetLogicalChildren().OfType<Control>();
            var keys = siblings.SelectMany(n => n.GetLogicalDescendants().OfType<Control>().Prepend(n)).Select(DirectKey).Where(k => k.Length > 0).Distinct().Take(2).ToArray();
            if (keys.Length == 1) return keys[0];
            if (keys.Length > 1) break;
        }
        return "";
    }
    private static WidgetKind? Component(Control c, Control root, string key)
    {
        if (ColourRules.Kind(key) is { } kind) return kind;
        for (Control? node = c; node != null && node != root; node = node.Parent as Control)
        {
            if (node.Tag is string tag && (tag.StartsWith("case-widget:") || tag.StartsWith("dashboard-part:")))
                return tag[(tag.IndexOf(':') + 1)..] switch { "cpu" => WidgetKind.Cpu, "gpu" => WidgetKind.Gpu, "ram" => WidgetKind.Memory, "ssd" => WidgetKind.Drives, "fan" or "board" => WidgetKind.Cooling, "net" => WidgetKind.Network, "power" => WidgetKind.Power, _ => null };
            var kinds = node.GetLogicalDescendants().OfType<Control>().Select(DirectKey).Where(k => k.Length > 0).Select(ColourRules.Kind).Where(k => k.HasValue).Distinct().Take(2).ToArray();
            if (kinds.Length == 1) return kinds[0];
            if (kinds.Length > 1) break;
        }
        return null;
    }
    private static bool TemperatureKey(string key) => key.EndsWith(".temp") || key.EndsWith(".hotspot") || key.EndsWith(".junction") || key.StartsWith("temp:") || key.StartsWith("aio.board.") || key.StartsWith("aio.system.disk.") || key.StartsWith("drive:") && key.EndsWith(":temp");
    private static double? Temperature(Control root, Owner owner, string key, Settings settings)
    {
        if (!TemperatureKey(key)) return null;
        if (root is AioTile aio) return aio.ColourTemperature(key);
        var snapshot = owner.Snapshot; if (snapshot == null) return null;
        if (key == "cpu.temp") return snapshot.Cpu?.Temperature;
        if (key.StartsWith("gpu."))
        {
            var gpu = GpuGroup.Visible(snapshot, settings).ElementAtOrDefault(owner.Index);
            return key switch { "gpu.temp" => gpu?.Temperature, "gpu.hotspot" => gpu?.HotSpot, "gpu.junction" => gpu?.MemoryJunction, _ => null };
        }
        if (key.StartsWith("drive:") && key.EndsWith(":temp")) return snapshot.Disks.FirstOrDefault(d => key == "drive:" + d.Id + ":temp")?.Temperature;
        if (key == "drives.temp") return snapshot.Disks.ElementAtOrDefault(owner.Index)?.Temperature;
        return null;
    }
    private static Color Warning(double temperature, double threshold) => Color.Parse(temperature >= threshold + 10 ? "#EF4444" : "#F59E0B");
    private static bool Warm(Color c) => c.R > 170 && c.R > c.G * 1.15 && c.B < 145;
    private sealed class OverrideText { public Color Original, Applied; public Color? Normal; }
    private static readonly ConditionalWeakTable<TextBlock, OverrideText> overrideTexts = new();
    private static readonly ConditionalWeakTable<TextBox,OverrideText> overrideInputs = new();

    private static void ApplyNoteInputs(Control root,Settings s,Owner owner)
    {
        // Notes are editable text, not a TextBlock; include their normal and focused foregrounds.
        if(owner.Slot?.Kind==WidgetKind.Notes)
            foreach(var input in root.GetLogicalDescendants().OfType<TextBox>())
            {
                if(input.Foreground is not ISolidColorBrush brush)continue;
                if(!overrideInputs.TryGetValue(input,out var state))overrideInputs.Add(input,state=new(){Original=brush.Color,Applied=brush.Color});
                else if(brush.Color!=state.Applied)state.Original=brush.Color;
                var value=Parse(ColourRules.Resolve(s,owner.Slot,"",ColourRole.Value).Hex)??state.Original;
                state.Applied=Color.FromArgb(state.Original.A,value.R,value.G,value.B);
                input.Foreground=Palette.Brush(state.Applied);input.Resources["TextControlForegroundFocused"]=input.Foreground;
            }
    }
    private static bool ApplyOverrides(Control root, Settings s)
    {
        if (!ColourRules.Active(s) && !owners.TryGetValue(root, out _)) return false;
        var owner = owners.GetOrCreateValue(root);
        if (!ColourRules.Active(s) && (owner.Slot == null || owner.Slot.Colours.InheritGlobal && owner.Slot.SensorColours.Count == 0))
        {
            if(owner.Overridden){RestoreOverrides(root);owner.Overridden=false;}
            ApplyNoteInputs(root,s,owner);return false;
        }
        owner.Overridden=true;ApplyNoteInputs(root,s,owner);
        foreach (var text in root.GetLogicalDescendants().OfType<TextBlock>().Prepend(root as TextBlock).OfType<TextBlock>())
        {
            if (text.Foreground is not ISolidColorBrush brush) continue;
            var current = brush.Color;
            if (!overrideTexts.TryGetValue(text, out var state)) overrideTexts.Add(text, state = new() { Original = current, Applied = current });
            else if (current != state.Applied) state.Original = current;
            var role = TextFonts.RoleOf(text) switch { TextRole.Title => ColourRole.Title, TextRole.Value => ColourRole.Value, TextRole.Graph => ColourRole.Graph, _ => ColourRole.Label };
            string key = Key(text, root, role == ColourRole.Label);
            var effective = ColourRules.Resolve(s, owner.Slot ?? s.ActiveWidget, key, role, Component(text, root, key));
            double? temp = Temperature(root, owner, key, s);
            bool warning = role == ColourRole.Value && TemperatureKey(key) && (temp >= s.TemperatureWarning || temp == null && Warm(state.Original));
            if (!warning) state.Normal = state.Original;
            Color target = state.Original;
            var chosen = Parse(effective.Hex);
            if (chosen.HasValue && (effective.Explicit || role is ColourRole.Title or ColourRole.Graph || Neutral(state.Original) || !effective.AutomaticWarnings || role == ColourRole.Value && Follows(text, root))) target = chosen.Value;
            if (warning && effective.AutomaticWarnings)
                target = Warm(state.Original) ? state.Original : Warning(temp ?? s.TemperatureWarning, s.TemperatureWarning);
            else if (warning && !effective.AutomaticWarnings)
                target = chosen ?? state.Normal ?? (owner.Dark ? Color.Parse("#EEF4FF") : Color.Parse("#151B2A"));
            target = Color.FromArgb(state.Original.A, target.R, target.G, target.B);
            state.Applied = target;
            if (target != current) text.Foreground = new SolidColorBrush(target, brush.Opacity);
        }
        foreach (var frame in root.GetLogicalDescendants().OfType<Border>().Where(b => b.GetValue(FrameProperty)))
        {
            string key = DirectKey(frame);
            ApplyFrame(frame, Parse(ColourRules.Resolve(s, owner.Slot ?? s.ActiveWidget, key, ColourRole.Graph, Component(frame, root, key)).Hex));
        }
        foreach (var control in root.GetLogicalDescendants().OfType<Control>().Prepend(root))
        {
            if (!IsGraph(control) && control is not AioCoreBars) continue;
            string key = Key(control, root, true); var component = Component(control, root, key);
            var effective = ColourRules.Resolve(s, owner.Slot ?? s.ActiveWidget, key, ColourRole.Graph, component);
            Color? graph = Parse(effective.Hex);
            var temp = Temperature(root, owner, key, s);
            if (graph.HasValue && effective.AutomaticWarnings && temp >= s.TemperatureWarning) graph = Warning(temp.Value, s.TemperatureWarning);
            if (control is AioCoreBars bars)
            {
                bars.ValueInk = Parse(ColourRules.Resolve(s,owner.Slot ?? s.ActiveWidget,"aio.cpu.coreloads",ColourRole.Value,WidgetKind.Cpu).Hex);
                bars.LabelInk = Parse(ColourRules.Resolve(s,owner.Slot ?? s.ActiveWidget,"aio.cpu.coreloads",ColourRole.Label,WidgetKind.Cpu).Hex);
            }
            if (control is MainboardCoreGrid grid)
            {
                grid.ValueInk = Parse(ColourRules.Resolve(s,owner.Slot ?? s.ActiveWidget,"aio.cpu.coreloads",ColourRole.Value,WidgetKind.Cpu).Hex);
                grid.LabelInk = Parse(ColourRules.Resolve(s,owner.Slot ?? s.ActiveWidget,"aio.cpu.coreloads",ColourRole.Label,WidgetKind.Cpu).Hex);
            }
            string secondKey = control.GetValue(SecondSensorProperty);
            Color? second = secondKey.Length == 0 ? graph : Parse(ColourRules.Resolve(s, owner.Slot ?? s.ActiveWidget, secondKey, ColourRole.Graph, Component(control,root,secondKey)).Hex);
            if(secondKey.Length>0 && second.HasValue)
            {
                var rule=ColourRules.Resolve(s,owner.Slot??s.ActiveWidget,secondKey,ColourRole.Graph,Component(control,root,secondKey));
                var otherTemperature=Temperature(root,owner,secondKey,s);
                if(rule.AutomaticWarnings && otherTemperature>=s.TemperatureWarning)second=Warning(otherTemperature.Value,s.TemperatureWarning);
            }
            ApplyGraph(control, graph, second);
        }
        return true;
    }

    private sealed class GraphState { public readonly Dictionary<FieldInfo,(object? Original,object? Applied)> Fields = new(); }
    private static readonly ConditionalWeakTable<Control, GraphState> graphStates = new();
    private static readonly Dictionary<Type, FieldInfo[]> graphFields = new();
    private static bool IsGraph(Control control)
    {
        if(control is IColourGraph)return true;
        if(control.GetValue(GraphSurfaceProperty))return true;
        if (control is Panel or Decorator or TextBlock or ContentControl || control.GetType().Namespace != typeof(TextColours).Namespace) return false;
        string name=control.GetType().Name;
        return new[]{"Chart","Meter","Ring","Bar","Plot","Sparkline","Skyline","History","Gauge","FillBack","CoreGrid"}.Any(name.Contains);
    }
    private sealed class SurfaceState { public IBrush? Original,Applied; }
    private static readonly ConditionalWeakTable<Border,SurfaceState> surfaces = new();
    private static void ApplyGraph(Control control, Color? colour, Color? second = null)
    {
        if(control is Border cell && cell.GetValue(GraphSurfaceProperty))
        {
            if(!colour.HasValue && !surfaces.TryGetValue(cell,out _))return;
            var surface=surfaces.GetOrCreateValue(cell);
            if(!ReferenceEquals(cell.Background,surface.Applied))surface.Original=cell.Background;
            surface.Applied=colour is {} tint ? Palette.Brush(Color.FromArgb(surface.Original is ISolidColorBrush b?b.Color.A:(byte)255,tint.R,tint.G,tint.B)):surface.Original;
            cell.Background=surface.Applied;return;
        }
        if (!IsGraph(control)) return;
        if (!colour.HasValue && !second.HasValue && !graphStates.TryGetValue(control,out _)) return;
        var state=graphStates.GetOrCreateValue(control); var type=control.GetType();
        if(!graphFields.TryGetValue(type,out var fields))
            graphFields[type]=fields=type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)
                .Where(f=>(f.FieldType==typeof(Color)||f.FieldType==typeof(Color?)||f.FieldType==typeof(Color[])||f.FieldType==typeof(PanelTheme)||f.FieldType==typeof(BlackTheme))
                    && !new[]{"ink","muted","track","back","grid","label","empty","warning"}.Any(n=>(f.Name.StartsWith('<')?f.Name[1..f.Name.IndexOf('>')]:f.Name).Contains(n,StringComparison.OrdinalIgnoreCase))).ToArray();
        foreach(var field in fields)
        {
            string fieldName = field.Name.StartsWith('<') ? field.Name[1..field.Name.IndexOf('>')] : field.Name;
            Color? tint = fieldName.Equals("OtherColor",StringComparison.OrdinalIgnoreCase) || fieldName.Contains("upColor",StringComparison.OrdinalIgnoreCase) || fieldName is "UpTint" or "second" ? second : colour;
            object? current=field.GetValue(control);
            var previous=state.Fields.TryGetValue(field,out var old)?old:(current,current);
            if(!Equals(current,previous.Item2))previous=(current,current);
            object? target=tint.HasValue ? previous.Item1 switch {
                Color c=>Color.FromArgb(c.A,tint.Value.R,tint.Value.G,tint.Value.B),
                Color[] cs=>cs.Select(c=>Color.FromArgb(c.A,tint.Value.R,tint.Value.G,tint.Value.B)).ToArray(),
                PanelTheme theme=>theme with {Accent=tint.Value,Hot=tint.Value},
                BlackTheme theme=>theme with {A=tint.Value,B=tint.Value},
                _=>tint.Value } : previous.Item1;
            if(!Equals(current,target)){field.SetValue(control,target);control.InvalidateVisual();}
            state.Fields[field]=(previous.Item1,target);
        }
    }
    private static void RestoreOverrides(Control root)
    {
        foreach(var c in root.GetLogicalDescendants().OfType<Control>().Prepend(root))
        {
            if(c is AioCoreBars bars){bars.ValueInk=null;bars.LabelInk=null;bars.InvalidateVisual();}
            if(c is MainboardCoreGrid grid){grid.ValueInk=null;grid.LabelInk=null;grid.InvalidateVisual();}
            if(c is TextBox input && overrideInputs.TryGetValue(input,out var textInput))
            {input.Foreground=Palette.Brush(textInput.Original);input.Resources["TextControlForegroundFocused"]=input.Foreground;overrideInputs.Remove(input);}
            if(c is TextBlock t && overrideTexts.TryGetValue(t,out var state))
            {
                if(t.Foreground is ISolidColorBrush b && b.Color==state.Applied)t.Foreground=Palette.Brush(state.Original);
                overrideTexts.Remove(t);
            }
            if(c is Border frame && frame.GetValue(FrameProperty))ApplyFrame(frame,null);
            ApplyGraph(c,null);
        }
    }
}
