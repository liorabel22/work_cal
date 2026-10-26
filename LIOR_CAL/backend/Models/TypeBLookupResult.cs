namespace MauiDbManager.Models;

public sealed record TypeBLookupResult(
    bool IsSuccess,
    string Message,
    IReadOnlyList<TypeBLookupItem> Items,
    IReadOnlySet<string> AvailableTables)
{
    public static TypeBLookupResult Success(
        IReadOnlyList<TypeBLookupItem> items,
        IReadOnlySet<string> availableTables,
        string message = "") =>
        new(true, message, items, availableTables);

    public static TypeBLookupResult Failure(string message) =>
        new(false, message, Array.Empty<TypeBLookupItem>(), new HashSet<string>(StringComparer.OrdinalIgnoreCase));
}
