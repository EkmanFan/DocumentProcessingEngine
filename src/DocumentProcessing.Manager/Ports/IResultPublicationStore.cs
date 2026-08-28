using DocumentProcessing.Manager.Publication;

namespace DocumentProcessing.Manager.Ports;

/// <summary>
/// Outbound port for durable at-least-once result delivery to named consumers.
/// </summary>
public interface IResultPublicationStore
{
    /// <summary>Claims the oldest unacknowledged result available to one consumer.</summary>
    ValueTask<ResultAvailableDelivery?> ClaimNextAsync(
        string consumerId,
        DateTimeOffset observedAtUtc,
        DateTimeOffset claimExpiresAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Acknowledges a claim after successful downstream persistence.</summary>
    ValueTask<bool> AcknowledgeAsync(
        string consumerId,
        string resultReference,
        Guid claimToken,
        DateTimeOffset acknowledgedAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Checks whether one consumer currently owns a readable claim.</summary>
    ValueTask<bool> OwnsClaimAsync(
        string consumerId,
        string resultReference,
        Guid claimToken,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken = default);
}
