namespace MauiDbManager.Models;

public sealed record ModificationPreviewRow(
    string TableName,
    string Action,
    string Key,
    string CurrentValues,
    string ProposedValues);
