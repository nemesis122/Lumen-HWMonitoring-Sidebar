using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Lumen.Core;

public static class SensorPicker
{
	public sealed record FanReading(string Key, string Name, double Rpm, double? Percent);

	public sealed record Nic(string Id, string Description, bool HasGateway, long BytesReceived);

	private const StringComparison I = StringComparison.OrdinalIgnoreCase;

	public static readonly Regex PerCore = new Regex("^(CPU |P-|E-)?Core #\\d+$", RegexOptions.IgnoreCase);

	public static readonly string[] BoardOrder = new string[4] { "Board", "VRM", "Chipset", "Coolant" };

	private static readonly string[] VirtualMarks = new string[27]
	{
		"Virtual", "Hyper-V", "vEthernet", "VPN", "TAP-", "Tunnel", "WireGuard", "Tailscale", "ZeroTier", "OpenVPN",
		"VMware", "VirtualBox", "Npcap", "Loopback", "WAN Miniport", "Bluetooth", "Kernel Debug", "Wintun", "Fortinet", "Cisco AnyConnect",
		"NordLynx", "ProtonVPN", "Hamachi", "Teredo", "isatap", "Pseudo", "Microsoft Wi-Fi Direct"
	};

	private static bool Ok(SensorValue s)
	{
		double? value = s.Value;
		if (value.HasValue)
		{
			double valueOrDefault = value.GetValueOrDefault();
			return double.IsFinite(valueOrDefault);
		}
		return false;
	}

	public static double? Find(IEnumerable<SensorValue> sensors, string type, params string[] names)
	{
		List<SensorValue> source = sensors.Where((SensorValue s) => s.Type == type && Ok(s)).ToList();
		foreach (string n in names)
		{
			SensorValue sensorValue = source.FirstOrDefault((SensorValue s) => s.Name.Trim().Equals(n, StringComparison.OrdinalIgnoreCase));
			if (sensorValue != null)
			{
				return sensorValue.Value;
			}
		}
		return null;
	}

	private static bool Contains(string name, params string[] parts)
	{
		return parts.Any((string p) => name.Contains(p, StringComparison.OrdinalIgnoreCase));
	}

