using Avalonia.Media;

namespace Lumen.App;

public sealed record Reading(string Label, string Value, double Fraction, Color Color);
