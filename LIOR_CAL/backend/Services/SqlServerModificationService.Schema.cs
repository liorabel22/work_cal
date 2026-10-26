using System.Data;
using Microsoft.Data.SqlClient;

namespace MauiDbManager.Services;

public sealed partial class SqlServerModificationService
{
    private static string QuoteIdentifier(string identifier) =>
        $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";

    private static async Task<IReadOnlyList<(long Tb, long Ta)>> LoadTypeAPairsAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string query = """
            SELECT DISTINCT typeA.[low_id], typeA.[high_id]
            FROM [dbo].[type_a] AS typeA
            INNER JOIN [dbo].[type_b] AS typeB
                ON typeB.[low_id] = typeA.[low_id]
            ORDER BY typeA.[low_id], typeA.[high_id];
            """;
        await using var command = new SqlCommand(query, connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var pairs = new List<(long Tb, long Ta)>();

        while (await reader.ReadAsync(cancellationToken))
        {
            pairs.Add((
                Convert.ToInt64(reader.GetValue(0), System.Globalization.CultureInfo.InvariantCulture),
                Convert.ToInt64(reader.GetValue(1), System.Globalization.CultureInfo.InvariantCulture)));
        }

        return pairs.Distinct().ToArray();
    }

    private static async Task<IReadOnlyList<string>> GetWritableColumnsAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        string qualifiedTable,
        CancellationToken cancellationToken)
    {
        const string query = """
            SELECT [name]
            FROM sys.columns
            WHERE [object_id] = OBJECT_ID(@tableName, N'U')
              AND [is_computed] = 0
              AND [is_identity] = 0
            ORDER BY [column_id];
            """;

        await using var command = new SqlCommand(query, connection, transaction);
        command.Parameters.Add("@tableName", SqlDbType.NVarChar, 260).Value = qualifiedTable;

        var columns = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            columns.Add(reader.GetString(0));

        if (columns.Count == 0)
            throw new InvalidOperationException($"Table {qualifiedTable} does not exist in the selected database.");

        return columns;
    }

    private static string FormatBoolean(object value) =>
        value is DBNull ? "NULL" : Convert.ToBoolean(value) ? "True" : "False";

    private static string FormatValue(object value) =>
        value is DBNull
            ? "NULL"
            : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "NULL";

    private static long ToInt64(object value) =>
        Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);
}
