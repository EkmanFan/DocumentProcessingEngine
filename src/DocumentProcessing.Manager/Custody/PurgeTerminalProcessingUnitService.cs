using DocumentProcessing.Manager.Ports;

namespace DocumentProcessing.Manager.Custody;

/// <summary>
/// Coordinates destructive metadata and filesystem cleanup while retaining a
/// durable cleanup job until every unshared artifact has been deleted.
/// </summary>
public sealed class PurgeTerminalProcessingUnitService
{
    private readonly IProcessingUnitCustodyPurgeStore _purgeStore;
    private readonly IProcessingResultArtifactPurger _resultPurger;
    private readonly ISourceArtifactPurger _sourcePurger;
    private readonly IProcessingVisualAssetPurger _visualPurger;

    public PurgeTerminalProcessingUnitService(
        IProcessingUnitCustodyPurgeStore purgeStore,
        IProcessingResultArtifactPurger resultPurger,
        ISourceArtifactPurger sourcePurger,
        IProcessingVisualAssetPurger visualPurger)
    {
        _purgeStore = purgeStore ?? throw new ArgumentNullException(nameof(purgeStore));
        _resultPurger = resultPurger ?? throw new ArgumentNullException(nameof(resultPurger));
        _sourcePurger = sourcePurger ?? throw new ArgumentNullException(nameof(sourcePurger));
        _visualPurger = visualPurger ?? throw new ArgumentNullException(nameof(visualPurger));
    }

    public async ValueTask PurgeAsync(
        PurgeTerminalProcessingUnitCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var purge = await _purgeStore
            .BeginPurgeAsync(command, cancellationToken)
            .ConfigureAwait(false);

        await CompleteAsync(purge, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask CompletePendingPurgesAsync(
        CancellationToken cancellationToken = default)
    {
        var pending = await _purgeStore
            .GetPendingPurgesAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var purge in pending)
        {
            await CompleteAsync(purge, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask CompleteAsync(
        ProcessingUnitCustodyPurge purge,
        CancellationToken cancellationToken)
    {
        if (purge.PublicationDirectory is not null)
        {
            await _visualPurger
                .DeletePublicationAsync(purge.UnitId, purge.PublicationDirectory, cancellationToken)
                .ConfigureAwait(false);
        }

        if (purge.ResultArtifactDigest is not null)
        {
            await _resultPurger
                .DeleteAsync(purge.ResultArtifactDigest.Value, cancellationToken)
                .ConfigureAwait(false);
        }

        if (purge.SourceArtifactDigest is not null)
        {
            await _sourcePurger
                .DeleteAsync(purge.SourceArtifactDigest.Value, cancellationToken)
                .ConfigureAwait(false);
        }

        await _purgeStore
            .CompletePurgeAsync(purge.PurgeId, cancellationToken)
            .ConfigureAwait(false);
    }
}
