using System;

namespace Lumen.Core;

public interface ISnapshotSource : IDisposable
{
	Snapshot Read();
}
