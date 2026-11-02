using Lumen.Core;

namespace Lumen.App;

public sealed record Metrics(double Pad, double Gap, double Radius, double Hero, double Chart, double Skyline, bool Subtitles)
{
	public static Metrics For(Density d)
	{
		if (d != Density.Compact)
		{
			return new Metrics(16.0, 10.0, 16.0, 40.0, 52.0, 28.0, Subtitles: true);
		}
		return new Metrics(12.0, 8.0, 14.0, 30.0, 34.0, 20.0, Subtitles: false);
	}
}
