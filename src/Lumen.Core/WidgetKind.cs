namespace Lumen.Core;

public enum WidgetKind
{
	Clock,
	WorldClocks,
	Calendar,
	Weather,
	Media,
	Battery,
	Cpu,
	Gpu,
	Fps,
	Cooling,
	Power,
	Memory,
	Processes,
	System,
	Drives,
	Volumes,
	Network,
	Ping,
	DataUsage,
	Notes,
	// The whole PC on one card.
	Overview,
	// Remaining life, hours and power-on count of the drives.
	DriveHealth,
	// Countdown timer.
	Timer
}
