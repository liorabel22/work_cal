namespace MauiDbManager.Models;

/// <summary>The outcome of connecting and loading databases visible to the login.</summary>
public sealed record ConnectionResult(
    bool IsSuccess,
    string Message,
    IReadOnlyList<string> Databases)
{
    public static ConnectionResult Success(string message, IReadOnlyList<string> databases) =>
        new(true, message, databases);

    public static ConnectionResult Failure(string message) =>
        new(false, message, Array.Empty<string>());
}
