using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed class CalendarCard : Card
{
	private readonly Grid grid = new Grid();

	private DateTime shown;

	public CalendarCard(Palette p, Metrics m, Settings s)
		: base(p, m, s, "M2,3.5 H14 V14 H2 Z M2,6.5 H14 M5,2 V5 M11,2 V5", "")
	{
		Body.Children.Add(grid);
	}

	public override void Update(Snapshot s, History h)
	{
		DateTime date = s.Time.Date;
		if (date == shown)
		{
			return;
		}
		shown = date;
		DateTime dateTime = new DateTime(date.Year, date.Month, 1);
		string text = dateTime.ToString("MMMM yyyy", L.Culture);
		ReadOnlySpan<char> readOnlySpan = new ReadOnlySpan<char>(char.ToUpper(text[0], L.Culture));
		string text2 = text;
		base.Title = string.Concat(readOnlySpan, text2.Substring(1, text2.Length - 1));
		base.Aside = L.T("Week") + " " + ISOWeek.GetWeekOfYear(date);
		grid.Children.Clear();
		grid.ColumnDefinitions = ColumnDefinitions.Parse("22,*,*,*,*,*,*,*");
		grid.RowDefinitions.Clear();
		DateTime dateTime2 = dateTime.AddDays(-((int)(dateTime.DayOfWeek + 6) % 7));
		int num = (int)Math.Ceiling(((dateTime - dateTime2).TotalDays + (double)DateTime.DaysInMonth(date.Year, date.Month)) / 7.0);
		for (int i = 0; i <= num; i++)
		{
			grid.RowDefinitions.Add(new RowDefinition((i == 0) ? GridLength.Auto : new GridLength(M.Subtitles ? 24 : 20)));
		}
		for (int j = 0; j < 7; j++)
		{
			string text3 = L.Culture.DateTimeFormat.AbbreviatedDayNames[(j + 1) % 7];
			TextBlock textBlock = Card.Label(text3.TrimEnd('.').Substring(0, Math.Min(2, text3.TrimEnd('.').Length)), P);
			textBlock.HorizontalAlignment = HorizontalAlignment.Center;
			textBlock.Margin = new Thickness(0.0, 0.0, 0.0, 4.0);
			Place(textBlock, 0, j + 1);
		}
		for (int k = 0; k < num; k++)
		{
			DateTime date2 = dateTime2.AddDays(k * 7);
			TextBlock textBlock2 = Ui.Text(ISOWeek.GetWeekOfYear(date2).ToString(), 10.0, P.Faint, FontWeight.Normal, numbers: true);
			textBlock2.HorizontalAlignment = HorizontalAlignment.Left;
			Place(textBlock2, k + 1, 0);
			for (int l = 0; l < 7; l++)
			{
				DateTime dateTime3 = date2.AddDays(l);
				bool flag = dateTime3.Month == date.Month;
				bool flag2 = dateTime3 == date;
				TextBlock textBlock3 = Ui.Text(dateTime3.Day.ToString(), 12.0, (!flag2) ? ((!flag) ? P.Faint : ((l >= 5) ? P.Soft : P.Ink)) : (P.Dark ? Color.Parse("#0B0E14") : Colors.White), flag2 ? FontWeight.Bold : FontWeight.Normal, numbers: true);
				textBlock3.HorizontalAlignment = HorizontalAlignment.Center;
				Control c = textBlock3;
				if (flag2)
				{
					c = new Border
					{
						Background = Palette.Brush(P.Cool),
						CornerRadius = new CornerRadius(10.0),
						Width = 22.0,
						Height = 20.0,
						Child = textBlock3,
						HorizontalAlignment = HorizontalAlignment.Center
					};
				}
				Place(c, k + 1, l + 1);
			}
		}
	}

	private void Place(Control c, int row, int col)
	{
		Grid.SetRow(c, row);
		Grid.SetColumn(c, col);
		grid.Children.Add(c);
	}
}
