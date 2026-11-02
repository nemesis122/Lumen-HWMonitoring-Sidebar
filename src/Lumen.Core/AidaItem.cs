namespace Lumen.Core;

public sealed record AidaItem(string Category, string Id, string Label, string Text)
{
	public double? Value => External.Number(Text);
}
