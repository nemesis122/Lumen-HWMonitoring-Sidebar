using System;

namespace Lumen.Core;

public sealed record WeatherDay(DateTime Date, double MinC, double MaxC, int Code);
