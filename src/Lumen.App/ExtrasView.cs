using System.Collections.Generic;
using System.Linq;
using System.Net;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Lumen.App;

public sealed class ExtrasView : ContentControl
{
	private readonly Palette p;

	private readonly bool wide;

	private string shape = "";

	private readonly List<Stat> stats = new List<Stat>();

	private readonly List<TextBlock> values = new List<TextBlock>();

	public ExtrasView(Palette p, bool wide)
	{
		this.p = p;
		this.wide = wide;
	}

	public void Set(List<(string Label, string Value)> items)
	{
		base.IsVisible = items.Count > 0;
		List<(string Label, string Value)> shown = items.Take(wide ? 6 : 3).ToList();
		string text = string.Join("|", shown.Select(((string Label, string Value) i) => i.Label + (Long(i.Value) ? "+" : "")));
		if (text != shape)
		{
			shape = text;
			stats.Clear();
			values.Clear();
			if (wide)
			{
				foreach (var item4 in shown)
				{
					string item = item4.Label;
					stats.Add(new Stat(item, p)
					{
						Margin = new Thickness(0.0, 0.0, 6.0, 4.0)
					});
				}
				Control[] array = stats.Where((Stat _, int i) => !Long(shown[i].Value)).Cast<Control>().ToArray();
				Control[] array2 = stats.Where((Stat _, int i) => Long(shown[i].Value)).Cast<Control>().ToArray();
				StackPanel stackPanel = new StackPanel();
				if (array.Length != 0)
				{
					stackPanel.Children.Add(new StatPanel(array.Select((Control _) => 1.0).ToArray(), array)
					{
						MaxColumns = 3,
						Balanced = true
					});
				}
				Control[] array3 = array2;
				foreach (Control item2 in array3)
				{
					stackPanel.Children.Add(item2);
				}
				base.Content = ((shown.Count == 0) ? null : stackPanel);
			}
			else
			{
				StackPanel stackPanel2 = new StackPanel
				{
					Spacing = 1.0
				};
				foreach (var item5 in shown)
				{
					TextBlock textBlock = Ui.Text(item5.Label, 10.5, p.Faint, FontWeight.Medium);
					TextBlock textBlock2 = Ui.Text("", 10.5, p.Ink, FontWeight.Medium, numbers: true);
					textBlock2.HorizontalAlignment = HorizontalAlignment.Right;
					values.Add(textBlock2);
					Grid item3 = Ui.Columns("*,Auto", textBlock, textBlock2);
					stackPanel2.Children.Add(item3);
				}
				base.Content = stackPanel2;
			}
		}
		for (int num2 = 0; num2 < shown.Count; num2++)
		{
			if (wide)
			{
				stats[num2].Value = shown[num2].Value;
			}
			else
			{
				values[num2].Text = shown[num2].Value;
			}
		}
		static bool Long(string v)
		{
			IPAddress address;
			if (v.Length <= 14)
			{
				return IPAddress.TryParse(v, out address);
			}
			return true;
		}
	}
}
