using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lumen.Core;

namespace Lumen.App;

public sealed class PillDuoPlusTile : SummaryTile
{
	private readonly FillBack fill;

	private readonly Meter lower;

	private readonly Border icon;

	private readonly TextBlock name;

	private readonly TextBlock value;

	private readonly TextBlock unit;

	private readonly TextBlock badgeText;

	private readonly Border badge;

	private string iconKey = "";
	private readonly Grid extraGrid;
	private readonly Border extraCapsule;
	private readonly TextBlock[] extraLabels = new TextBlock[2], extraValues = new TextBlock[2];
	private readonly StackPanel[] extraCells = new StackPanel[2];
	private readonly Border divider;
	private int extraCount = -1;

	public PillDuoPlusTile(WidgetKind kind, int index, Palette p, Metrics m, Settings s)
		: base(kind, index, p, m, s, half: false)
	{
		double num = (m.Subtitles ? 72 : 64);
		base.Background = Brushes.Transparent;
		base.Padding = new Thickness(0.0);
		base.CornerRadius = new CornerRadius(0);
		base.BorderThickness = new Thickness(0.0);
		base.ClipToBounds = false;
		fill = new FillBack
		{
			Radius = num / 2.0,
			Track = p.Card
		};
		lower = new Meter { Height = 3.0, Palette = p, Margin = new Thickness(0, 4, 0, 0) };
		icon = new Border
		{
			Width = num - 20.0,
			Height = num - 20.0,
			CornerRadius = new CornerRadius((num - 20.0) / 2.0),
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(10.0, 0.0, 12.0, 0.0)
		};
		name = Ui.Text("", 13.5, p.Ink, FontWeight.DemiBold);
		name.TextTrimming = TextTrimming.CharacterEllipsis;
		badgeText = Ui.Text("", 11.0, p.Ink, FontWeight.DemiBold, numbers: true);
		badge = new Border
		{
			CornerRadius = new CornerRadius(8.0),
			Padding = new Thickness(7.0, 1.0),
			Child = badgeText,
			HorizontalAlignment = HorizontalAlignment.Left,
			Margin = new Thickness(0.0, 3.0, 0.0, 0.0)
		};
		value = Ui.Text("", m.Subtitles ? 30 : 26, p.Ink, FontWeight.Black, numbers: true);
		value.LetterSpacing = -0.5;
		unit = Ui.Text("", 12.5, p.Soft, FontWeight.DemiBold);
		unit.VerticalAlignment = VerticalAlignment.Bottom;
		unit.Margin = new Thickness(1.0, 0.0, 0.0, 3.0);
		StackPanel stackPanel = Ui.Stack(0.0, name, badge, lower);
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel2 = Ui.Row(0.0, value, unit);
		stackPanel2.VerticalAlignment = VerticalAlignment.Center;
		stackPanel2.Margin = new Thickness(8.0, 0.0, 20.0, 0.0);
		Grid item = Ui.Columns("Auto,*,Auto", icon, stackPanel, stackPanel2);
		Grid.SetColumn(stackPanel2, 2);
		        var upper = new Border { CornerRadius = new CornerRadius(num/2), ClipToBounds = true,
            Child = new Panel { Height = num, Children = { fill, item } } };
        extraGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,1,*") };
        for(int i=0;i<2;i++) {
            extraLabels[i]=Ui.Text("",10.5,p.Soft,FontWeight.Medium);
            extraValues[i]=Ui.Text("",17,p.Ink,FontWeight.Bold,true);
            extraLabels[i].MaxLines=1;extraValues[i].MaxLines=1;
            extraCells[i]=Ui.Stack(1,extraLabels[i],extraValues[i]);
            extraCells[i].Margin=new Thickness(i==0?0:12,0,i==0?12:0,0);
            Grid.SetColumn(extraCells[i],i*2);extraGrid.Children.Add(extraCells[i]);
        }
        divider=new Border{Width=1,Background=Palette.Brush(p.Track),Margin=new Thickness(0,3)};
        Grid.SetColumn(divider,1);extraGrid.Children.Add(divider);
        extraCapsule=new Border { CornerRadius=new CornerRadius(17), Background=Palette.Brush(p.Card),
            Padding=new Thickness(16,8),Margin=new Thickness(10,0),Child=extraGrid,IsVisible=false };
        base.Child=Ui.Stack(5,upper,extraCapsule);

	}

	protected override void Show(Summary sum)
	{
		        var items=Summaries.Extras(Kind,Index,Last.Snap,Last.History,S)
            .Where(x=>!string.IsNullOrWhiteSpace(x.Value)&&!x.Value.Contains('—')).ToList();
        if(Kind==WidgetKind.Cpu && S.Shows("cpu.clock") && Last.Snap.Cpu?.ClockMhz is double clock && double.IsFinite(clock))
            items.Insert(0,(L.T("Clock speed"),Format.Clock(clock)));
        items=items.Take(2).ToList();
        if(extraCount!=items.Count) {
            extraCount=items.Count;extraCapsule.IsVisible=extraCount>0;
            extraCells[0].IsVisible=extraCount>0;extraCells[1].IsVisible=extraCount>1;divider.IsVisible=extraCount>1;
            extraGrid.ColumnDefinitions=new ColumnDefinitions(extraCount>1?"*,1,*":"*");
        }
        for(int i=0;i<items.Count;i++) {
            extraLabels[i].Text=items[i].Label;Motion.Text(extraValues[i],items[i].Value);
            ToolTip.SetTip(extraCells[i],items[i].Label+": "+items[i].Value);
        }

		Color c = Shade(sum);
		fill.Animate(sum.Second ?? sum.Fraction, Palette.Alpha(P.Cool, P.Dark ? 0.2 : 0.16));
		lower.Set(sum.Fraction, c);
		lower.IsVisible = sum.Second.HasValue;
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
		bool flag = sum.Second.HasValue && sum.SecondLabel.Length > 0;
		badgeText.Text = (flag ? (L.T("Load") + " " + sum.SecondLabel) : sum.Caption);
		badge.Background = Palette.Brush(flag ? Palette.Alpha(P.Cool, 0.22) : Colors.Transparent);
		badge.Padding = (flag ? new Thickness(7.0, 1.0) : new Thickness(0.0));
		badgeText.Foreground = Palette.Brush(flag ? P.Ink : P.Soft);
		Motion.Text(value, sum.Value);
		value.Foreground = Palette.Brush((sum.Color == P.Ink) ? P.Ink : sum.Color);
		unit.Text = ((sum.Value == "—") ? "" : sum.Unit);
	}
}



