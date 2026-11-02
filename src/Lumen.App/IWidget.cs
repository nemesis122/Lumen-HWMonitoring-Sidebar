using Lumen.Core;

namespace Lumen.App;

public interface IWidget
{
	void Update(Snapshot s, History h);
}