	public static CpuInfo? Cpu(IReadOnlyList<SensorNode> hardware, double? systemLoad = null)
	{
		SensorNode sensorNode = hardware.FirstOrDefault((SensorNode x) => x.Type == "Cpu");
		if ((object)sensorNode == null)
		{
			return null;
		}
		List<SensorValue> list = sensorNode.All.ToList();
		List<SensorValue> source = hardware.Where((SensorNode x) => x.IsBoard).SelectMany((SensorNode x) => x.All).ToList();
		double? num = Find(source.Where(delegate(SensorValue s)
		{
			double? value = s.Value;
			if (value.HasValue)
			{
				double valueOrDefault = value.GetValueOrDefault();
				if (valueOrDefault > 0.0)
				{
					return valueOrDefault < 125.0;
				}
			}
			return false;
		}), "Temperature", "CPU", "CPU Socket", "CPU (PECI)");
		double? num2 = num;
		if (!num2.HasValue)
		{
			num = Find(list.Where(delegate(SensorValue s)
			{
				double? value = s.Value;
				if (value.HasValue)
				{
					double valueOrDefault = value.GetValueOrDefault();
					if (valueOrDefault > 0.0)
					{
						return valueOrDefault < 125.0;
					}
				}
				return false;
			}), "Temperature", "Core (Tctl/Tdie)", "CPU Package", "Package", "Core (Tdie)", "Core (Tctl)", "Core Max", "Core Average", "Tdie", "Tctl", "CCDs Max (Tdie)", "CCD1 (Tdie)", "CPU Cores", "CPU Core");
		}
		num2 = num;
		if (!num2.HasValue)
		{
			num = (from s in list.Where(delegate(SensorValue s)
				{
					if (s.Type == "Temperature")
					{
						double? value = s.Value;
						if (value.HasValue)
						{
							double valueOrDefault = value.GetValueOrDefault();
							if (valueOrDefault > 0.0 && valueOrDefault < 125.0)
							{
								return !Contains(s.Name, "Distance", "TjMax", "SoC");
							}
						}
					}
					return false;
				})
				select s.Value).Max();
		}
		num2 = num;
		if (!num2.HasValue)
		{
			num = Find(source.Where(delegate(SensorValue s)
			{
				double? value = s.Value;
				if (value.HasValue)
				{
					double valueOrDefault = value.GetValueOrDefault();
					if (valueOrDefault > 0.0)
					{
						return valueOrDefault < 125.0;
					}
				}
				return false;
			}), "Temperature", "CPU", "CPU Package", "CPU (PECI)", "CPU Core", "CPU Socket", "CPU Die");
		}
		num2 = num;
		if (!num2.HasValue)
		{
			num = (from s in source.Where(delegate(SensorValue s)
				{
					if (s.Type == "Temperature")
					{
						double? value = s.Value;
						if (value.HasValue)
						{
							double valueOrDefault = value.GetValueOrDefault();
							if (valueOrDefault > 0.0 && valueOrDefault < 125.0)
							{
								return s.Name.Contains("CPU", StringComparison.OrdinalIgnoreCase);
							}
						}
					}
					return false;
				})
				select s.Value).FirstOrDefault();
		}
		List<SensorValue> list2 = list.Where((SensorValue s) => s.Type == "Load" && s.Name.Contains("Thread #", StringComparison.OrdinalIgnoreCase) && Ok(s)).ToList();
		double[] array = (from s in (list2.Count > 0) ? list2 : list.Where((SensorValue s) => s.Type == "Load" && PerCore.IsMatch(s.Name.Trim()) && Ok(s)).ToList()
			orderby s.Index
			select Math.Clamp(s.Value.Value, 0.0, 100.0)).ToArray();
		double? load = Find(list, "Load", "CPU Total") ?? ((array.Length != 0) ? new double?(array.Average()) : ((double?)null)) ?? systemLoad;
		List<double> list3 = (from s in list.Where(delegate(SensorValue s)
			{
				if (s.Type == "Clock" && PerCore.IsMatch(s.Name.Trim()))
				{
					double? value = s.Value;
					if (value.HasValue)
					{
						double valueOrDefault = value.GetValueOrDefault();
						if (valueOrDefault > 50.0)
						{
							return valueOrDefault < 10000.0;
						}
					}
					return false;
				}
				return false;
			})
			select s.Value.Value).ToList();
		double? clockMhz = Find(list.Where((SensorValue s) => s.Value > 50.0), "Clock", "Cores (Average)") ?? ((list3.Count > 0) ? new double?(list3.Average()) : ((double?)null));
		double? powerW = Find(list.Where((SensorValue s) => s.Value > 0.0), "Power", "Package", "CPU Package", "CPU Cores", "Core (SVI2 TFN)", "CPU PPT") ?? (from s in list
			where s.Type == "Power" && s.Value > 0.0 && !Contains(s.Name, "#", "Graphics", "Memory", "Platform", "SoC")
			select s.Value).FirstOrDefault();
		List<SensorValue> list4 = list.Where(delegate(SensorValue s)
		{
			double? value = s.Value;
			if (value.HasValue)
			{
				double valueOrDefault = value.GetValueOrDefault();
				if (valueOrDefault > 0.3)
				{
					return valueOrDefault < 2.0;
				}
			}
			return false;
		}).ToList();
		double? voltage = Find(list4, "Voltage", "Core (SVI2 TFN)", "Core (SVI3 TFN)", "CPU Core", "Core", "CPU Cores", "Vcore") ?? (from s in list4
			where s.Type == "Voltage" && PerCore.IsMatch(s.Name.Trim())
			select s.Value).Max() ?? (from s in list4
			where s.Type == "Voltage" && s.Name.EndsWith(" VID", StringComparison.OrdinalIgnoreCase)
			select s.Value).Max() ?? (from s in source.Where(delegate(SensorValue s)
			{
				if (s.Type == "Voltage")
				{
					double? value = s.Value;
					if (value.HasValue)
					{
						double valueOrDefault = value.GetValueOrDefault();
						if (valueOrDefault > 0.3 && valueOrDefault < 2.0)
						{
							if (!Contains(s.Name, "Vcore", "CPU VCORE"))
							{
								return s.Name.Equals("CPU Core", StringComparison.OrdinalIgnoreCase);
							}
							return true;
						}
					}
				}
				return false;
			})
			select s.Value).FirstOrDefault();
		return new CpuInfo(sensorNode.Name.Trim(), num, load, array, clockMhz, powerW, voltage, CpuFan(hardware));
	}

