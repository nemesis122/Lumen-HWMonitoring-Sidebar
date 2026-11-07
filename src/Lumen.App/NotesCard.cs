using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed class NotesCard : Card
{
	public static event Action<string>? Changed;

	public NotesCard(Palette p, Metrics m, Settings s)
		: base(p, m, s, "M3,2 H10 L13,5 V14 H3 Z M10,2 V5 H13 M5.5,8 H10.5 M5.5,10.5 H10.5", L.T("Notes"))
	{
		TextBox box = new TextBox
		{
			Text = s.Notes,
			Watermark = L.T("Write a note…"),
			AcceptsReturn = true,
			TextWrapping = TextWrapping.Wrap,
			MinHeight = 64.0,
			MaxHeight = 220.0,
			FontFamily = Ui.Font,
			FontStretch = Ui.FontWidth,
			FontSize = 12.5,
			Foreground = Palette.Brush(p.Ink),
			Background = Palette.Brush(p.Track),
			BorderThickness = new Thickness(0.0),
			CornerRadius = new CornerRadius(8.0),
			Padding = new Thickness(8.0, 6.0)
		};
		box.Resources["TextControlBackgroundPointerOver"] = Palette.Brush(p.Track);
		box.Resources["TextControlBackgroundFocused"] = Palette.Brush(p.Track);
		box.Resources["TextControlForegroundFocused"] = Palette.Brush(p.Ink);
		box.Resources["TextControlBorderBrushFocused"] = Palette.Brush(Palette.Alpha(p.Cool, 0.6));
		box.LostFocus += delegate
		{
			s.Notes = box.Text ?? "";
			NotesCard.Changed?.Invoke(s.Notes);
		};
		Body.Children.Add(box);
	}

	protected override void UpdateReadings(Snapshot s, History h)
	{
	}
}
