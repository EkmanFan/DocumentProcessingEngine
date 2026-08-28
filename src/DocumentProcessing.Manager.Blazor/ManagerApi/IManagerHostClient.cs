using DocumentProcessing.Manager.Blazor.Components.Workshop;
using DocumentProcessing.Manager.Blazor.Workshop;

namespace DocumentProcessing.Manager.Blazor.ManagerApi;

internal interface IManagerHostClient
{
    ValueTask<ManagerWorkshopSnapshot> GetWorkshopAsync(
        CancellationToken cancellationToken = default);

    ValueTask<ManagerWorkshopSettings> GetSettingsAsync(
        CancellationToken cancellationToken = default);

    ValueTask<ManagerWorkshopSettings> UpdateSettingsAsync(
        long expectedVersion,
        ManagerDocumentSubmissionBehavior submissionBehavior,
        string? visualDestinationRoot,
        int completedRetentionDays,
        CancellationToken cancellationToken = default);

    ValueTask<ManagerArchivePage> SearchArchiveAsync(
        ManagerArchiveQuery query,
        CancellationToken cancellationToken = default);

    ValueTask ExecuteControlAsync(
        ManagerControlAction action,
        CancellationToken cancellationToken = default);

    ValueTask ReorderQueueAsync(
        long expectedVersion,
        IReadOnlyList<Guid> orderedPendingUnitIds,
        CancellationToken cancellationToken = default);

    ValueTask ReleaseProcessingUnitAsync(
        Guid unitId,
        long expectedVersion,
        CancellationToken cancellationToken = default);
}
