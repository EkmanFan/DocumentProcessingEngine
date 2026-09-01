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

    ValueTask RetryFailedProcessingUnitAsync(
        Guid unitId,
        long expectedVersion,
        CancellationToken cancellationToken = default);

    ValueTask RemovePendingProcessingUnitAsync(
        Guid unitId,
        long expectedVersion,
        CancellationToken cancellationToken = default);

    ValueTask ClearPendingQueueAsync(
        long expectedVersion,
        CancellationToken cancellationToken = default);

    ValueTask HideTerminalProcessingUnitAsync(
        Guid unitId,
        long expectedVersion,
        CancellationToken cancellationToken = default);

    ValueTask PrepareProcessingUnitSplitAsync(
        Guid unitId,
        long expectedVersion,
        CancellationToken cancellationToken = default);

    ValueTask<ManagerSplitPreviewContract> GetSplitPreviewAsync(
        Guid unitId,
        CancellationToken cancellationToken = default);

    ValueTask<byte[]> GetSplitPreviewPageAsync(
        Guid unitId,
        int physicalPageNumber,
        CancellationToken cancellationToken = default);

    ValueTask<ManagerSplitPendingUnitResult> SplitPendingUnitAsync(
        Guid unitId,
        long expectedVersion,
        IReadOnlyList<ManagerPageRangeRequest> ranges,
        bool releaseAfterSplit,
        CancellationToken cancellationToken = default);
}
