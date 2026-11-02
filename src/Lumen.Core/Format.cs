using System;
using System.Globalization;

namespace Lumen.Core;

public static class Format
{
	private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

	public const string Missing = "—";

	public static string Number(double? value, string format = "0")
	{
		if (value.HasValue)
		{
			double valueOrDefault = value.GetValueOrDefault();
			if (double.IsFinite(valueOrDefault))
			{
				return valueOrDefault.ToString(format, Invariant);
			}
		}
		return "—";
	}

	public static string Temperature(double? celsius, bool fahrenheit)
	{
		if (celsius.HasValue)
		{
			double valueOrDefault = celsius.GetValueOrDefault();
			if (double.IsFinite(valueOrDefault))
			{
				return (fahrenheit ? (valueOrDefault * 1.8 + 32.0) : valueOrDefault).ToString("0", Invariant);
			}
		}
		return "—";
	}

	public static string TemperatureUnit(bool fahrenheit)
	{
		if (!fahrenheit)
		{
			return "°C";
		}
		return "°F";
	}

	public static string Percent(double? value)
	{
		if (value.HasValue)
		{
			double valueOrDefault = value.GetValueOrDefault();
			if (double.IsFinite(valueOrDefault))
			{
				return Math.Clamp(valueOrDefault, 0.0, 100.0).ToString("0", Invariant) + " %";
			}
		}
		return "—";
	}

	public static string Clock(double? mhz)
	{
		if (mhz.HasValue)
		{
			double valueOrDefault = mhz.GetValueOrDefault();
			if (double.IsFinite(valueOrDefault) && valueOrDefault > 0.0)
			{
				if (!(valueOrDefault >= 1000.0))
				{
					return valueOrDefault.ToString("0", Invariant) + " MHz";
				}
				return (valueOrDefault / 1000.0).ToString("0.00", Invariant) + " GHz";
			}
		}
		return "—";
	}

	public static string Watts(double? w)
	{
		if (w.HasValue)
		{
			double valueOrDefault = w.GetValueOrDefault();
			if (double.IsFinite(valueOrDefault))
			{
				return valueOrDefault.ToString((valueOrDefault < 10.0) ? "0.0" : "0", Invariant) + " W";
			}
		}
		return "—";
	}

	public static string Volts(double? v)
	{
		if (v.HasValue)
		{
			double valueOrDefault = v.GetValueOrDefault();
			if (double.IsFinite(valueOrDefault) && valueOrDefault > 0.0)
			{
				return valueOrDefault.ToString("0.000", Invariant) + " V";
			}
		}
		return "—";
	}

	public static string Rpm(double? r)
	{
		if (r.HasValue)
		{
			double valueOrDefault = r.GetValueOrDefault();
			if (double.IsFinite(valueOrDefault))
			{
				return valueOrDefault.ToString("0", Invariant) + " rpm";
			}
		}
		return "—";
	}

	public static string Gigabytes(double? gb)
	{
		if (gb.HasValue)
		{
			double valueOrDefault = gb.GetValueOrDefault();
			if (double.IsFinite(valueOrDefault))
			{
				return ((valueOrDefault >= 100.0) ? valueOrDefault.ToString("0", Invariant) : valueOrDefault.ToString("0.0", Invariant)) + " GB";
			}
		}
		return "—";
	}

	public static string Rate(double? bytesPerSecond)
	{
		if (bytesPerSecond.HasValue)
		{
			double valueOrDefault = bytesPerSecond.GetValueOrDefault();
			if (double.IsFinite(valueOrDefault))
			{
				valueOrDefault = Math.Max(0.0, valueOrDefault);
				string[] array = new string[4] { "B/s", "KB/s", "MB/s", "GB/s" };
				int num = 0;
				while (valueOrDefault >= 1024.0 && num < array.Length - 1)
				{
					valueOrDefault /= 1024.0;
					num++;
				}
				return ((num > 0 && valueOrDefault < 10.0) ? valueOrDefault.ToString("0.0", Invariant) : valueOrDefault.ToString("0", Invariant)) + " " + array[num];
			}
		}
		return "—";
	}

	public static (string Number, string Unit) Split(string text)
	{
		int num = text.LastIndexOf(' ');
		if (num >= 0)
		{
			string item = text.Substring(0, num);
			int num2 = num + 1;
			return (Number: item, Unit: text.Substring(num2, text.Length - num2));
		}
		return (Number: text, Unit: "");
	}

