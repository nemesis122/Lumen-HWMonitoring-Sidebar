using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Lumen.Core;

namespace Lumen.App;

public sealed class MediaCard : Card
{
	private readonly TextBlock title;

	private readonly TextBlock artist;

	private readonly TextBlock time;

	private readonly TextBlock empty;

	private readonly Meter progress;

	private readonly Border cover;

	private readonly Button play;

	private readonly Control content;

	private byte[]? lastCover;

	private bool coverShown;

	public static event Action<string>? Command;

	public MediaCard(Palette p, Metrics m, Settings s)
		: base(p, m, s, "M6,12 V3 L13,1.8 V10.5 M6,12 A2,2 0 1 1 5.99,12 Z M13,10.5 A2,2 0 1 1 12.99,10.5 Z", L.T("Now playing"))
	{
		cover = new Border
		{
			Width = 56.0,
			Height = 56.0,
			CornerRadius = new CornerRadius(10.0),
			ClipToBounds = true
		};
		title = Ui.Text("", 13.5, p.Ink, FontWeight.DemiBold);
		artist = Ui.Text("", 12.0, p.Soft);
		StackPanel stackPanel = Ui.Stack(1.0, title, artist);
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		stackPanel.Margin = new Thickness(12.0, 0.0, 0.0, 0.0);
		progress = new Meter
		{
			Height = 4.0,
			Palette = p
		};
		time = Ui.Text("", 11.0, p.Soft, FontWeight.Normal, numbers: true);
		play = Ctl("M5,3 L13,8 L5,13 Z", "toggle", 15.0);
		StackPanel stackPanel2 = Ui.Row(2.0, Ctl("M12,3 V13 M11,3 L4,8 L11,13 Z", "previous", 13.0), play, Ctl("M4,3 V13 M5,3 L12,8 L5,13 Z", "next", 13.0));
		stackPanel2.HorizontalAlignment = HorizontalAlignment.Right;
		StackPanel stackPanel3 = Ui.Stack(M.Gap, Ui.Columns("Auto,*", cover, stackPanel), progress, Ui.Columns("*,Auto", time, stackPanel2));
		content = stackPanel3;
		empty = Ui.Text(L.T("Nothing is playing."), 12.0, p.Soft);
		Body.Children.Add(content);
		Body.Children.Add(empty);
		Button Ctl(string glyph, string cmd, double size)
		{
			Button button = new Button();
			button.Content = Ui.Glyph(glyph, p.Ink, size);
			button.Background = Brushes.Transparent;
			button.BorderThickness = new Thickness(0.0);
			button.Padding = new Thickness(8.0, 4.0);
			button.CornerRadius = new CornerRadius(8.0);
			button.Cursor = new Cursor(StandardCursorType.Hand);
			button.Click += delegate
			{
				MediaCard.Command?.Invoke(cmd);
			};
			return button;
		}
        WidgetPresentation.Sensor(title, "media.title");
        WidgetPresentation.Sensor(artist, "media.artist");
        WidgetPresentation.Sensor(progress, "media.progress");
        WidgetPresentation.Sensor(time, "media.progress");
        WidgetPresentation.Sensor(cover, "media.cover");
	}

	protected override void UpdateReadings(Snapshot s, History h)
	{
		MediaInfo media = s.Media;
		content.IsVisible = media != null;
		empty.IsVisible = media == null;
		if (media == null)
		{
			base.Aside = "";
			return;
		}
		base.Aside = media.App;
		title.Text = media.Title;
		artist.Text = media.Artist;
		play.Content = Ui.Glyph(media.Playing ? "M5,3 V13 M11,3 V13" : "M5,3 L13,8 L5,13 Z", P.Ink);
		double? durationSeconds = media.DurationSeconds;
		if (durationSeconds.HasValue)
		{
			double valueOrDefault = durationSeconds.GetValueOrDefault();
			if (valueOrDefault > 0.0)
			{
				durationSeconds = media.PositionSeconds;
				if (durationSeconds.HasValue)
				{
					double valueOrDefault2 = durationSeconds.GetValueOrDefault();
					progress.IsVisible = true;
					progress.Set(valueOrDefault2 / valueOrDefault, P.Cool);
					time.Text = Clock(valueOrDefault2) + " / " + Clock(valueOrDefault);
					goto IL_0146;
				}
			}
		}
		progress.IsVisible = false;
		time.Text = "";
		goto IL_0146;
		IL_0146:
		if (coverShown && media.Cover == lastCover)
		{
			return;
		}
		coverShown = true;
		lastCover = media.Cover;
		cover.Child = null;
		cover.Background = new LinearGradientBrush
		{
			StartPoint = new RelativePoint(0.0, 0.0, RelativeUnit.Relative),
			EndPoint = new RelativePoint(1.0, 1.0, RelativeUnit.Relative),
			GradientStops = 
			{
				new GradientStop(P.Ramp[0], 0.0),
				new GradientStop(Palette.Mix(P.Ramp[0], P.Ramp[3], 0.55), 1.0)
			}
		};
		byte[] array = media.Cover;
		if (array != null && array.Length > 0)
		{
			try
			{
				cover.Child = new Image
				{
					Source = new Bitmap(new MemoryStream(array)),
					Stretch = Stretch.UniformToFill
				};
			}
			catch
			{
			}
		}
		if (cover.Child == null)
		{
			Control control = Ui.Glyph("M6,12 V3 L13,1.8 V10.5 M6,12 A2,2 0 1 1 5.99,12 Z M13,10.5 A2,2 0 1 1 12.99,10.5 Z", Colors.White, 24.0);
			control.HorizontalAlignment = HorizontalAlignment.Center;
			cover.Child = control;
		}
	}

	private static string Clock(double seconds)
	{
		return $"{(int)(seconds / 60.0)}:{(int)(seconds % 60.0):00}";
	}
}
