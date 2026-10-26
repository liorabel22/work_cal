using System.Data;
using System.Text.Json;
using MauiDbManager.Models;
using Microsoft.Data.SqlClient;

namespace MauiDbManager.Services;

public sealed partial class SqlServerModificationService
{
    public async Task<ModificationPreviewResult> PreviewModificationsAsync(
        string serverName,
        string databaseName,
        string userName,
        string password,
        bool trustServerCertificate,
        IReadOnlyList<TableStatusModification> statusModifications,
        OverallModification? overallModification,
        AmplitudeModification? amplitudeModification,
        CancellationToken cancellationToken = default)
    {
        if (statusModifications.Count == 0 && overallModification is null && amplitudeModification is null)
            return ModificationPreviewResult.Failure("There are no pending modifications to preview.");

        var connectionString = new SqlConnectionStringBuilder
        {
            DataSource = serverName,
            InitialCatalog = databaseName,
            UserID = userName,
            Password = password,
            IntegratedSecurity = false,
            Encrypt = true,
            TrustServerCertificate = trustServerCertificate,
            ConnectTimeout = 10,
            ApplicationName = "MAUI DB Manager"
        }.ConnectionString;

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            var summaries = new List<ModificationPreviewSummary>();
            var rows = new List<ModificationPreviewRow>();

            foreach (var modification in statusModifications)
            {
                var preview = await PreviewOneAsync(connection, modification, cancellationToken);
                summaries.Add(preview.Summary);
                rows.AddRange(preview.Rows);
            }

            if (overallModification is not null)
            {
                var preview = await PreviewOverallAsync(connection, overallModification, cancellationToken);
                summaries.Add(preview.Summary);
                rows.AddRange(preview.Rows);
            }

            if (amplitudeModification is not null)
            {
                var preview = await PreviewAmplitudeAsync(connection, amplitudeModification, cancellationToken);
                summaries.Add(preview.Summary);
                rows.AddRange(preview.Rows);
            }

            return ModificationPreviewResult.Success(summaries, rows);
        }
        catch (SqlException ex)
        {
            return ModificationPreviewResult.Failure($"Preview failed: {ex.Message} (SQL error {ex.Number})");
        }
        catch (Exception ex)
        {
            return ModificationPreviewResult.Failure($"Preview failed: {ex.Message}");
        }
    }

    private static async Task<TablePreview> PreviewOneAsync(
        SqlConnection connection,
        TableStatusModification modification,
        CancellationToken cancellationToken)
    {
        if (!AllowedTables.TryGetValue(modification.TableName, out var qualifiedTable))
            throw new InvalidOperationException($"Table {modification.TableName} is not an allowed modification target.");

        var columns = await GetWritableColumnsAsync(connection, null, qualifiedTable, cancellationToken);
        var statusColumn = columns.FirstOrDefault(
            column => column.Equals("status", StringComparison.OrdinalIgnoreCase));
        var keyColumns = columns
            .Where(column => !column.Equals("status", StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToArray();

        if (statusColumn is null || keyColumns.Length < 2)
        {
            throw new InvalidOperationException(
                $"Table {qualifiedTable} must contain two key columns and a status column.");
        }

        var tableParts = qualifiedTable.Split('.', 2);
        var quotedTable = $"{QuoteIdentifier(tableParts[0])}.{QuoteIdentifier(tableParts[1])}";
        var key1 = QuoteIdentifier(keyColumns[0]);
        var key2 = QuoteIdentifier(keyColumns[1]);
        var status = QuoteIdentifier(statusColumn);
        var query = $"SELECT TOP (1) {status} FROM {quotedTable} WHERE {key1} = 0 AND {key2} = 0;";

        await using var command = new SqlCommand(query, connection);
        var currentValue = await command.ExecuteScalarAsync(cancellationToken);
        var exists = currentValue is not null;
        var action = exists ? "Update" : "Insert";
        var proposedStatus = modification.Status == BooleanStatus.True ? "True" : "False";
        var current = exists ? $"status={FormatBoolean(currentValue!)}" : "—";
        var row = new ModificationPreviewRow(
            qualifiedTable,
            action,
            $"{keyColumns[0]}=0, {keyColumns[1]}=0",
            current,
            $"status={proposedStatus}");
        var summary = new ModificationPreviewSummary(
            qualifiedTable,
            exists ? 0 : 1,
            exists ? 1 : 0,
            1);
        return new(summary, [row]);
    }

    private static async Task<TablePreview> PreviewAmplitudeAsync(
        SqlConnection connection,
        AmplitudeModification modification,
        CancellationToken cancellationToken)
    {
        const string qualifiedTable = "dbo.amplitude";
        var columns = await GetWritableColumnsAsync(connection, null, qualifiedTable, cancellationToken);
        var lowIdColumn = columns.FirstOrDefault(
            column => column.Equals("low_id", StringComparison.OrdinalIgnoreCase));
        var statusColumn = columns.FirstOrDefault(
            column => column.Equals("status", StringComparison.OrdinalIgnoreCase));
        var timeColumn = columns.FirstOrDefault(
            column => column.Equals("time", StringComparison.OrdinalIgnoreCase));
        var zeroColumn = columns.FirstOrDefault(column =>
            !column.Equals("low_id", StringComparison.OrdinalIgnoreCase) &&
            !column.Equals("status", StringComparison.OrdinalIgnoreCase) &&
            !column.Equals("time", StringComparison.OrdinalIgnoreCase));

        if (lowIdColumn is null || statusColumn is null || timeColumn is null || zeroColumn is null ||
            !columns[0].Equals("low_id", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Table dbo.amplitude must start with low_id and contain a second value column, status, and time.");
        }

        var lowId = QuoteIdentifier(lowIdColumn);
        var status = QuoteIdentifier(statusColumn);
        var time = QuoteIdentifier(timeColumn);
        var query = $"""
            WITH requested AS
            (
                SELECT DISTINCT TRY_CONVERT(bigint, [value]) AS [low_id]
                FROM OPENJSON(@lowIds)
                WHERE TRY_CONVERT(bigint, [value]) IS NOT NULL
            )
            SELECT TOP (@limit)
                requested.[low_id], currentRow.[status], currentRow.[time], currentRow.[exists],
                COUNT_BIG(*) OVER () AS [total_count],
                SUM(CONVERT(bigint, CASE WHEN currentRow.[exists] IS NULL THEN 1 ELSE 0 END)) OVER () AS [insert_count]
            FROM requested
            OUTER APPLY
            (
                SELECT TOP (1) target.{status} AS [status], target.{time} AS [time], 1 AS [exists]
                FROM [dbo].[amplitude] AS target
                WHERE target.{lowId} = requested.[low_id]
            ) AS currentRow
            ORDER BY requested.[low_id];
            """;

        await using var command = new SqlCommand(query, connection);
        command.Parameters.Add("@lowIds", SqlDbType.NVarChar, -1).Value = JsonSerializer.Serialize(modification.LowIds);
        command.Parameters.Add("@limit", SqlDbType.Int).Value = PreviewRowsPerTable;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<ModificationPreviewRow>();
        long totalCount = 0;
        long insertCount = 0;

        while (await reader.ReadAsync(cancellationToken))
        {
            totalCount = reader.GetInt64(4);
            insertCount = reader.GetInt64(5);
            var exists = !reader.IsDBNull(3);
            var currentStatus = reader.IsDBNull(1) ? "NULL" : FormatBoolean(reader.GetValue(1));
            var currentTime = reader.IsDBNull(2) ? "NULL" : FormatValue(reader.GetValue(2));
            var proposedStatus = exists && !modification.UpdateStatus
                ? currentStatus
                : modification.Status == BooleanStatus.True ? "True" : "False";
            var proposedTime = exists && !modification.UpdateTime
                ? currentTime
                : modification.Time.ToString(System.Globalization.CultureInfo.InvariantCulture);

            rows.Add(new(
                qualifiedTable,
                exists ? "Update" : "Insert",
                $"low_id={reader.GetInt64(0)}",
                exists ? $"status={currentStatus}, time={currentTime}" : "—",
                $"status={proposedStatus}, time={proposedTime}"));
        }

        var summary = new ModificationPreviewSummary(
            qualifiedTable,
            insertCount,
            totalCount - insertCount,
            rows.Count);
        return new(summary, rows);
    }

    private static async Task<TablePreview> PreviewOverallAsync(
        SqlConnection connection,
        OverallModification modification,
        CancellationToken cancellationToken)
    {
        if (!modification.UpdateStatus && !modification.UpdateTime)
            throw new InvalidOperationException("Overall Modification must update status, time, or both.");

        var overallColumns = await GetWritableColumnsAsync(
            connection, null, "dbo.overall_status", cancellationToken);
        var requiredColumns = new[] { "mid_id", "high_id", "tb", "ta", "status", "time" };
        var missingColumns = requiredColumns
            .Where(required => !overallColumns.Contains(required, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        if (missingColumns.Length > 0)
        {
            throw new InvalidOperationException(
                $"Table dbo.overall_status is missing: {string.Join(", ", missingColumns)}.");
        }

        const string query = """
            WITH midIds AS
            (
                SELECT DISTINCT TRY_CONVERT(bigint, [value]) AS [mid_id]
                FROM OPENJSON(@midIds)
                WHERE TRY_CONVERT(bigint, [value]) IS NOT NULL
            ),
            highIds AS
            (
                SELECT DISTINCT TRY_CONVERT(bigint, [value]) AS [high_id]
                FROM OPENJSON(@highIds)
                WHERE TRY_CONVERT(bigint, [value]) IS NOT NULL
            ),
            selectedTb AS
            (
                SELECT DISTINCT TRY_CONVERT(bigint, [value]) AS [low_id]
                FROM OPENJSON(@selectedTb)
                WHERE TRY_CONVERT(bigint, [value]) IS NOT NULL
            ),
            typePairs AS
            (
                SELECT DISTINCT typeA.[low_id] AS [tb], typeA.[high_id] AS [ta]
                FROM [dbo].[type_a] AS typeA
                INNER JOIN [dbo].[type_b] AS typeB
                    ON typeB.[low_id] = typeA.[low_id]
                WHERE @useAll = 1
                   OR EXISTS (SELECT 1 FROM selectedTb WHERE selectedTb.[low_id] = typeA.[low_id])
            ),
            requested AS
            (
                SELECT midIds.[mid_id], highIds.[high_id], typePairs.[tb], typePairs.[ta]
                FROM midIds
                CROSS JOIN highIds
                CROSS JOIN typePairs
            )
            SELECT TOP (@limit)
                requested.[mid_id], requested.[high_id], requested.[tb], requested.[ta],
                currentRow.[status], currentRow.[time], currentRow.[exists],
                COUNT_BIG(*) OVER () AS [total_count],
                SUM(CONVERT(bigint, CASE WHEN currentRow.[exists] IS NULL THEN 1 ELSE 0 END)) OVER () AS [insert_count]
            FROM requested
            OUTER APPLY
            (
                SELECT TOP (1) target.[status], target.[time], 1 AS [exists]
                FROM [dbo].[overall_status] AS target
                WHERE target.[mid_id] = requested.[mid_id]
                  AND target.[high_id] = requested.[high_id]
                  AND target.[tb] = requested.[tb]
                  AND target.[ta] = requested.[ta]
            ) AS currentRow
            ORDER BY requested.[mid_id], requested.[high_id], requested.[tb], requested.[ta];
            """;

        await using var command = new SqlCommand(query, connection);
        command.Parameters.Add("@midIds", SqlDbType.NVarChar, -1).Value = JsonSerializer.Serialize(modification.MidIds);
        command.Parameters.Add("@highIds", SqlDbType.NVarChar, -1).Value = JsonSerializer.Serialize(modification.HighIds);
        command.Parameters.Add("@selectedTb", SqlDbType.NVarChar, -1).Value = JsonSerializer.Serialize(modification.SelectedTypeBLowIds);
        command.Parameters.Add("@useAll", SqlDbType.Bit).Value = modification.UseAllTypeB;
        command.Parameters.Add("@limit", SqlDbType.Int).Value = PreviewRowsPerTable;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<ModificationPreviewRow>();
        long totalCount = 0;
        long insertCount = 0;

        while (await reader.ReadAsync(cancellationToken))
        {
            totalCount = reader.GetInt64(7);
            insertCount = reader.GetInt64(8);
            if (totalCount > 100_000)
            {
                throw new InvalidOperationException(
                    $"Overall Modification would affect {totalCount:N0} rows; the limit is 100,000 per Apply.");
            }

            var exists = !reader.IsDBNull(6);
            var currentStatus = reader.IsDBNull(4) ? "NULL" : FormatValue(reader.GetValue(4));
            var currentTime = reader.IsDBNull(5) ? "NULL" : FormatValue(reader.GetValue(5));
            var proposedStatus = exists && !modification.UpdateStatus
                ? currentStatus
                : ((int)modification.Status).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var proposedTime = exists && !modification.UpdateTime
                ? currentTime
                : modification.Time.ToString(System.Globalization.CultureInfo.InvariantCulture);

            rows.Add(new(
                "dbo.overall_status",
                exists ? "Update" : "Insert",
                $"mid_id={ToInt64(reader.GetValue(0))}, high_id={ToInt64(reader.GetValue(1))}, tb={ToInt64(reader.GetValue(2))}, ta={ToInt64(reader.GetValue(3))}",
                exists ? $"status={currentStatus}, time={currentTime}" : "—",
                $"status={proposedStatus}, time={proposedTime}"));
        }

        if (totalCount == 0)
            throw new InvalidOperationException("The TB selection does not match any dbo.type_a rows.");

        var summary = new ModificationPreviewSummary(
            "dbo.overall_status",
            insertCount,
            totalCount - insertCount,
            rows.Count);
        return new(summary, rows);
    }

    private sealed record TablePreview(
        ModificationPreviewSummary Summary,
        IReadOnlyList<ModificationPreviewRow> Rows);
}