	public static double? CpuFan(IReadOnlyList<SensorNode> hardware)
	{
		List<SensorValue> source = hardware.Where((SensorNode x) => x.IsBoard || x.Type == "Cooler").SelectMany((SensorNode x) => x.All).Where(delegate(SensorValue s)
		{
			if (s.Type == "Fan")
			{
				double? value = s.Value;
				if (value.HasValue)
				{
					double valueOrDefault = value.GetValueOrDefault();
					if (valueOrDefault > 0.0)
					{
						return valueOrDefault < 20000.0;
					}
				}
				return false;
			}
			return false;
		})
			.ToList();
		return (source.FirstOrDefault((SensorValue s) => s.Name.Contains("CPU", StringComparison.OrdinalIgnoreCase) && !s.Name.Contains("Opt", StringComparison.OrdinalIgnoreCase)) ?? source.FirstOrDefault((SensorValue s) => s.Name.Contains("CPU", StringComparison.OrdinalIgnoreCase)) ?? source.FirstOrDefault((SensorValue s) => Contains(s.Name, "Pump", "AIO")) ?? source.FirstOrDefault((SensorValue s) => !Contains(s.Name, "Chipset", "PCH", "VRM", "Power")))?.Value;
	}

	public static string? BoardLabel(string name)
	{
		string text = name.Trim();
		if (Contains(text, "Water", "Liquid", "Coolant"))
		{
			return "Coolant";
		}
		if (Contains(text, "Chipset", "PCH", "Southbridge", "Northbridge"))
		{
			return "Chipset";
		}
		if (Contains(text, "VRM", "MOS", "VSoC", "VCore"))
		{
			return "VRM";
		}
		if (Contains(text, "Motherboard", "Mainboard", "System") || text.Equals("Board", StringComparison.OrdinalIgnoreCase))
		{
			return "Board";
		}
		return null;
	}

	public static IReadOnlyList<TempReading> BoardTemps(SensorNode? board)
	{
		if ((object)board == null)
		{
			return Array.Empty<TempReading>();
		}
		Dictionary<string, double> picked = new Dictionary<string, double>();
		foreach (SensorValue item in board.All.Where(delegate(SensorValue s)
		{
			if (s.Type == "Temperature")
			{
				double? value2 = s.Value;
				if (value2.HasValue)
				{
					double valueOrDefault = value2.GetValueOrDefault();
					if (valueOrDefault >= 15.0)
					{
						return valueOrDefault <= 115.0;
					}
				}
				return false;
			}
			return false;
		}))
		{
			string text = BoardLabel(item.Name);
			if (text == null || (text != "Coolant" && item.Value < 22.0))
			{
				continue;
			}
			if (picked.TryGetValue(text, out var value))
			{
				if (text == "Coolant")
				{
					picked[text] = Math.Max(value, item.Value.Value);
				}
			}
			else
			{
				picked[text] = item.Value.Value;
			}
		}
		return (from k in BoardOrder.Where(picked.ContainsKey)
			select new TempReading(k, picked[k])).ToList();
	}

