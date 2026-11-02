using System;

namespace Lumen.Core;

public sealed record SystemInfo(string Os, string Build, TimeSpan Uptime, DateTime BootTime, int Processes, int Threads);
