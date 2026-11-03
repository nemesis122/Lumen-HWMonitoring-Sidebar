using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Lumen.Core;

public sealed class Settings
{
	public const string StateColors = "state";

	public const string WindowsAccent = "windows";

	public const string MonoColors = "mono";

	public static readonly string[] Languages = new string[6] { "de", "en", "fr", "it", "es", "pt" };

	public static readonly string[] Fonts = new string[6] { "inter", "segoe", "segoe-variable", "bahnschrift", "bahnschrift-narrow", "mono" };

	public static readonly WidgetStyle[] Retired = new WidgetStyle[3]
	{
		WidgetStyle.Gauge,
		WidgetStyle.Columns,
		WidgetStyle.Dial
	};

	public static readonly JsonSerializerOptions Json = new JsonSerializerOptions
	{
		WriteIndented = true,
		Converters = { (JsonConverter)new JsonStringEnumConverter() }
	};

	public Edge Edge { get; set; }

	public bool Horizontal
	{
		get
		{
			Edge edge = Edge;
			if ((uint)(edge - 2) <= 1u)
			{
				return true;
			}
			return false;
		}
	}

	public double BarHeight { get; set; } = 300.0;

	public bool BarFullWidth { get; set; } = true;

	public int ColumnsInUse => Columns;

	public int Rows
	{
		get
		{
			if (!Horizontal)
			{
				return 1;
			}
			return Columns;
		}
	}

	public int Screen { get; set; }

	public double Width { get; set; } = 300.0;

	public Density Density { get; set; }

	public ThemeMode Theme { get; set; } = ThemeMode.Dark;

	public bool Glass { get; set; } = true;

	public bool HighContrast { get; set; } = true;

	public bool ShowNames { get; set; } = true;

	public bool SmoothTransitions { get; set; }

	public bool Modern { get; set; } = true;

	public double Opacity { get; set; } = 0.55;

	public double TextScale { get; set; } = 1.0;

	public string Colors { get; set; } = "state";

	public string Background { get; set; } = "graphite";

	public string Language { get; set; } = "auto";

	public string Font { get; set; } = "inter";

	public bool Topmost { get; set; }

	public bool ReserveSpace { get; set; }

	public bool Fahrenheit { get; set; }

	public int IntervalMs { get; set; } = 1000;

	public double TemperatureWarning { get; set; } = 85.0;

	public bool WarningPopup { get; set; } = true;

	public bool ShowIntegratedGpu { get; set; } = true;

	public SensorSource SensorSource { get; set; }

	public string WeatherPlace { get; set; } = "";

	public double? WeatherLatitude { get; set; }

	public double? WeatherLongitude { get; set; }

	public List<WidgetSlot> Widgets { get; set; } = DefaultWidgets();

	public List<string> HiddenValues { get; set; } = new List<string>();

	public int Columns { get; set; } = 1;

	public int Pages { get; set; } = 1;

	public List<string> PageNames { get; set; } = new List<string>();

	public bool HasPageNames => PageNames.Take(Pages).Any((string n) => n.Length > 0);

	public bool FullScreen { get; set; }

	public double PricePerKwh { get; set; } = 0.3;

	public string Currency { get; set; } = "CHF";

	public double BaseWatts { get; set; } = 50.0;

	public string PingTarget { get; set; } = "1.1.1.1";

	public List<string> WorldClocks { get; set; } = new List<string> { "America/New_York", "Asia/Tokyo" };

	public string Notes { get; set; } = "";

	public bool ClickThrough { get; set; }

	public Dictionary<string, string> Labels { get; set; } = new Dictionary<string, string>();

	public static IEnumerable<WidgetStyle> Selectable => from s in Enum.GetValues<WidgetStyle>()
		where !Retired.Contains(s)
		select s;

	public int ColumnCount
	{
		get
		{
			if (!Horizontal)
			{
				return Math.Max(1, Columns);
			}
			return Math.Max(1, Enumerable.Range(1, Pages).SelectMany((int pg) => Layout(null, pg)).Select(Units)
				.DefaultIfEmpty(1)
				.Max());
		}
	}

	public string PageName(int page)
	{
		if (page < 1 || page > PageNames.Count || PageNames[page - 1].Length <= 0)
		{
			return page.ToString();
		}
		return PageNames[page - 1];
	}

	public int EffectivePage(WidgetSlot slot) => Math.Clamp(slot.Page, 1, Pages);