	public static IReadOnlyList<FanReading> Fans(IReadOnlyList<SensorNode> hardware)
	{
		List<FanReading> list = new List<FanReading>();
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (SensorNode item in hardware.Where((SensorNode x) => x.IsBoard || x.Type == "Cooler"))
		{
			foreach (SensorNode item2 in new SensorNode[1] { item }.Concat(item.Sub))
			{
				foreach (SensorValue s in item2.Sensors.Where((SensorValue sensorValue2) => sensorValue2.Type == "Fan" && Ok(sensorValue2)))
				{
					double? value = s.Value;
					bool flag;
					if (value.HasValue)
					{
						double valueOrDefault = value.GetValueOrDefault();
						if (valueOrDefault < 0.0 || valueOrDefault > 20000.0)
						{
							flag = true;
							goto IL_0115;
						}
					}
					flag = false;
					goto IL_0115;
					IL_0115:
					if (flag)
					{
						continue;
					}
					string name = s.Name.Trim();
					if (hashSet.Add(name))
					{
						SensorValue sensorValue = item2.Sensors.FirstOrDefault((SensorValue c) => c.Type == "Control" && Ok(c) && ((c.Index == s.Index && c.Name.Trim().Equals(name, StringComparison.OrdinalIgnoreCase)) || c.Name.Trim().Equals(name, StringComparison.OrdinalIgnoreCase))) ?? item2.Sensors.FirstOrDefault((SensorValue c) => c.Type == "Control" && Ok(c) && c.Index == s.Index);
						string key = ((s.Id.Length > 0) ? s.Id : (item2.Name + "/" + name));
						string name2 = name;
						double value2 = s.Value.Value;
						value = sensorValue?.Value;
						double? percent;
						if (value.HasValue)
						{
							double valueOrDefault2 = value.GetValueOrDefault();
							percent = Math.Clamp(valueOrDefault2, 0.0, 100.0);
						}
						else
						{
							percent = null;
						}
						list.Add(new FanReading(key, name2, value2, percent));
					}
				}
			}
		}
		return list;
	}

	public static bool IsIntegrated(SensorNode h)
	{
		if (!(h.Type == "GpuIntel") || h.Name.Contains("Arc A", StringComparison.OrdinalIgnoreCase) || h.Name.Contains("Arc(TM) A", StringComparison.OrdinalIgnoreCase) || Regex.IsMatch(h.Name, "Arc.*\\bB\\d{3}\\b", RegexOptions.IgnoreCase))
		{
			if (h.Type == "GpuAmd")
			{
				return Regex.IsMatch(h.Name, "Radeon(\\(TM\\))? (Graphics|Vega)|Radeon \\d{3}M|Radeon\\(TM\\) \\d{3}M", RegexOptions.IgnoreCase);
			}
			return false;
		}
		return true;
	}

