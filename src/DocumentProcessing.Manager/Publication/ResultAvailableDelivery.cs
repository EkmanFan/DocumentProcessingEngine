using DocumentProcessing.Manager.Custody;
using DocumentProcessing.Manager.Queue;

namespace DocumentProcessing.Manager.Publication;

/// <summary>
/// Describes one durably claimable processing result for an external consumer.
/// </summary>
public sealed record ResultAvailableDelivery
{
    #region Properties

    /// <summary>Gets the opaque result reference.</summary>
    public string ResultReference { get; }

    /// <summary>Gets the submission that owns the result.</summary>
    public DocumentSubmissionId SubmissionId { get; }

    /// <summary>Gets the processing unit that produced the result.</summary>
    public ProcessingUnitId ProcessingUnitId { get; }

    /// <summary>Gets the immutable processing-unit scope.</summary>
    public ProcessingUnitScope Scope { get; }

    /// <summary>Gets the result-contract schema identifier.</summary>
    public string SchemaVersion { get; }

    /// <summary>Gets the normalized payload media type.</summary>
    public string MediaType { get; }

    /// <summary>Gets the exact payload byte length.</summary>
    public long ByteLength { get; }

    /// <summary>Gets the exact payload SHA-256 digest.</summary>
    public Sha256Digest Digest { get; }

    /// <summary>Gets the instant at which the result became available.</summary>
    public DateTimeOffset AvailableAtUtc { get; }

    /// <summary>Gets the opaque token required to acknowledge this claim.</summary>
    public Guid ClaimToken { get; }

    /// <summary>Gets the instant at which this claim expires.</summary>
    public DateTimeOffset ClaimExpiresAtUtc { get; }

    /// <summary>Gets the finalized manifest for the owning submission.</summary>
    public SubmissionPublicationManifest SubmissionManifest { get; }

    #endregion

    #region ctor

    /// <summary>Creates one claimed result delivery.</summary>
    public ResultAvailableDelivery(
        string resultReference,
        DocumentSubmissionId submissionId,
        ProcessingUnitId processingUnitId,
        ProcessingUnitScope scope,
        string schemaVersion,
        string mediaType,
        long byteLength,
        Sha256Digest digest,
        DateTimeOffset availableAtUtc,
        Guid claimToken,
        DateTimeOffset claimExpiresAtUtc,
        SubmissionPublicationManifest submissionManifest)
    {
        if (string.IsNullOrWhiteSpace(resultReference))
        {
            throw new ArgumentException(
                "Result reference cannot be empty.",
                nameof(resultReference));
        }

        if (string.IsNullOrWhiteSpace(schemaVersion))
        {
            throw new ArgumentException(
                "Schema version cannot be empty.",
                nameof(schemaVersion));
        }

        if (string.IsNullOrWhiteSpace(mediaType))
        {
            throw new ArgumentException(
                "Media type cannot be empty.",
                nameof(mediaType));
        }

        if (byteLength <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(byteLength));
        }

        if (claimToken == Guid.Empty)
        {
            throw new ArgumentException(
                "Claim token cannot be empty.",
                nameof(claimToken));
        }

        ResultReference = resultReference.Trim();
        SubmissionId = submissionId;
        ProcessingUnitId = processingUnitId;
        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
        SchemaVersion = schemaVersion.Trim();
        MediaType = mediaType.Trim().ToLowerInvariant();
        ByteLength = byteLength;
        Digest = digest;
        AvailableAtUtc = availableAtUtc.ToUniversalTime();
        ClaimToken = claimToken;
        ClaimExpiresAtUtc = claimExpiresAtUtc.ToUniversalTime();
        SubmissionManifest = submissionManifest ??
            throw new ArgumentNullException(nameof(submissionManifest));

        if (SubmissionManifest.SubmissionId != SubmissionId ||
            SubmissionManifest.ExpectedUnits.All(
                unit => unit.ProcessingUnitId != ProcessingUnitId))
        {
            throw new ArgumentException(
                "The submission manifest must own the delivered processing unit.",
                nameof(submissionManifest));
        }
    }

    #endregion
}