	public IEnumerable<WidgetSlot> WidgetsOnPage(int page) =>
		Widgets.Where(w => w.Visible && EffectivePage(w) == page);

	public static bool VisibleByDefault(WidgetKind k)
	{
		switch (k)
		{
		case WidgetKind.Clock:
		case WidgetKind.Battery:
		case WidgetKind.Cpu:
		case WidgetKind.Gpu:
		case WidgetKind.Cooling:
		case WidgetKind.Memory:
		case WidgetKind.Drives:
		case WidgetKind.Network:
			return true;
		default:
			return false;
		}
	}

	public static bool HasStyles(WidgetKind k)
	{
		if ((uint)k <= 18u)
		{
			return true;
		}
		return false;
	}

	public static WidgetStyle Successor(WidgetStyle s)
	{
		switch (s)
		{
		case WidgetStyle.Gauge:
		case WidgetStyle.Dial:
			return WidgetStyle.Scale;
		case WidgetStyle.Columns:
			return WidgetStyle.FillBars;
		default:
			return s;
		}
	}

	public static List<WidgetSlot> DefaultWidgets()
	{
		return (from k in Enum.GetValues<WidgetKind>()
			select new WidgetSlot
			{
				Kind = k,
				Visible = VisibleByDefault(k)
			}).ToList();
	}

	public bool Shows(string value)
	{
		return !HiddenValues.Contains(value);
	}

	public string? Label(string key)
	{
		if (!Labels.TryGetValue(key, out string value) || !(value != "-"))
		{
			return null;
		}
		return value;
	}

	public bool Hidden(string key)
	{
		if (Labels.TryGetValue(key, out string value))
		{
			return value == "-";
		}
		return false;
	}

