using System.Globalization;

namespace Lumen.Core;

// Every number on screen goes through here so units and precision stay consistent.
public static class Format
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    public const string Missing = "—";

    public static string Number(double? value, string format = "0")
        => value is double v && double.IsFinite(v) ? v.ToString(format, Invariant) : Missing;

    public static string Temperature(double? celsius, bool fahrenheit)
        => celsius is double c && double.IsFinite(c) ? (fahrenheit ? c * 1.8 + 32 : c).ToString("0", Invariant) : Missing;

    public static string TemperatureUnit(bool fahrenheit) => fahrenheit ? "°F" : "°C";

    public static string Percent(double? value) => value is double v && double.IsFinite(v) ? Math.Clamp(v, 0, 100).ToString("0", Invariant) + " %" : Missing;

    public static string Clock(double? mhz)
        => mhz is double m && double.IsFinite(m) && m > 0 ? (m >= 1000 ? (m / 1000).ToString("0.00", Invariant) + " GHz" : m.ToString("0", Invariant) + " MHz") : Missing;

    public static string Watts(double? w) => w is double v && double.IsFinite(v) ? v.ToString(v < 10 ? "0.0" : "0", Invariant) + " W" : Missing;

    public static string Volts(double? v) => v is double x && double.IsFinite(x) && x > 0 ? x.ToString("0.000", Invariant) + " V" : Missing;

    public static string Rpm(double? r) => r is double v && double.IsFinite(v) ? v.ToString("0", Invariant) + " rpm" : Missing;

    public static string Gigabytes(double? gb)
        => gb is double v && double.IsFinite(v) ? (v >= 100 ? v.ToString("0", Invariant) : v.ToString("0.0", Invariant)) + " GB" : Missing;

    // Bytes per second with binary steps, one decimal below ten: 0 B/s, 940 KB/s, 1.4 MB/s, 112 MB/s.
    public static string Rate(double? bytesPerSecond)
    {
        if (bytesPerSecond is not double v || !double.IsFinite(v)) return Missing;
        v = Math.Max(0, v);
        string[] units = { "B/s", "KB/s", "MB/s", "GB/s" };
        int i = 0;
        while (v >= 1024 && i < units.Length - 1) { v /= 1024; i++; }
        return (i > 0 && v < 10 ? v.ToString("0.0", Invariant) : v.ToString("0", Invariant)) + " " + units[i];
    }

    // Splits "1.4 MB/s" into number and unit for the large hero display.
    public static (string Number, string Unit) Split(string text)
    {
        int space = text.LastIndexOf(' ');
        return space < 0 ? (text, "") : (text[..space], text[(space + 1)..]);
    }

    public static string Link(double? bitsPerSecond)
    {
        if (bitsPerSecond is not double b || !double.IsFinite(b) || b <= 0) return Missing;
        return b >= 1e9 ? (b / 1e9).ToString(b % 1e9 == 0 ? "0" : "0.#", Invariant) + " Gbit/s" : (b / 1e6).ToString("0", Invariant) + " Mbit/s";
    }

    // Bits per second as network gear and providers state them: 850 kbit/s, 650 Mbit/s, 1.25 Gbit/s.
    public static string Bits(double? bitsPerSecond)
    {
        if (bitsPerSecond is not double b || !double.IsFinite(b)) return Missing;
        b = Math.Max(0, b);
        if (b >= 1e9) return (b / 1e9).ToString(b >= 1e10 ? "0.0" : "0.00", Invariant) + " Gbit/s";
        if (b >= 1e6) return (b / 1e6).ToString(b >= 1e8 ? "0" : "0.0", Invariant) + " Mbit/s";
        return (b / 1e3).ToString("0", Invariant) + " kbit/s";
    }

    // Byte totals: 840 MB, 18.4 GB, 1.21 TB.
    public static string Size(double? bytes)
    {
        if (bytes is not double v || !double.IsFinite(v)) return Missing;
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        int i = 0;
        v = Math.Max(0, v);
        while (v >= 1024 && i < units.Length - 1) { v /= 1024; i++; }
        return v.ToString(i == 0 || v >= 100 ? "0" : v >= 10 ? "0.0" : "0.00", Invariant) + " " + units[i];
    }

    // Disk capacities given in GiB: 512 GB, 1.82 TB.
    public static string Capacity(double? gb) => gb is double v && double.IsFinite(v)
        ? v >= 1000 ? (v / 1024).ToString("0.00", Invariant) + " TB" : v.ToString(v >= 100 ? "0" : "0.0", Invariant) + " GB" : Missing;

    // 2 d 3 h, 3 h 12 m, 12 m.
    public static string Duration(TimeSpan t) =>
        t.TotalDays >= 1 ? $"{(int)t.TotalDays} d {t.Hours} h" : t.TotalHours >= 1 ? $"{(int)t.TotalHours} h {t.Minutes:00} m" : $"{Math.Max(0, t.Minutes)} m";

    public static string Milliseconds(double? ms) => ms is double v && double.IsFinite(v) ? v.ToString(v < 10 ? "0.0" : "0", Invariant) + " ms" : Missing;

    public static string Money(double amount, string currency) => currency + " " + amount.ToString(amount < 10 ? "0.00" : "0.0", Invariant);

    // 0 at "cool", 1 at "hot"; used to colour readings by state rather than by category.
    public static double Heat(double? celsius) => celsius is double c && double.IsFinite(c) ? Math.Clamp((c - 30) / 65, 0, 1) : 0;
    public static double Share(double? percent) => percent is double p && double.IsFinite(p) ? Math.Clamp(p / 100, 0, 1) : 0;
}
