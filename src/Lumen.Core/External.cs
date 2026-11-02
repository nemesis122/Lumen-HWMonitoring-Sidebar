using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Lumen.Core;

public static class External
{
	private const StringComparison I = StringComparison.OrdinalIgnoreCase;

	private static readonly Regex AidaPattern = new Regex("<(\\w+)><id>(.*?)</id><label>(.*?)</label><value>(.*?)</value></\\1>", RegexOptions.Singleline);

	private static readonly Regex AidaGpu = new Regex("^[A-Z]GPU(\\d+)", RegexOptions.IgnoreCase);

	private static readonly Regex AidaCore = new Regex("^SCPU(\\d+)UTI$", RegexOptions.IgnoreCase);

	private static readonly Regex AidaCoreClock = new Regex("^SCC-\\d+-\\d+$", RegexOptions.IgnoreCase);

	private static readonly Regex AidaDisk = new Regex("^THDD(\\d+)$", RegexOptions.IgnoreCase);

	public const uint HwinfoSignature = 1399412552u;

	private static readonly Regex HwCoreUsage = new Regex("^(P-core|E-core|Core)\\s*(\\d+)(?:\\s*T(\\d+))?\\s+Usage$", RegexOptions.IgnoreCase);

	private static readonly Regex HwCoreClock = new Regex("^(P-core|E-core|Core)\\s*(\\d+)\\s+Clock(?:\\s*\\(.*\\))?$", RegexOptions.IgnoreCase);

	private static readonly Regex HwCoreVid = new Regex("^(P-core|E-core|Core)\\s*(\\d+)\\s+VID$", RegexOptions.IgnoreCase);

	private static readonly Regex HwGroup = new Regex("^(CPU|GPU)\\s*\\[#(\\d+)\\]", RegexOptions.IgnoreCase);

