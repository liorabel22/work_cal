namespace MauiDbManager.Models;

public sealed record ModificationPreviewSummary(
    string TableName,
    long InsertCount,
    long UpdateCount,
    int DisplayedCount)
{
    public long TotalCount => InsertCount + UpdateCount;
    public string CountsText => $"{InsertCount:N0} inserts · {UpdateCount:N0} updates · {TotalCount:N0} total";
    public string DisplayText => DisplayedCount < TotalCount
        ? $"Showing {DisplayedCount:N0} of {TotalCount:N0} rows"
        : $"Showing all {DisplayedCount:N0} rows";
}
