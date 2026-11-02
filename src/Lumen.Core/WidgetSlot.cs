namespace Lumen.Core;

public sealed class WidgetSlot
{
	public WidgetKind Kind { get; set; }

	public bool Visible { get; set; } = true;

	public WidgetStyle Style { get; set; }

	public int Column { get; set; }

	public int Page { get; set; } = 1;

	public string Name { get; set; } = "";

	public string HardwareName { get; set; } = "";

	public bool ShowTitle { get; set; } = true;

	public bool ShowHardware { get; set; } = true;
}
