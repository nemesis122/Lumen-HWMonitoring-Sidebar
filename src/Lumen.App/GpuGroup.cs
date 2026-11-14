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

	/// <summary>The main graphics card: a dedicated one before an integrated one, the one with the most memory first.</summary>
	public static GpuInfo? Main(IEnumerable<GpuInfo> gpus) =>
		gpus.OrderBy(g => g.Integrated ? 1 : 0).ThenByDescending(g => g.MemoryTotalMb ?? 0).FirstOrDefault();

	/// <summary>The AIO choice that adds a further graphics card (off unless the user ticks it).</summary>
	public static string ExtraKey(GpuInfo g) => "aio.gpu." + g.Id + ".extra";
}
