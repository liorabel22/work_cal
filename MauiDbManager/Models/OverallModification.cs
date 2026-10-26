namespace MauiDbManager.Models;

public sealed record OverallModification(
    IReadOnlyList<long> MidIds,
    IReadOnlyList<long> HighIds,
    bool UseAllTypeB,
    IReadOnlySet<long> SelectedTypeBLowIds,
    bool UpdateStatus,
    OverallStatus Status,
    bool UpdateTime,
    long Time);
