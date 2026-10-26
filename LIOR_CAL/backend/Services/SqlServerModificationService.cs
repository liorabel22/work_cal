namespace MauiDbManager.Services;

public sealed partial class SqlServerModificationService : IDatabaseModificationService
{
    private const int PreviewRowsPerTable = 100;
    private static readonly IReadOnlyDictionary<string, string> AllowedTables =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["dbo.gain"] = "dbo.gain",
            ["dbo.zakan"] = "dbo.zakan"
        };
}
