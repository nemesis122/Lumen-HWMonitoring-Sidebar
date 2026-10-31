using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace Lumen.Core;

// Weather from Open-Meteo: free, no account or key. Only the place's coordinates are sent.
public sealed class WeatherClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(12) };
    static WeatherClient() => Http.DefaultRequestHeaders.UserAgent.ParseAdd("Lumen-Sidebar/1.0");

    public sealed record Place(string Name, string Region, double Latitude, double Longitude);

    public static async Task<Place?> FindAsync(string query, string language)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;
        string url = $"https://geocoding-api.open-meteo.com/v1/search?count=1&format=json&language={language}&name={Uri.EscapeDataString(query.Trim())}";
        using var doc = JsonDocument.Parse(await Http.GetStringAsync(url));
        return ParsePlace(doc.RootElement);
    }

    public static Place? ParsePlace(JsonElement root)
    {
        if (!root.TryGetProperty("results", out var results) || results.GetArrayLength() == 0) return null;
        var r = results[0];
        string name = r.GetProperty("name").GetString() ?? "";
        string region = string.Join(", ", new[] { Str(r, "admin1"), Str(r, "country") }.Where(x => !string.IsNullOrEmpty(x) && x != name));
        return new Place(name, region, r.GetProperty("latitude").GetDouble(), r.GetProperty("longitude").GetDouble());
    }

    public static async Task<WeatherInfo?> ReadAsync(string place, double latitude, double longitude)
    {
        string url = "https://api.open-meteo.com/v1/forecast?latitude=" + latitude.ToString("0.####", CultureInfo.InvariantCulture) +
            "&longitude=" + longitude.ToString("0.####", CultureInfo.InvariantCulture) +
            "&current=temperature_2m,apparent_temperature,relative_humidity_2m,weather_code,wind_speed_10m,is_day" +
            "&daily=weather_code,temperature_2m_max,temperature_2m_min&timezone=auto&forecast_days=4";
        using var doc = JsonDocument.Parse(await Http.GetStringAsync(url));
        return Parse(doc.RootElement, place, DateTime.Now);
    }

    public static WeatherInfo? Parse(JsonElement root, string place, DateTime now)
    {
        if (!root.TryGetProperty("current", out var c)) return null;
        var days = new List<WeatherDay>();
        if (root.TryGetProperty("daily", out var d))
        {
            var time = d.GetProperty("time");
            for (int i = 0; i < time.GetArrayLength(); i++)
            {
                double? max = Num(d.GetProperty("temperature_2m_max")[i]), min = Num(d.GetProperty("temperature_2m_min")[i]);
                if (max is null || min is null) continue;
                days.Add(new WeatherDay(DateTime.Parse(time[i].GetString()!, CultureInfo.InvariantCulture), min.Value, max.Value, (int)(Num(d.GetProperty("weather_code")[i]) ?? 0)));
            }
        }
        double? temp = Field(c, "temperature_2m");
        if (temp is null) return null;
        return new WeatherInfo(place, temp.Value, Field(c, "apparent_temperature"), (int)(Field(c, "weather_code") ?? 0),
            (Field(c, "is_day") ?? 1) > 0, Field(c, "wind_speed_10m"), Field(c, "relative_humidity_2m"), days, now);
    }

    private static string? Str(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    private static double? Field(JsonElement e, string name) => e.TryGetProperty(name, out var v) ? Num(v) : null;
    private static double? Num(JsonElement v) => v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;

    // WMO weather codes folded into the groups the sidebar draws and names.
    public static string Group(int code) => code switch
    {
        0 => "clear",
        1 => "mostly-clear",
        2 => "partly-cloudy",
        3 => "overcast",
        45 or 48 => "fog",
        >= 51 and <= 55 => "drizzle",
        56 or 57 or 66 or 67 => "freezing",
        >= 61 and <= 65 => "rain",
        >= 71 and <= 77 => "snow",
        >= 80 and <= 82 => "showers",
        85 or 86 => "snow",
        >= 95 => "storm",
        _ => "overcast"
    };
}
