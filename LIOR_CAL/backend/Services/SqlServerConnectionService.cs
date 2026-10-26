using MauiDbManager.Models;
using Microsoft.Data.SqlClient;

namespace MauiDbManager.Services;

public sealed class SqlServerConnectionService : IDatabaseConnectionService
{
    public async Task<ConnectionResult> TestConnectionAsync(
        string serverName,
        string userName,
        string password,
        bool trustServerCertificate,
        CancellationToken cancellationToken = default)
    {
        // A builder safely escapes values; never create connection strings by concatenation.
        var connectionString = new SqlConnectionStringBuilder
        {
            DataSource = serverName,
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

            const string databaseQuery = """
                SELECT [name]
                FROM sys.databases
                WHERE [state] = 0
                  AND HAS_DBACCESS([name]) = 1
                ORDER BY [name];
                """;

            var databases = new List<string>();
            await using var command = new SqlCommand(databaseQuery, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
                databases.Add(reader.GetString(0));

            return ConnectionResult.Success(
                $"Connected to {connection.DataSource} · {databases.Count} database(s) available.",
                databases);
        }
        catch (SqlException ex)
        {
            // SQL Server error numbers make common failures clearer without exposing credentials.
            var reason = ex.Number switch
            {
                18456 => "The server rejected the user name or password.",
                -2 => "The connection timed out. Check the server name, port, and network.",
                53 or 11001 => "The SQL Server could not be found. Check the server name and network.",
                4060 => "The login cannot open its default database.",
                _ => ex.Message
            };

            return ConnectionResult.Failure($"Connection failed: {reason} (SQL error {ex.Number})");
        }
        catch (OperationCanceledException)
        {
            return ConnectionResult.Failure("The connection attempt was cancelled.");
        }
        catch (Exception ex)
        {
            return ConnectionResult.Failure($"Connection failed: {ex.Message}");
        }
    }
}
