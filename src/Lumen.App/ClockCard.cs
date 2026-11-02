using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed class ClockCard : Card
{
	private readonly TextBlock time;

	private readonly TextBlock seconds;

	private readonly TextBlock date;

	private readonly TextBlock dayText;

	private readonly Control dayBlock;

	private readonly Meter day;

	public ClockCard(Palette p, Metrics m, Settings s)
		: base(p, m, s, "M8,1.5 A6.5,6.5 0 1 1 7.99,1.5 Z M8,4.5 V8 L10.5,9.5", L.T("Today"))
	{
		time = Ui.Text("", m.Hero * 1.15, p.Ink, FontWeight.Light, numbers: true);
		time.LetterSpacing = (0.0 - m.Hero) * 0.04;
		seconds = Ui.Text("", m.Hero * 0.42, p.Soft, FontWeight.Normal, numbers: true);
		seconds.VerticalAlignment = VerticalAlignment.Bottom;
		seconds.Margin = new Thickness(4.0, 0.0, 0.0, m.Hero * 0.2);
		seconds.IsVisible = Shows("clock.seconds");
		date = Ui.Text("", 12.5, p.Soft, FontWeight.Medium);
		date.IsVisible = Shows("clock.date");
		Body.Children.Add(Ui.Row(0.0, time, seconds));
		Body.Children.Add(date);
		day = new Meter
		{
			Height = 4.0,
			Palette = p
		};
		dayText = Ui.Text("", 11.0, p.Soft, FontWeight.Normal, numbers: true);
		dayText.HorizontalAlignment = HorizontalAlignment.Right;
		dayBlock = Ui.Stack(5.0, Ui.Columns("*,Auto", Card.Label(L.T("Day"), p), dayText), day);
		dayBlock.IsVisible = Shows("clock.day");
		Body.Children.Add(dayBlock);
	}

	public override void Update(Snapshot s, History h)
	{
		DateTime dateTime = s.Time;
		time.Text = dateTime.ToString("HH:mm", L.Culture);
		seconds.Text = dateTime.ToString("ss", L.Culture);
		string text = dateTime.ToString(L.Culture.DateTimeFormat.LongDatePattern.Contains("dddd") ? L.Culture.DateTimeFormat.LongDatePattern : ("dddd, " + L.Culture.DateTimeFormat.LongDatePattern), L.Culture);
		TextBlock textBlock = date;
		string text2;
		if (text.Length <= 0)
		{
			text2 = text;
		}
		else
		{
			ReadOnlySpan<char> readOnlySpan = new ReadOnlySpan<char>(char.ToUpper(text[0], L.Culture));
			string text3 = text;
			text2 = string.Concat(readOnlySpan, text3.Substring(1, text3.Length - 1));
		}
		textBlock.Text = text2;
		base.Aside = L.T("Week") + " " + ISOWeek.GetWeekOfYear(dateTime);
		double num = dateTime.TimeOfDay.TotalHours / 24.0;
		day.Set(num, P.Cool);
		TimeSpan timeSpan = TimeSpan.FromHours(24.0) - dateTime.TimeOfDay;
		dayText.Text = Format.Number(num * 100.0) + " %  ·  " + L.F("{0} h {1:00} m left", (int)timeSpan.TotalHours, timeSpan.Minutes);
	}
}
