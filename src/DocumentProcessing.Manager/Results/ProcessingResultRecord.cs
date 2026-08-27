using DocumentProcessing.Manager.Queue;

namespace DocumentProcessing.Manager.Results;

/// <summary>
/// Immutable durable registry entry for one completed processing-unit result.
/// </summary>
public sealed record ProcessingResultRecord
{
    #region Properties

    /// <summary>
    /// Gets the opaque durable reference returned to Manager consumers.
    /// </summary>
    public string ResultReference { get; }

    /// <summary>
    /// Gets the processing unit that produced this result.
    /// </summary>
    public ProcessingUnitId UnitId { get; }

    /// <summary>
    /// Gets the source submission processed by the unit.
    /// </summary>
    public DocumentSubmissionId SubmissionId { get; }

    /// <summary>
    /// Gets the exact durable result-payload descriptor.
    /// </summary>
    public ProcessingResultArtifact Artifact { get; }

    /// <summary>
    /// Gets the normalized result media type.
    /// </summary>
    public string MediaType { get; }

    /// <summary>
    /// Gets the result-contract schema identifier.
    /// </summary>
    public string SchemaVersion { get; }

    /// <summary>
    /// Gets the instant at which result production completed.
    /// </summary>
    public DateTimeOffset ProducedAtUtc { get; }

    #endregion

    #region ctor

    /// <summary>
    /// Creates one immutable processing-result registry entry.
    /// </summary>
    public ProcessingResultRecord(
        string resultReference,
        ProcessingUnitId unitId,
        DocumentSubmissionId submissionId,
        ProcessingResultArtifact artifact,
        string mediaType,
        string schemaVersion,
        DateTimeOffset producedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(
                resultReference))
        {
            throw new ArgumentException(
                "Processing-result reference cannot be empty.",
                nameof(resultReference));
        }

        if (unitId.Value ==
            Guid.Empty)
        {
            throw new ArgumentException(
                "Processing-result unit identifier cannot be empty.",
                nameof(unitId));
        }

        if (submissionId.Value ==
            Guid.Empty)
        {
            throw new ArgumentException(
                "Processing-result submission identifier cannot be empty.",
                nameof(submissionId));
        }

        if (string.IsNullOrWhiteSpace(
                mediaType))
        {
            throw new ArgumentException(
                "Processing-result media type cannot be empty.",
                nameof(mediaType));
        }

        if (string.IsNullOrWhiteSpace(
                schemaVersion))
        {
            throw new ArgumentException(
                "Processing-result schema version cannot be empty.",
                nameof(schemaVersion));
        }

        ResultReference =
            resultReference.Trim();

        UnitId =
            unitId;

        SubmissionId =
            submissionId;

        Artifact =
            artifact ??
            throw new ArgumentNullException(
                nameof(artifact));

        MediaType =
            mediaType.Trim()
                .ToLowerInvariant();

        SchemaVersion =
            schemaVersion.Trim();

        ProducedAtUtc =
            producedAtUtc.ToUniversalTime();
    }

    #endregion
}
