using System.Data;
using MauiDbManager.Models;
using Microsoft.Data.SqlClient;

namespace MauiDbManager.Services;

public sealed partial class SqlServerModificationService
{
    public async Task<ModificationResult> ApplyModificationsAsync(
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
            return ModificationResult.Failure("Enable at least one modification section.");

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
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

            try
            {
                foreach (var modification in statusModifications)
                    await ApplyOneAsync(connection, transaction, modification, cancellationToken);

                if (overallModification is not null)
                    await ApplyOverallAsync(connection, transaction, overallModification, cancellationToken);

                if (amplitudeModification is not null)
                    await ApplyAmplitudeAsync(connection, transaction, amplitudeModification, cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                var tableNames = statusModifications.Select(item => item.TableName).ToList();
                if (overallModification is not null)
                    tableNames.Add("dbo.overall_status");
                if (amplitudeModification is not null)
                    tableNames.Add("dbo.amplitude");

                return ModificationResult.Success(
                    $"Applied successfully to {string.Join(", ", tableNames)}.");
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
        }
        catch (SqlException ex)
        {
            return ModificationResult.Failure($"Apply failed: {ex.Message} (SQL error {ex.Number})");
        }
        catch (Exception ex)
        {
            return ModificationResult.Failure($"Apply failed: {ex.Message}");
        }
    }

    private static async Task ApplyOneAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        TableStatusModification modification,
        CancellationToken cancellationToken)
    {
        if (!AllowedTables.TryGetValue(modification.TableName, out var qualifiedTable))
            throw new InvalidOperationException($"Table {modification.TableName} is not an allowed modification target.");

        var columns = await GetWritableColumnsAsync(
            connection, transaction, qualifiedTable, cancellationToken);

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

        var commandText = $"""
            IF EXISTS (SELECT 1 FROM {quotedTable} WHERE {key1} = 0 AND {key2} = 0)
                UPDATE {quotedTable}
                SET {status} = @status
                WHERE {key1} = 0 AND {key2} = 0;
            ELSE
                INSERT INTO {quotedTable} ({key1}, {key2}, {status})
                VALUES (0, 0, @status);
            """;

        await using var command = new SqlCommand(commandText, connection, transaction);
        command.Parameters.Add("@status", SqlDbType.Bit).Value = modification.Status == BooleanStatus.True;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }


    private static async Task ApplyAmplitudeAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        AmplitudeModification modification,
        CancellationToken cancellationToken)
    {
        const string qualifiedTable = "dbo.amplitude";
        var columns = await GetWritableColumnsAsync(
            connection, transaction, qualifiedTable, cancellationToken);

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
        var zero = QuoteIdentifier(zeroColumn);
        var status = QuoteIdentifier(statusColumn);
        var time = QuoteIdentifier(timeColumn);
        const string table = "[dbo].[amplitude]";
        var updateClause = (modification.UpdateStatus, modification.UpdateTime) switch
        {
            (true, true) => $"{status} = @status, {time} = @time",
            (true, false) => $"{status} = @status",
            (false, true) => $"{time} = @time",
            _ => throw new InvalidOperationException(
                "Amplitude Modification must update status, time, or both.")
        };

        var commandText = $"""
            UPDATE {table}
            SET {updateClause}
            WHERE {lowId} = @lowId;

            IF @@ROWCOUNT = 0
                INSERT INTO {table} ({lowId}, {zero}, {status}, {time})
                VALUES (@lowId, 0, @status, @time);
            """;

        await using var command = new SqlCommand(commandText, connection, transaction);
        var lowIdParameter = command.Parameters.Add("@lowId", SqlDbType.BigInt);
        command.Parameters.Add("@status", SqlDbType.Bit).Value = modification.Status == BooleanStatus.True;
        command.Parameters.Add("@time", SqlDbType.BigInt).Value = modification.Time;

        foreach (var value in modification.LowIds)
        {
            lowIdParameter.Value = value;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task ApplyOverallAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        OverallModification modification,
        CancellationToken cancellationToken)
    {
        if (!modification.UpdateStatus && !modification.UpdateTime)
            throw new InvalidOperationException("Overall Modification must update status, time, or both.");

        var overallColumns = await GetWritableColumnsAsync(
            connection, transaction, "dbo.overall_status", cancellationToken);
        var requiredColumns = new[] { "mid_id", "high_id", "tb", "ta", "status", "time" };
        var missingColumns = requiredColumns
            .Where(required => !overallColumns.Contains(required, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        if (missingColumns.Length > 0)
        {
            throw new InvalidOperationException(
                $"Table dbo.overall_status is missing: {string.Join(", ", missingColumns)}.");
        }

        var typeAPairs = await LoadTypeAPairsAsync(connection, transaction, cancellationToken);
        if (!modification.UseAllTypeB)
        {
            typeAPairs = typeAPairs
                .Where(pair => modification.SelectedTypeBLowIds.Contains(pair.Tb))
                .ToArray();
        }

        if (typeAPairs.Count == 0)
            throw new InvalidOperationException("The TB selection does not match any dbo.type_a rows.");

        const int maximumRowsPerApply = 100_000;
        var rowCount = (decimal)modification.MidIds.Count * modification.HighIds.Count * typeAPairs.Count;
        if (rowCount > maximumRowsPerApply)
        {
            throw new InvalidOperationException(
                $"Overall Modification would affect {rowCount:N0} rows; the limit is {maximumRowsPerApply:N0} per Apply.");
        }

        var updateClause = (modification.UpdateStatus, modification.UpdateTime) switch
        {
            (true, true) => "[status] = @status, [time] = @time",
            (true, false) => "[status] = @status",
            (false, true) => "[time] = @time",
            _ => throw new InvalidOperationException("Overall Modification has no selected output field.")
        };

        var commandText = $"""
            UPDATE [dbo].[overall_status]
            SET {updateClause}
            WHERE [mid_id] = @midId
              AND [high_id] = @highId
              AND [tb] = @tb
              AND [ta] = @ta;

            IF @@ROWCOUNT = 0
                INSERT INTO [dbo].[overall_status]
                    ([mid_id], [high_id], [tb], [ta], [status], [time])
                VALUES
                    (@midId, @highId, @tb, @ta, @status, @time);
            """;

        await using var command = new SqlCommand(commandText, connection, transaction);
        var midIdParameter = command.Parameters.Add("@midId", SqlDbType.BigInt);
        var highIdParameter = command.Parameters.Add("@highId", SqlDbType.BigInt);
        var tbParameter = command.Parameters.Add("@tb", SqlDbType.BigInt);
        var taParameter = command.Parameters.Add("@ta", SqlDbType.BigInt);
        command.Parameters.Add("@status", SqlDbType.Int).Value = (int)modification.Status;
        command.Parameters.Add("@time", SqlDbType.BigInt).Value = modification.Time;

        foreach (var midId in modification.MidIds)
        foreach (var highId in modification.HighIds)
        foreach (var pair in typeAPairs)
        {
            midIdParameter.Value = midId;
            highIdParameter.Value = highId;
            tbParameter.Value = pair.Tb;
            taParameter.Value = pair.Ta;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
