namespace Lumen.Core;

public sealed record UsageInfo(double WattsNow, double KwhToday, double KwhWeek, double KwhMonth, double DownToday, double UpToday, double DownWeek, double UpWeek, double DownMonth, double UpMonth);
