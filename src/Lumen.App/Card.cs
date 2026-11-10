using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public abstract class Card : Border, IWidget, INamed
{
	protected readonly Palette P;

	protected readonly Metrics M;

	protected readonly Settings S;

	protected readonly StackPanel Body;

	private readonly TextBlock aside;

	private readonly TextBlock subtitle;

	private readonly TextBlock title;

	private WidgetSlot? slot;

	private int slotIndex;

	private string builtInTitle = "";

	private string lastSubtitle = "";

	protected string Title
	{
		set
		{
			builtInTitle = value;
			title.Text = Naming.Title(slot, slotIndex, value);
		}
	}

	protected string Aside
	{
		set
		{
			aside.Text = value;
            if (S.ActiveWidget is { } widget)
                SensorDetails.Value(aside, widget.Kind switch { WidgetKind.Cpu => "cpu.clock", WidgetKind.Gpu => "gpu.clock", WidgetKind.Network => "net.link", WidgetKind.Drives => "drives.count", _ => "" });
            if (S.ActiveWidget?.Kind is WidgetKind.Cpu or WidgetKind.Gpu)
                aside.IsVisible = S.Shows(S.ActiveWidget.Kind == WidgetKind.Cpu ? "cpu.clock" : "gpu.clock");
            if (S.ActiveWidget?.Kind is WidgetKind.Clock or WidgetKind.Calendar or WidgetKind.Drives)
                aside.IsVisible = S.Shows(S.ActiveWidget.Kind switch { WidgetKind.Clock => "clock.week", WidgetKind.Calendar => "calendar.week", _ => "drives.count" });
            if (S.ActiveWidget?.Kind == WidgetKind.Weather) aside.IsVisible = S.Shows("weather.place");
            if (S.ActiveWidget?.Kind == WidgetKind.Fps) aside.IsVisible = S.Shows("fps.app");
            if (S.ActiveWidget?.Kind == WidgetKind.Media) aside.IsVisible = S.Shows("media.source");
            if (S.ActiveWidget?.Kind == WidgetKind.DataUsage) aside.IsVisible = S.Shows("data.period");
		}
	}

	protected string Subtitle
	{
		set
		{
			lastSubtitle = value;
			subtitle.Text = Naming.Hardware(slot, slotIndex, Names.Clean(value));
			subtitle.IsVisible = Naming.ShowHardware(slot, S) && !string.IsNullOrWhiteSpace(subtitle.Text) && (S.ActiveWidget?.Kind != WidgetKind.System || S.Shows("system.os"));
		}
	}

	protected string TempUnit => Format.TemperatureUnit(S.Fahrenheit);

	protected Card(Palette p, Metrics m, Settings s, string glyph, string titleText)
	{
		P = p;
		M = m;
		S = s;
		base.Background = Palette.Brush(p.Card);
		base.BorderBrush = Palette.Brush(p.Stroke);
		base.BorderThickness = Ui.CardEdge;
		base.CornerRadius = Ui.CardRadius(m);
		base.Padding = new Thickness(m.Pad, m.Pad - 2.0, m.Pad, m.Pad);
		title = Ui.Text(titleText, 12.5, p.Ink, FontWeight.DemiBold);
		builtInTitle = titleText;
		aside = Ui.Text("", 12.0, p.Soft, FontWeight.Medium, numbers: true);
		aside.HorizontalAlignment = HorizontalAlignment.Right;
		Grid grid = Ui.Columns("Auto,*,Auto", Ui.Badge(glyph, p, p.Cool), title, aside);
		title.Margin = new Thickness(Ui.Modern ? 10 : 8, 0.0, 8.0, 0.0);
		subtitle = Ui.Text("", 11.5, p.Soft);
        HardwareWidgetTitle.Register(this, title, subtitle);
		subtitle.IsVisible = false;
		Body = Ui.Stack(m.Gap, Ui.Stack(1.0, grid, subtitle));
		Body.Tag = "elastic";
		base.Child = Body;
	}

	public void Own(WidgetSlot slot, int index)
	{
		this.slot = slot;
		slotIndex = index;
		Title = builtInTitle;
		Subtitle = lastSubtitle;
	}

	protected bool Shows(string key)
	{
		return S.Shows(key);
	}

	protected string Temp(double? c)
	{
		return Format.Temperature(c, S.Fahrenheit);
	}

	protected string TempWithUnit(double? c)
	{
		if (c.HasValue)
		{
			return Temp(c) + " " + TempUnit;
		}
		return "—";
	}

	protected Color HeatColor(double? c)
	{
		if (c.HasValue)
		{
			double valueOrDefault = c.GetValueOrDefault();
			if (double.IsFinite(valueOrDefault))
			{
				if (!(valueOrDefault >= S.TemperatureWarning))
				{
					return P.At(Format.Heat(valueOrDefault));
				}
				return P.Ramp[^1];
			}
		}
		return P.Faint;
	}

    public void Update(Snapshot s, History h)
    {
        var filtered = WidgetSensors.FilterSnapshot(s, S);
        UpdateReadings(filtered, h);
        WidgetPresentation.Apply(this, S);
        if (S.ActiveWidget is { } active)
            SensorDetails.Apply(this, active.Kind, slotIndex, s, h, S, P);
        WidgetPresentation.Apply(this, S);
    }

    protected abstract void UpdateReadings(Snapshot s, History h);

	protected static TextBlock Label(string text, Palette p)
	{
		TextBlock textBlock = Ui.Text(text.ToUpper(L.Culture), 10.0, p.Faint, FontWeight.DemiBold);
		textBlock.LetterSpacing = 0.6;
		return textBlock;
	}

	protected (Hero hero, TextBlock side, AreaChart? chart) HeroBlock(string sideLabel, string chartKey)
	{
		Hero hero = new Hero(M.Hero, P);
		TextBlock textBlock = Label(sideLabel, P);
		textBlock.HorizontalAlignment = HorizontalAlignment.Right;
		TextBlock textBlock2 = Ui.Text("—", Math.Round(M.Hero * 0.5), P.Ink, FontWeight.Medium, numbers: true);
		textBlock2.HorizontalAlignment = HorizontalAlignment.Right;
		StackPanel stackPanel = Ui.Stack(0.0, textBlock, textBlock2);
		stackPanel.VerticalAlignment = VerticalAlignment.Bottom;
		stackPanel.Margin = new Thickness(0.0, 0.0, 0.0, M.Hero * 0.12);
		Body.Children.Add(Ui.Columns("*,Auto", hero, stackPanel));
		AreaChart areaChart = null;
		if (Shows(chartKey))
		{
			areaChart = new AreaChart
			{
				Height = M.Chart,
				Max = 100.0,
				Margin = new Thickness(0.0, -2.0, 0.0, 0.0)
			};
			Body.Children.Add(areaChart);
		}
		return (hero: hero, side: textBlock2, chart: areaChart);
	}

    protected (Hero hero, Hero side, AreaChart? chart) TwinHeroBlock(string sideLabel, string chartKey)
    {
        var hero = new Hero(M.Hero, P);
        var side = new Hero(M.Hero, P) { HorizontalAlignment = HorizontalAlignment.Right };
        var label = Label(sideLabel, P);
        label.HorizontalAlignment = HorizontalAlignment.Right;
        var sideGroup = Ui.Stack(0, label, side);
        sideGroup.VerticalAlignment = VerticalAlignment.Bottom;
        Body.Children.Add(Ui.Columns("*,Auto", hero, sideGroup));
        AreaChart? chart = null;
        if (Shows(chartKey))
        {
            chart = new AreaChart { Height = M.Chart, Max = 100, Margin = new Thickness(0, -2, 0, 0) };
            Body.Children.Add(chart);
        }
        return (hero, side, chart);
    }

	protected Dictionary<string, Stat> Stats(params (string key, string label)[] items)
	{
		Dictionary<string, Stat> dictionary = items.Where(((string key, string label) i) => Shows(i.key)).ToList().ToDictionary(((string key, string label) i) => i.key, ((string key, string label) i) => SensorDetails.Value(new Stat(i.label, P), i.key));
		if (dictionary.Count > 0)
		{
			Body.Children.Add(StatGrid.Of(dictionary.Values.ToArray()));
		}
		return dictionary;
	}

	protected static void Put(Dictionary<string, Stat> stats, string key, string value, Color? color = null)
	{
		if (stats.TryGetValue(key, out Stat value2))
		{
			value2.Value = value;
			if (color.HasValue)
			{
				Color valueOrDefault = color.GetValueOrDefault();
				value2.Color = valueOrDefault;
			}
		}
	}
}