	public static GpuInfo Gpu(SensorNode h)
	{
		List<SensorValue> list = h.All.ToList();
		bool flag = IsIntegrated(h);
		double? temperature = Find(list, "Temperature", "GPU Core", "GPU Temperature", "GPU") ?? (from s in list.Where(delegate(SensorValue s)
			{
				if (s.Type == "Temperature")
				{
					double? value = s.Value;
					if (value.HasValue)
					{
						double valueOrDefault5 = value.GetValueOrDefault();
						if (valueOrDefault5 > 0.0 && valueOrDefault5 < 125.0)
						{
							return !Contains(s.Name, "Hot", "Junction", "Memory", "VR", "Mem");
						}
					}
				}
				return false;
			})
			select s.Value).FirstOrDefault();
		double? hotSpot = Find(list, "Temperature", "GPU Hot Spot", "GPU Hotspot", "GPU Junction");
		double? load = Find(list, "Load", "GPU Core", "GPU Render/Compute", "D3D 3D", "GPU", "D3D Graphics_1");
		double? clockMhz = Find(list.Where((SensorValue s) => s.Value > 0.0), "Clock", "GPU Core", "GPU Shader", "GPU Render", "GPU");
		double? powerW = Find(list.Where((SensorValue s) => s.Value >= 0.0), "Power", "GPU Package", "GPU Power", "GPU Total", "GPU PPT", "GPU Core", "GPU") ?? (from s in list
			where s.Type == "Power" && s.Value > 0.0
			select s.Value).FirstOrDefault();
		List<double> list2 = (from s in list.Where(delegate(SensorValue s)
			{
				if (s.Type == "Fan")
				{
					double? value = s.Value;
					if (value.HasValue)
					{
						double valueOrDefault5 = value.GetValueOrDefault();
						if (valueOrDefault5 >= 0.0)
						{
							return valueOrDefault5 < 20000.0;
						}
					}
					return false;
				}
				return false;
			})
			select s.Value.Value).ToList();
		double? fanRpm = ((list2.Count > 0) ? new double?(list2.Max()) : ((double?)null));
		double? num = Find(list, "SmallData", "GPU Memory Used", "D3D Dedicated Memory Used");
		double? memoryTotalMb = Find(list.Where((SensorValue s) => s.Value > 0.0), "SmallData", "GPU Memory Total", "D3D Dedicated Memory Total");
		bool flag2 = flag;
		if (flag2)
		{
			bool flag3 = ((!memoryTotalMb.HasValue || memoryTotalMb.GetValueOrDefault() < 1024.0) ? true : false);
			flag2 = flag3;
		}
		if (flag2)
		{
			double? num2 = Find(list.Where((SensorValue s) => s.Value > 0.0), "SmallData", "D3D Shared Memory Total");
			if (num2.HasValue)
			{
				double valueOrDefault = num2.GetValueOrDefault();
				memoryTotalMb = valueOrDefault;
				num2 = Find(list, "SmallData", "D3D Shared Memory Used");
				double? num3;
				if (num2.HasValue)
				{
					double valueOrDefault2 = num2.GetValueOrDefault();
					num3 = valueOrDefault2 + num.GetValueOrDefault();
				}
				else
				{
					num3 = num;
				}
				num = num3;
			}
		}
		if (!memoryTotalMb.HasValue && num.HasValue)
		{
			double valueOrDefault3 = num.GetValueOrDefault();
			double? num2 = Find(list, "SmallData", "GPU Memory Free");
			if (num2.HasValue)
			{
				double valueOrDefault4 = num2.GetValueOrDefault();
				memoryTotalMb = valueOrDefault3 + valueOrDefault4;
			}
		}
		return new GpuInfo(h.Id, h.Name.Trim(), temperature, hotSpot, load, clockMhz, powerW, fanRpm, num, memoryTotalMb)
		{
			Integrated = flag,
			MemoryJunction = Find(list, "Temperature", "GPU Memory Junction", "Memory Junction", "GPU Memory Junction Temperature", "GPU Memory")
		};
	}

