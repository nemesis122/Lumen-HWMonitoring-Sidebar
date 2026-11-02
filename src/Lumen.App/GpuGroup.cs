using System.Collections.Generic;
using System.Linq;
using Lumen.Core;

namespace Lumen.App;

public static class GpuGroup
{
	public static IReadOnlyList<GpuInfo> Visible(Snapshot snap, Settings settings)
	{
		return (from g in snap.Gpus
			where settings.ShowIntegratedGpu || !g.Integrated || snap.Gpus.All((GpuInfo x) => x.Integrated)
			orderby g.Integrated ? 1 : 0
			select g).ToList();
	}
}
