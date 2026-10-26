using MauiDbManager.Models;
using Microsoft.Data.SqlClient;

namespace MauiDbManager.Services;

public sealed class SqlServerLookupService : IDatabaseLookupService
{
    public async Task<TypeBLookupResult> LoadTypeBOptionsAsync(
        string serverName,
        string databaseName,
        string userName,
        string password,
        bool trustServerCertificate,
        CancellationToken cancellationToken = default)
    {
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
            ApplicationName = "LIOR_CAL"
        }.ConnectionString;

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            const string tableQuery = """
                SELECT [schema].[name], [table].[name]
                FROM [sys].[tables] AS [table]
                INNER JOIN [sys].[schemas] AS [schema]
                    ON [schema].[schema_id] = [table].[schema_id]
                WHERE [schema].[name] = N'dbo';
                """;
            await using var tableCommand = new SqlCommand(tableQuery, connection);
            await using var tableReader = await tableCommand.ExecuteReaderAsync(cancellationToken);
            var availableTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            while (await tableReader.ReadAsync(cancellationToken))
                availableTables.Add($"{tableReader.GetString(0)}.{tableReader.GetString(1)}");

            await tableReader.CloseAsync();

            if (!availableTables.Contains("dbo.type_b"))
            {
                return TypeBLookupResult.Success(
                    Array.Empty<TypeBLookupItem>(),
                    availableTables,
                    "dbo.type_b is missing. Overall Modification is unavailable.");
            }

            const string typeBQuery = """
                SELECT [low_id], [id_text]
                FROM [dbo].[type_b]
                ORDER BY [id_text], [low_id];
                """;
            await using var command = new SqlCommand(typeBQuery, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var items = new List<TypeBLookupItem>();

            while (await reader.ReadAsync(cancellationToken))
                items.Add(new(
                    Convert.ToInt64(reader.GetValue(0), System.Globalization.CultureInfo.InvariantCulture),
                    reader.GetString(1)));

            return TypeBLookupResult.Success(items, availableTables);
        }
        catch (Exception ex)
        {
            return TypeBLookupResult.Failure($"Could not inspect the selected database: {ex.Message}");
        }
    }
}
