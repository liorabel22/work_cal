namespace MauiDbManager.Models;

public sealed record OverallStatusOption(OverallStatus Value, string Label)
{
    public override string ToString() => Label;
}
