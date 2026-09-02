using DocumentProcessing.Manager.Queue;

namespace DocumentProcessing.Manager.Publication;

/// <summary>Audited reset of all result deliveries for one submission and consumer.</summary>
public sealed record ResultDeliveryReplay(
    Guid ReplayId,
    DocumentSubmissionId SubmissionId,
    string ConsumerId,
    int ResultCount,
    DateTimeOffset RequestedAtUtc);
