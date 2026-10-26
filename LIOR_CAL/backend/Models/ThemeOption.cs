namespace MauiDbManager.Models;

public sealed record ThemeOption(ThemePreference Value, string Label)
{
    public override string ToString() => Label;
}
