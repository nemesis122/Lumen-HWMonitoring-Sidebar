using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed partial class SettingsWindow
{
    private readonly HashSet<WidgetKind> openColourSections = new();
    private Control ColourChoice(string name, Func<string> get, Action<string> set, string inherit, bool compact = false)
    {
        string current = get();
        var sample = new Border { Width = 17, Height = 17, CornerRadius = new CornerRadius(4), Background = Palette.Brush(Color.TryParse(current, out var c) ? c : p.Soft), BorderBrush = Palette.Brush(p.Stroke), BorderThickness = new Thickness(1) };
        var button = new Button { Name = name, Content = compact ? Ui.Row(4, sample, Ui.Text("▾", 10, p.Soft)) : Ui.Row(8, sample, Ui.Text(current.Length > 0 ? current : L.T(inherit), 12, p.Ink)), Padding = new Thickness(compact ? 7 : 10, 6), MinWidth = compact ? 36 : 160 };
        ToolTip.SetTip(button, L.T("Colour") + " · " + (current.Length > 0 ? current : L.T(inherit)));
        var flyout = new Flyout();
        flyout.Content = ColourPalette(get, value => { set(value); flyout.Hide(); changed(); Build(); }, inherit);
        button.Flyout = flyout;
        return button;
    }
    private Control ColourPalette(Func<string> get, Action<string> commit, string inherit)
    {
        var body = Ui.Stack(8);
        var inherited = new Button { Content = L.T(inherit), HorizontalAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(10, 6) };
        inherited.Click += (_, _) => commit(""); body.Children.Add(inherited);
        var swatches = new WrapPanel { Width = 288 };
        foreach (var (hex, title) in new[] { ("#FFFFFF", "White"), ("#D6DAE1", "Light grey"), ("#8A93A3", "Grey"), ("#151B2A", "Black") }.Concat(Palette.Presets))
        {
            var swatch = new Button { Width = 30, Height = 30, Margin = new Thickness(3), Padding = new Thickness(0), Background = Palette.Brush(Color.Parse(hex)), BorderBrush = Palette.Brush(get().Equals(hex, StringComparison.OrdinalIgnoreCase) ? p.Ink : p.Stroke), BorderThickness = new Thickness(get().Equals(hex, StringComparison.OrdinalIgnoreCase) ? 2 : 1), CornerRadius = new CornerRadius(5) };
            ToolTip.SetTip(swatch, L.T(title) + " · " + hex); swatch.Click += (_, _) => commit(hex); swatches.Children.Add(swatch);
        }
        body.Children.Add(swatches);
        var hexBox = new TextBox { Text = get(), Watermark = "#RRGGBB", Width = 174, MaxLength = 9, Name = "colour-hex" };
        var apply = new Button { Content = L.T("Apply"), Padding = new Thickness(10, 6), Name = "colour-apply" };
        var error = Ui.Text("", 11, Color.Parse("#F87171")); error.IsVisible = false;
        apply.Click += (_, _) =>
        {
            string value = ColourRules.Hex(hexBox.Text);
            if (value.Length == 0) { error.Text = L.T("Enter a colour such as #22D3EE."); error.IsVisible = true; return; }
            commit(value);
        };
        body.Children.Add(Ui.Row(8, hexBox, apply)); body.Children.Add(error);
        return new Border { Padding = new Thickness(8), Child = body };
    }
    private Control WarningChoice(string name, Func<bool?> get, Action<bool?> set, string inherit)
    {
        var choice = Choice(new[] { L.T(inherit), L.T("On"), L.T("Off") }, get() == null ? 0 : get() == true ? 1 : 2,
            i => set(i == 0 ? null : i == 1));
        choice.Name = name; return choice;
    }
    private Control WidgetColourSection(WidgetSlot slot)
    {
        var body = Ui.Stack(9);
        var toggle = Toggle(slot.Colours.InheritGlobal, v => slot.Colours.InheritGlobal = v, rebuild: true);
        toggle.Name = "colour-inherit-" + slot.Kind;
        body.Children.Add(Row(L.T("Use global colours"), toggle));
        if (!slot.Colours.InheritGlobal)
        {
            body.Children.Add(Row(L.T("Title"), ColourChoice("colour-title-" + slot.Kind, () => slot.Colours.TitleColor, v => slot.Colours.TitleColor = v, "Like global")));
            body.Children.Add(Row(L.T("Labels"), ColourChoice("colour-label-" + slot.Kind, () => slot.Colours.LabelColor, v => slot.Colours.LabelColor = v, "Like global")));
            body.Children.Add(Row(L.T("Values"), ColourChoice("colour-value-" + slot.Kind, () => slot.Colours.ValueColor, v => slot.Colours.ValueColor = v, "Like global")));
            body.Children.Add(Row(L.T("Graphs and bars"), ColourChoice("colour-graph-" + slot.Kind, () => slot.Colours.GraphColor, v => slot.Colours.GraphColor = v, "Like global")));
            body.Children.Add(Row(L.T("Automatic warning colours"), WarningChoice("colour-warning-" + slot.Kind, () => slot.Colours.AutomaticWarningColors, v => slot.Colours.AutomaticWarningColors = v, "Like global")));
            var reset = new Button { Content = L.T("Restore global colours"), Name = "colour-reset-" + slot.Kind, HorizontalAlignment = HorizontalAlignment.Left };
            reset.Click += (_, _) => { slot.Colours = new(); changed(); Build(); }; body.Children.Add(reset);
        }
        body.Children.Add(Ui.Text(L.T("Sensor colours below override the widget. Empty fields inherit their parent."), 11, p.Soft));
        var section = new Expander { Header = L.T("Colours"), Name = "widget-colours-" + slot.Kind, Content = body, IsExpanded = openColourSections.Contains(slot.Kind), Margin = new Thickness(28, 8, 0, 8), HorizontalAlignment = HorizontalAlignment.Stretch };
        section.PropertyChanged += (_, e) => { if(e.Property == Expander.IsExpandedProperty) { if(section.IsExpanded)openColourSections.Add(slot.Kind);else openColourSections.Remove(slot.Kind); } };
        return section;
    }
    private Control SensorColourRow(WidgetSlot slot, string key, CheckBox check)
    {
        if (AioReadings.Groups.Any(g => g.Key == key) || SensorDefaults.Groups.ContainsKey(key) || key.EndsWith(".extra") || key == "aio.graphs") return check;
        SensorColours Current() => slot.SensorColours.TryGetValue(key, out var value) ? value : new();
        SensorColours Edit() { if (!slot.SensorColours.TryGetValue(key, out var value)) slot.SensorColours[key] = value = new(); return value; }
        var current = Current();
        string effective = ColourRules.Resolve(s,slot,key,ColourRole.Value).Hex;
        var sample = new Border { Width = 15, Height = 15, CornerRadius = new CornerRadius(3), Background = Palette.Brush(Color.TryParse(effective, out var c) ? c : p.Soft) };
        var button = new Button { Name = "colour-sensor-" + slot.Kind + "-" + key.Replace('.', '-').Replace(':', '-'), Content = Ui.Row(4, sample, Ui.Text("▾", 10, p.Soft)), Padding = new Thickness(7, 4), MinWidth = 36, VerticalAlignment = VerticalAlignment.Center, BorderBrush = Palette.Brush(current.Empty ? p.Stroke : p.Cool), BorderThickness = new Thickness(1) };
        ToolTip.SetTip(button, L.T("Sensor colour") + " · " + (current.ValueColor.Length > 0 ? current.ValueColor : L.T("Like widget")));
        var flyout = new Flyout();
        void Commit(Action action) { action(); if (Current().Empty) slot.SensorColours.Remove(key); flyout.Hide(); changed(); Build(); }
        var values = ColourPalette(() => Current().ValueColor, v => Commit(() => Edit().ValueColor = v), "Like widget");
        var more = Ui.Stack(8,
            Ui.Text(L.T("Label colour"), 12, p.Ink), ColourPalette(() => Current().LabelColor, v => Commit(() => Edit().LabelColor = v), "Like widget"),
            Ui.Text(L.T("Graph colour"), 12, p.Ink), ColourPalette(() => Current().GraphColor, v => Commit(() => Edit().GraphColor = v), "Like widget"),
            Row(L.T("Automatic warning colours"), WarningChoice("sensor-warning-colour", () => Current().AutomaticWarningColors, v => Commit(() => Edit().AutomaticWarningColors = v), "Like widget")));
        var reset = new Button { Content = L.T("Reset sensor colours"), Name = "colour-sensor-reset", HorizontalAlignment = HorizontalAlignment.Stretch };
        reset.Click += (_, _) => Commit(() => slot.SensorColours.Remove(key));
        flyout.Content = new ScrollViewer { MaxHeight = 480, Content = Ui.Stack(8, Ui.Text(check.Content?.ToString() ?? key, 13, p.Ink), values, new Expander { Header = L.T("Labels, graphs and warnings"), Content = more }, reset) };
        button.Flyout = flyout;
        var row = Ui.Columns("*,Auto", check, button); row.ColumnSpacing = 8; row.ClipToBounds = true;
        return row;
    }
}
