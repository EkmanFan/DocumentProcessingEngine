using DocumentProcessing.Manager.Custody;

namespace DocumentProcessing.Manager.Ports;

/// <summary>Durable port for destructive administrative processing-unit purges.</summary>
public interface IProcessingUnitCustodyPurgeStore
{
    ValueTask<ProcessingUnitCustodyPurge> BeginPurgeAsync(
        PurgeTerminalProcessingUnitCommand command,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<ProcessingUnitCustodyPurge>> GetPendingPurgesAsync(
        CancellationToken cancellationToken = default);

    ValueTask CompletePurgeAsync(
        Guid purgeId,
        CancellationToken cancellationToken = default);
}
