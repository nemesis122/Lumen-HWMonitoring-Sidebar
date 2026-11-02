namespace Lumen.Core;

public sealed record VolumeInfo(string Name, string Label, double TotalGb, double FreeGb)
{
	public double UsedPercent
	{
		get
		{
			if (!(TotalGb > 0.0))
			{
				return 0.0;
			}
			return (TotalGb - FreeGb) / TotalGb * 100.0;
		}
	}
}
