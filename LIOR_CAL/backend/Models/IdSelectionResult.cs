namespace MauiDbManager.Models;

public sealed record IdSelectionResult(
    bool IsSuccess,
    IReadOnlyList<long> Values,
    string? ErrorMessage)
{
    public static IdSelectionResult Success(IReadOnlyList<long> values) => new(true, values, null);
    public static IdSelectionResult Failure(string message) => new(false, Array.Empty<long>(), message);
}
