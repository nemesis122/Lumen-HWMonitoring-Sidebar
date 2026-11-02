using System.Globalization;
using System.Linq;
using Avalonia.Controls;

namespace Lumen.App;

public static class StatGrid
{
	public static Control Of(params Stat[] stats)
	{
		return Weighted(string.Join(",", stats.Select((Stat _) => "*")), stats);
	}

	public static Control Weighted(string columns, params Stat[] stats)
	{
		double result;
		return new StatPanel((from c in columns.Split(',')
			select (!(c.Trim() == "*")) ? ((!double.TryParse(c.Trim().TrimEnd('*'), NumberStyles.Float, CultureInfo.InvariantCulture, out result)) ? 1.0 : result) : 1.0).ToArray(), stats);
	}
}