	public static DiskInfo Disk(SensorNode h)
	{
		List<SensorValue> list = h.All.ToList();
		double? temperature = Find(list.Where(delegate(SensorValue s)
		{
			double? value = s.Value;
			if (value.HasValue)
			{
				double valueOrDefault = value.GetValueOrDefault();
				if (valueOrDefault > 0.0)
				{
					return valueOrDefault < 100.0;
				}
			}
			return false;
		}), "Temperature", "Composite Temperature", "Temperature", "Airflow Temperature", "Temperature #1") ?? (from s in list.Where(delegate(SensorValue s)
			{
				if (s.Type == "Temperature")
				{
					double? value = s.Value;
					if (value.HasValue)
					{
						double valueOrDefault = value.GetValueOrDefault();
						if (valueOrDefault > 0.0 && valueOrDefault < 100.0)
						{
							return !Contains(s.Name, "Warning", "Critical", "Threshold", "Limit");
						}
					}
				}
				return false;
			})
			select s.Value).FirstOrDefault();
		double? used = Find(list, "Level", "Percentage Used");
		return new DiskInfo(h.Id, h.Name.Trim(), temperature, Find(list, "Load", "Used Space"), Find(list, "Throughput", "Read Rate"), Find(list, "Throughput", "Write Rate"))
		{
			ReadActivity = Find(list, "Load", "Read Activity"),
			WriteActivity = Find(list, "Load", "Write Activity"),
			TotalActivity = Find(list, "Load", "Total Activity"),
			PowerOnHours = Any(list, "Power On Hours", "Power-On Hours", "Power On Time"),
			PowerOnCount = Any(list, "Power On Count", "Power Cycle Count", "Power Cycles"),
			Life = Find(list, "Level", "Remaining Life", "Life") ?? (used.HasValue ? Math.Max(0.0, 100.0 - used.Value) : null)
		};
	}

	// A reading whose type differs between drive kinds, found by name alone.
	private static double? Any(IEnumerable<SensorValue> sensors, params string[] names)
	{
		foreach (string n in names)
		{
			SensorValue? s = sensors.FirstOrDefault((SensorValue x) => Ok(x) && x.Name.Trim().Equals(n, StringComparison.OrdinalIgnoreCase));
			if (s != null)
			{
				return s.Value;
			}
		}
		return null;
	}

	public static bool HasReadings(DiskInfo d)
	{
		if (!d.Temperature.HasValue && !d.UsedPercent.HasValue && !d.ReadBytesPerSecond.HasValue)
		{
			return d.WriteBytesPerSecond.HasValue;
		}
		return true;
	}

	public static int? DiskNumber(string id)
	{
		Match match = Regex.Match(id, "/(disk|nvme|hdd|ssd|ata|storage|scsi|usb)[^/]*/(\\d+)", RegexOptions.IgnoreCase);
		if (!match.Success || !int.TryParse(match.Groups[2].Value, out var result))
		{
			return null;
		}
		return result;
	}

	public static bool IsVirtualAdapter(string description)
	{
		return VirtualMarks.Any((string m) => description.Contains(m, StringComparison.OrdinalIgnoreCase));
	}

	public static Nic? Network(IReadOnlyList<Nic> up, string? current)
	{
		Nic nic = up.FirstOrDefault((Nic n) => n.Id == current);
		if ((object)nic != null)
		{
			return nic;
		}
		return (from n in up
			where n.HasGateway && !IsVirtualAdapter(n.Description)
			orderby n.BytesReceived descending
			select n).FirstOrDefault() ?? (from n in up
			where n.HasGateway
			orderby n.BytesReceived descending
			select n).FirstOrDefault() ?? (from n in up
			where !IsVirtualAdapter(n.Description)
			orderby n.BytesReceived descending
			select n).FirstOrDefault() ?? up.FirstOrDefault();
	}

	public static (string Word, string Number) FanName(string raw)
	{
		string text = raw.Trim();
		bool num = text.EndsWith("/ Pump", StringComparison.OrdinalIgnoreCase);
		if (num)
		{
			text = text.Substring(0, text.LastIndexOf('/')).Trim();
		}
		Match match = Regex.Match(text, "^(.*?)\\s*#?\\s*(\\d+)$");
		string item = (match.Success ? match.Groups[2].Value : "");
		string input = (match.Success ? match.Groups[1].Value.Trim() : text);
		input = Regex.Replace(input, "\\s*\\bFan\\b\\s*$", "", RegexOptions.IgnoreCase).Trim();
		input = Regex.Replace(input, "\\bOptional\\b", "Opt", RegexOptions.IgnoreCase);
		if (num)
		{
			input = "Pump";
		}
		if (input.Equals("Power", StringComparison.OrdinalIgnoreCase))
		{
			input = "Power Fan";
		}
		if (input.Length == 0)
		{
			input = "Fan";
		}
		return (Word: input, Number: item);
	}

