using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed class WeatherCard : Card
{
	private readonly Hero hero;

	private readonly WeatherIcon icon;

	private readonly TextBlock sky;

	private readonly TextBlock empty;

	private readonly Grid? forecast;

	private readonly Dictionary<string, Stat> stats;

	private readonly Control content;

	public WeatherCard(Palette p, Metrics m, Settings s)
		: base(p, m, s, "M4.5,12.5 H11.5 A3,3 0 0 0 11.7,6.5 A4,4 0 0 0 4.1,7.6 A2.5,2.5 0 0 0 4.5,12.5 Z", L.T("Weather"))
	{
		hero = new Hero(M.Hero, p);
		icon = new WeatherIcon
		{
			Width = M.Hero * 1.15,
			Height = M.Hero * 1.15,
			Palette = p,
			VerticalAlignment = VerticalAlignment.Center
		};
		sky = Ui.Text("", 12.5, p.Soft, FontWeight.Medium);
		StackPanel stackPanel = Ui.Stack(0.0, hero, sky);
		Grid grid = Ui.Columns("*,Auto", stackPanel, icon);
		StackPanel stackPanel2 = Ui.Stack(M.Gap, grid);
		stats = new Dictionary<string, Stat>();
		foreach (var (key, label) in new(string, string)[3]
		{
			("weather.feels", L.T("Feels like")),
			("weather.wind", L.T("Wind")),
			("weather.humidity", L.T("Humidity"))
		}.Where(((string, string) i) => Shows(i.Item1)).ToList())
		{
			stats[key] = new Stat(label, p);
		}
		if (stats.Count > 0)
		{
			stackPanel2.Children.Add(StatGrid.Of(stats.Values.ToArray()));
		}
		if (Shows("weather.forecast"))
		{
			forecast = new Grid
			{
				ColumnDefinitions = ColumnDefinitions.Parse("*,*,*"),
				Margin = new Thickness(0.0, 2.0, 0.0, 0.0)
			};
			stackPanel2.Children.Add(forecast);
		}
		content = stackPanel2;
		empty = Ui.Text(L.T("Set a place in the settings."), 12.0, p.Soft);
		empty.TextWrapping = TextWrapping.Wrap;
		empty.TextTrimming = TextTrimming.None;
		Body.Children.Add(content);
		Body.Children.Add(empty);
        WidgetPresentation.Sensor(hero, "weather.temp");
        WidgetPresentation.Sensor(sky, "weather.sky");
        WidgetPresentation.Sensor(icon, "weather.sky");
	}

	protected override void UpdateReadings(Snapshot s, History h)
	{
		WeatherInfo w = s.Weather;
		content.IsVisible = w != null;
		empty.IsVisible = w == null;
		if (w == null)
		{
			base.Aside = "";
			return;
		}
		base.Aside = w.Place;
		hero.Set(Temp(w.TemperatureC), base.TempUnit, P.At(Math.Clamp((w.TemperatureC + 10.0) / 45.0, 0.0, 1.0)));
		string text = WeatherClient.Group(w.Code);
		icon.Set(text, w.IsDay);
		sky.Text = Summaries.Sky(w.Code);
		Card.Put(stats, "weather.feels", TempWithUnit(w.FeelsLikeC));
		Dictionary<string, Stat> dictionary = stats;
		double? windKmh = w.WindKmh;
		object value;
		if (windKmh.HasValue)
		{
			double valueOrDefault = windKmh.GetValueOrDefault();
			value = Format.Number(S.Fahrenheit ? (valueOrDefault / 1.609) : valueOrDefault) + (S.Fahrenheit ? " mph" : " km/h");
		}
		else
		{
			value = "—";
		}
		Card.Put(dictionary, "weather.wind", (string)value);
		Card.Put(stats, "weather.humidity", Format.Percent(w.Humidity));
		if (forecast != null)
		{
			forecast.Children.Clear();
			List<WeatherDay> list = w.Days.Where((WeatherDay d) => d.Date.Date > w.Updated.Date).Take(3).ToList();
			for (int num = 0; num < list.Count; num++)
			{
				WeatherDay weatherDay = list[num];
				TextBlock textBlock = Card.Label(weatherDay.Date.ToString("ddd", L.Culture).TrimEnd('.'), P);
				WeatherIcon weatherIcon = new WeatherIcon
				{
					Width = 22.0,
					Height = 22.0,
					Palette = P,
					HorizontalAlignment = HorizontalAlignment.Left
				};
				weatherIcon.Set(WeatherClient.Group(weatherDay.Code), isDay: true);
				StackPanel stackPanel = Ui.Row(5.0, Ui.Text(Temp(weatherDay.MaxC) + "°", 12.5, P.Ink, FontWeight.Medium, numbers: true), Ui.Text(Temp(weatherDay.MinC) + "°", 12.5, P.Soft, FontWeight.Normal, numbers: true));
				StackPanel stackPanel2 = Ui.Stack(3.0, textBlock, Ui.Row(6.0, weatherIcon, stackPanel));
				Grid.SetColumn(stackPanel2, num);
				forecast.Children.Add(stackPanel2);
			}
		}
	}
}
