using System;
using System.Collections.Generic;

namespace Lumen.Core;

public sealed record WeatherInfo(string Place, double TemperatureC, double? FeelsLikeC, int Code, bool IsDay, double? WindKmh, double? Humidity, IReadOnlyList<WeatherDay> Days, DateTime Updated);