	public static string DefaultPath()
	{
		string text = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory, "lumen-settings.json");
		try
		{
			using (File.Open(text, FileMode.OpenOrCreate, FileAccess.ReadWrite))
			{
			}
			if (new FileInfo(text).Length == 0L)
			{
				File.Delete(text);
			}
			return text;
		}
		catch
		{
			string text2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lumen");
			Directory.CreateDirectory(text2);
			return Path.Combine(text2, "lumen-settings.json");
		}
	}

	public static Settings Load(string path)
	{
		try
		{
			return Normalize(JsonSerializer.Deserialize<Settings>(File.ReadAllText(path), Json) ?? new Settings());
		}
		catch
		{
			return new Settings();
		}
	}

	public void Save(string path)
	{
		string text = path + ".tmp";
		File.WriteAllText(text, JsonSerializer.Serialize(this, Json));
		File.Move(text, path, overwrite: true);
	}

	public static Settings Normalize(Settings s)
	{
		foreach (WidgetSlot item in s.Widgets ?? new List<WidgetSlot>())
		{
			if (item != null)
			{
				item.Style = Successor(item.Style);
			}
		}
		s.Width = (double.IsFinite(s.Width) ? Math.Clamp(s.Width, 240.0, 440.0) : 300.0);
		s.IntervalMs = Math.Clamp(s.IntervalMs, 500, 5000);
		s.TemperatureWarning = (double.IsFinite(s.TemperatureWarning) ? Math.Clamp(s.TemperatureWarning, 50.0, 110.0) : 85.0);
		s.Opacity = (double.IsFinite(s.Opacity) ? Math.Clamp(s.Opacity, 0.2, 1.0) : 0.55);
		s.TextScale = (double.IsFinite(s.TextScale) ? Math.Clamp(s.TextScale, 0.5, 2.0) : 1.0);
		s.Screen = Math.Max(0, s.Screen);
		s.Colors = (string.IsNullOrWhiteSpace(s.Colors) ? "state" : s.Colors.Trim());
		s.Background = (string.IsNullOrWhiteSpace(s.Background) ? "graphite" : s.Background.Trim().ToLowerInvariant());
		bool flag;
		switch (s.Colors)
		{
		case "state":
		case "windows":
		case "mono":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (!flag && !IsHexColor(s.Colors))
		{
			s.Colors = "state";
		}
		s.Language = (Languages.Contains(s.Language) ? s.Language : "auto");
		s.Font = (Fonts.Contains(s.Font?.Trim().ToLowerInvariant()) ? s.Font.Trim().ToLowerInvariant() : "inter");
		Settings settings = s;
		if (settings.WeatherPlace == null)
		{
			string text = (settings.WeatherPlace = "");
		}
		s.Columns = Math.Clamp(s.Columns, 1, 6);
		s.Pages = Math.Clamp(s.Pages, 1, 6);
		if (!Enum.IsDefined(s.Edge))
		{
			s.Edge = Edge.Right;
		}
		if (!Enum.IsDefined(s.SensorSource))
		{
			s.SensorSource = SensorSource.Builtin;
		}
		s.BarHeight = (double.IsFinite(s.BarHeight) ? Math.Clamp(s.BarHeight, 40.0, 700.0) : 300.0);
		s.PageNames = (from i in Enumerable.Range(0, 6)
			select (s.PageNames ?? new List<string>()).ElementAtOrDefault(i)?.Trim() ?? "" into n
			select (n.Length <= 16) ? n : n.Substring(0, 16)).ToList();
		s.PricePerKwh = (double.IsFinite(s.PricePerKwh) ? Math.Clamp(s.PricePerKwh, 0.0, 5.0) : 0.3);
		s.BaseWatts = (double.IsFinite(s.BaseWatts) ? Math.Clamp(s.BaseWatts, 0.0, 500.0) : 50.0);
		s.Currency = (string.IsNullOrWhiteSpace(s.Currency) ? "CHF" : s.Currency.Trim().Substring(0, Math.Min(5, s.Currency.Trim().Length)));
		s.PingTarget = (string.IsNullOrWhiteSpace(s.PingTarget) ? "1.1.1.1" : s.PingTarget.Trim());
		s.WorldClocks = (s.WorldClocks ?? new List<string>()).Where((string z) => !string.IsNullOrWhiteSpace(z)).Distinct().Take(4)
			.ToList();
		settings = s;
		if (settings.Notes == null)
		{
			string text = (settings.Notes = "");
		}
		s.Labels = new Dictionary<string, string>();
		s.HiddenValues = (from v in s.HiddenValues ?? new List<string>()
			where !string.IsNullOrWhiteSpace(v)
			select Regex.Replace(v, "^drive:.*?/(nvme|hdd|ssd|ata|storage|scsi|usb)[^/]*/(\\d+)$", "drive:/disk/$2")).Distinct().ToList();
		HashSet<WidgetKind> seen = new HashSet<WidgetKind>();
		s.Widgets = (s.Widgets ?? new List<WidgetSlot>()).Where((WidgetSlot w) => w != null && Enum.IsDefined(w.Kind) && seen.Add(w.Kind)).ToList();
		foreach (WidgetSlot widget in s.Widgets)
		{
			widget.Column = Math.Clamp(widget.Column, 0, 6);
			widget.Page = Math.Clamp(widget.Page, 1, 6);
			widget.Name = (widget.Name ?? "").Trim();
			widget.HardwareName = (widget.HardwareName ?? "").Trim();
			if (widget.Name.Length > 40)
			{
				widget.Name = widget.Name.Substring(0, 40);
			}
			if (widget.HardwareName.Length > 60)
			{
				widget.HardwareName = widget.HardwareName.Substring(0, 60);
			}
			if (!Enum.IsDefined(widget.Style) || !HasStyles(widget.Kind))
			{
				widget.Style = WidgetStyle.Standard;
			}
		}
		WidgetKind[] values = Enum.GetValues<WidgetKind>();
		foreach (WidgetKind widgetKind in values)
		{
			if (!seen.Add(widgetKind))
			{
				continue;
			}
			int index = 0;
			for (int num2 = 0; num2 < s.Widgets.Count; num2++)
			{
				if (s.Widgets[num2].Kind < widgetKind)
				{
					index = num2 + 1;
				}
			}
			s.Widgets.Insert(index, new WidgetSlot
			{
				Kind = widgetKind,
				Visible = VisibleByDefault(widgetKind)
			});
		}
		return s;
	}

	public static double Weight(WidgetSlot w)
	{
		double num;
		switch (w.Style)
		{
		case WidgetStyle.Ring:
		case WidgetStyle.Tile:
			num = 0.5;
			break;
		case WidgetStyle.Minimal:
			num = 0.35;
			break;
		case WidgetStyle.Bar:
			num = 0.45;
			break;
		case WidgetStyle.Graph:
			num = 0.7;
			break;
		case WidgetStyle.Compact:
			num = 0.32;
			break;
		case WidgetStyle.Status:
			num = 0.18;
			break;
		case WidgetStyle.Segments:
			num = 0.55;
			break;
		case WidgetStyle.DualRing:
			num = 0.5;
			break;
		case WidgetStyle.Timeline:
			num = 0.65;
			break;
		case WidgetStyle.Heatmap:
			num = 0.6;
			break;
		case WidgetStyle.Rings:
			num = 0.65;
			break;
		case WidgetStyle.BigNumber:
			num = 0.45;
			break;
		case WidgetStyle.Table:
			num = 0.6;
			break;
		case WidgetStyle.Pill:
			num = 0.32;
			break;
		case WidgetStyle.DotMatrix:
		case WidgetStyle.Bento:
		case WidgetStyle.MiniArc:
			num = 0.5;
			break;
		case WidgetStyle.Ticker:
			num = 0.3;
			break;
		case WidgetStyle.FillBars:
			num = 0.55;
			break;
		case WidgetStyle.Split:
			num = 0.45;
			break;
		case WidgetStyle.Chips:
			num = 0.18;
			break;
		case WidgetStyle.Scale:
			num = 0.45;
			break;
		case WidgetStyle.HistoryBars:
			num = 0.5;
			break;
		case WidgetStyle.Backdrop:
			num = 0.55;
			break;
		case WidgetStyle.PillDuo:
			num = 0.34;
			break;
		case WidgetStyle.PillRow:
			num = 0.45;
			break;
		case WidgetStyle.KeyFigures:
			num = 0.5;
			break;
		case WidgetStyle.BadgeList:
			num = 0.55;
			break;
		default:
			num = 1.0;
			break;
		}
		double num2 = num;
		double num3;
		switch (w.Kind)
		{
		case WidgetKind.Cpu:
		case WidgetKind.Gpu:
			num3 = 2.0;
			break;
		case WidgetKind.Calendar:
		case WidgetKind.Cooling:
		case WidgetKind.Network:
			num3 = 1.7;
			break;
		case WidgetKind.Weather:
		case WidgetKind.Processes:
			num3 = 1.4;
			break;
		case WidgetKind.Clock:
		case WidgetKind.Drives:
			num3 = 1.2;
			break;
		case WidgetKind.WorldClocks:
			num3 = 0.8;
			break;
		default:
			num3 = 1.05;
			break;
		}
		return num2 * num3;
	}

	public List<List<WidgetSlot>> Layout(Func<WidgetSlot, double>? weight = null, int page = 1, Func<WidgetSlot, int>? cards = null)
	{
		if (weight == null)
		{
			weight = Weight;
		}
		List<WidgetSlot> visible = WidgetsOnPage(page).ToList();
		if (Horizontal)
		{
			return BarRows(visible, cards ?? ((Func<WidgetSlot, int>)((WidgetSlot _) => 1)));
		}
		int n = ColumnsInUse;
		if (n < 2)
		{
			return new List<List<WidgetSlot>> { visible };
		}
		double[] array = new double[n];
		foreach (WidgetSlot item in visible.Where((WidgetSlot w) => Pinned(w) >= 0))
		{
			array[Pinned(item)] += weight(item);
		}
		List<WidgetSlot> list = visible.Where((WidgetSlot w) => Pinned(w) < 0).ToList();
		Dictionary<WidgetSlot, int> dictionary = new Dictionary<WidgetSlot, int>();
		if (n == 2)
		{
			int count = list.Count;
			if (count > 0 && count <= 16)
			{
				int num = 0;
				double num2 = double.MaxValue;
				double[] array2 = list.Select(weight).ToArray();
				for (int num3 = 0; num3 < 1 << list.Count; num3++)
				{
					double num4 = array[0];
					double num5 = array[1];
					for (int num6 = 0; num6 < list.Count; num6++)
					{
						if ((num3 & (1 << num6)) == 0)
						{
							num4 += array2[num6];
						}
						else
						{
							num5 += array2[num6];
						}
					}
					double num7 = Math.Max(num4, num5);
					if (num7 < num2 - 0.5)
					{
						num2 = num7;
						num = num3;
					}
				}
				for (int num8 = 0; num8 < list.Count; num8++)
				{
					dictionary[list[num8]] = (((num & (1 << num8)) != 0) ? 1 : 0);
				}
				goto IL_02a1;
			}
		}
		foreach (WidgetSlot item2 in list.OrderByDescending(weight))
		{
			int num9 = (dictionary[item2] = Array.IndexOf(array, array.Min()));
			array[num9] += weight(item2);
		}
		goto IL_02a1;
		IL_02a1:
		List<List<WidgetSlot>> list2 = (from _ in Enumerable.Range(0, n)
			select new List<WidgetSlot>()).ToList();
		foreach (WidgetSlot item3 in visible)
		{
			list2[(Pinned(item3) >= 0) ? Pinned(item3) : dictionary[item3]].Add(item3);
		}
		if (!visible.Any((WidgetSlot w) => Pinned(w) >= 0))
		{
			list2 = list2.OrderBy((List<WidgetSlot> c) => (c.Count != 0) ? visible.IndexOf(c[0]) : int.MaxValue).ToList();
		}
		return list2;
		int Pinned(WidgetSlot w)
		{
			if (w.Column <= 0)
			{
				return -1;
			}
			return Math.Min(w.Column, n) - 1;
		}
	}

	public static bool IsHalf(WidgetSlot w)
	{
		bool flag;
		switch (w.Style)
		{
		case WidgetStyle.Ring:
		case WidgetStyle.Tile:
		case WidgetStyle.DualRing:
		case WidgetStyle.BigNumber:
		case WidgetStyle.DotMatrix:
		case WidgetStyle.Bento:
		case WidgetStyle.MiniArc:
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (flag)
		{
			return HasStyles(w.Kind);
		}
		return false;
	}

	public static int Units(List<WidgetSlot> row)
	{
		int num = 0;
		for (int i = 0; i < row.Count; i++)
		{
			num++;
			if (IsHalf(row[i]) && i + 1 < row.Count && IsHalf(row[i + 1]))
			{
				i++;
			}
		}
		return num;
	}

	private List<List<WidgetSlot>> BarRows(List<WidgetSlot> visible, Func<WidgetSlot, int> cards)
	{
		int n = Math.Max(1, Columns);
		List<List<WidgetSlot>> list = new List<List<WidgetSlot>>();
		for (int i = 0; i < visible.Count; i++)
		{
			if (IsHalf(visible[i]) && i + 1 < visible.Count && IsHalf(visible[i + 1]) && Pinned(visible[i]) == Pinned(visible[i + 1]))
			{
				list.Add(new List<WidgetSlot>
				{
					visible[i],
					visible[i + 1]
				});
				i++;
			}
			else
			{
				list.Add(new List<WidgetSlot> { visible[i] });
			}
		}
		List<List<List<WidgetSlot>>> list2 = (from _ in Enumerable.Range(0, n)
			select new List<List<WidgetSlot>>()).ToList();
		foreach (List<WidgetSlot> item in list.Where((List<WidgetSlot> u) => Pinned(u[0]) >= 0))
		{
			list2[Pinned(item[0])].Add(item);
		}
		int total = ((IEnumerable<List<WidgetSlot>>)list).Sum((Func<List<WidgetSlot>, int>)Size);
		int[] array = (from num2 in Enumerable.Range(0, n)
			select total / n + ((num2 < total % n) ? 1 : 0)).ToArray();
		int[] array2 = list2.Select((List<List<WidgetSlot>> row) => ((IEnumerable<List<WidgetSlot>>)row).Sum((Func<List<WidgetSlot>, int>)Size)).ToArray();
		int num = 0;
		foreach (List<WidgetSlot> item2 in list.Where((List<WidgetSlot> u) => Pinned(u[0]) < 0))
		{
			for (; num < n - 1 && array2[num] + Size(item2) > array[num] && array2[num] > 0; num++)
			{
			}
			list2[num].Add(item2);
			array2[num] += Size(item2);
		}
		return list2.Select((List<List<WidgetSlot>> row) => row.OrderBy((List<WidgetSlot> u) => visible.IndexOf(u[0])).SelectMany((List<WidgetSlot> u) => u).ToList()).ToList();
		int Pinned(WidgetSlot w)
		{
			if (w.Column <= 0)
			{
				return -1;
			}
			return Math.Min(w.Column, n) - 1;
		}
		int Size(List<WidgetSlot> u)
		{
			if (u.Count != 2)
			{
				return Math.Max(1, cards(u[0]));
			}
			return 1;
		}
	}

	public static bool IsHexColor(string v)
	{
		if (v.Length == 7 && v[0] == '#')
		{
			return v.Substring(1, v.Length - 1).All(Uri.IsHexDigit);
		}
		return false;
	}
}
