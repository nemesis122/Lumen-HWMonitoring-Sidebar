using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed class CoolingCard : Card
{
	private readonly Hero hero;

	private readonly TextBlock heroName;

	private readonly TextBlock active;

	private readonly StackPanel? fans;

	private readonly Grid? temps;

	private readonly Dictionary<string, (TextBlock name, TextBlock rpm, Meter bar)> rows = new Dictionary<string, (TextBlock, TextBlock, Meter)>();

	public CoolingCard(Palette p, Metrics m, Settings s)
		: base(p, m, s, "M8,8 m-1.3,0 a1.3,1.3 0 1 0 2.6,0 a1.3,1.3 0 1 0 -2.6,0 M8,6.7 C7.2,4.5 7.6,2 9.6,1.8 C11.4,1.7 11,4.6 9.2,7.2 M9.3,8 C11.5,7.2 14,7.6 14.2,9.6 C14.3,11.4 11.4,11 8.8,9.2 M8,9.3 C8.8,11.5 8.4,14 6.4,14.2 C4.6,14.3 5,11.4 6.8,8.8 M6.7,8 C4.5,8.8 2,8.4 1.8,6.4 C1.7,4.6 4.6,5 7.2,6.8", L.T("Cooling"))
	{
		hero = new Hero(M.Hero * 0.8, p);
		heroName = Card.Label("", p);
		StackPanel stackPanel = Ui.Stack(0.0, heroName, hero);
		TextBlock textBlock = Card.Label(L.T("Fans"), p);
		textBlock.HorizontalAlignment = HorizontalAlignment.Right;
		active = Ui.Text("—", Math.Round(M.Hero * 0.5), p.Ink, FontWeight.Medium, numbers: true);
		active.HorizontalAlignment = HorizontalAlignment.Right;
		StackPanel stackPanel2 = Ui.Stack(0.0, textBlock, active);
		stackPanel2.VerticalAlignment = VerticalAlignment.Bottom;
		stackPanel2.Margin = new Thickness(0.0, 0.0, 0.0, M.Hero * 0.1);
		Body.Children.Add(Ui.Columns("*,Auto", stackPanel, stackPanel2));
		if (Shows("cooling.fans"))
		{
			fans = new StackPanel
			{
				Spacing = 7.0
			};
			Body.Children.Add(fans);
		}
		if (Shows("cooling.board"))
		{
			temps = new Grid();
			Body.Children.Add(temps);
		}
        WidgetPresentation.Sensor((Control)hero.Parent!, "cooling.board");
        WidgetPresentation.Sensor((Control)active.Parent!, "cooling.fans");
	}

	protected override void UpdateReadings(Snapshot s, History h)
	{
		List<FanInfo> list = s.Fans.Where((FanInfo f) => !S.Hidden("fan:" + f.Name)).ToList();
		List<TempReading> list2 = s.BoardTemps.Where((TempReading t) => !S.Hidden("temp:" + t.Name)).ToList();
		base.IsVisible = list.Count > 0 || list2.Count > 0;
		base.Subtitle = s.BoardName ?? "";
		// Big number: the mainboard sensor, otherwise the hottest; the row below lists only the others.
		TempReading tempReading = list2.FirstOrDefault((TempReading t) => t.Name == "Board") ?? list2.OrderByDescending((TempReading t) => t.Celsius).FirstOrDefault();
		List<TempReading> others = list2.Where((TempReading t) => t != tempReading).ToList();
		heroName.Text = (((object)tempReading == null) ? L.T("Board") : Names.Temp(tempReading.Name, S)).ToUpper(L.Culture);
		hero.Set(Temp(tempReading?.Celsius), base.TempUnit, HeatColor(((object)tempReading != null) ? new double?(tempReading.Celsius + 5.0) : ((double?)null)));
		Motion.Text(active, (list.Count == 0) ? "—" : $"{list.Count((FanInfo f) => f.Rpm > 0.0)}/{list.Count}");
		base.Aside = "";
		if (fans != null)
		{
			double num = Math.Max(1800.0, list.Select((FanInfo f) => f.Rpm).DefaultIfEmpty(0.0).Max() * 1.08);
			foreach (var value2 in rows.Values)
			{
				((Control)value2.name.Parent).IsVisible = false;
			}
			foreach (FanInfo item in list.Take(8))
			{
				if (!rows.TryGetValue(item.Name, out (TextBlock, TextBlock, Meter) value))
				{
					TextBlock textBlock = Ui.Text("", 11.5, P.Soft, FontWeight.Medium);
					textBlock.Width = 92.0;
					textBlock.Margin = new Thickness(0.0, 0.0, 10.0, 0.0);
					Meter meter = new Meter
					{
						Height = 4.0,
						Palette = P,
						VerticalAlignment = VerticalAlignment.Center,
						MinWidth = 30.0
					};
					TextBlock textBlock2 = Ui.Text("", 11.5, P.Ink, FontWeight.Medium, numbers: true);
					textBlock2.Width = 72.0;
					textBlock2.TextAlignment = TextAlignment.Right;
					fans.Children.Add(Ui.Columns("Auto,*,Auto", textBlock, meter, textBlock2));
					value = (rows[item.Name] = (textBlock, textBlock2, meter));
				}
				((Control)value.Item1.Parent).IsVisible = true;
				value.Item1.Text = Names.Fan(item.Name, S);
				Motion.Text(value.Item2, (item.Rpm > 0.0) ? Format.Rpm(item.Rpm) : L.T("Stopped"));
				value.Item2.Foreground = Palette.Brush((item.Rpm > 0.0) ? P.Ink : P.Faint);
				value.Item3.Set(item.Rpm / num, P.Cool);
			}
		}
		if (temps == null)
		{
			return;
		}
		temps.IsVisible = others.Count > 0;
		string text = string.Join("|", from t in others.Take(3)
			select t.Name + "=" + Names.Temp(t.Name, S));
		if (!object.Equals(temps.Tag, text))
		{
			temps.Tag = text;
			temps.Children.Clear();
			temps.ColumnDefinitions = ColumnDefinitions.Parse(string.Join(",", (from _ in others.Take(3)
				select "*").DefaultIfEmpty("*")));
			int num2 = 0;
			foreach (TempReading item2 in others.Take(3))
			{
				Stat stat = new Stat(Names.Temp(item2.Name, S), P)
				{
					Tag = item2.Name
				};
				Grid.SetColumn(stat, num2++);
				temps.Children.Add(stat);
			}
		}
		foreach (Stat st in temps.Children.OfType<Stat>())
		{
			TempReading tempReading2 = list2.FirstOrDefault((TempReading x) => x.Name == (string)st.Tag);
			st.Value = TempWithUnit(tempReading2?.Celsius);
			st.Color = (((object)tempReading2 == null) ? P.Ink : HeatColor(tempReading2.Celsius + 5.0));
		}
	}
}