	public static string Link(double? bitsPerSecond)
	{
		if (bitsPerSecond.HasValue)
		{
			double valueOrDefault = bitsPerSecond.GetValueOrDefault();
			if (double.IsFinite(valueOrDefault) && !(valueOrDefault <= 0.0))
			{
				if (!(valueOrDefault >= 1000000000.0))
				{
					return (valueOrDefault / 1000000.0).ToString("0", Invariant) + " Mbit/s";
				}
				return (valueOrDefault / 1000000000.0).ToString((valueOrDefault % 1000000000.0 == 0.0) ? "0" : "0.#", Invariant) + " Gbit/s";
			}
		}
		return "—";
	}

	public static string Bits(double? bitsPerSecond)
	{
		if (bitsPerSecond.HasValue)
		{
			double valueOrDefault = bitsPerSecond.GetValueOrDefault();
			if (double.IsFinite(valueOrDefault))
			{
				valueOrDefault = Math.Max(0.0, valueOrDefault);
				if (valueOrDefault >= 1000000000.0)
				{
					return (valueOrDefault / 1000000000.0).ToString((valueOrDefault >= 10000000000.0) ? "0.0" : "0.00", Invariant) + " Gbit/s";
				}
				if (valueOrDefault >= 1000000.0)
				{
					return (valueOrDefault / 1000000.0).ToString((valueOrDefault >= 100000000.0) ? "0" : "0.0", Invariant) + " Mbit/s";
				}
				return (valueOrDefault / 1000.0).ToString("0", Invariant) + " kbit/s";
			}
		}
		return "—";
	}

	public static string Size(double? bytes)
	{
		if (bytes.HasValue)
		{
			double valueOrDefault = bytes.GetValueOrDefault();
			if (double.IsFinite(valueOrDefault))
			{
				string[] array = new string[5] { "B", "KB", "MB", "GB", "TB" };
				int num = 0;
				valueOrDefault = Math.Max(0.0, valueOrDefault);
				while (valueOrDefault >= 1024.0 && num < array.Length - 1)
				{
					valueOrDefault /= 1024.0;
					num++;
				}
				return valueOrDefault.ToString((num == 0 || valueOrDefault >= 100.0) ? "0" : ((valueOrDefault >= 10.0) ? "0.0" : "0.00"), Invariant) + " " + array[num];
			}
		}
		return "—";
	}

	public static string Capacity(double? gb)
	{
		if (gb.HasValue)
		{
			double valueOrDefault = gb.GetValueOrDefault();
			if (double.IsFinite(valueOrDefault))
			{
				if (!(valueOrDefault >= 1000.0))
				{
					return valueOrDefault.ToString((valueOrDefault >= 100.0) ? "0" : "0.0", Invariant) + " GB";
				}
				return (valueOrDefault / 1024.0).ToString("0.00", Invariant) + " TB";
			}
		}
		return "—";
	}

	public static string Duration(TimeSpan t)
	{
		if (!(t.TotalDays >= 1.0))
		{
			if (t.TotalHours >= 1.0)
			{
				return $"{(int)t.TotalHours} h {t.Minutes:00} m";
			}
			return $"{Math.Max(0, t.Minutes)} m";
		}
		return $"{(int)t.TotalDays} d {t.Hours} h";
	}

	public static string Milliseconds(double? ms)
	{
		if (ms.HasValue)
		{
			double valueOrDefault = ms.GetValueOrDefault();
			if (double.IsFinite(valueOrDefault))
			{
				return valueOrDefault.ToString((valueOrDefault < 10.0) ? "0.0" : "0", Invariant) + " ms";
			}
		}
		return "—";
	}

	public static string Money(double amount, string currency)
	{
		return currency + " " + amount.ToString((amount < 10.0) ? "0.00" : "0.0", Invariant);
	}

	public static double Heat(double? celsius)
	{
		if (celsius.HasValue)
		{
			double valueOrDefault = celsius.GetValueOrDefault();
			if (double.IsFinite(valueOrDefault))
			{
				return Math.Clamp((valueOrDefault - 30.0) / 65.0, 0.0, 1.0);
			}
		}
		return 0.0;
	}

	public static double Share(double? percent)
	{
		if (percent.HasValue)
		{
			double valueOrDefault = percent.GetValueOrDefault();
			if (double.IsFinite(valueOrDefault))
			{
				return Math.Clamp(valueOrDefault / 100.0, 0.0, 1.0);
			}
		}
		return 0.0;
	}
}
