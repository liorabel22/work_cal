using MauiDbManager.Models;

namespace MauiDbManager.Services;

public interface IDatabaseConnectionService
{
    Task<ConnectionResult> TestConnectionAsync(
        string serverName,
        string userName,
        string password,
        bool trustServerCertificate,
        CancellationToken cancellationToken = default);
}
