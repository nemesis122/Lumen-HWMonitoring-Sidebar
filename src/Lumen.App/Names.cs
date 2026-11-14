using System;
using System.Text.RegularExpressions;
using Lumen.Core;

namespace Lumen.App;

public static class Names
{
    public static string CompactDrive(string name)
    {
        string text = Clean(name);
        text = Regex.Replace(text, @"^(?:Samsung|Western Digital|WDC|WD|Seagate|Crucial|Kingston|SK hynix|Toshiba|Kioxia|Micron|Intel|Corsair|Sabrent|Solidigm|SanDisk|PNY|ADATA)\s+", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\b(?:SSD|HDD|NVMe|ATA|SCSI|Disk Device)\b", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"(\d)\s*(TB|GB)\b", "$1 $2", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\s{2,}", " ").Trim();
        if (text.Length == 0) text = Clean(name);
        return text.Length > 24 ? text[..15].TrimEnd() + "…" + text[^8..].TrimStart() : text;
    }

	public static string Clean(string? name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return "";
		}
		return Regex.Replace(Regex.Replace(Regex.Replace(name, "\\((R|TM|tm|r)\\)|®|™", ""), "\\s+\\d+-Core Processor$|\\s+Processor$|\\s+CPU\\s*@.*$", "").Replace("Ethernet Server Adapter", "Ethernet").Replace("Ethernet Controller", "Ethernet")
			.Replace("Ethernet Connection", "Ethernet"), "\\s{2,}", " ").Trim();
	}

	public static string Fan(string raw, Settings s)
	{
		string text = s.Label("fan:" + raw);
		if (text != null)
		{
			return text;
		}
		(string Word, string Number) tuple = SensorPicker.FanName(raw);
		string item = tuple.Word;
		string item2 = tuple.Number;
		string text2 = L.T(item);
		if (item2.Length <= 0)
		{
			// A fan named after a part that also has a temperature ("Chipset") says it is the fan.
			return text2 == "Chipset" ? "Chipset Fan" : text2;
		}
		return text2 + " " + item2;
	}

	public static string Temp(string raw, Settings s)
	{
		return s.Label("temp:" + raw) ?? L.T(raw);
	}

	public static string Short(string? name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return "";
		}
		string input = Regex.Replace(name, "\\((R|TM|tm|r)\\)|®|™", "");
		input = Regex.Replace(input, "\\s+(CPU\\s*)?@.*$|\\s+\\d+-Core Processor$|\\s+Processor$", "");
		string[] array = new string[9] { "AMD ", "NVIDIA GeForce ", "NVIDIA ", "Intel Core ", "Intel ", "ASUS ", "MSI ", "Gigabyte ", "ASRock " };
		foreach (string text in array)
		{
			if (input.StartsWith(text, StringComparison.OrdinalIgnoreCase))
			{
				string text2 = input;
				int length = text.Length;
				input = text2.Substring(length, text2.Length - length);
				break;
			}
		}
		input = Regex.Replace(input, "\\s{2,}", " ").Trim();
		return input.Replace("Ethernet Server Adapter", "Ethernet").Replace("Ethernet Controller", "Ethernet");
	}
}
