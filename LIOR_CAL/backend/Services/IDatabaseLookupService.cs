using MauiDbManager.Models;

namespace MauiDbManager.Services;

public interface IDatabaseLookupService
{
    Task<TypeBLookupResult> LoadTypeBOptionsAsync(
        string serverName,
        string databaseName,
        string userName,
        string password,
        bool trustServerCertificate,
        CancellationToken cancellationToken = default);
}
