using System.Collections.Generic;
using System.Linq;

namespace Lumen.Core;

public sealed record SensorNode(string Type, string Name, string Id, IReadOnlyList<SensorValue> Sensors, IReadOnlyList<SensorNode> Sub)
{
	public IEnumerable<SensorValue> All => Sensors.Concat(Sub.SelectMany((SensorNode s) => s.All));

	public bool IsBoard => Type == "Motherboard";

	public bool IsGpu
	{
		get
		{
			switch (Type)
			{
			case "GpuNvidia":
			case "GpuAmd":
			case "GpuIntel":
				return true;
			default:
				return false;
			}
		}
	}
}
