using MauiDbManager.Models;

namespace MauiDbManager.Services;

public interface IDatabaseModificationService
{
    Task<ModificationPreviewResult> PreviewModificationsAsync(
        string serverName,
        string databaseName,
        string userName,
        string password,
        bool trustServerCertificate,
        IReadOnlyList<TableStatusModification> statusModifications,
        OverallModification? overallModification,
        AmplitudeModification? amplitudeModification,
        CancellationToken cancellationToken = default);

    Task<ModificationResult> ApplyModificationsAsync(
        string serverName,
        string databaseName,
        string userName,
        string password,
        bool trustServerCertificate,
        IReadOnlyList<TableStatusModification> statusModifications,
        OverallModification? overallModification,
        AmplitudeModification? amplitudeModification,
        CancellationToken cancellationToken = default);
}
