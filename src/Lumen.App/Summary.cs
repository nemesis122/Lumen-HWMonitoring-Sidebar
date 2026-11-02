using System;
using System.Runtime.CompilerServices;
using Avalonia.Media;

namespace Lumen.App;

public sealed record Summary(string Glyph, string Title, string Value, string Unit, Color Color, double Fraction, string Caption, double[] Series, double? SeriesMax)
{
	public string Name { get; init; } = "";

	public double? Second { get; init; }

	public string SecondLabel { get; init; } = "";

	public static readonly Summary Empty = new Summary("M5,3 H11 A2,2 0 0 1 13,5 V11 A2,2 0 0 1 11,13 H5 A2,2 0 0 1 3,11 V5 A2,2 0 0 1 5,3 Z M6.5,6.5 H9.5 V9.5 H6.5 Z M6,1 V3 M10,1 V3 M6,13 V15 M10,13 V15 M1,6 H3 M1,10 H3 M13,6 H15 M13,10 H15", "", "—", "", Colors.Gray, 0.0, "", Array.Empty<double>(), null);

	[CompilerGenerated]
	private Summary(Summary original)
	{
		Glyph = original.Glyph;
		Title = original.Title;
		Value = original.Value;
		Unit = original.Unit;
		Color = original.Color;
		Fraction = original.Fraction;
		Caption = original.Caption;
		Series = original.Series;
		SeriesMax = original.SeriesMax;
		Name = original.Name;
		Second = original.Second;
		SecondLabel = original.SecondLabel;
	}
}
