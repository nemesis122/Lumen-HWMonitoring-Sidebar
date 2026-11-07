namespace Lumen.Core;

public sealed class WidgetSlot
{
	public WidgetKind Kind { get; set; }

	public bool Visible { get; set; } = true;

	public WidgetStyle Style { get; set; }

    public double GraphicScale { get; set; } = 1.0;

    public double ValueScale { get; set; } = 1.0;

    public double LabelScale { get; set; } = 1.0;

    /// <summary>Size of the big main value (the number in the ring or the large figure at the top).</summary>
    public double HeroScale { get; set; } = 1.0;

    /// <summary>Size of the whole widget: multiplies graphics, values and labels together.</summary>
    public double WidgetScale { get; set; } = 1.0;

    // The sizes in effect: each one times the size of the whole widget.
    [System.Text.Json.Serialization.JsonIgnore] public double Graphics => GraphicScale * WidgetScale;

    [System.Text.Json.Serialization.JsonIgnore] public double Values => ValueScale * WidgetScale;

    [System.Text.Json.Serialization.JsonIgnore] public double Hero => HeroScale * WidgetScale;

    [System.Text.Json.Serialization.JsonIgnore] public double Labels => LabelScale * WidgetScale;

    public List<string> HiddenSensors { get; set; } = new();

	public int Column { get; set; }

	public int Page { get; set; } = 1;

	public string Name { get; set; } = "";

	public string HardwareName { get; set; } = "";

	public bool ShowTitle { get; set; } = true;

	public bool ShowHardware { get; set; } = true;

	// Accent colour as #RRGGBB; empty uses the colour of the widget kind.
	public string Accent { get; set; } = "";
}