	public static List<SensorNode> ParseReport(string text, string section = "LibreHardwareMonitor")
	{
		List<SensorNode> roots = new List<SensorNode>();
		List<(int indent, string type, string name, string id, List<SensorValue> sensors, List<SensorNode> sub)> stack = new List<(int, string, string, string, List<SensorValue>, List<SensorNode>)>();
		bool flag = false;
		Dictionary<string, int> dictionary = new Dictionary<string, int>();
		string[] array = text.Replace("\r", "").Split('\n');
		foreach (string text2 in array)
		{
			if (text2.StartsWith("==="))
			{
				Close(0);
				flag = text2.Contains(section, StringComparison.OrdinalIgnoreCase);
			}
			else
			{
				if (!flag || text2.Trim().Length == 0)
				{
					continue;
				}
				int indent = text2.Length - text2.TrimStart().Length;
				string input = text2.Trim();
				Match match = Regex.Match(input, "^\\[(\\w+)\\]\\s+(.*?)\\s*(?:\\(([^()]*/[^()]*)\\))?$");
				if (match.Success)
				{
					Close(stack.Count<(int, string, string, string, List<SensorValue>, List<SensorNode>)>(((int indent, string type, string name, string id, List<SensorValue> sensors, List<SensorNode> sub) s) => s.indent < indent));
					string item = (match.Groups[3].Success ? match.Groups[3].Value : ("/" + match.Groups[1].Value.ToLowerInvariant() + "/" + roots.Count + "/" + stack.Count));
					stack.Add((indent, match.Groups[1].Value, match.Groups[2].Value, item, new List<SensorValue>(), new List<SensorNode>()));
				}
				else if (stack.Count != 0)
				{
					Match match2 = Regex.Match(input, "^(\\w+)\\s+(.+?)\\s+(—|-?[\\d.]+(?:E[+-]?\\d+)?)$");
					if (match2.Success)
					{
						List<(int indent, string type, string name, string id, List<SensorValue> sensors, List<SensorNode> sub)> list = stack;
						(int, string, string, string, List<SensorValue>, List<SensorNode>) tuple = list[list.Count - 1];
						string value = match2.Groups[1].Value;
						string value2 = match2.Groups[2].Value;
						double? value3 = ((match2.Groups[3].Value == "—") ? ((double?)null) : new double?(double.Parse(match2.Groups[3].Value, CultureInfo.InvariantCulture)));
						string key = tuple.Item4 + "|" + value;
						int value4;
						int num = (dictionary.TryGetValue(key, out value4) ? value4 : 0);
						dictionary[key] = num + 1;
						tuple.Item5.Add(new SensorValue(value, value2, value3, num, tuple.Item4 + "/" + value.ToLowerInvariant() + "/" + num));
					}
				}
			}
		}
		Close(0);
		return roots;
		void Close(int depth)
		{
			while (stack.Count > depth)
			{
				List<(int indent, string type, string name, string id, List<SensorValue> sensors, List<SensorNode> sub)> list2 = stack;
				(int, string, string, string, List<SensorValue>, List<SensorNode>) tuple2 = list2[list2.Count - 1];
				stack.RemoveAt(stack.Count - 1);
				SensorNode item2 = new SensorNode(tuple2.Item2, tuple2.Item3, tuple2.Item4, tuple2.Item5, tuple2.Item6);
				if (stack.Count == 0)
				{
					roots.Add(item2);
				}
				else
				{
					List<(int indent, string type, string name, string id, List<SensorValue> sensors, List<SensorNode> sub)> list3 = stack;
					list3[list3.Count - 1].sub.Add(item2);
				}
			}
		}
	}
}
