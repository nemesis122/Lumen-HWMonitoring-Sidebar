using System;

namespace Lumen.Core;

public sealed record MemoryInfo(double UsedGb, double TotalGb)
{
	public double AvailableGb => Math.Max(0.0, TotalGb - UsedGb);

	public double Load
	{
		get
		{
			if (!(TotalGb > 0.0))
			{
				return 0.0;
			}
			return UsedGb / TotalGb * 100.0;
		}
	}
}
