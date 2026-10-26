namespace MauiDbManager.Models;

public sealed record ModificationPreviewResult(
    bool IsSuccess,
    string Message,
    IReadOnlyList<ModificationPreviewSummary> Summaries,
    IReadOnlyList<ModificationPreviewRow> Rows)
{
    public static ModificationPreviewResult Success(
        IReadOnlyList<ModificationPreviewSummary> summaries,
        IReadOnlyList<ModificationPreviewRow> rows) =>
        new(true, "Preview generated from the current database state. No changes were written.", summaries, rows);

    public static ModificationPreviewResult Failure(string message) =>
        new(false, message, Array.Empty<ModificationPreviewSummary>(), Array.Empty<ModificationPreviewRow>());
}
