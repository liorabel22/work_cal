namespace MauiDbManager.Models;

public sealed record AmplitudeModification(
    IReadOnlyList<long> LowIds,
    bool UpdateStatus,
    BooleanStatus Status,
    bool UpdateTime,
    long Time);
