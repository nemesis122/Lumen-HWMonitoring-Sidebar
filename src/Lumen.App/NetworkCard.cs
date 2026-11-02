using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Lumen.Core;

namespace Lumen.App;

public sealed class NetworkCard : Card
{
	private readonly Hero down;

	private readonly Hero up;

	private readonly MirrorChart? chart;

	private readonly Dictionary<string, Stat> stats;

	private readonly Meter? linkBar;

	private readonly TextBlock? externalValue;

	private readonly Control? externalRow;

	public NetworkCard(Palette p, Metrics m, Settings s)
		: base(p, m, s, "M5,13 V3 M2.5,5.5 L5,3 L7.5,5.5 M11,3 V13 M8.5,10.5 L11,13 L13.5,10.5", L.T("Network"))
	{
		NetworkCard networkCard = this;
		down = new Hero(Math.Round(m.Hero * 0.58), p);
		up = new Hero(Math.Round(m.Hero * 0.58), p);
		Control control = Labelled(L.T("Upload"), p.Calm, up);
		control.HorizontalAlignment = HorizontalAlignment.Right;
		Body.Children.Add(Ui.Columns("*,Auto", Labelled(L.T("Download"), p.Cool, down), control));
		if (Shows("net.chart"))
		{
			chart = new MirrorChart
			{
				Height = m.Chart + 6.0,
				Palette = p
			};
			Body.Children.Add(chart);
		}
		List<(string, string)> source = new(string, string)[3]
		{
			("net.use", L.T("Link use")),
			("net.peak", L.T("2-min peak")),
			("net.address", L.T("Address"))
		}.Where(((string, string) i) => networkCard.Shows(i.Item1)).ToList();
		stats = source.ToDictionary(((string, string) i) => i.Item1, ((string, string) i) => new Stat(i.Item2, p));
		if (stats.Count > 0)
		{
			Body.Children.Add(StatGrid.Weighted(string.Join(",", stats.Keys.Select((string k) => (!(k == "net.address")) ? "*" : "1.35*")), stats.Values.ToArray()));
		}
		if (stats.ContainsKey("net.use"))
		{
			linkBar = new Meter
			{
				Height = 3.0,
				Palette = p,
				Margin = new Thickness(0.0, -4.0, 0.0, 0.0)
			};
			Body.Children.Add(linkBar);
		}
		if (Shows("net.external"))
		{
			externalValue = Ui.Text("", 12.5, p.Ink, FontWeight.Medium, numbers: true);
			externalRow = Ui.Stack(1.0, Card.Label(L.T("External IP"), p), externalValue);
			Body.Children.Add(externalRow);
			ShowExternal();
			ExternalIp.Changed += OnExternal;
			base.DetachedFromVisualTree += delegate
			{
				ExternalIp.Changed -= networkCard.OnExternal;
			};
		}
		Control Labelled(string text, Color color, Hero hero)
		{
			Border border = new Border
			{
				Width = 6.0,
				Height = 6.0,
				CornerRadius = new CornerRadius(3.0),
				Background = Palette.Brush(color),
				VerticalAlignment = VerticalAlignment.Center
			};
			return Ui.Stack(1.0, Ui.Row(5.0, border, Card.Label(text, p)), hero);
		}
	}

	private void OnExternal()
	{
		Dispatcher.UIThread.Post(ShowExternal);
	}

	private void ShowExternal()
	{
		if (externalValue != null && externalRow != null)
		{
			externalRow.IsVisible = ExternalIp.Busy || ExternalIp.Address != null || ExternalIp.Failed;
			TextBlock? textBlock = externalValue;
			object text;
			if (!ExternalIp.Busy)
			{
				string address = ExternalIp.Address;
				text = ((address != null) ? $"{address}  ·  {ExternalIp.At:HH:mm}" : L.T("Not reachable"));
			}
			else
			{
				text = L.T("Asking…");
			}
			textBlock.Text = (string?)text;
			externalValue.Foreground = Palette.Brush((ExternalIp.Address == null) ? P.Soft : P.Ink);
		}
	}

	public override void Update(Snapshot s, History h)
	{
		NetworkInfo network = s.Network;
		base.Subtitle = network?.Name ?? "";
		base.Aside = (Shows("net.link") ? Format.Link(network?.LinkBitsPerSecond) : "");
		var (value, unitText) = Format.Split(Format.Rate(network?.DownloadBytesPerSecond));
		var (value2, unitText2) = Format.Split(Format.Rate(network?.UploadBytesPerSecond));
		down.Set(value, unitText, P.Ink);
		up.Set(value2, unitText2, P.Ink);
		double[] source = h.Read("net.down");
		chart?.SetData(source, h.Read("net.up"));
		double num = Math.Max((network?.DownloadBytesPerSecond).GetValueOrDefault(), (network?.UploadBytesPerSecond).GetValueOrDefault()) * 8.0;
		Card.Put(stats, "net.use", ((object)network == null) ? "—" : Format.Bits(num));
		double num3;
		if (linkBar != null)
		{
			double? num2 = network?.LinkBitsPerSecond;
			if (num2.HasValue)
			{
				double valueOrDefault = num2.GetValueOrDefault();
				if (valueOrDefault > 0.0)
				{
					num3 = num / valueOrDefault;
					goto IL_01d9;
				}
			}
			num3 = 0.0;
			goto IL_01d9;
		}
		goto IL_0237;
		IL_01d9:
		double num4 = num3;
		linkBar.Set(num4, (num4 >= 0.9) ? P.Ramp[^1] : ((num4 >= 0.7) ? P.Ramp[2] : P.Cool));
		goto IL_0237;
		IL_0237:
		double[] array = source.Where(double.IsFinite).ToArray();
		Card.Put(stats, "net.peak", (array.Length == 0) ? "—" : Format.Rate(array.Max()));
		Card.Put(stats, "net.address", network?.Address ?? "—");
	}
}
