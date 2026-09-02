using DocumentProcessing.Manager.Custody;
using DocumentProcessing.Manager.Queue;

namespace DocumentProcessing.Manager.Publication;

/// <summary>
/// Immutable versioned declaration of every processing unit expected for one
/// submitted source.
/// </summary>
public sealed record SubmissionPublicationManifest
{
    #region Properties

    public DocumentSubmissionId SubmissionId { get; }

    public int Revision { get; }

    public Sha256Digest SourceDigest { get; }

    public string OriginalFileName { get; }

    public DateTimeOffset FinalizedAtUtc { get; }

    public IReadOnlyList<ExpectedProcessingUnit> ExpectedUnits { get; }

    #endregion

    #region ctor

    public SubmissionPublicationManifest(
        DocumentSubmissionId submissionId,
        int revision,
        Sha256Digest sourceDigest,
        string originalFileName,
        DateTimeOffset finalizedAtUtc,
        IEnumerable<ExpectedProcessingUnit> expectedUnits)
    {
        if (submissionId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Submission identifier cannot be empty.",
                nameof(submissionId));
        }

        if (revision <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(revision));
        }

        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            throw new ArgumentException(
                "Original filename cannot be empty.",
                nameof(originalFileName));
        }

        ArgumentNullException.ThrowIfNull(expectedUnits);

        var units = expectedUnits.ToArray();

        if (units.Length == 0 ||
            units.Select(unit => unit.ProcessingUnitId).Distinct().Count() != units.Length ||
            !units.Select(unit => unit.Ordinal).SequenceEqual(Enumerable.Range(1, units.Length)))
        {
            throw new ArgumentException(
                "Expected processing units must be distinct and use contiguous one-based ordinals.",
                nameof(expectedUnits));
        }

        SubmissionId = submissionId;
        Revision = revision;
        SourceDigest = sourceDigest;
        OriginalFileName = originalFileName.Trim();
        FinalizedAtUtc = finalizedAtUtc.ToUniversalTime();
        ExpectedUnits = units;
    }

    #endregion
}

/// <summary>Describes one ordered processing unit in a submission manifest.</summary>
public sealed record ExpectedProcessingUnit
{
    public ProcessingUnitId ProcessingUnitId { get; }

    public int Ordinal { get; }

    public ProcessingUnitScope Scope { get; }

    public ExpectedProcessingUnit(
        ProcessingUnitId processingUnitId,
        int ordinal,
        ProcessingUnitScope scope)
    {
        if (processingUnitId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Processing-unit identifier cannot be empty.",
                nameof(processingUnitId));
        }

        if (ordinal <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        }

        ProcessingUnitId = processingUnitId;
        Ordinal = ordinal;
        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
    }
}
