using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed class PillTile : SummaryTile
{
	private readonly FillBack fill;

	private readonly Border icon;

	private readonly TextBlock name;
    private readonly TextBlock hardware;

	private readonly TextBlock sub;

	private readonly TextBlock value;

	private readonly TextBlock unit;

    private readonly TextBlock? uploadValue;
    private readonly TextBlock? uploadUnit;

	private string iconKey = "";

	private readonly DriveRows drives;

	private readonly StackPanel number;

	public PillTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s)
		: base(kind, index, p, m, s, half: false)
	{
		double num = (m.Subtitles ? 60 : 52);
		base.Background = Brushes.Transparent;
		base.Padding = new Thickness(0.0);
		base.CornerRadius = new CornerRadius(num / 2.0);
		base.BorderThickness = new Thickness(0.0);
		fill = new FillBack
		{
			Radius = num / 2.0,
			Track = p.Card
		};
		icon = new Border
		{
			Width = num - 18.0,
			Height = num - 18.0,
			CornerRadius = new CornerRadius((num - 18.0) / 2.0),
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(9.0, 0.0, 12.0, 0.0)
		};
		name = Ui.Text("", 13.5, p.Ink, FontWeight.DemiBold);
        hardware = Ui.Text("", 11, p.Soft);
        HardwareWidgetTitle.Register(this, name, hardware);
        hardware.TextWrapping = TextWrapping.Wrap;
        hardware.Margin = new Thickness(12, 0);
		sub = Ui.Text("", 11.5, p.Soft, FontWeight.Normal, numbers: true);
		value = Ui.Text("", m.Subtitles ? 24 : 21, p.Ink, FontWeight.Bold, numbers: true);
		value.LetterSpacing = -0.5;
		unit = Ui.Text("", 12.5, p.Soft, FontWeight.DemiBold, numbers: true);
		WidgetPresentation.Sensor(value, WidgetSensors.PrimaryKey(kind));
        WidgetPresentation.Sensor(unit, WidgetSensors.PrimaryKey(kind));
        WidgetPresentation.Sensor(sub, WidgetSensors.CaptionKey(kind));
		unit.VerticalAlignment = VerticalAlignment.Bottom;
		unit.Margin = new Thickness(1.0, 0.0, 0.0, 3.0);
		StackPanel stackPanel = Ui.Stack(0.0, name, sub);
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel2 = Ui.Row(0.0, value, unit);
		stackPanel2.VerticalAlignment = VerticalAlignment.Center;
		stackPanel2.Margin = new Thickness(8.0, 0.0, 18.0, 0.0);
		number = stackPanel2;
        if (kind == WidgetKind.Network)
        {
            sub.IsVisible = false;
            value.FontSize = m.Subtitles ? 18 : 16;
            unit.FontSize = 10.5;
            unit.Margin = new Thickness(0, 0, 0, 2);
            stackPanel2.Children.Clear();
            var downloadRow = TrafficRow("DL", value, unit);
            uploadValue = Ui.Text("", value.FontSize, p.Ink, FontWeight.Bold, numbers: true);
            SensorDetails.Value(uploadValue, "net.up");
            uploadUnit = Ui.Text("", 10.5, p.Soft, FontWeight.DemiBold, numbers: true);
            uploadUnit.VerticalAlignment = VerticalAlignment.Bottom;
            uploadUnit.Margin = new Thickness(0, 0, 0, 2);
            var uploadRow = TrafficRow("UP", uploadValue, uploadUnit);
            WidgetPresentation.Sensor(downloadRow, "net.down");
            WidgetPresentation.Sensor(uploadRow, "net.up");
            stackPanel2.Orientation = Orientation.Vertical;
            stackPanel2.Spacing = 2;
            stackPanel2.Children.Add(downloadRow);
            stackPanel2.Children.Add(uploadRow);
            Grid TrafficRow(string direction, TextBlock number, TextBlock units)
            {
                number.TextTrimming = TextTrimming.None;
                units.TextTrimming = TextTrimming.None;
                number.HorizontalAlignment = HorizontalAlignment.Right;
                Control Fit(Control child) => SteadyHeight.Of(new Viewbox { Child = child, StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = HorizontalAlignment.Stretch });
                var row = Ui.Columns("0.65*,1.6*,1*", Fit(Ui.Text(direction, 10.5, p.Cool, FontWeight.DemiBold)), Fit(number), Fit(units));
                row.Height = (m.Subtitles ? 22 : 19) * Math.Max(s.ActiveWidget?.Values ?? 1, s.ActiveWidget?.Labels ?? 1);
                return row;
            }
        }
		Grid item = Ui.Columns("Auto,*,Auto", icon, stackPanel, stackPanel2);
		if (kind == WidgetKind.Network)
		{
			// Keep the hardware title across the capsule instead of squeezing it between icon and rates.
			Grid.SetColumnSpan(stackPanel, 2);
			stackPanel.Margin = new Thickness(0, 0, 18, 6);
			item.RowDefinitions = RowDefinitions.Parse("Auto,Auto");
			Grid.SetRow(stackPanel2, 1);
			Grid.SetColumnSpan(stackPanel2, 2);
			Grid.SetRowSpan(icon, 2);
		}
		Grid.SetColumn(stackPanel2, 2);
		if (kind == WidgetKind.Network) Grid.SetColumn(stackPanel2, 1);
        item.Margin = new Thickness(0, 9);
		base.Child = new Panel
		{
            MinHeight = num,
            Classes = { "pill-surface" },
			Children =
			{
				(Control)fill,
				(Control)item
			}
		};
        var capsule = base.Child!;
        base.Child = null;
        // Drives: the capsule carries the name, every drive follows as its own line with a rounded bar.
        drives = new DriveRows(8, 11.5, 13, p.Soft, () => new FillBack { Height = 8, Radius = 4, Track = Palette.Mix(p.Card, p.Ink, p.Dark ? 0.1 : 0.08) },
            (c, r) => ((FillBack)c).Animate(r.Fraction, r.Color), r => r.Color) { Margin = new Thickness(12, 6, 12, 2), Tag = null };
        // Fixed line spacing: the drive lines are the only stretchable part of this design, so they would
        // otherwise take all the free height of the sidebar and drift far apart.
        base.Child = Ui.Stack(4, capsule, hardware, drives, Extras);
	}

	protected override void Show(Summary sum)
	{
        BindPrimary(value);
        hardware.Text = Naming.Hardware(Slot ?? S.ActiveWidget, Index, sum.Name);
        hardware.IsVisible = Naming.ShowHardware(Slot ?? S.ActiveWidget, S) && hardware.Text.Length > 0;
		Color c = Shade(sum);
		bool all = Kind == WidgetKind.Drives;
		number.IsVisible = !all;
		drives.IsVisible = false;
		if (all)
		{
			drives.Show(DriveRows.Of(Index, Last.Snap, Last.History, S, P));
		}
		// The capsule shows download and upload side by side, so it is not filled for one of them.
		fill.Animate((Kind is WidgetKind.Network or WidgetKind.Drives) ? 0.0 : (sum.Second ?? sum.Fraction), Palette.Alpha(c, P.Dark ? 0.24 : 0.2));
		string text = sum.Glyph + c;
		if (iconKey != text)
		{
			iconKey = text;
			icon.Background = Palette.Brush(c);
			icon.Child = new Viewbox
			{
				Width = 17.0,
				Height = 17.0,
				Child = Ui.Glyph(sum.Glyph, Readings.TextOn(c, P), 16.0)
			};
		}
		name.Text = Naming.Title(base.Slot, Index, sum.Title);
		// For drives the big number is the hottest one; the line below names it, its row is not repeated.
		sub.Text = all ? "" : ((sum.Second.HasValue && sum.SecondLabel.Length > 0) ? (L.T("Load") + " " + sum.SecondLabel) : sum.Caption);
		sub.IsVisible = Kind != WidgetKind.Network && sub.Text.Length > 0;
		Motion.Text(value, sum.Value);
		unit.Text = ((sum.Value == "—") ? "" : sum.Unit);
        if (uploadValue != null && uploadUnit != null)
        {
            var upload = Format.Split(Format.Rate(Last.Snap.Network?.UploadBytesPerSecond));
            Motion.Text(uploadValue, upload.Number);
            uploadUnit.Text = upload.Unit;
        }
	}
}
