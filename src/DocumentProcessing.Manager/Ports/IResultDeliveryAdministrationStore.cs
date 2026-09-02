using DocumentProcessing.Manager.Publication;
using DocumentProcessing.Manager.Queue;

namespace DocumentProcessing.Manager.Ports;

/// <summary>Administrative port for explicitly replaying durable downstream delivery.</summary>
public interface IResultDeliveryAdministrationStore
{
    /// <summary>
    /// Clears claims and acknowledgements for every published result in one submission.
    /// </summary>
    ValueTask<ResultDeliveryReplay?> ReplaySubmissionAsync(
        string consumerId,
        DocumentSubmissionId submissionId,
        DateTimeOffset requestedAtUtc,
        CancellationToken cancellationToken = default);
}
