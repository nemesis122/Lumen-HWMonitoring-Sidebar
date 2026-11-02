using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed class WorldClocksCard : Card
{
	private readonly StackPanel rows = new StackPanel
	{
		Spacing = 8.0
	};

	private readonly List<(TimeZoneInfo zone, TextBlock time, TextBlock info, Border dot)> clocks = new List<(TimeZoneInfo, TextBlock, TextBlock, Border)>();

	public WorldClocksCard(Palette p, Metrics m, Settings s)
		: base(p, m, s, "M8,1.5 A6.5,6.5 0 1 1 7.99,1.5 Z M1.5,8 H14.5 M8,1.5 C5.5,4 5.5,12 8,14.5 M8,1.5 C10.5,4 10.5,12 8,14.5", L.T("World clocks"))
	{
		foreach (string worldClock in s.WorldClocks)
		{
			if (TimeZoneInfo.TryFindSystemTimeZoneById(worldClock, out TimeZoneInfo timeZoneInfo))
			{
				TextBlock textBlock = Ui.Text(City(worldClock), 13.0, p.Ink, FontWeight.Medium);
				TextBlock textBlock2 = Ui.Text("", 11.5, p.Soft, FontWeight.Normal, numbers: true);
				Border border = new Border
				{
					Width = 7.0,
					Height = 7.0,
					CornerRadius = new CornerRadius(4.0),
					VerticalAlignment = VerticalAlignment.Center,
					Margin = new Thickness(0.0, 0.0, 8.0, 0.0)
				};
				TextBlock textBlock3 = Ui.Text("", 20.0, p.Ink, FontWeight.Light, numbers: true);
				StackPanel stackPanel = Ui.Stack(0.0, textBlock, textBlock2);
				rows.Children.Add(Ui.Columns("Auto,*,Auto", border, stackPanel, textBlock3));
				clocks.Add((timeZoneInfo, textBlock3, textBlock2, border));
			}
		}
		if (clocks.Count == 0)
		{
			rows.Children.Add(Ui.Text(L.T("Choose cities in the settings."), 12.0, p.Soft));
		}
		Body.Children.Add(rows);
	}

	public static string City(string id)
	{
		int num = id.LastIndexOf('/') + 1;
		return id.Substring(num, id.Length - num).Replace('_', ' ');
	}

	public override void Update(Snapshot s, History h)
	{
		TimeSpan utcOffset = TimeZoneInfo.Local.GetUtcOffset(s.Time);
		DateTime dateTime = s.Time - utcOffset;
		foreach (var clock in clocks)
		{
			TimeZoneInfo item = clock.zone;
			TextBlock item2 = clock.time;
			TextBlock item3 = clock.info;
			Border item4 = clock.dot;
			DateTime dateTime2 = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc), item);
			item2.Text = dateTime2.ToString("HH:mm", L.Culture);
			double totalHours = (item.GetUtcOffset(dateTime) - utcOffset).TotalHours;
			string text = ((totalHours == 0.0) ? "±0 h" : (((totalHours > 0.0) ? "+" : "−") + Math.Abs(totalHours).ToString("0.#", CultureInfo.InvariantCulture) + " h"));
			string text2 = ((dateTime2.Date > s.Time.Date) ? L.T("tomorrow") : ((dateTime2.Date < s.Time.Date) ? L.T("yesterday") : L.T("today")));
			item3.Text = text2 + " · " + text;
			int hour = dateTime2.Hour;
			item4.Background = Palette.Brush((hour >= 7 && hour < 19) ? P.Ramp[2] : P.Cool);
		}
	}
}
