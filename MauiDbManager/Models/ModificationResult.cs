namespace MauiDbManager.Models;

public sealed record ModificationResult(bool IsSuccess, string Message)
{
    public static ModificationResult Success(string message) => new(true, message);
    public static ModificationResult Failure(string message) => new(false, message);
}
