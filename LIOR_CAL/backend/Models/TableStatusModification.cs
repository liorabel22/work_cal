namespace MauiDbManager.Models;

/// <summary>One enabled section's requested table status change.</summary>
public sealed record TableStatusModification(string TableName, BooleanStatus Status);
