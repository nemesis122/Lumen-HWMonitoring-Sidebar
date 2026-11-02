using Lumen.Core;

namespace Lumen.App;

public static class Naming
{
	public static string Title(WidgetSlot? slot, int index, string builtIn)
	{
		if (slot == null || slot.ShowTitle)
		{
			if (index != 0 || string.IsNullOrWhiteSpace(slot?.Name))
			{
				return builtIn;
			}
			return slot.Name.Trim();
		}
		return "";
	}

	public static string Hardware(WidgetSlot? slot, int index, string builtIn)
	{
		if (index != 0 || string.IsNullOrWhiteSpace(slot?.HardwareName))
		{
			return builtIn;
		}
		return slot.HardwareName.Trim();
	}

	public static bool ShowHardware(WidgetSlot? slot, Settings s)
	{
		if (s.ShowNames)
		{
			return slot?.ShowHardware ?? true;
		}
		return false;
	}
}
