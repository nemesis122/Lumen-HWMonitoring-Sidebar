using Lumen.Core;

namespace Lumen.App;

public interface IMediaSource
{
	MediaInfo? Read();

	void Send(string command);
}