	public static double? Number(string? text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return null;
		}
		string text2 = text.Trim();
		if (!text2.Contains('.') && text2.Count((char c) => c == ',') == 1)
		{
			text2 = text2.Replace(',', '.');
		}
		if (!double.TryParse(text2, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) || !double.IsFinite(result))
		{
			return null;
		}
		return result;
	}

	public static List<AidaItem> ParseAida(string text)
	{
		return (from m in AidaPattern.Matches(text)
			select new AidaItem(m.Groups[1].Value, m.Groups[2].Value.Trim(), m.Groups[3].Value.Trim(), m.Groups[4].Value.Trim())).ToList();
	}

	public static List<SensorNode> AidaNodes(IReadOnlyList<AidaItem> items)
	{
		Dictionary<string, AidaItem> byId = new Dictionary<string, AidaItem>(StringComparer.OrdinalIgnoreCase);
		foreach (AidaItem item in items)
		{
			byId.TryAdd(item.Id, item);
		}
		List<SensorNode> list = new List<SensorNode>();
		List<SensorValue> list2 = new List<SensorValue>();
		Add(list2, "Temperature", "CPU Package", V(new string[3] { "TCPU", "TCPUPKG", "TCPUDIO" }));
		Add(list2, "Load", "CPU Total", V(new string[1] { "SCPUUTI" }));
		foreach (var item2 in from i in items
			select (i: i, m: AidaCore.Match(i.Id)) into x
			where x.m.Success && x.i.Value.HasValue
			orderby int.Parse(x.m.Groups[1].Value, CultureInfo.InvariantCulture)
			select x)
		{
			Add(list2, "Load", "Core #" + item2.Item2.Groups[1].Value, item2.Item1.Value, int.Parse(item2.Item2.Groups[1].Value, CultureInfo.InvariantCulture));
		}
		List<double> list3 = (from i in items.Where(delegate(AidaItem i)
			{
				if (AidaCoreClock.IsMatch(i.Id))
				{
					double? value2 = i.Value;
					if (value2.HasValue)
					{
						double valueOrDefault3 = value2.GetValueOrDefault();
						if (valueOrDefault3 > 50.0)
						{
							return valueOrDefault3 < 10000.0;
						}
					}
					return false;
				}
				return false;
			})
			select i.Value.Value).ToList();
		Add(list2, "Clock", "Cores (Average)", (list3.Count > 0) ? new double?(Math.Round(list3.Average())) : V(new string[1] { "SCPUCLK" }));
		Add(list2, "Power", "Package", V(new string[2] { "PCPUPKG", "PCPU" }));
		Add(list2, "Voltage", "Vcore", V(new string[1] { "VCPU" }));
		if (list2.Count > 0)
		{
			list.Add(new SensorNode("Cpu", "CPU", "aida/cpu", list2, Array.Empty<SensorNode>()));
		}
		foreach (int item3 in from n in (from i in items
				select AidaGpu.Match(i.Id) into m
				where m.Success
				select int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).Distinct()
			orderby n
			select n)
		{
			List<SensorValue> list4 = new List<SensorValue>();
			string p = item3.ToString(CultureInfo.InvariantCulture);
			Add(list4, "Temperature", "GPU Core", V(new string[2]
			{
				"TGPU" + p + "DIO",
				"TGPU" + p
			}));
			Add(list4, "Temperature", "GPU Hot Spot", items.FirstOrDefault((AidaItem i) => i.Category == "temp" && i.Id.StartsWith("TGPU" + p, StringComparison.OrdinalIgnoreCase) && i.Value.HasValue && (i.Id.Contains("HOT", StringComparison.OrdinalIgnoreCase) || i.Label.Contains("Hot", StringComparison.OrdinalIgnoreCase) || i.Label.Contains("Junction", StringComparison.OrdinalIgnoreCase)))?.Value);
			Add(list4, "Load", "GPU Core", V(new string[1] { "SGPU" + p + "UTI" }));
			Add(list4, "Clock", "GPU Core", V(new string[1] { "SGPU" + p + "CLK" }));
			Add(list4, "Power", "GPU Power", V(new string[1] { "PGPU" + p }));
			Add(list4, "Fan", "GPU Fan", V(new string[1] { "FGPU" + p }));
			double? value = V(new string[1] { "SGPU" + p + "USEDDEMEM" });
			double? num = V(new string[1] { "SGPU" + p + "FREEDEMEM" });
			Add(list4, "SmallData", "GPU Memory Used", value);
			if (value.HasValue)
			{
				double valueOrDefault = value.GetValueOrDefault();
				if (num.HasValue)
				{
					double valueOrDefault2 = num.GetValueOrDefault();
					Add(list4, "SmallData", "GPU Memory Total", valueOrDefault + valueOrDefault2);
				}
			}
			if (list4.Count > 0)
			{
				list.Add(new SensorNode("GpuNvidia", "GPU " + p, "aida/gpu/" + p, list4, Array.Empty<SensorNode>()));
			}
		}
		foreach (AidaItem item4 in items.Where((AidaItem i) => AidaDisk.IsMatch(i.Id) && i.Value.HasValue))
		{
			list.Add(new SensorNode("Storage", item4.Label, "aida/drive/" + AidaDisk.Match(item4.Id).Groups[1].Value, new SensorValue[1]
			{
				new SensorValue("Temperature", "Temperature", item4.Value, 0, "aida/" + item4.Id)
			}, Array.Empty<SensorNode>()));
		}
		List<SensorValue> list5 = new List<SensorValue>();
		foreach (AidaItem item5 in items.Where((AidaItem i) => i.Category == "temp" && i.Value.HasValue && !Regex.IsMatch(i.Id, "^T(CPU|CC|GPU|HDD)", RegexOptions.IgnoreCase)))
		{
			string text = item5.Id.ToUpperInvariant();
			string text2;
			switch (text)
			{
			case "TMOBO":
				text2 = "Motherboard";
				break;
			case "TCHIP":
			case "TPCH":
				text2 = "Chipset";
				break;
			default:
				text2 = ((!text.StartsWith("TVRM")) ? item5.Label : "VRM");
				break;
			}
			string name = text2;
			list5.Add(new SensorValue("Temperature", name, item5.Value, list5.Count, "aida/" + item5.Id));
		}
		int num2 = 0;
		foreach (AidaItem item6 in items.Where((AidaItem i) => i.Category == "fan" && i.Value.HasValue && !AidaGpu.IsMatch(i.Id)))
		{
			string name2 = ((item6.Id.Equals("FCPU", StringComparison.OrdinalIgnoreCase) && !item6.Label.Contains("CPU", StringComparison.OrdinalIgnoreCase)) ? "CPU Fan" : item6.Label);
			list5.Add(new SensorValue("Fan", name2, item6.Value, num2++, "aida/" + item6.Id));
		}
		if (list5.Count > 0)
		{
			list.Add(new SensorNode("Motherboard", "Mainboard", "aida/board", list5, Array.Empty<SensorNode>()));
		}
		return list;
		static void Add(List<SensorValue> list6, string type, string text3, double? num3, int index = 0)
		{
			if (num3.HasValue)
			{
				double valueOrDefault3 = num3.GetValueOrDefault();
				list6.Add(new SensorValue(type, text3, valueOrDefault3, index, "aida/" + type + "/" + text3));
			}
		}
		double? V(string[] ids)
		{
			foreach (string key in ids)
			{
				if (byId.TryGetValue(key, out AidaItem value2))
				{
					double? value3 = value2.Value;
					if (value3.HasValue)
					{
						return value3.GetValueOrDefault();
					}
				}
			}
			return null;
		}
	}

	private static string Ansi(ReadOnlySpan<byte> bytes)
	{
		int num = bytes.IndexOf<byte>(0);
		if (num >= 0)
		{
			bytes = bytes.Slice(0, num);
		}
		return Encoding.Latin1.GetString(bytes).Trim();
	}

	public static int? HwinfoSize(ReadOnlySpan<byte> header)
	{
		if (header.Length < 44 || BitConverter.ToUInt32(header.Slice(0, 4)) != 1399412552)
		{
			return null;
		}
		long val = BitConverter.ToUInt32(header.Slice(32, 4)) + (long)BitConverter.ToUInt32(header.Slice(36, 4)) * (long)BitConverter.ToUInt32(header.Slice(40, 4));
		long val2 = BitConverter.ToUInt32(header.Slice(20, 4)) + (long)BitConverter.ToUInt32(header.Slice(24, 4)) * (long)BitConverter.ToUInt32(header.Slice(28, 4));
		long num = Math.Max(val, val2);
		if (num <= 0 || num >= 67108864)
		{
			return null;
		}
		return (int)num;
	}

	public static List<HwinfoReading> ParseHwinfo(ReadOnlySpan<byte> data)
	{
		List<HwinfoReading> list = new List<HwinfoReading>();
		int? num = HwinfoSize(data);
		if (num.HasValue)
		{
			int valueOrDefault = num.GetValueOrDefault();
			if (data.Length >= valueOrDefault)
			{
				int num2 = U(data, 20);
				int num3 = U(data, 24);
				int num4 = U(data, 28);
				int num5 = U(data, 32);
				int num6 = U(data, 36);
				int num7 = U(data, 40);
				if (num3 < 136 || num6 < 316)
				{
					return list;
				}
				List<string> list2 = new List<string>();
				for (int i = 0; i < num4; i++)
				{
					list2.Add(Ansi(data.Slice(num2 + i * num3 + 8, 128)));
				}
				for (int j = 0; j < num7; j++)
				{
					ReadOnlySpan<byte> readOnlySpan = data.Slice(num5 + j * num6, num6);
					int num8 = (int)BitConverter.ToUInt32(readOnlySpan.Slice(0, 4));
					int num9 = (int)BitConverter.ToUInt32(readOnlySpan.Slice(4, 4));
					double num10 = BitConverter.ToDouble(readOnlySpan.Slice(284, 8));
					if (num8 != 0 && num9 >= 0 && num9 < list2.Count && double.IsFinite(num10))
					{
						list.Add(new HwinfoReading(list2[num9], num9, num8, Ansi(readOnlySpan.Slice(12, 128)), Ansi(readOnlySpan.Slice(268, 16)), num10));
					}
				}
				return list;
			}
		}
		return list;
		static int U(ReadOnlySpan<byte> d, int at)
		{
			return (int)BitConverter.ToUInt32(d.Slice(at, 4));
		}
	}

	public static string HwinfoName(string sensor)
	{
		int num = sensor.IndexOf(": ", StringComparison.Ordinal);
		string text;
		if (num < 0)
		{
			text = sensor;
		}
		else
		{
			int num2 = num + 2;
			text = sensor.Substring(num2, sensor.Length - num2);
		}
		string text2 = text;
		int num3 = text2.IndexOf(": ", StringComparison.Ordinal);
		if (num3 >= 0)
		{
			text2 = text2.Substring(0, num3);
		}
		text2 = Regex.Replace(text2, "\\s*\\([^()]*\\)\\s*$", "");
		return text2.Trim();
	}

	private static double Convert(HwinfoReading r)
	{
		switch (r.Unit.Trim())
		{
		case "°F":
			return (r.Value - 32.0) * 5.0 / 9.0;
		case "GB":
			if (r.Type == 8)
			{
				return r.Value * 1024.0;
			}
			break;
		case "KB/s":
			return r.Value * 1024.0;
		case "MB/s":
			return r.Value * 1024.0 * 1024.0;
		case "GB/s":
			return r.Value * 1024.0 * 1024.0 * 1024.0;
		case "GHz":
			return r.Value * 1000.0;
		}
		return r.Value;
	}

	public static List<SensorNode> HwinfoNodes(IReadOnlyList<HwinfoReading> readings)
	{
		List<SensorNode> list = new List<SensorNode>();
		List<IGrouping<string, HwinfoReading>> source = (from r in readings
			group r by r.Sensor).ToList();
		List<IGrouping<string, HwinfoReading>> list2 = source.Where((IGrouping<string, HwinfoReading> g) => g.Key.StartsWith("CPU [#0]", StringComparison.OrdinalIgnoreCase)).ToList();
		if (list2.Count > 0)
		{
			List<SensorValue> list3 = new List<SensorValue>();
			int num = 0;
			foreach (HwinfoReading item in list2.SelectMany((IGrouping<string, HwinfoReading> g) => g))
			{
				double value = Convert(item);
				string label = item.Label;
				(string, string)? tuple;
				switch (item.Type)
				{
				case 1:
					tuple = ((!label.Equals("CPU (Tctl/Tdie)", StringComparison.OrdinalIgnoreCase)) ? ((!label.Equals("CPU Package", StringComparison.OrdinalIgnoreCase)) ? ((!label.Equals("Core Max", StringComparison.OrdinalIgnoreCase)) ? ((!label.Equals("CPU Die (average)", StringComparison.OrdinalIgnoreCase)) ? new(string, string)?(("Temperature", label)) : new(string, string)?(("Temperature", "Core (Tdie)"))) : new(string, string)?(("Temperature", "Core Max"))) : new(string, string)?(("Temperature", "CPU Package"))) : new(string, string)?(("Temperature", "Core (Tctl/Tdie)")));
					break;
				case 7:
				{
					if (label.Equals("Total CPU Usage", StringComparison.OrdinalIgnoreCase))
					{
						tuple = ("Load", "CPU Total");
						break;
					}
					Match match3 = HwCoreUsage.Match(label);
					if (match3 != null && match3.Success && match3.Groups[3].Success)
					{
						int num2 = ++num;
						tuple = ("Load", "Thread #" + num2);
						break;
					}
					Match match4 = HwCoreUsage.Match(label);
					if (match4 != null && match4.Success)
					{
						tuple = ("Load", CoreName(match4.Groups[1].Value, match4.Groups[2].Value));
						break;
					}
					goto default;
				}
				case 6:
					if (label.StartsWith("Core Clocks", StringComparison.OrdinalIgnoreCase) && label.Contains("avg", StringComparison.OrdinalIgnoreCase))
					{
						tuple = ("Clock", "Cores (Average)");
						break;
					}
					if (!label.Contains("Effective", StringComparison.OrdinalIgnoreCase))
					{
						Match match2 = HwCoreClock.Match(label);
						if (match2 != null && match2.Success)
						{
							tuple = ("Clock", CoreName(match2.Groups[1].Value, match2.Groups[2].Value));
							break;
						}
					}
					goto default;
				case 5:
					if (label.Equals("CPU Package Power", StringComparison.OrdinalIgnoreCase))
					{
						tuple = ("Power", "Package");
						break;
					}
					if (label.Equals("CPU PPT", StringComparison.OrdinalIgnoreCase))
					{
						tuple = ("Power", "CPU PPT");
						break;
					}
					if (label.StartsWith("CPU Core Power", StringComparison.OrdinalIgnoreCase))
					{
						tuple = ("Power", "CPU Cores");
						break;
					}
					goto default;
				case 2:
				{
					if (label.StartsWith("CPU Core Voltage (SVI2", StringComparison.OrdinalIgnoreCase))
					{
						tuple = ("Voltage", "Core (SVI2 TFN)");
						break;
					}
					if (label.StartsWith("CPU Core Voltage (SVI3", StringComparison.OrdinalIgnoreCase))
					{
						tuple = ("Voltage", "Core (SVI3 TFN)");
						break;
					}
					if (label.Equals("Vcore", StringComparison.OrdinalIgnoreCase) || label.Equals("CPU Core Voltage", StringComparison.OrdinalIgnoreCase))
					{
						tuple = ("Voltage", "Vcore");
						break;
					}
					Match match = HwCoreVid.Match(label);
					if (match != null && match.Success)
					{
						tuple = ("Voltage", CoreName(match.Groups[1].Value, match.Groups[2].Value) + " VID");
						break;
					}
					goto default;
				}
				default:
					tuple = null;
					break;
				}
				(string, string)? tuple2 = tuple;
				if (tuple2.HasValue)
				{
					(string type, string name) x = tuple2.GetValueOrDefault();
					list3.Add(new SensorValue(x.type, x.name, value, list3.Count((SensorValue s) => s.Type == x.type), "hwinfo/cpu/" + label));
				}
			}
			string text = HwinfoName(list2[0].Key);
			list.Add(new SensorNode("Cpu", (text.Length > 0) ? text : "CPU", "hwinfo/cpu/0", list3, Array.Empty<SensorNode>()));
		}
		foreach (IGrouping<string, IGrouping<string, HwinfoReading>> item2 in from g in source.Where(delegate(IGrouping<string, HwinfoReading> g)
			{
				Match match5 = HwGroup.Match(g.Key);
				return match5 != null && match5.Success && match5.Groups[1].Value.Equals("GPU", StringComparison.OrdinalIgnoreCase);
			})
			group g by HwGroup.Match(g.Key).Groups[2].Value)
		{
			List<SensorValue> list4 = new List<SensorValue>();
			double? num3 = null;
			double? num4 = null;
			foreach (HwinfoReading item3 in item2.SelectMany((IGrouping<string, HwinfoReading> g) => g))
			{
				double value2 = Convert(item3);
				string label2 = item3.Label;
				(string, string)? tuple;
				switch (item3.Type)
				{
				case 1:
					tuple = ((!label2.Equals("GPU Temperature", StringComparison.OrdinalIgnoreCase)) ? ((!label2.Contains("Hot Spot", StringComparison.OrdinalIgnoreCase) && !label2.Contains("Hotspot", StringComparison.OrdinalIgnoreCase)) ? new(string, string)?(("Temperature", label2)) : new(string, string)?(("Temperature", "GPU Hot Spot"))) : new(string, string)?(("Temperature", "GPU Core")));
					break;
				case 7:
					if (label2.Equals("GPU Core Load", StringComparison.OrdinalIgnoreCase) || label2.Equals("GPU Utilization", StringComparison.OrdinalIgnoreCase))
					{
						tuple = ("Load", "GPU Core");
						break;
					}
					if (label2.Equals("GPU Memory Usage", StringComparison.OrdinalIgnoreCase))
					{
						tuple = ("Load", "GPU Memory");
						break;
					}
					goto default;
				case 6:
					if (label2.Equals("GPU Clock", StringComparison.OrdinalIgnoreCase) || label2.Equals("GPU Core Clock", StringComparison.OrdinalIgnoreCase))
					{
						tuple = ("Clock", "GPU Core");
						break;
					}
					goto default;
				case 5:
					if (label2.Equals("GPU Power", StringComparison.OrdinalIgnoreCase) || label2.Equals("GPU ASIC Power", StringComparison.OrdinalIgnoreCase) || label2.Equals("Total Graphics Power", StringComparison.OrdinalIgnoreCase) || label2.Equals("GPU PPT", StringComparison.OrdinalIgnoreCase))
					{
						tuple = ("Power", "GPU Power");
						break;
					}
					goto default;
				case 3:
					tuple = ("Fan", label2);
					break;
				case 8:
					if (label2.Equals("GPU Memory Allocated", StringComparison.OrdinalIgnoreCase))
					{
						tuple = ("SmallData", "GPU Memory Used");
						break;
					}
					if (label2.Equals("GPU Memory Available", StringComparison.OrdinalIgnoreCase))
					{
						tuple = ("SmallData", "GPU Memory Free");
						break;
					}
					if (label2.Equals("GPU D3D Memory Dedicated", StringComparison.OrdinalIgnoreCase))
					{
						tuple = ("SmallData", "D3D Dedicated Memory Used");
						break;
					}
					goto default;
				default:
					tuple = null;
					break;
				}
				(string, string)? tuple3 = tuple;
				if (tuple3.HasValue)
				{
					(string type, string name) x2 = tuple3.GetValueOrDefault();
					if (x2.name == "GPU Memory Used")
					{
						num3 = value2;
					}
					if (x2.name == "GPU Memory")
					{
						num4 = value2;
					}
					list4.Add(new SensorValue(x2.type, x2.name, value2, list4.Count((SensorValue s) => s.Type == x2.type), "hwinfo/gpu/" + label2));
				}
			}
			if (num3.HasValue)
			{
				double valueOrDefault = num3.GetValueOrDefault();
				if (num4.HasValue)
				{
					double valueOrDefault2 = num4.GetValueOrDefault();
					if (valueOrDefault2 > 1.0 && valueOrDefault2 <= 100.0 && !list4.Any(delegate(SensorValue s)
					{
						string name = s.Name;
						return (name == "GPU Memory Free" || name == "GPU Memory Total") ? true : false;
					}))
					{
						list4.Add(new SensorValue("SmallData", "GPU Memory Total", Math.Round(valueOrDefault / num4.Value * 100.0), 0, "hwinfo/gpu/total"));
					}
				}
			}
			string text2 = HwinfoName(item2.First().Key);
			string type = ((text2.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) || text2.Contains("GeForce", StringComparison.OrdinalIgnoreCase)) ? "GpuNvidia" : (text2.Contains("Intel", StringComparison.OrdinalIgnoreCase) ? "GpuIntel" : "GpuAmd"));
			list.Add(new SensorNode(type, text2, "hwinfo/gpu/" + item2.Key, list4, Array.Empty<SensorNode>()));
		}
		foreach (IGrouping<string, IGrouping<string, HwinfoReading>> item4 in from g in source
			where g.Key.StartsWith("S.M.A.R.T.", StringComparison.OrdinalIgnoreCase) || g.Key.StartsWith("Drive:", StringComparison.OrdinalIgnoreCase)
			group g by DriveName(g.Key))
		{
			List<SensorValue> list5 = new List<SensorValue>();
			foreach (HwinfoReading item5 in item4.SelectMany((IGrouping<string, HwinfoReading> g) => g))
			{
				int type2 = item5.Type;
				(string, string)? tuple;
				if (type2 == 1)
				{
					tuple = ((!item5.Label.StartsWith("Drive Temperature", StringComparison.OrdinalIgnoreCase) || item5.Label.Contains("2")) ? new(string, string)?(("Temperature", item5.Label)) : new(string, string)?(("Temperature", "Temperature")));
				}
				else
				{
					if (type2 != 8)
					{
						goto IL_0c01;
					}
					if (item5.Label.Equals("Read Rate", StringComparison.OrdinalIgnoreCase))
					{
						tuple = ("Throughput", "Read Rate");
					}
					else
					{
						if (!item5.Label.Equals("Write Rate", StringComparison.OrdinalIgnoreCase))
						{
							goto IL_0c01;
						}
						tuple = ("Throughput", "Write Rate");
					}
				}
				goto IL_0c0d;
				IL_0c0d:
				(string, string)? tuple4 = tuple;
				if (tuple4.HasValue)
				{
					(string type, string name) x3 = tuple4.GetValueOrDefault();
					list5.Add(new SensorValue(x3.type, x3.name, Convert(item5), list5.Count((SensorValue s) => s.Type == x3.type), "hwinfo/drive/" + item5.Label));
				}
				continue;
				IL_0c01:
				tuple = null;
				goto IL_0c0d;
			}
			if (list5.Count > 0)
			{
				list.Add(new SensorNode("Storage", item4.Key, "hwinfo/drive/" + item4.Key, list5, Array.Empty<SensorNode>()));
			}
		}
		string[] skip = new string[11]
		{
			"CPU [", "GPU [", "S.M.A.R.T.", "Drive:", "Network:", "Memory Timings", "System:", "Windows Hardware Errors", "Battery", "DIMM",
			"RTSS"
		};
		List<IGrouping<string, HwinfoReading>> list6 = source.Where((IGrouping<string, HwinfoReading> g) => !skip.Any((string s) => g.Key.StartsWith(s, StringComparison.OrdinalIgnoreCase)) && g.Any(delegate(HwinfoReading r)
		{
			int type3 = r.Type;
			return (type3 == 1 || type3 == 3) ? true : false;
		})).ToList();
		if (list6.Count > 0)
		{
			List<SensorNode> sub = list6.Select((IGrouping<string, HwinfoReading> g) => new SensorNode("SuperIO", g.Key, "hwinfo/board/" + g.Key, g.Where(delegate(HwinfoReading r)
			{
				int type3 = r.Type;
				return (type3 == 1 || type3 == 3) ? true : false;
			}).Select((HwinfoReading r, int i) => new SensorValue((r.Type == 1) ? "Temperature" : "Fan", r.Label, Convert(r), i, "hwinfo/board/" + g.Key + "/" + r.Label)).ToList(), Array.Empty<SensorNode>())).ToList();
			list.Add(new SensorNode("Motherboard", HwinfoName(list6[0].Key), "hwinfo/board", Array.Empty<SensorValue>(), sub));
		}
		return list;
	}

	public static string DriveName(string sensor)
	{
		int num = sensor.IndexOf(": ", StringComparison.Ordinal);
		string text;
		if (num < 0)
		{
			text = sensor;
		}
		else
		{
			int num2 = num + 2;
			text = sensor.Substring(num2, sensor.Length - num2);
		}
		return text.Trim();
	}

	private static string CoreName(string kind, string number)
	{
		int num = int.Parse(number, CultureInfo.InvariantCulture) + 1;
		string text = kind.ToLowerInvariant();
		if (!(text == "p-core"))
		{
			if (text == "e-core")
			{
				return "E-Core #" + num;
			}
			return "Core #" + num;
		}
		return "P-Core #" + num;
	}
}
