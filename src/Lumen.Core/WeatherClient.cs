using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Lumen.Core;

public sealed class WeatherClient
{
	public sealed record Place(string Name, string Region, double Latitude, double Longitude);

	private static readonly HttpClient Http;

	static WeatherClient()
	{
		Http = new HttpClient
		{
			Timeout = TimeSpan.FromSeconds(12.0)
		};
		Http.DefaultRequestHeaders.UserAgent.ParseAdd("Lumen-Sidebar/1.0");
	}

	public static async Task<Place?> FindAsync(string query, string language)
	{
		if (string.IsNullOrWhiteSpace(query))
		{
			return null;
		}
		string requestUri = "https://geocoding-api.open-meteo.com/v1/search?count=1&format=json&language=" + language + "&name=" + Uri.EscapeDataString(query.Trim());
		using JsonDocument jsonDocument = JsonDocument.Parse(await Http.GetStringAsync(requestUri));
		return ParsePlace(jsonDocument.RootElement);
	}

	public static Place? ParsePlace(JsonElement root)
	{
		if (!root.TryGetProperty("results", out var value) || value.GetArrayLength() == 0)
		{
			return null;
		}
		JsonElement e = value[0];
		string name = e.GetProperty("name").GetString() ?? "";
		string region = string.Join(", ", new string[2]
		{
			Str(e, "admin1"),
			Str(e, "country")
		}.Where((string x) => !string.IsNullOrEmpty(x) && x != name));
		return new Place(name, region, e.GetProperty("latitude").GetDouble(), e.GetProperty("longitude").GetDouble());
	}

	public static async Task<WeatherInfo?> ReadAsync(string place, double latitude, double longitude)
	{
		string requestUri = "https://api.open-meteo.com/v1/forecast?latitude=" + latitude.ToString("0.####", CultureInfo.InvariantCulture) + "&longitude=" + longitude.ToString("0.####", CultureInfo.InvariantCulture) + "&current=temperature_2m,apparent_temperature,relative_humidity_2m,weather_code,wind_speed_10m,is_day&daily=weather_code,temperature_2m_max,temperature_2m_min&timezone=auto&forecast_days=4";
		using JsonDocument jsonDocument = JsonDocument.Parse(await Http.GetStringAsync(requestUri));
		return Parse(jsonDocument.RootElement, place, DateTime.Now);
	}

	public static WeatherInfo? Parse(JsonElement root, string place, DateTime now)
	{
		if (!root.TryGetProperty("current", out var value))
		{
			return null;
		}
		List<WeatherDay> list = new List<WeatherDay>();
		if (root.TryGetProperty("daily", out var value2))
		{
			JsonElement property = value2.GetProperty("time");
			for (int i = 0; i < property.GetArrayLength(); i++)
			{
				double? num = Num(value2.GetProperty("temperature_2m_max")[i]);
				double? num2 = Num(value2.GetProperty("temperature_2m_min")[i]);
				if (num.HasValue && num2.HasValue)
				{
					list.Add(new WeatherDay(DateTime.Parse(property[i].GetString(), CultureInfo.InvariantCulture), num2.Value, num.Value, (int)Num(value2.GetProperty("weather_code")[i]).GetValueOrDefault()));
				}
			}
		}
		double? num3 = Field(value, "temperature_2m");
		if (!num3.HasValue)
		{
			return null;
		}
		return new WeatherInfo(place, num3.Value, Field(value, "apparent_temperature"), (int)Field(value, "weather_code").GetValueOrDefault(), (Field(value, "is_day") ?? 1.0) > 0.0, Field(value, "wind_speed_10m"), Field(value, "relative_humidity_2m"), list, now);
	}

	private static string? Str(JsonElement e, string name)
	{
		if (!e.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
		{
			return null;
		}
		return value.GetString();
	}

	private static double? Field(JsonElement e, string name)
	{
		if (!e.TryGetProperty(name, out var value))
		{
			return null;
		}
		return Num(value);
	}

	private static double? Num(JsonElement v)
	{
		if (v.ValueKind != JsonValueKind.Number)
		{
			return null;
		}
		return v.GetDouble();
	}

	public static string Group(int code)
	{
		if (code <= 77)
		{
			if (code <= 65)
			{
				if (code <= 55)
				{
					if (code >= 51)
					{
						return "drizzle";
					}
					switch (code)
					{
					case 0:
						return "clear";
					case 1:
						return "mostly-clear";
					case 2:
						return "partly-cloudy";
					case 3:
						return "overcast";
					case 45:
					case 48:
						return "fog";
					}
				}
				else
				{
					if (code >= 61)
					{
						return "rain";
					}
					if ((uint)(code - 56) <= 1u)
					{
						goto IL_00a5;
					}
				}
			}
			else
			{
				if (code >= 71)
				{
					return "snow";
				}
				if ((uint)(code - 66) <= 1u)
				{
					goto IL_00a5;
				}
			}
		}
		else if (code <= 82)
		{
			if (code >= 80)
			{
				return "showers";
			}
		}
		else
		{
			if (code >= 95)
			{
				return "storm";
			}
			if ((uint)(code - 85) <= 1u)
			{
				return "snow";
			}
		}
		return "overcast";
		IL_00a5:
		return "freezing";
	}
}
